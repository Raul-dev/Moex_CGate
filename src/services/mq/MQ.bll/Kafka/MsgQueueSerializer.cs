using Confluent.Kafka;
using System.Text;
using System.Text.Json;
using static MQ.dal.DBHelper;

namespace MQ.bll.Kafka
{
    public class MsgQueueSerializer : ISerializer<MsgQueueItem>, IDeserializer<MsgQueueItem>
    {
        public MsgQueueItem Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        {
            return JsonSerializer.Deserialize<MsgQueueItem>(Encoding.UTF8.GetString(data))!;
        }

        public byte[] Serialize(MsgQueueItem data, SerializationContext context)
        {
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data));
        }
    }
}
