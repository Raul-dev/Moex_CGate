using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ApplyProcLog.dal;

/// <summary>
/// Информация о таблице для экспорта данных.
/// </summary>
public class TableInfo
{
    /// <summary>
    /// Имя схемы (например, "DBTest").
    /// </summary>
    public string SchemaName { get; set; } = string.Empty;

    /// <summary>
    /// Имя таблицы (например, "Accounts").
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Полное имя таблицы в формате [Schema].[Table].
    /// </summary>
    public string FullName => $"[{SchemaName}].[{TableName}]";

    public override string ToString() => FullName;
}

/// <summary>
/// Результат экспорта данных одной таблицы.
/// </summary>
public class TableExportResult
{
    public TableInfo Table { get; set; } = new();
    public int TotalRows { get; set; }
    public int FilesCreated { get; set; }
    public long TotalBytes { get; set; }
    public List<string> OutputFiles { get; set; } = new();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Результат экспорта для коллекции таблиц.
/// </summary>
public class DataExportResult
{
    public int TotalTables { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public long TotalBytes { get; set; }
    public List<TableExportResult> TableResults { get; set; } = new();
    public int TotalFilesCreated => TableResults.Sum(t => t.FilesCreated);
}

/// <summary>
/// Результат генерации одного файла данных.
/// </summary>
public class DataFileResult
{
    /// <summary>
    /// Путь к файлу.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Размер файла в байтах.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Количество INSERT-операторов в файле.
    /// </summary>
    public int InsertCount { get; set; }
}

/// <summary>
/// Экстрактор данных таблиц с разбивкой по размеру файлов.
/// </summary>
public class TableDataExtractor
{
    private readonly string _connectionString;
    private readonly TestDBContext _context;
    private readonly Encoding _encoding;

    /// <summary>
    /// Размер одного файла данных по умолчанию (200 КБ).
    /// </summary>
    public const int DefaultMaxFileSizeBytes = 200 * 1024;

    /// <summary>
    /// Максимальный размер одного файла в байтах.
    /// </summary>
    public int MaxFileSizeBytes { get; set; } = DefaultMaxFileSizeBytes;

    /// <summary>
    /// Количество строк для выборки за один запрос (память).
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// Каталог для сохранения файлов.
    /// </summary>
    public string OutputDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "TableDataExport");

    /// <summary>
    /// Префикс имени файла (по умолчанию - имя таблицы).
    /// </summary>
    public string FileNamePrefix { get; set; } = string.Empty;

    /// <summary>
    /// Включать schema qualified имя таблицы в имя файла.
    /// </summary>
    public bool IncludeSchemaInFileName { get; set; } = true;

    /// <summary>
    /// Добавлять GO после каждого INSERT.
    /// </summary>
    public bool AppendGoAfterInsert { get; set; } = false;

    /// <summary>
    /// Экранировать имена колонок в квадратные скобки.
    /// </summary>
    public bool QuoteColumnNames { get; set; } = true;

    /// <summary>
    /// Список типов колонок, которые следует пропускать.
    /// </summary>
    public HashSet<string> ExcludedColumnTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "varbinary",
        "binary",
        "image",
        "xml",
        "text",
        "ntext"
    };

    public TableDataExtractor(string connectionString)
    {
        _connectionString = connectionString;
        var optionsBuilder = new DbContextOptionsBuilder<TestDBContext>();
        optionsBuilder.UseSqlServer(connectionString);
        _context = new TestDBContext(optionsBuilder.Options);
        _encoding = new UTF8Encoding(false);
    }

    public TableDataExtractor(TestDBContext context)
    {
        _context = context;
        var optionsBuilder = new DbContextOptionsBuilder<TestDBContext>();
        optionsBuilder.UseSqlServer(_connectionString);
        _encoding = new UTF8Encoding(false);
    }

    /// <summary>
    /// Экспортирует данные указанных таблиц в файлы.
    /// </summary>
    /// <param name="tables">Коллекция таблиц для экспорта.</param>
    /// <param name="outputDir">Каталог для сохранения файлов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат экспорта.</returns>
    public async Task<DataExportResult> ExportTablesAsync(
        IEnumerable<TableInfo> tables,
        string? outputDir = null,
        CancellationToken cancellationToken = default)
    {
        var result = new DataExportResult();
        var tablesList = tables.ToList();
        result.TotalTables = tablesList.Count;

        var outputDirectory = outputDir ?? OutputDirectory;
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        foreach (var table in tablesList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tableResult = await ExportSingleTableAsync(table, outputDirectory, cancellationToken);
            result.TableResults.Add(tableResult);

            if (tableResult.Success)
            {
                result.SuccessCount++;
                result.TotalBytes += tableResult.TotalBytes;
            }
            else
            {
                result.ErrorCount++;
            }
        }

        return result;
    }

