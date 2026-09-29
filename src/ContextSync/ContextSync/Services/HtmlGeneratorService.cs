using System.Text;
using ContextSync.Abstractions;
using ContextSync.dal.Models;
using Serilog;

namespace ContextSync.Services;

public class HtmlGeneratorService : IHtmlGeneratorService
{
    public string RenderTemplate(SourceDocument doc, string? template = null)
    {
        return template switch
        {
            "minimal" => RenderMinimal(doc),
            "report" => RenderReport(doc),
            _ => RenderDefault(doc)
        };
    }

    public async Task<HtmlFileResult> GenerateAsync(SourceDocument doc, string outputDir, string? template = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            var safeFileName = SanitizeFileName(doc.Title) + ".html";
            var filePath = Path.Combine(outputDir, safeFileName);

            var html = RenderTemplate(doc, template);
            await File.WriteAllTextAsync(filePath, html, new UTF8Encoding(false), cancellationToken);

            var size = new FileInfo(filePath).Length;
            Log.Information("HTML generated: {FilePath} ({Size} bytes)", filePath, size);

            return new HtmlFileResult
            {
                Title = doc.Title,
                FilePath = filePath,
                SizeBytes = size,
                Success = true
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "HTML generation error for {Title}", doc.Title);
            return new HtmlFileResult { Title = doc.Title, Success = false, Error = ex.Message };
        }
    }

    public async Task<List<HtmlFileResult>> GenerateManyAsync(IEnumerable<SourceDocument> docs, string outputDir, string? template = null, CancellationToken cancellationToken = default)
    {
        var results = new List<HtmlFileResult>();
        foreach (var doc in docs)
        {
            var result = await GenerateAsync(doc, outputDir, template, cancellationToken);
            results.Add(result);
        }
        return results;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var c in name)
        {
            sb.Append(invalid.Contains(c) ? '_' : c);
        }
        return sb.ToString();
    }

    private static string RenderDefault(SourceDocument doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("""<html lang="ru">""");
        sb.AppendLine("<head>");
        sb.AppendLine("""  <meta charset="utf-8">""");
        sb.AppendLine($"  <title>{EscapeHtml(doc.Title)}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: -apple-system, Segoe UI, Roboto, sans-serif; max-width: 900px; margin: 40px auto; padding: 20px; color: #333; }");
        sb.AppendLine("    h1 { color: #1a1a2e; border-bottom: 2px solid #16213e; padding-bottom: 10px; }");
        sb.AppendLine("""    .metadata { background: #f5f5f5; padding: 12px; border-radius: 6px; margin: 16px 0; font-size: 0.9em; }""");
        sb.AppendLine("""    .metadata dt { font-weight: 600; display: inline; }""");
        sb.AppendLine("""    .metadata dd { display: inline; margin-left: 8px; color: #666; }""");
        sb.AppendLine("""    .source { color: #888; font-size: 0.85em; margin-top: 40px; border-top: 1px solid #eee; padding-top: 10px; }""");
        sb.AppendLine("""    pre { background: #f8f8f8; padding: 12px; border-radius: 4px; overflow-x: auto; }""");
        sb.AppendLine("""    ul { line-height: 1.6; }""");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine($"  <h1>{EscapeHtml(doc.Title)}</h1>");

        if (doc.Metadata.Count > 0)
        {
            sb.AppendLine("""  <div class="metadata">""");
            foreach (var kv in doc.Metadata)
            {
                sb.AppendLine($"    <dt>{EscapeHtml(kv.Key)}:</dt><dd>{EscapeHtml(kv.Value)}</dd><br>");
            }
            sb.AppendLine("  </div>");
        }

        sb.AppendLine("""  <div class="content">""");
        sb.AppendLine(ConvertTextToHtml(doc.Content));
        sb.AppendLine("  </div>");

        sb.AppendLine("""  <div class="source">""");
        sb.AppendLine($"    <p>Source: {EscapeHtml(doc.SourceType)} — {EscapeHtml(doc.SourceLocation)}</p>");
        sb.AppendLine($"    <p>Generated: {doc.ReadAt:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine("  </div>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string RenderMinimal(SourceDocument doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("""<html lang="ru">""");
        sb.AppendLine("<head>");
        sb.AppendLine("""  <meta charset="utf-8">""");
        sb.AppendLine($"  <title>{EscapeHtml(doc.Title)}</title>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine($"  <h1>{EscapeHtml(doc.Title)}</h1>");
        sb.AppendLine(ConvertTextToHtml(doc.Content));
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string RenderReport(SourceDocument doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("""<html lang="ru">""");
        sb.AppendLine("<head>");
        sb.AppendLine("""  <meta charset="utf-8">""");
        sb.AppendLine($"  <title>{EscapeHtml(doc.Title)}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("""    body { font-family: 'Segoe UI', sans-serif; max-width: 1000px; margin: 0 auto; padding: 30px; }""");
        sb.AppendLine("""    header { background: linear-gradient(135deg, #1a1a2e, #16213e); color: white; padding: 30px; border-radius: 8px; margin-bottom: 30px; }""");
        sb.AppendLine("""    header h1 { margin: 0; }""");
        sb.AppendLine("""    .meta-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin: 20px 0; }""");
        sb.AppendLine("""    .meta-item { background: #f0f4ff; padding: 10px 14px; border-radius: 4px; }""");
        sb.AppendLine("""    .meta-item .label { font-size: 0.8em; color: #666; text-transform: uppercase; }""");
        sb.AppendLine("""    .meta-item .value { font-weight: 600; }""");
        sb.AppendLine("""    .content { line-height: 1.7; }""");
        sb.AppendLine("""    footer { margin-top: 40px; color: #999; font-size: 0.85em; border-top: 1px solid #eee; padding-top: 12px; }""");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <header>");
        sb.AppendLine($"    <h1>{EscapeHtml(doc.Title)}</h1>");
        sb.AppendLine("  </header>");

        if (doc.Metadata.Count > 0)
        {
            sb.AppendLine("""  <div class="meta-grid">""");
            foreach (var kv in doc.Metadata)
            {
                sb.AppendLine("""    <div class="meta-item">""");
                sb.AppendLine($$"""      <div class="label">{{EscapeHtml(kv.Key)}}</div>""");
                sb.AppendLine($$"""      <div class="value">{{EscapeHtml(kv.Value)}}</div>""");
                sb.AppendLine("    </div>");
            }
            sb.AppendLine("  </div>");
        }

        sb.AppendLine("""  <div class="content">""");
        sb.AppendLine(ConvertTextToHtml(doc.Content));
        sb.AppendLine("  </div>");

        sb.AppendLine("  <footer>");
        sb.AppendLine($"    <p>Source: {EscapeHtml(doc.SourceType)} | Location: {EscapeHtml(doc.SourceLocation)} | Generated: {doc.ReadAt:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine("  </footer>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string ConvertTextToHtml(string text)
    {
        var escaped = EscapeHtml(text);
        var lines = escaped.Split('\n');
        var sb = new StringBuilder();
        bool inList = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r').Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                if (inList) { sb.AppendLine("</ul>"); inList = false; }
                continue;
            }

            if (trimmed.StartsWith("# ") || trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
            {
                if (!inList) { sb.AppendLine("<ul>"); inList = true; }
                sb.AppendLine($"  <li>{trimmed.Substring(2)}</li>");
            }
            else
            {
                if (inList) { sb.AppendLine("</ul>"); inList = false; }
                sb.AppendLine($"<p>{trimmed}</p>");
            }
        }

        if (inList) sb.AppendLine("</ul>");
        return sb.ToString();
    }

    private static string EscapeHtml(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
