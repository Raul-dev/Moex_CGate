using CommandLine;
using MQ.OptionModels;
using Microsoft.Extensions.Configuration;
using Serilog;
using MQ.bll.Common;
using MQ.bll;
//using BenchmarkDotNet.Running;
// See https://aka.ms/new-console-template for more information

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

        var cmdRes = CommandLine.Parser.Default.ParseArguments<SendMsgOptions, GetMsgOptions>(args)
                                .WithNotParsed(HandleCmdError);

        var isOk = cmdRes.MapResult(
                                  (SendMsgOptions opts) => SendMsgExecute(opts, configuration).Result,
                                  (GetMsgOptions opts) => GetMsgExecute(opts, configuration).Result,
                                  errs => 1);
        Log.Logger.Information("MQ console App finished");
    }

    static void HandleCmdError(IEnumerable<Error> errs)
    {
        Console.WriteLine("Simple Usage: MQ.exe GetMsg/SendMsg -s ServerName -d DataBaseName ");
    }

    static async Task<int> SendMsgExecute(SendMsgOptions options, IConfiguration configuration)
    {
        BllOption bo = new BllOption();
        options.InitBllOption(bo, configuration);

        CancellationTokenSource cts = new CancellationTokenSource();
        MQ.bll.SendAllUnknownMsg snd = new MQ.bll.SendAllUnknownMsg(bo, configuration, cts.Token);
        await snd.ProcessLauncher();

        return 0;
    }
    static async Task<int> GetMsgExecute(GetMsgOptions options, IConfiguration configuration)
    {
        BllOption bo = new BllOption();
        options.InitBllOption(bo, configuration);

        CancellationTokenSource cts = new CancellationTokenSource();
        var snd = new ReceiveAllMessages(bo, configuration, cts.Token);
        await snd.ProcessLauncherConsoleAsync();
        return 0;
    }

}