    /// <summary>
    /// Экспортирует данные одной таблицы в файлы указанного размера.
    /// </summary>
    public async Task<TableExportResult> ExportSingleTableAsync(
        TableInfo table,
        string? outputDir = null,
        CancellationToken cancellationToken = default)
    {
        var result = new TableExportResult
        {
            Table = table
        };

        var outputDirectory = outputDir ?? OutputDirectory;

        try
        {
            // Получаем информацию о колонках
            var columns = await GetTableColumnsAsync(table, cancellationToken);
            if (columns.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "Таблица не найдена или не содержит колонок";
                return result;
            }

            // Формируем имя файла
            var baseFileName = GenerateFileName(table);
            var fileIndex = 0;
            var currentFileSize = 0L;
            var currentFileInserts = 0;
            StringBuilder currentContent = new StringBuilder();
            var outputFiles = new List<string>();

            // Получаем общее количество строк
            result.TotalRows = await GetRowCountAsync(table, cancellationToken);

            // Читаем данные батчами
            var offset = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rows = await GetTableDataBatchAsync(table, columns, offset, BatchSize, cancellationToken);
                if (rows.Count == 0) break;

                foreach (var row in rows)
                {
                    var insertStatement = GenerateInsertStatement(table, columns, row);

                    // Проверяем, нужно ли создать новый файл
                    var estimatedSize = currentFileSize + insertStatement.Length + (AppendGoAfterInsert ? 3 : 1);
                    if (estimatedSize > MaxFileSizeBytes && currentContent.Length > 0)
                    {
                        // Сохраняем текущий файл
                        var filePath = GetNextFilePath(outputDirectory, baseFileName, fileIndex);
                        await SaveContentToFileAsync(filePath, currentContent.ToString());
                        outputFiles.Add(filePath);
                        result.TotalBytes += new FileInfo(filePath).Length;
                        fileIndex++;
                        currentContent.Clear();
                        currentFileSize = 0;
                        currentFileInserts = 0;
                    }

                    currentContent.Append(insertStatement);
                    if (AppendGoAfterInsert)
                    {
                        currentContent.AppendLine("GO");
                    }
                    currentContent.AppendLine();
                    currentFileSize += insertStatement.Length + (AppendGoAfterInsert ? 3 : 1);
                    currentFileInserts++;
                }

                offset += BatchSize;

                // Если получено меньше чем запрашивали - выходим
                if (rows.Count < BatchSize) break;
            }

            // Сохраняем последний файл, если есть данные
            if (currentContent.Length > 0)
            {
                var filePath = GetNextFilePath(outputDirectory, baseFileName, fileIndex);
                await SaveContentToFileAsync(filePath, currentContent.ToString());
                outputFiles.Add(filePath);
                result.TotalBytes += new FileInfo(filePath).Length;
            }

            result.Success = true;
            result.FilesCreated = outputFiles.Count;
            result.OutputFiles = outputFiles;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Экспортирует данные таблиц по именам (например, "dbo.Users", "DBTest.Accounts").
    /// </summary>
    public async Task<DataExportResult> ExportTablesByNameAsync(
        IEnumerable<string> tableNames,
        string? outputDir = null,
        CancellationToken cancellationToken = default)
    {
        var tables = tableNames.Select(ParseTableName).Where(t => t != null).Cast<TableInfo>().ToList();
        return await ExportTablesAsync(tables, outputDir, cancellationToken);
    }

    /// <summary>
    /// Получает список всех пользовательских таблиц в базе данных.
    /// </summary>
    public async Task<List<TableInfo>> GetAllUserTablesAsync(CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT
                SCHEMA_NAME(t.schema_id) AS SchemaName,
                t.name AS TableName
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE t.is_ms_shipped = 0
            ORDER BY s.name, t.name";

        var tables = await _context.Database
            .SqlQueryRaw<TableInfo>(sql)
            .ToListAsync(cancellationToken);

        return tables;
    }

    /// <summary>
    /// Получает информацию о колонках таблицы.
    /// </summary>
    public async Task<List<ColumnInfo>> GetTableColumnsAsync(TableInfo table, CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT
                c.name,
                t.name,
                c.max_length,
                c.precision,
                c.scale,
                CASE WHEN c.is_nullable = 1 THEN 1 ELSE 0 END,
                CASE WHEN c.is_identity = 1 THEN 1 ELSE 0 END,
                CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END
            FROM sys.columns c
            INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
            LEFT JOIN (
                SELECT ic.column_id, ic.object_id
                FROM sys.index_columns ic
                INNER JOIN sys.indexes i ON ic.index_id = i.index_id AND ic.object_id = i.object_id
                WHERE i.is_primary_key = 1
            ) pk ON c.column_id = pk.column_id AND c.object_id = pk.object_id
            WHERE c.object_id = OBJECT_ID(@p0)
            ORDER BY c.column_id";

        var tableFullName = table.FullName;
        var columns = new List<ColumnInfo>();

        var conn = _context.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(cancellationToken);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var param = cmd.CreateParameter();
        param.ParameterName = "@p0";
        param.Value = tableFullName;
        cmd.Parameters.Add(param);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var col = new ColumnInfo
            {
                ColumnName = reader.GetString(0),
                DataType = reader.GetString(1),
                MaxLength = reader.GetInt16(2),
                Precision = reader.GetByte(3),
                Scale = reader.GetByte(4),
                IsNullable = reader.GetInt32(5) == 1,
                IsIdentity = reader.GetInt32(6) == 1,
                IsPrimaryKey = reader.GetInt32(7) == 1
            };
            columns.Add(col);
        }

        // Фильтруем исключённые типы колонок
        columns = columns
            .Where(c => !ExcludedColumnTypes.Any(ex => c.DataType.StartsWith(ex, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return columns;
    }

    /// <summary>
    /// Получает количество строк в таблице.
    /// </summary>
    public async Task<int> GetRowCountAsync(TableInfo table, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT COUNT(*) FROM {table.FullName}";
        var result = await _context.Database
            .SqlQueryRaw<int>(sql)
            .ToListAsync(cancellationToken);

        return result.FirstOrDefault();
    }

    /// <summary>
    /// Получает батч данных из таблицы.
    /// </summary>
    private async Task<List<Dictionary<string, object?>>> GetTableDataBatchAsync(
        TableInfo table,
        List<ColumnInfo> columns,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var result = new List<Dictionary<string, object?>>();

        var columnNames = columns.Select(c => QuoteColumnName(c.ColumnName, table));
        var sql = $"SELECT {string.Join(", ", columnNames)} FROM {table.FullName} ORDER BY (SELECT NULL) OFFSET {offset} ROWS FETCH NEXT {limit} ROWS ONLY";

        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;

        if (command.Connection?.State != System.Data.ConnectionState.Open)
        {
            await command.Connection!.OpenAsync(cancellationToken);
        }

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[columns[i].ColumnName] = value;
            }
            result.Add(row);
        }

        return result;
    }

    /// <summary>
    /// Генерирует INSERT-выражение для строки данных.
    /// </summary>
    public string GenerateInsertStatement(TableInfo table, List<ColumnInfo> columns, Dictionary<string, object?> row)
    {
        var sb = new StringBuilder();
        var columnList = columns.Where(c => !c.IsIdentity).Select(c => QuoteColumnName(c.ColumnName, table));
        var valueList = columns.Where(c => !c.IsIdentity).Select(c => FormatValue(row.GetValueOrDefault(c.ColumnName), c));

        sb.Append($"INSERT INTO {table.FullName} ({string.Join(", ", columnList)}) VALUES ({string.Join(", ", valueList)})");
        return sb.ToString();
    }

    /// <summary>
    /// Форматирует значение для SQL-выражения.
    /// </summary>
    public string FormatValue(object? value, ColumnInfo column)
    {
        if (value == null || value == DBNull.Value)
        {
            return "NULL";
        }

        var dataType = column.DataType.ToLowerInvariant();

        return dataType switch
        {
            "int" or "bigint" or "smallint" or "tinyint" or "bit" or "money" or "smallmoney" or "decimal" or "numeric" or "float" or "real" => FormatNumericValue(value, dataType),
            "uniqueidentifier" => value is Guid g ? $"'{g:G}'" : $"'{value}'",
            "datetime" or "datetime2" or "smalldatetime" or "date" or "time" or "datetimeoffset" => FormatDateTimeValue(value),
            "binary" or "varbinary" or "timestamp" => FormatBinaryValue(value),
            "xml" => $"'{value.ToString()!.Replace("'", "''")}'",
            _ => FormatStringValue(value.ToString()!)
        };
    }

    private static string FormatNumericValue(object value, string dataType)
    {
        if (dataType == "bit")
        {
            // SQL Server может возвращать bit как Int32 (0/1) или как Boolean
            if (value is bool b)
                return b ? "1" : "0";
            if (value is int i)
                return i != 0 ? "1" : "0";
            if (value is long l)
                return l != 0 ? "1" : "0";
        }
        return value.ToString()!;
    }

    private static string FormatDateTimeValue(object value)
    {
        if (value is DateTimeOffset dto)
        {
            return $"'{dto:yyyy-MM-ddTHH:mm:ss.fffzzz}'";
        }
        if (value is DateTime dt)
        {
            return $"'{dt:yyyy-MM-ddTHH:mm:ss.fff}'";
        }
        if (value is TimeSpan ts)
        {
            return $"'{ts}'";
        }
        return $"'{value}'";
    }

    private static string FormatBinaryValue(object value)
    {
        if (value is byte[] bytes)
        {
            return "0x" + BitConverter.ToString(bytes).Replace("-", "");
        }
        // SqlDataReader может возвращать примитивный массив для некоторых типов
        if (value is Array arr && arr.GetType().GetElementType() == typeof(byte))
        {
            var bytes2 = new byte[arr.Length];
            Array.Copy(arr, bytes2, arr.Length);
            return "0x" + BitConverter.ToString(bytes2).Replace("-", "");
        }
        return $"'{value}'";
    }

    private string FormatStringValue(string value)
    {
        // Экранируем одинарные кавычки
        var escaped = value.Replace("'", "''");
        return $"'{escaped}'";
    }

    /// <summary>
    /// Экранирует имя колонки.
    /// </summary>
    private string QuoteColumnName(string columnName, TableInfo table)
    {
        // Проверяем, нужно ли экранирование
        var needsQuotes = columnName.Any(c => !char.IsLetterOrDigit(c) && c != '_') ||
                         columnName.Any(char.IsUpper) || // содержит заглавные буквы
                         columnName.Any(char.IsLower) || // содержит строчные буквы
                         IsSqlReservedWord(columnName);

        return needsQuotes ? $"[{columnName}]" : columnName;
    }

    private static bool IsSqlReservedWord(string word)
    {
        var reservedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "select", "from", "where", "and", "or", "not", "in", "like",
            "order", "by", "group", "having", "join", "inner", "left", "right",
            "insert", "update", "delete", "create", "drop", "alter", "table",
            "index", "view", "procedure", "function", "trigger", "null", "default",
            "primary", "key", "foreign", "references", "constraint", "check"
        };
        return reservedWords.Contains(word);
    }

