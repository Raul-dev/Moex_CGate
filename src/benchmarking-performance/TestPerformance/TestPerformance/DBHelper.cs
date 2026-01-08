using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Perfolizer.Mathematics.Selectors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TestPerformance
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
/*
            services = new ServiceCollection();

            services.ConfigureDbContext<BlogContext>(options =>
                options.LogTo(Console.WriteLine));

            services.AddDbContext<BlogContext>(options =>
                options.UseInMemoryDatabase("CompositionExample"));

            services.ConfigureDbContext<BlogContext>(options =>
                options.EnableSensitiveDataLogging());
*/
        }
        public DBHelper(string server, string databasename, int port = 1433, SqlServerType type = SqlServerType.mssql, string user = "", string pwd = "")
        {
            var optionsBuilder = new DbContextOptionsBuilder<TestDBContext>();
            optionsBuilder.UseSqlServer(@$"Server = {server}; Database = {databasename}; User = {user}; Password ={pwd}; MultipleActiveResultSets = true; TrustServerCertificate = true; Encrypt = False");
            OptionsBuilder = optionsBuilder;
            AudiTestDBContext = new TestDBContext(optionsBuilder.Options);

        }

        public void SetLogType(LogType logType)
        {
            FormattableString sqlcmd;
          
            sqlcmd = $@"IF NOT EXISTS( SELECT * FROM  [audit].[Setting] WHERE IntValue = {(int)logType} AND ID = 2  ) BEGIN
                            UPDATE [audit].[Setting] SET
                            IntValue = {(int)logType}
                            WHERE ID = 2 
                            EXEC [rmq].[sp_clr_InitialiseRabbitMq]                             
                        END
                        ";
            
            AudiTestDBContext.Database.ExecuteSql(sqlcmd);
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
