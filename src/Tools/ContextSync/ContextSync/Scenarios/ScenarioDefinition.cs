namespace ContextSync.Scenarios;

public sealed class ScenarioDefinition
{
    public string Name { get; set; } = "unknown";
    public string Description { get; set; } = "";
    public int Version { get; set; } = 1;
    public ScenarioConnection Connection { get; set; } = new();
    public Dictionary<string, object?> Inputs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object?> Defaults { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public ScenarioFileFormat? FileFormat { get; set; }
    public List<ScenarioStep> Steps { get; set; } = new();
}

public sealed class ScenarioConnection
{
    public ScenarioJiraConnection? Jira { get; set; }
}

public sealed class ScenarioJiraConnection
{
    public string Url { get; set; } = "";
    public string Username { get; set; } = "";
    public string Token { get; set; } = "";
    public string PasswordProtected { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
}

public sealed class ScenarioFileFormat
{
    public string DateLineRegex { get; set; } = "";
    public string EntryLineRegex { get; set; } = "";
    public bool SkipBlank { get; set; } = true;
    public string DateFormat { get; set; } = "yyyyMMdd";
    public string Encoding { get; set; } = "UTF-8";
}

public sealed class ScenarioStep
{
    public string Id { get; set; } = "";
    public string Action { get; set; } = "";
    public string? ForEach { get; set; }
    public bool Flatten { get; set; }
    public string OnError { get; set; } = "stop";
    public Dictionary<string, object?> Args { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
