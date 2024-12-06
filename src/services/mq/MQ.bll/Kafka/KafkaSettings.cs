using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQ.bll.Common
{
    public class KafkaSettings
    {
        public required string Host { get; init; }
        public required string Port { get; init; }
        public required string GroupId { get; init; }
        public required string Topic { get; init; }
    }
}
