using Spectre.Console.Cli;
using System.ComponentModel;

namespace ContextSync.Features.SchemaHtml;

public class SchemaHtmlSettings : CommandSettings
{
    [Description("Server name (required)")]
    [CommandOption("-s|--server <SERVER>")]
    public string? Server { get; set; }

    [Description("Database name (required)")]
    [CommandOption("-d|--database <DB>")]
    public string? Database { get; set; }

    [Description("Specific table name to export")]
    [CommandOption("-t|--table <TABLE>")]
    public string? TableName { get; set; }

    [Description("Table filter (SQL LIKE pattern, e.g. 'tbl%')")]
    [CommandOption("-f|--filter <PATTERN>")]
    public string? TableFilter { get; set; }

    [Description("Output directory for HTML files")]
    [CommandOption("-o|--output <DIR>")]
    [DefaultValue("output/schema")]
    public string OutputDirectory { get; set; } = "output/schema";

    [Description("Scriban template file name (in Templates/ folder)")]
    [CommandOption("--template <FILE>")]
    [DefaultValue("table-schema.sbnhtml")]
    public string TemplateName { get; set; } = "table-schema.sbnhtml";

    [Description("Templates directory path")]
    [CommandOption("--templates-dir <DIR>")]
    [DefaultValue("Templates")]
    public string TemplatesDir { get; set; } = "Templates";

    [Description("SQL user (leave empty for Integrated Security)")]
    [CommandOption("--user <USER>")]
    public string? User { get; set; }

    [Description("SQL password")]
    [CommandOption("--password <PWD>")]
    public string? Password { get; set; }

    [Description("Command timeout in seconds (default: 60)")]
    [CommandOption("--timeout <SEC>")]
    [DefaultValue(60)]
    public int TimeoutSeconds { get; set; } = 60;
}
