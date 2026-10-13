using System.Diagnostics.CodeAnalysis;

namespace ImportFilePerformance.Models;

public sealed class BenchmarkSettings
{
    public string TestFilesRoot { get; set; } = string.Empty;
    public int IterationCount { get; set; } = 3;
    public int InvocationCount { get; set; } = 1;
    public int WarmupCount { get; set; } = 1;
    public int BatchSize { get; set; } = 50_000;
    /// <summary>Rows per JSON message in MessageBuffer* strategies (publicdwh Imp chunk size).</summary>
    public int ChunkRowSize { get; set; } = 100;
    public int RepeatCount { get; set; } = 3;
    public bool TruncateBeforeRun { get; set; } = true;
    public string MsSqlConnectionString { get; set; } = string.Empty;
    public string PostgresConnectionString { get; set; } = string.Empty;
    public string DefaultMode { get; set; } = "e2e";
    public string DefaultDatabase { get; set; } = "mssql";
    public string DefaultFile { get; set; } = "CsvTradeResult";
    public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string GetResolvedRoot()
    {
        if (string.IsNullOrWhiteSpace(TestFilesRoot))
            throw new InvalidOperationException("BenchmarkSettings.TestFilesRoot is empty.");

        if (Path.IsPathRooted(TestFilesRoot))
        {
            var rooted = Path.GetFullPath(TestFilesRoot);
            if (Directory.Exists(rooted))
                return rooted;
            throw new DirectoryNotFoundException($"Test files root not found: '{TestFilesRoot}'.");
        }

        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            if (string.IsNullOrWhiteSpace(start))
                continue;

            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                var candidate = Path.GetFullPath(Path.Combine(dir.FullName, TestFilesRoot));
                if (Directory.Exists(candidate))
                    return candidate;
            }
        }

        throw new DirectoryNotFoundException($"Test files root not found: '{TestFilesRoot}'.");
    }

    public string ToDisplayPath(string fullPath)
    {
        var name = Path.GetFileName(fullPath);
        if (string.IsNullOrWhiteSpace(TestFilesRoot))
            return name;

        return Path.Combine(TestFilesRoot.Replace('/', Path.DirectorySeparatorChar), name);
    }

    public bool TryResolveFile(string keyOrPath, [NotNullWhen(true)] out string? fullPath)
    {
        try
        {
            var resolved = ResolveFile(keyOrPath);
            if (File.Exists(resolved))
            {
                fullPath = resolved;
                return true;
            }
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }

        fullPath = null;
        return false;
    }

    public string ResolveFile(string keyOrPath)
    {
        if (File.Exists(keyOrPath))
            return Path.GetFullPath(keyOrPath);

        var root = GetResolvedRoot();

        if (Files.TryGetValue(keyOrPath, out var relative) && !string.IsNullOrWhiteSpace(relative))
        {
            var combined = Path.IsPathRooted(relative)
                ? relative
                : Path.Combine(root, relative);
            var full = Path.GetFullPath(combined);
            if (File.Exists(full))
                return full;

            throw new FileNotFoundException($"Test file not found: {keyOrPath} ({relative})");
        }

        var candidate = Path.Combine(root, keyOrPath);
        if (File.Exists(candidate))
            return Path.GetFullPath(candidate);

        throw new FileNotFoundException($"Test file not found: {keyOrPath}");
    }
}

public enum DatabaseKind
{
    MsSql,
    Postgres
}

public enum ImportDataset
{
    OrderLog,
    FuturesXml,
    SecurityXml,
    TradeResultCsv
}

public enum LoadStrategy
{
    /// <summary>XmlReader/StreamReader → IDataReader → SqlBulkCopy / COPY (all nvarchar staging).</summary>
    StreamingBulk,
    /// <summary>Same streaming bulk into typed staging (date/decimal/bigint/nvarchar×2).</summary>
    StreamingBulkTyped,
    /// <summary>Expands to ParseOnly_* baselines; no DB write.</summary>
    ParseOnly,
    /// <summary>CSV stream read only (parser for StreamingBulk / StreamingBulkTyped).</summary>
    ParseOnly_StreamingBulk,
    /// <summary>Same as ParseOnly_StreamingBulk (alias baseline for StreamingBulkTyped).</summary>
    ParseOnly_StreamingBulkTyped,
    /// <summary>CSV → named JSON chunks, discard (parser for MessageBufferThenLoad).</summary>
    ParseOnly_MessageBufferThenLoad,
    /// <summary>CSV → positional JSON arrays, discard (parser for MessageBufferShort*ThenLoad).</summary>
    ParseOnly_MessageBufferShortThenLoad,
    /// <summary>Same parser as Short (positional JSON), for ShortTyped full load.</summary>
    ParseOnly_MessageBufferShortTypedThenLoad,
    /// <summary>CSV stream read (parser for MessageStringBufferThenLoad — same as StreamingBulk parse).</summary>
    ParseOnly_MessageStringBufferThenLoad,
    /// <summary>Materialize all rows into List then bulk (anti-pattern, small files only).</summary>
    MaterializeThenBulk,
    /// <summary>CSV → JSON object chunks (~ChunkRowSize) → msg_buffer only (TradeResultCsv).</summary>
    MessageBufferChunks,
    /// <summary>MessageBufferChunks + EXEC load_* into nvarchar target (TradeResultCsv).</summary>
    MessageBufferThenLoad,
    /// <summary>Same as MessageBufferChunks but MessageBody is array-of-arrays (no field names).</summary>
    MessageBufferShortChunks,
    /// <summary>MessageBufferShortChunks + EXEC load_*_short → nvarchar target.</summary>
    MessageBufferShortThenLoad,
    /// <summary>Same short JSON buffer + EXEC load_*_short_typed → typed target (TRY_CONVERT).</summary>
    MessageBufferShortTypedThenLoad,
    /// <summary>CSV → SqlBulkCopy into all-string buffer (nvarchar ×3) → load proc CONVERT → typed target.</summary>
    MessageStringBufferThenLoad
}

public sealed class RunResult
{
    public required string TestName { get; init; }
    public required string Strategy { get; init; }
    public required string Database { get; init; }
    public required string FilePath { get; init; }
    public long FileBytes { get; init; }
    public long Rows { get; init; }
    public double ElapsedSeconds { get; init; }
    /// <summary>Final table where rows land for this strategy (e.g. stg_trade_result_typed_target).</summary>
    public string? DestinationTable { get; init; }
    /// <summary>
    /// Time of DB load procedure only (OPENJSON/CONVERT EXEC).
    /// null = N/A (ParseOnly / Chunks); 0 = no separate load (StreamingBulk).
    /// </summary>
    public double? LoadProcedureSeconds { get; init; }
    public long PeakWorkingSetBytes { get; init; }
    public double RowsPerSecond => ElapsedSeconds > 0 ? Rows / ElapsedSeconds : 0;
    public double MbPerSecond => ElapsedSeconds > 0 ? FileBytes / 1024d / 1024d / ElapsedSeconds : 0;
    public double? LoadProcedureRowsPerSecond =>
        LoadProcedureSeconds is > 0 ? Rows / LoadProcedureSeconds.Value : LoadProcedureSeconds is 0 ? null : null;
}
