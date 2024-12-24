using MQ.bll.Common;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Confluent.Kafka;
using static MQ.dal.DBHelper;
using NetTopologySuite.Index.HPRtree;
using static Confluent.Kafka.ConfigPropertyNames;
using static MongoDB.Driver.WriteConcern;
using BenchmarkDotNet.Engines;
using Microsoft.Identity.Client;
using MQ.dal;
using RTools_NTS.Util;

namespace MQ.bll.Kafka
{
    public class KafkaChannel : IQueueChannel
    {
        BllOption option;
        IProducer<long, MsgQueueItem> _producer;
        IConsumer<long, MsgQueueItem> _consumer;
        int _iCount = 0;

        IChannel _channel;
        string _queueName;
        bool _disposed;
        string _exchange;
        CancellationToken _cancellationToken;
        MQSession _MQSession;
        AsyncEventingBasicConsumer mqConsumer;
        string? ConsumeTag;
        
        public KafkaChannel(BllOption option)
        {
            this.option = option;
        }
        public KafkaChannel(IChannel channel)
        {
            this._channel = channel;
            _disposed = false;
        }
        public void Dispose()
        {
            if (_disposed) return;
            
            _channel.Dispose();
            _disposed = false;
        }
        public async Task InitSetup(CancellationToken cancellationToken, MQSession? mqSession = null, bool isSend = true, bool isSubscription = false)
        {
            var server = $"{option.KafkaServSettings.Host}:{option.KafkaServSettings.Port}";

            
            if (isSend)
            {
                var config = new ProducerConfig
                {
                    BootstrapServers = server, //"localhost:29092", //
                    AllowAutoCreateTopics = true,
                    EnableSslCertificateVerification = false,
                };
                _producer = new ProducerBuilder<long, MsgQueueItem>(config)
                    .SetKeySerializer(Serializers.Int64)
                    .SetValueSerializer(new MsgQueueSerializer())
                    .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
                    .Build();
                
            }
            else
            {
                var consConfig = new ConsumerConfig
                {
                    BootstrapServers = server, // TODO: make servers a collection and build string here.
                    ClientId = 1 + "_consumer",
                    GroupId = option.KafkaServSettings.GroupId,
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    //
                    //EnableAutoCommit = false,
                };
                _consumer = new ConsumerBuilder<long, MsgQueueItem>(consConfig)
                    .SetKeyDeserializer(Deserializers.Int64)
                    .SetValueDeserializer(new MsgQueueSerializer())
                    .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
                    .Build();

                _consumer.Subscribe(option.KafkaServSettings.Topic);
                //Task.Run(ReceivedMessageAsync); // ReceivedMessageAsync();
                var tsk = ReceivedMessageAsync();
                //tsk.Start();
            }

        }
        async Task ReceivedMessageAsync()
        {

            //await Task.Delay(3000);
            while (!_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _cancellationToken.ThrowIfCancellationRequested();

                    var consumeResult = _consumer.Consume(_cancellationToken); //TimeSpan.FromSeconds(1),
                    //var sessionId = consumeResult.Message.Key;
                    var msg = consumeResult.Message.Value;
                    Log.Information(@$"Recive  {msg.MsgKey!}, {msg.MsgId.ToString()!}");

                    if (!option.IsMultipleMessages)
                    {
                        // Save single messages to DB 2787 msg/s
                        //_MQSession.SaveMsgToDataBase(ea.BasicProperties, ea.Body);
                        _MQSession.SaveMsgToDataBase(msg.MsgId.ToString(), msg.Msg, msg.MsgKey);
                        if (option.IsConfirmMsgAndRemoveFromQueue)
                        {
                            
                            await ConfirmMessageAsync((ulong)consumeResult.Message.Key);
                        }
                    }
                    //else
                        // TODO Save multiple messages to DB 6900 msg/s
                        //await _MQSession.SendMsgToLocalQueue(ulong)consumeResult.Message.Key, msg.MsgId.ToString(), msg.Msg, msg.MsgKey);
                }
                catch (Exception e)
                {
                    Log.Error("Kafka Consumer Exception: " + e);
                    // Note: transactions may be committed / aborted in the partitions
                    // revoked / lost handler as a side effect of the call to close.
                    break;
                }
            }
            _consumer.Close();
        }
        public async Task<uint> MessageCountAsync()
        {
            return await _channel.MessageCountAsync(_queueName);
        }
        public async Task<BasicGetResult> GetMessageAsync()
        {
            return await _channel.BasicGetAsync(_queueName, false);
        }
        /*
           public void listenServiceCall(@Payload String message,
                                          Acknowledgment acknowledgment) {
                //here is your logic for message processing
                boolean logicForMessageProcessingCompleted = true;
                if (logicForMessageProcessingCompleted) {
                    //manual commit
                    acknowledgment.acknowledge();
                }
            }
         * */
        //item.MsgKey,item.msg rabbitMQSettings.Exchange
        public async Task PublishMessageAsync(string msgKey, string msg)
        {
            var m = new MsgQueueItem {
                SessionId = _MQSession.GetSessionId(),
                MsgId = Guid.NewGuid(),
                Msg = msg,
                MsgKey = msgKey
            };
            _producer.Produce(option.KafkaServSettings.Topic, new Message<long, MsgQueueItem> { Key = _iCount, Value = m });
            _iCount++;
            if (_iCount % 1000 == 0)
            {
                Log.Information(@$"Send {_iCount} messages.");
                _producer.Flush();
                _iCount = 0;
            }
/*
            byte[] messageBodyBytes = Encoding.UTF8.GetBytes(msg ?? throw new ArgumentNullException());
            BasicProperties props = new BasicProperties();
            props.ContentType = "text/plain";
            props.MessageId = Guid.NewGuid().ToString();
            props.Type = msgKey;
            props.DeliveryMode = DeliveryModes.Persistent;
            await _channel.BasicPublishAsync<BasicProperties>(_exchange, routingKey: msgKey, mandatory: true, props, messageBodyBytes);
*/
        }
        public bool IsOpen
        {
            get
            {
                return _channel != null && _channel.IsOpen && !_disposed;
            }
        }
        public async Task CloseAsync()
        {
            //await _channel.CloseAsync();
            //ConsumerUnSubscription();
            //await _producer.FlushAsync();
            _producer.Flush();
            _producer.Dispose();
        }
        public async Task ConfirmMessageAsync(ulong offsetId, bool multiple = false)
        {
            //TODO Kafka acknowledgement
            //await _channel.BasicAckAsync(offsetId, false);

        }
        public async Task RejectMessageAsync(ulong offsetId, bool requeue = true)
        {
            //await _channel.BasicRejectAsync(deliveryTag: offsetId, requeue: true);

        }
        
    }
}