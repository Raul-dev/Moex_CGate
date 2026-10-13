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

        // Comparable groups: 3× ParseOnly_* baselines first, then 3 full end-to-end loads.
        strategies = NormalizeStrategyOrder(strategies);

        var needsDatabase = strategies.Any(s => !IsParseBaseline(s));
        var needsMessageBuffer = strategies.Any(IsMessageBufferStrategy);
        if (needsMessageBuffer && dataset != ImportDataset.TradeResultCsv)
            throw new NotSupportedException("MessageBuffer* strategies are hardcoded for TradeResultCsv only.");

        Console.WriteLine("Run plan:");
        foreach (var s in strategies)
        {
            var tag = IsParseBaseline(s)
                ? "  ← parser baseline (no DB write)"
                : IsFullLoadComparable(s)
                    ? "  ← full load"
                    : "  ← partial / other";
            Console.WriteLine($"  - {s}{tag}");
        }
        Console.WriteLine();

        // Parse baselines can run without DB; still use selected db label for Method names.
        var dbTargets = needsDatabase ? databases : databases;

        foreach (var db in dbTargets)
        {
            IBulkImporter? importer = null;
            var dynamicCols = DatasetDetector.PeekDynamicColumns(filePath, dataset);
            if (needsDatabase)
            {
                importer = CreateImporter(db);
                await importer.EnsureSchemaAsync(dataset, dynamicCols, ct);
                if (needsMessageBuffer)
                {
                    await TradeResultMessageBufferPipeline.EnsureSchemaAsync(
                        db, ConnectionString(db), dynamicCols!, ct);
                    Console.WriteLine(
                        $"Message-buffer tables: {DescribeDb(db)} → " +
                        $"{TradeResultMessageBufferPipeline.BufferTable} → {TradeResultMessageBufferPipeline.TargetTable}; " +
                        $"string-buffer → {TradeResultMessageBufferPipeline.StringBufferTable} → " +
                        $"{TradeResultMessageBufferPipeline.TypedTargetTable} " +
                        $"(procs {TradeResultMessageBufferPipeline.LoadProcedure} | " +
                        $"{TradeResultMessageBufferPipeline.LoadProcedureShort} | " +
                        $"{TradeResultMessageBufferPipeline.LoadProcedureShortTyped} | " +
                        $"{TradeResultMessageBufferPipeline.LoadProcedureFromStringBuffer}, chunk={settings.ChunkRowSize})");
                }

                if (strategies.Any(s => s is LoadStrategy.StreamingBulk or LoadStrategy.MaterializeThenBulk))
                {
                    Console.WriteLine(
                        $"StreamingBulk target: {DescribeDb(db)} → dbo.{DatasetTables.TableName(dataset)} " +
                        $"(or public.{DatasetTables.TableName(dataset)} on Postgres)");
                }
                Console.WriteLine();
            }

            try
            {
                string? lastPhase = null;
                foreach (var strategy in strategies)
                {
                    if (strategy == LoadStrategy.MaterializeThenBulk && fileInfo.Length > 200L * 1024 * 1024)
                    {
                        Console.WriteLine($"SKIP MaterializeThenBulk for large file ({fileInfo.Length / 1024 / 1024} MB)");
                        continue;
                    }

                    var phase = IsParseBaseline(strategy)
                        ? "BASELINE ParseOnly (6 parsers)"
                        : IsFullLoadComparable(strategy)
                            ? "FULL LOAD"
                            : "OTHER / PARTIAL";
                    if (phase != lastPhase)
                    {
                        Console.WriteLine($"--- {phase} ---");
                        lastPhase = phase;
                    }

                    for (var rep = 1; rep <= settings.RepeatCount; rep++)
                    {
                        if (settings.TruncateBeforeRun && !IsParseBaseline(strategy))
                        {
                            if (IsMessageBufferStrategy(strategy))
                                await TradeResultMessageBufferPipeline.TruncateAsync(db, ConnectionString(db), ct);
                            else if (importer is not null)
                                await importer.TruncateAsync(dataset, ct);
                        }

                        var dbLabel = db.ToString();
                        var testName = $"{dbLabel}/{strategy}/{Path.GetFileName(filePath)}#{rep}";
                        Console.WriteLine($">>> {testName}");

                        var result = await MeasureAsync(
                            testName,
                            strategy.ToString(),
                            dbLabel,
                            filePath,
                            fileInfo.Length,
                            DestinationTableFor(strategy, dataset),
                            async () => await ExecuteAsync(importer, db, dataset, filePath, strategy, ct));

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
        PrintBenchmarkDotNetSummary(results);
        return results;
    }

    /// <summary>
    /// ParseOnly → three ParseOnly_* baselines → full comparable loads → everything else.
    /// </summary>
    private static LoadStrategy[] NormalizeStrategyOrder(LoadStrategy[] strategies)
    {
        var set = strategies.Distinct().ToList();
        if (set.Remove(LoadStrategy.ParseOnly))
        {
            // Expand shorthand into three parser estimates aligned with full-load methods.
            set.Add(LoadStrategy.ParseOnly_StreamingBulk);
            set.Add(LoadStrategy.ParseOnly_StreamingBulkTyped);
            set.Add(LoadStrategy.ParseOnly_MessageBufferThenLoad);
            set.Add(LoadStrategy.ParseOnly_MessageBufferShortThenLoad);
            set.Add(LoadStrategy.ParseOnly_MessageBufferShortTypedThenLoad);
            set.Add(LoadStrategy.ParseOnly_MessageStringBufferThenLoad);
        }

        set = set.Distinct().ToList();
        return set
            .Where(IsParseBaseline)
            .OrderBy(ParseBaselineSortKey)
            .Concat(set.Where(IsFullLoadComparable).OrderBy(FullLoadSortKey))
            .Concat(set.Where(s => !IsParseBaseline(s) && !IsFullLoadComparable(s)))
            .ToArray();
    }

    private static bool IsParseBaseline(LoadStrategy s) => s is
        LoadStrategy.ParseOnly or
        LoadStrategy.ParseOnly_StreamingBulk or
        LoadStrategy.ParseOnly_StreamingBulkTyped or
        LoadStrategy.ParseOnly_MessageBufferThenLoad or
        LoadStrategy.ParseOnly_MessageBufferShortThenLoad or
        LoadStrategy.ParseOnly_MessageBufferShortTypedThenLoad or
        LoadStrategy.ParseOnly_MessageStringBufferThenLoad;

    private static int ParseBaselineSortKey(LoadStrategy s) => s switch
    {
        LoadStrategy.ParseOnly_StreamingBulk => 0,
        LoadStrategy.ParseOnly_StreamingBulkTyped => 1,
        LoadStrategy.ParseOnly_MessageBufferThenLoad => 2,
        LoadStrategy.ParseOnly_MessageBufferShortThenLoad => 3,
        LoadStrategy.ParseOnly_MessageBufferShortTypedThenLoad => 4,
        LoadStrategy.ParseOnly_MessageStringBufferThenLoad => 5,
        _ => 99
    };

    private static bool IsFullLoadComparable(LoadStrategy s) => s is
        LoadStrategy.StreamingBulk or
        LoadStrategy.StreamingBulkTyped or
        LoadStrategy.MessageBufferThenLoad or
        LoadStrategy.MessageBufferShortThenLoad or
        LoadStrategy.MessageBufferShortTypedThenLoad or
        LoadStrategy.MessageStringBufferThenLoad;

    private static int FullLoadSortKey(LoadStrategy s) => s switch
    {
        LoadStrategy.StreamingBulk => 0,
        LoadStrategy.StreamingBulkTyped => 1,
        LoadStrategy.MessageBufferThenLoad => 2,
        LoadStrategy.MessageBufferShortThenLoad => 3,
        LoadStrategy.MessageBufferShortTypedThenLoad => 4,
        LoadStrategy.MessageStringBufferThenLoad => 5,
        _ => 99
    };

    private static bool IsMessageBufferStrategy(LoadStrategy s) => s is
        LoadStrategy.MessageBufferChunks or
        LoadStrategy.MessageBufferThenLoad or
        LoadStrategy.MessageBufferShortChunks or
        LoadStrategy.MessageBufferShortThenLoad or
        LoadStrategy.MessageBufferShortTypedThenLoad or
        LoadStrategy.MessageStringBufferThenLoad or
        LoadStrategy.StreamingBulkTyped;

    private static bool IsParseOnlyResult(RunResult r) =>
        r.Strategy.StartsWith("ParseOnly", StringComparison.Ordinal);

    private static bool IsFullLoadResult(RunResult r) =>
        r.Strategy is nameof(LoadStrategy.StreamingBulk)
            or nameof(LoadStrategy.StreamingBulkTyped)
            or nameof(LoadStrategy.MessageBufferThenLoad)
            or nameof(LoadStrategy.MessageBufferShortThenLoad)
            or nameof(LoadStrategy.MessageBufferShortTypedThenLoad)
            or nameof(LoadStrategy.MessageStringBufferThenLoad);

    /// <summary>Final destination table for comparable full-load strategies.</summary>
    public static string DestinationTableFor(LoadStrategy strategy, ImportDataset dataset) => strategy switch
    {
        LoadStrategy.StreamingBulk or LoadStrategy.MaterializeThenBulk =>
            DatasetTables.TableName(dataset),
        LoadStrategy.StreamingBulkTyped =>
            TradeResultMessageBufferPipeline.TypedCsvTable,
        LoadStrategy.MessageBufferThenLoad or LoadStrategy.MessageBufferShortThenLoad =>
            TradeResultMessageBufferPipeline.TargetTable,
        LoadStrategy.MessageBufferShortTypedThenLoad or LoadStrategy.MessageStringBufferThenLoad =>
            TradeResultMessageBufferPipeline.TypedTargetTable,
        _ => ""
    };

    private string ConnectionString(DatabaseKind db) => db switch
    {
        DatabaseKind.MsSql => settings.MsSqlConnectionString,
        DatabaseKind.Postgres => settings.PostgresConnectionString,
        _ => throw new ArgumentOutOfRangeException(nameof(db))
    };

    private static string DescribeDb(DatabaseKind db) => db switch
    {
        DatabaseKind.MsSql => "MSSQL ImportFile",
        DatabaseKind.Postgres => "Postgres ImportFile",
        _ => db.ToString()
    };

    private IBulkImporter CreateImporter(DatabaseKind kind) => kind switch
    {
        DatabaseKind.MsSql => new SqlServerBulkImporter(settings.MsSqlConnectionString),
        DatabaseKind.Postgres => new PostgresCopyImporter(settings.PostgresConnectionString),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private async Task<ExecuteOutcome> ExecuteAsync(
        IBulkImporter? importer,
        DatabaseKind db,
        ImportDataset dataset,
        string filePath,
        LoadStrategy strategy,
        CancellationToken ct)
    {
        switch (strategy)
        {
            case LoadStrategy.ParseOnly:
            case LoadStrategy.ParseOnly_StreamingBulk:
            case LoadStrategy.ParseOnly_StreamingBulkTyped:
            {
                await using var reader = DatasetDetector.OpenReader(filePath, dataset);
                long rows = 0;
                while (await reader.ReadAsync(ct))
                    rows++;
                return new ExecuteOutcome(rows, LoadProcedureSeconds: null);
            }
            case LoadStrategy.ParseOnly_MessageBufferThenLoad:
            {
                if (dataset != ImportDataset.TradeResultCsv)
                    throw new NotSupportedException("ParseOnly_MessageBufferThenLoad is for TradeResultCsv only.");
                var rows = await TradeResultMessageBufferPipeline.ParseChunksOnlyAsync(
                    filePath, settings.ChunkRowSize, MessageBodyFormat.NamedObjects, ct);
                return new ExecuteOutcome(rows, null);
            }
            case LoadStrategy.ParseOnly_MessageBufferShortThenLoad:
            case LoadStrategy.ParseOnly_MessageBufferShortTypedThenLoad:
            {
                if (dataset != ImportDataset.TradeResultCsv)
                    throw new NotSupportedException($"{strategy} is for TradeResultCsv only.");
                var rows = await TradeResultMessageBufferPipeline.ParseChunksOnlyAsync(
                    filePath, settings.ChunkRowSize, MessageBodyFormat.PositionalArrays, ct);
                return new ExecuteOutcome(rows, null);
            }
            case LoadStrategy.ParseOnly_MessageStringBufferThenLoad:
            {
                await using var reader = DatasetDetector.OpenReader(filePath, dataset);
                long rows = 0;
                while (await reader.ReadAsync(ct))
                    rows++;
                return new ExecuteOutcome(rows, null);
            }
            case LoadStrategy.StreamingBulk:
            {
                if (importer is null)
                    throw new InvalidOperationException("StreamingBulk requires a database importer.");
                await using var reader = DatasetDetector.OpenReader(filePath, dataset);
                var rows = await importer.BulkInsertAsync(dataset, reader, settings.BatchSize, ct);
                // No separate load procedure — data lands in staging directly.
                return new ExecuteOutcome(rows, LoadProcedureSeconds: 0);
            }
            case LoadStrategy.StreamingBulkTyped:
            {
                if (dataset != ImportDataset.TradeResultCsv)
                    throw new NotSupportedException("StreamingBulkTyped is for TradeResultCsv only.");
                var rows = await TradeResultMessageBufferPipeline.WriteTypedCsvBulkAsync(
                    db, ConnectionString(db), filePath, settings.BatchSize, ct);
                return new ExecuteOutcome(rows, LoadProcedureSeconds: 0);
            }
            case LoadStrategy.MaterializeThenBulk:
            {
                if (importer is null)
                    throw new InvalidOperationException("MaterializeThenBulk requires a database importer.");

                string[] names;
                Type[] types;
                var rowsList = new List<object?[]>();

                await using (var reader = DatasetDetector.OpenReader(filePath, dataset))
                {
                    names = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
                    types = Enumerable.Range(0, reader.FieldCount).Select(reader.GetFieldType).ToArray();
                    while (await reader.ReadAsync(ct))
                    {
                        var values = new object?[reader.FieldCount];
                        reader.GetValues(values!);
                        rowsList.Add(values);
                    }
                }

                await using var mat = new MaterializedDataReader(names, rowsList, types);
                var rows = await importer.BulkInsertAsync(dataset, mat, settings.BatchSize, ct);
                return new ExecuteOutcome(rows, LoadProcedureSeconds: 0);
            }
            case LoadStrategy.MessageBufferChunks:
            {
                var rows = await TradeResultMessageBufferPipeline.WriteChunksAsync(
                    db, ConnectionString(db), filePath, settings.ChunkRowSize,
                    MessageBodyFormat.NamedObjects, ct);
                return new ExecuteOutcome(rows, null);
            }
            case LoadStrategy.MessageBufferThenLoad:
            {
                await TradeResultMessageBufferPipeline.WriteChunksAsync(
                    db, ConnectionString(db), filePath, settings.ChunkRowSize,
                    MessageBodyFormat.NamedObjects, ct);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var rows = await TradeResultMessageBufferPipeline.LoadFromBufferAsync(
                    db, ConnectionString(db), MessageBodyFormat.NamedObjects, ct);
                sw.Stop();
                return new ExecuteOutcome(rows, sw.Elapsed.TotalSeconds);
            }
            case LoadStrategy.MessageBufferShortChunks:
            {
                var rows = await TradeResultMessageBufferPipeline.WriteChunksAsync(
                    db, ConnectionString(db), filePath, settings.ChunkRowSize,
                    MessageBodyFormat.PositionalArrays, ct);
                return new ExecuteOutcome(rows, null);
            }
            case LoadStrategy.MessageBufferShortThenLoad:
            {
                await TradeResultMessageBufferPipeline.WriteChunksAsync(
                    db, ConnectionString(db), filePath, settings.ChunkRowSize,
                    MessageBodyFormat.PositionalArrays, ct);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var rows = await TradeResultMessageBufferPipeline.LoadFromBufferAsync(
                    db, ConnectionString(db), MessageBodyFormat.PositionalArrays, ct);
                sw.Stop();
                return new ExecuteOutcome(rows, sw.Elapsed.TotalSeconds);
            }
            case LoadStrategy.MessageBufferShortTypedThenLoad:
            {
                if (dataset != ImportDataset.TradeResultCsv)
                    throw new NotSupportedException("MessageBufferShortTypedThenLoad is for TradeResultCsv only.");
                await TradeResultMessageBufferPipeline.WriteChunksAsync(
                    db, ConnectionString(db), filePath, settings.ChunkRowSize,
                    MessageBodyFormat.PositionalArrays, ct);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var rows = await TradeResultMessageBufferPipeline.LoadFromBufferAsync(
                    db, ConnectionString(db), MessageBodyFormat.PositionalArrays, ct,
                    toTypedTarget: true);
                sw.Stop();
                return new ExecuteOutcome(rows, sw.Elapsed.TotalSeconds);
            }
            case LoadStrategy.MessageStringBufferThenLoad:
            {
                if (dataset != ImportDataset.TradeResultCsv)
                    throw new NotSupportedException("MessageStringBufferThenLoad is for TradeResultCsv only.");
                await TradeResultMessageBufferPipeline.WriteStringBufferAsync(
                    db, ConnectionString(db), filePath, settings.BatchSize, ct);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var rows = await TradeResultMessageBufferPipeline.LoadFromStringBufferAsync(
                    db, ConnectionString(db), ct);
                sw.Stop();
                return new ExecuteOutcome(rows, sw.Elapsed.TotalSeconds);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(strategy));
        }
    }

    private readonly record struct ExecuteOutcome(long Rows, double? LoadProcedureSeconds);

    private static async Task<RunResult> MeasureAsync(
        string testName,
        string strategy,
        string database,
        string filePath,
        long fileBytes,
        string? destinationTable,
        Func<Task<ExecuteOutcome>> action)
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

        using var cts = new CancellationTokenSource();
        var memoryTask = TrackMemoryAsync(cts.Token);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        ExecuteOutcome outcome;
        try
        {
            outcome = await action();
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
            Rows = outcome.Rows,
            ElapsedSeconds = sw.Elapsed.TotalSeconds,
            DestinationTable = string.IsNullOrEmpty(destinationTable) ? null : destinationTable,
            LoadProcedureSeconds = outcome.LoadProcedureSeconds,
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
        var loadPart = r.LoadProcedureSeconds switch
        {
            null => "",
            0 => " | load_proc=0 (n/a)",
            var s => $" | load_proc={s:F3}s"
        };
        Console.WriteLine(
            $"    time={r.ElapsedSeconds:F2}s | rows={r.Rows:N0} | {r.RowsPerSecond:N0} rows/s | " +
            $"{r.MbPerSecond:F2} MB/s | peak RAM={r.PeakWorkingSetBytes / 1024d / 1024d:F1} MB{loadPart}");
    }

    private static void PrintSummary(IReadOnlyList<RunResult> results)
    {
        PrintBestOfGroup(
            "=== BASELINE: ParseOnly × 6 parsers (no DB write) ===",
            results.Where(IsParseOnlyResult).ToList(),
            showDestination: false);

        PrintBestOfGroupLoadProc(
            "=== LOAD PROCEDURE only (StreamingBulk = 0; buffer→target EXEC) ===",
            results.Where(IsFullLoadResult).ToList());

        PrintBestOfGroup(
            "=== FULL LOAD (comparable): end-to-end file → rows in DB ===",
            results.Where(IsFullLoadResult).ToList(),
            showDestination: true);

        var other = results.Where(r => !IsParseOnlyResult(r) && !IsFullLoadResult(r)).ToList();
        if (other.Count > 0)
            PrintBestOfGroup("=== OTHER / PARTIAL (not comparable to full load) ===", other, showDestination: false);
    }

    private static void PrintBestOfGroupLoadProc(string title, IReadOnlyList<RunResult> results)
    {
        Console.WriteLine();
        Console.WriteLine(title);
        if (results.Count == 0)
        {
            Console.WriteLine("  (none in this run)");
            return;
        }

        var groups = results.GroupBy(r => (r.Database, r.Strategy, Path.GetFileName(r.FilePath)));
        foreach (var g in groups.OrderBy(x => x.Key.Database).ThenBy(x =>
                 {
                     if (Enum.TryParse<LoadStrategy>(x.Key.Strategy, out var s))
                         return FullLoadSortKey(s);
                     return 500;
                 }))
        {
            var withLoad = g.Where(x => x.LoadProcedureSeconds.HasValue).ToList();
            if (withLoad.Count == 0)
            {
                Console.WriteLine($"{g.First().Database,-10} {g.Key.Strategy,-42}  (no load_proc timing)");
                continue;
            }

            var best = withLoad.OrderBy(x => x.LoadProcedureSeconds!.Value).First();
            var load = best.LoadProcedureSeconds!.Value;
            var dest = best.DestinationTable ?? "-";
            Console.WriteLine(
                $"{best.Database,-10} {best.Strategy,-42} → {dest,-32} " +
                $"load_proc={load,8:F3}s  " +
                (load > 0
                    ? $"{best.Rows / load,12:N0} rows/s (proc)"
                    : "           n/a (direct bulk)"));
        }
    }

    private static void PrintBestOfGroup(string title, IReadOnlyList<RunResult> results, bool showDestination)
    {
        Console.WriteLine();
        Console.WriteLine(title);
        if (results.Count == 0)
        {
            Console.WriteLine("  (none in this run)");
            return;
        }

        var groups = results.GroupBy(r => (r.Database, r.Strategy, Path.GetFileName(r.FilePath)));
        foreach (var g in groups.OrderBy(x => x.Key.Database).ThenBy(x =>
                 {
                     if (Enum.TryParse<LoadStrategy>(x.Key.Strategy, out var s))
                         return IsParseBaseline(s) ? ParseBaselineSortKey(s) : 100 + FullLoadSortKey(s);
                     return 500;
                 }))
        {
            var best = g.OrderBy(x => x.ElapsedSeconds).First();
            if (showDestination)
            {
                Console.WriteLine(
                    $"{best.Database,-10} {best.Strategy,-42} → {(best.DestinationTable ?? "-"),-32} " +
                    $"{best.ElapsedSeconds,8:F2}s  {best.RowsPerSecond,12:N0} rows/s  " +
                    $"RAM {best.PeakWorkingSetBytes / 1024d / 1024d,7:F1} MB");
            }
            else
            {
                Console.WriteLine(
                    $"{best.Database,-10} {best.Strategy,-42} {g.Key.Item3,-40} " +
                    $"{best.ElapsedSeconds,8:F2}s  {best.RowsPerSecond,12:N0} rows/s  " +
                    $"RAM {best.PeakWorkingSetBytes / 1024d / 1024d,7:F1} MB");
            }
        }
    }

    /// <summary>BenchmarkDotNet-style tables split by comparable groups.</summary>
    private static void PrintBenchmarkDotNetSummary(IReadOnlyList<RunResult> results)
    {
        if (results.Count == 0)
            return;

        Console.WriteLine();
        Console.WriteLine("// * Summary *");

        PrintBdnTable(
            "Baseline — ParseOnly × 6 parsers (no DB write)",
            results.Where(IsParseOnlyResult).ToList(),
            useLoadProcedureTime: false,
            showDestination: false);

        PrintBdnTable(
            "Load procedure only — EXEC load_* (StreamingBulk = 0)",
            results.Where(IsFullLoadResult).ToList(),
            useLoadProcedureTime: true,
            showDestination: true);

        PrintBdnTable(
            "Full load — comparable end-to-end (file → rows in DB)",
            results.Where(IsFullLoadResult).ToList(),
            useLoadProcedureTime: false,
            showDestination: true);

        var other = results.Where(r => !IsParseOnlyResult(r) && !IsFullLoadResult(r)).ToList();
        if (other.Count > 0)
            PrintBdnTable("Other / partial (Chunks only, Materialize, …)", other, useLoadProcedureTime: false, showDestination: false);

        Console.WriteLine();
        Console.WriteLine("// * Legends *");
        Console.WriteLine("  Mean    : Arithmetic mean of all measurements");
        Console.WriteLine("  Error   : Half of 95% confidence interval");
        Console.WriteLine("  StdDev  : Standard deviation of all measurements");
        Console.WriteLine("  Rows/s  : Best-repeat throughput");
        Console.WriteLine("  Peak RAM: Max process working set across repeats");
        Console.WriteLine("  Dest    : Final destination table");
        Console.WriteLine("  Load proc table: only dbo.load_* time; StreamingBulk / StreamingBulkTyped = 0");
    }

    private static void PrintBdnTable(
        string title,
        IReadOnlyList<RunResult> results,
        bool useLoadProcedureTime,
        bool showDestination)
    {
        Console.WriteLine();
        Console.WriteLine($"// {title}");
        if (results.Count == 0)
        {
            Console.WriteLine("//   (none)");
            return;
        }

        var groups = results
            .GroupBy(r => $"{r.Database}_{r.Strategy}")
            .OrderBy(g =>
            {
                var strategy = g.First().Strategy;
                if (!Enum.TryParse<LoadStrategy>(strategy, out var s))
                    return 50;
                return IsParseBaseline(s) ? ParseBaselineSortKey(s) : 100 + FullLoadSortKey(s);
            })
            .ThenBy(g => g.Key)
            .ToList();

        var stats = groups.Select(g =>
        {
            double[] timesMs;
            if (useLoadProcedureTime)
            {
                timesMs = g
                    .Where(r => r.LoadProcedureSeconds.HasValue)
                    .Select(r => r.LoadProcedureSeconds!.Value * 1000d)
                    .ToArray();
                if (timesMs.Length == 0)
                    timesMs = [0];
            }
            else
            {
                timesMs = g.Select(r => r.ElapsedSeconds * 1000d).ToArray();
            }

            var mean = timesMs.Average();
            var n = timesMs.Length;
            var stdDev = n <= 1
                ? 0d
                : Math.Sqrt(timesMs.Sum(t => (t - mean) * (t - mean)) / (n - 1));
            var error = n <= 1 ? 0d : 1.96 * stdDev / Math.Sqrt(n);
            var best = useLoadProcedureTime
                ? g.Where(r => r.LoadProcedureSeconds.HasValue).OrderBy(r => r.LoadProcedureSeconds).FirstOrDefault()
                  ?? g.OrderBy(r => r.ElapsedSeconds).First()
                : g.OrderBy(r => r.ElapsedSeconds).First();

            double? rowsPerSec = null;
            if (useLoadProcedureTime && best.LoadProcedureSeconds is > 0)
                rowsPerSec = best.Rows / best.LoadProcedureSeconds.Value;
            else if (!useLoadProcedureTime)
                rowsPerSec = best.RowsPerSecond;

            return new
            {
                Method = g.Key,
                Dest = best.DestinationTable ?? "-",
                MeanMs = mean,
                ErrorMs = error,
                StdDevMs = stdDev,
                RowsPerSec = rowsPerSec,
                PeakRamMb = g.Max(r => r.PeakWorkingSetBytes) / 1024d / 1024d,
                IsZeroLoad = useLoadProcedureTime && best.LoadProcedureSeconds is 0,
            };
        }).ToList();

        var methodW = Math.Max(6, stats.Max(s => s.Method.Length));
        var destW = showDestination ? Math.Max(4, stats.Max(s => s.Dest.Length)) : 0;

        if (showDestination)
        {
            Console.WriteLine(
                $"| {"Method".PadRight(methodW)} | {"Dest".PadRight(destW)} | {"Mean",12} | {"Error",12} | {"StdDev",12} | {"Rows/s",12} | {"Peak RAM",10} |");
            Console.WriteLine(
                $"| {new string('-', methodW)} | {new string('-', destW)} | {new string('-', 11)}: | {new string('-', 11)}: | {new string('-', 11)}: | {new string('-', 11)}: | {new string('-', 9)}: |");
        }
        else
        {
            Console.WriteLine(
                $"| {"Method".PadRight(methodW)} | {"Mean",12} | {"Error",12} | {"StdDev",12} | {"Rows/s",12} | {"Peak RAM",10} |");
            Console.WriteLine(
                $"| {new string('-', methodW)} | {new string('-', 11)}: | {new string('-', 11)}: | {new string('-', 11)}: | {new string('-', 11)}: | {new string('-', 9)}: |");
        }

        foreach (var s in stats)
        {
            var rowsCell = s.IsZeroLoad
                ? "           0"
                : s.RowsPerSec is null
                    ? "         n/a"
                    : $"{s.RowsPerSec.Value,12:N0}";
            if (showDestination)
            {
                Console.WriteLine(
                    $"| {s.Method.PadRight(methodW)} | {s.Dest.PadRight(destW)} | {FormatMs(s.MeanMs),12} | {FormatMs(s.ErrorMs),12} | {FormatMs(s.StdDevMs),12} | {rowsCell} | {s.PeakRamMb,8:F1} MB |");
            }
            else
            {
                Console.WriteLine(
                    $"| {s.Method.PadRight(methodW)} | {FormatMs(s.MeanMs),12} | {FormatMs(s.ErrorMs),12} | {FormatMs(s.StdDevMs),12} | {rowsCell} | {s.PeakRamMb,8:F1} MB |");
            }
        }
    }

    private static string FormatMs(double ms)
    {
        if (ms <= 0)
            return "0";
        if (ms >= 1000)
            return $"{ms / 1000d:0.000} s";
        if (ms >= 1)
            return $"{ms:0.000} ms";
        return $"{ms * 1000d:0.000} us";
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
