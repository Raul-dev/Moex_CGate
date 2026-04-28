using System.Text.RegularExpressions;
using ApplyProcLog;
using CommandLine;
using Microsoft.Extensions.Configuration;
using ApplyProcLog.dal;
using Serilog;
using Serilog.Context;

public class ProcedureSettings
{
    public string DefaultFilter { get; set; } = "%";
    public List<string> ExcludeSchemas { get; set; } = new();
    public List<string> Procedures { get; set; } = new();
}

class Program
{
    static async Task<int> Main(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        try
        {
            var parserResult = CommandLine.Parser.Default.ParseArguments<
                CmdOption,
                ExecOptions.ExecFile,
                ExecOptions.ExecFolder,
                ExecOptions.ExecAll,
                ExecOptions.ExportData>(args);

            int result = parserResult.MapResult(
                (CmdOption opts) => LaunchApplyProcLog(opts, configuration).GetAwaiter().GetResult(),
                (ExecOptions.ExecFile opts) => ExecuteFile(opts).GetAwaiter().GetResult(),
                (ExecOptions.ExecFolder opts) => ExecuteFolder(opts).GetAwaiter().GetResult(),
                (ExecOptions.ExecAll opts) => ExecuteAll(opts).GetAwaiter().GetResult(),
                (ExecOptions.ExportData opts) => ExportData(opts).GetAwaiter().GetResult(),
                errs => { HandleParseError(errs); return 1; });

            return result;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    static void HandleParseError(IEnumerable<CommandLine.Error> errs)
    {
        foreach (var err in errs)
        {
            if (err.StopsProcessing) Log.Error(err.ToString());
            else Log.Warning(err.ToString());
        }
        Console.WriteLine("Usage:");
        Console.WriteLine("  ApplyProcLog.exe generate [--clean] [--filter <маска>] [--except <схема исключения>] [--use-config]");
        Console.WriteLine("  ApplyProcLog.exe exec-file -f <path>     Выполнить один SQL-файл");
        Console.WriteLine("  ApplyProcLog.exe exec-folder -f <path>   Выполнить SQL из папки");
        Console.WriteLine("  ApplyProcLog.exe exec-all                Применить Table + Base + Original");
        Console.WriteLine("  ApplyProcLog.exe export-data             Экспорт данных таблиц");
        Console.WriteLine();
        Console.WriteLine("Примеры exec-file:");
        Console.WriteLine("  exec-file -f \"D:\\Temp3\\fileProc.sql\"");
        Console.WriteLine();
        Console.WriteLine("Примеры export-data:");
        Console.WriteLine("  export-data -s localhost -d DBTest -t \"schema.%, dbo.%\" -o D:\\Temp3\\Data");
        Console.WriteLine("  export-data -s localhost -d DBTest -t \"%.Accounts\"");
        Console.WriteLine("  export-data -s localhost -d DBTest -t \"dbo.Users,schema.Accounts\"");
        Console.WriteLine("  export-data -s localhost -d DBTest --max-size 500 --batch-size 5000");
    }

    static async Task<int> LaunchApplyProcLog(CmdOption opts, IConfiguration configuration)
    {
        if (string.IsNullOrEmpty(opts.ExceptFilter))
            opts.ExceptFilter = "audit%";

        var connStr = BuildConnectionString(opts.ServerName, opts.DatabaseName);
		List<StoredProcedureInfo> storedProcedures = new List<StoredProcedureInfo>();
        CancellationToken ct = new CancellationToken();
        DBHelper dbset = new DBHelper(connStr);
        StoredProcedureGenerator spg = new StoredProcedureGenerator();

        if (opts.Clean)
        {
            spg.CleanOutputDirectories();
        }

        if (opts.UseConfig)
        {
            var section = configuration.GetSection("ProcedureSettings");
            var settings = new ProcedureSettings();
            section.Bind(settings);

            if (settings.Procedures.Count > 0)
            {
                Log.Information("Используется список из appsettings.json: {Count} процедур", settings.Procedures.Count);
                foreach (var p in settings.Procedures)
                    Log.Debug("  {Proc}", p);

                var filteredProcedures = settings.Procedures
                    .Where(n => !settings.ExcludeSchemas.Any(s => n.StartsWith(s + ".", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                storedProcedures = await dbset.GetSqlProceduresByNamesAsync(filteredProcedures, ct).ConfigureAwait(false);
            }
            else
            {
                Log.Warning("ProcedureSettings.Procedures в appsettings.json пуст, используется --filter");
                storedProcedures = await dbset.GetSqlProcedures(opts.Filter, ct, opts.ExceptFilter).ConfigureAwait(false);
            }
        }
        else
        {
            storedProcedures = await dbset.GetSqlProcedures(opts.Filter, ct, opts.ExceptFilter).ConfigureAwait(false);
        }

        Log.Information("Найдено процедур: {Count}", storedProcedures.Count);
        spg.CreateProcedureFilesAsync(storedProcedures).Wait();
        return 0;
    }

    static async Task<int> ExecuteFile(ExecOptions.ExecFile opts)
    {
        var connStr = BuildConnectionString(opts.ServerName, opts.DatabaseName);
        var executor = new SqlFileExecutor(connStr, opts.Timeout);

        var fileName = Path.GetFileName(opts.FilePath);
        Log.Information("[{FileName}] ->", fileName);

        try
        {
            await executor.ExecuteFileAsync(opts.FilePath);
            Log.Information("[{FileName}] -> OK (applied)", fileName);
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error("[{FileName}] -> ERROR: {Message}", fileName, ex.Message);
            return 1;
        }
    }

    static async Task<int> ExecuteFolder(ExecOptions.ExecFolder opts)
    {
		var connStr = BuildConnectionString(opts.ServerName, opts.DatabaseName);
		var executor = new SqlFileExecutor(connStr, opts.Timeout);

        var result = await executor.ExecuteFolderAsync(opts.Folder);

        if (result.HasErrors)
        {
            Log.Error("Errors: {Errors}", result.Errors);
            foreach (var msg in result.ErrorMessages)
                Log.Error(msg);
        }

        return result.HasErrors ? 1 : 0;
    }

    static async Task<int> ExecuteAll(ExecOptions.ExecAll opts)
    {
        var procFolder = opts.ProcFolder ?? Path.Combine(Directory.GetCurrentDirectory(), "PROC");
        var connStr = BuildConnectionString(opts.ServerName, opts.DatabaseName);
        var executor = new SqlFileExecutor(connStr, opts.Timeout);

        Log.Information("Применение PROC из {Folder} на {Server}.{DB}", procFolder, opts.ServerName, opts.DatabaseName);

        if (!opts.NoTable)
        {
            var tableFolder = Path.Combine(procFolder, "Table");
            if (Directory.Exists(tableFolder))
            {
                Log.Information("=== Применение Table ===");
                await executor.ExecuteFolderAsync(tableFolder);
            }
            else
            {
                Log.Warning("Папка Table не найдена: {Path}", tableFolder);
            }
        }

        if (!opts.NoBase)
        {
            var baseFolder = Path.Combine(procFolder, "Base");
            if (Directory.Exists(baseFolder))
            {
                Log.Information("=== Применение Base ===");
                await executor.ExecuteFolderAsync(baseFolder);
            }
            else
            {
                Log.Warning("Папка Base не найдена: {Path}", baseFolder);
            }
        }

        if (!opts.NoOriginal)
        {
            var originalFolder = Path.Combine(procFolder, "Original");
            if (Directory.Exists(originalFolder))
            {
                Log.Information("=== Применение Original ===");
                await executor.ExecuteFolderAsync(originalFolder);
            }
            else
            {
                Log.Warning("Папка Original не найдена: {Path}", originalFolder);
            }
        }

        Log.Information("Готово");
        return 0;
    }

    static async Task<int> ExportData(ExecOptions.ExportData opts)
    {
        var connStr = BuildConnectionString(opts.ServerName, opts.DatabaseName);
        var outputDir = opts.Output ?? Path.Combine(Directory.GetCurrentDirectory(), "DataExport");

        Log.Information("Экспорт данных: {Server}.{DB}", opts.ServerName, opts.DatabaseName);
        Log.Information("Выходная папка: {Output}", outputDir);

        var extractor = new TableDataExtractor(connStr)
        {
            MaxFileSizeBytes = opts.MaxSizeKb * 1024,
            BatchSize = opts.BatchSize,
            OutputDirectory = outputDir,
            AppendGoAfterInsert = opts.AppendGo,
            IncludeSchemaInFileName = opts.IncludeSchema
        };

        List<TableInfo> tables;

        if (!string.IsNullOrWhiteSpace(opts.Tables))
        {
            var names = opts.Tables.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Log.Information("Фильтр таблиц: {Count} масок", names.Length);

            var allTables = await extractor.GetAllUserTablesAsync();
            tables = FilterTables(allTables, names);

            Log.Information("Найдено таблиц по фильтру: {Count}", tables.Count);
        }
        else
        {
            tables = await extractor.GetAllUserTablesAsync();
            Log.Information("Все таблицы: {Count}", tables.Count);
        }

        if (tables.Count == 0)
        {
            Log.Warning("Нет таблиц для экспорта");
            return 1;
        }

        var result = await extractor.ExportTablesAsync(tables);

        Log.Information("Экспорт завершён:");
        Log.Information("  Таблиц обработано: {Total}", result.TotalTables);
        Log.Information("  Успешно: {Success}", result.SuccessCount);
        Log.Information("  С ошибками: {Errors}", result.ErrorCount);
        Log.Information("  Файлов создано: {Files}", result.TotalFilesCreated);
        Log.Information("  Общий размер: {Bytes:N0} байт", result.TotalBytes);

        foreach (var tr in result.TableResults.Where(t => !t.Success))
        {
            Log.Error("Ошибка экспорта {Table}: {Error}", tr.Table.FullName, tr.ErrorMessage);
        }

        return result.ErrorCount > 0 ? 1 : 0;
    }

    /// <summary>
    /// Фильтрует таблицы по списку имён/масок.
    /// Поддерживает: точные имена (dbo.Users), схема.% (dbo.%), %.имя (%.Accounts), % (все)
    /// </summary>
    private static List<TableInfo> FilterTables(List<TableInfo> allTables, string[] filters)
    {
        if (filters.Contains("%", StringComparer.OrdinalIgnoreCase))
            return allTables;

        var result = new List<TableInfo>();

        foreach (var table in allTables)
        {
            foreach (var filter in filters)
            {
                if (MatchesFilter(table, filter))
                {
                    result.Add(table);
                    break;
                }
            }
        }

        return result;
    }

    private static bool MatchesFilter(TableInfo table, string filter)
    {
        // Маска SQL: %.% — все таблицы
        if (filter.Equals("%", StringComparison.OrdinalIgnoreCase))
            return true;

        // Маска: %.Name или Schema.% или %.Name (contains)
        var lowerFilter = filter.ToLowerInvariant();

        if (lowerFilter.StartsWith("%.") && lowerFilter.EndsWith("%"))
        {
            // %.Name% — содержит в имени
            var namePart = filter.Trim('%', '.').ToLowerInvariant();
            return table.TableName.ToLowerInvariant().Contains(namePart);
        }
        else if (lowerFilter.StartsWith("%."))
        {
            // %.Name — точное имя, любая схема
            var namePart = filter.Substring(2).ToLowerInvariant();
            return table.TableName.Equals(namePart, StringComparison.OrdinalIgnoreCase);
        }
        else if (lowerFilter.EndsWith(".%"))
        {
            // Schema.% — точное имя схемы, любая таблица
            var schemaPart = filter.TrimEnd('%', '.').ToLowerInvariant();
            return table.SchemaName.Equals(schemaPart, StringComparison.OrdinalIgnoreCase);
        }
        else if (filter.Contains('.'))
        {
            // Schema.Table — точное совпадение
            var parts = filter.Split('.');
            if (parts.Length == 2)
            {
                var schemaPart = parts[0].Trim('[', ']').ToLowerInvariant();
                var tablePart = parts[1].Trim('[', ']').ToLowerInvariant();
                return table.SchemaName.Equals(schemaPart, StringComparison.OrdinalIgnoreCase) &&
                       table.TableName.Equals(tablePart, StringComparison.OrdinalIgnoreCase);
            }
        }
        else
        {
            // Просто имя таблицы (любая схема)
            return table.TableName.Equals(filter, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string BuildConnectionString(string server, string database)
    {
        return $"Server={server};Database={database};Integrated Security=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=False";
    }
}