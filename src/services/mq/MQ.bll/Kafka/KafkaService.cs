using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualBasic;
using MQ.bll.Common;
using MQ.dal;
using MQ.dal.Models;
using RabbitMQ.Client;
using Serilog;
using System.Text;
using static Confluent.Kafka.ConfigPropertyNames;
using static MQ.dal.DBHelper;

namespace MQ.bll.Kafka;

public class KafkaService : IQueueService
{
    public KafkaService(BllOption bo, IConfiguration configuration)
    {
        Bo = bo;
        Configuration = configuration;
        var readSettings = configuration.GetRequiredSection(nameof(KafkaSettings)).Get<KafkaSettings>();
        if (readSettings == null) throw new ArgumentException(nameof(KafkaSettings));
        KafkaSettings = readSettings;
        DbHelper = new DBHelper(bo.ServerName, bo.DatabaseName, bo.Port, bo.ServerType, bo.User, bo.Password);
    }

    public BllOption Bo { get; }
    public DBHelper DbHelper { get; }
    public IConfiguration Configuration { get; }
    public KafkaSettings KafkaSettings { get; }

    public async Task GetAllMessages(CancellationTokenSource cts)
    {
        var server = $"{KafkaSettings.Host}:{KafkaSettings.Port}";
        
        var consConfig = new ConsumerConfig
        {
            BootstrapServers = server, // TODO: make servers a collection and build string here.
            ClientId = 1 + "_consumer",
            GroupId = KafkaSettings.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            //
            //EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<long, MsgQueueItem>(consConfig)
            .SetKeyDeserializer(Deserializers.Int64)
            .SetValueDeserializer(new MsgQueueSerializer())
            .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
            .Build();

        //using var consumer = new ConsumerBuilder<Ignore, string>(consConfig)
        //    .Build();

        consumer.Subscribe(KafkaSettings.Topic);
        var p = consumer.Position;
        var token = cts.Token;
        int iCount = 0 ;

        // как я понял это позиционирование на начало
        //consumer.Assign(partitions.Select(p => new TopicPartitionOffset(Topic_Counts, p, Offset.Beginning)));

        Log.Information(@$"We are starting to insert {consumer.Position} messages to the Database from Kafka.");
        while (!token.IsCancellationRequested)
        {
            try
            {
                token.ThrowIfCancellationRequested();

                var consumeResult = consumer.Consume(cts.Token); //TimeSpan.FromSeconds(1),
                var sessionId = consumeResult.Message.Key;
                var msg = consumeResult.Message.Value;

                // int messageTypeId = mqmsg.BasicProperties.ContentType == "xmlfile" ? 2 : 1;
                int msgTypeId = 1;
                DbHelper.SaveMsgToDataBase(sessionId, "msgqueue", msg.MsgId.ToString()!, msg.Msg!, msg.MsgKey!, msgTypeId);
                iCount++;
                if (iCount % 1000 == 0)
                {
                    Log.Information(@$"Recive {iCount} messages.");
                }
            }
            catch (Exception e)
            {
                Log.Error("Kafka Consumer Exception: " + e);
                // Note: transactions may be committed / aborted in the partitions
                // revoked / lost handler as a side effect of the call to close.
                break;
            }
        }
        consumer.Close();
        Log.Information(@$"Recive {iCount} messages.");
    }
    public async Task SendAllMessages()
    {
        var server = $"{KafkaSettings.Host}:{KafkaSettings.Port}";
        var config = new ProducerConfig
        {
            BootstrapServers = server, //"localhost:29092", //
            AllowAutoCreateTopics = true,
            EnableSslCertificateVerification = false,
        };
        using var producer = new ProducerBuilder<long, MsgQueueItem>(config)
        .SetKeySerializer(Serializers.Int64)
        .SetValueSerializer(new MsgQueueSerializer())
            .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
            .Build();

        List<MsgQueueItem> mq = DbHelper.GetMsgqueueItems();
        int iCount = 0;
        Random rnd = new Random();
        Log.Information(@$"We are starting to add {mq.Count} messages to the Kafka.");
        foreach (var item in mq)
        {
            producer.Produce(KafkaSettings.Topic, new Message<long, MsgQueueItem> { Key = item.SessionId, Value = item, });
            iCount++;
            if (iCount % 1000 == 0)
            {
                Log.Information(@$"Send {iCount} messages.");
                producer.Flush();
            }
            if (Bo.PauseMs != 0)
            {
                Thread.SpinWait(rnd.Next(Bo.PauseMs / 2, Bo.PauseMs));
            }
            
        }
        Log.Information(@$"Send {iCount} messages.");
        producer.Flush();


    }
}

