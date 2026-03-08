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
        //private static CancellationTokenSource? cts = null;
        //private static CancellationToken ct;
        CancellationToken _cancellationToken;                         // Local for worker
        CancellationTokenSource cts = new CancellationTokenSource();  // Local for worker
        CancellationToken _cancellationTokenGlobal;                   // Global for application

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
        }
        
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                SessionMode =  "FullMode";
                Log.Information(@$"Started sessionMode {SessionMode}");
                //cts = new CancellationTokenSource();
                //ct = cts.Token;
                //DoWork(_configuration, ct).GetAwaiter().GetResult(); Call from sync method
                _DoWork = DoWork(_configuration, cancellationToken);
                await Task.Delay(1000, cancellationToken);

  
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
            try
            {


                ServiceMsgSettings serviceMsgSettings = _configuration.GetRequiredSection("ServiceWinMsgSettings").Get<ServiceMsgSettings>() ?? throw new ArgumentNullException();
                //#endif
                ThreadManagerAsync tm = new ThreadManagerAsync(serviceMsgSettings, cts);
                RecipientOfTheMessages = tm.GetWorker();
                await tm.MonitorAndRestart();
                var tsk = tm.TaskCompletionSourceWithCancelation(cts.Token);
                tsk.Wait();

            }
            catch (Exception ex)
            {
                // Log or handle the error
                Log.Error($"Configuration error: {ex.Message}");

            }
        }

    }
}