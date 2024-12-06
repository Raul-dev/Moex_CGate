using Microsoft.Extensions.Configuration;
using MQ.bll.Common;
using MQ.dal;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Runtime.ConstrainedExecution;
using System.Collections;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Threading;
using System.Threading.Channels;
using MongoDB.Driver.Core.Bindings;
using System.Diagnostics;
using MQ.dal.Models;

namespace MQ.bll.RabbitMQ
{
    public class ReceiveAllMessages
    {
        CancellationToken cancellationToken;
        RabbitMQSettings? rabbitMQSettings;
        MQSession rabbitMQSession;
        AsyncEventingBasicConsumer mqConsumer;
        IChannel? mqChannel = null;
        //IConnection?
        RabbitMQConnection? mqConnection = null;
        ConnectionFactory? mqFactory = null;

        string? ConsumeTag;
        BllOption option;

        public ReceiveAllMessages(BllOption bllOption, IConfiguration configuration, CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            option = bllOption;
            rabbitMQSettings = configuration.GetRequiredSection(nameof(RabbitMQSettings)).Get<RabbitMQSettings>() ?? throw new ArgumentNullException();
            //dbHelper = new DBHelper(option.ServerName, option.DatabaseName, option.Port, option.ServerType, option.User, option.Password);
        }
        public async Task ProcessLauncherAsync()
        {
            int executionCount = 0;

            rabbitMQSession = new MQSession(option, cancellationToken);
            long sessionId = rabbitMQSession.StartSessionProcessing();
            if (sessionId == -1)
            {
                Log.Error("SessionId -1");
                return;
            }
            string queueName = await InitFactory();
            if (rabbitMQSession.SessionMode != SessionModeEnum.BufferOnly)
                rabbitMQSession.RunEtlThread("All");
            while (!cancellationToken.IsCancellationRequested)
            {
                executionCount++;
                await Task.Delay(50000, cancellationToken);

                Log.Information("Service running. Count: {Count} Connection state: {state}", executionCount, mqConnection.IsOpen);
            }

        }


        public async Task ProcessLauncher()
        {
            rabbitMQSession = new MQSession(option, cancellationToken);
            long sessionId = rabbitMQSession.StartSessionProcessing();
            if (sessionId == -1)
                return;
            int errorCount = 0;
            while (true)
            {
                var res = await MQProcess();
                if (res != 0)
                    errorCount++;
                if (cancellationToken.IsCancellationRequested == true || errorCount > 10)
                {
                    break;
                }
            }
            return;
        }
        public async Task<string> InitFactory()
        {
            mqFactory = new ConnectionFactory();
            mqFactory.UserName = rabbitMQSettings?.UserName ?? throw new ArgumentNullException();
            mqFactory.Password = rabbitMQSettings.UserPassword;
            mqFactory.VirtualHost = rabbitMQSettings.VirtualHost;
            mqFactory.HostName = rabbitMQSettings.Host;
            mqFactory.Port = int.Parse(rabbitMQSettings.Port);
            string queueName = rabbitMQSettings.DefaultQueue;
            if (rabbitMQSession.SessionMode == SessionModeEnum.BufferOnly ||
                rabbitMQSession.SessionMode == SessionModeEnum.FullMode ||
                rabbitMQSession.SessionMode == SessionModeEnum.WhileGet
                )
            {
                Log.Debug($@"Creating RabbitMQ factory connection: Host={mqFactory.HostName}, Port={rabbitMQSettings.Port}, Mode={rabbitMQSession.SessionMode.ToString()}.");
                
                mqConnection = new RabbitMQConnection(mqFactory);
                await mqConnection.TryConnect();
                mqChannel = await mqConnection.CreateChannelAsync();
                if (rabbitMQSession.SessionMode != SessionModeEnum.WhileGet)
                {
                    Log.Debug($@"Starting ConsumerSubscription creation.");
                    await ConsumerSubscription(queueName);
                }
            }

            return queueName;
        }
        public async Task<int>  MQProcess()
        {
            //string ErrorMsg = "";
            try
            {
                string queueName = await InitFactory();
                if(mqChannel == null) throw new ArgumentNullException();
                while (true)
                {
                    if (rabbitMQSession.SessionMode == SessionModeEnum.WhileGet)
                    {
                        //Test load procedures
                        rabbitMQSession.RunEtlLoadProcedure("All");
                        uint msgcnt = 0; // for Debug Mode
                        msgcnt = await mqChannel.MessageCountAsync(queueName);

                        if (msgcnt > 0)
                            Log.Debug("Start get from queue. Rabbit Messages Count: {0}", msgcnt);
                        for (uint i = 0; i < msgcnt; i++)
                        {
                            BasicGetResult message = await mqChannel.BasicGetAsync(queueName, false);
                            if (message != null)
                            {

                                rabbitMQSession.SaveMsgToDataBase(message.BasicProperties , message.Body);
                                if (option.IsConfirmMsgAndRemoveFromQueue)
                                    await mqChannel.BasicAckAsync(message.DeliveryTag, true);
                            }
                        }
                        if (msgcnt > 0)
                            Log.Debug("Finish get from queue. Rabbit Message Count: {0}", msgcnt);
                    }

                    if (rabbitMQSession.SessionMode != SessionModeEnum.BufferOnly)
                        rabbitMQSession.RunEtlThread("All");

                    Thread.Sleep(2000);
                    if (rabbitMQSession.SessionMode != SessionModeEnum.BufferOnly)
                        if (mqChannel == null || !mqChannel.IsOpen)
                        {
                            Log.Error("Rabbit channel is closed.");
                            break;
                        }
                }

                return 0;
            }
            catch (Exception ex)
            {

                Log.Error(ex.Message);
                return -1;
            }
            finally
            {
                CleanProcess();
            }
        }
        public void CleanProcess()
        {
            //if(cancellationToken.IsCancellationRequested == false)
            //    cancellationToken.Cancel();

            if (mqChannel != null)
            {
                ConsumerUnSubscription();
                mqChannel.Dispose();
                mqChannel = null;
            }
            rabbitMQSession.FinishSessionProcessing();

            rabbitMQSession.CleanProcess();
            if (mqConnection != null)
            {
                
                mqConnection.Dispose();
                mqConnection = null;
            }
            mqFactory = null;
        }

