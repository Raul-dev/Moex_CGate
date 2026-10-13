using ContextSync.dal.Models;

namespace ContextSync.Abstractions;

public interface IHtmlGeneratorService
{
    Task<HtmlFileResult> GenerateAsync(SourceDocument doc, string outputDir, string? template = null, CancellationToken cancellationToken = default);
    Task<List<HtmlFileResult>> GenerateManyAsync(IEnumerable<SourceDocument> docs, string outputDir, string? template = null, CancellationToken cancellationToken = default);
    string RenderTemplate(SourceDocument doc, string? template = null);
}
