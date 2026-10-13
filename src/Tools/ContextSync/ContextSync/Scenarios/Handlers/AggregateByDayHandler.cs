using System.Globalization;
using ContextSync.Scenarios;
using Scriban;
using Scriban.Runtime;

namespace ContextSync.Scenarios.Handlers;

public sealed class AggregateByDayHandler : IScenarioStepHandler
{
    public string Type => "aggregate.by_day";

    public Task<object?> ExecuteAsync(ScenarioStep step, ScenarioExecContext ctx, CancellationToken cancellationToken)
    {
        var args = ScribanEvaluator.RenderArgs(step.Args, ctx.Root);
        var datasetPath = args.GetString("dataset", "worklogs");
        var workdaysPath = args.GetString("workdays", "");
        var author = args.GetString("author", "");
        var expected = args.GetNumber("expected") ?? 8.0;

        var worklogs = ctx.Scenario.GetListPath(datasetPath);

        var byDate = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (workdaysPath.Length > 0)
        {
            var workdays = ctx.Scenario.GetListPath(workdaysPath) ?? new List<object?>();
            foreach (var day in workdays)
                byDate[CalendarIso(day)] = new List<string>();
        }

        foreach (var item in worklogs ?? [])
        {
            if (item is not ScriptObject row)
                continue;

            var date = GetStringMember(row, "date", "");
            if (date.Length < 10)
                continue;
            var iso = date[..10];

            if (author.Length > 0 && !string.Equals(GetStringMember(row, "author", ""), author, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!byDate.TryGetValue(iso, out var keys))
            {
                keys = new List<string>();
                byDate[iso] = keys;
            }

            var issueKey = GetStringMember(row, "issue_key", "");
            if (issueKey.Length > 0 && !keys.Contains(issueKey))
                keys.Add(issueKey);
        }

        var hoursByDate = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in worklogs ?? [])
        {
            if (item is not ScriptObject row)
                continue;
            var date = GetStringMember(row, "date", "");
            if (date.Length < 10)
                continue;
            if (author.Length > 0 && !string.Equals(GetStringMember(row, "author", ""), author, StringComparison.OrdinalIgnoreCase))
                continue;
            var hours = GetDoubleMember(row, "hours");
            hoursByDate[date[..10]] = hoursByDate.GetValueOrDefault(date[..10]) + hours;
        }

        var days = byDate
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv =>
            {
                var logged = Math.Round(hoursByDate.GetValueOrDefault(kv.Key), 2);
                return new ScriptObject
                {
                    ["date"] = kv.Key,
                    ["expected_hours"] = expected,
                    ["logged_hours"] = logged,
                    ["diff"] = Math.Round(logged - expected, 2),
                    ["issues"] = kv.Value.Cast<object?>().ToList()
                };
            })
            .ToList();

        var summary = new ScriptObject
        {
            ["days"] = days.Cast<object?>().ToList(),
            ["wrong_days"] = days.Count(d => d is ScriptObject o && o["logged_hours"] is double l && l < expected - 0.01),
            ["total_logged"] = Math.Round(hoursByDate.Values.Sum(), 2)
        };

        return Task.FromResult<object?>(summary);
    }

    private static string CalendarIso(object? value)
    {
        var result = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return result.Length >= 10 ? result[..10] : result;
    }

    private static string GetStringMember(ScriptObject row, string key, string fallback) =>
        row.ContainsKey(key) && row[key] != null
            ? row[key]?.ToString() ?? fallback
            : fallback;

    private static double GetDoubleMember(ScriptObject row, string key) =>
        row.ContainsKey(key) && row[key] is double d ? d : 0;
}
