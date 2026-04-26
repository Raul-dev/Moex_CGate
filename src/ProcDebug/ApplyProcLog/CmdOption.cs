using CommandLine;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApplyProcLog
{
    [Verb("CmdOption", HelpText = "")]
    public class CmdOption
    {
        [Option('s', "Database Server name.", Required = false, Default = "localhost", HelpText = "Database Server name.")]
        public string ServerName { get; set; } = "";

        [Option('d', "Database name.", Required = false, Default = @"CGate", HelpText = "Database name.")]
        public string DatabaseName { get; set; } = "";

        [Option('t', "DB server type.", Required = false, Default = "mssql", HelpText = "mssql или psql.")]
        public string ServerType { get; set; } = "";

        [Option('p', "Port of Database server.", Required = false, Default = "54321", HelpText = "Port of Database server.")]
        public string Port { get; set; } = "";

        [Option('u', "Database User.", Required = false, Default = "", HelpText = "Database User.")]
        public string User { get; set; } = "";

        [Option('w', "Database Password.", Required = false, Default = "postgres", HelpText = "Database Password.")]
        public string Password { get; set; } = "";
    }

}