    /// <summary>
    /// Генерирует базовое имя файла для таблицы.
    /// </summary>
    private string GenerateFileName(TableInfo table)
    {
        var prefix = string.IsNullOrEmpty(FileNamePrefix) ? "" : FileNamePrefix + "_";
        var schemaPart = IncludeSchemaInFileName ? $"{table.SchemaName}." : "";
        // Заменяем недопустимые символы в имени файла
        var tablePart = table.TableName.Replace(":", "_").Replace("/", "_").Replace("\\", "_");
        return $"{prefix}{schemaPart}{tablePart}";
    }

    /// <summary>
    /// Получает путь к файлу с учётом индекса.
    /// </summary>
    private string GetNextFilePath(string directory, string baseName, int index)
    {
        var fileName = index == 0 ? $"{baseName}.sql" : $"{baseName}_{index:D3}.sql";
        var path = Path.Combine(directory, fileName);

        // Если файл уже существует, добавляем суффикс
        if (File.Exists(path))
        {
            var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
            fileName = index == 0 ? $"{baseName}_{uniqueSuffix}.sql" : $"{baseName}_{index:D3}_{uniqueSuffix}.sql";
            path = Path.Combine(directory, fileName);
        }

        return path;
    }

    /// <summary>
    /// Сохраняет содержимое в файл.
    /// </summary>
    private async Task SaveContentToFileAsync(string filePath, string content)
    {
        await File.WriteAllTextAsync(filePath, content, _encoding);
    }

    /// <summary>
    /// Парсит строку с именем таблицы (например, "dbo.Users" или "[DBTest].[Accounts]").
    /// </summary>
    public static TableInfo? ParseTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return null;

        // Убираем квадратные скобки
        var cleaned = tableName.Trim('[', ']');
        var parts = cleaned.Split('.');

        if (parts.Length == 2)
        {
            return new TableInfo
            {
                SchemaName = parts[0].Trim('[', ']'),
                TableName = parts[1].Trim('[', ']')
            };
        }
        else if (parts.Length == 1)
        {
            return new TableInfo
            {
                SchemaName = "dbo",
                TableName = parts[0].Trim('[', ']')
            };
        }

        return null;
    }
}

/// <summary>
/// Информация о колонке таблицы.
/// </summary>
public class ColumnInfo
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public short MaxLength { get; set; }
    public byte Precision { get; set; }
    public byte Scale { get; set; }
    public bool IsNullable { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsPrimaryKey { get; set; }
}
