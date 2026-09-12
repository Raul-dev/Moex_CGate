using System.Data;
using System.Text;
using System.Text.Json;
using ImportFilePerformance.Models;
using ImportFilePerformance.Readers;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace ImportFilePerformance.Importers;

public enum MessageBodyFormat
{
    /// <summary>[{"tradedate":"…","securityid":"…"}, …]</summary>
    NamedObjects,
    /// <summary>[["…","…"], …] — positional, like crs.load_OrdersLog OPENJSON '$[n]'.</summary>
    PositionalArrays
}

/// <summary>
/// Hardcoded TradeResult path mirroring publicdwh imp queue model:
/// CSV → JSON chunks (~ChunkRowSize) → msg_buffer → load proc → typed target.
/// Schema is created from CSV header at Ensure time (no generator).
/// </summary>
public static class TradeResultMessageBufferPipeline
{
    public const string MessageKey = "SPB.TradeResult.EQF";
    public const byte MetaAdapterCsv = 1;

    public static string BufferTable => "stg_trade_result_msg_buffer";
    public static string TargetTable => "stg_trade_result_target";
    public static string StringBufferTable => "stg_trade_result_string_buffer";
    public static string TypedTargetTable => "stg_trade_result_typed_target";
    public static string TypedCsvTable => "stg_trade_result_typed_csv";
    public static string LoadProcedure => "load_trade_result_from_buffer";
    public static string LoadProcedureShort => "load_trade_result_from_buffer_short";
    public static string LoadProcedureShortTyped => "load_trade_result_from_buffer_short_typed";
    public static string LoadProcedureFromStringBuffer => "load_trade_result_from_string_buffer";

    /// <summary>Target nvarchar width; string-buffer columns are 3× this (or 3× scalar display width).</summary>
    public const int TargetNvarcharLen = 512;
    public const int TypedCsvNvarcharLen = TargetNvarcharLen * 2; // 1024 — strings 2× expected
    public const int StringBufferNvarcharLen = TargetNvarcharLen * 3; // 1536
    public const int ScalarStringBufferLen = 64 * 3; // 192 for date/decimal/bigint source strings

