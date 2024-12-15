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
        Task CloseAsync();
        Task<uint> MessageCountAsync();
        Task<BasicGetResult> GetMessageAsync();
        Task PublishMessageAsync(string msgKey, string msg);
        Task ConfirmMessageAsync(ulong offsetId, bool multiple = false);
        Task RejectMessageAsync(ulong offsetId, bool requeue = true);
    }
}
