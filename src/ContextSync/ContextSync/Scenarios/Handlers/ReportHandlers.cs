using System.Globalization;
using System.Net;
using Spectre.Console;
using ContextSync.Scenarios;
using Scriban;
using Scriban.Runtime;

namespace ContextSync.Scenarios.Handlers;

public sealed class ReportConsoleHandler : IScenarioStepHandler
{
    public string Type => "report.console";

    public Task<object?> ExecuteAsync(ScenarioStep step, ScenarioExecContext ctx, CancellationToken cancellationToken)
    {
        var args = ScribanEvaluator.RenderArgs(step.Args, ctx.Root);
        var datasetPath = args.GetString("dataset", "");
        var title = args.GetString("title", "");
        var onlyWrong = args.GetBoolValue("only_wrong", false);
        var maxRows = (int)(args.GetNumber("max_rows") ?? 0);

        var columns = step.Args.TryGetValue("columns", out var rawColumns)
            ? rawColumns.AsStringList()
            : new List<string>();

        if (datasetPath.Length == 0)
            throw new ScenarioStepException("report.console: args.dataset is required");

        var items = ctx.Scenario.GetListPath(datasetPath);
        if (items == null)
            throw new ScenarioStepException($"report.console: dataset '{datasetPath}' not found or not a list");

        if (onlyWrong)
            items = items.Where(FilterWrong).ToList();

        if (items.Count == 0)
        {
            ctx.Console.MarkupLine("[yellow]Nothing to report: dataset is empty.[/]");
            return Task.FromResult<object?>(new ScriptObject { ["rows"] = (long)0 });
        }

        if (columns.Count == 0 && items[0] is ScriptObject first)
            columns = first.Keys.ToList();

        var table = new Table().RoundedBorder();
        if (title.Length > 0)
            table.Title = new TableTitle(title);
        foreach (var column in columns)
            table.AddColumn(ToLabel(column));

        var rowCount = 0;
        foreach (var item in items.OfType<ScriptObject>())
        {
            if (maxRows > 0 && rowCount >= maxRows)
            {
                ctx.Console.MarkupLine($"[grey]... {items.Count - rowCount} more rows hidden (max_rows={maxRows})[/]");
                break;
            }
            var cells = columns.Select(column => GetColumnValue(item, column));
            table.AddRow(cells.ToArray());
            rowCount++;
        }

        ctx.Console.Write(table);

        return Task.FromResult<object?>(new ScriptObject { ["rows"] = (long)rowCount });
    }

    private static bool FilterWrong(object? item)
    {
        if (item is not ScriptObject row)
            return false;
        if (row.TryGetValue("logged_hours", out var loggedObj) && loggedObj is double logged &&
            row.TryGetValue("expected_hours", out var expectedObj) && expectedObj is double expected)
            return logged < expected - 0.01;
        return true;
    }

    private static string ToLabel(string column) => column.Replace('_', ' ');

    private static string GetColumnValue(ScriptObject item, string column)
    {
        var current = item;
        var segments = column.Split('.');
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (current.TryGetValue(segments[i], out var inner) && inner is ScriptObject nested)
                current = nested;
            else
                return "";
        }

        var key = segments[^1];
        if (!current.TryGetValue(key, out var value))
            return "";

        return FormatValue(value);
    }

    private static string FormatValue(object? value)
    {
        if (value == null) return "";
        if (value is double d) return d.ToString("0.##", CultureInfo.InvariantCulture);
        if (value is long l) return l.ToString(CultureInfo.InvariantCulture);
        if (value is int i) return i.ToString(CultureInfo.InvariantCulture);
        if (value is bool b) return b ? "true" : "false";
        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            var items = enumerable.Cast<object?>().Select(o => o?.ToString() ?? "").ToList();
            return string.Join(", ", items);
        }
        return value.ToString() ?? "";
    }
}

public sealed class ReportLinkHandler : IScenarioStepHandler
{
    public string Type => "report.link";

    public Task<object?> ExecuteAsync(ScenarioStep step, ScenarioExecContext ctx, CancellationToken cancellationToken)
    {
        var args = ScribanEvaluator.RenderArgs(step.Args, ctx.Root);

        var reportKey = args.GetString("report_key", "jira-timesheet-plugin:report");
        var targetUser = args.GetString("target_user", "");
        var reportingDay = (int)(args.GetNumber("reporting_day") ?? 2);
        var numOfWeeks = (int)(args.GetNumber("num_of_weeks") ?? 0);
        var sum = args.GetString("sum", "day");
        var sumSubtasks = args.GetBoolValue("sum_subtasks", true);
        var offset = (int)(args.GetNumber("offset") ?? 0);

        var baseUrl = ctx.Definition.Connection.Jira?.Url ?? "";
        if (baseUrl.Length == 0)
            throw new ScenarioStepException("report.link: connection.jira.url is required");

        var query = new List<string>
        {
            $"reportKey={WebUtility.UrlEncode(reportKey)}",
            $"targetUser={WebUtility.UrlEncode(targetUser)}",
            $"reportingDay={reportingDay}",
            $"sumSubTasks={(sumSubtasks ? "true" : "false")}",
            $"sum={WebUtility.UrlEncode(sum)}",
            "page=0"
        };
        if (numOfWeeks > 0)
            query.Add($"numOfWeeks={numOfWeeks}");
        if (offset != 0)
            query.Add($"offset={offset}");

        var url = $"{baseUrl.TrimEnd('/')}/secure/TimesheetReport.jspa?{string.Join("&", query)}";

        var rule = new Rule("[bold blue]Jira timesheet report link[/]").RuleStyle("blue").LeftJustified();
        ctx.Console.Write(rule);
        ctx.Console.WriteLine(url);
        ctx.Console.WriteLine();

        return Task.FromResult<object?>(new ScriptObject
        {
            ["url"] = url,
            ["target_user"] = targetUser
        });
    }
}
