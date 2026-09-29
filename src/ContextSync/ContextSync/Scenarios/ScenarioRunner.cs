using Spectre.Console;
using Scriban.Runtime;
using ContextSync.Infrastructure.Settings;
using ContextSync.Abstractions;
using ContextSync.Scenarios.Handlers;
using ContextSync.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ContextSync.Scenarios;

public sealed class ScenarioRunResult
{
    public bool HasErrors { get; internal set; }
    public bool Aborted { get; internal set; }
    public List<string> Warnings { get; } = new();
    public int StepsOk { get; internal set; }
    public int StepsFailed { get; internal set; }
}

public sealed class ScenarioRunner
{
    private readonly ScenarioDefinition _definition;
    private readonly IAnsiConsole _console;
    private readonly bool _dryRun;
    private readonly JiraSettings? _jiraFallback;
    private IJiraService? _jiraService;
    private ScenarioContext? _context;

    public ScenarioRunner(ScenarioDefinition definition, IAnsiConsole console, bool dryRun, JiraSettings? jiraFallback = null)
    {
        _definition = definition;
        _console = console;
        _dryRun = dryRun;
        _jiraFallback = jiraFallback;
    }

    public ScriptObject? Root => _context?.Root;

    public async Task<ScenarioRunResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var result = new ScenarioRunResult();
        var context = new ScenarioContext(_definition);
        _context = context;

        var handlers = CreateHandlers();

        if (_dryRun)
            _console.MarkupLine("[grey]DRY-RUN: read-only mode; pass --apply to perform writes to Jira[/]");
        _console.WriteLine();

        for (var index = 0; index < _definition.Steps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var step = _definition.Steps[index];
            var stepId = step.Id.Length > 0 ? step.Id : step.Action;

            var exec = new ScenarioExecContext
            {
                Console = _console,
                Root = context.Root,
                Definition = _definition,
                Scenario = context,
                DryRun = _dryRun,
                GetJira = () => GetOrCreateJira(result)
            };

            try
            {
                var handler = GetHandler(handlers, step, stepId);
                object? output;
                if (step.ForEach != null)
                    output = await ExecuteForEachAsync(step, handler, exec, cancellationToken);
                else
                    output = await handler.ExecuteAsync(step, exec, cancellationToken);

                context.SetStepResult(step.Id.Length > 0 ? step.Id : step.Action, output);
                result.StepsOk++;

                PrintStepStatus(stepId, step, exec, output, context);
            }
            catch (Exception ex)
            {
                result.StepsFailed++;
                result.HasErrors = true;
                var message = ex.InnerException != null ? $"{ex.Message} | {ex.InnerException.Message}" : ex.Message;

                Log.Error(ex, "Scenario step failed: {StepId} ({Action})", stepId, step.Action);
                result.Warnings.Add($"[{stepId}] {message}");

                if (!IsContinue(step.OnError))
                {
                    result.Aborted = true;
                    _console.MarkupLine($"[red]Step failed (aborting pipeline): [[{stepId.EscapeMarkup()}]] {message.EscapeMarkup()}[/]");
                    break;
                }

                _console.MarkupLine($"[red]Step failed (continue): [[{stepId.EscapeMarkup()}]] {message.EscapeMarkup()}[/]");
            }

            foreach (var warning in exec.Warnings)
                result.Warnings.Add(warning);
        }

