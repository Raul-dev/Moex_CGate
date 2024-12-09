using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using MQ.dal.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Data.SqlClient;
using RabbitMQ.Client;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Npgsql;
using System.Data;
using Microsoft.Data.Sqlite;
using ClickHouse.EntityFrameworkCore.Extensions;
using ClickHouse.Client.ADO.Parameters;
using System.Reflection.Metadata;
using static System.Net.Mime.MediaTypeNames;
using System.Runtime.CompilerServices;
using static MQ.dal.DBHelper;
using static MongoDB.Driver.WriteConcern;
using MongoDB.Bson;
using Microsoft.VisualBasic;
using static Azure.Core.HttpHeader;
using static System.Net.WebRequestMethods;
using Mono.TextTemplating;
using System.ComponentModel;
using System.Reflection;
using System.Security.AccessControl;
using Microsoft.IdentityModel.Tokens;
//using Amazon.Auth.AccessControlPolicy;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using NodaTime.Text;
using System.Collections;
using Humanizer.Configuration;
using NodaTime;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Options;
using EFCore.BulkExtensions;
//using ClickHouse.Client.ADO.Parameters;

namespace MQ.dal
{
    public class MetaDataIntegrity
    {
        public string? MetadataVer { set; get; }
        public string? MetamapVer { set; get; }
        public string? TableName { set; get; }
    }


    public class DBHelper
    {
        MetastorageContext MetastorageDbContext;
        SqlServerType ServerType;
        DbContextOptionsBuilder<MetastorageContext> OptionsBuilder;
        public DBHelper(string server, string databasename, int port = 1433, SqlServerType type = SqlServerType.mssql, string user = "", string pwd = "")
        {
           ServerType = type;
           var optionsBuilder = new DbContextOptionsBuilder<MetastorageContext>();
            if (type == SqlServerType.psql)
                optionsBuilder.UseNpgsql(@$"Host={server};Port={port};Database={databasename};Username={user};Password={pwd};");
            else if (type == SqlServerType.sqlite)
                optionsBuilder.UseSqlite(@$"Data Source={databasename}");
            else if (type == SqlServerType.mssql)
            {
                if( user.IsNullOrEmpty() )
                {
                    optionsBuilder.UseSqlServer(@$"Server = {server}; Database = {databasename}; Trusted_Connection = True; MultipleActiveResultSets = true; TrustServerCertificate = true; Encrypt = False");
                } else
                {
                    optionsBuilder.UseSqlServer(@$"Server = {server}; Database = {databasename}; User = {user}; Password ={pwd}; MultipleActiveResultSets = true; TrustServerCertificate = true; Encrypt = False");
                }

                
            }
            else if (type == SqlServerType.clickhouse)
                optionsBuilder.UseClickHouse(@$"Host=localhost;Port=8123;Database=default;Username=admin;Password=admin;");
            else
                throw new Exception($"DBHelper not supported for {SqlServerTypeHelper.GetString(ServerType)}");
            OptionsBuilder = optionsBuilder;
            MetastorageDbContext = new MetastorageContext(optionsBuilder.Options);
#if (DEBUG)
            Test();
#endif
        }
        
        public void Test()
        {


            var v = from c in MetastorageDbContext.Metamaps
                    select c;
            foreach (var obj in v)
            {
                Console.WriteLine($" {obj.TableName}, {obj.Namespace} ");
                
            }
        }


        public class MsgQueueItem
        {
            public long MsgOrder { get; set; }
            public long SessionId { get; set; }
            public Guid? MsgId { get; set; }
            public string? Msg { get; set; }
            public string? MsgKey { get; set; }
            public DateTime UpdateDate { get; set; }
        }
        public List<MsgQueueItem> GetMsgqueueItems()
        {
            var v = from r in MetastorageDbContext.Msgqueues
                    orderby r.SessionId, r.BufferId
                    select new MsgQueueItem()
                    {
                        MsgOrder = r.BufferId,
                        SessionId = r.SessionId,
                        MsgId = r.MsgId,
                        Msg = r.Msg,
                        MsgKey = r.MsgKey,
                        UpdateDate = r.UpdateDate
                    };    

            return v.ToList();

        }

        public List<Metamap> GetMappingSetup()
        {

            var v = from c in MetastorageDbContext.Metamaps
                    where c.IsEnable == true
                    select new Metamap()
            {
                MetamapId = c.MetamapId,
                MsgKey = c.MsgKey,
                TableName = c.TableName,
                MetaAdapterId = c.MetaAdapterId,
                Namespace = c.Namespace,
                NamespaceVer = c.NamespaceVer,
                EtlQuery = c.EtlQuery,
                ImportQuery = c.ImportQuery,
                IsEnable = c.IsEnable
            };
            return v.ToList();
 
        }

