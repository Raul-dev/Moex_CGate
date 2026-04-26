using ApplyProcLog;
using CommandLine;
using Microsoft.Extensions.Configuration;
using ApplyProcLog.dal;
using Serilog;
using Serilog.Context;
using static System.Runtime.InteropServices.JavaScript.JSType;

class Program
{

    static async Task Main(string[] args)
    {
        // Build a config object, using env vars and JSON providers.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();
        
        //BenchmarkSettings benchmarkSettings = configuration.GetSection("BenchmarkSettings").Get<BenchmarkSettings>() ?? new BenchmarkSettings();

        var cmdRes = CommandLine.Parser.Default.ParseArguments<CmdOption>(args)
                                .WithNotParsed(HandleCmdError);

        var isOk = cmdRes.MapResult(
                                  (CmdOption opts) => LaunchApplyProcLog(opts, configuration).Result,
                                  errs => 1);
        Log.Logger.Information("SP console App finished");
    }

    static void HandleCmdError(IEnumerable<CommandLine.Error> errs)
    {
        Console.WriteLine("Simple Usage: SP.exe -s ServerName -d DataBaseName ");
    }

    static async Task<int> LaunchApplyProcLog(CmdOption options, IConfiguration configuration)
    {
        //await snd.ProcessLauncher();
        string connection = @$"Server = localhost; Database = CGate; User = CGateUser; Password = MyPassword321; MultipleActiveResultSets = true; TrustServerCertificate = true; Encrypt = False";
        List<StoredProcedureInfo> storedProcedures = new List<StoredProcedureInfo>();
        CancellationToken ct = new CancellationToken();
        DBHelper dbset = new DBHelper(connection);
        storedProcedures = await dbset.GetSqlProcedures("sp_Gener%", ct).ConfigureAwait(false);
        StoredProcedureGenerator spg = new StoredProcedureGenerator();
        spg.CreateProcedureFilesAsync(storedProcedures).Wait();
        return 0;
    }
}