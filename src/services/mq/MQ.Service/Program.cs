using MQ.Service;

using Serilog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;



var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();
/*
Log.Logger = new LoggerConfiguration()
    .WriteTo.File(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../logs/sample-service.log")
    )
    .CreateLogger();
*/
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
    })
    // Configure as a Windows Service
    .UseWindowsService(options =>
    {
        options.ServiceName = "My Service";
    })
    .UseSerilog()
    /*
    .UseSerilog((ctx, options) =>
            {
                options.ReadFrom.Configuration(ctx.Configuration);
            })
    */
    .Build();


try
{
    Log.Logger.Information("Rabbit lisen started");
    host.Run();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    return -1;
}
finally
{
    Log.CloseAndFlush();
}