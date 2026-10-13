namespace ContextSync.dal.Models;

public class JiraIssueResult
{
    public string Key { get; set; } = "";
    public string Id { get; set; } = "";
    public string Url { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class WikiPageResult
{
    public string Title { get; set; } = "";
    public string SpaceKey { get; set; } = "";
    public long? PageId { get; set; }
    public string Url { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class HtmlFileResult
{
    public string Title { get; set; } = "";
    public string FilePath { get; set; } = "";
    public long SizeBytes { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class SyncResult
{
    public List<JiraIssueResult> JiraResults { get; set; } = new();
    public List<WikiPageResult> WikiResults { get; set; } = new();
    public List<HtmlFileResult> HtmlResults { get; set; } = new();
    public int TotalDocuments { get; set; }
    public int TotalSuccess => JiraResults.Count(r => r.Success) + WikiResults.Count(r => r.Success) + HtmlResults.Count(r => r.Success);
    public int TotalErrors => JiraResults.Count(r => !r.Success) + WikiResults.Count(r => !r.Success) + HtmlResults.Count(r => !r.Success);
    public bool HasErrors => TotalErrors > 0;
}
