using System.Text;
using ContextSync.dal.Abstractions;
using ContextSync.dal.Models;
using Serilog;

namespace ContextSync.Services;

public class FileSourceReader : ISourceReader
{
    private readonly string _inputDirectory;
    private readonly string _filePattern;
    private readonly bool _recursive;
    private readonly Encoding _encoding;

    public string SourceType => "file";

    public FileSourceReader(string inputDirectory, string filePattern, bool recursive, string encodingName = "UTF-8")
    {
        _inputDirectory = inputDirectory;
        _filePattern = string.IsNullOrEmpty(filePattern) ? "*.txt" : filePattern;
        _recursive = recursive;
        _encoding = encodingName.Equals("UTF-8", StringComparison.OrdinalIgnoreCase)
            ? new UTF8Encoding(false)
            : Encoding.GetEncoding(encodingName);
    }

    public async Task<List<SourceDocument>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<SourceDocument>();

        if (!Directory.Exists(_inputDirectory))
        {
            Log.Warning("Input directory not found: {Dir}", _inputDirectory);
            return result;
        }

        var searchOption = _recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(_inputDirectory, _filePattern, searchOption)
            .OrderBy(f => f)
            .ToList();

        Log.Information("Found {Count} files in {Dir}", files.Count, _inputDirectory);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var content = await File.ReadAllTextAsync(file, _encoding, cancellationToken);
                var title = Path.GetFileNameWithoutExtension(file);

                var doc = ParseDocument(title, content, file);
                result.Add(doc);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reading file: {File}", file);
            }
        }

        return result;
    }

    private static SourceDocument ParseDocument(string title, string content, string filePath)
    {
        var doc = new SourceDocument
        {
            Title = title,
            Content = content,
            SourceType = "file",
            SourceLocation = filePath
        };

        var lines = content.Split('\n');
        bool inFrontMatter = false;
        var contentLines = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');

            if (i == 0 && line == "---")
            {
                inFrontMatter = true;
                continue;
            }

            if (inFrontMatter)
            {
                if (line == "---")
                {
                    inFrontMatter = false;
                    continue;
                }

                var colonIdx = line.IndexOf(':');
                if (colonIdx > 0)
                {
                    var key = line.Substring(0, colonIdx).Trim();
                    var value = line.Substring(colonIdx + 1).Trim();

                    switch (key.ToLowerInvariant())
                    {
                        case "title": doc.Title = value; break;
                        case "jira-project": doc.JiraProjectKey = value; break;
                        case "jira-issuetype": doc.JiraIssueType = value; break;
                        case "jira-assignee": doc.JiraAssignee = value; break;
                        case "jira-epic": doc.JiraEpicKey = value; break;
                        case "jira-priority": doc.JiraPriority = value; break;
                        case "jira-labels":
                            doc.JiraLabels = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                            break;
                        case "wiki-space": doc.WikiSpaceKey = value; break;
                        case "wiki-parent": doc.WikiParentTitle = value; break;
                        case "html-template": doc.HtmlTemplate = value; break;
                        case "html-output": doc.HtmlOutputPath = value; break;
                        default: doc.Metadata[key] = value; break;
                    }
                }
            }
            else
            {
                contentLines.Add(line);
            }
        }

        doc.Content = string.Join('\n', contentLines);
        return doc;
    }
}
