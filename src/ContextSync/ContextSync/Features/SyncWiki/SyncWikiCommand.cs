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

namespace ContextSync.Features.SyncWiki;

public class SyncWikiCommand : AsyncCommand<SyncWikiSettings>
{
    private readonly IAnsiConsole _console;

    public SyncWikiCommand(IAnsiConsole console)
    {
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SyncWikiSettings settings)
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

        _console.Write(new Rule("[bold green]Sync to Confluence Wiki[/]").RuleStyle("green").Centered());
        _console.WriteLine();

        var confluenceSettings = provider.GetRequiredService<ConfluenceSettings>();
        if (string.IsNullOrEmpty(confluenceSettings.Url))
        {
            _console.MarkupLine("[red]Confluence URL is not configured. Set it in appsettings.json or environment variables.[/]");
            return 1;
        }

        await _console.Status()
            .StartAsync("[yellow]Testing Confluence connection...[/]", async ctx =>
            {
                var confluenceService = provider.GetRequiredService<IConfluenceService>();
                var connected = await confluenceService.TestConnectionAsync();
                if (!connected)
                {
                    _console.MarkupLine("[red]Cannot connect to Confluence. Check URL and credentials.[/]");
                    throw new Exception("Confluence connection failed");
                }
                _console.MarkupLine("[green]Confluence connection OK[/]");
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

        ApplyOverrides(documents, settings, confluenceSettings);

        var table = new Table().RoundedBorder();
        table.AddColumn("Title");
        table.AddColumn("Space");
        table.AddColumn("Parent");

        foreach (var doc in documents)
        {
            table.AddRow(
                doc.Title.Length > 50 ? doc.Title[..47] + "..." : doc.Title,
                doc.WikiSpaceKey ?? confluenceSettings.DefaultSpaceKey,
                doc.WikiParentTitle ?? confluenceSettings.DefaultParentTitle ?? "-"
            );
        }

        _console.Write(table);
        _console.WriteLine();

        var confirmed = _console.Confirm("[bold]Proceed with creating wiki pages?[/]");
        if (!confirmed)
        {
            _console.MarkupLine("[yellow]Cancelled by user.[/]");
            return 0;
        }

        var confluenceService = provider.GetRequiredService<IConfluenceService>();
        var results = new List<WikiPageResult>();

        await _console.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Creating wiki pages...[/]");
                task.MaxValue(documents.Count);

                foreach (var doc in documents)
                {
                    var result = await confluenceService.CreatePageAsync(doc);
                    results.Add(result);
                    task.Increment(1);
                }
            });

        _console.WriteLine();
        var resultTable = new Table().RoundedBorder();
        resultTable.AddColumn("Status");
        resultTable.AddColumn("Title");
        resultTable.AddColumn("Page ID");
        resultTable.AddColumn("URL");
        resultTable.AddColumn("Error");

        foreach (var r in results)
        {
            resultTable.AddRow(
                r.Success ? "[green]OK[/]" : "[red]FAIL[/]",
                r.Title,
                r.PageId?.ToString() ?? "-",
                r.Url,
                r.Error ?? ""
            );
        }

        _console.Write(resultTable);

        var ok = results.Count(r => r.Success);
        var fail = results.Count(r => !r.Success);
        _console.WriteLine();
        _console.MarkupLine(ok > 0 ? $"[green]Created: {ok} pages[/]" : "[yellow]No pages created[/]");
        if (fail > 0)
            _console.MarkupLine($"[red]Failed: {fail} pages[/]");

        Log.Information("SyncWiki completed: {Ok} ok, {Fail} errors", ok, fail);
        return fail > 0 ? 1 : 0;
    }

    private static void ApplyOverrides(List<SourceDocument> documents, SyncWikiSettings settings, ConfluenceSettings confluenceSettings)
    {
        foreach (var doc in documents)
        {
            if (!string.IsNullOrEmpty(settings.SpaceKey))
                doc.WikiSpaceKey = settings.SpaceKey;
            if (!string.IsNullOrEmpty(settings.ParentTitle))
                doc.WikiParentTitle = settings.ParentTitle;

            if (string.IsNullOrEmpty(doc.WikiSpaceKey))
                doc.WikiSpaceKey = confluenceSettings.DefaultSpaceKey;
        }
    }
}
