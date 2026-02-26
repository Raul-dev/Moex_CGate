using MQ.dal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQ.bll.Common
{
#pragma warning disable CS8618
    public class DataBaseSettings
    {
        
        public string ServerName { get; set; }
        public int Port { get; set; }
        public SqlServerType ServerType { get; set; } = SqlServerType.mssql;
        public string ClientName { get; set; }
        public string DataBase { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public SessionModeEnum SessionMode { get; set; }
        public string GetConnection()
        {
            if (ServerType == SqlServerType.mssql)
            {
                return $"Server={ServerName};Database={DataBase};User Id={User};Password={Password};Trusted_Connection=False;MultipleActiveResultSets=true;TrustServerCertificate=True";
            }
            if (ServerType == SqlServerType.psql)
                return $"Host={ServerName};Port={Port};Database={DataBase};Username={User};Password={Password}";

            throw new NotImplementedException();
        }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
