using Azure;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using MQ.bll.Common;
using MQ.dal;
using MQ.dal.Models;
using Serilog;

namespace MQ.bll
{
    enum SessionState
    {
        StartedSession = 1,
        FinishedSession = 2,
        ErrorSession = 3
    }
    //public enum SessionModeEnum
    //{
    //    FullMode = 1,
    //    BufferOnly = 2,
    //    EtlOnly = 3,
    //    WhileGet = 4,
    //}
    public class MQSession
    {
        BllOption _option;
        CancellationToken _cancellationToken;
        public SessionModeEnum SessionMode;
        public long SessionId = 0;
        public Dictionary<string, MQMessagePropertyKey> MQMessagePropertyKeyList;
        DBHelper dbHelper;
        MongoHelper? mongoHelper;

        public MQSession(BllOption option, CancellationToken cancellationToken)
        {
            this._option = option;
            SessionMode = _option.DataBaseServSettings.SessionMode;
            this._cancellationToken = cancellationToken;
            
            dbHelper = new DBHelper(_option.DataBaseServSettings?.ServerName ?? "", _option.DataBaseServSettings?.DataBase ?? "", _option.DataBaseServSettings?.Port ?? 0, _option.ServerType, _option.DataBaseServSettings?.User ?? "", _option.DataBaseServSettings?.Password ?? "");
            MQMessagePropertyKeyList = new Dictionary<string, MQMessagePropertyKey>();
            if (option.MongoEnable)
                mongoHelper = new MongoHelper(option.MongoServSettings?.Url ?? "", option.MongoServSettings?.User ?? "", option.MongoServSettings?.Password ?? "", option.MongoServSettings?.DataBase ?? "");
        }