        async Task ConsumerSubscription(string queueName)
        {
            Log.Debug($"ConsumerSubscription +OnReceivedMessageHandler {queueName}");
            if (mqChannel != null)
            {
                await mqChannel.QueueDeclarePassiveAsync(queueName);
                await mqChannel.BasicQosAsync(0, 20000, false);
                mqConsumer = new AsyncEventingBasicConsumer(mqChannel);

                mqConsumer.ReceivedAsync += OnReceivedMessageHandler;
                mqConsumer.UnregisteredAsync += OnCancel;
                ConsumeTag = await mqChannel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: mqConsumer);
            }
        }



        void ConsumerUnSubscription()
        {
            try
            {
                if (ConsumeTag != null)
                {
                    if (mqChannel.IsOpen)
                        mqChannel.BasicCancelAsync(ConsumeTag);
                    

                    //mqConsumer.ConsumerCancelled -= OnCancel;
                    mqConsumer.ReceivedAsync -= OnReceivedMessageHandler;
                    ConsumeTag = null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"ConsumerUnSubscription: {ex.Message}");
            }
        }

        private Task OnCancel(object sender, ConsumerEventArgs @event)
        {
            if (cancellationToken.IsCancellationRequested == true)
                return Task.CompletedTask;
            Log.Debug(@$"OnCancel MQ Consumer: {((AsyncEventingBasicConsumer)sender).ShutdownReason}");
            return Task.CompletedTask;
        }

        async Task OnReceivedMessageHandler(object sender, BasicDeliverEventArgs ea)
        {
            if (cancellationToken.IsCancellationRequested == true) return;
            Log.Debug(@$"Received Message {ea.BasicProperties.Type} cancellationToken.IsCancellationRequested: {cancellationToken.IsCancellationRequested}");
            AsyncEventingBasicConsumer cons = (AsyncEventingBasicConsumer)sender;
            IChannel ch = cons.Channel;
            try
            {

                rabbitMQSession.SaveMsgToDataBase(ea.BasicProperties, ea.Body);
                Log.Debug(@$"Saved message to DB");
                if (option.IsConfirmMsgAndRemoveFromQueue)
                {
                    await ch.BasicAckAsync(ea.DeliveryTag, true);
                    Log.Debug(@$"Removed message tag {ea.DeliveryTag} fom RabbitMQ");
                }
            } 
            catch (Exception ex)
            {
                Log.Debug(@$"OnReceivedMessage err: {ex.Message}");
                await ch.BasicRejectAsync(deliveryTag: ea.DeliveryTag, requeue: true);

            }
            finally
            {

            }
            // return Task.CompletedTask;
            
            
        }
    }
}
