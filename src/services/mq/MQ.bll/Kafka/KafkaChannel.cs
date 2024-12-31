using MQ.bll.Common;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using Serilog;
using System.Text;
using Confluent.Kafka;
using Serilog.Parsing;
using MongoDB.Driver.Core.Servers;
using BenchmarkDotNet.Disassemblers;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using ZstdSharp.Unsafe;


namespace MQ.bll.Kafka
{
    public class MsgKafkaItem
    {
        public Guid MsgId { get; set; }
        public required string Msg { get; set; }
        public required string MsgKey { get; set; }
        public DateTime CreateDate { get; set; }
    }
    public class KafkaChannel : KafkaGateway, IQueueChannel //, KafkaGateway
    {
        BllOption option;
        IProducer<long, MsgKafkaItem> _producer;
        IConsumer<long, MsgKafkaItem> _consumer;
        int _iCount = 0;

        //IChannel _channel;
        //string _queueName;
        private ConsumerConfig _consumerConfig;
        private List<TopicPartition> _partitions = new List<TopicPartition>();
        private readonly ConcurrentBag<TopicPartitionOffset> _offsetStorage = new();
        private readonly long _maxBatchSize;
        private readonly TimeSpan _readCommittedOffsetsTimeout;
        private bool _hasFatalError;

        private DateTime _lastFlushAt = DateTime.UtcNow;
        private readonly SemaphoreSlim _flushToken = new(1, 1);
        private readonly TimeSpan _sweepUncommittedInterval = TimeSpan.FromSeconds(30);
        
        bool _disposed;
        Task _subscription;
        TopicPartition _partition;
        //string _exchange;
        CancellationToken _cancellationToken;
        MQSession _MQSession;
        AsyncEventingBasicConsumer mqConsumer;
        string? ConsumeTag;
        
        public KafkaChannel(BllOption option)
        {
            this.option = option;
            _disposed = false;
            Topic = option.KafkaServSettings.Topic;
            _clientConfig = new ClientConfig
            {
                BootstrapServers = $"{option.KafkaServSettings.Host}:{option.KafkaServSettings.Port}",
                ClientId = option.KafkaServSettings.GroupId,
                //A comma-separated list of debug contexts to enable.  Producer: broker, topic, msg. Consumer: consumer, cgrp, topic, fetch                
                //Debug = configuration.Debug,
                //SaslMechanism = configuration.SaslMechanisms.HasValue ? (Confluent.Kafka.SaslMechanism?)((int)configuration.SaslMechanisms.Value) : null,
                //SaslKerberosPrincipal = configuration.SaslKerberosPrincipal,
                //SaslUsername = configuration.SaslUsername,
                //SaslPassword = configuration.SaslPassword,
                //SecurityProtocol = configuration.SecurityProtocol.HasValue ? (Confluent.Kafka.SecurityProtocol?)((int)configuration.SecurityProtocol.Value) : null,
                //SslCaLocation = configuration.SslCaLocation
            };
        }

