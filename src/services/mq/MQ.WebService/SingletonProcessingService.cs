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
        Task _DoWork;
        private static CancellationTokenSource cts;
        private static CancellationToken ct;
        ReceiveAllMessages? RecipientOfTheMessages = null;
        string? SessionMode ;
        private SingletonProcessingService() {
        }

        public async Task Start(IConfiguration configuration, string? sessionMode = null)
        {
            //"BufferOnly"
            //"FullMode"
            SessionMode = sessionMode?? "FullMode";
            Log.Information(@$"Started sessionMode {SessionMode}");
            cts = new CancellationTokenSource();
            ct = cts.Token;
            _DoWork = DoWork(configuration, ct);
        }
        public bool GetStatus()
        {
            if (RecipientOfTheMessages == null || RecipientOfTheMessages.GetExecutionCount() == 0)
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
                Log.Information("Listening to the MQ queue has been stopped.");
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
            BllOption bo = new()
            {
                DataBaseServSettings = _configuration.GetRequiredSection(nameof(DataBaseSettings)).Get<DataBaseSettings>() ?? throw new ArgumentNullException(),
                RabbitMQServSettings = _configuration.GetRequiredSection(nameof(RabbitMQSettings)).Get<RabbitMQSettings>() ?? throw new ArgumentNullException(),
                KafkaServSettings = _configuration.GetRequiredSection(nameof(KafkaSettings)).Get<KafkaSettings>(),
                IsConfirmMsgAndRemoveFromQueue = true
            };
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