        PrintWarnings(result);
        return result;
    }

    private async Task<object?> ExecuteForEachAsync(ScenarioStep step, IScenarioStepHandler handler, ScenarioExecContext exec, CancellationToken cancellationToken)
    {
        var items = _context!.GetListPath(step.ForEach!)
            ?? throw new ScenarioStepException($"step '{step.Id}': for_each path '{step.ForEach}' does not resolve to a list");

        var perItem = new List<ScriptObject>();
        var flat = new List<ScriptObject>();
        var failures = 0;

        await _console.Status().StartAsync($"[yellow]{step.Id}: {items.Count} items[/]", async status =>
        {
            for (var i = 0; i < items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                status.Status($"[yellow]{step.Id}: {i + 1}/{items.Count}[/]");
                exec.Root["item"] = items[i];

                try
                {
                    var row = await handler.ExecuteAsync(step, exec, cancellationToken);
                    MergeItem(row, perItem, flat, step.Flatten);
                    if (step.Flatten && row is ScriptObject so && IsFailureRow(so))
                        failures++;
                }
                catch
                {
                    failures++;
                    throw;   // Let the main loop deal with on_error semantics
                }
            }
        });

        if (step.Flatten)
            return flat.Cast<object?>().ToList();

        return new ScriptObject
        {
            ["results"] = perItem.Cast<object?>().ToList(),
            ["count"] = perItem.Count,
            ["failures"] = failures
        };
    }

    private static void MergeItem(object? row, List<ScriptObject> perItem, List<ScriptObject> flat, bool flatten)
    {
        if (row is ScriptObject so)
        {
            perItem.Add(so);
            if (flatten)
                flat.Add(so);
            return;
        }

        if (row is List<ScriptObject> list)
        {
            perItem.Add(new ScriptObject { ["count"] = list.Count });
            flat.AddRange(list);
            return;
        }

        if (row is List<object?> generic)
        {
            perItem.Add(new ScriptObject { ["count"] = generic.Count });
            flat.AddRange(generic.OfType<ScriptObject>());
            return;
        }

        perItem.Add(new ScriptObject());
    }

    private static bool IsFailureRow(ScriptObject row) =>
        row.TryGetValue("status", out var status) && status is string s && s.Equals("error", StringComparison.OrdinalIgnoreCase);

    private static IScenarioStepHandler GetHandler(Dictionary<string, IScenarioStepHandler> handlers, ScenarioStep step, string stepId)
    {
        if (!handlers.TryGetValue(step.Action, out var handler))
            throw new ScenarioStepException($"step '{stepId}': no handler for action '{step.Action}'");
        return handler;
    }

    private static bool IsContinue(string onError)
    {
        var value = onError.Trim().ToLowerInvariant();
        return value.Equals("continue", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, IScenarioStepHandler> CreateHandlers() => new(StringComparer.Ordinal)
    {
        ["jira.test_connection"] = new JiraTestConnectionHandler(),
        ["jira.search"] = new JiraSearchHandler(),
        ["jira.get_worklogs"] = new JiraGetWorklogsHandler(),
        ["jira.add_worklog"] = new JiraAddWorklogHandler(),
        ["jira.timesheet"] = new JiraTimesheetHandler(),

        ["jira.tempo_worklogs"] = new JiraTempoWorklogsHandler(),
        ["file.parse_sheet"] = new FileParseSheetHandler(),
        ["calendar.workdays"] = new CalendarWorkdaysHandler(),
        ["aggregate.by_day"] = new AggregateByDayHandler(),
        ["report.console"] = new ReportConsoleHandler(),
        ["report.day_totals"] = new ReportDayTotalsHandler(),
        ["report.link"] = new ReportLinkHandler()
    };

    private IJiraService GetOrCreateJira(ScenarioRunResult result)
    {
        if (_jiraService != null)
            return _jiraService;

        var connection = _definition.Connection.Jira
            ?? throw new ScenarioStepException("connection.jira is not defined in this scenario");

        var url = RenderConnectionValue(connection.Url, "url", result);
        var username = RenderConnectionValue(connection.Username, "username", result);
        var token = RenderConnectionValue(connection.Token, "token", result);

        // Creds, проверенные EnsureLoginAsync (login OK), всегда важнее env-шаблона
        if (_jiraFallback is { ApiToken: { Length: > 0 } })
        {
            token = _jiraFallback.ApiToken;
            if (_jiraFallback.Username.Length > 0)
                username = _jiraFallback.Username;
        }

        if (url.Length == 0) url = _jiraFallback?.Url ?? "";
        if (username.Length == 0) username = _jiraFallback?.Username ?? "";
        if (token.Length == 0) token = _jiraFallback?.ApiToken ?? "";

        if (url.Length == 0)
            throw new ScenarioStepException("connection.jira.url is empty: configure connection.jira.url in the scenario YAML or Jira:Url in appsettings.json");

        var services = new ServiceCollection();
        services.AddHttpClient("jira").AddStandardResilienceHandler();
        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("jira");

        var settings = new JiraSettings
        {
            Url = url,
            Username = username,
            ApiToken = token,
            TimeoutSeconds = connection.TimeoutSeconds,
            MaxRetries = connection.MaxRetries
        };

        _jiraService = new JiraService(settings, client);
        return _jiraService;
    }

    private string RenderConnectionValue(string value, string fieldName, ScenarioRunResult result)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOf("{{", StringComparison.Ordinal) < 0)
            return value;

        try
        {
            return ScribanEvaluator.RenderString(value, _context!.Root);
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"connection.{fieldName}: template error: {ex.Message}");
            return "";
        }
    }

    private void PrintStepStatus(string stepId, ScenarioStep step, ScenarioExecContext exec, object? output, ScenarioContext context)
    {
        var itemsInfo = step.ForEach != null ? " (list)" : "";

        if (step.Action == "jira.add_worklog")
        {
            var rows = context.GetListPath(step.Flatten ? step.Id : $"{step.Id}.results");
            var count = rows?.Count ?? 0;
            var errors = rows?.Count(r => r is ScriptObject so && so.TryGetValue("status", out var status) && status is string s && s.Equals("error", StringComparison.OrdinalIgnoreCase)) ?? 0;
            _console.MarkupLine($"[green]OK[/] [bold]{stepId.EscapeMarkup()}[/] [grey]({step.Action}){itemsInfo}[/] — {count} item(s){(errors > 0 ? $", [red]{errors} error(s)[/]" : "")}");
            return;
        }

        var value = output switch
        {
            ScriptObject so when so.ContainsKey("count") => $"{so["count"]}",
            System.Collections.IList list => $"{list.Count}",
            _ => "done"
        };

        _console.MarkupLine($"[green]OK[/] [bold]{stepId.EscapeMarkup()}[/] [grey]({step.Action})[/] {(itemsInfo.Length > 0 ? itemsInfo + " " : "")}[grey]{value}[/]");
    }

    private void PrintWarnings(ScenarioRunResult result)
    {
        if (result.Warnings.Count == 0)
            return;

        _console.WriteLine();
        var rule = new Rule("[yellow]Warnings[/]").RuleStyle("yellow dim").LeftJustified();
        _console.Write(rule);
        foreach (var warning in result.Warnings)
            _console.MarkupLine($"[yellow]- {warning.EscapeMarkup()}[/]");
    }
}
