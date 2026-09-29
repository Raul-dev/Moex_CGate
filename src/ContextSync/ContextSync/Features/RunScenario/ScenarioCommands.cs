using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli;
using ContextSync.Infrastructure.Settings;
using ContextSync.Scenarios;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace ContextSync.Features.RunScenario;

public class ScenarioSettings : CommandSettings
{
    [CommandOption("--dir")]
    public string? TemplatesDir { get; set; }
}

public sealed class RunScenarioSettings : ScenarioSettings
{
    [CommandArgument(0, "<name|path>")]
    public string Name { get; set; } = "";

    [CommandOption("--file")]
    public string? File { get; set; }

    [CommandOption("--user")]
    public string? User { get; set; }

    [CommandOption("--weeks")]
    public int? Weeks { get; set; }

    [CommandOption("--expected")]
    public double? ExpectedHours { get; set; }

    [CommandOption("--from")]
    public string? From { get; set; }

    [CommandOption("--to")]
    public string? To { get; set; }

    [CommandOption("--mode")]
    public string? Mode { get; set; }

    [CommandOption("--max-hours")]
    public double? MaxHours { get; set; }

    [CommandOption("--dry-run")]
    public bool DryRun { get; set; }

    [CommandOption("--apply")]
    public bool Apply { get; set; }
}

public sealed class ListScenarioSettings : ScenarioSettings
{
}

public sealed class ValidateScenarioSettings : ScenarioSettings
{
    [CommandArgument(0, "<name|path>")]
    public string Name { get; set; } = "";
}

public sealed class RunScenarioCommand : AsyncCommand<RunScenarioSettings>
{
    private readonly IAnsiConsole _console;

    public RunScenarioCommand(IAnsiConsole console)
    {
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, RunScenarioSettings settings)
    {
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var appSettings = new AppSettings();
            configuration.Bind(appSettings);

            var path = ScenarioLoader.ResolvePath(settings.Name, settings.TemplatesDir);
            var definition = ScenarioLoader.Load(path, BuildOverrides(settings));
            var dryRun = !settings.Apply;

            if (definition.Connection.Jira != null && NeedsJiraAccess(definition, dryRun))
            {
                var loggedIn = await JiraCredentials.EnsureLoginAsync(
                    _console, definition.Connection.Jira, appSettings.Jira, path, CancellationToken.None);
                if (!loggedIn)
                    return 1;
            }

            _console.Write(new Rule($"[bold blue]Scenario: {definition.Name.EscapeMarkup()}[/]").RuleStyle("blue").LeftJustified());
            if (definition.Description.Length > 0)
                _console.MarkupLine($"[grey]{definition.Description.EscapeMarkup()}[/]");
            _console.MarkupLine($"[grey]{path.EscapeMarkup()}[/]");
            _console.WriteLine();

            PrintPlan(definition);

            var runner = new ScenarioRunner(definition, _console, dryRun: !settings.Apply, appSettings.Jira);
            var result = await runner.RunAsync();

            _console.WriteLine();
            var summary = new Rule("[bold]Summary[/]").RuleStyle("grey dim").LeftJustified();
            _console.Write(summary);
            _console.MarkupLine($"[grey]Steps:[/] OK {result.StepsOk} / Failed {result.StepsFailed}{(result.Aborted ? " [red](aborted)[/]" : "")}");

            Log.Information("Scenario '{Name}' finished: ok={Ok} failed={Failed} aborted={Aborted}", definition.Name, result.StepsOk, result.StepsFailed, result.Aborted);
            return result.HasErrors ? 1 : 0;
        }
        catch (ScenarioLoadException ex)
        {
            _console.MarkupLine($"[red]{ex.Message.EscapeMarkup()}[/]");
            Log.Error(ex, "Scenario load/validate failed");
            return 1;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Scenario run failed");
            _console.WriteException(ex);
            return 1;
        }
    }

    private static Dictionary<string, string?> BuildOverrides(RunScenarioSettings settings)
    {
        var overrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(settings.File)) overrides["file"] = settings.File;
        if (!string.IsNullOrWhiteSpace(settings.User)) overrides["user"] = settings.User;
        if (settings.Weeks.HasValue) overrides["num_of_weeks"] = settings.Weeks.Value.ToString(CultureInfo.InvariantCulture);
        if (settings.ExpectedHours.HasValue) overrides["expected_hours_day"] = settings.ExpectedHours.Value.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(settings.From)) overrides["from"] = settings.From;
        if (!string.IsNullOrWhiteSpace(settings.To)) overrides["to"] = settings.To;
        if (!string.IsNullOrWhiteSpace(settings.Mode)) overrides["mode"] = settings.Mode;
        if (settings.MaxHours.HasValue) overrides["max_hours"] = settings.MaxHours.Value.ToString(CultureInfo.InvariantCulture);

        return overrides;
    }

    private static bool IsWriteScenarioNeeded(ScenarioDefinition definition) =>
        definition.Steps.Any(step => step.Action == "jira.add_worklog");

    private static bool NeedsJiraAccess(ScenarioDefinition definition, bool dryRun) =>
        definition.Steps.Any(step =>
            step.Action.StartsWith("jira.", StringComparison.Ordinal) &&
            !(step.Action == "jira.add_worklog" && dryRun));

    private void PrintPlan(ScenarioDefinition definition)
    {
        var table = new Table().RoundedBorder();
        table.AddColumn("#");
        table.AddColumn("Id");
        table.AddColumn("Action");
        table.AddColumn("For Each");

        for (var i = 0; i < definition.Steps.Count; i++)
        {
            var step = definition.Steps[i];
            table.AddRow(
                (i + 1).ToString(),
                string.IsNullOrEmpty(step.Id) ? "-" : step.Id,
                step.Action,
                step.ForEach ?? "-");
        }

        _console.Write(table);
        _console.WriteLine();
    }
}

