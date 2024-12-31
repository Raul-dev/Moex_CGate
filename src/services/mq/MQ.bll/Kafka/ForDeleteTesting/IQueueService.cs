namespace MQ.bll.Kafka.ForDeleteTesting
{
    public interface IQueueService
    {
        Task GetAllMessages(CancellationTokenSource cts);
        Task SendAllMessages(CancellationTokenSource cts);
    }
}
