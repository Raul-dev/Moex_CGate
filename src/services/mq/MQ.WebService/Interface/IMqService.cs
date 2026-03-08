namespace MQ.WebService.Interface
{
    public interface IMqService
    {
        Task Start(IConfiguration configuration, string? sessionMode = null);
    }
}
