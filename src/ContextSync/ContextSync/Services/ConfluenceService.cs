using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ContextSync.Abstractions;
using ContextSync.Infrastructure.Settings;
using ContextSync.dal.Models;
using Serilog;

namespace ContextSync.Services;

public class ConfluenceService : IConfluenceService
{
    private readonly ConfluenceSettings _settings;
    private readonly HttpClient _httpClient;

    public ConfluenceService(ConfluenceSettings settings, HttpClient httpClient)
    {
        _settings = settings;
        _httpClient = httpClient;
        ConfigureClient();
    }

    private void ConfigureClient()
    {
        if (string.IsNullOrEmpty(_settings.Url))
            return;

        _httpClient.BaseAddress = new Uri(_settings.Url.TrimEnd('/') + "/");
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

        var auth = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_settings.Username}:{_settings.ApiToken}"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", auth);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_settings.Url))
            return false;

        try
        {
            var response = await _httpClient.GetAsync("rest/api/user/current", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Confluence connection test failed");
            return false;
        }
    }

    public async Task<WikiPageResult> CreatePageAsync(SourceDocument doc, CancellationToken cancellationToken = default)
    {
        var spaceKey = doc.WikiSpaceKey ?? _settings.DefaultSpaceKey;

        long? parentId = null;
        if (!string.IsNullOrEmpty(doc.WikiParentTitle) || !string.IsNullOrEmpty(_settings.DefaultParentTitle))
        {
            var parentTitle = doc.WikiParentTitle ?? _settings.DefaultParentTitle;
            var parent = await FindPageAsync(spaceKey, parentTitle, cancellationToken);
            parentId = parent?.PageId;
        }

        var payload = new
        {
            type = "page",
            title = doc.Title,
            space = new { key = spaceKey },
            ancestors = parentId.HasValue ? new[] { new { id = parentId.Value.ToString() } } : null,
            body = new
            {
                storage = new
                {
                    value = ConvertToStorageFormat(doc.Content),
                    representation = "storage"
                }
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        try
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("rest/api/content", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var created = JsonSerializer.Deserialize<JsonElement>(responseJson);
                var id = created.GetProperty("id").GetString() ?? "";
                var title = created.GetProperty("title").GetString() ?? doc.Title;

                Log.Information("Confluence page created: {Title} (id={Id})", title, id);
                return new WikiPageResult
                {
                    Title = title,
                    SpaceKey = spaceKey,
                    PageId = long.TryParse(id, out var pid) ? pid : null,
                    Url = $"{_settings.Url}/pages/viewpage.action?pageId={id}",
                    Success = true
                };
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                Log.Error("Confluence create page failed: {Error}", error);
                return new WikiPageResult { Success = false, Error = error };
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Confluence create page error for {Title}", doc.Title);
            return new WikiPageResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<WikiPageResult> UpdatePageAsync(long pageId, SourceDocument doc, CancellationToken cancellationToken = default)
    {
        try
        {
            var getResponse = await _httpClient.GetAsync($"rest/api/content/{pageId}?expand=version", cancellationToken);
            if (!getResponse.IsSuccessStatusCode)
            {
                return new WikiPageResult { Success = false, Error = "Page not found" };
            }

            var pageJson = await getResponse.Content.ReadAsStringAsync(cancellationToken);
            var page = JsonSerializer.Deserialize<JsonElement>(pageJson);
            var version = page.GetProperty("version").GetProperty("number").GetInt32();
            var title = page.GetProperty("title").GetString() ?? doc.Title;

            var payload = new
            {
                id = pageId.ToString(),
                type = "page",
                title = doc.Title,
                body = new
                {
                    storage = new
                    {
                        value = ConvertToStorageFormat(doc.Content),
                        representation = "storage"
                    }
                },
                version = new { number = version + 1 }
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync($"rest/api/content/{pageId}", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                Log.Information("Confluence page updated: {Title} (id={Id})", doc.Title, pageId);
                return new WikiPageResult
                {
                    Title = doc.Title,
                    PageId = pageId,
                    Url = $"{_settings.Url}/pages/viewpage.action?pageId={pageId}",
                    Success = true
                };
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return new WikiPageResult { Success = false, Error = error };
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Confluence update page error for id={Id}", pageId);
            return new WikiPageResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<WikiPageResult?> FindPageAsync(string spaceKey, string title, CancellationToken cancellationToken = default)
    {
        try
        {
            var encodedTitle = Uri.EscapeDataString(title);
            var response = await _httpClient.GetAsync(
                $"rest/api/content?spaceKey={spaceKey}&title={encodedTitle}&expand=version", cancellationToken);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            if (data.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
            {
                var first = results[0];
                var id = first.GetProperty("id").GetString() ?? "";
                return new WikiPageResult
                {
                    Title = title,
                    SpaceKey = spaceKey,
                    PageId = long.TryParse(id, out var pid) ? pid : null,
                    Success = true
                };
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Confluence find page failed: {Title}", title);
        }

        return null;
    }

    public async Task<List<WikiPageResult>> CreatePagesAsync(IEnumerable<SourceDocument> docs, CancellationToken cancellationToken = default)
    {
        var results = new List<WikiPageResult>();
        foreach (var doc in docs)
        {
            var result = await CreatePageAsync(doc, cancellationToken);
            results.Add(result);
        }
        return results;
    }

    private static string ConvertToStorageFormat(string content)
    {
        var escaped = content
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");

        var lines = escaped.Split('\n');
        var sb = new StringBuilder();

        bool inList = false;
        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r').Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                if (inList) { sb.Append("</ul>"); inList = false; }
                continue;
            }

            if (trimmed.StartsWith("# "))
            {
                if (!inList) { sb.Append("<ul>"); inList = true; }
                sb.Append($"<li>{trimmed.Substring(2)}</li>");
            }
            else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
            {
                if (!inList) { sb.Append("<ul>"); inList = true; }
                sb.Append($"<li>{trimmed.Substring(2)}</li>");
            }
            else
            {
                if (inList) { sb.Append("</ul>"); inList = false; }
                sb.Append($"<p>{trimmed}</p>");
            }
        }

        if (inList) sb.Append("</ul>");

        return sb.ToString();
    }
}
