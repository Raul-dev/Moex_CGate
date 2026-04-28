using CommandLine;

namespace ApplyProcLog;

public class ExecOptions
{
    [Verb("exec-file", HelpText = "Выполнить один SQL-файл")]
    public class ExecFile
    {
        [Option('f', "file", Required = true, HelpText = "Путь к SQL-файлу")]
        public string FilePath { get; set; } = "";

        [Option('s', "server", Required = false, Default = "localhost", HelpText = "Имя сервера")]
        public string ServerName { get; set; } = "localhost";

        [Option('d', "database", Required = false, Default = "DBTest", HelpText = "Имя базы данных")]
        public string DatabaseName { get; set; } = "DBTest";

        [Option('t', "timeout", Required = false, Default = 300, HelpText = "Таймаут команды (сек)")]
        public int Timeout { get; set; } = 300;
    }

    [Verb("exec-folder", HelpText = "Выполнить SQL-файлы из папки")]
    public class ExecFolder
    {
        [Option('f', "folder", Required = false, Default = "PROC", HelpText = "Путь к папке с .sql файлами")]
        public string Folder { get; set; } = "PROC";

        [Option('s', "server", Required = false, Default = "localhost", HelpText = "Имя сервера")]
        public string ServerName { get; set; } = "localhost";

        [Option('d', "database", Required = false, Default = "DBTest", HelpText = "Имя базы данных")]
        public string DatabaseName { get; set; } = "DBTest";

        [Option('t', "timeout", Required = false, Default = 300, HelpText = "Таймаут команды (сек)")]
        public int Timeout { get; set; } = 300;
    }

    [Verb("exec-all", HelpText = "Применить PROC (Table + Base + Original)")]
    public class ExecAll
    {
        [Option('s', "server", Required = false, Default = "localhost", HelpText = "Имя сервера")]
        public string ServerName { get; set; } = "localhost";

        [Option('d', "database", Required = false, Default = "DBTest", HelpText = "Имя базы данных")]
        public string DatabaseName { get; set; } = "DBTest";

        [Option('p', "proc-folder", Required = false, HelpText = "Путь к папке PROC (по умолчанию: текущая директория/PROC)")]
        public string? ProcFolder { get; set; }

        [Option('t', "timeout", Required = false, Default = 300, HelpText = "Таймаут команды (сек)")]
        public int Timeout { get; set; } = 300;

        [Option("no-table", Required = false, HelpText = "Не применять Table")]
        public bool NoTable { get; set; }

        [Option("no-base", Required = false, HelpText = "Не применять Base")]
        public bool NoBase { get; set; }

        [Option("no-original", Required = false, HelpText = "Не применять Original")]
        public bool NoOriginal { get; set; }
    }

    [Verb("export-data", HelpText = "Экспорт данных таблиц в SQL-файлы (INSERT)")]
    public class ExportData
    {
        [Option('s', "server", Required = false, Default = "localhost", HelpText = "Имя сервера")]
        public string ServerName { get; set; } = "localhost";

        [Option('d', "database", Required = false, Default = "DBTest", HelpText = "Имя базы данных")]
        public string DatabaseName { get; set; } = "DBTest";

        [Option('t', "tables", Required = false, HelpText = "Фильтр таблиц (через запятую: dbo.Users,DBTest.Accounts или маска SQL: dbo.% или %.Accounts). Пусто = все.")]
        public string? Tables { get; set; }

        [Option('o', "output", Required = false, HelpText = "Выходная папка (по умолчанию: текущая директория/DataExport)")]
        public string? Output { get; set; }

        [Option("max-size", Required = false, Default = 200, HelpText = "Максимальный размер одного файла в КБ (по умолчанию 200)")]
        public int MaxSizeKb { get; set; } = 200;

        [Option("batch-size", Required = false, Default = 1000, HelpText = "Количество строк за один запрос (память)")]
        public int BatchSize { get; set; } = 1000;

        [Option("append-go", Required = false, HelpText = "Добавлять GO после каждого INSERT")]
        public bool AppendGo { get; set; }

        [Option("include-schema", Required = false, Default = true, HelpText = "Включать имя схемы в имя файла")]
        public bool IncludeSchema { get; set; } = true;

        [Option("exclude-types", Required = false, HelpText = "Исключить типы колонок (через запятую: varbinary,xml,text). По умолчанию: varbinary,binary,image,xml,text,ntext")]
        public string? ExcludeTypes { get; set; }
    }
}
