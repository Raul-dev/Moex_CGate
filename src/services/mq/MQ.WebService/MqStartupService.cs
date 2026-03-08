using MQ.WebService.Interface;

namespace MQ.WebService
{
    public class MqStartupService : IHostedService
    {
        private readonly IMqService _singleton;
        private readonly IConfiguration _configuration;
        public MqStartupService(IConfiguration configuration, IMqService singleton)
        {
            _singleton = singleton;
            _configuration = configuration;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Этот код выполнится ПОСЛЕ того, как Host будет запущен
            _singleton.Start(_configuration);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

}
