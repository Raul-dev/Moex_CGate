using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MQ.bll;

namespace MQ.WebService
{
    internal interface IScopedProcessingService
    {
        Task DoWork(CancellationToken stoppingToken);
    }
    internal class ScopedProcessingService : IScopedProcessingService
    {
        private int executionCount = 0;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        public ScopedProcessingService(ILogger<ScopedProcessingService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task DoWork(CancellationToken stoppingToken)
        {
            BllOption bo = new BllOption();
            //options.InitBllOption(bo);
            //bo.
            bo.ServerName = "host.docker.internal";
            bo.DatabaseName = "client3_ods";
            //bo.Port, 
            bo.ServerType = dal.SqlServerType.mssql;
            bo.User = "password";
            bo.Password = "password";
            // = new CancellationToken vs CancellationToken
            ReceiveAllMessages snd = new ReceiveAllMessages(bo, _configuration, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                executionCount++;
                await snd.ProcessLauncherAsync();
                _logger.LogInformation(
                    "Scoped Processing Service is working. Count: {Count}", executionCount);

                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}