        public KafkaChannel(IChannel channel)
        {
            //this._channel = channel;
            _disposed = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            Close();
            //_channel.Dispose();
            _disposed = false;
            GC.SuppressFinalize(this);
        }
        private void Close()
        {
            try
            {
                _consumer.Commit();

                var committedOffsets = _consumer.Committed(_partitions, _readCommittedOffsetsTimeout);
                foreach (var committedOffset in committedOffsets)
                    Log.Information("Committed offset: {Offset} on partition: {ChannelName} for topic: {Topic}", committedOffset.Offset.Value.ToString(), committedOffset.Partition.Value.ToString(), committedOffset.Topic);

            }
            catch (Exception ex)
            {
                //this may happen if the offset is already committed
                Log.Debug("Error committing the current offset to Kafka before closing: {ErrorMessage}", ex.Message);
            }
        }
        public async Task InitSetup(CancellationToken cancellationToken, MQSession? mqSession = null, bool isSend = true, bool isSubscription = false)
        {
            _disposed = false;
            var server = $"{option.KafkaServSettings.Host}:{option.KafkaServSettings.Port}";
            this._cancellationToken = cancellationToken;
            if (mqSession == null)
                _MQSession = new MQSession(option, cancellationToken);
            else
                _MQSession = mqSession;
            if (isSend)
            {
                var config = new ProducerConfig
                {
                    BootstrapServers = server, 
                    AllowAutoCreateTopics = true,
                    EnableSslCertificateVerification = false,
                    Acks = Confluent.Kafka.Acks.All,
                };
                _producer = new ProducerBuilder<long, MsgKafkaItem>(config)
                    .SetKeySerializer(Serializers.Int64)
                    .SetValueSerializer(new MsgQueueSerializer())
                    .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
                    .Build();
                
            }
            else
            {
                await InitConsumer(server, isSubscription);
               
            }
            
            MakeChannels = OnMissingChannel.Create;
            NumPartitions = 1;
            ReplicationFactor = 1;
            TopicFindTimeout = TimeSpan.FromMilliseconds(10000);

            EnsureTopic();
            if (!isSend)
            {
                _consumer.Subscribe(option.KafkaServSettings.Topic);

                if (_partitions.Count > 0)
                    _partition = _partitions.First();
            }
        }
        public async Task InitConsumer(string server, bool isSubscription = false)
        {
            _consumerConfig = new ConsumerConfig
            {
                BootstrapServers = server, // TODO: make servers a collection and build string here.
                ClientId = 1 + "_consumer",
                GroupId = option.KafkaServSettings.GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoOffsetStore = false,
                EnableAutoCommit = false,
                //AckMode
                //Acks = Confluent.Kafka.Acks.Leader,
                EnablePartitionEof = true,
                AllowAutoCreateTopics = false, //We will do this explicit always so as to allow us to set parameters for the topic
                IsolationLevel = IsolationLevel.ReadCommitted,
                PartitionAssignmentStrategy = PartitionAssignmentStrategy.Range, // .CooperativeSticky,

            };
            _consumer = new ConsumerBuilder<long, MsgKafkaItem>(_consumerConfig)
                .SetKeyDeserializer(Deserializers.Int64)
                .SetValueDeserializer(new MsgQueueSerializer())
                .SetPartitionsAssignedHandler((_, list) =>
                {
                    var partitions = list.Select(p => $"{p.Topic} : {p.Partition.Value}");

                    Log.Information("Partition Added {Channels}", String.Join(",", partitions));

                    _partitions.AddRange(list);
                    _partition = _partitions.First();
                })
                .SetPartitionsRevokedHandler((_, list) =>
                {
                    //We should commit any offsets we have stored for these partitions
                    CommitOffsetsFor(list);

                    var revokedPartitionInfo = list.Select(tpo => $"{tpo.Topic} : {tpo.Partition}").ToList();

                    Log.Information("Partitions for consumer revoked {Channels}", string.Join(",", revokedPartitionInfo));

                    _partitions = _partitions.Where(tp => list.All(tpo => tpo.TopicPartition != tp)).ToList();
                })
                .SetPartitionsLostHandler((_, list) =>
                {
                    var lostPartitions = list.Select(tpo => $"{tpo.Topic} : {tpo.Partition}").ToList();

                    Log.Information("Partitions for consumer lost {Channels}", string.Join(",", lostPartitions));

                    _partitions = _partitions.Where(tp => list.All(tpo => tpo.TopicPartition != tp)).ToList();
                })
                .SetErrorHandler((_, error) =>
                {
                    _hasFatalError = error.IsFatal;

                    if (_hasFatalError)
                        Log.Error("Code: {ErrorCode}, Reason: {ErrorMessage}, Fatal: {FatalError}", error.Code, error.Reason, true);
                    else
                        Log.Warning("Code: {ErrorCode}, Reason: {ErrorMessage}, Fatal: {FatalError}", error.Code, error.Reason, false);
                })

                .Build();

            if (isSubscription)
                _subscription = Task.Run(ReceivedMessageAsync);
        }
        //Called during a revoke, we are passed the partitions that we are revoking and their last offset and we need to
        //commit anything we have not stored.
        private void CommitOffsetsFor(List<TopicPartitionOffset> revokedPartitions)
        {
            try
            {
                //find the provided set of partitions amongst our stored offsets 
                var partitionOffsets = _offsetStorage.ToArray();
                var revokedOffsetsToCommit =
                    partitionOffsets.Where(tpo =>
                            revokedPartitions.Any(ptc =>
                                ptc.TopicPartition == tpo.TopicPartition
                                && ptc.Offset.Value != Offset.Unset.Value
                                && tpo.Offset.Value > ptc.Offset.Value
                            )
                        )
                        .ToList();
                //determine if we have offsets still to commit
                if (revokedOffsetsToCommit.Any())
                {
                    //commit them
                    LogOffSetCommitRevokedPartitions(revokedOffsetsToCommit);
                    _consumer.Commit(revokedOffsetsToCommit);
                }
            }
            catch (KafkaException error)
            {
                Log.Error(
                    "Error Committing Offsets During Partition Revoke: {Message} Code: {ErrorCode}, Reason: {ErrorMessage}, Fatal: {FatalError}",
                    error.Message, error.Error.Code, error.Error.Reason, error.Error.IsFatal
                );
            }
        }
        [Conditional("DEBUG")]
        [DebuggerStepThrough]
        private void LogOffSetCommitRevokedPartitions(List<TopicPartitionOffset> revokedOffsetsToCommit)
        {
            Log.Debug("Saving revoked partition offsets: {OffSetCount}", revokedOffsetsToCommit.Count);
            foreach (var offset in revokedOffsetsToCommit)
            {
                Log.Debug("Saving revoked partition offset: {Offset} on partition: {Partition} for topic: {Topic}",
                    offset.Offset.Value.ToString(), offset.Partition.Value.ToString(), offset.Topic);
            }
        }

