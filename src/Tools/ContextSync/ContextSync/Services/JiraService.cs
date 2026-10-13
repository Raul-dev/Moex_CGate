using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ContextSync.Abstractions;
using ContextSync.Infrastructure.Settings;
using ContextSync.Scenarios;
using ContextSync.dal.Models;
using Serilog;

namespace ContextSync.Services;

public class JiraService : IJiraService
{
    private readonly JiraSettings _settings;
    private readonly HttpClient _httpClient;

    public JiraService(JiraSettings settings, HttpClient httpClient)
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
            var response = await _httpClient.GetAsync("rest/api/2/myself", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Jira connection test failed");
            return false;
        }
    }

    public async Task<JiraIssueResult> CreateIssueAsync(SourceDocument doc, CancellationToken cancellationToken = default)
    {
        var projectKey = doc.JiraProjectKey ?? _settings.DefaultProjectKey;
        var issueType = doc.JiraIssueType ?? _settings.DefaultIssueType;
        var priority = doc.JiraPriority ?? _settings.DefaultPriority;

        var payload = new
        {
            fields = new
            {
                project = new { key = projectKey },
                summary = doc.Title,
                description = doc.Content,
                issuetype = new { name = issueType },
                priority = new { name = priority },
                labels = doc.JiraLabels.Count > 0 ? doc.JiraLabels : new List<string>(),
                assignee = !string.IsNullOrEmpty(doc.JiraAssignee) ? new { name = doc.JiraAssignee } : null,
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
            var response = await _httpClient.PostAsync("rest/api/2/issue", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var created = JsonSerializer.Deserialize<JsonElement>(responseJson);
                var key = created.GetProperty("key").GetString() ?? "";
                var id = created.GetProperty("id").GetString() ?? "";

                Log.Information("Jira issue created: {Key}", key);
                return new JiraIssueResult
                {
                    Key = key,
                    Id = id,
                    Url = $"{_settings.Url}/browse/{key}",
                    Success = true
                };
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                Log.Error("Jira create issue failed: {Error}", error);
                return new JiraIssueResult { Success = false, Error = error };
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Jira create issue error for {Title}", doc.Title);
            return new JiraIssueResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<JiraIssueResult> UpdateIssueAsync(string issueKey, SourceDocument doc, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            fields = new
            {
                summary = doc.Title,
                description = doc.Content,
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
            var response = await _httpClient.PutAsync($"rest/api/2/issue/{issueKey}", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                Log.Information("Jira issue updated: {Key}", issueKey);
                return new JiraIssueResult
                {
                    Key = issueKey,
                    Url = $"{_settings.Url}/browse/{issueKey}",
                    Success = true
                };
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return new JiraIssueResult { Key = issueKey, Success = false, Error = error };
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Jira update issue error for {Key}", issueKey);
            return new JiraIssueResult { Key = issueKey, Success = false, Error = ex.Message };
        }
    }

    public async Task<bool> IssueExistsAsync(string issueKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"rest/api/2/issue/{issueKey}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<JiraIssueResult>> CreateIssuesAsync(IEnumerable<SourceDocument> docs, CancellationToken cancellationToken = default)
    {
        var results = new List<JiraIssueResult>();
        var docList = docs.ToList();
        var batchSize = _settings.BatchSize > 0 ? _settings.BatchSize : docList.Count;

        for (int i = 0; i < docList.Count; i += batchSize)
        {
            var batch = docList.Skip(i).Take(batchSize);
            var tasks = batch.Select(d => CreateIssueAsync(d, cancellationToken));
            var batchResults = await Task.WhenAll(tasks);
            results.AddRange(batchResults);
        }

        return results;
    }

    public async Task<List<JiraSearchIssue>> SearchAsync(string jql, int maxResults = 200, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["jql"] = jql,
            ["maxResults"] = maxResults,
            ["fields"] = new List<string> { "key", "summary", "status" }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("rest/api/2/search", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = CompactError(response, body);
            Log.Error("Jira search failed: {Error}", message);
            throw new InvalidOperationException($"Jira search failed: {message}");
        }

        var document = JsonDocument.Parse(body);
        var issues = new List<JiraSearchIssue>();

        if (document.RootElement.TryGetProperty("issues", out var issuesElement) && issuesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var issue in issuesElement.EnumerateArray())
            {
                var key = issue.TryGetProperty("key", out var keyElement) ? keyElement.GetString() ?? "" : "";
                var summary = "";
                var status = "";
                if (issue.TryGetProperty("fields", out var fields))
                {
                    if (fields.TryGetProperty("summary", out var summaryElement) && summaryElement.ValueKind == JsonValueKind.String)
                        summary = summaryElement.GetString() ?? "";
                    if (fields.TryGetProperty("status", out var statusElement) && statusElement.TryGetProperty("name", out var statusName))
                        status = statusName.GetString() ?? "";
                }
                issues.Add(new JiraSearchIssue(key, summary, status));
            }
        }

        Log.Information("Jira search found {Count} issues for JQL: {Jql}", issues.Count, jql);
        return issues;
    }

    public async Task<List<JiraWorklog>> GetWorklogsAsync(string issueKey, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"rest/api/2/issue/{Uri.EscapeDataString(issueKey)}/worklog", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = CompactError(response, body);
            Log.Error("Jira get worklogs failed for {IssueKey}: {Error}", issueKey, message);
            throw new InvalidOperationException($"Jira get worklogs failed for {issueKey}: {message}");
        }

        var document = JsonDocument.Parse(body);
        var worklogs = new List<JiraWorklog>();

        if (document.RootElement.TryGetProperty("worklogs", out var worklogsElement) && worklogsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var w in worklogsElement.EnumerateArray())
            {
                var author = "";
                if (w.TryGetProperty("author", out var authorElement))
                {
                    author =
                        authorElement.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String ? nameElement.GetString() ?? "" :
                        authorElement.TryGetProperty("key", out var keyElement) && keyElement.ValueKind == JsonValueKind.String ? keyElement.GetString() ?? "" :
                        authorElement.TryGetProperty("displayName", out var displayNameElement) && displayNameElement.ValueKind == JsonValueKind.String ? displayNameElement.GetString() ?? "" : "";
                }

                var started = w.TryGetProperty("started", out var startedElement) && startedElement.ValueKind == JsonValueKind.String
                    ? startedElement.GetString() ?? ""
                    : "";

                var seconds = w.TryGetProperty("timeSpentSeconds", out var secondsElement) && secondsElement.TryGetInt64(out var s)
                    ? secondsElement.GetInt64()
                    : 0L;

                var comment = w.TryGetProperty("comment", out var commentElement) && commentElement.ValueKind == JsonValueKind.String
                    ? commentElement.GetString()
                    : null;

                worklogs.Add(new JiraWorklog(
                    issueKey,
                    w.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? "" : "",
                    author,
                    started,
                    started.Length >= 10 ? started[..10] : started,
                    Math.Round(seconds / 3600.0, 2),
                    comment));
            }
        }

        Log.Information("Jira worklogs loaded: {IssueKey} -> {Count}", issueKey, worklogs.Count);
        return worklogs;
    }

    public async Task<JiraWorklogResult> AddWorklogAsync(JiraWorklogRequest request, CancellationToken cancellationToken = default)
    {
        var fields = new Dictionary<string, object?>
        {
            ["timeSpent"] = request.TimeSpent,
            ["started"] = request.Started,
            ["comment"] = request.Comment ?? ""
        };

        if (request.ExtraFields != null)
        {
            foreach (var kv in request.ExtraFields)
                fields[kv.Key] = kv.Value;
        }

        var json = JsonSerializer.Serialize(fields, JsonBodyOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"rest/api/2/issue/{Uri.EscapeDataString(request.IssueKey)}/worklog?adjustEstimate=auto", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var document = JsonDocument.Parse(body);
            var id = document.RootElement.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? "" : "";
            Log.Information("Jira worklog created: {IssueKey} {Started} {TimeSpent}", request.IssueKey, request.Started, request.TimeSpent);
            return new JiraWorklogResult(true, id, null);
        }

        Log.Error("Jira add worklog failed for {IssueKey}: {Error}", request.IssueKey, CompactError(response, body));
        return new JiraWorklogResult(false, null, $"Jira add worklog failed for {request.IssueKey}: {CompactError(response, body)}");
    }

    public async Task<List<JiraTempoWorklogRow>> GetTempoWorklogsAsync(string username, DateOnly dateFrom, DateOnly dateTo, CancellationToken cancellationToken = default)
    {
        var url = $"rest/tempo-timesheets/3/worklogs?username={Uri.EscapeDataString(username)}&dateFrom={dateFrom:yyyy-MM-dd}&dateTo={dateTo:yyyy-MM-dd}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = CompactError(response, body);
            Log.Error("Tempo v3 worklogs fetch failed: {Error}", message);
            throw new InvalidOperationException($"Tempo v3 worklogs fetch failed: {message}");
        }

        using var document = JsonDocument.Parse(body);
        var arrayElement = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement
            : document.RootElement.TryGetProperty("worklogs", out var worklogsElement) && worklogsElement.ValueKind == JsonValueKind.Array
                ? worklogsElement
                : default;

        var rows = new List<JiraTempoWorklogRow>();
        if (arrayElement.ValueKind != JsonValueKind.Array)
            return rows;

        foreach (var w in arrayElement.EnumerateArray())
            rows.Add(ParseTempoWorklogRow(w));

        Log.Information("Tempo v3 worklogs loaded: {Username} {From}..{To} -> {Count}", username, dateFrom, dateTo, rows.Count);
        return rows;
    }

    private static JiraTempoWorklogRow ParseTempoWorklogRow(JsonElement w)
    {
        var issueKey = "";
        if (w.TryGetProperty("issue", out var issueElement) &&
            issueElement.TryGetProperty("key", out var keyElement) &&
            keyElement.ValueKind == JsonValueKind.String)
            issueKey = keyElement.GetString() ?? "";

        var dateStarted = w.TryGetProperty("dateStarted", out var dateElement) && dateElement.ValueKind == JsonValueKind.String
            ? dateElement.GetString() ?? ""
            : "";

        var seconds = w.TryGetProperty("timeSpentSeconds", out var secondsElement) && secondsElement.TryGetInt64(out var s)
            ? s
            : 0L;

        var account = "";
        if (w.TryGetProperty("worklogAttributes", out var attrs) && attrs.ValueKind == JsonValueKind.Array)
        {
            foreach (var attr in attrs.EnumerateArray())
            {
                if (attr.ValueKind != JsonValueKind.Object)
                    continue;
                if (attr.TryGetProperty("key", out var attrKey) &&
                    string.Equals(attrKey.GetString(), "_Account_", StringComparison.OrdinalIgnoreCase) &&
                    attr.TryGetProperty("value", out var attrValue) &&
                    attrValue.ValueKind == JsonValueKind.String)
                {
                    var value = attrValue.GetString() ?? "";
                    if (value.Length > 0)
                        account = value;
                }
            }
        }

        var comment = w.TryGetProperty("comment", out var commentElement) && commentElement.ValueKind == JsonValueKind.String
            ? commentElement.GetString() ?? ""
            : "";

        return new JiraTempoWorklogRow(
            dateStarted.Length >= 10 ? dateStarted[..10] : dateStarted,
            issueKey,
            Math.Round(seconds / 3600.0, 2),
            account,
            comment);
    }

    private string CompactError(HttpResponseMessage response, string body)
    {
        var url = response.RequestMessage?.RequestUri?.ToString() ?? $"{_settings.Url}";
        return $"{url} -> HTTP {(int)response.StatusCode}: {JiraCredentials.Compact(body)}";
    }

    public async Task<List<JiraTimesheetRow>> GetTimesheetReportAsync(string reportUrl, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(reportUrl, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = CompactError(response, html);
            Log.Error("Jira timesheet report failed: {Error}", message);
            throw new InvalidOperationException($"Jira timesheet report failed: {message}");
        }

        var rows = TimesheetHtmlParser.Parse(html);
        Log.Information("Jira timesheet report parsed: {Url} -> {Count} rows", reportUrl, rows.Count);
        return rows;
    }

    private static readonly JsonSerializerOptions JsonBodyOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
