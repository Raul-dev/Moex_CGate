using ContextSync.dal.Settings;

namespace ContextSync.Infrastructure.Settings;

public class AppSettings
{
    public JiraSettings Jira { get; set; } = new();
    public ConfluenceSettings Confluence { get; set; } = new();
    public DatabaseSettings Database { get; set; } = new();
    public HtmlSettings Html { get; set; } = new();
    public FileSourceSettings FileSource { get; set; } = new();
    public SerilogSettings? Serilog { get; set; }
}

public class JiraSettings
{
    public string Url { get; set; } = "";
    public string Username { get; set; } = "";
    public string ApiToken { get; set; } = "";
    public string DefaultProjectKey { get; set; } = "";
    public string DefaultIssueType { get; set; } = "Task";
    public string DefaultPriority { get; set; } = "Medium";
    public int BatchSize { get; set; } = 10;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
}

public class ConfluenceSettings
{
    public string Url { get; set; } = "";
    public string Username { get; set; } = "";
    public string ApiToken { get; set; } = "";
    public string DefaultSpaceKey { get; set; } = "";
    public string DefaultParentTitle { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
}

public class HtmlSettings
{
    public string OutputDirectory { get; set; } = "output/html";
    public string Template { get; set; } = "default";
    public bool IncludeStyles { get; set; } = true;
    public bool GroupByMetadata { get; set; } = false;
}

public class FileSourceSettings
{
    public string InputDirectory { get; set; } = "input";
    public string FilePattern { get; set; } = "*.txt";
    public bool Recursive { get; set; } = true;
    public string Encoding { get; set; } = "UTF-8";
}

public class SerilogSettings
{
    public string MinimumLevel { get; set; } = "Information";
    public string LogFile { get; set; } = "logs/ContextSync-.log";
}
