using CommandLine;
using Microsoft.Extensions.Configuration;
using MQ.bll;
using MQ.bll.Common;
using MQ.OptionModels;
using MQ.bll.Extensions;
using Serilog;
using Serilog.Context;

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
            .Enrich.WithThreadId()
            .Enrich.FromLogContext()
            .Enrich.With(new CustomPropertyEnricher("WorkerLogPrefix", "SYS"))
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

    //Debug docker sql cmd SendMsg -t mssql -s "localhost,1434" -d CGate -u CGateUser -w MyPassword321 -i 10 -a 10000
    //SendMsg -t mssql -s "localhost" -d CGate -u CGateUser -w MyPassword321 -i 10 -a 1000
    static async Task<int> SendMsgExecute(SendMsgOptions options, IConfiguration configuration)
    {
        BllOption bo = new() { DataBaseServSettings = new DataBaseSettings() };
        options.InitBllOption(bo, configuration);

        CancellationTokenSource cts = new CancellationTokenSource();
        MQ.bll.SendAllUnknownMsg snd = new MQ.bll.SendAllUnknownMsg(bo, configuration, cts.Token);
        await snd.ProcessLauncher();
        //var tsk = snd.TaskCompletionSourceWithCancelation(cts.Token);
        //tsk.Wait();
        return 0;
    }
    static async Task<int> GetMsgExecute(GetMsgOptions options, IConfiguration configuration)
    {
            BllOption bo = new() { DataBaseServSettings = new DataBaseSettings() };
        options.InitBllOption(bo, configuration);

        CancellationTokenSource cts = new CancellationTokenSource();
        var snd = new ReceiveAllMessages(bo, cts.Token);
        await snd.ProcessLauncherAsync();
        var tsk = snd.TaskCompletionSourceWithCancelation(cts.Token);
        tsk.Wait();
        return 0;
    }
 
    static async Task<int> ConfigMsgExecute(ConfigMsgOptions options, IConfiguration configuration)
    {
        CancellationTokenSource cts = new CancellationTokenSource();
        /*
        BllOption bo = new BllOption();
        options.InitBllOption(bo, configuration);

        CancellationTokenSource cts = new CancellationTokenSource();
        var snd = new ReceiveAllMessages(bo, configuration, cts.Token);
        await snd.ProcessLauncherConsoleAsync();
        */
        //Servicegetmsgsettings servicegetmsgsettings = configuration.GetRequiredSection(options.ConfigName).Get<Servicegetmsgsettings>() ?? throw new ArgumentNullException();

        Log.Information("test");
      //  LogContext.PushProperty("WorkerLogPrefix", "DSA");
      //  Log.Information("test1");
        
        try
        {
            //var section = configuration.GetRequiredSection("MissingSection");
            //RabbitMQSettings rabbitMQSettings = configuration.GetRequiredSection("RabbitMQSettings").Get<RabbitMQSettings>() ?? throw new ArgumentNullException();
            //DataBaseSettings dataBaseSettings = configuration.GetRequiredSection("DataBaseSettings").Get<DataBaseSettings>() ?? throw new ArgumentNullException();
            ServiceMsgSettings serviceMsgSettings = configuration.GetRequiredSection(options.ConfigName).Get<ServiceMsgSettings>() ?? throw new ArgumentNullException();

            ThreadManagerAsync tm = new ThreadManagerAsync(serviceMsgSettings, cts);
            //await tm.RunAsync("FullRabbit");
            //var tsk = tm.TaskCompletionSourceWithCancelation(cts.Token);
            //tsk.Wait();
            //object value = await tm.MonitorAndRestart().Result();
            await tm.MonitorAndRestart();
            var tsk = tm.TaskCompletionSourceWithCancelation(cts.Token);
            tsk.Wait();
            //var snd = new ReceiveAllMessages(bo, configuration, cts.Token);
            //MQ.bll.SendAllUnknownMsg snd1 = new MQ.bll.SendAllUnknownMsg(bo, configuration, cts.Token);
            //await snd1.ProcessLauncher();
        }
        catch (Exception ex)
        {
            // Log or handle the error
            Log.Error($"Configuration error: {ex.Message}");
            
        }
        return 0;
    }
}