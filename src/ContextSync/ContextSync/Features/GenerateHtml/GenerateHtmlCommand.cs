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

namespace ContextSync.Features.GenerateHtml;

public class GenerateHtmlCommand : AsyncCommand<GenerateHtmlSettings>
{
    private readonly IAnsiConsole _console;

    public GenerateHtmlCommand(IAnsiConsole console)
    {
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, GenerateHtmlSettings settings)
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

        _console.Write(new Rule("[bold yellow]Generate HTML[/]").RuleStyle("yellow").Centered());
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
            _console.MarkupLine("[yellow]No documents found. Nothing to generate.[/]");
            return 0;
        }

        foreach (var doc in documents)
        {
            if (string.IsNullOrEmpty(doc.HtmlTemplate))
                doc.HtmlTemplate = settings.Template;
        }

        var htmlService = provider.GetRequiredService<IHtmlGeneratorService>();
        var results = new List<HtmlFileResult>();

        await _console.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Generating HTML files...[/]");
                task.MaxValue(documents.Count);

                foreach (var doc in documents)
                {
                    var result = await htmlService.GenerateAsync(doc, settings.OutputDirectory, doc.HtmlTemplate);
                    results.Add(result);
                    task.Increment(1);
                }
            });

        _console.WriteLine();
        var table = new Table().RoundedBorder();
        table.AddColumn("Status");
        table.AddColumn("Title");
        table.AddColumn("File Path");
        table.AddColumn("Error");

        foreach (var r in results)
        {
            table.AddRow(
                r.Success ? "[green]OK[/]" : "[red]FAIL[/]",
                r.Title,
                r.FilePath ?? "-",
                r.Error ?? ""
            );
        }

        _console.Write(table);

        var ok = results.Count(r => r.Success);
        var fail = results.Count(r => !r.Success);
        _console.WriteLine();
        _console.MarkupLine(ok > 0 ? $"[green]Generated: {ok} HTML files in {settings.OutputDirectory}[/]" : "[yellow]No files generated[/]");
        if (fail > 0)
            _console.MarkupLine($"[red]Failed: {fail} files[/]");

        Log.Information("GenerateHtml completed: {Ok} ok, {Fail} errors, output={Dir}", ok, fail, settings.OutputDirectory);
        return fail > 0 ? 1 : 0;
    }
}
