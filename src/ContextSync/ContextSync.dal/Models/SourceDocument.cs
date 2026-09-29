namespace ContextSync.dal.Models;

public class SourceDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string SourceType { get; set; } = "file";
    public string SourceLocation { get; set; } = "";
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;

    public string? JiraProjectKey { get; set; }
    public string? JiraIssueType { get; set; }
    public string? JiraAssignee { get; set; }
    public string? JiraEpicKey { get; set; }
    public string? JiraPriority { get; set; }
    public List<string> JiraLabels { get; set; } = new();

    public string? WikiSpaceKey { get; set; }
    public string? WikiParentTitle { get; set; }
    public string? HtmlOutputPath { get; set; }
    public string? HtmlTemplate { get; set; }
}
