//using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using Serilog;

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
        string connectionString;

        public DBHelper(string strConnection)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TestDBContext>();
            optionsBuilder.UseSqlServer(strConnection);
            OptionsBuilder = optionsBuilder;
            AudiTestDBContext = new TestDBContext(optionsBuilder.Options);
            AudiTestDBContext.Database.SetCommandTimeout(500);
            connectionString = strConnection;
        }
        public DBHelper(string server, string databasename, int port = 1433, SqlServerType type = SqlServerType.mssql, string user = "", string pwd = "")
        {
            var optionsBuilder = new DbContextOptionsBuilder<TestDBContext>();
            optionsBuilder.UseSqlServer(@$"Server = {server}; Database = {databasename}; User = {user}; Password ={pwd}; MultipleActiveResultSets = true; TrustServerCertificate = true; Encrypt = False");
            OptionsBuilder = optionsBuilder;
            AudiTestDBContext = new TestDBContext(optionsBuilder.Options);
        }

        /// <summary>
        /// Возвращает параметры процедуры по object_id.
        /// SELECT p.*, t.*, IIF(EXC.TypeName IS NULL, 0, 1) AS is_ignore
        /// FROM sys.parameters p
        /// JOIN sys.types t ON p.user_type_id = t.user_type_id
        /// LEFT JOIN [audit].[fn_BuildExceptType]() EXC ON t.[name] = EXC.TypeName
        /// WHERE p.object_id = @objectId
        /// </summary>
        public async Task<List<ProcedureParameter>> GetProcedureParametersAsync(int objectId, CancellationToken cancellationToken = default)
        {
            string sql = @"
SELECT
    p.object_id        AS ObjectId,
    p.name             AS Name,
    p.parameter_id     AS ParameterId,
    p.user_type_id     AS UserTypeId,
    p.system_type_id   AS SystemTypeId,
    p.max_length       AS MaxLength,
    p.precision        AS Precision,
    p.scale            AS Scale,
    p.is_output        AS IsOutput,
    p.is_cursor_ref    AS IsCursorRef,
    p.is_readonly      AS IsReadOnly,
    p.has_default_value AS HasDefaultValue,
    p.default_value    AS DefaultValue,
    t.name             AS TypeName,
    t.max_length       AS TypeMaxLength,
    t.precision        AS TypePrecision,
    t.scale            AS TypeScale,
    t.is_table_type    AS IsTableType,
    t.is_user_defined  AS IsUserDefined,
    t.is_assembly_type AS IsAssemblyType,
    t.is_nullable      AS IsNullable,
    IIF(EXC.TypeName IS NULL, 0, 1) AS IsIgnore
FROM sys.parameters p
JOIN sys.types t ON p.user_type_id = t.user_type_id
LEFT JOIN [audit].[fn_BuildExceptType]() EXC ON t.name = EXC.TypeName
WHERE p.object_id = @objectId
ORDER BY p.parameter_id;";

            return await AudiTestDBContext.Database
                .SqlQueryRaw<ProcedureParameter>(sql, new SqlParameter("@objectId", objectId))
                .ToListAsync(cancellationToken: cancellationToken);
        }

        public async Task<List<StoredProcedureInfo>> GetSqlProcedures(string? searchPattern, CancellationToken cancellationToken, string? exceptSchemaFilter = null)
        {
            searchPattern = string.IsNullOrEmpty(searchPattern) ? "%" : searchPattern;
            exceptSchemaFilter = string.IsNullOrEmpty(exceptSchemaFilter) ? "audit.%" : exceptSchemaFilter;

            string sqlcmd = $@"
SELECT
    p.[object_id] AS ObjectId,
    SCHEMA_NAME(p.schema_id) AS SchemaName,
    IIF(v.number = 1,p.name, p.name + ';' + CAST(v.number AS VARCHAR(10))) AS ProcedureName,
    v.number AS ProcedureNumber,
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
    AND p.name LIKE @filter
    AND SCHEMA_NAME(p.schema_id) NOT LIKE @exceptFilter
ORDER BY SchemaName, ProcedureName, v.number;";
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
        /// Получает процедуры по object_id и опционально по number.
        /// Если number = null, возвращает все версии процедуры.
        /// </summary>
        public async Task<List<StoredProcedureInfo>> GetSqlProceduresByNumber(
            int? objectId,
            CancellationToken cancellationToken,
            System.Int16? number = null)
        {
            string numberFilter = number.HasValue ? "AND c.number = @number\n" : "";

            string sqlcmd = $@"
SELECT
    p.[object_id] AS ObjectId,
    SCHEMA_NAME(p.schema_id) AS SchemaName,
    IIF(v.number = 1, p.name, p.name + ';' + CAST(v.number AS VARCHAR(10))) AS ProcedureName,
    v.number AS ProcedureNumber,
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
    {numberFilter}) AS v
WHERE
    p.is_ms_shipped = 0
    AND p.[object_id] = @objectId
ORDER BY SchemaName, ProcedureName, v.number;";

            try
            {
                var objectIdParam = new SqlParameter("@objectId", objectId ?? (object)DBNull.Value);
                if (number.HasValue)
                {
                    var numberParam = new SqlParameter("@number", number.Value);
                    return await AudiTestDBContext.Database
                        .SqlQueryRaw<StoredProcedureInfo>(sqlcmd, objectIdParam, numberParam)
                        .ToListAsync(cancellationToken: cancellationToken);
                }
                return await AudiTestDBContext.Database
                    .SqlQueryRaw<StoredProcedureInfo>(sqlcmd, objectIdParam)
                    .ToListAsync(cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        /// <summary>
        /// Получает список процедур по конкретным именам из настроек.
        /// Логика:
        /// 1. GetSqlProceduresObjecIdAsync — получить все процедуры (objectId, schema, name)
        /// 2. В этом списке искать по именам из настроек, отсекая номера версий (;N)
        /// 3. Для найденных — GetSqlProceduresByNumber(objectId, number) + ProcedureParamParser
        /// </summary>
        public async Task<List<StoredProcedureInfo>> GetSqlProceduresByNamesAsync(
            IEnumerable<string> procedureNames,
            Dictionary<string, string>? nameToAuditCode,
            CancellationToken cancellationToken)
        {
            var namesList = procedureNames.ToList();
            if (namesList.Count == 0)
                return new List<StoredProcedureInfo>();

            // Разделяем: первая точка — схема, остаток — имя в БД
            var parsed = namesList
                .Select(n => ParseFullNameToSchemaAndDbName(n))
                .ToList();

            var uniqueSchemas = parsed
                .Select(p => p.Schema)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .ToList();

            if (uniqueSchemas.Count == 0)
                return new List<StoredProcedureInfo>();

            var allProcedures = await GetSqlProceduresObjecIdAsync();
            Log.Debug($"Ищем среди всех процедур Count={allProcedures.Count}");

            // Точные ключи: schema.name для фильтрации
            var targetKeys = new HashSet<string>(parsed.Count * 2, StringComparer.OrdinalIgnoreCase);
            foreach (var p in parsed)
            {
                if (string.IsNullOrEmpty(p.Schema) || string.IsNullOrEmpty(p.Name))
                    continue;
                targetKeys.Add($"{p.Schema}.{p.Name}");
            }

            // Выбираем процедуры из allProcedures по схеме и имени из parsed
            var selectedProcedures = allProcedures.Where(proc =>
                targetKeys.Contains($"{proc.SchemaName}.{proc.ProcedureName}"))
                .ToList();

            // Собираем результат: для каждой найденной процедуры получаем полную информацию
            var result = new List<StoredProcedureInfo>();
            Log.Debug($"Найдено процедур {selectedProcedures.Count}");

            foreach (var procInfo in selectedProcedures)
            {
                // Все номера версий для этой процедуры из parsed
                var matched = parsed.Where(p =>
                    p.Schema.Equals(procInfo.SchemaName, StringComparison.OrdinalIgnoreCase) &&
                    p.Name.Equals(procInfo.ProcedureName, StringComparison.OrdinalIgnoreCase));

                foreach (var match in matched)
                {
                    short number = (short)match.Number;

                    // Получаем полную информацию о процедуре (body, params из fn_BuildProcedureParams)
                    var procDetails = await GetSqlProceduresByNumber(procInfo.ObjectId, cancellationToken, number);

                    foreach (var proc in procDetails)
                    {
                        // Переопределяем ProcedureParams через ProcedureParamParser (парсит body)
                        if (!string.IsNullOrEmpty(proc.ProcedureBody))
                        {
                            if (number == 1)
                                proc.ProcedureParams = new ProcedureParamParser(proc.ObjectId, connectionString).GetParametersForAudit();
                            else
                                proc.ProcedureParams = new ProcedureParamParser(proc.ProcedureBody).GetParametersForAudit();
                        }

                        if (nameToAuditCode != null && nameToAuditCode.TryGetValue(match.Original, out var auditCode))
                            proc.AuditEnabledCode = auditCode;

                        result.Add(proc);
                    }
                }
            }
            Log.Debug($"Преобразовано процедур {result.Count}");
            return result;
        }

        private static string EscapeSql(string s) => s.Replace("'", "''");

        private record ParsedProcName(string Original, string Schema, string Name, int Number);

        private static ParsedProcName ParseFullNameToSchemaAndDbName(string fullName)
        {
            // Первая точка отделяет схему от имени процедуры
            int dotIndex = fullName.IndexOf('.');
            string schema = "";
            string nameWithNumber = "";

            if (dotIndex > 0)
            {
                schema = fullName.Substring(0, dotIndex);
                nameWithNumber = fullName.Substring(dotIndex + 1);
            }
            else
            {
                nameWithNumber = fullName;
            }

            // Отсекаем ;N от имени
            int semicolonIndex = nameWithNumber.IndexOf(';');
            string name = semicolonIndex > 0 ? nameWithNumber.Substring(0, semicolonIndex) : nameWithNumber;
            int number = 1;

            if (semicolonIndex > 0 && semicolonIndex < nameWithNumber.Length - 1)
            {
                string numStr = nameWithNumber.Substring(semicolonIndex + 1);
                if (int.TryParse(numStr, out int n))
                    number = n;
            }

            return new ParsedProcName(fullName, schema, name, number);
        }

        /// <summary>
        /// Извлекает параметры процедуры из её тела (для версионных процедур,
        /// где fn_BuildProcedureParams возвращает параметры версии 0).
        /// Параметры — всё между ALTER/CREATE PROCEDURE [Schema].[Name];N и первым неquoted словом AS.
        /// Концом объявления считается: строка "WITH EXECUTE AS <role>" и далее отдельное слово AS.
        /// </summary>
        private static string ExtractParamsFromBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return "''''";

            string clean = RemoveSqlComments(body);

            var match = System.Text.RegularExpressions.Regex.Match(
                clean,
                @"(ALTER|CREATE)\s+PROCEDURE\s+\[[\w:.]+\]\.\[[\w:.]+\](;\d+)?[\s\S]*?(?=\n\s*WITH\s+EXECUTE\s+AS\s+\w+\s*\n\s*AS\b)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                // Fallback: ищем AS как отдельное слово на отдельной строке
                match = System.Text.RegularExpressions.Regex.Match(
                    clean,
                    @"(ALTER|CREATE)\s+PROCEDURE\s+\[[\w:.]+\]\.\[[\w:.]+\](;\d+)?[\s\S]*?(?=\n[ \t]*AS[ \t]*\n)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            if (!match.Success)
                return "''''";

            string rawParams = match.Value;
            rawParams = System.Text.RegularExpressions.Regex.Replace(rawParams, @"[\r\n]+", " ");
            rawParams = rawParams.Trim();

            if (string.IsNullOrEmpty(rawParams))
                return "''''";

            return "'" + rawParams.Replace("'", "''") + "'";
        }

        private static string RemoveSqlComments(string s)
        {
            s = System.Text.RegularExpressions.Regex.Replace(s, @"/\*[\s\S]*?\*/", "");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"--[^\r\n]*", "");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"^---[\s]*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            return s;
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
        /// Получает список всех хранимых процедур с их object_id, схемой и именем.
        /// SELECT o.object_id AS ObjectId, s.name AS SchemaName, o.name AS ProcedureName
        /// FROM sys.objects o
        /// INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
        /// WHERE o.type = N'P'
        /// ORDER BY s.name, o.name
        /// </summary>
        public async Task<List<StoredProcedureObjecId>> GetSqlProceduresObjecIdAsync(
            CancellationToken cancellationToken = default)
        {
            string sql = @"
SELECT
    o.object_id        AS ObjectId,
    s.name             AS SchemaName,
    o.name             AS ProcedureName
FROM sys.objects o
INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE o.type = N'P'
ORDER BY s.name, o.name;";

            return await AudiTestDBContext.Database
                .SqlQueryRaw<StoredProcedureObjecId>(sql)
                .ToListAsync(cancellationToken: cancellationToken);
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

public class StoredProcedureObjecId
{
    public int ObjectId { get; set; }
    public string SchemaName { get; set; } = "";
    public string ProcedureName { get; set; } = "";
}

public class SqlExecutionResult
{
    public int Applied { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public bool HasErrors => Errors > 0;
}

public class ProcedureParameter
{
    public int ObjectId { get; set; }
    public string Name { get; set; } = "";
    public int ParameterId { get; set; }
    public int UserTypeId { get; set; }
    public byte SystemTypeId { get; set; }
    public short MaxLength { get; set; }
    public byte Precision { get; set; }
    public byte Scale { get; set; }
    public bool IsOutput { get; set; }
    public bool IsCursorRef { get; set; }
    public bool IsReadOnly { get; set; }
    public bool HasDefaultValue { get; set; }
    [NotMapped]
    public object? DefaultValue { get; set; }
    public string TypeName { get; set; } = "";
    public short TypeMaxLength { get; set; }
    public byte TypePrecision { get; set; }
    public byte TypeScale { get; set; }
    public bool IsTableType { get; set; }
    public bool IsUserDefined { get; set; }
    public bool IsAssemblyType { get; set; }
    public bool IsNullable { get; set; }
    public int IsIgnore { get; set; }
}
