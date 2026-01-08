using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using System;
using TestPerformance;

namespace benchmarkdotnetdemo
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                var config = ManualConfig.CreateEmpty()
                    .AddExporter(HtmlExporter.Default)
                    .AddColumnProvider(DefaultColumnProviders.Instance)
                    .AddLogger(ConsoleLogger.Default);

/*
                var builder = new ConfigurationBuilder()
                                 .SetBasePath(Directory.GetCurrentDirectory())
                                 .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                IConfiguration configuration = builder.Build();

                BenchmarkSettings benchmarkSettings = configuration.GetSection("Settings").Get<BenchmarkSettings>() ?? new BenchmarkSettings();
                config.AddJob(Job.Default.WithIterationCount(benchmarkSettings.Iterations));

                // Example usage
                Console.WriteLine($"Benchmark InputPath: {benchmarkSettings.InputPath}");
                Console.WriteLine($"Benchmark Iterations: {benchmarkSettings.Iterations}");
                Console.WriteLine($"Benchmark Iterations: {benchmarkSettings.ConnectionString}");
*/

#if DEBUG 
                BenchmarkRunner.Run<AuditParserBenchmarks>(new DebugInProcessConfig());
#else
                BenchmarkRunner.Run<AuditParserBenchmarks>(config);
#endif

                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
                Console.ResetColor();
                return 1;
            }
        }
    }
}
