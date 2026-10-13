using System.Globalization;
using Spectre.Console;
using ContextSync.Scenarios;
using Scriban;
using Scriban.Runtime;

namespace ContextSync.Scenarios.Handlers;

/// <summary>
/// Шаг report.day_totals: сводка заполненных часов по датам.
/// Режим summary — таблица дата / сумма часов / статус относительно max_hours.
/// Режим detail — сначала детальная таблица день / задача / часы, затем сводка по дням.
/// </summary>
public sealed class ReportDayTotalsHandler : IScenarioStepHandler
{
    public string Type => "report.day_totals";

    public Task<object?> ExecuteAsync(ScenarioStep step, ScenarioExecContext ctx, CancellationToken cancellationToken)
    {
        var args = ScribanEvaluator.RenderArgs(step.Args, ctx.Root);
        var datasetPath = args.GetString("dataset", "");
        var mode = args.GetString("mode", "summary").Trim().ToLowerInvariant();
        var maxHours = args.GetNumber("max_hours") ?? 8.0;
        var defaultHours = args.GetNumber("default_hours") ?? 0.0;
        var title = args.GetString("title", "");

        if (datasetPath.Length == 0)
            throw new ScenarioStepException("report.day_totals: args.dataset is required");

        var items = ctx.Scenario.GetListPath(datasetPath);
        if (items == null)
            throw new ScenarioStepException($"report.day_totals: dataset '{datasetPath}' not found or not a list");

        var rows = items.OfType<ScriptObject>().ToList();

        var hoursByDate = new Dictionary<string, double>(StringComparer.Ordinal);
        var detailRows = new List<(string Date, string Issue, double Hours)>();

        foreach (var row in rows)
        {
            var date = GetText(row, "date");
            if (date.Length < 10)
                continue;
            date = date[..10];

            var hours = GetHours(row, defaultHours);
            hoursByDate[date] = hoursByDate.GetValueOrDefault(date) + hours;

            var issue = GetIssue(row);
            if (issue.Length > 0)
                detailRows.Add((date, issue, hours));
        }

        if (hoursByDate.Count == 0)
        {
            ctx.Console.MarkupLine("[yellow]Nothing to report: no dated rows in dataset.[/]");
            return Task.FromResult<object?>(new ScriptObject
            {
                ["days"] = new List<object?>(),
                ["count"] = (long)0,
                ["wrong_days"] = (long)0,
                ["total_hours"] = 0.0
            });
        }

        if (mode == "detail")
        {
            var detailEntries = detailRows
                .GroupBy(x => (x.Date, x.Issue))
                .OrderBy(g => g.Key.Date, StringComparer.Ordinal)
                .ThenBy(g => g.Key.Issue, StringComparer.Ordinal)
                .Select(g => new { date = g.Key.Date, issue = g.Key.Issue, hours = Math.Round(g.Sum(x => x.Hours), 2) })
                .ToList();

            var detailTable = new Table().RoundedBorder();
            detailTable.Title = new TableTitle("Детализация по задачам");
            detailTable.AddColumn("Date");
            detailTable.AddColumn("Issue");
            detailTable.AddColumn("Hours");
            foreach (var entry in detailEntries)
                detailTable.AddRow(
                    entry.date.EscapeMarkup(),
                    entry.issue.EscapeMarkup(),
                    entry.hours.ToString("0.##", CultureInfo.InvariantCulture));
            ctx.Console.Write(detailTable);
            ctx.Console.WriteLine();
        }

        var table = new Table().RoundedBorder();
        if (title.Length > 0)
            table.Title = new TableTitle($"{title.EscapeMarkup()} (цель {maxHours:0.##}h)");
        else if (mode == "detail")
            table.Title = new TableTitle($"Сумма по дням (цель {maxHours:0.##}h)");
        else
            table.Title = new TableTitle($"Часы по дням (цель {maxHours:0.##}h)");
        table.AddColumn("Date");
        table.AddColumn("Hours");
        table.AddColumn("Status");

        var dayObjects = new List<object?>();
        var wrongDays = 0;
        double total = 0;

        foreach (var kv in hoursByDate.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var logged = Math.Round(kv.Value, 2);
            total += logged;
            var diff = Math.Round(logged - maxHours, 2);
            if (diff < -0.005)
                wrongDays++;

            table.AddRow(
                kv.Key.EscapeMarkup(),
                logged.ToString("0.##", CultureInfo.InvariantCulture),
                StatusMarkup(diff));

            dayObjects.Add(new ScriptObject
            {
                ["date"] = kv.Key,
                ["logged_hours"] = logged,
                ["diff"] = diff,
                ["status"] = DiffStatus(diff)
            });
        }

        ctx.Console.Write(table);

        var wrongLabel = wrongDays == 0 ? "[green]0[/]" : $"[yellow]{wrongDays}[/]";
        ctx.Console.MarkupLine(
            $"[grey]Итого:[/] [bold]{Math.Round(total, 2):0.##}h[/] за {dayObjects.Count} дн. · цель {maxHours:0.##}h/день · дней с недобором: {wrongLabel}");

        return Task.FromResult<object?>(new ScriptObject
        {
            ["days"] = dayObjects,
            ["count"] = (long)dayObjects.Count,
            ["wrong_days"] = (long)wrongDays,
            ["total_hours"] = Math.Round(total, 2)
        });
    }

    private static string StatusMarkup(double diff)
    {
        if (diff < -0.005)
            return $"[yellow]недобор {Math.Abs(diff):0.##}h[/]";
        if (diff > 0.005)
            return $"[red]перебор +{diff:0.##}h[/]";
        return "[green]ок[/]";
    }

    private static string DiffStatus(double diff)
    {
        if (diff < -0.005) return "under";
        if (diff > 0.005) return "over";
        return "ok";
    }

    private static string GetIssue(ScriptObject row)
    {
        if (row.TryGetValue("issue", out var i) && i != null && i.ToString()?.Length > 0)
            return i.ToString() ?? "";
        if (row.TryGetValue("issue_key", out var ik) && ik != null && ik.ToString()?.Length > 0)
            return ik.ToString() ?? "";
        if (row.TryGetValue("key", out var k) && k != null && k.ToString()?.Length > 0)
            return k.ToString() ?? "";
        return "";
    }

    private static string GetText(ScriptObject row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
            return "";
        if (value is double d)
            return d.ToString("0.##", CultureInfo.InvariantCulture);
        if (value is long l)
            return l.ToString(CultureInfo.InvariantCulture);
        return value.ToString() ?? "";
    }

    private static double GetHours(ScriptObject row, double defaultHours)
    {
        if (row.TryGetValue("hours", out var h) && h is double d && d > 0)
            return d;
        if (row.TryGetValue("logged_hours", out var lh) && lh is double ld && ld > 0)
            return ld;
        if (row.TryGetValue("time_spent", out var ts) && ts is string s && ParseTimeSpentToHours(s) > 0)
            return ParseTimeSpentToHours(s);
        return defaultHours;
    }

    private static double ParseTimeSpentToHours(string timeSpent)
    {
        var hours = 0.0;
        var match = System.Text.RegularExpressions.Regex.Match(timeSpent, @"(\d+(?:[.,]\d+)?)\s*(w|d|h|m)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        while (match.Success)
        {
            var value = double.Parse(match.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            hours += match.Groups[2].Value.ToLowerInvariant() switch
            {
                "w" => value * 40.0,
                "d" => value * 8.0,
                "m" => value / 60.0,
                _ => value
            };
            match = match.NextMatch();
        }
        return hours;
    }
}
