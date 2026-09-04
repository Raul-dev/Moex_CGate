using System.Data.Common;
using ImportFilePerformance.Importers;
using ImportFilePerformance.Models;
using ImportFilePerformance.Readers;

namespace ImportFilePerformance.Runner;

public static class DatasetDetector
{
    public static ImportDataset Detect(string filePath)
    {
        var name = Path.GetFileName(filePath).ToUpperInvariant();
        if (name.Contains("ORDER_LOG") || name.StartsWith("ORDERS-"))
            return ImportDataset.OrderLog;
        if (name.EndsWith(".CSV"))
            return ImportDataset.TradeResultCsv;

        using var fs = File.OpenRead(filePath);
        var buf = new byte[8192];
        var n = fs.Read(buf, 0, buf.Length);
        var head = System.Text.Encoding.UTF8.GetString(buf, 0, n);

        if (head.Contains("<SECURITY", StringComparison.OrdinalIgnoreCase) &&
            !head.Contains("<FUTURES", StringComparison.OrdinalIgnoreCase))
            return ImportDataset.SecurityXml;

        if (head.Contains('<'))
            return ImportDataset.FuturesXml;

        return ImportDataset.OrderLog;
    }

    public static DbDataReader OpenReader(string filePath, ImportDataset dataset) => dataset switch
    {
        ImportDataset.OrderLog => new OrderLogDataReader(filePath),
        ImportDataset.FuturesXml => new FuturesXmlDataReader(filePath),
        ImportDataset.TradeResultCsv => DelimitedCsvDataReader.Create(filePath, ';'),
        ImportDataset.SecurityXml => throw new NotSupportedException(
            "SecurityXml reader not implemented yet — use FuturesXml / OrderLog / CSV files for benchmarks."),
        _ => throw new NotSupportedException($"No streaming reader for {dataset}")
    };

    public static IReadOnlyList<string>? PeekDynamicColumns(string filePath, ImportDataset dataset)
    {
        if (dataset != ImportDataset.TradeResultCsv)
            return null;

        using var reader = DelimitedCsvDataReader.Create(filePath, ';');
        return reader.ColumnNames.ToArray();
    }
}

