using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using ImportFilePerformance.Benchmarks;
using ImportFilePerformance.Models;
using ImportFilePerformance.Runner;
using Microsoft.Extensions.Configuration;

namespace ImportFilePerformance;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var config = AppSettingsLoader.Load(args);

            var settings = config.GetSection("BenchmarkSettings").Get<BenchmarkSettings>()
                ?? new BenchmarkSettings();

            // CLI overrides: --mode=e2e|bench --db=mssql|postgres|both --file=CsvTradeResult|path
            //                --strategy=StreamingBulk,ParseOnly,MaterializeThenBulk
            //                --settings=local  (or DOTNET_ENVIRONMENT=Local)
            var mode = AppSettingsLoader.GetArg(args, "mode") ?? settings.DefaultMode;
            var dbArg = AppSettingsLoader.GetArg(args, "db") ?? settings.DefaultDatabase;
            var fileArg = AppSettingsLoader.GetArg(args, "file") ?? settings.DefaultFile;
            var strategyArg = AppSettingsLoader.GetArg(args, "strategy") ?? "ParseOnly,StreamingBulk";
            var settingsLabel = AppSettingsLoader.UseLocalSettings(args)
                ? AppSettingsLoader.LocalFileName
                : AppSettingsLoader.DefaultFileName;

            Console.WriteLine("ImportFilePerformance (.NET 9)");
            Console.WriteLine($"  mode={mode} db={dbArg} file={fileArg}");
            Console.WriteLine($"  settings={settingsLabel}");
            Console.WriteLine($"  files root: {settings.TestFilesRoot}");
            Console.WriteLine();

            if (string.Equals(mode, "bench", StringComparison.OrdinalIgnoreCase))
                return RunBenchmarkDotNet(settings);

            var databases = ParseDatabases(dbArg);
            var strategies = ParseStrategies(strategyArg);
            var runner = new EndToEndRunner(settings);
            await runner.RunAsync(fileArg, databases, strategies);
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ex.ToString());
            Console.ResetColor();
            return 1;
        }
    }

    private static int RunBenchmarkDotNet(BenchmarkSettings settings)
    {
        var categories = new List<string> { "csv" };
        if (settings.TryResolveFile("XmlMedium", out _))
            categories.Add("xml");

        var filter = new AnyCategoriesFilter([.. categories]);

        var config = ManualConfig.CreateEmpty()
            .AddExporter(HtmlExporter.Default)
            .AddColumnProvider(DefaultColumnProviders.Instance)
            .AddColumn(StatisticColumn.Min, StatisticColumn.Max)
            .AddLogger(ConsoleLogger.Default)
            .AddFilter(filter)
            .AddJob(Job.Default
                .WithLaunchCount(1)
                .WithWarmupCount(Math.Max(1, settings.WarmupCount))
                .WithIterationCount(Math.Max(1, settings.IterationCount))
                .WithInvocationCount(Math.Max(1, settings.InvocationCount))
                .WithUnrollFactor(1));

        Console.WriteLine($"BDN IterationCount={settings.IterationCount} InvocationCount={settings.InvocationCount}");
        Console.WriteLine($"BDN categories: {string.Join(", ", categories)}");

#if DEBUG
        BenchmarkRunner.Run<ImportBenchmarks>(new DebugInProcessConfig().AddFilter(filter));
#else
        BenchmarkRunner.Run<ImportBenchmarks>(config);
#endif
        return 0;
    }

    private static DatabaseKind[] ParseDatabases(string arg) => arg.ToLowerInvariant() switch
    {
        "mssql" or "sql" or "sqlserver" => [DatabaseKind.MsSql],
        "postgres" or "psql" or "pg" => [DatabaseKind.Postgres],
        "both" or "all" => [DatabaseKind.MsSql, DatabaseKind.Postgres],
        _ => throw new ArgumentException($"Unknown --db value: {arg}")
    };

    private static LoadStrategy[] ParseStrategies(string arg)
    {
        return arg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.Parse<LoadStrategy>(s, ignoreCase: true))
            .ToArray();
    }

}
