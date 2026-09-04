using System.Data.Common;
using ImportFilePerformance.Models;

namespace ImportFilePerformance.Importers;

public interface IBulkImporter : IAsyncDisposable
{
    DatabaseKind Kind { get; }
    Task EnsureSchemaAsync(ImportDataset dataset, IReadOnlyList<string>? dynamicColumns = null, CancellationToken ct = default);
    Task TruncateAsync(ImportDataset dataset, CancellationToken ct = default);
    Task<long> BulkInsertAsync(ImportDataset dataset, DbDataReader reader, int batchSize, CancellationToken ct = default);
    Task<long> GetRowCountAsync(ImportDataset dataset, CancellationToken ct = default);
}

public static class DatasetTables
{
    public static string TableName(ImportDataset dataset) => dataset switch
    {
        ImportDataset.OrderLog => "stg_order_log",
        ImportDataset.FuturesXml => "stg_futures_xml",
        ImportDataset.SecurityXml => "stg_security_xml",
        ImportDataset.TradeResultCsv => "stg_trade_result_csv",
        _ => throw new ArgumentOutOfRangeException(nameof(dataset))
    };
}
