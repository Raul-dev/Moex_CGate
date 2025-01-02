using MQ.dal;
using Serilog;
using MQ.dal.Models;
using Microsoft.IdentityModel.Tokens;
using MQ.bll.Common;

namespace MQ.bll
{

    public class MQMessagePropertyKey
    {
        public string MessagePropertyKey;
        public string TableName;
        public string ProcessQuery;
        public IQueueChannel? MQChanel { get; set; }
        protected BllOption option;
        protected long sessionId=0;
        
        protected MongoHelper? mongoHelper;
        private Thread? _loadThread;
        private CancellationToken _cancellationToken;
        private long _incomingMessagesCounter = 0; // DB saved msg for trigger load procedures
        private object _incomingMessagesCounterLock = new();
        private int _messageCurentQueue = 0;
        private object _messageCurentQueueLock = new();

        //private Queue<MessageBuffer>[] _messageBufferQueue = new Queue<MessageBuffer>[2];
        private Queue<object>[] _messageBufferQueue = new Queue<object>[2];
        private Queue<ulong>[] _messageOffsetIdQueue = new Queue<ulong>[2];

        public void IncreaseIncomingMessagesCounter()
        {
            lock (_incomingMessagesCounterLock)
            {
                _incomingMessagesCounter++;
            }
        }
        public void ResetIncomingMessagesCounter()
        {
            lock (_incomingMessagesCounterLock)
            {
                _incomingMessagesCounter = 0;
            }
        }
        public long GetIncomingMessagesCounter()
        {
            long res;
            lock (_incomingMessagesCounterLock)
            {
                res = _incomingMessagesCounter;
            }
            return res;
        }
        public MQMessagePropertyKey(BllOption option, string messagePropertyKey, string tableName, string processQuery, long sessionid, CancellationToken cancellationToken)
        {
            this._cancellationToken = cancellationToken;
            this.option = option;
            MessagePropertyKey = messagePropertyKey;
            TableName = tableName;
            sessionId = sessionid;
            ProcessQuery = processQuery;

            _messageBufferQueue[0] = new Queue<object>();
            _messageBufferQueue[1] = new Queue<object>();
            _messageOffsetIdQueue[0] = new Queue<ulong>();
            _messageOffsetIdQueue[1] = new Queue<ulong>();
        }

         public void CleanProcess()
        {
            Log.Information($@"Abort thread {ProcessQuery}");
            if (_loadThread != null)
                if (_loadThread.IsAlive)
                {
                    _loadThread.Abort();
                    /*
                    if(!cancellationToken.IsCancellationRequested )
                        CancellationTokenSource.CreateLinkedTokenSource(cancellationToken).Cancel();
                    while (_loadThread.IsAlive)
                    {
                        Task.Delay(100);
                        Log.Information($@"Wait thread finished {ProcessQuery}");
                    }
                    */
                }
        }

        public void StartEtlThread(object? sender)
        {
            if (_loadThread == null)
            {
                Thread thread = new Thread(EtlThread);
                thread.Name = MessagePropertyKey;
                if (MessagePropertyKey != "Unknown" &&
                    MessagePropertyKey != "Справочник.адаптер_СхемыДанных")
                {
                    thread.Start(_cancellationToken);
                    _loadThread = thread;
                }
            }
            else
            {
                if (!_loadThread.IsAlive)
                {

                    Thread thread = new Thread(EtlThread);
                    thread.Name = MessagePropertyKey;
                    thread.Start(_cancellationToken);
                    _loadThread = thread;
                }

            }
        }
        public void EtlThread(object? sender)
        {
            if (sender is null)
                return;
            CancellationToken token = (CancellationToken)sender;
            long bufferId = 0;
            long oldBufferId = -1; // Start procedures on the start
            int hash = Thread.CurrentThread.GetHashCode();
            Log.Debug("Start Load Thread name: {0}, hash: {1}", MessagePropertyKey, hash);
            int i = 0;
            long cnt = 0;
            string errorMessage;
            DBHelper? dbHelper = null;
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
                    Log.Debug("Thread hash {0}, In iteration {1}, cancellation has been requested...", hash, i);
                    break;
                }
                
