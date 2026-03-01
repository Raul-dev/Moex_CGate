using MQ.dal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQ.bll.Common
{
#pragma warning disable CS8618
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class DataBaseSettings
    {
        
        public string ServerName { get; set; }
        public int Port { get; set; }
        public SqlServerType ServerType { get; set; } = SqlServerType.mssql;
        public string DataBase { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public SessionModeEnum SessionMode { get; set; }
        public string Filter { get; set; } = "CGateJson";
        public int DataSourceID { get; set; } = 1;
        public int MetaAdapterId { get; set; } = 1;
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
        private string GetDebuggerDisplay()
        {
            return ToString();
        }
        public override string ToString()
        {
            string ret = "{";
            ret += ServerName ?? "";
            ret += ",";
            ret += DataBase ?? "";
            ret += "}";
            return ret;
        }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
