using ContextSync.dal.Models;

namespace ContextSync.Abstractions;

public interface ISyncOrchestrator
{
    Task<SyncResult> SyncAllAsync(IEnumerable<SourceDocument> documents, bool syncJira, bool syncWiki, bool generateHtml, string? htmlOutputDir = null, CancellationToken cancellationToken = default);
}