public sealed class EndToEndRunner(BenchmarkSettings settings)
{
    public async Task<IReadOnlyList<RunResult>> RunAsync(
        string fileKeyOrPath,
        DatabaseKind[] databases,
        LoadStrategy[] strategies,
        CancellationToken ct = default)
    {
        var filePath = settings.ResolveFile(fileKeyOrPath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException(settings.ToDisplayPath(filePath));

        var dataset = DatasetDetector.Detect(filePath);
        var fileInfo = new FileInfo(filePath);
        var results = new List<RunResult>();

        Console.WriteLine($"File: {settings.ToDisplayPath(filePath)}");
        Console.WriteLine($"Size: {fileInfo.Length / 1024d / 1024d:F2} MB | Dataset: {dataset}");
        Console.WriteLine();

        var needsDatabase = strategies.Any(s => s != LoadStrategy.ParseOnly);

        // ParseOnly can run without any database connection.
        var dbTargets = needsDatabase ? databases : [databases.FirstOrDefault()];

        foreach (var db in dbTargets)
        {
            IBulkImporter? importer = null;
            if (needsDatabase)
            {
                importer = CreateImporter(db);
                var dynamicCols = DatasetDetector.PeekDynamicColumns(filePath, dataset);
                await importer.EnsureSchemaAsync(dataset, dynamicCols, ct);
            }

            try
            {
                foreach (var strategy in strategies)
                {
                    if (strategy == LoadStrategy.MaterializeThenBulk && fileInfo.Length > 200L * 1024 * 1024)
                    {
                        Console.WriteLine($"SKIP MaterializeThenBulk for large file ({fileInfo.Length / 1024 / 1024} MB)");
                        continue;
                    }

                    for (var rep = 1; rep <= settings.RepeatCount; rep++)
                    {
                        if (importer is not null && settings.TruncateBeforeRun && strategy != LoadStrategy.ParseOnly)
                            await importer.TruncateAsync(dataset, ct);

                        var dbLabel = needsDatabase ? db.ToString() : "None";
                        var testName = $"{dbLabel}/{strategy}/{Path.GetFileName(filePath)}#{rep}";
                        Console.WriteLine($">>> {testName}");

                        var result = await MeasureAsync(
                            testName,
                            strategy.ToString(),
                            dbLabel,
                            filePath,
                            fileInfo.Length,
                            async () => await ExecuteAsync(importer, dataset, filePath, strategy, ct));

                        results.Add(result);
                        Print(result);
                    }
                }
            }
            finally
            {
                if (importer is not null)
                    await importer.DisposeAsync();
            }
        }

        PrintSummary(results);
        return results;
    }

    private IBulkImporter CreateImporter(DatabaseKind kind) => kind switch
    {
        DatabaseKind.MsSql => new SqlServerBulkImporter(settings.MsSqlConnectionString),
        DatabaseKind.Postgres => new PostgresCopyImporter(settings.PostgresConnectionString),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private async Task<long> ExecuteAsync(
        IBulkImporter? importer,
        ImportDataset dataset,
        string filePath,
        LoadStrategy strategy,
        CancellationToken ct)
    {
        switch (strategy)
        {
            case LoadStrategy.ParseOnly:
            {
                await using var reader = DatasetDetector.OpenReader(filePath, dataset);
                long rows = 0;
                while (await reader.ReadAsync(ct))
                    rows++;
                return rows;
            }
            case LoadStrategy.StreamingBulk:
            {
                if (importer is null)
                    throw new InvalidOperationException("StreamingBulk requires a database importer.");
                await using var reader = DatasetDetector.OpenReader(filePath, dataset);
                return await importer.BulkInsertAsync(dataset, reader, settings.BatchSize, ct);
            }
            case LoadStrategy.MaterializeThenBulk:
            {
                if (importer is null)
                    throw new InvalidOperationException("MaterializeThenBulk requires a database importer.");

                string[] names;
                Type[] types;
                var rows = new List<object?[]>();

                await using (var reader = DatasetDetector.OpenReader(filePath, dataset))
                {
                    names = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
                    types = Enumerable.Range(0, reader.FieldCount).Select(reader.GetFieldType).ToArray();
                    while (await reader.ReadAsync(ct))
                    {
                        var values = new object?[reader.FieldCount];
                        reader.GetValues(values!);
                        rows.Add(values);
                    }
                }

                await using var mat = new MaterializedDataReader(names, rows, types);
                return await importer.BulkInsertAsync(dataset, mat, settings.BatchSize, ct);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(strategy));
        }
    }

    private static async Task<RunResult> MeasureAsync(
        string testName,
        string strategy,
        string database,
        string filePath,
        long fileBytes,
        Func<Task<long>> action)
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

        using var cts = new CancellationTokenSource();
        var memoryTask = TrackMemoryAsync(cts.Token);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long rows;
        try
        {
            rows = await action();
        }
        finally
        {
            await cts.CancelAsync();
            sw.Stop();
        }

        var peak = await memoryTask;
        return new RunResult
        {
            TestName = testName,
            Strategy = strategy,
            Database = database,
            FilePath = filePath,
            FileBytes = fileBytes,
            Rows = rows,
            ElapsedSeconds = sw.Elapsed.TotalSeconds,
            PeakWorkingSetBytes = peak
        };
    }

    private static async Task<long> TrackMemoryAsync(CancellationToken token)
    {
        long max = 0;
        var proc = System.Diagnostics.Process.GetCurrentProcess();
        try
        {
            while (!token.IsCancellationRequested)
            {
                proc.Refresh();
                max = Math.Max(max, proc.WorkingSet64);
                await Task.Delay(100, token);
            }
        }
        catch (OperationCanceledException)
        {
            // expected
        }

        proc.Refresh();
        return Math.Max(max, proc.WorkingSet64);
    }

    private static void Print(RunResult r)
    {
        Console.WriteLine(
            $"    time={r.ElapsedSeconds:F2}s | rows={r.Rows:N0} | {r.RowsPerSecond:N0} rows/s | " +
            $"{r.MbPerSecond:F2} MB/s | peak RAM={r.PeakWorkingSetBytes / 1024d / 1024d:F1} MB");
    }

    private static void PrintSummary(IReadOnlyList<RunResult> results)
    {
        Console.WriteLine();
        Console.WriteLine("=== SUMMARY (best per strategy/db) ===");
        var groups = results.GroupBy(r => (r.Database, r.Strategy, Path.GetFileName(r.FilePath)));
        foreach (var g in groups.OrderBy(x => x.Key.Database).ThenBy(x => x.Key.Strategy))
        {
            var best = g.OrderBy(x => x.ElapsedSeconds).First();
            Console.WriteLine(
                $"{best.Database,-10} {best.Strategy,-22} {g.Key.Item3,-45} " +
                $"{best.ElapsedSeconds,8:F2}s  {best.RowsPerSecond,12:N0} rows/s  " +
                $"RAM {best.PeakWorkingSetBytes / 1024d / 1024d,7:F1} MB");
        }
    }
}

/// <summary>IDataReader over an already-materialized list (anti-pattern baseline).</summary>
internal sealed class MaterializedDataReader : StreamingDataReaderBase
{
    private readonly List<object?[]> _rows;
    private int _index = -1;

    public MaterializedDataReader(string[] names, List<object?[]> rows, Type[]? types = null)
        : base(names, types ?? names.Select(_ => typeof(object)).ToArray())
    {
        _rows = rows;
    }

    protected override bool TryReadNext()
    {
        _index++;
        if (_index >= _rows.Count)
            return false;

        var row = _rows[_index];
        for (var i = 0; i < Values.Length; i++)
            Values[i] = i < row.Length ? row[i] : null;
        return true;
    }
}