        public class Session
        {
            public long session_id { get; set; }
        }
        public long SaveSessionState(int stateid =1, long? sessionid = null, int datasourceid = 1, string errormsg = "")
        {
            long id = 0;
            if (ServerType == SqlServerType.mssql)
            {
                var session_id = new SqlParameter("@session_id",System.Data.SqlDbType.BigInt);
                session_id.Value = (sessionid == null) ? DBNull.Value : (int)sessionid;
                var data_source_id = new SqlParameter("@data_source_id", datasourceid);
                var session_state_id = new SqlParameter("@session_state_id", stateid);
                var error_message = new SqlParameter("@error_message", errormsg);
                
                var res = MetastorageDbContext.Database.SqlQueryRaw<Int64>($"EXEC rb_SaveSessionState @session_id, @data_source_id, @session_state_id, @error_message", session_id, data_source_id, session_state_id, error_message).ToList();
                foreach (var s in res)
                {
                    sessionid = s;
                }
                if (sessionid != null)
                    id = (Int64)sessionid;

            }
            if (ServerType == SqlServerType.psql)
            {
                string cmd = "CALL public.\"rb_SaveSessionState\"( @par_session_id )";
                var par_session_id = new NpgsqlParameter("par_session_id", DbType.Int64);
                par_session_id.Direction = ParameterDirection.InputOutput;
                par_session_id.Value = DBNull.Value;
                var v = MetastorageDbContext.Database.ExecuteSqlRaw(cmd, par_session_id);
                id = (long)par_session_id.Value;
       
            }
          
            return id;
        }
        public long GetBufferCount(string tableName)
        {
            long cnt = 0;
            if (ServerType == SqlServerType.mssql)
            {
/*                SELECT TOP 1 row_count FROM sys.dm_db_partition_stats
                WHERE[object_id] = OBJECT_ID('[erp].[TABLE_REP_buffer]')
                ORDER BY index_id desc
*/
                string cmd = "";
                if (tableName.Contains("msgqueue"))
                    cmd = @$"SELECT @rescnt = ISNULL((SELECT top 1 CAST(1 AS BIGINT) FROM {tableName} ),0);";
                else
                    cmd = @$"SELECT @rescnt = ISNULL((SELECT top 1 CAST(1 AS BIGINT) FROM {tableName} WHERE is_error = 0),0);";

                var rescnt = new SqlParameter("@rescnt", cnt);
                rescnt.Direction = ParameterDirection.Output;
                MetastorageDbContext.Database.ExecuteSqlRaw(cmd, rescnt);
                cnt = ConvertFromDBVal<long>(rescnt) ;
            } else if (ServerType == SqlServerType.psql)
            {
                string cmd = "";
                if (tableName.Contains("msgqueue"))
                    cmd = @$"SELECT reltuples::bigint  FROM pg_class    
                        WHERE  oid = '{{tableName}}'::regclass;";
                else
                    cmd = @$"SELECT 1::bigint FROM {tableName} WHERE is_error = false LIMIT(1);";

                var v_rescnt = new NpgsqlParameter("v_rescnt", DbType.Int64);
                v_rescnt.Direction = ParameterDirection.Output;
                MetastorageDbContext.Database.ExecuteSqlRaw(cmd, v_rescnt);
                //var comps = MetastorageDbContext.Database.SqlQueryRaw<long>(cmd).ToList();
                
                cnt = ConvertFromDBValPl<long>(v_rescnt);
                //cnt = 0;
            } else
                throw new Exception($"GetBufferCount not supported for server type= {SqlServerTypeHelper.GetString(ServerType)}");
            return cnt;
        }
        public static T ConvertFromDBValPl<T>(object obj)
        {
            if (obj == null || ((NpgsqlParameter)obj).Value == null || ((NpgsqlParameter)obj).Value == DBNull.Value)
            {
                return default(T); // returns the default value for the type
            }
            else
            {
                return (T)((NpgsqlParameter)obj).Value;
            }
        }

