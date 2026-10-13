using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using SqlPerf;

namespace PerfBenchmark;

/// <summary>
/// Сравнение HTTP GET (Kestrel) и постоянных TCP-сокетов (эмуляция пула SQL CLR):
/// <see cref="Requests"/> параллельных запросов, каждый отдаёт ровно 5 КБ.
/// Перед запуском должны быть доступны оба сервиса (docker compose up -d --build
/// либо локально dotnet run для PositionTcpService и PositionHttpService).
/// </summary>
[MemoryDiagnoser]
public class NetworkProtocolsBenchmark
{
    private const int MaxParallelFactor = 4;

    [Params(1000)]
    public int Requests;

    private HttpClient _httpClient = null!;
    private ConcurrentBag<TcpClient> _tcpPool = null!;

    private static string HttpBase => $"http://{Protocol.DefaultHost}:{Protocol.HttpPort}{Protocol.HttpPathRoot}";

    [GlobalSetup]
    public async Task SetupAsync()
    {
        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        _tcpPool = new ConcurrentBag<TcpClient>();
        // Прогреваем пул постоянных сокетов, как это делает SQL CLR после первых вызовов.
        for (int i = 0; i < 50; i++)
        {
            _tcpPool.Add(await ConnectTcpAsync().ConfigureAwait(false));
        }

        // Проверка доступности обоих сервисов до начала замеров.
        byte[] httpProbe = await _httpClient.GetByteArrayAsync($"{HttpBase}/1").ConfigureAwait(false);
        EnsurePayloadSize(httpProbe.Length, "HTTP");

        NetworkStream probeStream = (await ConnectTcpAsync().ConfigureAwait(false)).GetStream();
        byte[] tcpPayload = await RoundTripAsync(probeStream, 1).ConfigureAwait(false);
        EnsurePayloadSize(tcpPayload.Length, "TCP");
        probeStream.Close();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _httpClient.Dispose();
        while (_tcpPool.TryTake(out TcpClient? client))
        {
            try { client.Dispose(); }
            catch { /* закрытие пула "изо всех сил" */ }
        }
    }

    [Benchmark(Baseline = true, Description = "HTTP GET: Kestrel, параллельные запросы")]
    public void Test_HttpGet_Parallel()
    {
        Parallel.For(0, Requests, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * MaxParallelFactor }, i =>
        {
            byte[] response = _httpClient.GetByteArrayAsync($"{HttpBase}/{i}").GetAwaiter().GetResult();
            EnsurePayloadSize(response.Length, "HTTP");
        });
    }

    [Benchmark(Description = "TCP Persistent Socket: пул, параллельные запросы")]
    public void Test_TcpPersistentSocket_Parallel()
    {
        Parallel.For(0, Requests, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * MaxParallelFactor }, i =>
        {
            bool fromPool = _tcpPool.TryTake(out TcpClient? client);
            if (!fromPool)
            {
                // Пул исчерпан — открываем новое постоянное соединение, как это делает SQL CLR.
                client = new TcpClient { NoDelay = true };
            }

            try
            {
                if (!fromPool)
                {
                    client!.Connect(Protocol.DefaultHost, Protocol.TcpPort);
                }

                NetworkStream stream = client!.GetStream();
                byte[] response = RoundTripAsync(stream, i).GetAwaiter().GetResult();
                EnsurePayloadSize(response.Length, "TCP");

                _tcpPool.Add(client); // возврат соединения в пул; stream не закрываем — это ресурс client
            }
            catch
            {
                client!.Dispose();
            }
        });
    }

    /// <summary>Диагностика: собственный параметр Requests задаёт число параллельных запросов.</summary>
    public override string ToString() => $"Requests={Requests}";

    private static void EnsurePayloadSize(int actual, string channel)
    {
        if (actual != Protocol.PayloadByteLength)
        {
            throw new InvalidOperationException(
                $"{channel}: получено {actual} байт вместо {Protocol.PayloadByteLength}. Запустите сервисы: docker compose up -d --build");
        }
    }

    private static async Task<TcpClient> ConnectTcpAsync()
    {
        var client = new TcpClient { NoDelay = true };
        try
        {
            await client.ConnectAsync(Protocol.DefaultHost, Protocol.TcpPort).ConfigureAwait(false);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        return client;
    }

    private static async Task<byte[]> RoundTripAsync(NetworkStream stream, int id)
    {
        byte[] request = BitConverter.GetBytes(id);
        byte[] buffer = new byte[Protocol.PayloadByteLength];

        await stream.WriteAsync(request, CancellationToken.None).ConfigureAwait(false);

        int totalRead = 0;
        while (totalRead < Protocol.PayloadByteLength)
        {
            int read = await stream.ReadAsync(
                buffer.AsMemory(totalRead, Protocol.PayloadByteLength - totalRead),
                CancellationToken.None).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException($"Соединение разорвано сервером (прочитано {totalRead} из {Protocol.PayloadByteLength} байт).");
            }

            totalRead += read;
        }

        return buffer;
    }
}
