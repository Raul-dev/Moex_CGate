using Spectre.Console.Cli;
using System.ComponentModel;

namespace ContextSync.Features.SyncJira;

public class SyncJiraSettings : CommandSettings
{
    [Description("Input directory with text files")]
    [CommandOption("-i|--input <DIR>")]
    [DefaultValue("input")]
    public string InputDirectory { get; set; } = "input";

    [Description("File pattern (default: *.txt)")]
    [CommandOption("--pattern <PATTERN>")]
    [DefaultValue("*.txt")]
    public string FilePattern { get; set; } = "*.txt";

    [Description("Jira project key (overrides appsettings)")]
    [CommandOption("-p|--project <KEY>")]
    public string? ProjectKey { get; set; }

    [Description("Jira issue type (default: Task)")]
    [CommandOption("--type <TYPE>")]
    public string? IssueType { get; set; }

    [Description("Jira assignee")]
    [CommandOption("-a|--assignee <USER>")]
    public string? Assignee { get; set; }

    [Description("Jira epic key")]
    [CommandOption("-e|--epic <KEY>")]
    public string? EpicKey { get; set; }

    [Description("Jira priority (default: Medium)")]
    [CommandOption("--priority <P>")]
    public string? Priority { get; set; }

    [Description("Jira labels (comma-separated)")]
    [CommandOption("-l|--labels <LABELS>")]
    public string? Labels { get; set; }

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
