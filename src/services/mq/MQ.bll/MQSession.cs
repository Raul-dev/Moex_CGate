﻿using Serilog;
using MQ.dal;
using MQ.dal.Models;
using MQ.bll.Common;

using Microsoft.IdentityModel.Tokens;

namespace MQ.bll
{
    enum SessionState
    {
        StartedSession = 1,
        FinishedSession = 2,
        ErrorSession = 3
    }
    public enum SessionModeEnum
    {
        FullMode = 1,
        BufferOnly = 2,
        EtlOnly = 3,
        WhileGet = 4,
    }
    public class MQSession
    {
        BllOption option;
        CancellationToken _cancellationToken;
        public SessionModeEnum SessionMode;
        public long SessionId = 0;
        public Dictionary<string, MQMessagePropertyKey> MQMessagePropertyKeyList;
        DBHelper dbHelper;
        MongoHelper? mongoHelper;

        public MQSession(BllOption option, CancellationToken cancellationToken)
        {
            SessionMode = option.SessionMode;
            this._cancellationToken = cancellationToken;
            this.option = option;
            dbHelper = new DBHelper(option.ServerName, option.DatabaseName, option.Port, option.ServerType, option.User, option.Password);
            MQMessagePropertyKeyList = new Dictionary<string, MQMessagePropertyKey>();
            if (option.MongoEnable)
                mongoHelper = new MongoHelper(option.MongoUrl, option.MongoUser, option.MongoPassword, option.MongoDatabase);
        }

        public long StartSessionProcessing()
        {
            SessionId = SaveSessionState(SessionState.StartedSession);
            int res = InitMessagePropertyKeyList();
            if (res != 0)
                return -1;
            Log.Information($@"Start mq Session Id = {SessionId}");
            return SessionId;
        }
        public int FinishSessionProcessing(string errormsg = "", bool IsUserFinished = false)
        {
            try
            {
                if (IsUserFinished || errormsg.Length == 0)
                {
                    Log.Debug($@"Finish Session processing {IsUserFinished} {errormsg.Length.ToString()}  Session Id = {SessionId}");
                    SaveSessionState(SessionState.FinishedSession, SessionId, 1, errormsg);
                    Log.Information($@"Finished Session processing Session Id = {SessionId}");
                }
                else
                {
                    Log.Debug($@"Finish Session processing {IsUserFinished} {errormsg.Length.ToString()}  Session Id = {SessionId}");
                    SaveSessionState(SessionState.ErrorSession, SessionId, 1, errormsg);
                    Log.Information($@"Finished Session processing Session Id = {SessionId} with error.");
                }
            }
            catch (Exception ex)
            {
                Log.Error("Finish Session processing caused an exception:");
                Log.Error(ex.Message);
            }
            return 0;
        }

        public void CleanProcess()
        {

            foreach (KeyValuePair<string, MQMessagePropertyKey> kvp in MQMessagePropertyKeyList)
            {
                kvp.Value.CleanProcess();
            }

            MQMessagePropertyKeyList.Clear();
        }

        public MQMessagePropertyKey? GetMQMessagePropertyKey(string messagePropertyKey = "")
        {
            MQMessagePropertyKey? ms = null;
            bool keyres = MQMessagePropertyKeyList.TryGetValue(messagePropertyKey, out ms);
            if (!keyres)
            {
                MQMessagePropertyKeyList.TryGetValue("Unknown", out ms);
            }
            return ms;
        }
        public string GetTableName(string messagePropertyKey = "")
        {

            MQMessagePropertyKey? ms = null;
            if (messagePropertyKey == null)
                return "msgqueue";
            if (messagePropertyKey.Length != 0 && messagePropertyKey != "All")
            {
                ms = GetMQMessagePropertyKey(messagePropertyKey);
            }
            if (ms == null) throw new Exception(@$"messagePropertyKey = {messagePropertyKey} Not found.");
            return ms.TableName;
        }
        public string? GetProcessQuery(string messagePropertyKey = "")
        {

            MQMessagePropertyKey? ms = null;
            if (messagePropertyKey == null)
                return null;
            if (messagePropertyKey.Length != 0 && messagePropertyKey != "All")
            {
                ms = GetMQMessagePropertyKey(messagePropertyKey);
            }
            if (ms == null) throw new Exception("Empty QueueList");
            return ms.ProcessQuery;
        }

