using CommandLine;
using MQ.OptionModels;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using RabbitMQ.Client;
using CommandLine.Text;
using NodaTime.Text;
using MQ;
using MQ.bll.RabbitMQ;
using MQ.bll.Common;
using MQ.bll;
using System.Threading;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using MQ.bll.Kafka;
//using BenchmarkDotNet.Running;
// See https://aka.ms/new-console-template for more information

class Program
{

    public static async Task<int> Main(string[] args)
    {

        CancellationTokenSource cts = new CancellationTokenSource();

        System.Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await MainAsync(args, cts.Token); 
        return 0;
    }
    
    
    static async Task MainAsync(string[] args, CancellationToken token)
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
        //var summary = BenchmarkRunner.Run<Program>();
        Log.Logger.Information("MQ console App finished");
    }

    static void HandleCmdError(IEnumerable<Error> errs)
    {
        Console.WriteLine("Simple Usage: MQ.exe GetMsg/SendMsg -s ServerName -d DataBaseName ");
    }

    static async Task<int> SendMsgExecute(SendMsgOptions options, IConfiguration configuration)
    {
        BllOption bo = new BllOption();
        options.InitBllOption(bo);

        IQueueService snd = options.IsKafka ?? false ? new KafkaService(bo, configuration) : new RabbitService(bo, configuration);

        await snd.SendAllMessages();

        return 0;
    }
    static async Task<int> GetMsgExecute(GetMsgOptions options, IConfiguration configuration)
    {
        BllOption bo = new BllOption();
        options.InitBllOption(bo);

        CancellationTokenSource cts = new CancellationTokenSource();
        IQueueService snd = options.IsKafka ?? false ? new KafkaService(bo, configuration) : new RabbitService(bo, configuration);
        await snd.GetAllMessages(cts);

        return 0;
    }

}