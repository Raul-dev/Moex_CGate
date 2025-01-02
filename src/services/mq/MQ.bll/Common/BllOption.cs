using MQ.dal;

namespace MQ.bll.Common
{
    public enum BllOptionVerb
    {
        Sql,
        Json,
        SqlFromJson
    }

    public class BllOption
    {
        public BllOptionVerb Verb { get; set; }


        //DB Server name
        public string ServerName { get; set; } = "";

        //Database name
        public string DatabaseName { get; set; } = "";


        public SqlServerType ServerType { get; set; }
        public SqlServerType GenerationServerType { get; set; }
        public int Port { get; set; }
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string OutputJsonFile { get; set; } = "";
        public string InputJsonFile { get; set; } = "";
        public bool IsConfirmMsgAndRemoveFromQueue { get; set; }
        public SessionModeEnum SessionMode { get; set; }
        public int Iteration { get; set; }
        public int PauseMs { get; set; }

        public bool IsKafka { get; set; }
        public bool IsMultipleMessages { get; set; } = true;
        public KafkaSettings KafkaServSettings { get; set; }
        public RabbitMQSettings RabbitMQServSettings { get; set; }

        //Mongo
        public bool MongoEnable { get; set; } = false;
        public string MongoUrl { get; set; } = "localhost:27017/";
        public string MongoUser { get; set; } = "admin";
        public string MongoPassword { get; set; } = "admin";
        public string MongoDatabase { get; set; } = "rbbt";

    }
}
