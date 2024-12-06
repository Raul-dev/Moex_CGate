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

namespace MQ.bll.RabbitMQ
{
    public class SendAllUnknownMsg
    {
        RabbitMQSettings rabbitMQSettings;
        BllOption option;
        DBHelper dbHelper;
        public SendAllUnknownMsg(BllOption bllOption, IConfiguration configuration)
        {
            option = bllOption;
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
                await mqConnection.TryConnect();
                {
                    Log.Information("Start sending messages.");
                    using var channel = await mqConnection.CreateChannelAsync();
                    {
                        await channel.QueueBindAsync(queueName, rabbitMQSettings.Exchange, "*", arguments: new Dictionary<string, object>());
                        await channel.QueueUnbindAsync("notification-queue", rabbitMQSettings.Exchange, "*", new Dictionary<string, object>());
                        Log.Information(@$"We are starting to add {mq.Count} messages to the RabitMQ.");
                        foreach (var item in mq)
                        {
                            
                            byte[] messageBodyBytes = Encoding.UTF8.GetBytes(item.Msg?? throw new ArgumentNullException() );
                            BasicProperties props = new BasicProperties();
                            props.ContentType = "text/plain";
                            props.MessageId = Guid.NewGuid().ToString();
                            props.Type = item.MsgKey;
                            props.DeliveryMode = DeliveryModes.Persistent;
                            await channel.BasicPublishAsync<BasicProperties>(rabbitMQSettings.Exchange,routingKey: item.MsgKey??"", mandatory: true, props, messageBodyBytes);
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
