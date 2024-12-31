using Confluent.Kafka.Admin;
using Confluent.Kafka;
using Serilog;

namespace MQ.bll.Kafka
{
    public enum OnMissingChannel
    {
        Create = 0,
        Validate = 1,
        Assume = 2
    }
    //internal class KafkaGateway
    public class KafkaGateway
    {
        //protected static readonly ILogger s_logger = ApplicationLogging.CreateLogger<KafkaMessageProducer>();
        protected ClientConfig _clientConfig;
        protected OnMissingChannel MakeChannels;
        protected string Topic;
        protected int NumPartitions;
        protected short ReplicationFactor;
        protected TimeSpan TopicFindTimeout;

        protected void EnsureTopic()
        {
            if (MakeChannels == OnMissingChannel.Assume)
                return;

            if (MakeChannels == OnMissingChannel.Validate || MakeChannels == OnMissingChannel.Create)
            {
                var exists = FindTopic();

                if (!exists && MakeChannels == OnMissingChannel.Validate)
                    throw new Exception($"Topic: {Topic} does not exist");

                if (!exists && MakeChannels == OnMissingChannel.Create)
                    MakeTopic().GetAwaiter().GetResult();
            }
        }
        public static List<TopicPartition> GetTopicPartitions(string bootstrapServers, string topicValue)
        {
            AdminClientConfig adminClientConfig = new AdminClientConfig { BootstrapServers = bootstrapServers };
            using (var adminClient = new AdminClientBuilder(adminClientConfig).Build())
            {
                var meta = adminClient.GetMetadata(TimeSpan.FromSeconds(20));
                TopicMetadata? topicMetadata = meta.Topics.SingleOrDefault(t => topicValue.Equals(t.Topic));
                if (topicMetadata != null)
                {
                    return topicMetadata.Partitions
                        .Select(x => new TopicPartition(topicMetadata.Topic, x.PartitionId))
                        .ToList();
                }
            }
            return new List<TopicPartition>();
        }
        private async Task MakeTopic()
        {
            using var adminClient = new AdminClientBuilder(_clientConfig).Build();
            try
            {
                await adminClient.CreateTopicsAsync(new List<TopicSpecification>
                {
                    new TopicSpecification
                    {
                        Name = Topic,
                        NumPartitions = NumPartitions,
                        ReplicationFactor = ReplicationFactor
                    }
                });
            }
            catch (CreateTopicsException e)
            {
                if (e.Results[0].Error.Code != ErrorCode.TopicAlreadyExists)
                {
                    throw new Exception(
                        $"An error occured creating topic {Topic}: {e.Results[0].Error.Reason}");
                }

                Log.Debug("Topic {Topic} already exists", Topic);
            }
        }

        private bool FindTopic()
        {
            using var adminClient = new AdminClientBuilder(_clientConfig).Build();
            try
            {
                bool found = false;

                var metadata = adminClient.GetMetadata(Topic, TopicFindTimeout);
                //confirm we are in the list
                var matchingTopics = metadata.Topics.Where(tp => tp.Topic == Topic).ToArray();
                if (matchingTopics.Length > 0)
                {
                    var matchingTopic = matchingTopics[0];

                    //was it found?
                    found = matchingTopic.Error != null && matchingTopic.Error.Code != ErrorCode.UnknownTopicOrPart;
                    if (found)
                    {
                        //is it in error, and does it have required number of partitions or replicas
                        bool inError = matchingTopic.Error != null && matchingTopic.Error.Code != ErrorCode.NoError;
                        bool matchingPartitions = matchingTopic.Partitions.Count == NumPartitions;
                        bool replicated =
                            matchingTopic.Partitions.All(
                                partition => partition.Replicas.Length == ReplicationFactor);

                        bool valid = !inError && matchingPartitions && replicated;

                        if (!valid)
                        {
                            string error = "Topic exists but does not match publication: ";
                            //if topic is in error
                            if (inError)
                            {
                                error += $" topic is in error => {matchingTopic.Error.Code};";
                            }

                            if (!matchingPartitions)
                            {
                                error +=
                                    $"topic is misconfigured => NumPartitions should be {NumPartitions} but is {matchingTopic.Partitions.Count};";
                            }

                            if (!replicated)
                            {
                                error +=
                                    $"topic is misconfigured => ReplicationFactor should be {ReplicationFactor} but is {matchingTopic.Partitions[0].Replicas.Length};";
                            }

                            Log.Warning(error);
                        }
                    }
                }

                if (found)
                    Log.Information($"Topic {Topic} exists");

                return found;
            }
            catch (Exception e)
            {
                throw new Exception($"Error finding topic {Topic}", e);
            }
        }
    }
}
