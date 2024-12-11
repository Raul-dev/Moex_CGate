using MQ.dal;
using RabbitMQ.Client;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using MQ.dal.Models;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel;
using System.Threading;
using static System.Runtime.InteropServices.JavaScript.JSType;
using MQ.bll.Common;

namespace MQ.bll
{

    public class MQMessagePropertyKey
    {

        CancellationToken cancellationToken;
        public string MessagePropertyKey;
        public string TableName;
        public string ProcessQuery;
        long sessionId;
        DBHelper dbHelper;
        MongoHelper mongoHelper;
        Thread loadThread;
        BllOption option;
        private object _incomingMessagesCounterLock = new();
        private long _incomingMessagesCounter = 0;
        //messagereservedCollection

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
            this.cancellationToken = cancellationToken;
            this.option = option;
            MessagePropertyKey = messagePropertyKey;
            TableName = tableName;
            sessionId = sessionid;
            ProcessQuery = processQuery;


            // ThreadPool.QueueUserWorkItem(new WaitCallback(kvp.Value.EtlThread), cancellationToken.Token);
        }

        /*
        private void SaveMsgToDataBase(Msg m)
        {
            Msgqueue mu = new Msgqueue { SessionId = sessionId, MsgId = new Guid(m.message.BasicProperties.MessageId),  Msg = Encoding.UTF8.GetString(m.message.Body.ToArray()), MsgKey = m.message.BasicProperties.Type, UpdateDate = m.RecivedDate };

            dbHelper.SaveMsgToDataBase(MessagePropertyKey, TableName, mu);
 
        }
        */
        public void CleanProcess()
        {
            Log.Information($@"Abort thread {ProcessQuery}");
            if (loadThread != null)
                if (loadThread.IsAlive)
                    loadThread.Abort();
            //cancellationToken.Cancel();
        }

        public void StartEtlThread(object? sender)
        {
            if (loadThread == null)
            {
                Thread thread = new Thread(EtlThread);
                thread.Name = MessagePropertyKey;
                if (MessagePropertyKey != "Unknown" &&
                    MessagePropertyKey != "Справочник.адаптер_СхемыДанных")
                {
                    thread.Start(cancellationToken);
                    loadThread = thread;
                }
            }
            else
            {
                if (!loadThread.IsAlive)
                {

                    Thread thread = new Thread(EtlThread);
                    thread.Name = MessagePropertyKey;
                    thread.Start(cancellationToken);
                    loadThread = thread;
                }

            }
        }
        public void EtlThread(object? sender)
        {
            if (sender is null)
                return;
            CancellationToken token = (CancellationToken)sender;
            int hash = Thread.CurrentThread.GetHashCode();
            Log.Debug("Start Thread name {0} hash {1}", MessagePropertyKey, hash);
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
                    Log.Debug("Thread hash {0}, In iteration {1}, cancellation has been requested...", hash, i);
                    break;
                }

                try
                {
                    if (dbHelper == null)
                        dbHelper = new DBHelper(option.ServerName, option.DatabaseName, option.Port, option.ServerType, option.User, option.Password);

                    if (mongoHelper == null && option.MongoEnable)
                        mongoHelper = new MongoHelper(option.MongoUrl, option.MongoUser, option.MongoPassword, option.MongoDatabase);

                    long cnt = 0;
                    if (option.MongoEnable && mongoHelper != null)
                        cnt = mongoHelper.SaveCollectionToDB(sessionId, MessagePropertyKey, dbHelper, TableName, ProcessQuery, token);
                    else
                        if (ProcessQuery.IsNullOrEmpty() == false)
                          cnt = GetIncomingMessagesCounter();

                    if (cnt > 0 && ProcessQuery.IsNullOrEmpty() == false)
                    {
                        string errorMessage;
                        DateTime dt = DateTime.Now;
                        ResetIncomingMessagesCounter();
                        cnt = dbHelper.EtlLoadProcess(sessionId, ProcessQuery, out errorMessage);
                        TimeSpan ts = DateTime.Now - dt;
                        if (!errorMessage.IsNullOrEmpty())
                        {
                            Log.Error("Call {0}; count={1}, Error: {2}", ProcessQuery, cnt, errorMessage);
                        }
                        else
                            Log.Information("Call {0}; count={1} ms={2}", ProcessQuery, cnt, (int)ts.TotalMilliseconds);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("{0} thread error: {1}.", MessagePropertyKey, ex.Message);

                    dbHelper = null;

                    mongoHelper = null;
                }
    
                token.WaitHandle.WaitOne(1000);
                i++;
            }
        }
    }

}
