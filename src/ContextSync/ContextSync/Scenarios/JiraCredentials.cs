using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using ContextSync.Infrastructure.Security;
using ContextSync.Infrastructure.Settings;
using Spectre.Console;

namespace ContextSync.Scenarios;

[SupportedOSPlatform("windows")]
public static class JiraCredentials
{
    public static async Task<bool> EnsureLoginAsync(
        IAnsiConsole console,
        ScenarioJiraConnection conn,
        JiraSettings fallback,
        string yamlPath,
        CancellationToken cancellationToken = default)
    {
        var url = conn.Url.Length > 0 ? conn.Url : fallback.Url;
        var endpoint = url.TrimEnd('/') + "/rest/api/2/myself";
        var username = ResolveUsername(conn, fallback, console);

        var password = "";
        var sourceLabel = "will prompt";
        var shouldSave = false;

        if (conn.PasswordProtected.Length > 0)
        {
            try
            {
                password = PasswordProtector.Unprotect(conn.PasswordProtected);
                sourceLabel = "scenario yaml (DPAPI)";
            }
            catch
            {
                console.MarkupLine("[yellow]Stored password cannot be decrypted on this Windows account - it will be asked again[/]");
            }
        }

        if (password.Length == 0 && Environment.GetEnvironmentVariable("JIRA_TOKEN") is { Length: > 0 } envToken)
        {
            password = envToken;
            sourceLabel = "JIRA_TOKEN env";
        }

        console.MarkupLine("[grey]connection[/]");
        console.MarkupLine($"  [grey]username:[/] {username.EscapeMarkup()}");
        console.MarkupLine($"  [grey]url:[/]      {url.EscapeMarkup()}");
        console.MarkupLine($"  [grey]check:[/]    [link]{endpoint.EscapeMarkup()}[/]");
        console.MarkupLine($"  [grey]password:[/]  {sourceLabel.EscapeMarkup()}");
        console.WriteLine();

        var maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            console.MarkupLine($"[grey]GET[/] {endpoint.EscapeMarkup()}");

            var (ok, status, reason) = await ProbeAsync(url, username, password, cancellationToken);

            if (ok)
            {
                console.MarkupLine($"[green]HTTP {status}[/] login OK: {username.EscapeMarkup()} @ {url.EscapeMarkup()}");
                fallback.Username = username;
                fallback.ApiToken = password;

                if (shouldSave)
                {
                    if (SaveToYaml(yamlPath, username, PasswordProtector.Protect(password)))
                        console.MarkupLine($"[green]credentials saved[/] [grey]{Path.GetFileName(yamlPath).EscapeMarkup()} (password_protected, DPAPI: current Windows account)[/]");
                    else
                        console.MarkupLine("[yellow]could not save credentials to scenario YAML[/]");
                }
                return true;
            }

            console.MarkupLine($"[red]HTTP {status}[/]{ReasonMarkup(reason)}");

            if (status == 0 || (status != 401 && status != 403) || attempt == maxAttempts)
                return false;

            console.MarkupLine($"[yellow]password rejected - enter it again ({attempt + 1}/{maxAttempts})[/]");
            password = console.Prompt(
                new TextPrompt<string>($"Jira password for [green]{username.EscapeMarkup()}[/]:")
                    .PromptStyle("red")
                    .Secret());
            shouldSave = true;
        }

        return false;
    }

    private static string ResolveUsername(ScenarioJiraConnection conn, JiraSettings fallback, IAnsiConsole console)
    {
        var username = conn.Username.Contains("{{") ? "" : conn.Username;
        if (username.Length == 0) username = fallback.Username;
        if (username.Length == 0)
            username = console.Prompt(new TextPrompt<string>("Jira username:").DefaultValue(Environment.UserName));
        return username;
    }

    private static async Task<(bool Ok, int Status, string Reason)> ProbeAsync(
        string url, string username, string password, CancellationToken cancellationToken)
    {
        try
        {
            using var http = new HttpClient();
            http.BaseAddress = new Uri(url.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(20);
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await http.GetAsync("rest/api/2/myself", cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return (response.IsSuccessStatusCode, (int)response.StatusCode, Compact(body));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return (false, 0, ex.Message);
        }
    }

    internal static string Compact(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var plain = System.Net.WebUtility.HtmlDecode(Regex.Replace(text, "<[^>]+>", " "));
        plain = Regex.Replace(plain, @"\s+", " ").Trim();
        var reason = Regex.Match(plain, @"Reason\s*:\s*([A-Za-z0-9_]+)");
        if (reason.Success) return reason.Groups[1].Value;
        return plain.Length > 120 ? plain[..120] : plain;
    }

    private static string ReasonMarkup(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return "";
        var trimmed = reason.Length > 120 ? reason[..120] : reason;
        return $" [grey]{trimmed.EscapeMarkup()}[/]";
    }

    private static bool SaveToYaml(string yamlPath, string username, string protectedPassword)
    {
        if (!File.Exists(yamlPath)) return false;

        var text = File.ReadAllText(yamlPath);
        var jiraIndent = Regex.Match(text, @"(?m)^(\s+)jira:");
        if (!jiraIndent.Success) return false;
        var childIndent = jiraIndent.Groups[1].Value + "  ";

        var usernamePattern = $@"(?m)^{Regex.Escape(childIndent)}username:.*$";
        var protectedPattern = $@"(?m)^{Regex.Escape(childIndent)}password_protected:.*$";

        if (!Regex.IsMatch(text, usernamePattern))
        {
            var urlMatch = Regex.Match(text, $@"(?m)^{Regex.Escape(childIndent)}url:.*$");
            if (!urlMatch.Success) return false;
            text = text.Insert(urlMatch.Index + urlMatch.Length, $"\n{childIndent}username: {Quote(username)}");
        }

        var usernameMatch = Regex.Match(text, usernamePattern);
        if (!usernameMatch.Success) return false;

        var updated = Regex.IsMatch(text, protectedPattern)
            ? Regex.Replace(text, protectedPattern, _ => $"{childIndent}password_protected: {Quote(protectedPassword)}")
            : text.Insert(usernameMatch.Index + usernameMatch.Length,
                $"\n{childIndent}password_protected: {Quote(protectedPassword)}");

        var bytes = File.ReadAllBytes(yamlPath);
        var encoding = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF
            ? new UTF8Encoding(true)
            : new UTF8Encoding(false);

        File.WriteAllText(yamlPath, updated, encoding);
        return true;
    }

    private static string Quote(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
