using ContextSync.Abstractions;
using ContextSync.dal.Models;
using Serilog;

namespace ContextSync.Services;

public class SyncOrchestrator : ISyncOrchestrator
{
    private readonly IJiraService _jiraService;
    private readonly IConfluenceService _confluenceService;
    private readonly IHtmlGeneratorService _htmlGenerator;

    public SyncOrchestrator(
        IJiraService jiraService,
        IConfluenceService confluenceService,
        IHtmlGeneratorService htmlGenerator)
    {
        _jiraService = jiraService;
        _confluenceService = confluenceService;
        _htmlGenerator = htmlGenerator;
    }

    public async Task<SyncResult> SyncAllAsync(
        IEnumerable<SourceDocument> documents,
        bool syncJira,
        bool syncWiki,
        bool generateHtml,
        string? htmlOutputDir = null,
        CancellationToken cancellationToken = default)
    {
        var result = new SyncResult();
        var docList = documents.ToList();
        result.TotalDocuments = docList.Count;

        Log.Information("SyncAll: {Count} documents | Jira={Jira} Wiki={Wiki} Html={Html}",
            docList.Count, syncJira, syncWiki, generateHtml);

        if (syncJira)
        {
            result.JiraResults = await _jiraService.CreateIssuesAsync(docList, cancellationToken);
            Log.Information("Jira sync: {Success} ok, {Errors} errors",
                result.JiraResults.Count(r => r.Success), result.JiraResults.Count(r => !r.Success));
        }

        if (syncWiki)
        {
            result.WikiResults = await _confluenceService.CreatePagesAsync(docList, cancellationToken);
            Log.Information("Wiki sync: {Success} ok, {Errors} errors",
                result.WikiResults.Count(r => r.Success), result.WikiResults.Count(r => !r.Success));
        }

        if (generateHtml)
        {
            var outputDir = htmlOutputDir ?? "output/html";
            result.HtmlResults = await _htmlGenerator.GenerateManyAsync(docList, outputDir, null, cancellationToken);
            Log.Information("HTML generation: {Success} ok, {Errors} errors",
                result.HtmlResults.Count(r => r.Success), result.HtmlResults.Count(r => !r.Success));
        }

        return result;
    }
}
