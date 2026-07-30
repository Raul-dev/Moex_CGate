using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MQ.dal.Models;
using MQ.WebService.Health;

namespace MQ.WebService.Extensions;

public static class HealthCheckExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static IServiceCollection AddMqHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("live", () => HealthCheckResult.Healthy("Application is running."), tags: ["live"])
            .AddDbContextCheck<MetastorageContext>("database", tags: ["ready"])
            .AddCheck<MqProcessingHealthCheck>("mq-processing", tags: ["ready"]);

        return services;
    }

    public static IEndpointRouteBuilder MapMqHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/v1/mq/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
            ResponseWriter = WriteHealthResponse
        });

        endpoints.MapHealthChecks("/v1/mq/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthResponse
        });

        endpoints.MapHealthChecks("/v1/mq/health", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthResponse
        });

        return endpoints;
    }

    private static Task WriteHealthResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    duration = entry.Value.Duration.TotalMilliseconds,
                    exception = entry.Value.Exception?.Message,
                    data = entry.Value.Data.Count > 0 ? entry.Value.Data : null
                })
        };

        context.Response.StatusCode = report.Status == HealthStatus.Unhealthy
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK;

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
