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
        public string ServerName { get; init; }
        public string Port { get; init; }
        public string ServerType { get; init; }
        public string ClientName { get; init; }
        public string DataBase { get; init; }
        public string User { get; init; }
        public string Password { get; init; }
        public string SessionMode { get; init; }
        public string GetConnection()
        {
            if (string.IsNullOrEmpty(ServerType) || ServerType != "psql")
            {
                return $"Server={ServerName};Database={DataBase};User Id={User};Password={Password};Trusted_Connection=False;MultipleActiveResultSets=true;TrustServerCertificate=True";
            }else
                return $"Host={ServerName};Port={Port};Database={DataBase};Username={User};Password={Password}";
        }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
