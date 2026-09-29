using ContextSync.dal.Models;

namespace ContextSync.Abstractions;

public interface IConfluenceService
{
    Task<WikiPageResult> CreatePageAsync(SourceDocument doc, CancellationToken cancellationToken = default);
    Task<WikiPageResult> UpdatePageAsync(long pageId, SourceDocument doc, CancellationToken cancellationToken = default);
    Task<WikiPageResult?> FindPageAsync(string spaceKey, string title, CancellationToken cancellationToken = default);
    Task<List<WikiPageResult>> CreatePagesAsync(IEnumerable<SourceDocument> docs, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
