using System.Globalization;
using Spectre.Console;
using ContextSync.Abstractions;
using ContextSync.Scenarios;
using Scriban;
using Scriban.Runtime;

namespace ContextSync.Scenarios.Handlers;

/// <summary>
/// Шаг jira.tempo_worklogs: выборка журналов работ автора из Tempo Timesheets REST v3
/// (/rest/tempo-timesheets/3/worklogs?username=...&dateFrom=...&dateTo=...) с агрегацией по (дата, задача).
/// </summary>
public sealed class JiraTempoWorklogsHandler : IScenarioStepHandler
{
    public string Type => "jira.tempo_worklogs";

    public async Task<object?> ExecuteAsync(ScenarioStep step, ScenarioExecContext ctx, CancellationToken cancellationToken)
    {
        var args = ScribanEvaluator.RenderArgs(step.Args, ctx.Root);

        var baseUrl = ctx.Definition.Connection.Jira?.Url ?? "";
        var username = args.GetString("username", "");
        var fromRaw = args.GetString("from", "");
        var toRaw = args.GetString("to", "");
        var weeks = (int?)args.GetNumber("num_of_weeks") ?? 0;
        var datasetPath = args.GetString("dataset", "");

        if (username.Length == 0)
            throw new ScenarioStepException("jira.tempo_worklogs: args.username is required");

        DateOnly from;
        DateOnly to;

        if (datasetPath.Length > 0 && fromRaw.Length == 0 && toRaw.Length == 0)
        {
            // Диапазон определяем по датам из указанного набора (например parse.entries)
            var datasetRows = ctx.Scenario.GetListPath(datasetPath);
            if (datasetRows == null)
                throw new ScenarioStepException($"jira.tempo_worklogs: dataset '{datasetPath}' not found or not a list");

            var dated = datasetRows
                .OfType<ScriptObject>()
                .Select(r => r.TryGetValue("date", out var d) ? Convert.ToString(d, CultureInfo.InvariantCulture) ?? "" : "")
                .Where(s => s.Length >= 10)
                .Select(s => DateOnly.Parse(s[..10], CultureInfo.InvariantCulture))
                .ToList();

            if (dated.Count == 0)
                throw new ScenarioStepException($"jira.tempo_worklogs: dataset '{datasetPath}' has no dated rows");

            from = dated.Min();
            to = dated.Max();
        }
        else
        {
            if (weeks <= 0 && fromRaw.Length == 0 && toRaw.Length == 0)
                throw new ScenarioStepException("jira.tempo_worklogs: args.from, args.dataset or args.num_of_weeks is required");

            if (fromRaw.Length > 0)
                from = ParseDate(fromRaw)
                    ?? throw new ScenarioStepException($"jira.tempo_worklogs: cannot parse args.from '{fromRaw}' (expected yyyy-MM-dd or dd.MM.yyyy)");
            else
                from = DateOnly.FromDateTime(DateTime.Today).AddDays(-(weeks * 7 - 1));

            if (toRaw.Length > 0)
                to = ParseDate(toRaw)
                    ?? throw new ScenarioStepException($"jira.tempo_worklogs: cannot parse args.to '{toRaw}' (expected yyyy-MM-dd or dd.MM.yyyy)");
            else
                to = weeks > 0 ? from.AddDays(weeks * 7 - 1) : from;
        }

        var url = $"{baseUrl.TrimEnd('/')}/rest/tempo-timesheets/3/worklogs?username={Uri.EscapeDataString(username)}&dateFrom={from:yyyy-MM-dd}&dateTo={to:yyyy-MM-dd}";
        ctx.Console.MarkupLine($"[grey]GET[/] {url.EscapeMarkup()}");

        var rows = await ctx.GetJira().GetTempoWorklogsAsync(username, from, to, cancellationToken);

        var entries = rows
            .GroupBy(r => (r.Date, r.IssueKey))
            .OrderBy(g => g.Key.Date, StringComparer.Ordinal)
            .ThenBy(g => g.Key.IssueKey, StringComparer.Ordinal)
            .Select(g => new ScriptObject
            {
                ["date"] = g.Key.Date,
                ["issue"] = g.Key.IssueKey,
                ["hours"] = g.Sum(x => x.Hours),
                ["account"] = string.Join(", ", g.Select(x => x.Account).Where(a => a.Length > 0).Distinct()),
                ["comment"] = string.Join(" | ", g.Select(x => x.Comment).Where(c => c.Length > 0).Distinct())
            })
            .Cast<object?>()
            .ToList();

        return new ScriptObject
        {
            ["entries"] = entries,
            ["count"] = (long)entries.Count
        };
    }

    private static DateOnly? ParseDate(string value)
    {
        if (value.Length == 0)
            return null;

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
            return iso;
        if (DateOnly.TryParseExact(value, "yyyy.MM.dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dotIso))
            return dotIso;
        if (DateOnly.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ru))
            return ru;
        if (DateOnly.TryParseExact(value, "dd.MM.yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ruShort))
            return ruShort;
        return null;
    }
}
