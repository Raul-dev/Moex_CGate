using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using System.Threading;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace RabbitMQSqlClr
{
  internal class RabbitPublisher
  {

    internal IConnection RabbitConn;
    ConnectionFactory _connFactory;
    private string _connString;
    private int _channels;
    internal int RabbitEndpointId;

    readonly ConcurrentStack<IModel> _rabbitChannels = new ConcurrentStack<IModel>();


    internal RabbitPublisher(string amqpConnString, int endPointId, int channels = 100)
    {
      _connString = amqpConnString;
      _channels = channels;
      RabbitEndpointId = endPointId;
    }

    internal bool InternalConnect()
    {
        try
        {
            _connFactory = new ConnectionFactory();
            _connFactory.Uri = new Uri(_connString);
            _connFactory.AutomaticRecoveryEnabled = true;
            _connFactory.TopologyRecoveryEnabled = true;

            // ПРАВИЛЬНАЯ НАСТРОЙКА SOCKETFACTORY:
            // Передаем лямбду, которая принимает семейство адресов (addressFamily)
            _connFactory.SocketFactory = (addressFamily) =>
            {
                // 1. Создаем стандартный клиентский сокет .NET
                var tcpClient = new System.Net.Sockets.TcpClient(addressFamily);

                // 2. Применяем высокоскоростные оптимизации к системному сокету
                tcpClient.NoDelay = true;             // Отключаем алгоритм Нагла (минус задержки пакетов)
                tcpClient.SendBufferSize = 64 * 1024; // Увеличиваем буфер отправки до 64 КБ

                // 3. Оборачиваем его в интерфейс ITcpClient, который требует RabbitMQ
                return new TcpClientAdapter(tcpClient.Client);
            };

            RabbitConn = _connFactory.CreateConnection();

            for (int x = 0; x < _channels; x++)
            {
                var ch = RabbitConn.CreateModel();
                _rabbitChannels.Push(ch);
            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

        internal bool Post(string exchange, byte[] msg, string topic)
    {
        // Извлекаем канал БЕЗ блокировок. 
        // Если стек пуст, TryPop вернет false, но мы НЕ блокируем поток локом.
        if (_rabbitChannels.TryPop(out IModel channel))
        {
            try
            {
                // Отправка в сокет. Чтобы было максимально быстро, 
                // убедитесь, что свойства сообщения (IBasicProperties) не создаются заново каждый раз.
                channel.BasicPublish(exchange, topic, false, null, msg);

                // Возвращаем обратно в пул
                _rabbitChannels.Push(channel);
                return true;
            }
            catch (Exception)
            {
                if (channel != null) { try { channel.Close(); } catch { } }
                throw;
            }
        }
        else
        {
            // Экстремальный случай: если 50-100 каналов из пула внезапно кончились, 
            // создаем канал БЕЗ лока. Да, CreateModel() на одном Connection не на 100% потокобезопасен 
            // в старых версиях клиента, но в современном RabbitMQ.Client под .NET он оптимизирован.
            var tCh = RabbitConn.CreateModel();
            tCh.BasicPublish(exchange, topic, false, null, msg);
            _rabbitChannels.Push(tCh); // Расширяем пул лениво
            return true;
        }
    }

    internal void Shutdown()
    {
      try
      {
        IModel value;
        while (_rabbitChannels.Count > 0)
        {
          if (!_rabbitChannels.TryPop(out value)) continue;
          value.Close();
          value = null;
        }

        RabbitConn.Close();
      }
      finally
      {
        _rabbitChannels.Clear();
      }
    }

  }
}
