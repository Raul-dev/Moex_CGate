//https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-8.0&tabs=visual-studio
using Microsoft.EntityFrameworkCore;
using MQ.bll.Common;
using MQ.dal.Models;
using MQ.WebService;
using MQ.WebService.Controllers;
using MQ.WebService.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
DataBaseSettings databaseSettings = builder.Configuration.GetRequiredSection(nameof(DataBaseSettings)).Get<DataBaseSettings>() ?? throw new ArgumentNullException();
var connection = databaseSettings.GetConnection(); //builder.Configuration.GetConnectionString("DefaultConnection");

Log.Logger = LogExtensions.ConfigureLoger()
        .CreateBootstrapLogger();

builder.Host.UseLogging();
builder.LogStartUp(connection);

// Add services to the container.

if(databaseSettings.ServerType == "psql")
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
await SingletonProcessingService.Instance.Start(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