    public static async Task EnsureSchemaAsync(
        DatabaseKind db,
        string connectionString,
        IReadOnlyList<string> columns,
        CancellationToken ct = default)
    {
        if (columns.Count == 0)
            throw new ArgumentException("TradeResult message-buffer path needs CSV header columns.");

        if (db == DatabaseKind.MsSql)
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);
            // CREATE/ALTER PROCEDURE must be alone in its batch (SqlClient has no GO).
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = BuildMsSqlTables(columns);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = BuildMsSqlLoadProcedureNamed(columns);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = BuildMsSqlLoadProcedureShort(columns);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = BuildMsSqlLoadProcedureShortTyped(columns);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = BuildMsSqlLoadProcedureFromStringBuffer(columns);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            return;
        }

        if (db == DatabaseKind.Postgres)
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = BuildPostgresSchema(columns);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public static async Task TruncateAsync(DatabaseKind db, string connectionString, CancellationToken ct = default)
    {
        if (db == DatabaseKind.MsSql)
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                IF OBJECT_ID(N'dbo.[{BufferTable}]', N'U') IS NOT NULL TRUNCATE TABLE dbo.[{BufferTable}];
                IF OBJECT_ID(N'dbo.[{TargetTable}]', N'U') IS NOT NULL TRUNCATE TABLE dbo.[{TargetTable}];
                IF OBJECT_ID(N'dbo.[{StringBufferTable}]', N'U') IS NOT NULL TRUNCATE TABLE dbo.[{StringBufferTable}];
                IF OBJECT_ID(N'dbo.[{TypedTargetTable}]', N'U') IS NOT NULL TRUNCATE TABLE dbo.[{TypedTargetTable}];
                IF OBJECT_ID(N'dbo.[{TypedCsvTable}]', N'U') IS NOT NULL TRUNCATE TABLE dbo.[{TypedCsvTable}];
                """;
            await cmd.ExecuteNonQueryAsync(ct);
            return;
        }

        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"TRUNCATE TABLE {BufferTable}, {TargetTable}, {StringBufferTable}, {TypedTargetTable}, {TypedCsvTable};";
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>
    /// Same client work as WriteChunksAsync (CSV → JSON chunks) but discards bodies — parser baseline.
    /// </summary>
    public static async Task<long> ParseChunksOnlyAsync(
        string filePath,
        int chunkRowSize,
        MessageBodyFormat format,
        CancellationToken ct = default)
    {
        chunkRowSize = Math.Clamp(chunkRowSize, 1, 5000);
        await using var reader = DelimitedCsvDataReader.Create(filePath, ';');
        var headers = reader.ColumnNames.ToArray();
        var fieldCount = headers.Length;

        var namedChunk = format == MessageBodyFormat.NamedObjects
            ? new List<Dictionary<string, string?>>(chunkRowSize)
            : null;
        var shortChunk = format == MessageBodyFormat.PositionalArrays
            ? new List<string?[]>(chunkRowSize)
            : null;

        long sourceRows = 0;
        long discardedBytes = 0;

        while (await reader.ReadAsync(ct))
        {
            if (namedChunk is not null)
            {
                var dict = new Dictionary<string, string?>(fieldCount, StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < fieldCount; i++)
                {
                    if (reader.IsDBNull(i))
                        dict[headers[i]] = null;
                    else
                        dict[headers[i]] = Convert.ToString(reader.GetValue(i));
                }
                namedChunk.Add(dict);
            }
            else
            {
                var values = new string?[fieldCount];
                for (var i = 0; i < fieldCount; i++)
                {
                    if (reader.IsDBNull(i))
                        values[i] = null;
                    else
                        values[i] = Convert.ToString(reader.GetValue(i));
                }
                shortChunk!.Add(values);
            }

            sourceRows++;

            var filled = namedChunk?.Count ?? shortChunk!.Count;
            if (filled < chunkRowSize)
                continue;

            var json = namedChunk is not null
                ? JsonSerializer.Serialize(namedChunk)
                : JsonSerializer.Serialize(shortChunk);
            discardedBytes += json.Length;
            namedChunk?.Clear();
            shortChunk?.Clear();
        }

        if ((namedChunk?.Count ?? 0) > 0 || (shortChunk?.Count ?? 0) > 0)
        {
            var json = namedChunk is not null
                ? JsonSerializer.Serialize(namedChunk)
                : JsonSerializer.Serialize(shortChunk);
            discardedBytes += json.Length;
        }

        _ = discardedBytes;
        return sourceRows;
    }

    /// <summary>Returns source CSV row count (not chunk count).</summary>
    public static async Task<long> WriteChunksAsync(
        DatabaseKind db,
        string connectionString,
        string filePath,
        int chunkRowSize,
        MessageBodyFormat format,
        CancellationToken ct = default)
    {
        chunkRowSize = Math.Clamp(chunkRowSize, 1, 5000);
        await using var reader = DelimitedCsvDataReader.Create(filePath, ';');
        var headers = reader.ColumnNames.ToArray();
        var fieldCount = headers.Length;

        var namedChunk = format == MessageBodyFormat.NamedObjects
            ? new List<Dictionary<string, string?>>(chunkRowSize)
            : null;
        var shortChunk = format == MessageBodyFormat.PositionalArrays
            ? new List<string?[]>(chunkRowSize)
            : null;

        long sourceRows = 0;
        var batch = CreateBufferBatch();

        async Task FlushAsync()
        {
            if (batch.Rows.Count == 0)
                return;
            if (db == DatabaseKind.MsSql)
                await BulkInsertMsSqlAsync(connectionString, batch, ct);
            else
                await BulkInsertPostgresAsync(connectionString, batch, ct);
            batch.Rows.Clear();
        }

        while (await reader.ReadAsync(ct))
        {
            if (namedChunk is not null)
            {
                var dict = new Dictionary<string, string?>(fieldCount, StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < fieldCount; i++)
                {
                    if (reader.IsDBNull(i))
                        dict[headers[i]] = null;
                    else
                        dict[headers[i]] = Convert.ToString(reader.GetValue(i));
                }
                namedChunk.Add(dict);
            }
            else
            {
                var values = new string?[fieldCount];
                for (var i = 0; i < fieldCount; i++)
                {
                    if (reader.IsDBNull(i))
                        values[i] = null;
                    else
                        values[i] = Convert.ToString(reader.GetValue(i));
                }
                shortChunk!.Add(values);
            }

            sourceRows++;

            var filled = namedChunk?.Count ?? shortChunk!.Count;
            if (filled < chunkRowSize)
                continue;

            AppendChunkRow(batch, namedChunk, shortChunk);
            namedChunk?.Clear();
            shortChunk?.Clear();
            if (batch.Rows.Count >= 50)
                await FlushAsync();
        }

        if ((namedChunk?.Count ?? 0) > 0 || (shortChunk?.Count ?? 0) > 0)
            AppendChunkRow(batch, namedChunk, shortChunk);

        await FlushAsync();
        return sourceRows;
    }

    public static async Task<long> LoadFromBufferAsync(
        DatabaseKind db,
        string connectionString,
        MessageBodyFormat format,
        CancellationToken ct = default,
        bool toTypedTarget = false)
    {
        var proc = format == MessageBodyFormat.PositionalArrays
            ? (toTypedTarget ? LoadProcedureShortTyped : LoadProcedureShort)
            : LoadProcedure;
        if (toTypedTarget && format != MessageBodyFormat.PositionalArrays)
            throw new ArgumentException("Typed load from message-buffer is implemented for positional (short) JSON only.");

        var destTable = toTypedTarget ? TypedTargetTable : TargetTable;

        if (db == DatabaseKind.MsSql)
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand($"dbo.[{proc}]", conn)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 0,
            };
            cmd.Parameters.Add("@SessionId", SqlDbType.BigInt).Value = 0L;
            await cmd.ExecuteNonQueryAsync(ct);

            await using var count = conn.CreateCommand();
            count.CommandText = $"SELECT COUNT_BIG(*) FROM dbo.[{destTable}];";
            return Convert.ToInt64(await count.ExecuteScalarAsync(ct));
        }

        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CALL {proc}(0);";
            cmd.CommandTimeout = 0;
            await cmd.ExecuteNonQueryAsync(ct);

            await using var count = conn.CreateCommand();
            count.CommandText = $"SELECT COUNT(*) FROM {destTable};";
            return Convert.ToInt64(await count.ExecuteScalarAsync(ct));
        }
    }

    /// <summary>
    /// CSV → SqlBulkCopy/COPY into all-string buffer (columns = target fields, nvarchar ×3), then load proc → typed target.
    /// </summary>
    public static async Task<long> WriteStringBufferAndLoadAsync(
        DatabaseKind db,
        string connectionString,
        string filePath,
        int batchSize,
        CancellationToken ct = default)
    {
        await WriteStringBufferAsync(db, connectionString, filePath, batchSize, ct);
        return await LoadFromStringBufferAsync(db, connectionString, ct);
    }

    public static async Task<long> WriteStringBufferAsync(
        DatabaseKind db,
        string connectionString,
        string filePath,
        int batchSize,
        CancellationToken ct = default)
    {
        batchSize = Math.Clamp(batchSize <= 0 ? 50_000 : batchSize, 1000, 500_000);
        await using var reader = DelimitedCsvDataReader.Create(filePath, ';');

        if (db == DatabaseKind.MsSql)
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);
            using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.TableLock, null)
            {
                DestinationTableName = $"dbo.[{StringBufferTable}]",
                BatchSize = batchSize,
                BulkCopyTimeout = 0,
                EnableStreaming = true,
            };
            for (var i = 0; i < reader.FieldCount; i++)
                bulk.ColumnMappings.Add(reader.GetName(i), reader.GetName(i));

            await bulk.WriteToServerAsync(reader, ct);
            return reader.RowsRead;
        }

        // Postgres COPY BINARY (strings)
        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync(ct);
            var cols = reader.ColumnNames;
            var colList = string.Join(", ", cols.Select(c => $"\"{c}\""));
            await using var importer = await conn.BeginBinaryImportAsync(
                $"COPY {StringBufferTable} ({colList}) FROM STDIN (FORMAT BINARY)", ct);

            long rows = 0;
            while (await reader.ReadAsync(ct))
            {
                await importer.StartRowAsync(ct);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.IsDBNull(i))
                        await importer.WriteNullAsync(ct);
                    else
                        await importer.WriteAsync(Convert.ToString(reader.GetValue(i)) ?? "", ct);
                }
                rows++;
            }
            await importer.CompleteAsync(ct);
            return rows;
        }
    }

    /// <summary>
    /// Streaming SqlBulkCopy/COPY into typed CSV staging. Reader yields strings;
    /// SqlClient/Npgsql convert into date/decimal/bigint (same as usual BCP typed destinations).
    /// </summary>
    public static async Task<long> WriteTypedCsvBulkAsync(
        DatabaseKind db,
        string connectionString,
        string filePath,
        int batchSize,
        CancellationToken ct = default)
    {
        batchSize = Math.Clamp(batchSize <= 0 ? 50_000 : batchSize, 1000, 500_000);
        await using var reader = DelimitedCsvDataReader.Create(filePath, ';');

        if (db == DatabaseKind.MsSql)
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);
            using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.TableLock, null)
            {
                DestinationTableName = $"dbo.[{TypedCsvTable}]",
                BatchSize = batchSize,
                BulkCopyTimeout = 0,
                EnableStreaming = true,
            };
            for (var i = 0; i < reader.FieldCount; i++)
                bulk.ColumnMappings.Add(reader.GetName(i), reader.GetName(i));

            await bulk.WriteToServerAsync(reader, ct);
            return reader.RowsRead;
        }

        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync(ct);
            var cols = reader.ColumnNames;
            var colList = string.Join(", ", cols.Select(c => $"\"{c}\""));
            // Text COPY: empty fields → NULL for typed columns; dispose completes the import.
            await using (var importer = await conn.BeginTextImportAsync(
                             $"COPY {TypedCsvTable} ({colList}) FROM STDIN (FORMAT csv)", ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    var fields = new string[reader.FieldCount];
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        if (reader.IsDBNull(i))
                            fields[i] = "";
                        else
                            fields[i] = EscapePgCopyCsv(Convert.ToString(reader.GetValue(i)) ?? "");
                    }
                    await importer.WriteAsync((string.Join(',', fields) + "\n").AsMemory(), ct);
                }
            }
            return reader.RowsRead;
        }
    }

    private static string EscapePgCopyCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    public static async Task<long> LoadFromStringBufferAsync(
        DatabaseKind db,
        string connectionString,
        CancellationToken ct = default)
    {
        if (db == DatabaseKind.MsSql)
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand($"dbo.[{LoadProcedureFromStringBuffer}]", conn)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 0,
            };
            await cmd.ExecuteNonQueryAsync(ct);

            await using var count = conn.CreateCommand();
            count.CommandText = $"SELECT COUNT_BIG(*) FROM dbo.[{TypedTargetTable}];";
            return Convert.ToInt64(await count.ExecuteScalarAsync(ct));
        }

        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CALL {LoadProcedureFromStringBuffer}();";
            cmd.CommandTimeout = 0;
            await cmd.ExecuteNonQueryAsync(ct);

            await using var count = conn.CreateCommand();
            count.CommandText = $"SELECT COUNT(*) FROM {TypedTargetTable};";
            return Convert.ToInt64(await count.ExecuteScalarAsync(ct));
        }
    }

    private static DataTable CreateBufferBatch()
    {
        var batch = new DataTable();
        batch.Columns.Add("SessionId", typeof(long));
        batch.Columns.Add("MessageId", typeof(Guid));
        batch.Columns.Add("MessageBody", typeof(string));
        batch.Columns.Add("MessageKey", typeof(string));
        batch.Columns.Add("MetaAdapterId", typeof(byte));
        batch.Columns.Add("IsError", typeof(bool));
        return batch;
    }

    private static void AppendChunkRow(
        DataTable batch,
        List<Dictionary<string, string?>>? namedChunk,
        List<string?[]>? shortChunk)
    {
        var body = namedChunk is not null
            ? JsonSerializer.Serialize(namedChunk)
            : JsonSerializer.Serialize(shortChunk);

        var row = batch.NewRow();
        row["SessionId"] = 0L;
        row["MessageId"] = Guid.NewGuid();
        row["MessageBody"] = body;
        row["MessageKey"] = MessageKey;
        row["MetaAdapterId"] = MetaAdapterCsv;
        row["IsError"] = false;
        batch.Rows.Add(row);
    }

    private static async Task BulkInsertMsSqlAsync(string cs, DataTable batch, CancellationToken ct)
    {
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync(ct);
        using var bulk = new SqlBulkCopy(conn)
        {
            DestinationTableName = $"dbo.[{BufferTable}]",
            BatchSize = batch.Rows.Count,
            BulkCopyTimeout = 0,
        };
        foreach (DataColumn col in batch.Columns)
            bulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        await bulk.WriteToServerAsync(batch, ct);
    }

    private static async Task BulkInsertPostgresAsync(string cs, DataTable batch, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync(ct);

        var sql = $"""
            INSERT INTO {BufferTable}
              (session_id, message_id, message_body, message_key, metaadapter_id, is_error)
            VALUES
              (@session_id, @message_id, @message_body, @message_key, @metaadapter_id, @is_error);
            """;

        foreach (DataRow row in batch.Rows)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("session_id", Convert.ToInt64(row["SessionId"]));
            cmd.Parameters.AddWithValue("message_id", (Guid)row["MessageId"]);
            cmd.Parameters.AddWithValue("message_body", Convert.ToString(row["MessageBody"]) ?? "");
            cmd.Parameters.AddWithValue("message_key", Convert.ToString(row["MessageKey"]) ?? "");
            cmd.Parameters.AddWithValue("metaadapter_id", Convert.ToInt16(row["MetaAdapterId"]));
            cmd.Parameters.AddWithValue("is_error", row["IsError"] is true);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static string BuildMsSqlTables(IReadOnlyList<string> columns)
    {
        var cols = columns.Select(SanitizeColumnName).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine($"""
            IF OBJECT_ID(N'dbo.[{BufferTable}]', N'U') IS NULL
            CREATE TABLE dbo.[{BufferTable}] (
              BufferId       bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
              SessionId      bigint NOT NULL,
              MessageId      uniqueidentifier NULL,
              MessageBody    nvarchar(max) NULL,
              MessageKey     nvarchar(256) NULL,
              MetaAdapterId  tinyint NULL,
              IsError        bit NOT NULL CONSTRAINT DF_{BufferTable}_IsError DEFAULT (0),
              CreatedAt      datetime2(4) NOT NULL CONSTRAINT DF_{BufferTable}_CreatedAt DEFAULT (sysdatetime())
            );
            """);

        // JSON path target (all strings) — unchanged for MessageBuffer* strategies
        sb.AppendLine($"IF OBJECT_ID(N'dbo.[{TargetTable}]', N'U') IS NULL");
        sb.AppendLine($"CREATE TABLE dbo.[{TargetTable}] (");
        sb.AppendLine("  Id bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,");
        for (var i = 0; i < cols.Length; i++)
        {
            sb.Append($"  [{cols[i]}] nvarchar({TargetNvarcharLen}) NULL");
            sb.AppendLine(i < cols.Length - 1 ? "," : "");
        }
        sb.AppendLine(");");

        // Typed target for MessageStringBufferThenLoad
        sb.AppendLine($"IF OBJECT_ID(N'dbo.[{TypedTargetTable}]', N'U') IS NULL");
        sb.AppendLine($"CREATE TABLE dbo.[{TypedTargetTable}] (");
        sb.AppendLine("  Id bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,");
        for (var i = 0; i < cols.Length; i++)
        {
            var map = MapTargetColumn(cols[i]);
            sb.Append($"  [{cols[i]}] {map.TargetSqlType} NULL");
            sb.AppendLine(i < cols.Length - 1 ? "," : "");
        }
        sb.AppendLine(");");

        // String buffer: same fields as typed target, all nvarchar, 3× length
        sb.AppendLine($"IF OBJECT_ID(N'dbo.[{StringBufferTable}]', N'U') IS NULL");
        sb.AppendLine($"CREATE TABLE dbo.[{StringBufferTable}] (");
        for (var i = 0; i < cols.Length; i++)
        {
            var map = MapTargetColumn(cols[i]);
            sb.Append($"  [{cols[i]}] nvarchar({map.BufferNvarcharLen}) NULL");
            sb.AppendLine(i < cols.Length - 1 ? "," : "");
        }
        sb.AppendLine(");");

        // StreamingBulkTyped: typed columns, string fields 2× expected width
        sb.AppendLine($"IF OBJECT_ID(N'dbo.[{TypedCsvTable}]', N'U') IS NULL");
        sb.AppendLine($"CREATE TABLE dbo.[{TypedCsvTable}] (");
        for (var i = 0; i < cols.Length; i++)
        {
            sb.Append($"  [{cols[i]}] {MapTypedCsvSqlType(cols[i])} NULL");
            sb.AppendLine(i < cols.Length - 1 ? "," : "");
        }
        sb.AppendLine(");");

        // Existing DBs may still have isaddress as tinyint (CSV has Y/N) — fix in place.
        sb.AppendLine($"""
            IF COL_LENGTH(N'dbo.[{TypedCsvTable}]', N'isaddress') IS NOT NULL
              ALTER TABLE dbo.[{TypedCsvTable}] ALTER COLUMN [isaddress] nvarchar({TypedCsvNvarcharLen}) NULL;
            IF COL_LENGTH(N'dbo.[{TypedTargetTable}]', N'isaddress') IS NOT NULL
              ALTER TABLE dbo.[{TypedTargetTable}] ALTER COLUMN [isaddress] nvarchar({TargetNvarcharLen}) NULL;
            IF COL_LENGTH(N'dbo.[{StringBufferTable}]', N'isaddress') IS NOT NULL
              ALTER TABLE dbo.[{StringBufferTable}] ALTER COLUMN [isaddress] nvarchar({StringBufferNvarcharLen}) NULL;
            """);
        return sb.ToString();
    }

    private static string MapTypedCsvSqlType(string col)
    {
        var map = MapTargetColumn(col);
        if (map.TargetSqlType.StartsWith("nvarchar", StringComparison.OrdinalIgnoreCase))
            return $"nvarchar({TypedCsvNvarcharLen})";
        return map.TargetSqlType;
    }

    private static string BuildMsSqlLoadProcedureFromStringBuffer(IReadOnlyList<string> columns)
    {
        var cols = columns.Select(SanitizeColumnName).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE OR ALTER PROCEDURE dbo.[{LoadProcedureFromStringBuffer}]");
        sb.AppendLine("AS");
        sb.AppendLine("BEGIN");
        sb.AppendLine("  SET NOCOUNT ON;");
        sb.AppendLine($"  INSERT INTO dbo.[{TypedTargetTable}] (");
        sb.AppendLine("    " + string.Join(", ", cols.Select(c => $"[{c}]")));
        sb.AppendLine("  )");
        sb.AppendLine("  SELECT");
        sb.AppendLine("    " + string.Join(",\n    ", cols.Select(c =>
        {
            var map = MapTargetColumn(c, srcAlias: "s");
            return $"{map.ConvertExpr} AS [{c}]";
        })));
        sb.AppendLine($"  FROM dbo.[{StringBufferTable}] s;");
        sb.AppendLine("END;");
        return sb.ToString();
    }

    /// <summary>
    /// Maps TradeResult column → typed target SQL + string-buffer width (3×) + CONVERT from alias.
    /// </summary>
    private static (string TargetSqlType, int BufferNvarcharLen, string ConvertExpr) MapTargetColumn(
        string col,
        string srcAlias = "s")
    {
        var a = srcAlias;
        if (col is "tradedate" || col.EndsWith("date", StringComparison.Ordinal))
            return ("date", ScalarStringBufferLen,
                $"TRY_CONVERT(date, NULLIF(LTRIM(RTRIM({a}.[{col}])), ''))");

        if (col.Contains("count", StringComparison.Ordinal) || col.EndsWith("count", StringComparison.Ordinal))
            return ("bigint", ScalarStringBufferLen,
                $"TRY_CONVERT(bigint, NULLIF(LTRIM(RTRIM(REPLACE({a}.[{col}], ',', '.'))), ''))");

        if (col.Contains("price", StringComparison.Ordinal)
            || col.Contains("volume", StringComparison.Ordinal)
            || col.Contains("amount", StringComparison.Ordinal)
            || col is "facevalue" or "waprice" or "accruedinterest" or "mp2volume" or "mp3volume"
            || col.Contains("trend", StringComparison.Ordinal))
            return ("decimal(28,8)", ScalarStringBufferLen,
                $"TRY_CONVERT(decimal(28,8), NULLIF(LTRIM(RTRIM(REPLACE({a}.[{col}], ',', '.'))), ''))");

        // Flags like isaddress are Y/N in SPB CSV — keep as string (not tinyint).
        return ($"nvarchar({TargetNvarcharLen})", StringBufferNvarcharLen,
            $"CONVERT(nvarchar({TargetNvarcharLen}), {a}.[{col}])");
    }

    private static string BuildMsSqlLoadProcedureNamed(IReadOnlyList<string> columns)
    {
        var cols = columns.Select(SanitizeColumnName).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE OR ALTER PROCEDURE dbo.[{LoadProcedure}]");
        sb.AppendLine("  @SessionId bigint = 0");
        sb.AppendLine("AS");
        sb.AppendLine("BEGIN");
        sb.AppendLine("  SET NOCOUNT ON;");
        sb.AppendLine($"  INSERT INTO dbo.[{TargetTable}] (");
        sb.AppendLine("    " + string.Join(", ", cols.Select(c => $"[{c}]")));
        sb.AppendLine("  )");
        sb.AppendLine("  SELECT");
        sb.AppendLine("    " + string.Join(",\n    ", cols.Select(c => $"j.[{c}]")));
        sb.AppendLine($"  FROM dbo.[{BufferTable}] b");
        sb.AppendLine("  CROSS APPLY OPENJSON(b.MessageBody) WITH (");
        for (var i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            sb.Append($"    [{c}] nvarchar(512) '$.{c}'");
            sb.AppendLine(i < cols.Length - 1 ? "," : "");
        }
        sb.AppendLine("  ) j");
        sb.AppendLine("  WHERE b.SessionId = @SessionId AND b.IsError = 0;");
        sb.AppendLine("END;");
        return sb.ToString();
    }

    /// <summary>Positional arrays like [crs].[load_OrdersLog]: OPENJSON … WITH (col '$[0]', …).</summary>
    private static string BuildMsSqlLoadProcedureShort(IReadOnlyList<string> columns)
    {
        var cols = columns.Select(SanitizeColumnName).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE OR ALTER PROCEDURE dbo.[{LoadProcedureShort}]");
        sb.AppendLine("  @SessionId bigint = 0");
        sb.AppendLine("AS");
        sb.AppendLine("BEGIN");
        sb.AppendLine("  SET NOCOUNT ON;");
        sb.AppendLine($"  INSERT INTO dbo.[{TargetTable}] (");
        sb.AppendLine("    " + string.Join(", ", cols.Select(c => $"[{c}]")));
        sb.AppendLine("  )");
        sb.AppendLine("  SELECT");
        sb.AppendLine("    " + string.Join(",\n    ", cols.Select(c => $"j.[{c}]")));
        sb.AppendLine($"  FROM dbo.[{BufferTable}] b");
        sb.AppendLine("  CROSS APPLY (");
        sb.AppendLine("    SELECT *");
        sb.AppendLine("    FROM OPENJSON(b.MessageBody, '$')");
        sb.AppendLine("    WITH (");
        for (var i = 0; i < cols.Length; i++)
        {
            sb.Append($"      [{cols[i]}] nvarchar(512) '$[{i}]'");
            sb.AppendLine(i < cols.Length - 1 ? "," : "");
        }
        sb.AppendLine("    )");
        sb.AppendLine("  ) j");
        sb.AppendLine("  WHERE b.SessionId = @SessionId AND b.IsError = 0;");
        sb.AppendLine("END;");
        return sb.ToString();
    }

    /// <summary>Short JSON ($[n]) + TRY_CONVERT into typed target.</summary>
    private static string BuildMsSqlLoadProcedureShortTyped(IReadOnlyList<string> columns)
    {
        var cols = columns.Select(SanitizeColumnName).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE OR ALTER PROCEDURE dbo.[{LoadProcedureShortTyped}]");
        sb.AppendLine("  @SessionId bigint = 0");
        sb.AppendLine("AS");
        sb.AppendLine("BEGIN");
        sb.AppendLine("  SET NOCOUNT ON;");
        sb.AppendLine($"  INSERT INTO dbo.[{TypedTargetTable}] (");
        sb.AppendLine("    " + string.Join(", ", cols.Select(c => $"[{c}]")));
        sb.AppendLine("  )");
        sb.AppendLine("  SELECT");
        sb.AppendLine("    " + string.Join(",\n    ", cols.Select(c =>
        {
            var map = MapTargetColumn(c, srcAlias: "j");
            return $"{map.ConvertExpr} AS [{c}]";
        })));
        sb.AppendLine($"  FROM dbo.[{BufferTable}] b");
        sb.AppendLine("  CROSS APPLY (");
        sb.AppendLine("    SELECT *");
        sb.AppendLine("    FROM OPENJSON(b.MessageBody, '$')");
        sb.AppendLine("    WITH (");
        for (var i = 0; i < cols.Length; i++)
        {
            sb.Append($"      [{cols[i]}] nvarchar(512) '$[{i}]'");
            sb.AppendLine(i < cols.Length - 1 ? "," : "");
        }
        sb.AppendLine("    )");
        sb.AppendLine("  ) j");
        sb.AppendLine("  WHERE b.SessionId = @SessionId AND b.IsError = 0;");
        sb.AppendLine("END;");
        return sb.ToString();
    }

    private static string BuildPostgresSchema(IReadOnlyList<string> columns)
    {
        var cols = columns.Select(SanitizeColumnName).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine($"""
            CREATE TABLE IF NOT EXISTS {BufferTable} (
              buffer_id       bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
              session_id      bigint NOT NULL,
              message_id      uuid NULL,
              message_body    text NULL,
              message_key     varchar(256) NULL,
              metaadapter_id  smallint NULL,
              is_error        boolean NOT NULL DEFAULT false,
              created_at      timestamp without time zone NOT NULL DEFAULT now()
            );
            """);

        sb.AppendLine($"CREATE TABLE IF NOT EXISTS {TargetTable} (");
        sb.AppendLine("  id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,");
        for (var i = 0; i < cols.Length; i++)
        {
            sb.Append($"  \"{cols[i]}\" varchar(512) NULL");
            sb.AppendLine(i < cols.Length - 1 ? "," : "");
        }
        sb.AppendLine(");");

        // Named objects
        sb.AppendLine($"CREATE OR REPLACE PROCEDURE {LoadProcedure}(p_session_id bigint DEFAULT 0)");
        sb.AppendLine("LANGUAGE plpgsql AS $$");
        sb.AppendLine("BEGIN");
        sb.AppendLine($"  INSERT INTO {TargetTable} (");
        sb.AppendLine("    " + string.Join(", ", cols.Select(c => $"\"{c}\"")));
        sb.AppendLine("  )");
        sb.AppendLine("  SELECT");
        sb.AppendLine("    " + string.Join(",\n    ", cols.Select(c =>
            $"(e.elem ->> '{c}')")));
        sb.AppendLine($"  FROM {BufferTable} b");
        sb.AppendLine("  CROSS JOIN LATERAL jsonb_array_elements(b.message_body::jsonb) AS e(elem)");
        sb.AppendLine("  WHERE b.session_id = p_session_id AND b.is_error = false;");
        sb.AppendLine("END;");
        sb.AppendLine("$$;");

        // Positional arrays: elem->>0, elem->>1, …
        sb.AppendLine($"CREATE OR REPLACE PROCEDURE {LoadProcedureShort}(p_session_id bigint DEFAULT 0)");
        sb.AppendLine("LANGUAGE plpgsql AS $$");
        sb.AppendLine("BEGIN");
        sb.AppendLine($"  INSERT INTO {TargetTable} (");
        sb.AppendLine("    " + string.Join(", ", cols.Select(c => $"\"{c}\"")));
        sb.AppendLine("  )");
        sb.AppendLine("  SELECT");
        sb.AppendLine("    " + string.Join(",\n    ", Enumerable.Range(0, cols.Length).Select(i =>
            $"(e.elem ->> {i})")));
        sb.AppendLine($"  FROM {BufferTable} b");
        sb.AppendLine("  CROSS JOIN LATERAL jsonb_array_elements(b.message_body::jsonb) AS e(elem)");
        sb.AppendLine("  WHERE b.session_id = p_session_id AND b.is_error = false;");
        sb.AppendLine("END;");
        sb.AppendLine("$$;");

        // Typed target + string buffer first (needed by short_typed + string load)
        sb.AppendLine($"CREATE TABLE IF NOT EXISTS {TypedTargetTable} (");
        sb.AppendLine("  id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,");
        for (var i = 0; i < cols.Length; i++)
        {
            var map = MapTargetColumnPg(cols[i]);
            sb.Append($"  \"{cols[i]}\" {map.TargetSqlType} NULL");
            sb.AppendLine(i < cols.Length - 1 ? "," : "");
        }
        sb.AppendLine(");");

        sb.AppendLine($"CREATE TABLE IF NOT EXISTS {StringBufferTable} (");
        for (var i = 0; i < cols.Length; i++)
        {
            var map = MapTargetColumnPg(cols[i]);
            sb.Append($"  \"{cols[i]}\" varchar({map.BufferLen}) NULL");
            sb.AppendLine(i < cols.Length - 1 ? "," : "");
        }
        sb.AppendLine(");");

        sb.AppendLine($"CREATE TABLE IF NOT EXISTS {TypedCsvTable} (");
        for (var i = 0; i < cols.Length; i++)
        {
            var map = MapTargetColumnPg(cols[i]);
            var sqlType = map.TargetSqlType.StartsWith("varchar", StringComparison.OrdinalIgnoreCase)
                ? $"varchar({TypedCsvNvarcharLen})"
                : map.TargetSqlType;
            sb.Append($"  \"{cols[i]}\" {sqlType} NULL");
            sb.AppendLine(i < cols.Length - 1 ? "," : "");
        }
        sb.AppendLine(");");

        // Short + typed convert
        sb.AppendLine($"CREATE OR REPLACE PROCEDURE {LoadProcedureShortTyped}(p_session_id bigint DEFAULT 0)");
        sb.AppendLine("LANGUAGE plpgsql AS $$");
        sb.AppendLine("BEGIN");
        sb.AppendLine($"  INSERT INTO {TypedTargetTable} (");
        sb.AppendLine("    " + string.Join(", ", cols.Select(c => $"\"{c}\"")));
        sb.AppendLine("  )");
        sb.AppendLine("  SELECT");
        sb.AppendLine("    " + string.Join(",\n    ", cols.Select((c, i) => MapTargetColumnPgFromJson(c, i))));
        sb.AppendLine($"  FROM {BufferTable} b");
        sb.AppendLine("  CROSS JOIN LATERAL jsonb_array_elements(b.message_body::jsonb) AS e(elem)");
        sb.AppendLine("  WHERE b.session_id = p_session_id AND b.is_error = false;");
        sb.AppendLine("END;");
        sb.AppendLine("$$;");

        sb.AppendLine($"CREATE OR REPLACE PROCEDURE {LoadProcedureFromStringBuffer}()");
        sb.AppendLine("LANGUAGE plpgsql AS $$");
        sb.AppendLine("BEGIN");
        sb.AppendLine($"  INSERT INTO {TypedTargetTable} (");
        sb.AppendLine("    " + string.Join(", ", cols.Select(c => $"\"{c}\"")));
        sb.AppendLine("  )");
        sb.AppendLine("  SELECT");
        sb.AppendLine("    " + string.Join(",\n    ", cols.Select(c => MapTargetColumnPg(c).ConvertExpr)));
        sb.AppendLine($"  FROM {StringBufferTable} s;");
        sb.AppendLine("END;");
        sb.AppendLine("$$;");
        return sb.ToString();
    }

    private static (string TargetSqlType, int BufferLen, string ConvertExpr) MapTargetColumnPg(string col)
    {
        if (col is "tradedate" || col.EndsWith("date", StringComparison.Ordinal))
            return ("date", ScalarStringBufferLen,
                $"NULLIF(TRIM(s.\"{col}\"), '')::date");

        if (col.Contains("count", StringComparison.Ordinal))
            return ("bigint", ScalarStringBufferLen,
                $"NULLIF(REPLACE(TRIM(s.\"{col}\"), ',', '.'), '')::bigint");

        if (col.Contains("price", StringComparison.Ordinal)
            || col.Contains("volume", StringComparison.Ordinal)
            || col.Contains("amount", StringComparison.Ordinal)
            || col is "facevalue" or "waprice" or "accruedinterest" or "mp2volume" or "mp3volume"
            || col.Contains("trend", StringComparison.Ordinal))
            return ("numeric(28,8)", ScalarStringBufferLen,
                $"NULLIF(REPLACE(TRIM(s.\"{col}\"), ',', '.'), '')::numeric(28,8)");

        return ($"varchar({TargetNvarcharLen})", StringBufferNvarcharLen,
            $"LEFT(s.\"{col}\", {TargetNvarcharLen})");
    }

    private static string MapTargetColumnPgFromJson(string col, int index)
    {
        var raw = $"(e.elem ->> {index})";
        if (col is "tradedate" || col.EndsWith("date", StringComparison.Ordinal))
            return $"NULLIF(TRIM({raw}), '')::date";
        if (col.Contains("count", StringComparison.Ordinal))
            return $"NULLIF(REPLACE(TRIM({raw}), ',', '.'), '')::bigint";
        if (col.Contains("price", StringComparison.Ordinal)
            || col.Contains("volume", StringComparison.Ordinal)
            || col.Contains("amount", StringComparison.Ordinal)
            || col is "facevalue" or "waprice" or "accruedinterest" or "mp2volume" or "mp3volume"
            || col.Contains("trend", StringComparison.Ordinal))
            return $"NULLIF(REPLACE(TRIM({raw}), ',', '.'), '')::numeric(28,8)";
        return $"LEFT({raw}, {TargetNvarcharLen})";
    }

    /// <summary>Must match <see cref="DelimitedCsvDataReader"/> column sanitization.</summary>
    private static string SanitizeColumnName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "col";

        var sb = new StringBuilder(name.Length);
        foreach (var c in name.Trim())
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                sb.Append(c);
            else
                sb.Append('_');
        }

        var s = sb.ToString();
        if (s.Length == 0 || char.IsDigit(s[0]))
            s = "c_" + s;
        return s.ToLowerInvariant();
    }
}
