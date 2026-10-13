using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlPerf;

namespace PositionTcpService;

/// <summary>
/// TCP-сервер протокола 4B-запрос/5KB-ответ: асинхронный пул соединений,
/// отдача заранее собранного фиксированного пакета 5 КБ.
/// Работает как консоль/docker (Linux) и как Windows Service (режим запуска с ключом --winsvc).
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly int _port;
    private readonly byte[] _payload;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _port = configuration.GetValue("TcpPort", Protocol.TcpPort);
        _payload = PositionPayloadBuilder.BuildPayload();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start(backlog: ushort.MaxValue);
        _logger.LogInformation(
            "TCP-сервер слушает *:{Port}, размер ответа: {PayloadSize}Б, NoDelay=true",
            _port,
            Protocol.PayloadByteLength);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "Принимающий цикл завершён (остановка службы)");
                    break;
                }

                _ = HandleConnectionAsync(client, stoppingToken);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken stoppingToken)
    {
        using (client)
        {
            string remote = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            try
            {
                client.NoDelay = true;
                using NetworkStream stream = client.GetStream();
                byte[] request = new byte[Protocol.RequestByteLength];

                while (!stoppingToken.IsCancellationRequested)
                {
                    // Протокол: читаем ровно RequestByteLength байт, затем пишем ровно PayloadByteLength байт.
                    await stream.ReadExactlyAsync(request, stoppingToken).ConfigureAwait(false);
                    await stream.WriteAsync(_payload, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                // Нормальные обрывы: отключение клиента / остановка службы (IO/Socket/Canceled/Disposed).
                _logger.LogDebug(ex, "Соединение закрыто: {RemoteEndPoint}", remote);
            }
        }
    }
}
