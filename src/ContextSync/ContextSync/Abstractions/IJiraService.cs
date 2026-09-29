using ContextSync.dal.Models;
using ContextSync.Scenarios;

namespace ContextSync.Abstractions;

public interface IJiraService
{
    Task<JiraIssueResult> CreateIssueAsync(SourceDocument doc, CancellationToken cancellationToken = default);
    Task<JiraIssueResult> UpdateIssueAsync(string issueKey, SourceDocument doc, CancellationToken cancellationToken = default);
    Task<bool> IssueExistsAsync(string issueKey, CancellationToken cancellationToken = default);
    Task<List<JiraIssueResult>> CreateIssuesAsync(IEnumerable<SourceDocument> docs, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    Task<List<JiraSearchIssue>> SearchAsync(string jql, int maxResults = 200, CancellationToken cancellationToken = default);
    Task<List<JiraWorklog>> GetWorklogsAsync(string issueKey, CancellationToken cancellationToken = default);
    Task<JiraWorklogResult> AddWorklogAsync(JiraWorklogRequest request, CancellationToken cancellationToken = default);
    Task<List<JiraTimesheetRow>> GetTimesheetReportAsync(string reportUrl, CancellationToken cancellationToken = default);
    Task<List<JiraTempoWorklogRow>> GetTempoWorklogsAsync(string username, DateOnly dateFrom, DateOnly dateTo, CancellationToken cancellationToken = default);
}
