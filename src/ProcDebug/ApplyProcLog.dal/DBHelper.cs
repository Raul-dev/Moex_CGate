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
        /// </summary>
        public async Task<List<StoredProcedureInfo>> GetSqlProceduresByNamesAsync(
            IEnumerable<string> procedureNames, CancellationToken cancellationToken)
        {
            var namesList = procedureNames.ToList();
            if (namesList.Count == 0)
                return new List<StoredProcedureInfo>();

            string namePlaceholders = string.Join(",", namesList.Select((_, i) => $"@p{i}"));
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
WHERE
    p.is_ms_shipped = 0
    AND SCHEMA_NAME(p.schema_id) + '.' + p.name IN ({namePlaceholders})
ORDER BY SchemaName, ProcedureName;
                        ";

            var parameters = namesList
                .Select((name, i) => new SqlParameter($"@p{i}", name))
                .ToArray();

            return await AudiTestDBContext.Database
                .SqlQueryRaw<StoredProcedureInfo>(sqlcmd, parameters)
                .ToListAsync(cancellationToken: cancellationToken);
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
