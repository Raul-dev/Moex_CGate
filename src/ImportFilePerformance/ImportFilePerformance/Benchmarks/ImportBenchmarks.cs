using BenchmarkDotNet.Attributes;
using ImportFilePerformance.Models;
using ImportFilePerformance.Runner;
using Microsoft.Extensions.Configuration;

namespace ImportFilePerformance.Benchmarks;

/// <summary>
/// BenchmarkDotNet suite for small/medium files (XML, CSV).
/// Huge order-log files (&gt;1 GB) must use --mode e2e (custom timer + RAM tracker).
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class ImportBenchmarks
{
    private BenchmarkSettings _settings = null!;
    private string _xmlPath = null!;
    private string _csvPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        var config = AppSettingsLoader.Load();

        _settings = config.GetSection("BenchmarkSettings").Get<BenchmarkSettings>()
            ?? throw new InvalidOperationException("BenchmarkSettings missing");

        _csvPath = _settings.ResolveFile("CsvTradeResult");
        _xmlPath = _settings.TryResolveFile("XmlMedium", out var xml) ? xml : string.Empty;
        // OrderLogMedium (~1.3 GB) is too heavy for BenchmarkDotNet — use --mode e2e.
    }

    [Benchmark]
    [BenchmarkCategory("xml")]
    public long ParseOnly_FuturesXml()
    {
        using var reader = DatasetDetector.OpenReader(_xmlPath, ImportDataset.FuturesXml);
        long rows = 0;
        while (reader.Read())
            rows++;
        return rows;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("csv")]
    public long ParseOnly_TradeResultCsv()
    {
        using var reader = DatasetDetector.OpenReader(_csvPath, ImportDataset.TradeResultCsv);
        long rows = 0;
        while (reader.Read())
            rows++;
        return rows;
    }

    [Benchmark]
    [BenchmarkCategory("xml")]
    public async Task<long> MsSql_StreamingBulk_FuturesXml()
    {
        var importer = new Importers.SqlServerBulkImporter(_settings.MsSqlConnectionString);
        await importer.EnsureSchemaAsync(ImportDataset.FuturesXml);
        await importer.TruncateAsync(ImportDataset.FuturesXml);
        await using var reader = DatasetDetector.OpenReader(_xmlPath, ImportDataset.FuturesXml);
        return await importer.BulkInsertAsync(ImportDataset.FuturesXml, reader, _settings.BatchSize);
    }

    [Benchmark]
    [BenchmarkCategory("csv")]
    public async Task<long> MsSql_StreamingBulk_TradeResultCsv()
    {
        var cols = DatasetDetector.PeekDynamicColumns(_csvPath, ImportDataset.TradeResultCsv);
        var importer = new Importers.SqlServerBulkImporter(_settings.MsSqlConnectionString);
        await importer.EnsureSchemaAsync(ImportDataset.TradeResultCsv, cols);
        await importer.TruncateAsync(ImportDataset.TradeResultCsv);
        await using var reader = DatasetDetector.OpenReader(_csvPath, ImportDataset.TradeResultCsv);
        return await importer.BulkInsertAsync(ImportDataset.TradeResultCsv, reader, _settings.BatchSize);
    }

    [Benchmark]
    [BenchmarkCategory("xml")]
    public async Task<long> Postgres_StreamingBulk_FuturesXml()
    {
        var importer = new Importers.PostgresCopyImporter(_settings.PostgresConnectionString);
        await importer.EnsureSchemaAsync(ImportDataset.FuturesXml);
        await importer.TruncateAsync(ImportDataset.FuturesXml);
        await using var reader = DatasetDetector.OpenReader(_xmlPath, ImportDataset.FuturesXml);
        return await importer.BulkInsertAsync(ImportDataset.FuturesXml, reader, _settings.BatchSize);
    }

    [Benchmark]
    [BenchmarkCategory("csv")]
    public async Task<long> Postgres_StreamingBulk_TradeResultCsv()
    {
        var cols = DatasetDetector.PeekDynamicColumns(_csvPath, ImportDataset.TradeResultCsv);
        var importer = new Importers.PostgresCopyImporter(_settings.PostgresConnectionString);
        await importer.EnsureSchemaAsync(ImportDataset.TradeResultCsv, cols);
        await importer.TruncateAsync(ImportDataset.TradeResultCsv);
        await using var reader = DatasetDetector.OpenReader(_csvPath, ImportDataset.TradeResultCsv);
        return await importer.BulkInsertAsync(ImportDataset.TradeResultCsv, reader, _settings.BatchSize);
    }
}
