using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQ.dal
{
    public class DBConnection
    {
        public string Server;
        string DataBase;
        string User;
        string Password;
        string AppType;
        bool IntegratedSecurity;

        public string SSISPackageName { get; set; }

        public DBConnection(string dataBase, string server = "localhost", string appType = "Landing", string user = "", string password = "", bool integratedSecurity = true)
        {
            Server = server;
            DataBase = dataBase;
            User = user;
            Password = password;
            IntegratedSecurity = integratedSecurity;
            AppType = appType;

        }
        public void Copy(DBConnection obj)
        {
            User = obj.User;
            Password = obj.Password;
            IntegratedSecurity = obj.IntegratedSecurity;
            if (obj.SSISPackageName != null)
                SSISPackageName = obj.SSISPackageName;
            //obj.AppType = AppType;
        }

        public string GetName()
        {
            return GetConnectionName(DataBase, Server, AppType);
        }
        static public string GetConnectionName(string dataBase, string server = "localhost", string appType = "Landing")
        {
            return server + "_" + dataBase + "_" + appType;
        }
        public string GetADONetConnectionstring()
        {
            string con;
            if (IntegratedSecurity)
                con = $"Data Source = {Server}; Initial Catalog = {DataBase}; Max Pool Size = 600; Connect Timeout = 300; Integrated Security = True;";
            else
                con = $"Data Source = {Server}; Initial Catalog = {DataBase}; Max Pool Size = 600; Connect Timeout = 300; User ID = {User}; Integrated Security = False; Password={Password}";
            //; Persist Security Info = True, Password={Password};
            return con;
        }
        public string GetSqlNetConnectionstring()
        {
            string con;
            if (IntegratedSecurity)
                //con = $"Server ={Server}; Database={DataBase}; Trusted_Connection = True;Connection Lifetime=3000;Max Pool Size=300;";
                con = $"Server ={Server}; Database={DataBase}; Trusted_Connection = True;Connection Timeout = 1000000;Connection Lifetime=300000;Max Pool Size=3;";

            else
                //con = $"Server ={Server}; Database={DataBase}; Trusted_Connection = False;Connection Lifetime=3;Max Pool Size=3; User ID = {User}; Password={Password};";
                con = $"Server ={Server}; Database={DataBase}; Trusted_Connection = False; ConnectionTimeout = 1000000; User ID = {User}; Password={Password};";
            return con;
        }

        public string GetOleDBConnectionstring()
        {
            string con;
            if (IntegratedSecurity)
                con = $"Provider = SQLNCLI11.1; Auto Translate = False;ServerName = {Server}; InitialCatalog = {DataBase};";
            else
                con = $"Provider = SQLNCLI11.1; Auto Translate = False; ServerName = {Server}; InitialCatalog = {DataBase}; UserName = {User}; Password = {Password}";
            return con;
        }



    }
}
