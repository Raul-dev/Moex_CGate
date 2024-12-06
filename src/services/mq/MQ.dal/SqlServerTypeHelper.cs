using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQ.dal
{
    public enum SqlServerType
    {
        mssql,
        psql,
        osql,
        sqlite,
        clickhouse,
        xdto,
        unknown
    }
    public static class SqlServerTypeHelper
    {
        public static SqlServerType GetTypeFromString(string serverType)
        {
            switch (serverType)
            {
                case "mssql":
                    return SqlServerType.mssql;
                case "psql":
                    return SqlServerType.psql;
                case "osql":
                    return SqlServerType.osql;
                case "sqlite":
                    return SqlServerType.sqlite;
                case "clickhouse":
                    return SqlServerType.clickhouse;
                default: 
                    return SqlServerType.unknown;
            }
        }
        public static string GetString(SqlServerType serverType)
        {
            switch (serverType)
            {
                case SqlServerType.mssql:
                    return "mssql";
                case SqlServerType.psql:
                    return "psql";
                case SqlServerType.osql:
                    return "osql";
                case SqlServerType.sqlite:
                    return "sqlite";
                case SqlServerType.clickhouse:
                    return "clickhouse";
                default:
                    return "unknown";
            }
            
        }
    }
}
