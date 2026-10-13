using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using System.Threading;
using System.Collections.Concurrent;

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


    internal RabbitPublisher(string amqpConnString, int endPointId, int channels = 5)
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
        RabbitConn = _connFactory.CreateConnection();
        

        for (int x = 0; x < _channels; x++)
        {
          var ch = RabbitConn.CreateModel();
          _rabbitChannels.Push(ch);
        }

        return true;
      }
      catch(Exception)
      {
        return false;
      }
    }


    internal bool Post(string exchange, byte[] msg, string topic)
    {
        // Пытаемся взять готовый канал из пула без блокировок
        if (!_rabbitChannels.TryPop(out IModel channel))
        {
            // Если свободных каналов в пуле НЕ ОСТАЛОСЬ, мы не падаем в ошибку!
            // Мы просто создаем ОДИН новый канал для этого конкретного потока
            lock (RabbitConn)
            {
                channel = RabbitConn.CreateModel();
            }
        }

        try
        {
            // Публикуем сообщение (теперь у каждого потока строго свой канал)
            channel.BasicPublish(exchange, topic, false, null, msg);

            // Возвращаем его в пул — теперь пул сам вырос и готов к большей нагрузке
            _rabbitChannels.Push(channel);
            return true;
        }
        catch (Exception)
        {
            // Если была ошибка сети, возвращаем канал, чтобы не потерять объект
            if (channel != null) _rabbitChannels.Push(channel);
            throw;
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
