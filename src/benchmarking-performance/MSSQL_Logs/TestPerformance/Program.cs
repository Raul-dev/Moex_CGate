using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using BenchmarkDotNet.Characteristics;
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
                PrintBenchmarkSummary(summary, TotalCount);

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
        /// <summary>
        /// Выводит расширенную статистику по итерациям, операциям и базовому Mean (среднему времени)
        /// </summary>
        /// <param name="summary">Результат выполнения BenchmarkRunner.Run</param>
        /// <param name="totalCount">Общее базовое количество записей для расчета (TotalCount)</param>
        public static void PrintBenchmarkSummary(Summary summary, int totalCount)
        {
            // 1. Проверяем, что отчёт содержит данные
            if (summary == null || summary.Reports == null || !summary.Reports.Any())
            {
                Console.WriteLine("Отчет бенчмарка пуст или равен null.");
                return;
            }

            // 2. Сохраняем исходный цвет консоли
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;

            // 3. Вытаскиваем из отчета уникальные комбинации параметров
            var uniqueParams = summary.Reports
                .Where(r => r.BenchmarkCase.Parameters["ThreadCount"] != null)
                .Select(r => new
                {
                    ThreadCount = int.Parse(r.BenchmarkCase.Parameters["ThreadCount"].ToString())
                })
                .Distinct();

            // 4. Выводим расчет для каждой комбинации ThreadCount
            foreach (var param in uniqueParams)
            {
                int targetRecordCount = totalCount / param.ThreadCount;
                int totalInsertsForMean = param.ThreadCount * targetRecordCount;

                // Ищем отчет для БАЗОВОГО бенчмарка (Baseline = true) с текущим ThreadCount
                var baselineReport = summary.Reports
                    .FirstOrDefault(r => r.BenchmarkCase.Descriptor.Baseline &&
                                         int.Parse(r.BenchmarkCase.Parameters["ThreadCount"].ToString()) == param.ThreadCount);

                Console.WriteLine($"\n[Параметры: ThreadCount={param.ThreadCount}]");
                Console.WriteLine($"-> Логика: {totalInsertsForMean} операций = {param.ThreadCount} потоков * {targetRecordCount} записей");

                if (baselineReport != null)
                {
                    var runMode = baselineReport.BenchmarkCase.Job.Run;
                    var resolver = EnvironmentResolver.Instance;

                    // В v0.15.8 значения извлекаются через ResolveValue, если они не заданы явно (Auto-режим)
                    int launchCount = runMode.ResolveValue(RunMode.LaunchCountCharacteristic, resolver);
                    int unrollFactor = runMode.ResolveValue(RunMode.UnrollFactorCharacteristic, resolver);

                    // Считаем фактическое количество итераций прогрева (Warmup) из логов измерений.
                    // В v0.15.8 прогрев — это режим Workload на стадии Warmup.
                    int warmupCount = baselineReport.AllMeasurements
                        .Count(m => m.IterationMode == IterationMode.Workload && m.IterationStage == IterationStage.Warmup) / launchCount;

                    // Считаем фактическое количество замеров (Workload/Actual)
                    int iterationCount = baselineReport.AllMeasurements
                        .Count(m => m.IterationMode == IterationMode.Workload && m.IterationStage == IterationStage.Actual) / launchCount;
                    // Фактическое динамическое значение InvocationCount для одной итерации
                    long invocationCount = runMode.ResolveValue(RunMode.InvocationCountCharacteristic, resolver);

                    // Вывод результатов Mean
                    if (baselineReport.ResultStatistics != null)
                    {
                        // Mean переводим из наносекунд в миллисекунды (ms)
                        double meanMs = baselineReport.ResultStatistics.Mean / 1_000_000.0;

                        // Делим Mean на количество операций в одной логической итерации метода
                        double timePerOpMs = meanMs / totalInsertsForMean;

                        Console.WriteLine($"-> Baseline Mean: {meanMs:F4} ms");
                        Console.WriteLine($"-> Время на 1 операцию (Mean / логические операции): {timePerOpMs:F6} ms");
                    }
                    else
                    {
                        Console.WriteLine("-> Предупреждение: Статистика для базового бенчмарка пуста.");
                    }

                    // Расчет ПОЛНОЙ суммы физических операций на основе динамических данных
                    long opsPerMethodInvoke = totalInsertsForMean;
                    long operationsPerIteration = opsPerMethodInvoke * invocationCount;

                    long totalWarmupOperations = operationsPerIteration * warmupCount * launchCount;
                    long totalMeasuredOperations = operationsPerIteration * iterationCount * launchCount;
                    long absoluteTotalOperations = totalWarmupOperations + totalMeasuredOperations;

                    Console.WriteLine($"-> Параметры из теста: LaunchCount={launchCount}, WarmupCount={warmupCount}, IterationCount={iterationCount}, InvocationCount={invocationCount}, UnrollFactor={unrollFactor}");
                    Console.WriteLine("-> Полная сумма физических операций (с учетом структуры рана):");
                    Console.WriteLine($"   - В одной итерации: {operationsPerIteration} оп. ({opsPerMethodInvoke} * {invocationCount} InvocationCount)");
                    Console.WriteLine($"   - Всего на прогреве (Warmup): {totalWarmupOperations} оп.");
                    Console.WriteLine($"   - Всего на замерах (Iteration): {totalMeasuredOperations} оп.");
                    Console.WriteLine($"   - АБСОЛЮТНЫЙ ИТОГ (Всего запуск): {absoluteTotalOperations} операций выполненных CPU.");
                }
                else
                {
                    Console.WriteLine("-> Предупреждение: Базовый бенчмарк (Baseline) для данного ThreadCount не найден.");
                }
            }

            // 5. Возвращаем цвет обратно
            Console.ForegroundColor = originalColor;
        }
    }
}