        public void RunEtlLoadProcedure(string messagePropertyKey = "")
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                return;
            }
            try
            {

                long cnt;
                string errorMessage;
                long oldBufferId = 0, bufferId = 0;
                TimeSpan ts;
                DateTime dt;
                if (messagePropertyKey.Length != 0 && messagePropertyKey != "All")
                {
                    MQMessagePropertyKey? ms = GetMQMessagePropertyKey(messagePropertyKey);
                    if (ms == null) throw new Exception(@$"Not found messagePropertyKey = {messagePropertyKey}.");
                    dt = DateTime.Now;
                    cnt = dbHelper.EtlLoadProcess(SessionId, ms.ProcessQuery, oldBufferId, out errorMessage, out bufferId);
                    ts = DateTime.Now - dt;
                    if (!errorMessage.IsNullOrEmpty())
                    {
                        Log.Error("Call {0}; count= {1}, Error: {2}", ms.ProcessQuery, cnt, errorMessage);
                    }
                    else
                        Log.Information("Call {0}; count={1} ms={2}", ms.ProcessQuery, cnt, (int)ts.TotalMilliseconds);
                }
                else
                {
                    foreach (KeyValuePair<string, MQMessagePropertyKey> kvp in MQMessagePropertyKeyList)
                    {
                        if (kvp.Value.ProcessQuery.IsNullOrEmpty())
                            continue;
                        dt = DateTime.Now;
                        oldBufferId = 0;
                        cnt = dbHelper.EtlLoadProcess(SessionId, kvp.Value.ProcessQuery, oldBufferId, out errorMessage, out bufferId);
                        ts = DateTime.Now - dt;
                        if (!errorMessage.IsNullOrEmpty())
                        {
                            Log.Error("Call {0}; count= {1}, Error: {2}", kvp.Value.ProcessQuery, cnt, errorMessage);
                        }
                        else
                            Log.Information("Call {0}; count={1} ms={2}", kvp.Value.ProcessQuery, cnt, (int)ts.TotalMilliseconds);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($@"RunEtlLoadProcedure caused an exception. messagePropertyKey = {(messagePropertyKey.Length == 0 ? "All" : messagePropertyKey)}");
                Log.Error(ex.Message);
                throw new Exception($@"RunEtlLoadProcedure caused an exception. messagePropertyKey = {(messagePropertyKey.Length == 0 ? "All" : messagePropertyKey)}. {ex.Message}");
            }
        }
        public void RunEtlThread(string messagePropertyKey = "")
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                return;
            }
            try
            {

                if (messagePropertyKey.Length != 0 && messagePropertyKey != "All")
                {
                    MQMessagePropertyKey? ms = GetMQMessagePropertyKey(messagePropertyKey);
                    if (ms == null) throw new Exception(@$"Not found messagePropertyKey = {messagePropertyKey}.");
                    ms.StartEtlThread(_cancellationToken);
                }
                else
                {
                    foreach (KeyValuePair<string, MQMessagePropertyKey> kvp in MQMessagePropertyKeyList)
                    {
                        kvp.Value.StartEtlThread(_cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($@"RunEtlThread caused an exception. messagePropertyKey = {(messagePropertyKey.Length == 0 ? "All" : messagePropertyKey)}");
                Log.Error(ex.Message);
                throw new Exception($@"RunEtlThread caused an exception. messagePropertyKey = {(messagePropertyKey.Length == 0 ? "All" : messagePropertyKey)}. {ex.Message}");
            }
        }
        public long GetSessionId()
        {
            return SessionId;
        }

        public int InitMessagePropertyKeyList()
        {
            try
            {

                List<Metamap> mms = dbHelper.GetMappingSetup();
                //.Where<MsgMappingSetup>(c => (c.TableName.Contains("msgqueue") || c.TableName.Contains("TABLE_REPL")))
                foreach (Metamap m in mms)
                {
                    MQMessagePropertyKey ms = new MQMessagePropertyKey(option, m.MsgKey, m.TableName, m.EtlQuery ?? "", SessionId, _cancellationToken);
                    MQMessagePropertyKeyList.Add(m.MsgKey, ms);
                }

                return 0;
            }
            catch (Exception ex)
            {
                Log.Error("GetMappingSetup caused an exception:");
                Log.Error(ex.Message);
                SaveSessionState(SessionState.ErrorSession, SessionId, 1, ex.Message);
                return -1;
            }
        }

        long SaveSessionState(SessionState stateID, long? sessionid = null, int datasourceid = 1, string errormsg = "")
        {
            return dbHelper.SaveSessionState((int)stateID, sessionid, datasourceid, errormsg);
        }

        public void SetChannel(IQueueChannel channel)
        {
            foreach (KeyValuePair<string, MQMessagePropertyKey> kvp in MQMessagePropertyKeyList)
            {
                kvp.Value.MQChanel = channel;
            }
        }
/*
        public async Task SaveMsgToDataBaseAsync(IReadOnlyBasicProperties basicProperties, ReadOnlyMemory<byte> body)
        {

            int messageTypeId = 1; //Тестируем  Bulk запись, парсинг процедурой load_orders_log
            messageTypeId = 2; //Тестируем Array парсинг процедурой load_orders_log_array
            
            //Log.Debug($@"sessionId {sessionId}, basicProperties.Type {basicProperties.Type}, Table {GetTableName(basicProperties.Type)}, messageTypeId {messageTypeId.ToString()} .");

            MQMessagePropertyKey? ms = GetMQMessagePropertyKey(basicProperties.Type?? "Unknown");
            if (ms == null)
                return;
#pragma warning disable CS8604 // Possible null reference argument.
            if (messageTypeId == 1)
            {
                string str = Encoding.UTF8.GetString(body.ToArray());

                await dbHelper.EfBulkInsertAsync(str, sessionId, new Guid(basicProperties.MessageId) );

                ms.IncreaseIncomingMessagesCounter();
            }
            if (messageTypeId == 2)
            {
                //dbHelper.SaveMsgToDataBase(sessionId, GetTableName(basicProperties.Type) ?? "msgqueue", basicProperties.MessageId, Encoding.UTF8.GetString(body.ToArray()), basicProperties.Type, messageTypeId);
                //Async метод медленнее на 20%
                Task task = dbHelper.SaveMsgToDataBaseAsync(sessionId, GetTableName(basicProperties.Type) ?? "msgqueue", basicProperties.MessageId, Encoding.UTF8.GetString(body.ToArray()), basicProperties.Type, messageTypeId);
                //todo
                ms.IncreaseIncomingMessagesCounter();
            }
#pragma warning restore CS8604 // Possible null reference argument.

        }
        
        public void SaveMsgToDataBase(IReadOnlyBasicProperties basicProperties, ReadOnlyMemory<byte> body) {
            SaveMsgToDataBase( basicProperties.MessageId, Encoding.UTF8.GetString(body.ToArray()), basicProperties.Type);
        }
*/
        public void SaveMsgToDataBase( string messageId, string body, string messageKey)
        {
            string tableName = GetTableName(messageKey) ?? "msgqueue";
            int messageTypeId = 1; //Тестируем  Bulk запись, парсинг процедурой load_orders_log
            messageTypeId = 2; //Тестируем Array парсинг процедурой load_orders_log_array

            //Log.Debug($@"sessionId {sessionId}, basicProperties.Type {basicProperties.Type}, Table {GetTableName(basicProperties.Type)}, messageTypeId {messageTypeId.ToString()} .");

            MQMessagePropertyKey? ms = GetMQMessagePropertyKey(messageKey ?? "Unknown");
            
            if (ms == null)
                return;
#pragma warning disable CS8604 // Possible null reference argument.
            if (messageTypeId == 1)
            {
                dbHelper.EfBulkInsertAsync(body, SessionId, new Guid(messageId)).RunSynchronously();
                ms.IncreaseIncomingMessagesCounter();
            }
            if (messageTypeId == 2)
            {
                dbHelper.SaveMsgToDataBase(SessionId, GetTableName(messageKey) ?? "msgqueue", messageId, body, messageKey, messageTypeId);
                ms.IncreaseIncomingMessagesCounter();
            }
#pragma warning restore CS8604 // Possible null reference argument.

        }

        public async Task SendMsgToLocalQueue(ulong offsetId, string messageId, string body, string messagePropertyKey)
        {
            MQMessagePropertyKey? ms = GetMQMessagePropertyKey(messagePropertyKey ?? "Unknown") ;

            if (ms == null)
                return;
            await ms.SendMsgToLocalQueue(offsetId, messageId, body);
        }
        public void SaveMsgToDataBaseBulk()
        {
            foreach (KeyValuePair<string, MQMessagePropertyKey> kvp in MQMessagePropertyKeyList)
            {
                kvp.Value.SaveMsgToDataBaseBulk(dbHelper);
 
            }
        }

    }
}
