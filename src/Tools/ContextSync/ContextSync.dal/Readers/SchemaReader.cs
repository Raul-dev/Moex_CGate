using System.Data;
using ContextSync.dal.Abstractions;
using ContextSync.dal.Models;
using ContextSync.dal.Settings;
using Microsoft.Data.SqlClient;
using Serilog;

namespace ContextSync.dal.Readers;

public class SchemaReader : ISchemaReader
{
    private readonly DatabaseSettings _settings;

    public SchemaReader(DatabaseSettings settings)
    {
        _settings = settings;
    }

    public async Task<List<string>> GetTableNamesAsync(
        string? tableFilter = null,
        CancellationToken cancellationToken = default)
    {
        var result = new List<string>();
        var connStr = _settings.BuildConnectionString();

        const string sql = @"
            SELECT t.name AS table_name
            FROM sys.tables t
            WHERE (@tableFilter IS NULL OR t.name LIKE @tableFilter)
              AND t.is_ms_shipped = 0
            ORDER BY t.name;";

        try
        {
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(cancellationToken);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = _settings.CommandTimeoutSeconds;
            cmd.Parameters.Add(new SqlParameter("@tableFilter", tableFilter ?? (object)DBNull.Value));

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(reader.GetString("table_name"));
            }

            Log.Information("Found {Count} tables (filter={Filter})", result.Count, tableFilter ?? "*");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SchemaReader: error reading table names");
        }

        return result;
    }

    public async Task<List<TableSchema>> ReadSchemaAsync(
        string? tableName = null,
        string? tableFilter = null,
        CancellationToken cancellationToken = default)
    {
        var result = new List<TableSchema>();
        var connStr = _settings.BuildConnectionString();

        const string sql = @"
            SELECT
                t.name  AS table_name,
                c.column_id,
                c.name  AS column_name,
                tp.name AS data_type,
                c.is_nullable,
                CASE WHEN fk.parent_column_id IS NOT NULL
                     THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS is_foreign_key,
                rt.name AS referenced_table,
                rc.name AS referenced_column
            FROM sys.tables t
            INNER JOIN sys.columns c  ON t.object_id = c.object_id
            INNER JOIN sys.types   tp ON c.user_type_id = tp.user_type_id
            LEFT JOIN sys.foreign_key_columns fk
                   ON fk.parent_object_id = t.object_id
                  AND fk.parent_column_id = c.column_id
            LEFT JOIN sys.tables  rt ON fk.referenced_object_id = rt.object_id
            LEFT JOIN sys.columns rc
                   ON fk.referenced_object_id = rc.object_id
                  AND fk.referenced_column_id = rc.column_id
            WHERE (@tableName  IS NULL OR t.name = @tableName)
              AND (@tableFilter IS NULL OR t.name LIKE @tableFilter)
              AND t.is_ms_shipped = 0
            ORDER BY t.name, c.column_id;";

        try
        {
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(cancellationToken);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = _settings.CommandTimeoutSeconds;
            cmd.Parameters.Add(new SqlParameter("@tableName", tableName ?? (object)DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@tableFilter", tableFilter ?? (object)DBNull.Value));

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            var map = new Dictionary<string, TableSchema>();

            while (await reader.ReadAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var tbl = reader.GetString("table_name");

                if (!map.TryGetValue(tbl, out var schema))
                {
                    schema = new TableSchema { TableName = tbl };
                    map[tbl] = schema;
                }

                schema.Columns.Add(new ColumnSchema
                {
                    ColumnId = reader.GetInt32("column_id"),
                    Name = reader.GetString("column_name"),
                    DataType = reader.GetString("data_type"),
                    IsNullable = reader.GetBoolean("is_nullable"),
                    IsForeignKey = reader.GetBoolean("is_foreign_key"),
                    ReferencedTable = reader.IsDBNull("referenced_table") ? null : reader.GetString("referenced_table"),
                    ReferencedColumn = reader.IsDBNull("referenced_column") ? null : reader.GetString("referenced_column")
                });
            }

            result = map.Values.OrderBy(t => t.TableName).ToList();
            Log.Information("Read schema for {Count} tables", result.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SchemaReader: error reading schema");
        }

        return result;
    }
}
