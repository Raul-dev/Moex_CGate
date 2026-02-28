using RabbitMQSqlClr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQTestApp
{
  class Program
  {
    static void Main(string[] args)
    {

      //set the local connection string
      RabbitMQSqlServer.LocalhostConnectionString = "server = localhost; database = CGate; uid = CGateUser; pwd = MyPassword321";

	  RabbitMQSqlServer.sp_clr_InitialiseRabbitMq();
      Console.WriteLine("Rabbit is initialised. Press any key to send msg");
      //Console.ReadLine();
   	  RabbitMQSqlServer.sp_clr_PostRabbitMsg(2, "Hello World");

      Console.WriteLine("Message posted. Press any key to exit");
      Console.ReadLine();

    }
  }
}
