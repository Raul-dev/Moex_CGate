using CommandLine;
using MQ.bll.Common;
using MQ.dal;
using Serilog;

namespace MQ.OptionModels
{
    public class BaseOptions
    {
        [Option('s', "Database Server name.", Required = false, Default = "localhost", HelpText = "Database Server name.")]
        public string ServerName { get; set; }

        [Option('d', "Database name.", Required = false, Default = @"CGate", HelpText = "Database name.")] 
        public string DatabaseName { get; set; }

        [Option('t', "DB server type.", Required = false, Default = "mssql", HelpText = "mssql или psql.")]
        public string ServerType { get; set; }

        [Option('p', "Port of Database server.", Required = false, Default = "54321", HelpText = "Port of Database server.")]
        public string Port { get; set; }

        [Option('u', "Database User.", Required = false, Default = "", HelpText = "Database User.")]
        public string User { get; set; }

        [Option('w', "Database Password.", Required = false, Default = "postgres", HelpText = "Database Password.")]
        public string Password { get; set; }

        [Option('k', "is kafka.", Required = false, Default = false, HelpText = "is kafka? if not it is rabbit.")]
        public bool? IsKafka { get; set; }

        public virtual void InitBllOption(BllOption blloption)
        {

            blloption.ServerName = ServerName;
            blloption.DatabaseName = DatabaseName;

            blloption.ServerType = SqlServerTypeHelper.GetTypeFromString(ServerType);
            
            try
            {
                blloption.Port = int.Parse(Port);
            }
            catch (Exception)
            {
                Log.Error("Port must be a number. Cant convert [0]", Port);
            }
            //For default user postgres
            if(blloption.ServerType == SqlServerType.psql && String.IsNullOrEmpty(User))
            {
                blloption.User = "postgres";
            }
            else
                blloption.User = User;

            blloption.Password = Password;
            blloption.IsKafka = IsKafka??false;
        }
    }
}