        public static T ConvertFromDBVal<T>(object obj)
        {
            if (obj == null || ((SqlParameter)obj).Value == DBNull.Value)
            {
                return default(T); // returns the default value for the type
            }
            else
            {
                return (T)((SqlParameter)obj).Value;
            }
        }
        public int EtlLoadProcess(long sessionid, string processQuery, out string errorMessage)
        {
            int iRowCountInt = 0;
            errorMessage = "";
            if (processQuery.IsNullOrEmpty()) return 0;
            if (ServerType == SqlServerType.mssql)
            {
                string cmd = @$"EXEC {processQuery} @session_id = @session_id, @RowCount = @RowCount OUTPUT, @ErrorMessage = @ErrorMessage OUTPUT";
                var session_id = new SqlParameter("@session_id", sessionid);
                var rowCount = new SqlParameter("@RowCount", iRowCountInt);
                rowCount.Direction = ParameterDirection.Output;
                var errMessage = new SqlParameter("@ErrorMessage", SqlDbType.NVarChar, 4000);
                errMessage.Direction = ParameterDirection.Output;
                MetastorageDbContext.Database.ExecuteSqlRaw(cmd, session_id, rowCount, errMessage);
                //iRowCountInt = ConvertFromDBVal<int>(rowCount);
                //errorMessage = ConvertFromDBVal<string>(errMessage);
                iRowCountInt = (int)((rowCount == null || rowCount.Value == DBNull.Value) ? 0 : rowCount.Value);
                errorMessage = (string)((errMessage == null || errMessage.Value == DBNull.Value) ? "" : errMessage.Value);

            }
            else if (ServerType == SqlServerType.psql)
            {
                string cmd = @$"CALL {processQuery}( @par_session_id, @par_rowcount)";
                var par_session_id = new NpgsqlParameter("par_session_id", sessionid);
                
                var par_rowcount = new NpgsqlParameter("par_rowcount", iRowCountInt);
                par_rowcount.Direction = ParameterDirection.InputOutput;
                MetastorageDbContext.Database.ExecuteSqlRaw(cmd, par_session_id, par_rowcount);
                iRowCountInt = (int)par_rowcount.Value;
            }
            else
                throw new Exception($"SaveMsgToDataBase not supported for {SqlServerTypeHelper.GetString(ServerType)}");

            return iRowCountInt;
        }


