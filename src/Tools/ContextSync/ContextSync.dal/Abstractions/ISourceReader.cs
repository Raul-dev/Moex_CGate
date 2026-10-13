using ContextSync.dal.Models;

namespace ContextSync.dal.Abstractions;

public interface ISourceReader
{
    Task<List<SourceDocument>> ReadAsync(CancellationToken cancellationToken = default);
    string SourceType { get; }
}
