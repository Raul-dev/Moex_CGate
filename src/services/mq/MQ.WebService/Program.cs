//https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-8.0&tabs=visual-studio
using Microsoft.DotNet.Scaffolding.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.Json;
using MQ.bll.Common;
using MQ.dal.Models;
using MQ.WebService;
using MQ.WebService.Controllers;
using MQ.WebService.Extensions;
using MQ.WebService.Interface;
using Serilog;
using Serilog.AspNetCore;
using Serilog.Context;
using System.IO;

var builder = WebApplication.CreateBuilder(args);
DataBaseSettings databaseSettings = builder.Configuration.GetRequiredSection(nameof(DataBaseSettings)).Get<DataBaseSettings>() ?? throw new ArgumentNullException();
var connection = databaseSettings.GetConnection(); //builder.Configuration.GetConnectionString("DefaultConnection");

Log.Logger = LogExtensions.ConfigureLoger()
        .CreateBootstrapLogger();

builder.Host.UseLogging();
builder.LogStartUp(connection);

// Add services to the container.

if(databaseSettings.ServerType == MQ.dal.SqlServerType.psql)
    builder.Services.AddDbContext<MetastorageContext>(options =>
        options.UseNpgsql(connection));
else
    builder.Services.AddDbContext<MetastorageContext>(
        options => options.UseSqlServer(connection));

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//builder.Services.AddHostedService<ConsumeScopedServiceHostedService>();
//builder.Services.AddScoped<IScopedProcessingService, ScopedProcessingService>();


//_ = SingletonProcessingService.Instance.Start(builder.Configuration);
//await SingletonProcessingService.Instance.Start(builder.Configuration);

builder.Services.AddSingleton<IMqService, SingletonProcessingService>();
builder.Services.AddHostedService<MqStartupService>();
var app = builder.Build();


string confVal = app.Configuration["Serilog:WriteTo:1:Args:path"]?.ToString() ?? "";
Log.Debug("File log path: {0}", confVal);
confVal = app.Configuration["Serilog:WriteTo:1:Args:formatter:template"]?.ToString() ?? "";
Log.Debug("File log template: {0}", confVal);

// Регистрируем Middleware
app.UseMiddleware<LogEnrichmentMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Подписка на событие запуска
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStarted.Register(() =>
{
    var myService = app.Services.GetRequiredService<IMqService>();
    
});
app.UseSerilogRequestLogging();

app.Run();
