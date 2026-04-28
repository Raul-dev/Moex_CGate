using CommandLine;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApplyProcLog
{
    [Verb("generate", HelpText = "Генерация процедур.")]
    public class CmdOption
    {
        [Option('s', "Database Server name.", Required = false, Default = "localhost", HelpText = "Database Server name.")]
        public string ServerName { get; set; } = "";

        [Option('d', "Database name.", Required = false, Default = @"DBTest", HelpText = "Database name.")]
        public string DatabaseName { get; set; } = "";

        [Option('t', "DB server type.", Required = false, Default = "mssql", HelpText = "mssql или psql.")]
        public string ServerType { get; set; } = "";

        [Option('p', "Port of Database server.", Required = false, Default = "54321", HelpText = "Port of Database server.")]
        public string Port { get; set; } = "";

        [Option('u', "Database User.", Required = false, Default = "", HelpText = "Database User.")]
        public string User { get; set; } = "";

        [Option('w', "Database Password.", Required = false, Default = "postgres", HelpText = "Database Password.")]
        public string Password { get; set; } = "";

        [Option("clean", Required = false, Default = false, HelpText = "Очистить папки Proc и Original перед генерацией.")]
        public bool Clean { get; set; }

        [Option("generate", Required = false, Default = false, HelpText = "Генерация процедур.")]
        public bool Generate { get; set; }

        [Option("filter", Required = false, Default = "%", HelpText = "Маска процедур (по умолчанию %).")]
        public string Filter { get; set; } = "%";

        [Option("except", Required = false, Default = "audit%", HelpText = "Маска исключения схемы (по умолчанию audit%).")]
        public string ExceptFilter { get; set; } = "audit%";

        [Option("use-config", Required = false, Default = false, HelpText = "Использовать список процедур из appsettings.json (ProcedureSettings.Procedures).")]
        public bool UseConfig { get; set; }
    }

}
