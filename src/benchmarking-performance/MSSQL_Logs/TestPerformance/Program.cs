using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Configuration;
using BenchmarkDotNet.Reports;
using Microsoft.Extensions.FileProviders;
using System;
using TestPerformance;

namespace TestPerformance
{
    public class Program
    {
        // 1. Создаем публичное статическое поле для общего доступа, это количество инсертов за тест, среднее считается за TotalCount инсертов
        public static int TotalCount = 100; 
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
                Summary summary =  BenchmarkRunner.Run<AuditParserBenchmarks>(config);

                // 2. Сохраняем исходный цвет консоли
                ConsoleColor originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Green;

                Console.WriteLine("\n==================================================================");
                Console.WriteLine("📊 РАСЧЕТ ИНСЕРТОВ ДЛЯ ВЫВЕДЕННЫХ В ТАБЛИЦЕ ЗНАЧЕНИЙ MEAN:");
                Console.WriteLine("==================================================================");

                // 3. Вытаскиваем из отчета все уникальные комбинации параметров, которые запускались
                // BenchmarkDotNet хранит параметры в виде строк, поэтому приводим их к int
                var uniqueParams = summary.Reports
                    .Select(r => new
                    {
                        ThreadCount = int.Parse(r.BenchmarkCase.Parameters["ThreadCount"].ToString()),
                        IsADO = int.Parse(r.BenchmarkCase.Parameters["IsADO"].ToString())
                    })
                    .Distinct();

                // 4. Выводим расчет для каждой комбинации
                int totalCount = 100;
                foreach (var param in uniqueParams)
                {
                    int targetRecordCount = totalCount / param.ThreadCount;
                    int totalInsertsForMean = param.ThreadCount * targetRecordCount;

                    Console.WriteLine($"[Параметры: ThreadCount={param.ThreadCount}, IsADO={param.IsADO}] " +
                                    $"Итого для Mean: {totalInsertsForMean} инсертов = {param.ThreadCount} потоков * {targetRecordCount} записей");
                }

                Console.WriteLine("==================================================================\n");

                // 5. Возвращаем цвет обратно
                Console.ForegroundColor = originalColor;
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
