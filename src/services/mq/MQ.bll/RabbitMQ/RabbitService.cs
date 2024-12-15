using Microsoft.Extensions.Configuration;
using MQ.bll.Common;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQ.bll.RabbitMQ
{
    public class RabbitService : IQueueService
    {
        private BllOption Bo;
        private IConfiguration Configuration;

        public RabbitService(BllOption bo, IConfiguration configuration)
        {
            this.Bo = bo;
            this.Configuration = configuration;
        }

        public async Task GetAllMessages(CancellationTokenSource cts)
        {
            var funcClass = new ReceiveAllMessages(Bo, Configuration, cts.Token);
            await funcClass.ProcessLauncherConsoleAsync();
        }

        public async Task SendAllMessages(CancellationTokenSource cts)
        {
 
            var funcClass = new SendAllUnknownMsg(Bo, Configuration, cts.Token);
            await funcClass.ProcessLauncher();
        }
    }
}
