using System.Data.Common;
using System.Text;
using ImportFilePerformance.Models;
using ImportFilePerformance.Readers;
using Npgsql;
using NpgsqlTypes;

namespace ImportFilePerformance.Importers;

public sealed class PostgresCopyImporter(string connectionString) : IBulkImporter
{
    public DatabaseKind Kind => DatabaseKind.Postgres;

    public async Task EnsureSchemaAsync(
        ImportDataset dataset,
        IReadOnlyList<string>? dynamicColumns = null,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = BuildCreateSql(dataset, dynamicColumns);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task TruncateAsync(ImportDataset dataset, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"TRUNCATE TABLE {DatasetTables.TableName(dataset)};";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<long> BulkInsertAsync(
        ImportDataset dataset,
        DbDataReader reader,
        int batchSize,
        CancellationToken ct = default)
    {
        // batchSize reserved for API symmetry; COPY streams continuously.
        _ = batchSize;

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        var table = DatasetTables.TableName(dataset);
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        var colList = string.Join(", ", columns.Select(c => $"\"{c}\""));

        await using var importer = await conn.BeginBinaryImportAsync(
            $"COPY {table} ({colList}) FROM STDIN (FORMAT BINARY)", ct);

        long rows = 0;
        while (await reader.ReadAsync(ct))
        {
            await importer.StartRowAsync(ct);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                if (reader.IsDBNull(i))
                {
                    await importer.WriteNullAsync(ct);
                    continue;
                }

                var value = reader.GetValue(i);
                await WriteValueAsync(importer, value, ct);
            }
            rows++;
        }

        await importer.CompleteAsync(ct);
        return rows;
    }

    private static async Task WriteValueAsync(NpgsqlBinaryImporter importer, object value, CancellationToken ct)
    {
        switch (value)
        {
            case string s:
                await importer.WriteAsync(s, NpgsqlDbType.Text, ct);
                break;
            case long l:
                await importer.WriteAsync(l, NpgsqlDbType.Bigint, ct);
                break;
            case int i:
                await importer.WriteAsync(i, NpgsqlDbType.Integer, ct);
                break;
            case decimal d:
                await importer.WriteAsync(d, NpgsqlDbType.Numeric, ct);
                break;
            case double dbl:
                await importer.WriteAsync(dbl, NpgsqlDbType.Double, ct);
                break;
            case bool b:
                await importer.WriteAsync(b, NpgsqlDbType.Boolean, ct);
                break;
            case DateTime dt:
                await importer.WriteAsync(dt, NpgsqlDbType.Timestamp, ct);
                break;
            default:
                await importer.WriteAsync(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    NpgsqlDbType.Text, ct);
                break;
        }
    }

    public async Task<long> GetRowCountAsync(ImportDataset dataset, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {DatasetTables.TableName(dataset)};";
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result);
    }

    private static string BuildCreateSql(ImportDataset dataset, IReadOnlyList<string>? dynamicColumns)
    {
        var table = DatasetTables.TableName(dataset);
        return dataset switch
        {
            ImportDataset.OrderLog => $"""
                CREATE TABLE IF NOT EXISTS {table} (
                    sess_id       bigint,
                    ticker        text,
                    buysell       text,
                    time_str      text,
                    orderno       bigint,
                    action        integer,
                    price         numeric(18,4),
                    volume        bigint,
                    tradeno       bigint,
                    tradeprice    numeric(18,4)
                );
                """,
            ImportDataset.FuturesXml => $"""
                CREATE TABLE IF NOT EXISTS {table} (
                    report_date      text,
                    board_id         text,
                    base_asset_type  text,
                    base_asset_code  text,
                    base_asset_isin  text,
                    futures_code     text,
                    futures_name     text,
                    delivery_type    text,
                    currency_id      text,
                    lot              numeric(28,8),
                    min_step         numeric(28,8),
                    step_price       numeric(28,8),
                    trade_lot        numeric(28,8),
                    point_rate       numeric(28,8),
                    total_amount     numeric(28,8),
                    total_volume     numeric(28,8),
                    total_deal_count bigint,
                    max_deal_price   numeric(28,8),
                    min_deal_price   numeric(28,8),
                    last_deal_price  numeric(28,8),
                    clearing_price   numeric(28,8),
                    current_price    numeric(28,8)
                );
                """,
            ImportDataset.TradeResultCsv => BuildDynamicCreate(table, dynamicColumns),
            _ => throw new NotSupportedException($"Dataset {dataset} not supported for Postgres schema yet.")
        };
    }

    private static string BuildDynamicCreate(string table, IReadOnlyList<string>? columns)
    {
        if (columns is null || columns.Count == 0)
            throw new ArgumentException("TradeResultCsv requires dynamic column names from CSV header.");

        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE IF NOT EXISTS {table} (");
        for (var i = 0; i < columns.Count; i++)
        {
            sb.Append($"    \"{columns[i]}\" text");
            sb.AppendLine(i < columns.Count - 1 ? "," : "");
        }
        sb.AppendLine(");");
        return sb.ToString();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
