using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQ.bll.Common
{
#pragma warning disable CS8618
    public class RabbitMQSettings
    {
        public string Host { get; init; }
        public string VirtualHost { get; init; }
        public string Port { get; init; }
        public string Exchange { get; init; }
        public string UserName { get; init; }
        public string UserPassword { get; init; }
        public string DefaultQueue { get; init; }
        public string SslEnabled { get; init; }
        public string SslServerName { get; init; }
        public string SslVersion { get; init; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
