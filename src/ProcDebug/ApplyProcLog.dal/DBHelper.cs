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

        public async Task<List<StoredProcedureInfo>> GetSqlProcedures(string? searchPattern, CancellationToken cancellationToken)
        {
            //FormattableString sqlcmd;
            // Маска фильтрации
            searchPattern =(String.IsNullOrEmpty(searchPattern)) ? "%" : searchPattern;

            string sqlcmd = $@"SELECT 
    p.object_id AS ObjectId,
    SCHEMA_NAME(p.schema_id) AS SchemaName,
    p.name AS ProcedureName,
    m.definition AS ProcedureBody,
    ISNULL(params.ParamString, '') AS ProcedureParams,
    p.create_date  AS CreateDate,
    p.modify_date  AS ModifyDate
FROM 
    sys.procedures AS p
JOIN sys.sql_modules AS m ON p.object_id = m.object_id
JOIN sys.objects AS obj ON m.object_id = obj.object_id
OUTER APPLY (
    SELECT STRING_AGG(
        '''' + p.name + '=''' + '+ ISNULL(LTRIM(CAST(' + p.name + ' AS varchar(' + [audit].fn_GetEstimatedStringLength(p.[user_type_id], p.[max_length], p.[precision] )+ '))),''NULL'')', 
        ' + '', '' + '
    ) WITHIN GROUP (ORDER BY p.parameter_id) AS ParamString
    FROM sys.parameters p
    WHERE p.object_id = obj.object_id
) AS params
WHERE 
    p.is_ms_shipped = 0 -- Исключает системные процедуры
    AND obj.name LIKE @filter -- Добавлен фильтр по маске"";
ORDER BY 
    SchemaName, 
    ProcedureName;
                        ";
            try
            {
                // Создаем параметр для SQL-запроса
                var filterParam = new SqlParameter("@filter", searchPattern);

                // Используем SqlQueryRaw для маппинга в POCO класс
                return await AudiTestDBContext.Database.SqlQueryRaw<StoredProcedureInfo>(sqlcmd, filterParam).ToListAsync(cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Обработка отмены операции, если это необходимо
                throw;
            }

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
        

    }
}
