using Serilog;
using System.Threading;

namespace MQ.WebService
{
    public class ConsumeScopedServiceHostedService : BackgroundService
    {
        //private readonly ILogger<ConsumeScopedServiceHostedService> _logger;
        //CancellationTokenSource _stoppingCts;
        public ConsumeScopedServiceHostedService(IServiceProvider services)
        {
            Services = services;
            //_logger = logger;
        }

        public IServiceProvider Services { get; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log.Information("Consume Scoped Service Hosted Service running.");

            //_stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            await DoWork(stoppingToken);
        }

        private async Task DoWork(CancellationToken stoppingToken)
        {
            Log.Information(
                "Consume Scoped Service Hosted Service is working.");

            using (var scope = Services.CreateScope())
            {
                var scopedProcessingService =
                    scope.ServiceProvider
                        .GetRequiredService<IScopedProcessingService>();

                await scopedProcessingService.DoWork(stoppingToken);
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            Log.Information(
                "Consume Scoped Service Hosted Service is stopping.");

            await base.StopAsync(stoppingToken);
        }
    }
}
