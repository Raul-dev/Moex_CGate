using Serilog;
using Serilog.Context;
using Serilog.Core.Enrichers;
using System.Text.RegularExpressions;


namespace MQ.WebService.Extensions
{
    public static class LogExtensions
    {

        public static IHostBuilder UseLogging(this IHostBuilder hostBuilder)
        {
            Serilog.Debugging.SelfLog.Enable(Console.Error);

            hostBuilder.UseSerilog((ctx, options) =>
            {
                options.ReadFrom.Configuration(ctx.Configuration);
            });

            return hostBuilder;
        }

        public static string GetEnvironment()
        {
            // The environment variable is needed for some logging configuration.
            string env = "ASPNETCORE_ENVIRONMENT";
            var environment = Environment.GetEnvironmentVariable(env);
            if (environment == null)
            {
                throw new NullReferenceException($"{env} environment variable is not set.");
            }

            return environment;
        }

        private static LoggerConfiguration ConfigureDefaults(string environment)
        {
            // Use the appsettings.json configuration to override minimum levels and add any additional sinks.
            var config = new ConfigurationBuilder()
                .AddJsonFile($"appsettings.json")
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .Build();

            var cfg = new LoggerConfiguration()
                .ReadFrom.Configuration(config);

            return cfg;
        }
        public static LoggerConfiguration ConfigureLoger()
        {
            return ConfigureDefaults(GetEnvironment());
        }
        public static WebApplicationBuilder LogStartUp(this WebApplicationBuilder builder, string connection = "")
        {
            string env = builder.Environment.EnvironmentName;

            Log.Logger.Debug($"Environment ASPNETCORE_ENVIRONMENT: {env}");

            //if (!builder.Environment.IsProduction())
            //{
            if (string.IsNullOrEmpty(connection))
                connection = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
            if (!string.IsNullOrEmpty(connection) && connection.Contains("Host ="))
            {
                string strPort = "";
                Regex rx = new(@$"Host=([^;]*)");
                Match match = rx.Matches(connection)[0];
                Regex rxDatabase = new(@$"Database=([^;]*)");
                Match matchDatabase = rxDatabase.Matches(connection)[0];
                Regex rxPort = new(@$"Port=([^;]*)");
                Match matchPort = rxPort.Matches(connection)[0];
                if (match.Groups.Count == 0)
                    Log.Logger.Debug("DB Host not found.");
                if (matchDatabase.Groups.Count == 0)
                    Log.Logger.Debug("Database not found.");
                if (matchPort.Groups.Count != 0)
                    strPort = ", " + matchPort.Groups[0].Value;
                if (match.Groups.Count != 0 && matchDatabase.Groups.Count != 0)
                    Log.Logger.Debug("Postgres DB setup: {0}, {1}{2}.", match.Groups[0].Value, matchDatabase.Groups[0].Value, strPort);
            }
            //}

            return builder;
        }

    }
}
