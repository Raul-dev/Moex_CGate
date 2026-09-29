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

namespace ContextSync.Features.SyncAll;

public class SyncAllCommand : AsyncCommand<SyncAllSettings>
{
    private readonly IAnsiConsole _console;

    public SyncAllCommand(IAnsiConsole console)
    {
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SyncAllSettings settings)
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

        var syncJira = !settings.SkipJira;
        var syncWiki = !settings.SkipWiki;
        var generateHtml = !settings.SkipHtml;

        _console.Write(new Rule("[bold magenta]Sync All[/]").RuleStyle("magenta").Centered());
        _console.WriteLine();

        var panel = new Panel(new Markup(
            $"[grey]Jira:[/]     {(syncJira ? "[green]ENABLED[/]" : "[red]disabled[/]")}\n" +
            $"[grey]Wiki:[/]    {(syncWiki ? "[green]ENABLED[/]" : "[red]disabled[/]")}\n" +
            $"[grey]HTML:[/]    {(generateHtml ? "[green]ENABLED[/]" : "[red]disabled[/]")}"))
        {
            Header = new PanelHeader("Sync Targets"),
            Border = BoxBorder.Rounded
        };
        _console.Write(panel);
        _console.WriteLine();

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

        var jiraSettings = provider.GetRequiredService<JiraSettings>();
        foreach (var doc in documents)
        {
            if (string.IsNullOrEmpty(doc.JiraProjectKey))
                doc.JiraProjectKey = jiraSettings.DefaultProjectKey;
            if (string.IsNullOrEmpty(doc.JiraIssueType))
                doc.JiraIssueType = jiraSettings.DefaultIssueType;
            if (string.IsNullOrEmpty(doc.JiraPriority))
                doc.JiraPriority = jiraSettings.DefaultPriority;
        }

        var confirmed = _console.Confirm("[bold]Proceed with sync?[/]");
        if (!confirmed)
        {
            _console.MarkupLine("[yellow]Cancelled by user.[/]");
            return 0;
        }

        var orchestrator = provider.GetRequiredService<ISyncOrchestrator>();
        SyncResult? result = null;

        await _console.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Syncing all targets...[/]");
                task.MaxValue(3);

                if (syncJira) task.Description = "[green]Syncing Jira...[/]";
                if (syncWiki) task.Description = "[green]Syncing Wiki...[/]";
                if (generateHtml) task.Description = "[green]Generating HTML...[/]";

                result = await orchestrator.SyncAllAsync(
                    documents,
                    syncJira,
                    syncWiki,
                    generateHtml,
                    settings.HtmlOutputDirectory);

                task.Increment(3);
            });

        _console.WriteLine();
        _console.Write(new Rule("[bold]Results[/]").RuleStyle("grey").Centered());
        _console.WriteLine();

        if (result == null)
        {
            _console.MarkupLine("[red]Sync failed unexpectedly.[/]");
            return 1;
        }

        if (syncJira && result.JiraResults.Count > 0)
        {
            _console.MarkupLine("[bold blue]Jira Issues:[/]");
            var jiraTable = new Table().RoundedBorder();
            jiraTable.AddColumn("Status");
            jiraTable.AddColumn("Key");
            jiraTable.AddColumn("URL");
            jiraTable.AddColumn("Error");
            foreach (var r in result.JiraResults)
            {
                jiraTable.AddRow(
                    r.Success ? "[green]OK[/]" : "[red]FAIL[/]",
                    r.Key,
                    r.Url,
                    r.Error ?? "");
            }
            _console.Write(jiraTable);
            _console.WriteLine();
        }

        if (syncWiki && result.WikiResults.Count > 0)
        {
            _console.MarkupLine("[bold green]Wiki Pages:[/]");
            var wikiTable = new Table().RoundedBorder();
            wikiTable.AddColumn("Status");
            wikiTable.AddColumn("Title");
            wikiTable.AddColumn("Page ID");
            wikiTable.AddColumn("URL");
            wikiTable.AddColumn("Error");
            foreach (var r in result.WikiResults)
            {
                wikiTable.AddRow(
                    r.Success ? "[green]OK[/]" : "[red]FAIL[/]",
                    r.Title,
                    r.PageId?.ToString() ?? "-",
                    r.Url,
                    r.Error ?? "");
            }
            _console.Write(wikiTable);
            _console.WriteLine();
        }

        if (generateHtml && result.HtmlResults.Count > 0)
        {
            _console.MarkupLine("[bold yellow]HTML Files:[/]");
            var htmlTable = new Table().RoundedBorder();
            htmlTable.AddColumn("Status");
            htmlTable.AddColumn("Title");
            htmlTable.AddColumn("File Path");
            htmlTable.AddColumn("Error");
            foreach (var r in result.HtmlResults)
            {
                htmlTable.AddRow(
                    r.Success ? "[green]OK[/]" : "[red]FAIL[/]",
                    r.Title,
                    r.FilePath ?? "-",
                    r.Error ?? "");
            }
            _console.Write(htmlTable);
            _console.WriteLine();
        }

        _console.Write(new Rule().RuleStyle("grey"));
        _console.MarkupLine($"[bold]Summary:[/]  {result.TotalDocuments} documents processed");
        if (syncJira)
            _console.MarkupLine($"  Jira:  [green]{result.JiraResults.Count(r => r.Success)} ok[/]  [red]{result.JiraResults.Count(r => !r.Success)} failed[/]");
        if (syncWiki)
            _console.MarkupLine($"  Wiki:  [green]{result.WikiResults.Count(r => r.Success)} ok[/]  [red]{result.WikiResults.Count(r => !r.Success)} failed[/]");
        if (generateHtml)
            _console.MarkupLine($"  HTML:  [green]{result.HtmlResults.Count(r => r.Success)} ok[/]  [red]{result.HtmlResults.Count(r => !r.Success)} failed[/]");

        var totalErrors = result.JiraResults.Count(r => !r.Success)
            + result.WikiResults.Count(r => !r.Success)
            + result.HtmlResults.Count(r => !r.Success);

        Log.Information("SyncAll completed: {Total} docs, {Errors} total errors", result.TotalDocuments, totalErrors);
        return totalErrors > 0 ? 1 : 0;
    }
}
