
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson;
using RabbitMQ.Client;
using static System.Net.Mime.MediaTypeNames;
using MQ.dal.Models;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using static MQ.dal.DBHelper;
using MongoDB.Bson.IO;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static Npgsql.PostgresTypes.PostgresCompositeType;
using System.Collections;
using SharpCompress.Common;
using Microsoft.IdentityModel.Tokens;

namespace MQ.dal
{
    public class MongoHelper
    {
        //public IMongoCollection<BsonDocument> devCollection;
        public IMongoDatabase db;
        public MongoHelper(string url, string user, string password, string mongoDatabase)
        {
            /*Create Admin user in mongo console:
              use admin
              db.createUser(
                { 
                 user: "hello_admin",
                 pwd:  "hello123",
                 roles:
                 [
                 { role:"readWrite",db:"config"},
                 "clusterAdmin"
                 ] } );
             * */
            IMongoClient client = new MongoClient(MongoUrl.Create(@$"mongodb://{user}:{password}@{url}"));
            
            db = client.GetDatabase(mongoDatabase);
            bool isMongoLive = db.RunCommandAsync((Command<BsonDocument>)"{ping:1}").Wait(1000);
            if (!isMongoLive)
                throw new Exception("Mongo server is not available.");
            //var status =  db.S .ServerStatus();
            //db.CreateCollection("BasicGetResult");
            //IMongoCollection<BsonDocument> devCollection = db.GetCollection<BsonDocument>("o");
            //var filter = Builders<BsonDocument>.Filter.Empty;
            //var r = devCollection.CountDocuments(filter);// .Count(null);
        }

        public void test()
        {
            var connectionString = Environment.GetEnvironmentVariable("MONGODB_URI");
            if (connectionString == null)
            {
                Console.WriteLine("You must set your 'MONGODB_URI' environmental variable. See\n\t https://www.mongodb.com/docs/drivers/csharp/current/quick-start/#set-your-connection-string");
                Environment.Exit(0);
            }
            var client = new MongoClient(connectionString);
            var collection = client.GetDatabase("sample_mflix").GetCollection<BsonDocument>("movies");
            var filter = Builders<BsonDocument>.Filter.Eq("title", "Back to the Future");
            var document = collection.Find(filter).First();
            Console.WriteLine(document);
        }
        public class BsonDoc
        {
            public string RoutingKey = "";
            public string Type = "";
            public string MessageID = "";
            public string Body = "";
        }
        public void Save(BasicGetResult resmsg, string collectionName)
        {
            //12603/12.23 ---1030 per sec
            //[21:13:46.34] [DBG] Start queue. Rabbit Message Count: 12603
            //[21:13:58.57][DBG] Finish queue. Rabbit Message Count: 12603 "Реквизиты"[0]
            //c удалением сообщений
            //[23:39:39.78] [DBG] Start queue. Rabbit Message Count: 12603
            //[23:39:53.10][DBG] Finish queue. Rabbit Message Count: 12603
            var bsonDoc = BsonDocument.Parse(Newtonsoft.Json.JsonConvert.SerializeObject((new BsonDoc { RoutingKey = resmsg.RoutingKey, MessageID = resmsg.BasicProperties.MessageId!, Type = resmsg.BasicProperties.Type! , Body = Encoding.UTF8.GetString(resmsg.Body.ToArray()) })));
            IMongoCollection<BsonDocument> lCollection = db.GetCollection<BsonDocument>(collectionName);
            lCollection.InsertOne(bsonDoc);

        }

