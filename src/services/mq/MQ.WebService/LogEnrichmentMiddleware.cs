namespace MQ.WebService
{
    public class LogEnrichmentMiddleware
    {
        private readonly RequestDelegate _next;

        public LogEnrichmentMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        //настраивается LogContext
        public async Task InvokeAsync(HttpContext context)
        {
            using (Serilog.Context.LogContext.PushProperty("WorkerLogPrefix", "SYS3"))
            {
                await _next(context); // Передаем управление дальше по конвейеру
            }
            //context.Connection.RemoteIpAddress
        }
    }
}
