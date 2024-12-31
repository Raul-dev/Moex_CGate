using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver.Core.Servers;
using MQ.bll;
using MQ.bll.Common;
using Serilog;
using System.Xml.Linq;

namespace MQ.WebService
{
    public class SingletonProcessingService
    {
        private int executionCount = 0;

        //public static IConfiguration Config { get; set; }
        Task _DoWork;
        private static CancellationTokenSource cts;
        private static CancellationToken ct;
        ReceiveAllMessages? RecipientOfTheMessages = null;
        string? SessionMode = null;
        private SingletonProcessingService() {
        }

        public async Task Start(IConfiguration configuration, string? sessionMode = null)
        {
            //"BufferOnly"
            //"FullMode"
            SessionMode = sessionMode;
            Log.Information("Started sessionMode {sessionMode}");
            cts = new CancellationTokenSource();
            ct = cts.Token;
            _DoWork = DoWork(configuration, ct);
        }
        public bool GetStatus()
        {
            if (RecipientOfTheMessages == null)
                return false;
            else 
                return true;
        }

        public void Stop()
        {

            if (RecipientOfTheMessages != null)
            {
                cts.Cancel();
                RecipientOfTheMessages.CleanProcess();
                RecipientOfTheMessages = null;
                Log.Information("Listening to the RabbitMQ queue has been stopped.");
            }

               
        }


        private static SingletonProcessingService? _instance;  
        public static SingletonProcessingService Instance { 
            get {
               if (_instance == null) _instance = new SingletonProcessingService();
                return _instance;
            }}

        public async Task DoWork(IConfiguration _configuration, CancellationToken stoppingToken)
        {
            int iPort;
            BllOption bo = new BllOption();
            //await Task.Delay(10000, stoppingToken);
            //options.InitBllOption(bo);
            //bo.
            DataBaseSettings databaseSettings = _configuration.GetRequiredSection(nameof(DataBaseSettings)).Get<DataBaseSettings>() ?? throw new ArgumentNullException();
            bo.RabbitMQServSettings = _configuration.GetRequiredSection(nameof(RabbitMQSettings)).Get<RabbitMQSettings>() ?? throw new ArgumentNullException();
            //bo.KafkaServSettings = _configuration.GetRequiredSection(nameof(KafkaSettings)).Get<KafkaSettings>() ?? throw new Exception("Have not config KafkaSettings");

            bo.ServerName = databaseSettings.ServerName;
            string dbname = $"{databaseSettings.DataBase}";
            bo.DatabaseName = dbname;
            Log.Information($"Database Name {bo.DatabaseName}");

            //bo.Port, 
            if (databaseSettings.ServerType == "psql")
            {
                bo.ServerType = dal.SqlServerType.psql;
                Log.Information($"Database ServerType psql");
            }
            else
            {
                bo.ServerType = dal.SqlServerType.mssql;
                Log.Information($"Database ServerType mssql");
            }
            bo.User = databaseSettings.User;
            bo.Password = databaseSettings.Password;
            int.TryParse( databaseSettings.Port, out iPort);
            bo.Port = iPort;
            
            if (SessionMode != "BufferOnly" && databaseSettings.SessionMode == "FullMode")
                bo.SessionMode = SessionModeEnum.FullMode;
            else if (SessionMode == "BufferOnly" || databaseSettings.SessionMode == "BufferOnly")
                bo.SessionMode = SessionModeEnum.BufferOnly;
            else
                throw new InvalidOperationException();

            bo.IsConfirmMsgAndRemoveFromQueue = true;

            // = new CancellationToken vs CancellationToken
            RecipientOfTheMessages = new ReceiveAllMessages(bo, _configuration, stoppingToken);
            bool isRunning = true;
            while (isRunning)
            {
                executionCount++;
                try
                {
                    await RecipientOfTheMessages.ProcessLauncherAsync();
                    Log.Information(
                        "External Stopped ProcessLauncherAsync. Count: {Count}", executionCount);
                    if (stoppingToken.IsCancellationRequested)
                        isRunning = false;
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                    if(ex.Message == "Exception Rabbit connection: None of the specified endpoints were reachable")
                        await Task.Delay(100000, stoppingToken);
                }
                finally
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            RecipientOfTheMessages.CleanProcess();
            await Task.Delay(1000, stoppingToken);
            RecipientOfTheMessages = null;
        }
    }
}