        /// <summary>
        /// Acknowledges the specified message.
        /// We do not have autocommit on and this stores the message that has just been processed.
        /// We use the header bag to store the partition offset of the message when  reading it from Kafka. This enables us to get hold of it when
        /// we acknowledge the message via Brighter. We store the offset via the consumer, and keep an in-memory list of offsets. If we have hit the
        /// batch size we commit the offsets. if not, we trigger the sweeper, which will commit the offset once the specified time interval has passed if
        /// a batch has not done so.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Acknowledge(TopicPartitionOffset bagData )
        {
            //if (!message.Header.Bag.TryGetValue(HeaderNames.PARTITION_OFFSET, out var bagData))
            //    return;

            try
            {
                var topicPartitionOffset = bagData as TopicPartitionOffset;

                var offset = new TopicPartitionOffset(topicPartitionOffset.TopicPartition, new Offset(topicPartitionOffset.Offset + 1));

                /*
                Log.Information("Storing offset {Offset} to topic {Topic} for partition {ChannelName}",
                    new Offset(topicPartitionOffset.Offset + 1).Value, topicPartitionOffset.TopicPartition.Topic,
                    topicPartitionOffset.TopicPartition.Partition.Value);
                */
                _offsetStorage.Add(offset);


                //if (_offsetStorage.Count % _maxBatchSize == 0)
                //   FlushOffsets();
                //else
                SweepOffsets();

                //Log.Information("Current Kafka batch count {OffsetCount} and {MaxBatchSize}", _offsetStorage.Count.ToString(), _maxBatchSize.ToString());
            }
            catch (TopicPartitionException tpe)
            {
                var results = tpe.Results.Select(r =>
                    $"Error committing topic {r.Topic} for partition {r.Partition.Value.ToString()} because {r.Error.Reason}");
                var errorString = string.Join(Environment.NewLine, results);
               Log.Debug("Error committing offsets: {0} {ErrorMessage}", Environment.NewLine, errorString);
            }
        }
        //If it is has been too long since we flushed, flush now to prevent offsets accumulating 
        private void SweepOffsets()
        {
            var now = DateTime.UtcNow;

            if (now - _lastFlushAt < _sweepUncommittedInterval)
            {
                return;
            }

            if (_flushToken.Wait(TimeSpan.Zero))
            {
                if (now - _lastFlushAt < _sweepUncommittedInterval)
                {
                    _flushToken.Release(1);
                    return;
                }

                //This is expensive, so use a background thread
                Task.Factory.StartNew(
                    action: state => CommitAllOffsets((DateTime)state),
                    state: now,
                    cancellationToken: CancellationToken.None,
                    creationOptions: TaskCreationOptions.DenyChildAttach,
                    scheduler: TaskScheduler.Default);
            }
            else
            {
                Log.Information("Skipped sweeping offsets, as another commit or sweep was running");
            }
        }
        //Just flush everything
        private void CommitAllOffsets(DateTime flushTime)
        {
            try
            {
                var listOffsets = new List<TopicPartitionOffset>();
                var currentOffsetsInBag = _offsetStorage.Count;
                for (int i = 0; i < currentOffsetsInBag; i++)
                {
                    bool hasOffsets = _offsetStorage.TryTake(out var offset);
                    if (hasOffsets)
                        listOffsets.Add(offset);
                    else
                        break;

                }

                if (Log.IsEnabled(Serilog.Events.LogEventLevel.Information))
                {
                    var offsets = listOffsets.Select(tpo =>
                        $"Topic: {tpo.Topic} Partition: {tpo.Partition.Value} Offset: {tpo.Offset.Value}");
                    var offsetAsString = string.Join(Environment.NewLine, offsets);
                    Log.Information("Sweeping offsets: {0} {Offset}", Environment.NewLine, offsetAsString);
                }

                _consumer.Commit(listOffsets);
                _lastFlushAt = flushTime;
            }
            finally
            {
                _flushToken.Release(1);
            }
        }
        async Task ReceivedMessageAsync()
        {
            int iCount = 0;
            
            while (!_cancellationToken.IsCancellationRequested)
            {
                
                try
                {
                    _cancellationToken.ThrowIfCancellationRequested();

                    var consumeResult = _consumer.Consume(_cancellationToken); //TimeSpan.FromSeconds(1),
                    if (consumeResult.IsPartitionEOF)
                        break;
                    var msg = consumeResult.Message.Value;
                    if(iCount%1000 == 0)
                        Log.Information(@$"Recive {msg.MsgId!}  {msg.MsgKey!}, offset {consumeResult.Offset.Value}");
                    iCount++;
                    
                    if (!option.IsMultipleMessages)
                    {
                        // Save single messages to DB 2787 msg/s
                        _MQSession.SaveMsgToDataBase(msg.MsgId.ToString(), msg.Msg, msg.MsgKey);
                        if (option.IsConfirmMsgAndRemoveFromQueue)
                        {
                            Acknowledge(consumeResult.TopicPartitionOffset);
                            
                        }
                    }
                   else
                        // TODO Save multiple messages to DB 6900 msg/s
                        await _MQSession.SendMsgToLocalQueue((ulong)consumeResult.Message.Key, msg.MsgId.ToString(), msg.Msg, msg.MsgKey);
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
        public async Task<long> MessageCountAsync()
        {

            //TODO починить: Assign не работает, Приходится забирать первое сообщение _consumer.Consume
            //_consumer.Assign(new TopicPartition(option.KafkaServSettings.Topic, 0));
            if (_consumer != null)
            {
                ConsumeResult<long, MsgKafkaItem> consumeResult;
                int iteration = 0;
                do
                {
                    consumeResult = _consumer.Consume(TimeSpan.FromSeconds(1));
                    iteration++;
                } while (consumeResult == null && iteration < 10);
                if (consumeResult == null || consumeResult.IsPartitionEOF)
                    return -1;
                if (_partition == null)
                    _partition = _consumer.Assignment.FirstOrDefault();
                RejectMessageAsync((ulong)consumeResult.Offset.Value);
                WatermarkOffsets watermarkOffsets = _consumer!.QueryWatermarkOffsets(_partition, TimeSpan.FromSeconds(10));
                long total = watermarkOffsets.High - consumeResult.Offset.Value;
                return total;
            }
            //
            if (_partition == null)
            {
                //await InitConsumer($"{option.KafkaServSettings.Host}:{option.KafkaServSettings.Port}", false);
                ConsumerConfig config = new ConsumerConfig
                {
                    BootstrapServers = $"{option.KafkaServSettings.Host}:{option.KafkaServSettings.Port}",
                    ClientId = 1 + "_consumer",
                    GroupId = option.KafkaServSettings.GroupId,
                    AutoOffsetReset = AutoOffsetReset.Earliest, 
                };
                ConsumerBuilder<long, MsgKafkaItem> builder = new ConsumerBuilder<long, MsgKafkaItem>(config);
                builder.SetValueDeserializer(new MsgQueueSerializer());
                IConsumer<long, MsgKafkaItem> consumer = builder.Build();
                List<TopicPartition> partitions = GetTopicPartitions($"{option.KafkaServSettings.Host}:{option.KafkaServSettings.Port}", option.KafkaServSettings.Topic);
                TopicPartition firstPartition = partitions.First();
                WatermarkOffsets watermarkOffsets1 = consumer!.QueryWatermarkOffsets(firstPartition, TimeSpan.FromSeconds(10));
                long total1 = watermarkOffsets1.High - watermarkOffsets1.Low;
                consumer.Close();
                return (uint)total1;
            }
            if (_partition != null)
            {
                WatermarkOffsets watermarkOffsets2 = _consumer!.QueryWatermarkOffsets(_partition, TimeSpan.FromSeconds(10));
                long total2 = watermarkOffsets2.High - watermarkOffsets2.Low;
                return (uint)total2;
            }
            else return -1;
        }
        public async Task<BasicGetResult> GetMessageAsync()
        {

            ConsumeResult<long, MsgKafkaItem> consumeResult;
            int iteration = 0;
            do
            {
                consumeResult = _consumer.Consume(TimeSpan.FromSeconds(1));
            } while (consumeResult == null && iteration < 10);
            if (consumeResult == null || consumeResult.IsPartitionEOF)
                return null;
            var msg = consumeResult.Message.Value;
            var basicGetResult1 = new BasicGetResult((ulong)consumeResult.Offset.Value, true, option.KafkaServSettings.Topic, msg.MsgKey, 1, new BasicProperties() { MessageId = msg.MsgId!.ToString(), Type = msg.MsgKey }, Encoding.Default.GetBytes(msg.Msg!));

            if(_partition == null)
                _partition = _consumer.Assignment.FirstOrDefault();
            return basicGetResult1;
        }

        public async Task PublishMessageAsync(string msgKey, string msg)
        {
            var m = new MsgKafkaItem {
                MsgId = Guid.NewGuid(),
                Msg = msg,
                MsgKey = msgKey,
                CreateDate = DateTime.Now,
            };
            _producer.Produce(option.KafkaServSettings.Topic, new Message<long, MsgKafkaItem> { Key = _iCount, Value = m });
            _iCount++;
            if (_iCount % 1000 == 0)
            {
                //Log.Information(@$"Send {_iCount} messages.");
                _producer.Flush();
                _iCount = 0;
            }

        }

        public bool IsOpen
        {
            get
            {
                return !_disposed; //&& _consumer;
            }
        }

        public async Task CloseAsync()
        {
            //await _channel.CloseAsync();
            //ConsumerUnSubscription();
            //await _producer.FlushAsync();
            if (_producer != null)
            {
                _producer.Flush();
                _producer.Dispose();
            }
            if (_consumer != null)
            {
                _consumer.Close(); // .Flush();
                _consumer.Dispose();
            }
        }
        public async Task ConfirmMessageAsync(ulong offsetId, bool multiple = false)
        {
            TopicPartitionOffset t = new TopicPartitionOffset(topic: option.KafkaServSettings.Topic, _partitions.First().Partition, offset: new Offset((long)offsetId));
            Acknowledge(t);
        }
        public async Task RejectMessageAsync(ulong offsetId, bool requeue = true)
        {
            _consumer.Seek(new TopicPartitionOffset(_partition.Topic, _partition.Partition, (int)offsetId));
        }

    }

}