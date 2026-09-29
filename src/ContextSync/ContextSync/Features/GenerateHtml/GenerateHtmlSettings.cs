using Spectre.Console.Cli;
using System.ComponentModel;

namespace ContextSync.Features.GenerateHtml;

public class GenerateHtmlSettings : CommandSettings
{
    [Description("Input directory with text files")]
    [CommandOption("-i|--input <DIR>")]
    [DefaultValue("input")]
    public string InputDirectory { get; set; } = "input";

    [Description("File pattern (default: *.txt)")]
    [CommandOption("--pattern <PATTERN>")]
    [DefaultValue("*.txt")]
    public string FilePattern { get; set; } = "*.txt";

    [Description("Output directory for HTML files")]
    [CommandOption("-o|--output <DIR>")]
    [DefaultValue("output/html")]
    public string OutputDirectory { get; set; } = "output/html";

    [Description("HTML template: default, minimal, report")]
    [CommandOption("-t|--template <NAME>")]
    [DefaultValue("default")]
    public string Template { get; set; } = "default";

    [Description("Read from database instead of files")]
    [CommandOption("--from-db")]
    [DefaultValue(false)]
    public bool FromDatabase { get; set; }

    [Description("Server name (for --from-db)")]
    [CommandOption("--server <SERVER>")]
    public string? Server { get; set; }

    [Description("Database name (for --from-db)")]
    [CommandOption("--database <DB>")]
    public string? Database { get; set; }

    [Description("SQL query (for --from-db)")]
    [CommandOption("-q|--query <SQL>")]
    public string? Query { get; set; }
}
