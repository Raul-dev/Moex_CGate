namespace ContextSync.Scenarios;

public sealed record JiraSearchIssue(string Key, string Summary, string Status);

public sealed record JiraWorklog(
    string IssueKey,
    string WorklogId,
    string Author,
    string Started,
    string Date,
    double Hours,
    string? Comment);

public sealed record JiraWorklogRequest(
    string IssueKey,
    string Started,
    string TimeSpent,
    string? Comment,
    Dictionary<string, object?>? ExtraFields);

public sealed record JiraWorklogResult(
    bool Success,
    string? WorklogId,
    string? Error);

public sealed record JiraTimesheetRow(string Date, string Issue, string Summary, double Hours);

public sealed record JiraTempoWorklogRow(string Date, string IssueKey, double Hours, string Account, string Comment);
