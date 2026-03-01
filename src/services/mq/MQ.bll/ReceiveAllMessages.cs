using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MQ.bll.Common;
using MQ.bll.Kafka;
using MQ.bll.RabbitMQ;
using RabbitMQ.Client;
using Serilog;
using Serilog.Context;
using System.Text;
using System.Threading;

namespace MQ.bll
{
    public class ReceiveAllMessages
    {
        BllOption _option;
        CancellationToken _cancellationToken;                         // Local for worker
        CancellationTokenSource cts = new CancellationTokenSource();  // Local for worker
        CancellationToken _cancellationTokenGlobal;                   // Global for application
        MQSession? _MQSession;
        Thread? _bulkThread;
        IQueueChannel? _channel;
        int _executionCount;
        string? _errorMessage;
        public int GetExecutionCount()
        {
            return _executionCount;
        }
        public ReceiveAllMessages(BllOption bllOption, CancellationToken cancellationToken)
        {
            _cancellationToken = cts.Token;
            _cancellationTokenGlobal = cancellationToken;
            _option = bllOption;
           
        }
        public async Task ProcessLauncherAsync()
        {
            _executionCount = 0;
            var tcs = new TaskCompletionSource<int>();
            string errorMessage="";
            try
            {
                LogContext.PushProperty("WorkerLogPrefix", _option.LogPrefix);
                Log.Information("Worker initialisation");
                if (String.IsNullOrEmpty(_option.DataBaseServSettings.ServerName) || String.IsNullOrEmpty(_option.DataBaseServSettings.DataBase))
                {
                    errorMessage = "Empty DataBase or Server DB";
                    _errorMessage += errorMessage + ";";
                    Log.Error(errorMessage);
                    return;
                }
                else
                    Log.Information("Database Server:{0}, DB:{1}", _option.DataBaseServSettings.ServerName, _option.DataBaseServSettings.DataBase);

                _MQSession = new MQSession(_option, _cancellationToken);
                long sessionId = _MQSession.StartSessionProcessing();
                if (sessionId == -1)
                {
                    errorMessage = "SessionId -1";
                    _errorMessage += errorMessage + ";";
                    Log.Error("SessionId -1");
                    return;
                }

                int result = await InitFactory();
                if (_MQSession.SessionMode != SessionModeEnum.BufferOnly)
                    _MQSession.RunEtlThread("All");
                while (!_cancellationToken.IsCancellationRequested && !_cancellationTokenGlobal.IsCancellationRequested && result == 0)
                {
                    _executionCount++;
                    await Task.Delay(50000, _cancellationTokenGlobal);

                    Log.Debug("Worker running. Count: {Count}, Connection channel state: {state}", _executionCount, _channel!.IsOpen);
                }
                _executionCount = 0;
            }
            catch (Exception ex)
            {
                _errorMessage += ex.Message + ";";
                Log.Error(ex.Message);
            }
            finally
            {
                CancelAll(_cancellationTokenGlobal.IsCancellationRequested);
                _errorMessage = "";
            }
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

        public async Task<int> InitFactory()
        {
            int res = 0;
            if (_MQSession!.SessionMode == SessionModeEnum.BufferOnly ||
                _MQSession.SessionMode == SessionModeEnum.FullMode ||
                _MQSession.SessionMode == SessionModeEnum.WhileGet
                )
            {
                //Log.Debug($@"Creating MQ factory connection: Host={_MQSettings.Host}, Port={_MQSettings.Port}, Mode={_MQSession.SessionMode.ToString()}.");

                _channel = _option.IsKafka ? new KafkaChannel(_option, _cancellationToken) : new RabbitMQChannel(_option, _cancellationToken);
                Random rnd = new Random();
                try
                {
                    if (_MQSession.SessionMode != SessionModeEnum.WhileGet)
                    {
                        Log.Debug($@"Starting Consumer subscription.");

                        await _channel!.InitSetup(_MQSession, false, true);
                        if (_option.IsMultipleMessages)
                            StartBulkThread();
                    }
                    else
                        await _channel!.InitSetup(_MQSession, false, false);
                    _MQSession.SetChannel(_channel);
                }catch(Exception ex)
                {
                    _errorMessage += ex.Message + ";";
                    Log.Error(ex.Message);
                    res = 1;
                }

            }
            return res;
        }
        /*
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
                            if (_option.IsConfirmMsgAndRemoveFromQueue)
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

                //while (true && _MQSession.SessionMode != SessionModeEnum.WhileGet)
                //{

                //    _cancellationToken.WaitHandle.WaitOne(2000);

                //    if (_MQSession.SessionMode != SessionModeEnum.BufferOnly)
                //        if (_channel == null || !_channel!.IsOpen)
                //        {
                //            Log.Error("MQ channel is closed.");
                //            break;
                //        }
                //}
                if (_MQSession.SessionMode != SessionModeEnum.WhileGet)
                  await TaskCompletionSourceWithCancelation(_cancellationToken);

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
        */
        public Task TaskCompletionSourceWithCancelation(CancellationToken cancellationToken) { 
            
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => tcs.SetResult(true), tcs);
            return tcs.Task;
        }
        public void CancelAll(bool IsUserFinished = false)
        {

            
            if(!cts.IsCancellationRequested)
                cts.Cancel();
            CleanProcess(IsUserFinished);
        }
        private void CleanProcess(bool IsUserFinished = false)
        {

            if (_channel != null)
            {
                _channel!.CloseAsync();
                
            }
            if (_MQSession != null)
            {
                _MQSession!.FinishSessionProcessing( _errorMessage, IsUserFinished);

                _MQSession.CleanProcess();
            }

        }
    }
}
