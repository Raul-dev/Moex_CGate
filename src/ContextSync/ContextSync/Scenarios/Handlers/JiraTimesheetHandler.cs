using System.Net;
using Spectre.Console;
using ContextSync.Abstractions;
using ContextSync.Scenarios;
using Scriban;
using Scriban.Runtime;

namespace ContextSync.Scenarios.Handlers;

/// <summary>
/// Шаг jira.timesheet: скачивает TimesheetReport.jspa (jira-timesheet-plugin, sum=day)
/// от имениAuthenticated-пользователя и разбирает HTML в структуру Дата / Задача / Часы.
/// </summary>
public sealed class JiraTimesheetHandler : IScenarioStepHandler
{
    public string Type => "jira.timesheet";

    public async Task<object?> ExecuteAsync(ScenarioStep step, ScenarioExecContext ctx, CancellationToken cancellationToken)
    {
        var args = ScribanEvaluator.RenderArgs(step.Args, ctx.Root);

        var baseUrl = ctx.Definition.Connection.Jira?.Url ?? "";
        if (baseUrl.Length == 0)
            throw new ScenarioStepException("jira.timesheet: connection.jira.url is required");

        var url = TimesheetUrlBuilder.Build(
            baseUrl,
            args.GetString("report_key", "jira-timesheet-plugin:report"),
            args.GetString("target_user", ""),
            (int)(args.GetNumber("reporting_day") ?? 2),
            args.GetBoolValue("sum_subtasks", true),
            args.GetString("sum", "day"),
            (int)(args.GetNumber("num_of_weeks") ?? 0),
            (int)(args.GetNumber("offset") ?? 0));

        ctx.Console.MarkupLine($"[grey]GET[/] {url.EscapeMarkup()}");

        var rows = await ctx.GetJira().GetTimesheetReportAsync(url, cancellationToken);

        var entries = rows.Select(r => new ScriptObject
        {
            ["date"] = r.Date,
            ["issue"] = r.Issue,
            ["summary"] = r.Summary,
            ["hours"] = r.Hours
        }).Cast<object?>().ToList();

        return new ScriptObject
        {
            ["entries"] = entries,
            ["count"] = (long)entries.Count
        };
    }
}

internal static class TimesheetUrlBuilder
{
    public static string Build(
        string baseUrl,
        string reportKey,
        string targetUser,
        int reportingDay,
        bool sumSubTasks,
        string sum,
        int numOfWeeks,
        int offset)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ScenarioStepException("jira.timesheet: connection.jira.url is empty");

        var query = new List<string>
        {
            $"reportKey={WebUtility.UrlEncode(reportKey)}",
            $"targetUser={WebUtility.UrlEncode(targetUser)}",
            $"reportingDay={reportingDay}",
            $"sumSubTasks={(sumSubTasks ? "true" : "false")}",
            $"sum={WebUtility.UrlEncode(sum)}",
            "page=1"
        };
        if (numOfWeeks > 0)
            query.Add($"numOfWeeks={numOfWeeks}");
        if (offset != 0)
            query.Add($"offset={offset}");

        return $"{baseUrl.TrimEnd('/')}/secure/TimesheetReport.jspa?{string.Join("&", query)}";
    }
}
