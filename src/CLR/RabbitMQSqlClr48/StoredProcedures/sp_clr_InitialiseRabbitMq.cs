using System;


namespace RabbitMQSqlClr
{
  public partial class RabbitMQSqlServer
  {

    public static void sp_clr_InitialiseRabbitMq()
    {
      try
      {

        lock(_lockInitialise)
        {
          if(!_isInitialised)
          {
            Initialise();
            _isInitialised = true;
          }
        }

      }
      catch(Exception ex)
      {
        throw;
      }

    }

  }
}
