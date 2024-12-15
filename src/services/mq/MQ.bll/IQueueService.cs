
namespace MQ.bll
{
    public interface IQueueService
    {
        Task GetAllMessages(CancellationTokenSource cts);
        Task SendAllMessages(CancellationTokenSource cts);
    }
}
