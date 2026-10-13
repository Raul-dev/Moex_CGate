using System.Globalization;
using ContextSync.Abstractions;
using ContextSync.Scenarios;
using Scriban;
using Scriban.Runtime;

namespace ContextSync.Scenarios.Handlers;

public sealed class JiraTestConnectionHandler : IScenarioStepHandler
{
    public string Type => "jira.test_connection";

    public async Task<object?> ExecuteAsync(ScenarioStep step, ScenarioExecContext ctx, CancellationToken cancellationToken)
    {
        var ok = await ctx.GetJira().TestConnectionAsync(cancellationToken);
        return new ScriptObject
        {
            ["ok"] = ok,
            ["url"] = ctx.Definition.Connection.Jira?.Url ?? ""
        };
    }
}

public sealed class JiraSearchHandler : IScenarioStepHandler
{
    public string Type => "jira.search";

    public async Task<object?> ExecuteAsync(ScenarioStep step, ScenarioExecContext ctx, CancellationToken cancellationToken)
    {
        var args = ScribanEvaluator.RenderArgs(step.Args, ctx.Root);
        var jql = args.GetString("jql", "");

        if (string.IsNullOrWhiteSpace(jql))
            throw new ScenarioStepException("jira.search: args.jql is required");

        var maxResults = (int)(args.GetNumber("max_results") ?? 200);
        if (maxResults <= 0) maxResults = 200;

        var issues = await ctx.GetJira().SearchAsync(jql, maxResults, cancellationToken);

        return issues.Select(issue => new ScriptObject
        {
            ["key"] = issue.Key,
            ["summary"] = issue.Summary,
            ["status"] = issue.Status
        }).Cast<object?>().ToList();
    }
}

public sealed class JiraGetWorklogsHandler : IScenarioStepHandler
{
    public string Type => "jira.get_worklogs";

    public async Task<object?> ExecuteAsync(ScenarioStep step, ScenarioExecContext ctx, CancellationToken cancellationToken)
    {
        var args = ScribanEvaluator.RenderArgs(step.Args, ctx.Root);
        var issueKey = args.GetString("issue_key", "");

        if (string.IsNullOrWhiteSpace(issueKey))
            throw new ScenarioStepException("jira.get_worklogs: args.issue_key is required");

        var worklogs = await ctx.GetJira().GetWorklogsAsync(issueKey, cancellationToken);

        return worklogs.Select(w =>
        {
            var row = new ScriptObject
            {
                ["issue_key"] = w.IssueKey,
                ["worklog_id"] = w.WorklogId,
                ["author"] = w.Author,
                ["started"] = w.Started,
                ["date"] = w.Date,
                ["hours"] = w.Hours,
                ["comment"] = (object?)w.Comment ?? ""
            };
            return row;
        }).Cast<object?>().ToList();
    }
}

public sealed class JiraAddWorklogHandler : IScenarioStepHandler
{
    public string Type => "jira.add_worklog";

