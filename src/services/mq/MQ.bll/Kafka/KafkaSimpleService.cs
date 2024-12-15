using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using MQ.bll.Common;
using Serilog;

namespace MQ.bll.Kafka;

public class KafkaSimpleService : IQueueService
{
    public KafkaSimpleService(BllOption bo, IConfiguration configuration)
    {
        Bo = bo;
        Configuration = configuration;
        var readSettings = configuration.GetRequiredSection(nameof(KafkaSettings)).Get<KafkaSettings>();
        if (readSettings == null) throw new ArgumentException(nameof(KafkaSettings));
        KafkaSettings = readSettings;
    }

    public BllOption Bo { get; }
    public IConfiguration Configuration { get; }
    public KafkaSettings KafkaSettings { get; }

    public async Task GetAllMessages(CancellationTokenSource cts)
    {
        var server = $"{KafkaSettings.Host}:{KafkaSettings.Port}";
        var consConfig = new ConsumerConfig
        {
            BootstrapServers = server, // TODO: make servers a collection and build string here.
            GroupId = KafkaSettings.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(consConfig).Build();

        consumer.Subscribe(KafkaSettings.Topic);

        var token = cts.Token;

        while (!token.IsCancellationRequested)
        {
            var consumeResult = consumer.Consume(cts.Token);
            Log.Information($"message: {consumeResult.Message.Value}");
        }

        consumer.Close();
    }

    public async Task SendAllMessages(CancellationTokenSource cts)
    {
        var server = $"{KafkaSettings.Host}:{KafkaSettings.Port}";
        var config = new ProducerConfig
        {
            BootstrapServers = server,
            AllowAutoCreateTopics = true,
            EnableSslCertificateVerification = false,
        };

        using var producer = new ProducerBuilder<Null, string>(config).Build();

        for (int i = 0; i < 10; i++)
        {
            producer.Produce(KafkaSettings.Topic, new Message<Null, string> { Value = $"log {i}", });
            producer.Flush();
        }


    }
}