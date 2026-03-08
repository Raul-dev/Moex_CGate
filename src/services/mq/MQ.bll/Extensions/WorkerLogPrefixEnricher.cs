using Serilog.Core;
using Serilog.Events;

namespace Serilog
{

    public class WorkerLogPrefixEnricher : ILogEventEnricher
    {
    //    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    //    {
    //        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("WorkerLogPrefix", "INIT"));
    //    }
        private readonly string _prefix;
        public WorkerLogPrefixEnricher(string prefix) => _prefix = prefix;

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("WorkerLogPrefix", _prefix));
        }
}
}
