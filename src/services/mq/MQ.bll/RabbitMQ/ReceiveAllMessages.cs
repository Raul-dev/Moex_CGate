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
using Microsoft.CodeAnalysis.Text;
using Microsoft.IdentityModel.Tokens;
using static MQ.dal.DBHelper;

namespace MQ.bll.RabbitMQ
{
    public class ReceiveAllMessages
    {
        CancellationToken cancellationToken;
        RabbitMQSettings? rabbitMQSettings;
        MQSession rabbitMQSession;
        //IChannel? mqChannel = null;
        RabbitMQChannel mqChannel;
        //IConnection?
        RabbitMQConnection? mqConnection = null;
        ConnectionFactory? mqFactory = null;
        Thread _bulkThread;
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


        public async Task ProcessLauncherConsoleAsync()
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
        public void StartBulkThread()
        {
            if (_bulkThread == null)
            {
                _bulkThread = new Thread(BulkThread);
                _bulkThread.Name = "BulkThread";
            }

            if (!_bulkThread.IsAlive)
            {
                _bulkThread.Start(cancellationToken);
                
            }
        }
        public void BulkThread(object? sender)
        {
            if (sender is null)
                return;
            CancellationToken token = (CancellationToken)sender;
            int hash = Thread.CurrentThread.GetHashCode();
            Log.Debug("Start Bulk Thread hash {0}", hash);
            int i = 0;
            //For Debug
            //if(MessagePropertyKey == "key")
            //{
            //    int h = 0;
            //    h++;
            //}
            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    Log.Debug("Bulk Thread hash, In iteration {1}, cancellation has been requested...", hash, i);
                    break;
                }

                try
                {
                    rabbitMQSession.SaveMsgToDataBaseBulk();
                }
                catch (Exception ex)
                {
                    Log.Error("Bulk thread error: {0}.", ex.Message);
                   
                }

                token.WaitHandle.WaitOne(1000);
                i++;
            }
        }
        public async Task<string> InitFactory()
        {
            mqFactory = new ConnectionFactory();
            mqFactory.UserName = rabbitMQSettings?.UserName ?? throw new ArgumentNullException();
            mqFactory.Password = rabbitMQSettings.UserPassword;
            mqFactory.VirtualHost = rabbitMQSettings.VirtualHost;
            mqFactory.HostName = rabbitMQSettings.Host;
            mqFactory.Port = int.Parse(rabbitMQSettings.Port);
            
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
                    await mqChannel.InitSetup(option, rabbitMQSettings.Exchange, rabbitMQSettings.DefaultQueue, cancellationToken, rabbitMQSession, true);
                    if(option.IsMultipleMessages)
                        StartBulkThread();
                } else
                    await mqChannel.InitSetup(option, rabbitMQSettings.Exchange, rabbitMQSettings.DefaultQueue, cancellationToken, rabbitMQSession, false);

            }

            return rabbitMQSettings.DefaultQueue;
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
                        msgcnt = await mqChannel.MessageCountAsync();

                        if (msgcnt > 0)
                            Log.Debug("Start get from queue. Rabbit Messages Count: {0}", msgcnt);
                        for (uint i = 0; i < msgcnt; i++)
                        {
                            //BasicGetResult message = await mqChannel.BasicGetAsync(queueName, false);
                            BasicGetResult message = await mqChannel.GetMessageAsync();
                            if (message != null)
                            {

                                rabbitMQSession.SaveMsgToDataBase(message.BasicProperties , message.Body);
                                if (option.IsConfirmMsgAndRemoveFromQueue)
                                    //await mqChannel.BasicAckAsync(message.DeliveryTag, true);
                                    await mqChannel.ConfirmMessageAsync(message.DeliveryTag);
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
    }
}
