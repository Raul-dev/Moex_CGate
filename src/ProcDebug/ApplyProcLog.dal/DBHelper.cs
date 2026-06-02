//using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace ApplyProcLog.dal
{
    public enum SqlServerType
    {
        mssql,
        psql,
        osql,
        sqlite,
        clickhouse,
        xdto,
        unknown
    }
    public enum LogType
    {
        LocalTable = 1,
        LinkedServerTable = 2,
        RabbitMQPost = 3
    }
    public class DBHelper
    {
        ServiceCollection services;
        TestDBContext AudiTestDBContext;
        SqlServerType ServerType;
        object LockObjSaveMsgToDataBase = new object();
        DbContextOptionsBuilder<TestDBContext> OptionsBuilder;
        
        public DBHelper(string strConnection)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TestDBContext>();
            optionsBuilder.UseSqlServer(strConnection);
            OptionsBuilder = optionsBuilder;
            AudiTestDBContext = new TestDBContext(optionsBuilder.Options);
        }
        public DBHelper(string server, string databasename, int port = 1433, SqlServerType type = SqlServerType.mssql, string user = "", string pwd = "")
        {
            var optionsBuilder = new DbContextOptionsBuilder<TestDBContext>();
            optionsBuilder.UseSqlServer(@$"Server = {server}; Database = {databasename}; User = {user}; Password ={pwd}; MultipleActiveResultSets = true; TrustServerCertificate = true; Encrypt = False");
            OptionsBuilder = optionsBuilder;
            AudiTestDBContext = new TestDBContext(optionsBuilder.Options);

        }

        public async Task<List<StoredProcedureInfo>> GetSqlProcedures(string? searchPattern, CancellationToken cancellationToken, string? exceptSchemaFilter = null)
        {
            searchPattern = string.IsNullOrEmpty(searchPattern) ? "%" : searchPattern;
            exceptSchemaFilter = string.IsNullOrEmpty(exceptSchemaFilter) ? "audit.%" : exceptSchemaFilter;

            string sqlcmd = $@"
SELECT
    p.[object_id] AS ObjectId,
    SCHEMA_NAME(p.schema_id) AS SchemaName,
    p.name AS ProcedureName,
    m.[definition] AS ProcedureBody,
    ISNULL([audit].fn_BuildProcedureParams(p.[object_id]), '''''') AS ProcedureParams,
    p.[create_date]  AS CreateDate,
    p.[modify_date]  AS ModifyDate
FROM
    sys.procedures AS p
JOIN sys.sql_modules AS m ON p.object_id = m.object_id
JOIN sys.objects AS obj ON m.object_id = obj.object_id
WHERE
    p.is_ms_shipped = 0
    AND obj.name LIKE @filter
    AND SCHEMA_NAME(p.schema_id) NOT LIKE @exceptFilter
ORDER BY SchemaName, ProcedureName;
                        ";
            try
            {
                var filterParam = new SqlParameter("@filter", searchPattern);
                var exceptParam = new SqlParameter("@exceptFilter", exceptSchemaFilter);
                return await AudiTestDBContext.Database
                    .SqlQueryRaw<StoredProcedureInfo>(sqlcmd, filterParam, exceptParam)
                    .ToListAsync(cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        /// <summary>
        /// Получает список процедур по конкретным именам из appsettings.json.
        /// Поддерживает версионные процедуры SQL Server (Numbered Procedures, ;N).
        /// Формат appsettings: Schema.Name::SubName;V
        /// В БД: sys.procedures + sys.numbered_procedures
        /// </summary>
        public async Task<List<StoredProcedureInfo>> GetSqlProceduresByNamesAsync(
            IEnumerable<string> procedureNames, CancellationToken cancellationToken)
        {
            var namesList = procedureNames.ToList();
            if (namesList.Count == 0)
                return new List<StoredProcedureInfo>();

            // Разделяем: первая точка — схема, остаток — имя в БД
            var parsed = namesList
                .Select(n => ParseFullNameToSchemaAndDbName(n))
                .ToList();

            var uniqueSchemas = parsed
                .Select(p => p.schema)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .ToList();

            if (uniqueSchemas.Count == 0)
                return new List<StoredProcedureInfo>();

            // Экранируем ' для SQL
            var safeSchemas = uniqueSchemas.Select(s => s.Replace("'", "''")).ToList();
            string schemaFilter = string.Join(",", safeSchemas.Select(s => $"'{s}'"));

            // 1. Базовая процедура (number = 0) — из sys.sql_modules
            string baseQuery = $@"
SELECT
    p.[object_id] AS ObjectId,
    SCHEMA_NAME(p.schema_id) AS SchemaName,
    p.name AS ProcedureName,
    m.[definition] AS ProcedureBody,
    ISNULL([audit].fn_BuildProcedureParams(p.[object_id], 0), '''''') AS ProcedureParams,
    p.[create_date]  AS CreateDate,
    p.[modify_date]  AS ModifyDate
FROM
    sys.procedures AS p
JOIN sys.sql_modules AS m ON p.object_id = m.object_id
WHERE
    p.is_ms_shipped = 0
    AND SCHEMA_NAME(p.schema_id) IN ({schemaFilter});
";

            // 2. Версионные процедуры (number > 0) — из sys.syscomments
            //CROSS APPLY собирает все строки текста (colid) через FOR XML PATH
            string versionQuery = $@"
SELECT
    p.[object_id] AS ObjectId,
    SCHEMA_NAME(p.schema_id) AS SchemaName,
    p.name + ';' + CAST(v.number AS VARCHAR(10)) AS ProcedureName,
    v.ProcText AS ProcedureBody,
    ISNULL([audit].fn_BuildProcedureParams(p.[object_id], v.number), '''''') AS ProcedureParams,
    p.[create_date]  AS CreateDate,
    p.[modify_date]  AS ModifyDate
FROM
    sys.procedures AS p
CROSS APPLY (
    SELECT DISTINCT c.number,
        (SELECT c2.text FROM sys.syscomments c2
         WHERE c2.id = p.object_id AND c2.number = c.number
         ORDER BY c2.colid
         FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)') AS ProcText
    FROM sys.syscomments c
    WHERE c.id = p.object_id AND c.number > 0
) AS v
WHERE
    p.is_ms_shipped = 0
    AND SCHEMA_NAME(p.schema_id) IN ({schemaFilter});
";

            // Выполняем последовательно (DbContext не поддерживает параллельные операции)
            var baseProcedures = await AudiTestDBContext.Database
                .SqlQueryRaw<StoredProcedureInfo>(baseQuery)
                .ToListAsync(cancellationToken: cancellationToken);

            var versionProcedures = await AudiTestDBContext.Database
                .SqlQueryRaw<StoredProcedureInfo>(versionQuery)
                .ToListAsync(cancellationToken: cancellationToken);

            // Для версионных процедур переопределяем параметры из тела (fn_BuildProcedureParams берёт от версии 0)
            foreach (var proc in versionProcedures)
            {
                proc.ProcedureParams = ExtractParamsFromBody(proc.ProcedureBody);
            }

            var allProcedures = baseProcedures.Concat(versionProcedures).ToList();

            // Точные ключи: schema.name для фильтрации
            var targetKeys = new HashSet<string>(parsed.Count * 2, StringComparer.OrdinalIgnoreCase);
            foreach (var (schema, dbName) in parsed)
            {
                if (string.IsNullOrEmpty(schema) || string.IsNullOrEmpty(dbName))
                    continue;
                targetKeys.Add($"{schema}.{dbName}");
            }

            return allProcedures.Where(proc =>
                targetKeys.Contains($"{proc.SchemaName}.{proc.ProcedureName}"))
                .ToList();
        }

        private static string EscapeSql(string s) => s.Replace("'", "''");

        /// <summary>
        /// Извлекает параметры процедуры из её тела (для версионных процедур, 
        /// где fn_BuildProcedureParams возвращает параметры версии 0).
        /// Ищет: ALTER PROCEDURE [Schema].[Name] (param1, param2, ...)
        /// </summary>
        private static string ExtractParamsFromBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return "''''";

            // Ищем скобки с параметрами после ALTER PROCEDURE [Schema].[Name]
            var match = System.Text.RegularExpressions.Regex.Match(
                body,
                @"ALTER\s+PROCEDURE\s+\[[^\]]+\]\.\[[^\]]+\](;\d+)?\s*(\([^)]*\))",
                System.Text.RegularExpressions.RegexOptions.Singleline
                | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
            {
                string rawParams = match.Groups[2].Value;
                // Экранируем одинарные кавычки для SQL-строки
                return "'" + rawParams.Replace("'", "''") + "'";
            }

            return "''''";
        }

        /// <summary>
        /// Разделяет полное имя из конфига на схему и имя в БД.
        /// Формат конфига: Schema.Name::SubName;V (первая точка — разделитель схемы)
        /// В БД: Schema = Schema, name = Name::SubName;V
        /// </summary>
        private static (string schema, string dbName) ParseFullNameToSchemaAndDbName(string fullName)
        {
            // Первая точка отделяет схему от имени процедуры
            int dotIndex = fullName.IndexOf('.');
            if (dotIndex > 0)
            {
                string schema = fullName.Substring(0, dotIndex);
                string dbName = fullName.Substring(dotIndex + 1);
                return (schema, dbName);
            }

            return (fullName, "");
        }

        /// <summary>
        /// Парсит полное имя процедуры из appsettings.json.
        /// Поддерживает: Schema.Name;V, Schema.Name::SubName;V, Schema.SubName;V
        /// </summary>
        private static (string schema, string name, string? version) ParseProcedureName(string fullName)
        {
            // Отделяем версию ;N
            string nameWithoutVersion = fullName;
            string? version = null;
            int versionIndex = fullName.LastIndexOf(';');
            if (versionIndex > 0)
            {
                string afterSemi = fullName.Substring(versionIndex + 1);
                // После ; должен быть номер версии (цифры)
                if (int.TryParse(afterSemi, out _))
                {
                    nameWithoutVersion = fullName.Substring(0, versionIndex);
                    version = afterSemi;
                }
            }

            // Ищем :: — разделитель схемы и имени в конфиге
            int sepIndex = nameWithoutVersion.IndexOf("::");
            if (sepIndex > 0)
            {
                string schema = nameWithoutVersion.Substring(0, sepIndex);
                string name = nameWithoutVersion.Substring(sepIndex + 2);
                return (schema, name, version);
            }

            // Обычное Schema.Name
            int dotIndex = nameWithoutVersion.IndexOf('.');
            if (dotIndex > 0)
            {
                string schema = nameWithoutVersion.Substring(0, dotIndex);
                string name = nameWithoutVersion.Substring(dotIndex + 1);
                return (schema, name, version);
            }

            return (nameWithoutVersion, "", version);
        }

        public void AddLogMessage(string? sKeyField = null, string? sKeyValue = null, string? sMessageCode = null, string? sMessage = null)
        {
            var keyField = new SqlParameter("@KeyField", sKeyField);
            var keyValue = new SqlParameter("@KeyValue", System.Data.SqlDbType.BigInt);
            var messageCode = new SqlParameter("@MessageCode", sKeyField);
            var message = new SqlParameter("@Message", sMessage);

            if (sKeyField == null)
                keyField.Value = DBNull.Value;

            keyValue.Value = (sKeyValue == null) ? DBNull.Value : long.Parse(sKeyValue);

            if (sKeyField == null)
                messageCode.Value = DBNull.Value;
            if (sMessage == null)
                message.Value = DBNull.Value;

            AudiTestDBContext.Database.ExecuteSqlRaw($"EXEC [audit].[sp_LogText_Add] 'FullAuditEnabled', @KeyField, @KeyValue, @MessageCode, @Message", keyField, keyValue, messageCode, message);

        }

        /// <summary>
        /// Выполняет набор SQL-батчей через EF Core DbContext.
        /// </summary>
        /// <param name="batches">Коллекция SQL-операторов для выполнения</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Кортеж (applied, skipped, errors)</returns>
        public async Task<SqlExecutionResult> ExecuteBatchesAsync(
            IEnumerable<string> batches,
            CancellationToken cancellationToken = default)
        {
            var result = new SqlExecutionResult();
            var batchList = batches.ToList();

            foreach (var batch in batchList)
            {
                var trimmedBatch = batch.Trim();
                if (string.IsNullOrWhiteSpace(trimmedBatch)) continue;

                try
                {
                    await AudiTestDBContext.Database.ExecuteSqlRawAsync(trimmedBatch, cancellationToken);
                    result.Applied++;
                }
                catch (SqlException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
                                             ex.Message.Contains("There is already an object", StringComparison.OrdinalIgnoreCase))
                {
                    result.Skipped++;
                }
                catch (SqlException ex)
                {
                    result.Errors++;
                    result.ErrorMessages.Add($"SQL ERROR: {ex.Message}");
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    result.ErrorMessages.Add($"ERROR: {ex.Message}");
                }
            }

            return result;
        }
    }
}

public class SqlExecutionResult
{
    public int Applied { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public bool HasErrors => Errors > 0;
}
