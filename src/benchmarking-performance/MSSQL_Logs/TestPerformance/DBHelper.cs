using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Perfolizer.Mathematics.Selectors;
using System;
using System.Collections.Generic;
using System.Data;
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
        RabbitMQPost = 3,
        InMemoryTable = 4,      // Лог в Memory-Optimized таблицу (Lock-Free RAM)
        SequentialDiskTable = 5, // Лог на диск с оптимизацией SEQUENTIAL_KEY
        ServiceBroker = 6        // Асинхронное логирование через внутреннюю очередь SQL Server
    }

    // ДОБАВЛЕНО: Реализуем IDisposable для автоматического закрытия SqlConnection
    public class DBHelper : IDisposable
    {
        ServiceCollection services;
        TestDBContext AudiTestDBContext;
        SqlServerType ServerType;
        object LockObjSaveMsgToDataBase = new object();
        DbContextOptionsBuilder<TestDBContext> OptionsBuilder;

        // ДОБАВЛЕНО: Храним постоянное открытое подключение ADO.NET для прямых команд
        public SqlConnection DirectConnection { get; private set; }

        public DBHelper(string strConnection)
        {
            // 1. Инициализируем и открываем физический сокет ADO.NET
            DirectConnection = new SqlConnection(strConnection);
            DirectConnection.Open();

            // 2. Связываем Entity Framework с ЭТИМ ЖЕ открытым соединением (UseSqlServer принимает Connection)
            var optionsBuilder = new DbContextOptionsBuilder<TestDBContext>();
            optionsBuilder.UseSqlServer(DirectConnection);
            OptionsBuilder = optionsBuilder;

            AudiTestDBContext = new TestDBContext(optionsBuilder.Options);
        }

        // ДОБАВЛЕНО: Новый конструктор с двумя параметрами, как вы просили
        public DBHelper(string strConnection, LogType logType) : this(strConnection)
        {
            // Сразу устанавливаем тип логирования при создании объекта
            SetLogType(logType);
        }

        public DBHelper(string server, string databasename, int port = 1433, SqlServerType type = SqlServerType.mssql, string user = "", string pwd = "")
        {
            string connectionString = @$"Server = {server}; Database = {databasename}; User = {user}; Password ={pwd}; MultipleActiveResultSets = true; TrustServerCertificate = true; Encrypt = False";

            DirectConnection = new SqlConnection(connectionString);
            DirectConnection.Open();

            var optionsBuilder = new DbContextOptionsBuilder<TestDBContext>();
            optionsBuilder.UseSqlServer(DirectConnection);
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

        // Старый метод: Медленная вставка через Entity Framework Core
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

            AudiTestDBContext.Database.ExecuteSqlRaw($"EXEC [audit].[sp_LogTextAdd] 'FullAuditEnabled', @KeyField, @KeyValue, @MessageCode, @Message", keyField, keyValue, messageCode, message);
        }

        // ДОБАВЛЕНО: Новый СВЕРХБЫСТРЫЙ метод для отправки НАПРЯМУЮ без фреймворка
        public void AddLogMessageDirect(string? sKeyField = null, string? sKeyValue = null, string? sMessageCode = null, string? sMessage = null)
        {
            // Используем уже открытое свойство DirectConnection без пересоздания сокетов
            using (var command = new SqlCommand("EXEC [audit].[sp_LogTextAdd] 'FullAuditEnabled', @KeyField, @KeyValue, @MessageCode, @Message", DirectConnection))
            {
                command.CommandType = CommandType.Text;

                command.Parameters.Add(new SqlParameter("@KeyField", (object?)sKeyField ?? DBNull.Value));

                var keyValueParam = new SqlParameter("@KeyValue", SqlDbType.BigInt);
                keyValueParam.Value = (sKeyValue == null) ? DBNull.Value : long.Parse(sKeyValue);
                command.Parameters.Add(keyValueParam);

                command.Parameters.Add(new SqlParameter("@MessageCode", (object?)sKeyField ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Message", (object?)sMessage ?? DBNull.Value));

                command.ExecuteNonQuery();
            }
        }

        // ДОБАВЛЕНО: Безопасно закрываем SqlConnection при уничтожении DBHelper
        public void Dispose()
        {
            AudiTestDBContext?.Dispose();
            if (DirectConnection != null)
            {
                if (DirectConnection.State == ConnectionState.Open)
                {
                    DirectConnection.Close();
                }
                DirectConnection.Dispose();
            }
        }
    }
}
