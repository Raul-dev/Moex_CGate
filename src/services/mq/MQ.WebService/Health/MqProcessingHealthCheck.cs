using Microsoft.Extensions.Diagnostics.HealthChecks;
using MQ.WebService.Interface;

namespace MQ.WebService.Health;

public sealed class MqProcessingHealthCheck : IHealthCheck
{
    private readonly IMqService _mqService;

    public MqProcessingHealthCheck(IMqService mqService)
    {
        _mqService = mqService;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            _mqService.GetStatus()
                ? HealthCheckResult.Healthy("MQ processing is running.")
                : HealthCheckResult.Unhealthy("MQ processing is not running."));
    }
}
