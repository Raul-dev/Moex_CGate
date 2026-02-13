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
                    .AddColumn(StatisticColumn.Min, StatisticColumn.Max)
                    .AddLogger(ConsoleLogger.Default);


                var builder = new ConfigurationBuilder()
                                 //.SetBasePath(Directory.GetCurrentDirectory())
                                 .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                IConfiguration configuration = builder.Build();

                BenchmarkSettings benchmarkSettings = configuration.GetSection("BenchmarkSettings").Get<BenchmarkSettings>() ?? new BenchmarkSettings();
                /*
                 Количество вызовов для результатных измерений:
                    Total Workload Invocations=LaunchCount×IterationCount×InvocationCount

                LaunchCount – Сколько раз запускается сам процесс бенчмарка (разные процессы для устойчивости, изоляции).
                WarmupCount – Сколько раз делается «разогревающая» (warmup) итерация (они не входят в финальный результат).
                IterationCount – Сколько измерительных итераций (workload).
                InvocationCount – Сколько раз вызывается ваш метод за одну итерацию (это то, что важно).
                UnrollFactor – Сколько раз подряд вызывается метод внутри одного внутреннего блока (это оптимизация для минимизации накладных расходов; на общее количество вызовов не влияет — влияет, как считается InvocationCount).
                */
                config.AddJob(Job.Default.WithLaunchCount(1)
                                .WithWarmupCount(2)
                                .WithUnrollFactor(10)
                                .WithIterationCount(benchmarkSettings.IterationCount)
                                .WithInvocationCount(benchmarkSettings.InvocationCount)
                );

                // Example usage
                Console.WriteLine($"Benchmark InputPath: {benchmarkSettings.InputPath}");
                Console.WriteLine($"Benchmark IterationCount: {benchmarkSettings.IterationCount}");
                Console.WriteLine($"Benchmark InvocationCount: {benchmarkSettings.InvocationCount}");


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
