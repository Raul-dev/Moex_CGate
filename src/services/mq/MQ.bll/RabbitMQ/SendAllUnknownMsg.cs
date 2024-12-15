using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MQ.bll.Common;
using Microsoft.Extensions.Configuration;
using MQ.dal;
using static MQ.dal.DBHelper;
using Microsoft.VisualBasic;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel;
using System.Security.AccessControl;
//using BenchmarkDotNet.Attributes;
//using BenchmarkDotNet.Running;
using Microsoft.IdentityModel.Tokens;
using Microsoft.CodeAnalysis.Text;
using System.Threading.Channels;

namespace MQ.bll.RabbitMQ
{
    public class SendAllUnknownMsg
    {
        RabbitMQSettings rabbitMQSettings;
        BllOption option;
        DBHelper dbHelper;
        CancellationToken cancellationToken;



        public SendAllUnknownMsg(BllOption bllOption, IConfiguration configuration, CancellationToken cancellationToken)
        {
            option = bllOption;
            this.cancellationToken = cancellationToken;
            rabbitMQSettings = configuration.GetRequiredSection(nameof(RabbitMQSettings)).Get<RabbitMQSettings>() ?? throw new Exception("Нет конфига") ;
            dbHelper = new DBHelper(option.ServerName, option.DatabaseName, option.Port, option.ServerType, option.User, option.Password);
        }
        public async Task ProcessLauncher()
        {
            
            if (option.Iteration > 0)
                for (int i = 0; i < option.Iteration; i++)
                {
                    await MQProcess();
                }
            else
                await MQProcess();

            //var summary = BenchmarkRunner.Run<SendAllUnknownMsg>();
        }
        
        public async Task MQProcess()
        {
            List<MsgQueueItem> mq = dbHelper.GetMsgqueueItems();

            string ErrorMsg = "";
            try
            {
                var factory = new ConnectionFactory();
                factory.UserName = rabbitMQSettings.UserName;
                factory.Password = rabbitMQSettings.UserPassword;
                factory.VirtualHost = rabbitMQSettings.VirtualHost;
                factory.HostName = rabbitMQSettings.Host;
                factory.Port = int.Parse(rabbitMQSettings.Port);
                factory.RequestedConnectionTimeout = TimeSpan.FromSeconds(200) ;
                
                Random rnd = new Random();

                int iCount = 0;
                var queueName = rabbitMQSettings.DefaultQueue;

                using var mqConnection = new RabbitMQConnection(factory);
                if(await mqConnection.TryConnect())
                {
                    Log.Information("Start sending messages.");
                    using var channel = await mqConnection.CreateChannelAsync();
                    {
                        
                        await channel.InitSetup(option, rabbitMQSettings.Exchange, rabbitMQSettings.DefaultQueue, cancellationToken, null, false);

                        Log.Information(@$"We are starting to send {mq.Count} messages to the RabbitMQ.");
                        foreach (var item in mq)
                        {

                            if (item.Msg.IsNullOrEmpty())
                            {
                                Log.Warning("Null MsgOrder={0}, MsgKey={1}.", item.MsgOrder, item.MsgKey);
                                continue;
                            }
                            await channel.PublishMessageAsync(item.MsgKey??"", item.Msg?? throw new ArgumentNullException());
                            iCount++;
                            if (iCount % 1000 == 0)
                            {
                                Log.Information(@$"Send {iCount} messages.");
                            }
                            if (option.PauseMs != 0)
                            {
                                Thread.SpinWait(rnd.Next(option.PauseMs / 2, option.PauseMs));
                            }
                        }
                        Log.Information(@$"Send {iCount} messages.");
                        await Task.WhenAll(channel.CloseAsync(), mqConnection.CloseAsync());
                    }

                }
            }
            catch (Exception ex)
            {
                Log.Error("MQProcess:" + ex.Message);
                ErrorMsg = ex.Message;
            }

        }

    }
}
