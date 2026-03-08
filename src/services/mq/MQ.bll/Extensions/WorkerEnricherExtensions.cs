using Serilog.Configuration;

namespace Serilog // Рекомендуется использовать этот namespace
{
    public static class WorkerEnricherExtensions
    {
        public static LoggerConfiguration WithWorkerLogPrefix(this LoggerEnrichmentConfiguration enrichmentConfiguration,
            string prefix = "DEFAULT")
        {
            if (enrichmentConfiguration == null) throw new ArgumentNullException(nameof(enrichmentConfiguration));
            //return enrichmentConfiguration.With<WorkerLogPrefixEnricher>();
            return enrichmentConfiguration.With(new WorkerLogPrefixEnricher(prefix));
        }
    }
}