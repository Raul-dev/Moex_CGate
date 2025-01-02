using Microsoft.Extensions.Configuration;
using MQ.bll.Common;
using RabbitMQ.Client;
using Serilog;
using System.Text;
using MQ.bll.RabbitMQ;
using MQ.bll.Kafka;

namespace MQ.bll
{
    public class ReceiveAllMessages
    {
        BllOption option;
        CancellationToken _cancellationToken;
        MQSession? _MQSession;
        Thread? _bulkThread;
        IQueueChannel? _channel;

        public ReceiveAllMessages(BllOption bllOption, IConfiguration configuration, CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            option = bllOption;
        }
        public async Task ProcessLauncherAsync()
        {
            int executionCount = 0;

            _MQSession = new MQSession(option, _cancellationToken);
            long sessionId = _MQSession.StartSessionProcessing();
            if (sessionId == -1)
            {
                Log.Error("SessionId -1");
                return;
            }
            await InitFactory();
            if (_MQSession.SessionMode != SessionModeEnum.BufferOnly)
                _MQSession.RunEtlThread("All");
            while (!_cancellationToken.IsCancellationRequested)
            {
                executionCount++;
                await Task.Delay(50000, _cancellationToken);

                Log.Information("Service running. Count: {Count} Connection state: {state}", executionCount, _channel!.IsOpen);
            }

        }

        public async Task ProcessLauncherConsoleAsync()
        {
            _MQSession = new MQSession(option, _cancellationToken);
            long sessionId = _MQSession.StartSessionProcessing();
            if (sessionId == -1)
                return;
            int errorCount = 0;
            var res = await MQProcess();
            if (res != 0)
                errorCount++;
            return;
        }
        public void StartBulkThread()
        {
            if (_bulkThread == null)
            {
                _bulkThread = new Thread(BulkThread);
                _bulkThread.Name = "BulkThread";
            }

            if (!_bulkThread.IsAlive)
            {
                _bulkThread.Start(_cancellationToken);

            }
        }
        public void BulkThread(object? sender)
        {
            if (sender is null)
                return;
            CancellationToken token = (CancellationToken)sender;
            int hash = Thread.CurrentThread.GetHashCode();
            Log.Debug("Start Bulk Thread hash {0}", hash);
            int i = 0;
            //For Debug
            //if(MessagePropertyKey == "key")
            //{
            //    int h = 0;
            //    h++;
            //}
            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    Log.Debug("Bulk Thread hash, In iteration {1}, cancellation has been requested...", hash, i);
                    break;
                }

                try
                {
                    _MQSession!.SaveMsgToDataBaseBulk();
                }
                catch (Exception ex)
                {
                    Log.Error("Bulk thread error: {0}.", ex.Message);

                }

                token.WaitHandle.WaitOne(1000);
                i++;
            }
        }

        public async Task InitFactory()
        {

            if (_MQSession!.SessionMode == SessionModeEnum.BufferOnly ||
                _MQSession.SessionMode == SessionModeEnum.FullMode ||
                _MQSession.SessionMode == SessionModeEnum.WhileGet
                )
            {
                //Log.Debug($@"Creating MQ factory connection: Host={_MQSettings.Host}, Port={_MQSettings.Port}, Mode={_MQSession.SessionMode.ToString()}.");

                _channel = option.IsKafka ? new KafkaChannel(option, _cancellationToken) : new RabbitMQChannel(option, _cancellationToken);
                Random rnd = new Random();
                
                if (_MQSession.SessionMode != SessionModeEnum.WhileGet)
                {
                    Log.Debug($@"Starting ConsumerSubscription creation.");
                
                    await _channel!.InitSetup( _MQSession, false, true);
                    if (option.IsMultipleMessages)
                        StartBulkThread();
                }
                else
                    await _channel!.InitSetup( _MQSession, false, false);
                _MQSession.SetChannel(_channel);

            }
        }

        public async Task<int> MQProcess()
        {
            try
            {
                
                await InitFactory();
                if (_channel == null) throw new ArgumentNullException();

                if (_MQSession!.SessionMode == SessionModeEnum.WhileGet) //Get message by message
                {
                    //Test load procedures
                    _MQSession.RunEtlLoadProcedure("All"); //execute load procedures
                    long msgcnt = 0, rcvcnt =0; // for Debug Mode
                    msgcnt = await _channel!.MessageCountAsync();
                    
                    if (msgcnt > 0)
                        Log.Debug("We are starting to receive messages from the queue. Messages Count: {0}", msgcnt);
                    for (uint i = 0; i < msgcnt; i++)
                    {
                        
                        BasicGetResult? message = await _channel!.GetMessageAsync();
                        if (message != null)
                        {
                            rcvcnt ++;
                            _MQSession.SaveMsgToDataBase(message.BasicProperties.MessageId!, Encoding.UTF8.GetString(message.Body.ToArray()), message.BasicProperties.Type!);
                            if (option.IsConfirmMsgAndRemoveFromQueue)
                            {
                                await _channel!.AcknowledgeMessageAsync(message.DeliveryTag);
                            }
                        }
                        else
                            Log.Error($"Null message {i}");
                    }
                    if (msgcnt > 0)
                        Log.Debug("Finish of receiving the messages. MQ Messages Count: {0}", rcvcnt);
                }
                else
                    if(_MQSession.SessionMode != SessionModeEnum.BufferOnly)
                        _MQSession.RunEtlThread("All"); //Started load proc threads

                while (true && _MQSession.SessionMode != SessionModeEnum.WhileGet)
                {

                    _cancellationToken.WaitHandle.WaitOne(2000);
                    
                    if (_MQSession.SessionMode != SessionModeEnum.BufferOnly)
                        if (_channel == null || !_channel!.IsOpen)
                        {
                            Log.Error("MQ channel is closed.");
                            break;
                        }
                }

                return 0;
            }
            catch (Exception ex)
            {

                Log.Error(ex.Message);
                return -1;
            }
            finally     
            {
                await _channel!.CloseAsync();
                CleanProcess();
            }
        }
        public void CleanProcess()
        {

            if (_channel != null)
            {
                _channel!.CloseAsync();
                
            }
            _MQSession!.FinishSessionProcessing();

            _MQSession.CleanProcess();

        }
    }
}
