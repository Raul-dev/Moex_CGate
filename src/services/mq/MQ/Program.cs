using CommandLine;
using Microsoft.Extensions.Configuration;
using MQ.bll;
using MQ.bll.Common;
using MQ.OptionModels;
using MQ.Share;
using MQ.Share.Configuration;
using Serilog;

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
            .Enrich.With(new CustomPropertyEnricher("Q","Gen"))
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        var cmdRes = CommandLine.Parser.Default.ParseArguments<SendMsgOptions, GetMsgOptions, ConfigMsgOptions>(args)
                                .WithNotParsed(HandleCmdError);

        var isOk = cmdRes.MapResult(
                                  (SendMsgOptions opts) => SendMsgExecute(opts, configuration).Result,
                                  (GetMsgOptions opts) => GetMsgExecute(opts, configuration).Result,
                                  (ConfigMsgOptions opts) => ConfigMsgExecute(opts, configuration).Result,
                                  errs => 1);
        Log.Logger.Information("MQ console App finished");
    }

    static void HandleCmdError(IEnumerable<Error> errs)
    {
        Console.WriteLine("Simple Usage: MQ.exe GetMsg/SendMsg -s ServerName -d DataBaseName ");
    }
    
    //Debug cmd SendMsg -t mssql -s "localhost,1434" -d CGate -u CGateUser -w MyPassword321 -i 10 -a 10000
    static async Task<int> SendMsgExecute(SendMsgOptions options, IConfiguration configuration)
    {
        BllOption bo = new() { DataBaseServSettings = new DataBaseSettings() };
        options.InitBllOption(bo, configuration);

        CancellationTokenSource cts = new CancellationTokenSource();
        MQ.bll.SendAllUnknownMsg snd = new MQ.bll.SendAllUnknownMsg(bo, configuration, cts.Token);
        await snd.ProcessLauncher();

        return 0;
    }
    static async Task<int> GetMsgExecute(GetMsgOptions options, IConfiguration configuration)
    {
            BllOption bo = new() { DataBaseServSettings = new DataBaseSettings() };
        options.InitBllOption(bo, configuration);

        CancellationTokenSource cts = new CancellationTokenSource();
        var snd = new ReceiveAllMessages(bo, configuration, cts.Token);
        await snd.ProcessLauncherConsoleAsync();
        return 0;
    }
    static async Task<int> ConfigMsgExecute(ConfigMsgOptions options, IConfiguration configuration)
    {
        /*
        BllOption bo = new BllOption();
        options.InitBllOption(bo, configuration);

        CancellationTokenSource cts = new CancellationTokenSource();
        var snd = new ReceiveAllMessages(bo, configuration, cts.Token);
        await snd.ProcessLauncherConsoleAsync();
        */
        //Servicegetmsgsettings servicegetmsgsettings = configuration.GetRequiredSection(options.ConfigName).Get<Servicegetmsgsettings>() ?? throw new ArgumentNullException();
        try
        {
            //var section = configuration.GetRequiredSection("MissingSection");
            Servicegetmsgsettings servicegetmsgsettings = configuration.GetRequiredSection(options.ConfigName).Get<Servicegetmsgsettings>() ?? throw new ArgumentNullException();
        }
        catch (Exception ex)
        {
            // Log or handle the error
            Console.WriteLine($"Configuration error: {ex.Message}");
        }
        return 0;
    }
}