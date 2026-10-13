using System.Net;
using Microsoft.AspNetCore.Http.Features;
using SqlPerf;

var builder = WebApplication.CreateBuilder(args);

// Минимальный оверхед отдачи: отключаем служебные заголовки и буферизацию ответов.
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxConcurrentConnections = 20000;
    options.Listen(IPAddress.Any, builder.Configuration.GetValue("HttpPort", Protocol.HttpPort));
});

builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = Protocol.PayloadByteLength;
    options.BufferBody = false;
});

var app = builder.Build();

byte[] payload = PositionPayloadBuilder.BuildPayload();

app.MapGet(Protocol.HttpPathRoot + "/{id:int}", (int id) =>
{
    // Фиксированный пакет ровно Protocol.PayloadSize байт, без сериализации в момент запроса.
    return Results.Bytes(payload, "application/json");
});

app.MapGet("/health", () => Results.Ok("ok"));

app.Run();
