using Spectre.Console;
using Spectre.Console.Cli;
using ContextSync.Abstractions;
using ContextSync.dal.Abstractions;
using ContextSync.dal.Models;
using ContextSync.dal.Readers;
using ContextSync.Infrastructure;
using ContextSync.Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ContextSync.Features.SyncJira;

public class SyncJiraCommand : AsyncCommand<SyncJiraSettings>
{
    private readonly IAnsiConsole _console;

    public SyncJiraCommand(IAnsiConsole console)
    {
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SyncJiraSettings settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        var appSettings = new AppSettings();
        configuration.Bind(appSettings);

        if (settings.FromDatabase)
        {
            if (!string.IsNullOrEmpty(settings.Server))
                appSettings.Database.Server = settings.Server;
            if (!string.IsNullOrEmpty(settings.Database))
                appSettings.Database.Database = settings.Database;
            if (!string.IsNullOrEmpty(settings.Query))
                appSettings.Database.QueryText = settings.Query;
        }

        if (!string.IsNullOrEmpty(settings.InputDirectory))
            appSettings.FileSource.InputDirectory = settings.InputDirectory;
        if (!string.IsNullOrEmpty(settings.FilePattern))
            appSettings.FileSource.FilePattern = settings.FilePattern;

        var provider = settings.FromDatabase
            ? DependencyInjection.ConfigureServicesWithDbReader(configuration)
            : DependencyInjection.ConfigureServices(configuration);

        _console.Write(new Rule("[bold blue]Sync to Jira[/]").RuleStyle("blue").Centered());
        _console.WriteLine();

        var jiraSettings = provider.GetRequiredService<JiraSettings>();
        if (string.IsNullOrEmpty(jiraSettings.Url))
        {
            _console.MarkupLine("[red]Jira URL is not configured. Set it in appsettings.json or environment variables.[/]");
            return 1;
        }

        await _console.Status()
            .StartAsync("[yellow]Testing Jira connection...[/]", async ctx =>
            {
                var jiraService = provider.GetRequiredService<IJiraService>();
                var connected = await jiraService.TestConnectionAsync();
                if (!connected)
                {
                    _console.MarkupLine("[red]Cannot connect to Jira. Check URL and credentials.[/]");
                    throw new Exception("Jira connection failed");
                }
                _console.MarkupLine("[green]Jira connection OK[/]");
            });

        var documents = new List<SourceDocument>();

        if (settings.FromDatabase)
        {
            await _console.Status()
                .StartAsync("[yellow]Reading from database...[/]", async ctx =>
                {
                    var dbReader = provider.GetRequiredService<DatabaseSourceReader>();
                    documents = await dbReader.ReadAsync();
                    _console.MarkupLine($"[green]Read {documents.Count} documents from database[/]");
                });
        }
        else
        {
            await _console.Status()
                .StartAsync("[yellow]Reading input files...[/]", async ctx =>
                {
                    var fileReader = provider.GetRequiredService<ISourceReader>();
                    documents = await fileReader.ReadAsync();
                    _console.MarkupLine($"[green]Read {documents.Count} files[/]");
                });
        }

        if (documents.Count == 0)
        {
            _console.MarkupLine("[yellow]No documents found. Nothing to sync.[/]");
            return 0;
        }

        ApplyOverrides(documents, settings, jiraSettings);

        var table = new Table().RoundedBorder();
        table.AddColumn("Title");
        table.AddColumn("Project");
        table.AddColumn("Type");
        table.AddColumn("Assignee");

        foreach (var doc in documents)
        {
            table.AddRow(
                doc.Title.Length > 50 ? doc.Title[..47] + "..." : doc.Title,
                doc.JiraProjectKey ?? jiraSettings.DefaultProjectKey,
                doc.JiraIssueType ?? jiraSettings.DefaultIssueType,
                doc.JiraAssignee ?? "-"
            );
        }

        _console.Write(table);
        _console.WriteLine();

        var confirmed = _console.Confirm("[bold]Proceed with creating Jira issues?[/]");
        if (!confirmed)
        {
            _console.MarkupLine("[yellow]Cancelled by user.[/]");
            return 0;
        }

        var jiraService = provider.GetRequiredService<IJiraService>();
        var results = new List<JiraIssueResult>();

        await _console.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Creating Jira issues...[/]");
                task.MaxValue(documents.Count);

                foreach (var doc in documents)
                {
                    var result = await jiraService.CreateIssueAsync(doc);
                    results.Add(result);
                    task.Increment(1);
                }
            });

        _console.WriteLine();
        var resultTable = new Table().RoundedBorder();
        resultTable.AddColumn("Status");
        resultTable.AddColumn("Key");
        resultTable.AddColumn("URL");
        resultTable.AddColumn("Error");

        foreach (var r in results)
        {
            resultTable.AddRow(
                r.Success ? "[green]OK[/]" : "[red]FAIL[/]",
                r.Key,
                r.Url,
                r.Error ?? ""
            );
        }

        _console.Write(resultTable);

        var ok = results.Count(r => r.Success);
        var fail = results.Count(r => !r.Success);
        _console.WriteLine();
        _console.MarkupLine(ok > 0 ? $"[green]Created: {ok} issues[/]" : "[yellow]No issues created[/]");
        if (fail > 0)
            _console.MarkupLine($"[red]Failed: {fail} issues[/]");

        Log.Information("SyncJira completed: {Ok} ok, {Fail} errors", ok, fail);
        return fail > 0 ? 1 : 0;
    }

    private static void ApplyOverrides(List<SourceDocument> documents, SyncJiraSettings settings, JiraSettings jiraSettings)
    {
        foreach (var doc in documents)
        {
            if (!string.IsNullOrEmpty(settings.ProjectKey))
                doc.JiraProjectKey = settings.ProjectKey;
            if (!string.IsNullOrEmpty(settings.IssueType))
                doc.JiraIssueType = settings.IssueType;
            if (!string.IsNullOrEmpty(settings.Assignee))
                doc.JiraAssignee = settings.Assignee;
            if (!string.IsNullOrEmpty(settings.EpicKey))
                doc.JiraEpicKey = settings.EpicKey;
            if (!string.IsNullOrEmpty(settings.Priority))
                doc.JiraPriority = settings.Priority;
            if (!string.IsNullOrEmpty(settings.Labels))
                doc.JiraLabels = settings.Labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            if (string.IsNullOrEmpty(doc.JiraProjectKey))
                doc.JiraProjectKey = jiraSettings.DefaultProjectKey;
            if (string.IsNullOrEmpty(doc.JiraIssueType))
                doc.JiraIssueType = jiraSettings.DefaultIssueType;
            if (string.IsNullOrEmpty(doc.JiraPriority))
                doc.JiraPriority = jiraSettings.DefaultPriority;
        }
    }
}
