using Microsoft.Extensions.Configuration;
using Serilog;
using MQ.bll;
using MQ.bll.Common;
using Serilog;
namespace MQ.Service
{
    public class Worker : BackgroundService
    {
        
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private ReceiveAllMessages? RecipientOfTheMessages = null;
        private string? SessionMode;
        private int executionCount = 0;
        private Task? _DoWork = null;
        private static CancellationTokenSource? cts = null;
        private static CancellationToken ct;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                SessionMode =  "FullMode";
                Log.Information(@$"Started sessionMode {SessionMode}");
                //cts = new CancellationTokenSource();
                //ct = cts.Token;
                //DoWork(_configuration, ct).GetAwaiter().GetResult(); Call from sync method
                _DoWork = DoWork(_configuration, stoppingToken);
                await Task.Delay(1000, stoppingToken);
            }
        }
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Service starting...");
            await base.StartAsync(cancellationToken);
        }
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Service stopping...");
            await base.StopAsync(cancellationToken);
        }
        public async Task DoWork(IConfiguration _configuration, CancellationToken stoppingToken)
        {
            int iPort;
            BllOption bo = new BllOption();
            //await Task.Delay(10000, stoppingToken);
            //options.InitBllOption(bo);
            //bo.
            DataBaseSettings databaseSettings = _configuration.GetRequiredSection(nameof(DataBaseSettings)).Get<DataBaseSettings>() ?? throw new ArgumentNullException();
            bo.RabbitMQServSettings = _configuration.GetRequiredSection(nameof(RabbitMQSettings)).Get<RabbitMQSettings>() ?? throw new ArgumentNullException();
            bo.KafkaServSettings = _configuration.GetRequiredSection(nameof(KafkaSettings)).Get<KafkaSettings>() ?? throw new Exception("Have not config KafkaSettings");

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
            int.TryParse(databaseSettings.Port, out iPort);
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
                    if (ex.Message == "Exception Rabbit connection: None of the specified endpoints were reachable")
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