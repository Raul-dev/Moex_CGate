﻿using Confluent.Kafka;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQ.bll
{
    public interface IQueueChannel: IDisposable
    {
        public bool IsOpen { get; }
        Task InitSetup(MQSession? mqSession = null, bool isSend = true, bool isSubscription = false);
        Task CloseAsync();
        Task<long> MessageCountAsync();
        Task<BasicGetResult?> GetMessageAsync();
        Task PublishMessageAsync(string msgKey, string msg);
        Task AcknowledgeMessageAsync(ulong offsetId, bool multiple = false);
        Task RejectMessageAsync(ulong offsetId, bool requeue = true);
        void Flush();
    }
}
