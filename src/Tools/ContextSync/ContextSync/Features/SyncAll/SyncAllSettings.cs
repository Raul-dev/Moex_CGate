using Spectre.Console.Cli;
using System.ComponentModel;

namespace ContextSync.Features.SyncAll;

public class SyncAllSettings : CommandSettings
{
    [Description("Input directory with text files")]
    [CommandOption("-i|--input <DIR>")]
    [DefaultValue("input")]
    public string InputDirectory { get; set; } = "input";

    [Description("File pattern (default: *.txt)")]
    [CommandOption("--pattern <PATTERN>")]
    [DefaultValue("*.txt")]
    public string FilePattern { get; set; } = "*.txt";

    [Description("Skip Jira sync")]
    [CommandOption("--no-jira")]
    [DefaultValue(false)]
    public bool SkipJira { get; set; }

    [Description("Skip Confluence wiki sync")]
    [CommandOption("--no-wiki")]
    [DefaultValue(false)]
    public bool SkipWiki { get; set; }

    [Description("Skip HTML generation")]
    [CommandOption("--no-html")]
    [DefaultValue(false)]
    public bool SkipHtml { get; set; }

    [Description("HTML output directory")]
    [CommandOption("--html-output <DIR>")]
    [DefaultValue("output/html")]
    public string HtmlOutputDirectory { get; set; } = "output/html";

    [Description("Read from database instead of files")]
    [CommandOption("--from-db")]
    [DefaultValue(false)]
    public bool FromDatabase { get; set; }

    [Description("Server name (for --from-db)")]
    [CommandOption("-s|--server <SERVER>")]
    public string? Server { get; set; }

    [Description("Database name (for --from-db)")]
    [CommandOption("-d|--database <DB>")]
    public string? Database { get; set; }

    [Description("SQL query (for --from-db)")]
    [CommandOption("-q|--query <SQL>")]
    public string? Query { get; set; }
}
