using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text;
using MQ.bll.Common;
using Serilog;
using Confluent.Kafka;
using System.Threading;

namespace MQ.bll.RabbitMQ
{
    public class RabbitMQChannel : IQueueChannel
    {
        BllOption option;
        MQSession? _MQSession;
        RabbitMQConnection? _connection;
        IChannel? _channel;
        string _queueName;
        bool _disposed = false;
        string _exchange;
        CancellationToken _cancellationToken;
        AsyncEventingBasicConsumer? _mqConsumer;
        string? _consumeTag;
        

        public RabbitMQChannel(BllOption option, CancellationToken cancellationToken)
        {
            this.option = option;
            _cancellationToken = cancellationToken;
            _queueName = option.RabbitMQServSettings.DefaultQueue;
            _exchange = option.RabbitMQServSettings.Exchange;
        }
        //public RabbitMQChannel(IChannel channel, CancellationToken cancellationToken)
        //{
        //    this._channel = channel;
        //    _cancellationToken = cancellationToken;
        //    _queueName = "";
        //    _disposed = false;
        //}
        public void Flush()
        {

        }
        public void Dispose()
        {
            if (_disposed) return;
            ConsumerUnSubscription();
            _channel!.Dispose();
            _disposed = false;
        }
        public async Task InitSetup(  MQSession? mqSession = null, bool isSend = true, bool isSubscription = false)
        {
            this._exchange = option.RabbitMQServSettings.Exchange;
            this._queueName = option.RabbitMQServSettings.DefaultQueue;
            
            _MQSession = mqSession!;
            _connection = new RabbitMQConnection(option.RabbitMQServSettings);
            await _connection.TryConnect();
            _channel = await _connection.CreateChannelAsync(_cancellationToken);
            if (isSend)
            {
                await _channel.QueueBindAsync(_queueName, _exchange, "*", arguments: new Dictionary<string, object?>());
            }

            if(isSubscription)
                await ConsumerSubscription(_queueName);

        }
        
        public async Task<long> MessageCountAsync()
        {
            return await _channel!.MessageCountAsync(_queueName);
        }
        public async Task<BasicGetResult?> GetMessageAsync()
        {
            return await _channel!.BasicGetAsync(_queueName, false);
        }

        public async Task PublishMessageAsync(string msgKey, string msg)
        {

            byte[] messageBodyBytes = Encoding.UTF8.GetBytes(msg ?? throw new ArgumentNullException());
            BasicProperties props = new BasicProperties();
            props.ContentType = "text/plain";
            props.MessageId = Guid.NewGuid().ToString();
            props.Type = msgKey;
            props.DeliveryMode = DeliveryModes.Persistent;
            await _channel!.BasicPublishAsync<BasicProperties>(_exchange, routingKey: msgKey, mandatory: true, props, messageBodyBytes);
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
            await _connection!.CloseAsync();
            await _channel!.CloseAsync();
            ConsumerUnSubscription();

        }
        public async Task AcknowledgeMessageAsync(ulong offsetId, bool multiple = false)
        {
            await _channel!.BasicAckAsync(offsetId, false);
        }
        public async Task RejectMessageAsync(ulong offsetId, bool requeue = true)
        {
            await _channel!.BasicRejectAsync(deliveryTag: offsetId, requeue: true);

        }
        async Task ConsumerSubscription(string queueName)
        {
            Log.Debug($"ConsumerSubscription +OnReceivedMessageHandler, Queue name: {queueName}");
            if (_channel != null)
            {
                await _channel.QueueDeclarePassiveAsync(queueName);
                await _channel.BasicQosAsync(0, 20000, false);
                _mqConsumer = new AsyncEventingBasicConsumer(_channel);

                _mqConsumer.ReceivedAsync += OnReceivedMessageHandler;

                _mqConsumer.UnregisteredAsync += OnCancel;
                _consumeTag = await _channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: _mqConsumer);
            }
        }


        void ConsumerUnSubscription()
        {
            try
            {
                if (_consumeTag != null)
                {
                    if (_channel!.IsOpen)
                        _channel.BasicCancelAsync(_consumeTag);


                    //mqConsumer.ConsumerCancelled -= OnCancel;
                    _mqConsumer!.ReceivedAsync -= OnReceivedMessageHandler;
                    _consumeTag = null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"ConsumerUnSubscription: {ex.Message}");
            }
        }

        private Task OnCancel(object sender, ConsumerEventArgs @event)
        {
            if (_cancellationToken.IsCancellationRequested == true)
                return Task.CompletedTask;
            Log.Debug(@$"OnCancel MQ Consumer: {((AsyncEventingBasicConsumer)sender).ShutdownReason}");
            return Task.CompletedTask;
        }

        async Task OnReceivedMessageHandler(object sender, BasicDeliverEventArgs ea)
        {
            if (_cancellationToken.IsCancellationRequested == true) return;
            //Log.Debug(@$"Received Message {ea.BasicProperties.Type} _cancellationToken.IsCancellationRequested: {_cancellationToken.IsCancellationRequested}");
            AsyncEventingBasicConsumer cons = (AsyncEventingBasicConsumer)sender;
            IChannel ch = cons.Channel;
            try
            {
                if (!option.IsMultipleMessages)
                {
                    // Save single messages to DB 2787 msg/s
                    _MQSession!.SaveMsgToDataBase(ea.BasicProperties.MessageId!, Encoding.UTF8.GetString(ea.Body.ToArray()), ea.BasicProperties.Type!);
                    
                    if (option.IsConfirmMsgAndRemoveFromQueue)
                    {
                        await ch.BasicAckAsync(ea.DeliveryTag, false);
                    }
                }else
                    // Save multiple messages to DB 6900 msg/s
                    await _MQSession!.SendMsgToLocalQueue(ea.DeliveryTag, ea.BasicProperties.MessageId!, Encoding.UTF8.GetString(ea.Body.ToArray()), ea.BasicProperties.Type! );

            }
            catch (Exception ex)
            {
                Log.Debug(@$"OnReceivedMessage err: {ex.Message}");
                await ch.BasicRejectAsync(deliveryTag: ea.DeliveryTag, requeue: true);

            }
            finally
            {

            }

        }
    }
}