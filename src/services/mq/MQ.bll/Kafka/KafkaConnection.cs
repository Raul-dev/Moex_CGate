using Microsoft.EntityFrameworkCore.Metadata;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using MQ.bll.Common;

namespace MQ.bll.Kafka
{
    class KafkaConnection : IDisposable
    {
        private readonly IConnectionFactory _connectionFactory;
        private IConnection _connection;
        private bool _disposed;
        private bool _isEvent;
        private readonly object sync_root = new object();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public KafkaConnection(KafkaSettings KafkaSettings)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
            //_connectionFactory = new ConnectionFactory();
            //_connectionFactory = connectionFactory;
        }

        public bool IsOpen
        {
            get
            {
                return _connection != null && _connection.IsOpen && !_disposed;
            }
        }
        
        public async Task CloseAsync()
        {
            await _connection.CloseAsync();
            if (_isEvent)
            {
//                _connection.ConnectionShutdownAsync -= OnConnectionShutdown;
//                _connection.CallbackExceptionAsync -= OnCallbackException;
//                _connection.ConnectionBlockedAsync -= OnConnectionBlocked;
                _isEvent = false;
            }

        }
        public async Task<KafkaChannel> CreateChannelAsync()
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("No RabbitMQ connections are available to perform this action");
            }
            IChannel ch = await _connection.CreateChannelAsync();

            return new KafkaChannel(ch);
        }

        public void Dispose()
        {
            if (_disposed) return;

            if (IsOpen)
                _connection.CloseAsync();
            _disposed = true;
            if (_isEvent)
            {

                _isEvent = false;
            }
            if(_connection != null)
              _connection.Dispose();
        }

        public async Task<bool> TryConnect()
        {
            await _semaphore.WaitAsync();
            try
            {
                _connection = await _connectionFactory.CreateConnectionAsync();

                if (IsOpen)
                {
                    Log.Debug("A RabbitMQ connection has been created.");
                    //_connection.ConnectionShutdownAsync += OnConnectionShutdown;
                    //_connection.CallbackExceptionAsync += OnCallbackException;
                    //_connection.ConnectionBlockedAsync += OnConnectionBlocked;
                    _isEvent = true;
                    return true;
                }
                else
                {
                    return false;
                }

            }
            catch (Exception ex)
            {
                Log.Error("Kafka connection error: {0}", ex.Message);
                return false;
            }
            finally
            {
                _semaphore.Release();

            }
        }
        /*
        private async Task OnConnectionBlocked(object sender, ConnectionBlockedEventArgs e)
        {
            if (_disposed) return;
            await TryConnect();
        }

        private async Task OnCallbackException(object sender, CallbackExceptionEventArgs e)
        {
            if (_disposed) return;
            await TryConnect();
        }

        private async Task OnConnectionShutdown(object sender, ShutdownEventArgs reason)
        {
            if (_disposed) return;
            
            if (reason.ReplyText != "Goodbye")
            {
                Log.Error(reason.ToString());
                await TryConnect();
            }
            else {
                Log.Debug(reason.ToString());
            }
        }
*/
    }
}