public sealed class ListScenarioCommand : Command<ListScenarioSettings>
{
    private readonly IAnsiConsole _console;

    public ListScenarioCommand(IAnsiConsole console)
    {
        _console = console;
    }

    public override int Execute(CommandContext context, ListScenarioSettings settings)
    {
        var dir = settings.TemplatesDir ?? ScenarioLoader.DefaultTemplatesDir;

        if (!Directory.Exists(dir))
        {
            _console.MarkupLine($"[yellow]Templates directory not found: {MarkupPath(dir)}[/]");
            _console.MarkupLine("[grey]Create YAML scenario files in this directory.[/]");
            return 1;
        }

        var scenarios = ScenarioLoader.ListTemplates(dir);
        if (scenarios.Count == 0)
        {
            _console.MarkupLine($"[yellow]No YAML scenarios found in {MarkupPath(dir)}[/]");
            return 1;
        }

        var table = new Table().RoundedBorder();
        table.Title = new TableTitle($"Scenario templates: {dir}");
        table.AddColumn("Name");
        table.AddColumn("Description");
        table.AddColumn("File");

        foreach (var (name, description, path) in scenarios)
            table.AddRow(
                name.EscapeMarkup(),
                string.IsNullOrEmpty(description) ? "-" : description.EscapeMarkup(),
                path.EscapeMarkup());

        _console.Write(table);
        return 0;
    }

    private static string MarkupPath(string value) => value.EscapeMarkup();
}

public sealed class ValidateScenarioCommand : Command<ValidateScenarioSettings>
{
    private readonly IAnsiConsole _console;

    public ValidateScenarioCommand(IAnsiConsole console)
    {
        _console = console;
    }

    public override int Execute(CommandContext context, ValidateScenarioSettings settings)
    {
        try
        {
            var path = ScenarioLoader.ResolvePath(settings.Name, settings.TemplatesDir);
            var definition = ScenarioLoader.Load(path);

            _console.MarkupLine($"[green]Scenario '{definition.Name.EscapeMarkup()}' is valid: {definition.Steps.Count} step(s)[/]");
            _console.MarkupLine($"[grey]{MarkupPath(path)}[/]");
            return 0;
        }
        catch (ScenarioLoadException ex)
        {
            _console.MarkupLine($"[red]{ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]{ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }

    private static string MarkupPath(string path) => System.IO.Path.GetFileName(path).EscapeMarkup();
}
