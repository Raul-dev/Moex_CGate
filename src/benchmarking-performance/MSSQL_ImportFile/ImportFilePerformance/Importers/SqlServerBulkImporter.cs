using System.Data.Common;
using System.Text;
using ImportFilePerformance.Models;
using ImportFilePerformance.Readers;
using Microsoft.Data.SqlClient;

namespace ImportFilePerformance.Importers;

public sealed class SqlServerBulkImporter(string connectionString) : IBulkImporter
{
    public DatabaseKind Kind => DatabaseKind.MsSql;

    public async Task EnsureSchemaAsync(
        ImportDataset dataset,
        IReadOnlyList<string>? dynamicColumns = null,
        CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = BuildCreateSql(dataset, dynamicColumns);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task TruncateAsync(ImportDataset dataset, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"TRUNCATE TABLE dbo.[{DatasetTables.TableName(dataset)}];";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<long> BulkInsertAsync(
        ImportDataset dataset,
        DbDataReader reader,
        int batchSize,
        CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.CheckConstraints, null)
        {
            DestinationTableName = $"dbo.[{DatasetTables.TableName(dataset)}]",
            BatchSize = batchSize,
            BulkCopyTimeout = 0,
            EnableStreaming = true
        };

        for (var i = 0; i < reader.FieldCount; i++)
            bulk.ColumnMappings.Add(reader.GetName(i), reader.GetName(i));

        await bulk.WriteToServerAsync(reader, ct);
        return reader is StreamingDataReaderBase s ? s.RowsRead : -1;
    }

    public async Task<long> GetRowCountAsync(ImportDataset dataset, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT_BIG(*) FROM dbo.[{DatasetTables.TableName(dataset)}];";
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result);
    }

    private static string BuildCreateSql(ImportDataset dataset, IReadOnlyList<string>? dynamicColumns)
    {
        var table = DatasetTables.TableName(dataset);
        return dataset switch
        {
            ImportDataset.OrderLog => $"""
                IF OBJECT_ID(N'dbo.[{table}]', N'U') IS NULL
                CREATE TABLE dbo.[{table}] (
                    sess_id       bigint       NULL,
                    ticker        nvarchar(32) NULL,
                    buysell       nvarchar(1)  NULL,
                    time_str      nvarchar(32) NULL,
                    orderno       bigint       NULL,
                    action        int          NULL,
                    price         decimal(18,4) NULL,
                    volume        bigint       NULL,
                    tradeno       bigint       NULL,
                    tradeprice    decimal(18,4) NULL
                );
                """,
            ImportDataset.FuturesXml => $"""
                IF OBJECT_ID(N'dbo.[{table}]', N'U') IS NULL
                CREATE TABLE dbo.[{table}] (
                    report_date      nvarchar(32)  NULL,
                    board_id         nvarchar(32)  NULL,
                    base_asset_type  nvarchar(64)  NULL,
                    base_asset_code  nvarchar(64)  NULL,
                    base_asset_isin  nvarchar(64)  NULL,
                    futures_code     nvarchar(64)  NULL,
                    futures_name     nvarchar(256) NULL,
                    delivery_type    nvarchar(8)   NULL,
                    currency_id      nvarchar(16)  NULL,
                    lot              decimal(28,8) NULL,
                    min_step         decimal(28,8) NULL,
                    step_price       decimal(28,8) NULL,
                    trade_lot        decimal(28,8) NULL,
                    point_rate       decimal(28,8) NULL,
                    total_amount     decimal(28,8) NULL,
                    total_volume     decimal(28,8) NULL,
                    total_deal_count bigint        NULL,
                    max_deal_price   decimal(28,8) NULL,
                    min_deal_price   decimal(28,8) NULL,
                    last_deal_price  decimal(28,8) NULL,
                    clearing_price   decimal(28,8) NULL,
                    current_price    decimal(28,8) NULL
                );
                """,
            ImportDataset.TradeResultCsv => BuildDynamicCreate(table, dynamicColumns),
            _ => throw new NotSupportedException($"Dataset {dataset} not supported for MSSQL schema yet.")
        };
    }

    private static string BuildDynamicCreate(string table, IReadOnlyList<string>? columns)
    {
        if (columns is null || columns.Count == 0)
            throw new ArgumentException("TradeResultCsv requires dynamic column names from CSV header.");

        var sb = new StringBuilder();
        sb.AppendLine($"IF OBJECT_ID(N'dbo.[{table}]', N'U') IS NULL");
        sb.AppendLine($"CREATE TABLE dbo.[{table}] (");
        for (var i = 0; i < columns.Count; i++)
        {
            sb.Append($"    [{columns[i]}] nvarchar(512) NULL");
            sb.AppendLine(i < columns.Count - 1 ? "," : "");
        }
        sb.AppendLine(");");
        return sb.ToString();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