        public void SaveMsgToBroker(long sessionId, string tableName, string messageId, string body, string messageKey)
        {
            string cmd = @"BEGIN TRY
BEGIN TRANSACTION;
    DECLARE @UniId UNIQUEIDENTIFIER
    BEGIN DIALOG CONVERSATION @UniId

    FROM SERVICE SBDemoInitiatorService
    TO SERVICE 'SBDemoTargetService'

    ON CONTRACT[http://Extreme-Advice.com/SBDemo01/ExtremeAdviceContractDemo]
	WITH ENCRYPTION = OFF;

    SEND ON CONVERSATION @UniId MESSAGE TYPE
    [http://Extreme-Advice.com/SBDemo01/RequestMessage]
	(
    @msg
    );
            Print 'succes 1'
COMMIT;
END TRY
BEGIN CATCH
    print 'Error 1'
    ROLLBACK TRANSACTION
END CATCH
";
    
            var msg = new SqlParameter("@msg", body);
            MetastorageDbContext.Database.ExecuteSqlRaw(cmd, msg);
        }
        public void SaveMsgToDataBase(long sessionId, string tableName, string messageId, string body, string messageKey, int messageTypeId)
        {
            string cmd = "";
            if (ServerType == SqlServerType.mssql)
            {//3100 msg в сек c удалением в одну таблицу и по потокам в разные
                
                if (tableName.Contains("msgqueue"))
                    cmd = @$"INSERT {tableName} ([session_id], [msg_id], [msg], [msg_key])
                                        VALUES ({sessionId}, @msg_id, @msg, @msgKey);";
                else
                    cmd = @$"INSERT {tableName} ([session_id],[msg_id],[msg], [msgtype_id])
                                        VALUES ({sessionId}, @msg_id, @msg, @msgtype_id);";

                var msgId = new SqlParameter("@msg_id", new Guid(messageId));
                var msg = new SqlParameter("@msg", body);
                var msgKey = new SqlParameter("@msgKey", messageKey);
                var msgtype_id = new SqlParameter("@msgtype_id", messageTypeId);
                

                MetastorageDbContext.Database.ExecuteSqlRaw(cmd, msgId, msg, msgKey, msgtype_id);

            }
            else if (ServerType == SqlServerType.psql)
            {
                /*
                500 msg в сек
                SELECT count(*),
                EXTRACT(EPOCH FROM max(dt_create)),
                EXTRACT(EPOCH FROM min(dt_create)),
                count(*) / (EXTRACT(EPOCH FROM max(dt_create)) - EXTRACT(EPOCH FROM min(dt_create)))
                FROM msgqueue
                */
                if (tableName == "msgqueue")
                    cmd = @$"INSERT INTO {tableName} (session_id, msg_id, msg, msg_key)
                                        VALUES ({sessionId}, @msg_id, @msg, @msgKey);";
                else
                    cmd = @$"INSERT INTO {tableName} (session_id, msg_id, msg)
                                        VALUES ({sessionId}, @msg_id, @msg);";
                var msgId = new NpgsqlParameter("msg_id", new Guid(messageId));
                var msg = new NpgsqlParameter("msg", body);
                var msgkey = new NpgsqlParameter("msgkey", messageKey);
                MetastorageDbContext.Database.ExecuteSqlRaw(cmd, msgId, msg, msgkey);
            }
            else
                throw new Exception($"SaveMsgToDataBase not supported for {SqlServerTypeHelper.GetString(ServerType)}");
        }
        public async Task SaveMsgToDataBaseAsync(int sessionId, string tableName, string messageId, string xdto, string messageKey)
        {
            string cmd = "";
            if (ServerType == SqlServerType.mssql)
            {   //900 msg в сек без удаления и в одну таблицу
                //600 msg в сек c удалением и в разные таблицы
                if (tableName.Contains("msgqueue"))
                    cmd = @$"INSERT {tableName} ([session_id], [msg_id], [msg], [msg_key])
                                        VALUES ({sessionId}, @msg_id, @msg, @msgKey);";
                else
                    cmd = @$"INSERT {tableName} ([session_id],[msg_id],[msg])
                                        VALUES ({sessionId}, @msg_id, @msg);";

                var msgId = new SqlParameter("@msg_id", new Guid(messageId));
                var msg = new SqlParameter("@msg", xdto);
                var msgKey = new SqlParameter("@msgKey", messageKey);

                await MetastorageDbContext.Database.ExecuteSqlRawAsync(cmd, msgId, msg, msgKey);

            }
            else
                throw new Exception($"SaveMsgToDataBase not supported for {SqlServerTypeHelper.GetString(ServerType)}");
        }
        public void SaveMsgToDataBase(int sessionId, string tableName, BasicGetResult mqmsg)
        {
            /*
                        Msgqueue mu = new Msgqueue { SessionId = sessionId, MsgId = new Guid(mqmsg.BasicProperties.MessageId), Msg = Encoding.UTF8.GetString(mqmsg.Body.ToArray()), MsgOrder = (int)mqmsg.DeliveryTag, MsgKey = mqmsg.BasicProperties.Type, UpdateDate = DateTime.Now };
                        MetastorageDbContext.Msgqueues.Add(mu);
                        MetastorageDbContext.SaveChanges();
            */
            string cmd = "";
            if (ServerType == SqlServerType.mssql)
            {   //900 msg в сек без удаления и в одну таблицу
                //600 msg в сек
                if (tableName.Contains("msgqueue"))
                    cmd = @$"INSERT {tableName} ([session_id], [msg_id], [msg], [msg_key])
                                        VALUES ({sessionId}, @msg_id, @msg, @msgKey);";
                else
                    cmd = @$"INSERT {tableName} ([session_id],[msg_id],[msg])
                                        VALUES ({sessionId}, @msg_id, @msg);";

                var msgId = new SqlParameter("@msg_id", mqmsg.BasicProperties.MessageId);
                var msg = new SqlParameter("@msg", Encoding.UTF8.GetString(mqmsg.Body.ToArray()));
                var msgKey = new SqlParameter("@msgKey", mqmsg.BasicProperties.Type);

                MetastorageDbContext.Database.ExecuteSqlRaw(cmd, msgId, msg, @msgKey);

            }
            else if (ServerType == SqlServerType.psql)
            {
                //340 msg в сек
                if (tableName == "msgqueue")
                    cmd = @$"INSERT INTO {tableName} (session_id, msg_id, msg, msg_key)
                                        VALUES ({sessionId}, @msg_id, @msg, @msgKey);";
                else
                    cmd = @$"INSERT INTO {tableName} (session_id, msg_id, msg)
                                        VALUES ({sessionId}, @msg_id, @msg);";
                var msgId = new NpgsqlParameter("msg_id", new Guid(mqmsg.BasicProperties.MessageId));
                var msg = new NpgsqlParameter("msg", Encoding.UTF8.GetString(mqmsg.Body.ToArray()));
                var msgkey = new NpgsqlParameter("msgkey", mqmsg.BasicProperties.Type);
                MetastorageDbContext.Database.ExecuteSqlRaw(cmd, msgId, msg, msgkey);

            }
            else if (ServerType == SqlServerType.sqlite)
            {
                //210 msg в сек   
                //if (tableName == "msgqueue")
                cmd = @$"INSERT INTO msgqueue (session_id, msg_id, msg, msg_key)
                                        VALUES ({sessionId}, @msg_id, @msg, @msgkey);";
                /*else
                    cmd = @$"INSERT INTO {tableName} (session_id, msg_id, msg)
                                        VALUES ({sessionId}, @msg_id, @msg);";
                */
                var msgId = new SqliteParameter("@msg_id", new Guid(mqmsg.BasicProperties.MessageId));
                var msg = new SqliteParameter("@msg", Encoding.UTF8.GetString(mqmsg.Body.ToArray()));
                var msgkey = new SqliteParameter("@msgkey", mqmsg.BasicProperties.Type);
                MetastorageDbContext.Database.ExecuteSqlRaw(cmd, msgId, msg, msgkey);
            }
            else if (ServerType == SqlServerType.clickhouse)
            {
                // 6 msg в сек
                //!!!Bug Bug Bug
                //    
                //if (tableName == "msgqueue")
                //cmd = @$"INSERT INTO msgqueue (session_id, msg_id, msg, msg_key)
                //                        VALUES ({sessionId}, @msg_id, @msg, @msgkey);";
                cmd = @"INSERT INTO msgqueue (session_id, msg_id, msg, msg_key)
                                        VALUES ({0}, '" + new Guid(mqmsg.BasicProperties.MessageId).ToString() + @"', '{1}'" + $@", '{mqmsg.BasicProperties.Type}');";
                /*else
                    cmd = @$"INSERT INTO {tableName} (session_id, msg_id, msg)
                */
                ClickHouseDbParameter id = new ClickHouseDbParameter { ParameterName = sessionId.ToString(), Value = "id" };
                ClickHouseDbParameter msg = new ClickHouseDbParameter { ParameterName = Encoding.UTF8.GetString(mqmsg.Body.ToArray()), Value = "asss" };
                ClickHouseDbParameter[] result = new ClickHouseDbParameter[2];
                result[0] = id; // параметры работают только из массива, и местами перепутаны: ParameterName с Value, короче жестокое опенсоурс
                result[1] = msg;
                MetastorageDbContext.Database.ExecuteSqlRaw(cmd, result);
            }
            else
                throw new Exception($"SaveMsgToDataBase not supported for {SqlServerTypeHelper.GetString(ServerType)}");

        }

        public OrdersLogBuffer[] GetOrdersLogBuffer(string atrArray, long sessionId, Guid msgId)
        {
            string[] subs = atrArray.Replace("[[", "").Replace("]]", "").Split("],[");
            return Enumerable.Range(0, subs.Length)
               .Select(x => new OrdersLogBuffer
               {
                    SessionId = sessionId,
                    MsgId = msgId,
                    Msg  = "[" + subs[x] + "]",
                    MsgTypeId = 1,
                    IsError  = false,
                    CreateDate = DateTime.Now,
                    UpdateDate = new DateTime(1900, 1, 1)
               }
               ).ToArray();
        }

        
        public async Task EfBulkInsert(string atrArray, long sessionId, Guid msgId) 
        {
                using var context = new MetastorageContext(OptionsBuilder.Options);
                await context.BulkInsertAsync(GetOrdersLogBuffer(atrArray, sessionId, msgId));
        }
 
        
        public static List<OrdersLogBuffer> GetListOrdersLogBuffer(string atrArray, int uniqueId)
        {
            string[] subs = atrArray.Replace("[[", "").Replace("]]", "").Split("],[");
            List<OrdersLogBuffer> dbs = new List<OrdersLogBuffer>();
            
            Dictionary<long, string> uniqOrder = new Dictionary<long, string>();
            long key;
            
            for (int i = 0; i < subs.Length; i++)
            {
                string[] subs2 = subs[i].Split("\",\"");
                if (long.TryParse(subs2[uniqueId], out key))
                {
                    if (uniqOrder.ContainsKey(key))
                        uniqOrder[key] = subs[i];
                    else
                        uniqOrder.Add(key, subs[i]);
                }
                else
                    throw new Exception(@"item {i} Convertation key={subs2[uniqueId]} to long ");
            //uniqOrder.TryGetValue(key);
            //uniqOrder.Add
                OrdersLogBuffer olb = new OrdersLogBuffer();
                dbs.Add(olb);
            }
            //MetastorageDbContext.OrdersLogBuffers.Add(dbs.ToArray());
            return dbs; // uniqOrder.Values.ToList(); 
           
        }
    }


}
