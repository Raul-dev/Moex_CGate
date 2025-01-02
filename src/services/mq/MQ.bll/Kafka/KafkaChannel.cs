using MQ.bll.Common;
using RabbitMQ.Client;
using Serilog;
using System.Text;
using Confluent.Kafka;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace MQ.bll.Kafka
{
    public class MsgKafkaItem
    {
        public Guid MsgId { get; set; }
        public required string Msg { get; set; }
        public required string MsgKey { get; set; }
        public DateTime CreateDate { get; set; }
    }
    public class KafkaChannel : KafkaGateway, IQueueChannel 
    {
        BllOption option;
        CancellationToken _cancellationToken;
        IProducer<long, MsgKafkaItem>? _producer;
        IConsumer<long, MsgKafkaItem>? _consumer;
        int _iCount = 0;

        private ConsumerConfig _consumerConfig = new ConsumerConfig();
        private List<TopicPartition> _partitions = new List<TopicPartition>();
        private readonly ConcurrentQueue<TopicPartitionOffset> _offsetStorage = new();
        private readonly long _maxBatchSize = 40000;
        //private readonly TimeSpan _readCommittedOffsetsTimeout;
        private bool _hasFatalError = false;

        private DateTime _lastFlushAt = DateTime.UtcNow;
        private readonly Semaphore _flushToken = new(1, 1);
        private readonly TimeSpan _sweepUncommittedInterval = TimeSpan.FromSeconds(5);
        
        bool _disposed = false;
        Task? _subscription;
        TopicPartition? _partition;
        MQSession? _MQSession;
        
        public KafkaChannel(BllOption option, CancellationToken cancellationToken)
        {
            this.option = option;
            _disposed = false;
            _cancellationToken = cancellationToken;
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
                //_consumer!.Commit();
                //var committedOffsets = _consumer!.Committed(_partitions, _readCommittedOffsetsTimeout);
                //foreach (var committedOffset in committedOffsets)
                //    Log.Information("Committed offset: {Offset} on partition: {ChannelName} for topic: {Topic}", committedOffset.Offset.Value.ToString(), committedOffset.Partition.Value.ToString(), committedOffset.Topic);

            }
            catch (Exception ex)
            {
                //this may happen if the offset is already committed
                Log.Debug("Error committing the current offset to Kafka before closing: {ErrorMessage}", ex.Message);
            }
        }
        public async Task InitSetup( MQSession? mqSession = null, bool isSend = true, bool isSubscription = false)
        {
            _disposed = false;
            var server = $"{option.KafkaServSettings.Host}:{option.KafkaServSettings.Port}";
            
            if (mqSession == null)
                _MQSession = new MQSession(option, _cancellationToken);
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
                _consumer!.Subscribe(option.KafkaServSettings.Topic);

                if (_partitions.Count > 0)
                    _partition = _partitions.First();
                
                var r = Task.Run(CommitMessageAsync);
            }
        }
        public async Task InitConsumer(string server, bool isSubscription = false)
        {
            _consumerConfig = new ConsumerConfig
            {
                BootstrapServers = server, 
                ClientId = 1 + "_consumer",
                GroupId = option.KafkaServSettings.GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoOffsetStore = false,
                EnableAutoCommit = false,
                ReconnectBackoffMs = 10,
                //ReconnectMs = 10,
                //AckMode
                //Acks = Confluent.Kafka.Acks.Leader,
                EnablePartitionEof = false,
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
                    //CommitOffsetsFor(list);
                    while (_flushToken.WaitOne())
                    {
                        CommitAllOffsets(DateTime.Now);
                        break;
                    }

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
            {
                _subscription = Task.Run(ReceivedMessageAsync);
                

            }
          
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
                    _consumer!.Commit(revokedOffsetsToCommit);
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

        void Acknowledge(TopicPartitionOffset bagData )
        {

            try
            {
                //Acknowledge Не работает по 1й штучке, поэтому делаем пачками в потоке CommitMessageAsync() 
                //var lst = new List<TopicPartitionOffset>();
                //lst.Add(bagData);
                //_consumer!.Commit(lst);
                //return;
                var topicPartitionOffset = bagData as TopicPartitionOffset;

                var offset = new TopicPartitionOffset(topicPartitionOffset.TopicPartition, new Offset(topicPartitionOffset.Offset + 1));

                /*
                Log.Information("Storing offset {Offset} to topic {Topic} for partition {ChannelName}",
                    new Offset(topicPartitionOffset.Offset + 1).Value, topicPartitionOffset.TopicPartition.Topic,
                    topicPartitionOffset.TopicPartition.Partition.Value);
                */


                _offsetStorage.Enqueue (offset);


                /*
                if (_offsetStorage.Count % _maxBatchSize == 0)
                  FlushOffsets();
                else
                  SweepOffsets();
                */
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

        // The batch size has been exceeded, so flush our offsets
        private void FlushOffsets()
        {
            var now = DateTime.UtcNow;
            if (_flushToken.WaitOne())
            {
                //This is expensive, so use a background thread
                Task.Factory.StartNew(
                    action: _ => CommitOffsets(),
                    state: now,
                    cancellationToken: CancellationToken.None,
                    creationOptions: TaskCreationOptions.DenyChildAttach,
                    scheduler: TaskScheduler.Default);
            }
            //else
            //{
            //    Log.Information("Skipped committing offsets, as another commit or sweep was running");
            //}
        }

        public void Flush()
        {
            if(_offsetStorage.Count > 0 )
                //if (_flushToken.CurrentCount == 1)
                    SweepOffsets();
        }

        //If it is has been too long since we flushed, flush now to prevent offsets accumulating 
        private void SweepOffsets()
        {
            var now = DateTime.UtcNow;

            if (now - _lastFlushAt < _sweepUncommittedInterval)
            {
                return;
            }
            //var r  = _flushToken.
            //if (_flushToken.Wait(TimeSpan.Zero))
            if (_flushToken.WaitOne())
            {
                if (now - _lastFlushAt < _sweepUncommittedInterval)
                {
                    _flushToken.Release(1);
                    return;
                }
                Log.Information(@$"Start sweep commit offsets {_lastFlushAt} Count: {_offsetStorage.Count}");
                //This is expensive, so use a background thread
                Task.Factory.StartNew(
                    action: state => CommitAllOffsets((DateTime)state!),
                    state: now,
                    cancellationToken: CancellationToken.None,
                    creationOptions: TaskCreationOptions.DenyChildAttach,
                    scheduler: TaskScheduler.Default);
            }
            //else
            //{
            //    Log.Information("Skipped sweeping offsets, as another commit or sweep was running");
            //}
        }
        private void CommitOffsets()
        {
            try
            {
                var listOffsets = new List<TopicPartitionOffset>();
                for (int i = 0; i < _maxBatchSize; i++)
                {
                    bool hasOffsets = _offsetStorage.TryDequeue(out var offset);
                    if (hasOffsets)
                        listOffsets.Add(offset!);
                    else
                        break;

                }

                /*
                if (Log.IsEnabled(Log.Level.Information))
                {
                    var offsets = listOffsets.Select(tpo =>
                        $"Topic: {tpo.Topic} Partition: {tpo.Partition.Value} Offset: {tpo.Offset.Value}");
                    var offsetAsString = string.Join(Environment.NewLine, offsets);
                    Log.Information("Commiting offsets: {0} {Offset}", Environment.NewLine, offsetAsString);
                }
                */
                if(listOffsets.Count > 0)
                    _consumer!.Commit(listOffsets);
                

            }
            finally
            {
                _flushToken.Release(1);
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
                    bool hasOffsets = _offsetStorage.TryDequeue(out var offset);
                    if (hasOffsets)
                        listOffsets.Add(offset!);
                    else
                        break;

                }

                //if (Log.IsEnabled(Serilog.Events.LogEventLevel.Information))
                //{
                //    var offsets = listOffsets.Select(tpo =>
                //        $"Topic: {tpo.Topic} Partition: {tpo.Partition.Value} Offset: {tpo.Offset.Value}");
                //    var offsetAsString = string.Join(Environment.NewLine, offsets);
                //    Log.Information("Sweeping offsets: {0} {Offset}", Environment.NewLine, offsetAsString);
                //}
                if (listOffsets.Count > 0)
                {
                    _consumer!.Commit(listOffsets);
                    _lastFlushAt = flushTime;
                }
                
            }
            finally
            {
                
                _flushToken.Release(1);
            }
        }

        async Task CommitMessageAsync()
        {

            while (!_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _cancellationToken.ThrowIfCancellationRequested();

                    long lmin = 0;
                    long lmax = 0;
                    var listOffsets = new List<TopicPartitionOffset>();
                    var currentOffsetsInBag = (_maxBatchSize < _offsetStorage.Count) ? _maxBatchSize : _offsetStorage.Count;
                    for (int i = 0; i < currentOffsetsInBag; i++)
                    {
                        bool hasOffsets = _offsetStorage.TryDequeue(out var offset);

                        if (hasOffsets)
                        {
                            if (i == 0)
                                lmin = offset!.Offset.Value;
                            lmin = (lmin < offset!.Offset.Value) ? lmin : offset.Offset.Value;
                            lmax = (lmax > offset!.Offset.Value) ? lmax : offset.Offset.Value;
                            listOffsets.Add(offset!);
                        }
                        else
                            break;

                    }

                    //if (Log.IsEnabled(Serilog.Events.LogEventLevel.Information))
                    //{
                    //    var offsets = listOffsets.Select(tpo =>
                    //        $"Topic: {tpo.Topic} Partition: {tpo.Partition.Value} Offset: {tpo.Offset.Value}");
                    //    var offsetAsString = string.Join(Environment.NewLine, offsets);
                    //    Log.Information("Sweeping offsets: {0} {Offset}", Environment.NewLine, offsetAsString);
                    //}
                    if (listOffsets.Count > 0)
                    {
                        Log.Information(@$"Commit count {listOffsets.Count} min {lmin}, max {lmax}");
                        if (_flushToken.WaitOne())
                        {
                            await Task.Run(() => _consumer!.Commit(listOffsets));
                            _flushToken.Release();
                            
                        }

                    }
                    
                    _cancellationToken.WaitHandle.WaitOne(1000);
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                }
            }
           
        }
        async Task ReceivedMessageAsync()
        {
            int iCount = 0;
            
            while (!_cancellationToken.IsCancellationRequested)
            {
                ConsumeResult<long, MsgKafkaItem>? consumeResult;
                try
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    consumeResult = null;
                    consumeResult = _consumer!.Consume(_cancellationToken); //TimeSpan.FromSeconds(1),
                     
                    if (consumeResult == null || consumeResult.IsPartitionEOF)
                        continue;
                    var msg = consumeResult.Message.Value;
                    
                    iCount++;
                    
                    if (!option.IsMultipleMessages)
                    {
                        //Save single message to DB 2787 msg/s
                        _MQSession!.SaveMsgToDataBase(msg.MsgId.ToString(), msg.Msg, msg.MsgKey);
                        if (option.IsConfirmMsgAndRemoveFromQueue)
                        {
                            Acknowledge(consumeResult.TopicPartitionOffset);
                        }
                    }
                   else
                        //Bulk Save multiple messages to DB 6900 msg/s
                        await _MQSession!.SendMsgToLocalQueue((ulong)consumeResult.Offset.Value, msg.MsgId.ToString(), msg.Msg, msg.MsgKey);

                }
                catch (Exception e)
                {
                    Log.Error("Kafka Consumer Exception: " + e);
                    // Note: transactions may be committed / aborted in the partitions
                    // revoked / lost handler as a side effect of the call to close.
                    break;
                }
                //if (iCount % 10000 == 0)
                //{
                //    Log.Information(@$"Receive offset {consumeResult.Offset.Value}");
                //    //_cancellationToken.WaitHandle.WaitOne(1);
                //}
            }
            _consumer!.Close();
        }
        public async Task<long> MessageCountAsync()
        {
            if (_producer != null)
            {
                await Task.Run(()=>_producer.Flush());
            }
            //TODO починить: Assign не работает, Приходится забирать первое сообщение _consumer!.Consume
            //_consumer!.Assign(new TopicPartition(option.KafkaServSettings.Topic, 0));
            if (_consumer != null)
            {
                ConsumeResult<long, MsgKafkaItem> consumeResult;
                int iteration = 0;
                do
                {
                    consumeResult = await Task.Run(() => _consumer!.Consume(TimeSpan.FromSeconds(5)));
                    iteration++;
                } while (consumeResult == null && iteration < 10);
                if (consumeResult == null || consumeResult.IsPartitionEOF)
                    return -1;
                if (_partition == null)
                    _partition = _consumer!.Assignment.FirstOrDefault()!;
                await RejectMessageAsync((ulong)consumeResult.Offset.Value);
                WatermarkOffsets watermarkOffsets = _consumer!.QueryWatermarkOffsets(_partition, TimeSpan.FromSeconds(10));
                long total = watermarkOffsets.High - consumeResult.Offset.Value;
                return total;
            }
          
            if (_partition == null)
            {
                
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
        public async Task<BasicGetResult?> GetMessageAsync()
        {

            ConsumeResult<long, MsgKafkaItem> consumeResult;
            int iteration = 0;
            do
            {
                consumeResult = await Task.Run(() => _consumer!.Consume(TimeSpan.FromSeconds(5)));
            } while (consumeResult == null && iteration < 10);
            if (consumeResult == null || consumeResult.IsPartitionEOF)
                return null;
            var msg = consumeResult.Message.Value;
            var basicGetResult1 = new BasicGetResult((ulong)consumeResult.Offset.Value, true, option.KafkaServSettings.Topic, msg.MsgKey, 1, new BasicProperties() { MessageId = msg.MsgId!.ToString(), Type = msg.MsgKey }, Encoding.Default.GetBytes(msg.Msg!));

            if(_partition == null)
                _partition = _consumer!.Assignment.FirstOrDefault()!;
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
            await Task.Run(() => _producer!.Produce(option.KafkaServSettings.Topic, new Message<long, MsgKafkaItem> { Key = _iCount, Value = m }));
            _iCount++;
            if (_iCount % 10000 == 0)
            {
                _producer!.Flush();
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

            if (_producer != null)
            {
                await Task.Run(() => _producer.Flush());
                _producer.Dispose();
            }
            if (_consumer != null)
            {
      
                Flush();
                _consumer!.Close();
                _consumer!.Dispose();
            }
        }
        public async Task AcknowledgeMessageAsync(ulong offsetId, bool multiple = false)
        {
            TopicPartitionOffset t = new TopicPartitionOffset(topic: option.KafkaServSettings.Topic, new Partition(), offset: new Offset((long)offsetId));
            await Task.Run(() => Acknowledge(t));
        }
        public async Task RejectMessageAsync(ulong offsetId, bool requeue = true)
        {
            if (_flushToken.WaitOne())
            {
                Log.Warning($@"Reject {offsetId}");
                await Task.Run(() => _consumer!.Seek(new TopicPartitionOffset(option.KafkaServSettings.Topic, new Partition(0), (int)offsetId)));
                _flushToken.Release();
            }

        }

    }

}