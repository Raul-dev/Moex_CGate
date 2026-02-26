using MQ.bll.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQ.Share.Configuration
{
    public enum SessionModeEnum
    {
        FullMode = 1,
        BufferOnly = 2,
        EtlOnly = 3,
        WhileGet = 4,
    }
    public class Rootobject
    {
        public Servicegetmsgsettings ServiceGetMsgSettings { get; set; }
    }

    public class Servicegetmsgsettings
    {
        public string ServiceName { get; set; }
        public string ServiceDisplayName { get; set; }
        public string ServiceDescription { get; set; }
        public BllOption[] Services { get; set; }
    }
    /*
    public class Service
    {
        public string Name { get; set; }
        public bool IsEnabled { get; set; }
        public string LogPrefix { get; set; }
        public Rabbitmqsettings RabbitMQSettings { get; set; }
        public Databasesettings DataBaseSettings { get; set; }
    }
    
    public class Rabbitmqsettings
    {
        public string Host { get; set; }
        public string VirtualHost { get; set; }
        public string Exchange { get; set; }
        public int Port { get; set; }
        public string UserName { get; set; }
        public string UserPassword { get; set; }
        public string DefaultQueue { get; set; }
        public string SslEnabled { get; set; }
        public string SslServerName { get; set; }
        public string SslVersion { get; set; }
    }

    public class Databasesettings
    {
        public string ServerName { get; set; }
        public string Port { get; set; }
        public string ServerType { get; set; }
        public string DataBase { get; set; }
        public string ClientName { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        [EnumDataType(typeof(SessionModeEnum))]
        // Ensures the value is specifically Category1 or Category2 (requires .NET 8+)
        //[AllowedValues(SessionModeEnum.)]
        public SessionModeEnum? SessionMode { get; set; }
    }
    */
}
