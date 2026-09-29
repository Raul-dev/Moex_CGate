using System.Text;
using Spectre.Console;
using Spectre.Console.Cli;
using ContextSync.dal.Abstractions;
using ContextSync.dal.Models;
using ContextSync.dal.Readers;
using ContextSync.dal.Settings;
using ContextSync.Infrastructure.Settings;
using ContextSync.Services;
using Serilog;

namespace ContextSync.Features.SchemaHtml;

public class SchemaHtmlCommand : AsyncCommand<SchemaHtmlSettings>
{
    private readonly IAnsiConsole _console;

    public SchemaHtmlCommand(IAnsiConsole console)
    {
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SchemaHtmlSettings settings)
    {
        if (string.IsNullOrEmpty(settings.Server) || string.IsNullOrEmpty(settings.Database))
        {
            _console.MarkupLine("[red]Server (-s) and Database (-d) are required.[/]");
            return 1;
        }

        _console.Write(new Rule("[bold cyan]Schema -> HTML[/]").RuleStyle("cyan").Centered());
        _console.WriteLine();

        var dbSettings = new DatabaseSettings
        {
            Server = settings.Server,
            Database = settings.Database,
            IntegratedSecurity = string.IsNullOrEmpty(settings.User),
            User = settings.User ?? "",
            Password = settings.Password ?? "",
            CommandTimeoutSeconds = settings.TimeoutSeconds
        };

        var schemaReader = new SchemaReader(dbSettings);
        var templateRenderer = new ScribanTemplateRenderer(settings.TemplatesDir);

        _console.MarkupLine($"[grey]Server:[/]   {settings.Server}");
        _console.MarkupLine($"[grey]Database:[/] {settings.Database}");
        if (!string.IsNullOrEmpty(settings.TableName))
            _console.MarkupLine($"[grey]Table:[/]    {settings.TableName}");
        else if (!string.IsNullOrEmpty(settings.TableFilter))
            _console.MarkupLine($"[grey]Filter:[/]   {settings.TableFilter}");
        else
            _console.MarkupLine("[grey]Filter:[/]   * (all user tables)");
        _console.MarkupLine($"[grey]Output:[/]   {settings.OutputDirectory}");
        _console.MarkupLine($"[grey]Template:[/] {settings.TemplateName}");
        _console.WriteLine();

        List<TableSchema> tables = new();

        await _console.Status()
            .StartAsync("[yellow]Connecting and reading schema...[/]", async ctx =>
            {
                tables = await schemaReader.ReadSchemaAsync(settings.TableName, settings.TableFilter);

                if (tables.Count == 0)
                {
                    _console.MarkupLine("[yellow]No tables found matching the criteria.[/]");
                    return;
                }

                _console.MarkupLine($"[green]Found {tables.Count} tables[/]");
            });

        if (tables.Count == 0)
            return 0;

        var preview = new Table().RoundedBorder();
        preview.AddColumn("Table");
        preview.AddColumn("Columns");
        preview.AddColumn("Foreign Keys");

        foreach (var t in tables)
        {
            preview.AddRow(
                t.TableName,
                t.Columns.Count.ToString(),
                t.Columns.Count(c => c.IsForeignKey).ToString()
            );
        }

        _console.Write(preview);
        _console.WriteLine();

        var confirmed = _console.Confirm("[bold]Proceed with generating HTML?[/]");
        if (!confirmed)
        {
            _console.MarkupLine("[yellow]Cancelled by user.[/]");
            return 0;
        }

        if (!Directory.Exists(settings.OutputDirectory))
            Directory.CreateDirectory(settings.OutputDirectory);

        var results = new List<(string Table, string FilePath, bool Success, string? Error)>();

        await _console.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Rendering HTML pages...[/]");
                task.MaxValue(tables.Count);

                foreach (var table in tables)
                {
                    try
                    {
                        var html = templateRenderer.Render(settings.TemplateName, table);

                        var safeName = SanitizeFileName(table.TableName);
                        var filePath = Path.Combine(settings.OutputDirectory, safeName + ".html");
                        await File.WriteAllTextAsync(filePath, html, new UTF8Encoding(false));

                        results.Add((table.TableName, filePath, true, null));
                        Log.Information("Generated HTML for {Table} -> {File}", table.TableName, filePath);
                    }
                    catch (Exception ex)
                    {
                        results.Add((table.TableName, "", false, ex.Message));
                        Log.Error(ex, "Error generating HTML for {Table}", table.TableName);
                    }

                    task.Increment(1);
                }
            });

        _console.WriteLine();
        var resultTable = new Table().RoundedBorder();
        resultTable.AddColumn("Status");
        resultTable.AddColumn("Table");
        resultTable.AddColumn("File Path");
        resultTable.AddColumn("Error");

        foreach (var r in results)
        {
            resultTable.AddRow(
                r.Success ? "[green]OK[/]" : "[red]FAIL[/]",
                r.Table,
                r.FilePath,
                r.Error ?? ""
            );
        }

        _console.Write(resultTable);

        var ok = results.Count(r => r.Success);
        var fail = results.Count(r => !r.Success);
        _console.WriteLine();
        _console.MarkupLine(ok > 0 ? $"[green]Generated: {ok} HTML files in {settings.OutputDirectory}[/]" : "[yellow]No files generated[/]");
        if (fail > 0)
            _console.MarkupLine($"[red]Failed: {fail} tables[/]");

        Log.Information("SchemaHtml completed: {Ok} ok, {Fail} errors", ok, fail);
        return fail > 0 ? 1 : 0;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var c in name)
        {
            sb.Append(invalid.Contains(c) ? '_' : c);
        }
        return sb.ToString();
    }
}
