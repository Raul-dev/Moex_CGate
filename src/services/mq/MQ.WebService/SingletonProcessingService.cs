using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver.Core.Servers;
using MQ.bll;
using MQ.bll.Common;
using Serilog;
using System.ComponentModel;
using System.Configuration;
using System.Xml.Linq;

namespace MQ.WebService
{
    public class SingletonProcessingService
    {
        private int executionCount = 0;
        Task? _DoWork;
        private static CancellationTokenSource cts = new CancellationTokenSource();
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
                //cts.Cancel();
                RecipientOfTheMessages.CancelAll();
                //RecipientOfTheMessages = null;
                Log.Information("Listening to the MQ queue has been stopped.");
            }
        }

        private static SingletonProcessingService? _instance;  
        public static SingletonProcessingService Instance { 
            get {
               if (_instance == null) _instance = new SingletonProcessingService();
                return _instance;
            }}

        public async Task DoWork(IConfiguration _configuration, CancellationToken cancellationToken)
        {
//#if (DEBUG)
            ServiceMsgSettings serviceMsgSettings = new()
            {

                ServiceName = "test",
                ServiceDescription = "test",
                ServiceDisplayName = "test",
                Workers = new BllOption[1]
                {
                    new BllOption()
                    {
                        DataBaseServSettings = _configuration.GetRequiredSection(nameof(DataBaseSettings)).Get<DataBaseSettings>() ?? throw new ArgumentNullException(),
                        RabbitMQServSettings = _configuration.GetRequiredSection(nameof(RabbitMQSettings)).Get<RabbitMQSettings>() ?? throw new ArgumentNullException(),
                        KafkaServSettings = _configuration.GetRequiredSection(nameof(KafkaSettings)).Get<KafkaSettings>(),
                        IsConfirmMsgAndRemoveFromQueue = true,
                        IsKafka = false,
                        Name = "FullRabbit",
                        IsEnabled = true,
                        LogPrefix = "FL",
                        Iteration = 100,
                        PauseMs = 1000,
                    }
                }
            };

//#else
//            ServiceMsgSettings serviceMsgSettings = _configuration.GetRequiredSection("ServiceRabbitMsgSettings").Get<ServiceMsgSettings>() ?? throw new ArgumentNullException();
//#endif
            ThreadManagerAsync tm = new ThreadManagerAsync(serviceMsgSettings, cts);
            RecipientOfTheMessages = tm.GetWorker();
            await tm.MonitorAndRestart();
            var tsk = tm.TaskCompletionSourceWithCancelation(cts.Token);
            tsk.Wait();
        }

    }
}
