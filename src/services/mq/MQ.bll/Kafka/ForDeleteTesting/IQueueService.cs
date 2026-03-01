namespace MQ.bll.Kafka.ForDeleteTesting
{
    public interface IQueueService
    {
        Task GetAllMessages(CancellationTokenSource cts, string table);
        Task SendAllMessages(CancellationTokenSource cts);
    }
}
