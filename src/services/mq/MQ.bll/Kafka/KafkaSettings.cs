using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQ.bll.Common
{
#pragma warning disable CS8618
    public class KafkaSettings
    {
        public required string Host { get; init; }
        public required string Port { get; init; }
        public required string GroupId { get; init; }
        public required string Topic { get; init; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
