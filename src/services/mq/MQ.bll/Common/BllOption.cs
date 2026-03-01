using MQ.dal;
using System.Diagnostics;

namespace MQ.bll.Common
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class BllOption
    {
        public string Name { get; set; } = "";  
        public bool IsEnabled { get; set; } = false;
        public string LogPrefix { get; set; } = ""; // Used as LogPrefix parameter WorkerLogPrefix
        /*
        //DB Server name
        public string ServerName { get; set; } = "";

        //Database name
        public string DatabaseName { get; set; } = "";
        public int Port { get; set; }
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        */
        public SqlServerType ServerType { get; set; } = SqlServerType.mssql;
        public SqlServerType GenerationServerType { get; set; } = SqlServerType.mssql;

        public string OutputJsonFile { get; set; } = "";
        public string InputJsonFile { get; set; } = "";
        public bool IsConfirmMsgAndRemoveFromQueue { get; set; } = false;
        public SessionModeEnum SessionMode
        {
            get => DataBaseServSettings?.SessionMode ?? SessionModeEnum.BufferOnly;
        }
        public int Iteration { get; set; }
        public int PauseMs { get; set; }

        public bool IsKafka { get; set; } = false;
        public bool IsMultipleMessages { get; set; } = false; // Bulk insert
        public KafkaSettings? KafkaServSettings { get; set; }
        public RabbitMQSettings? RabbitMQServSettings { get; set; }
        public required DataBaseSettings DataBaseServSettings { get; set; } = new DataBaseSettings();
        //Mongo
        //MongoSettings
        public bool MongoEnable { get; set; } = false;
        public MongoSettings? MongoServSettings { get; set; }

        public BllOption()
        {
            DataBaseServSettings  = new DataBaseSettings();
        }

        private string GetDebuggerDisplay()
        {
            return ToString();
        }
        public override string ToString()
        {
            string ret = "{";
            ret += Name;
            ret += ",";
            ret += DataBaseServSettings?.ServerName ?? "";
            ret += ",";
            ret += SessionMode;
            ret += "}";
            return ret;
        }
    }
}