        public long SaveCollectionToDB(long sessionId, string collectionName, DBHelper dbHelper, string tableName, string processQuery, CancellationToken cancellationToken)
        {
            long cnt = 0;
            {
                IMongoCollection<BsonDocument> lCollection = db.GetCollection<BsonDocument>(collectionName);
                var filter = Builders<BsonDocument>.Filter.Empty;

                var col = lCollection.Find(_ => true);

                foreach (var doc in col.ToList())
                {
                    if (cancellationToken.IsCancellationRequested == true)
                        break;
                    cnt++;
                    string guid = doc["MessageID"].ToString() ?? "00000000 - 0000 - 0000 - 0000 - 000000000000";
                    dbHelper.SaveMsgToDataBase(sessionId, tableName, guid, doc["Body"].ToString() ?? "", collectionName,1);
                    filter = Builders<BsonDocument>.Filter.Eq("_id", doc["_id"].AsObjectId);
                    lCollection.DeleteOne(filter);
                }

                return cnt;
            }
        }
        public async Task<int> SaveCollectionToDBAsync(int sessionId, string collectionName, DBHelper dbHelper, string tableName, string processQuery)
        {
            
            try { 
                IMongoCollection<BsonDocument> lCollection = db.GetCollection<BsonDocument>(collectionName);
                var filter = Builders<BsonDocument>.Filter.Empty;
                int cnt = ((int)lCollection.CountDocuments(filter));
                //var uu = lCollection.FindAs(_ => true).Explain();
                FindOptions<BsonDocument> options = new FindOptions<BsonDocument> { Limit = 1000000, CursorType = CursorType.Tailable };
                //FindOptions options = new FindOptions{ Limit = 1 };
                using (var cursor = await lCollection.FindAsync(_ => true, options)) // ("{}"))
                {
                    while (await cursor.MoveNextAsync())
                    {
                        int i = 0;
                        foreach (var doc in cursor.Current.ToArray())
                        {
                            Console.WriteLine(i++.ToString());
                            string guid = doc["MessageID"].ToString() ?? "00000000 - 0000 - 0000 - 0000 - 000000000000";
                            dbHelper.SaveMsgToDataBase(sessionId, tableName, guid, doc["Body"].ToString() ?? "", collectionName, 1); //TODO ContentType
                        }
                        //cursor.De
                    }
                }
                return cnt;
                /*
                            foreach (var doc in lCollection.Find(_ => true).ToList())
                            {
                                Newtonsoft.Json.JsonConvert.SerializeObject(all)
                                string guid = doc["СвойстваСообщения"]["ИдентификаторСообщения"].ToJson();
                                //collection.Find(criteria).SetFields(Fields.Include("oneField", "anotherField").Exclude("_id"))
                                //var i = doc.GetValue<string>("СвойстваСообщения");
                                JSchema jsSchema = JSchema.Parse(doc.ToJson());

                                //JSchema jsSchema = doc.ToJson();
                                //                var js = jsSchema.ToJson();
                                //                BsonElement
                                //"СвойстваСообщения={ ""СобытиеСообщения"" : ""Выгружено"", ""ДатаСобытия"" : ""2022-12-16T06:20:22"", ""ИдентификаторСообщения"" : ""0f1d3154-9621-4b6d-b034-d75d994e621a"", ""ИдентификаторСообщенияИсточника"" : ""0f1d3154-9621-4b6d-b034-d75d994e621a"", ""ИмяБазы"" : ""ERP_prod"", ""ПолноеИмяБазы"" : ""Srvr=\""msk - erpapp - 01\""; Ref =\""ERP_prod\""; "", ""ИмяБазыИсточника"" : ""ERP_prod"", ""ПолноеИмяБазыИсточника"" : ""Srvr =\""msk - erpapp - 01\""; Ref =\""ERP_prod\""; "", ""КлючМаршрутизации"" : ""json.Документ.TABLE_REP"" }
                                JToken jvalue;
                            bool bres = jsSchema.ExtensionData.TryGetValue("СвойстваСообщения.ИдентификаторСообщения", out jvalue);
                            if (!bres)
                            {
                                return ;
                            }
                            // string messageId, string xdto, string messageKey,
                            //await 
                            //await dbHelper.SaveMsgToDataBaseAsync(sessionId, tableName, messageId, doc.ToString(), messageKey);

                                dbHelper.SaveMsgToDataBase(sessionId, tableName, "", doc.ToJson(), collectionName);

                            }
                */
                
                // bulk
                // https://habr.com/ru/articles/251397/
                //https://github.com/aidforwork/PaperSource.EF.SqlBulkCopy
            } catch (Exception ex)
            {
                Console.WriteLine("SaveCollectionToDB: " + ex.Message);
            }
            return 0;
        }
    }
}