        public long StartSessionProcessing()
        {
            SessionId = SaveSessionState(SessionState.StartedSession, null, _option.DataBaseServSettings.DataSourceID);
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
                    SaveSessionState(SessionState.FinishedSession, SessionId, _option.DataBaseServSettings.DataSourceID, errormsg);
                    Log.Information($@"Finished Session processing Session Id = {SessionId}");
                }
                else
                {
                    Log.Debug($@"Finish Session processing {IsUserFinished} {errormsg.Length.ToString()}  Session Id = {SessionId}");
                    SaveSessionState(SessionState.FinishedSession, SessionId, _option.DataBaseServSettings.DataSourceID, errormsg);
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

        public MQMessagePropertyKey? GetMQMessagePropertyKey(string? messagePropertyKey )
        {
            MQMessagePropertyKey? ms = null;
            bool keyres = false;
            if (!string.IsNullOrEmpty(messagePropertyKey))
            {
                keyres = MQMessagePropertyKeyList.TryGetValue(messagePropertyKey, out ms);
            }
            
            if (!keyres)
            {
                MQMessagePropertyKeyList.TryGetValue("Unknown", out ms);
            }
            return ms;
        }
        public string GetTableName(string? messagePropertyKey)
        {

            MQMessagePropertyKey? ms = null;

            ms = GetMQMessagePropertyKey(messagePropertyKey);
            
            if (ms == null) throw new Exception( string.Format("GetTableName messagePropertyKey = {0} Not found. Должен быть настроен UNKNOWN для очереди DataSourceID={1}, Objects Count ={2}", messagePropertyKey , _option.DataBaseServSettings.DataSourceID, MQMessagePropertyKeyList.Count) );
            return ms.TableName;
        }
        public string? GetProcessQuery(string? messagePropertyKey )
        {

            MQMessagePropertyKey? ms = null;
    
            if (messagePropertyKey != "All")
            {
                ms = GetMQMessagePropertyKey(messagePropertyKey);
            }
            if (ms == null) throw new Exception(string.Format("GetProcessQuery messagePropertyKey = {0} Not found. Должен быть настроен UNKNOWN для очереди DataSourceID={1}, Objects Count ={2}", messagePropertyKey, _option.DataBaseServSettings.DataSourceID, MQMessagePropertyKeyList.Count));
            return ms.ProcessQuery;
        }

        public void RunEtlLoadProcedure(string? messagePropertyKey)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                return;
            }
            string processQuery = "";
            try
            {

                long cnt;
                string errorMessage;
                
                long oldBufferId = 0, bufferId = 0;
                TimeSpan ts;
                DateTime dt;
                if (messagePropertyKey != "All")
                {
                    MQMessagePropertyKey? ms = GetMQMessagePropertyKey(messagePropertyKey);
                    if (ms == null) throw new Exception(string.Format("RunEtlLoadProcedure messagePropertyKey = {0} Not found. Должен быть настроен UNKNOWN для очереди DataSourceID={1}, Objects Count ={2}", messagePropertyKey, _option.DataBaseServSettings.DataSourceID, MQMessagePropertyKeyList.Count));
                    dt = DateTime.Now;
                    processQuery = ms.ProcessQuery;
                    cnt = dbHelper.EtlLoadProcess(SessionId, ms.ProcessQuery, oldBufferId, out errorMessage, out bufferId, _cancellationToken);
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

                        if (_cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                        if (kvp.Value.ProcessQuery.IsNullOrEmpty())
                            continue;
                        dt = DateTime.Now;
                        oldBufferId = 0;
                        processQuery = kvp.Value.ProcessQuery;
                        cnt = dbHelper.EtlLoadProcess(SessionId, kvp.Value.ProcessQuery, oldBufferId, out errorMessage, out bufferId, _cancellationToken);
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
               
                Log.Error($@"RunEtlLoadProcedure caused an exception. messagePropertyKey = {messagePropertyKey}");
                Log.Error(ex.Message);
                SaveSessionState(SessionState.ErrorSession, SessionId, _option.DataBaseServSettings.DataSourceID, string.Format("{0}, Error: {1}", processQuery, ex.Message));
                throw new Exception($@"RunEtlLoadProcedure caused an exception.messagePropertyKey = {messagePropertyKey}, {ex.Message}");
            }
        }
        public void RunEtlThread(string? messagePropertyKey)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                return;
            }
            try
            {

                if (messagePropertyKey != "All")
                {
                    MQMessagePropertyKey? ms = GetMQMessagePropertyKey(messagePropertyKey);
                    if (ms != null)
                    {
                        ms.StartEtlThread(_cancellationToken);
                    }else
                    {
                        Log.Debug("Empty StartEtlThread List");
                    }
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

                List<Metamap> mms = dbHelper.GetMappingSetup(_option.DataBaseServSettings.MetaAdapterId);
                //.Where<MsgMappingSetup>(c => (c.TableName.Contains("msgqueue") || c.TableName.Contains("TABLE_REPL")))
                Log.Debug("Для адаптера MetaAdapterId={0}, в базе [{1}]. Будет настроенно Count={2} MessagePropertyKeyList", _option.DataBaseServSettings.MetaAdapterId, _option.DataBaseServSettings.DataBase, mms.Count);
                foreach (Metamap m in mms)
                {
                    MQMessagePropertyKey ms = new MQMessagePropertyKey(_option, m.MsgKey, m.TableName, m.EtlQuery ?? "", SessionId, _cancellationToken);
                    MQMessagePropertyKeyList.Add(m.MsgKey, ms);
                }
                if ((mms.Count > 0 && !MQMessagePropertyKeyList.ContainsKey("Unknown")) || MQMessagePropertyKeyList.Count == 0)
                    throw new Exception(string.Format("Отсутствует Unknown Message Key для адаптера MetaAdapterId {0}", _option.DataBaseServSettings.MetaAdapterId));
                return 0;
            }
            catch (Exception ex)
            {
                Log.Error("GetMappingSetup caused an exception:");
                Log.Error(ex.Message);
                SaveSessionState(SessionState.ErrorSession, SessionId, _option.DataBaseServSettings.DataSourceID, ex.Message);
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

        public async Task SaveMsgToDataBaseAsync(string messageId, string body, string? messageKey, CancellationToken cancellationToken)
        {
            string tableName = GetTableName(messageKey);
            int messageTypeId = 1; //Тестируем  Bulk запись, парсинг процедурой load_orders_log
            messageTypeId = 2; //Тестируем Array парсинг процедурой load_orders_log_array

            //Log.Debug($@"sessionId {sessionId}, basicProperties.Type {basicProperties.Type}, Table {GetTableName(basicProperties.Type)}, messageTypeId {messageTypeId.ToString()} .");

            MQMessagePropertyKey? ms = GetMQMessagePropertyKey(messageKey );

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
                await dbHelper.SaveMsgToDataBaseAsync(SessionId, GetTableName(messageKey), messageId, body, messageKey, messageTypeId, cancellationToken);
                ms.IncreaseIncomingMessagesCounter();
            }
#pragma warning restore CS8604 // Possible null reference argument.

        }
        public void SaveMsgToDataBase( string messageId, string body, string messageKey)
        {
            string tableName = GetTableName(messageKey);
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
                dbHelper.SaveMsgToDataBase(SessionId, GetTableName(messageKey), messageId, body, messageKey, messageTypeId);
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