                try
                {
                    if (dbHelper == null)
                        dbHelper = new DBHelper(option.ServerName, option.DatabaseName, option.Port, option.ServerType, option.User, option.Password);

                    if (mongoHelper == null && option.MongoEnable)
                        mongoHelper = new MongoHelper(option.MongoUrl, option.MongoUser, option.MongoPassword, option.MongoDatabase);

                    
                    if (option.MongoEnable && mongoHelper != null)
                        cnt = mongoHelper.SaveCollectionToDB(sessionId, MessagePropertyKey, dbHelper, TableName, ProcessQuery, token);
                    else
                        if (cnt != 200000 && ProcessQuery.IsNullOrEmpty() == false)
                          cnt = GetIncomingMessagesCounter();
                    
                    if ((oldBufferId == -1 || cnt > 0)  && ProcessQuery.IsNullOrEmpty() == false)
                    {
                        
                        DateTime dt = DateTime.Now;
                        ResetIncomingMessagesCounter();
                        cnt = dbHelper.EtlLoadProcess(sessionId, ProcessQuery, oldBufferId, out errorMessage, out bufferId);
                        
                        TimeSpan ts = DateTime.Now - dt;
                        if (!errorMessage.IsNullOrEmpty())
                        {
                            Log.Error("Call {0}; count={1}, Error: {2}", ProcessQuery, cnt, errorMessage);
                        }
                        else
                            Log.Information("Call {0}; count={1}; ms={2}; StartBufferId={3}", ProcessQuery, cnt, (int)ts.TotalMilliseconds, oldBufferId);
                        oldBufferId = (bufferId == -1) ? 0 : bufferId;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("EtlThread: {0}, Error: {1}.", MessagePropertyKey, ex.Message);

                }
                if (cnt != 200000) // TOP 200000 , нужно повторить процедуру без паузы
                    token.WaitHandle.WaitOne(1000);
                i++;
            }
        }

        //public async Task SendMsgToLocalQueue(ulong offsetId, IReadOnlyBasicProperties basicProperties, ReadOnlyMemory<byte> body)
        public async Task SendMsgToLocalQueue(ulong offsetId, string messageId, string body)
        {

            var buff = (MessagePropertyKey == "Unknown") ? (object)new MsgQueue
            {
                SessionId = sessionId,
                MsgId = new Guid(messageId),
                Msg = body,
                //Encoding.UTF8.GetString(body.ToArray()),
                MsgKey = MessagePropertyKey,
                UpdateDate = DateTime.Now
            } :
            (object) new OrdersLogBuffer
            {
                SessionId = sessionId,
                MsgId = new Guid(messageId),
                Msg = body,
                //Encoding.UTF8.GetString(body.ToArray()),
                MsgTypeId = 1,
                IsError = false,
                CreateDate = DateTime.Now,
                UpdateDate = new DateTime(1900, 1, 1)
            };

            lock (_messageCurentQueueLock)
            {
                _messageBufferQueue[_messageCurentQueue].Enqueue(buff);
                _messageOffsetIdQueue[_messageCurentQueue].Enqueue(offsetId);
            }
            
        }

        public void SaveMsgToDataBaseBulk(DBHelper dbHelper)
        {
            int cnt = _messageBufferQueue[_messageCurentQueue].Count;
            if (cnt == 0)
                return;
            int prevMessageCurentList = 0;
            lock (_messageCurentQueueLock)
            {
                prevMessageCurentList = _messageCurentQueue;
                if (prevMessageCurentList == 0)
                    _messageCurentQueue = 1;
                if (prevMessageCurentList == 1)
                    _messageCurentQueue = 0;
            }
   
            var buff = _messageBufferQueue[prevMessageCurentList].ToArray();
            bool res = dbHelper.EfBulkInsertBufferAsync(MessagePropertyKey, buff).Result;

            _messageBufferQueue[prevMessageCurentList].Clear();
            if(res)
                IncreaseIncomingMessagesCounter();
            
            foreach (var offsetId in _messageOffsetIdQueue[prevMessageCurentList]){
                if (MQChanel != null && MQChanel.IsOpen)
                {
                    //Log.Debug("Confirm message Tag: {0} ,prevMessageCurentList {1}", offsetId, prevMessageCurentList);
                    if(res)
                        MQChanel.AcknowledgeMessageAsync(offsetId).Wait();
                    else
                        MQChanel.RejectMessageAsync(offsetId).Wait();   
                }
            }
            if (MQChanel != null && MQChanel.IsOpen)
                _messageOffsetIdQueue[prevMessageCurentList].Clear();
            if (!res) //Сбрасываем все сообщения назад в очередь
            {
                lock (_messageCurentQueueLock)
                {
                    foreach (var offsetId in _messageOffsetIdQueue[_messageCurentQueue])
                    {
                        if (MQChanel != null && MQChanel.IsOpen)
                        {
                            MQChanel.RejectMessageAsync(offsetId).Wait();
                        }
                    }
                    _messageBufferQueue[_messageCurentQueue].Clear();
                    _messageOffsetIdQueue[_messageCurentQueue].Clear();

                }
            }


        }

    }

}
