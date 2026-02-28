
namespace RabbitMQSqlClr
{
  public partial class RabbitMQSqlServer
  {
    public static void sp_clr_ReloadRabbitEndpoints()
    {
      try
      {
        if(!_isInitialised)
        {
          sp_clr_InitialiseRabbitMq();
          return;
        }
        LoadRabbitEndpoints();
      }
      catch
      {
        throw;
      }

    }
  }
}