    public async Task<object?> ExecuteAsync(ScenarioStep step, ScenarioExecContext ctx, CancellationToken cancellationToken)
    {
        var args = ScribanEvaluator.RenderArgs(step.Args, ctx.Root);

        var issueKey = args.GetString("issue_key", "");
        var started = args.GetString("started", "");
        var timeSpent = args.GetString("time_spent", "");
        var comment = args.GetString("comment", ".");
        var account = args.GetString("account", "");
        var accountField = args.GetString("account_field", "");
        var onExists = args.GetString("on_exists", "skip").ToLowerInvariant();
        var username = args.GetString("username", "") is { Length: > 0 } u ? u : (ctx.Definition.Connection.Jira?.Username ?? "");
        var maxHours = args.GetNumber("max_hours") ?? 0.0;

        if (string.IsNullOrWhiteSpace(issueKey))
            throw new ScenarioStepException("jira.add_worklog: args.issue_key is required");

        if (started.Length == 0)
        {
            var dateArg = args.GetString("date", "");
            if (dateArg.Length < 10)
                throw new ScenarioStepException("jira.add_worklog: args.started or args.date (yyyy-MM-dd) is required");

            var startTime = args.GetString("start_time", "12:00");
            var tzOffset = args.GetString("tz_offset", "+03:00").Replace(":", "");
            started = $"{dateArg[..10]}T{startTime}:00.000{tzOffset}";
        }

        if (timeSpent.Length == 0)
        {
            var hours = args.GetNumber("hours");
            if (hours == null)
                throw new ScenarioStepException("jira.add_worklog: args.time_spent (e.g. '6h' or seconds) or args.hours is required");

            timeSpent = $"{Convert.ToString(hours.Value, CultureInfo.InvariantCulture)}h";
        }

        var author = ctx.Definition.Connection.Jira?.Username ?? "";
        var date = started[..10];
        var newHours = args.GetNumber("hours") ?? ParseTimeSpentToHours(timeSpent);

        if (ctx.DryRun)
        {
            return BuildRow(issueKey, date, timeSpent, account, "dry", "", null, comment, dry: true);
        }

        var jira = ctx.GetJira();

        if (newHours <= 0 && maxHours > 0)
        {
            return BuildRow(issueKey, date, timeSpent, account, "skipped_zero_hours", "no remaining day budget after max_hours split", null, comment, dry: false);
        }

        // Tempo v3 (read-only): существующие записи автора за этот день
        IReadOnlyList<JiraTempoWorklogRow>? tempoRows = null;
        try
        {
            var dateOnly = DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            tempoRows = await jira.GetTempoWorklogsAsync(username, dateOnly, dateOnly, cancellationToken);
        }
        catch (Exception ex)
        {
            ctx.Warnings.Add($"{issueKey} {date}: Tempo day check failed ({ex.Message})");
        }

        var tempoList = tempoRows ?? new List<JiraTempoWorklogRow>();
        var dayExisting = tempoList.Sum(x => x.Hours);
        var duplicate = tempoList.Any(w =>
            w.Date == date &&
            string.Equals(w.IssueKey, issueKey, StringComparison.OrdinalIgnoreCase));

        if (duplicate && onExists == "skip")
        {
            return BuildRow(issueKey, date, timeSpent, account, "skipped_duplicate", "", null, comment, dry: false);
        }

        if (duplicate && onExists == "warn")
        {
            ctx.Warnings.Add($"{issueKey} {date}: worklog already exists, creating another one (on_exists=warn)");
        }

        if (maxHours > 0 && dayExisting + newHours > maxHours + 0.005)
        {
            return BuildRow(
                issueKey, date, timeSpent, account,
                "skipped_day_limit",
                $"day already {dayExisting:0.##}h, new {newHours:0.##}h would exceed max_hours={maxHours:0.##}h",
                null, comment, dry: false);
        }

        var extraFields = new Dictionary<string, object?>();
        if (account.Length > 0)
        {
            if (accountField.Length > 0)
                extraFields[accountField] = account;
            else if (!string.IsNullOrEmpty(account))
                ctx.Warnings.Add($"{issueKey} {date}: account value '{account}' set but args.account_field is missing - ACCOUNT was not sent (Tempo POST pending; attribute format: _Account_ = tempo account key, e.g. TASK)");
        }

        var response = await jira.AddWorklogAsync(
            new JiraWorklogRequest(issueKey, started, timeSpent, comment, extraFields),
            cancellationToken);

        if (!response.Success)
            return BuildRow(issueKey, date, timeSpent, account, "error", response.Error ?? "unknown error", null, comment, dry: false);

        return BuildRow(issueKey, date, timeSpent, account, "created", "", response.WorklogId, comment, dry: false);
    }

    public static ScriptObject BuildRow(
        string issueKey, string date, string timeSpent, string account,
        string status, string error, string? worklogId, string comment, bool dry) =>
        new()
        {
            ["issue_key"] = issueKey,
            ["date"] = date,
            ["time_spent"] = timeSpent,
            ["account"] = account,
            ["comment"] = comment,
            ["status"] = status,
            ["error"] = error,
            ["worklog_id"] = (object?)worklogId ?? "",
            ["dry_run"] = dry
        };

    private static double ParseTimeSpentToHours(string timeSpent)
    {
        var hours = 0.0;
        var match = System.Text.RegularExpressions.Regex.Match(timeSpent, @"(\d+(?:[.,]\d+)?)\s*(w|d|h|m)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        while (match.Success)
        {
            var value = double.Parse(match.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            var total = match.Groups[2].Value.ToLowerInvariant() switch
            {
                "w" => value * 40.0,
                "d" => value * 8.0,
                "m" => value / 60.0,
                _ => value
            };
            hours += total;
            match = match.NextMatch();
        }
        return hours;
    }
}
