using System.Data;
using System.Text;
using ContextSync.dal.Abstractions;
using ContextSync.dal.Models;
using ContextSync.dal.Settings;
using Microsoft.Data.SqlClient;
using Serilog;

namespace ContextSync.dal.Readers;

public class DatabaseSourceReader : ISourceReader
{
    private readonly DatabaseSettings _settings;
    public string SourceType => "database";

    public DatabaseSourceReader(DatabaseSettings settings)
    {
        _settings = settings;
    }

    public async Task<List<SourceDocument>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<SourceDocument>();

        if (string.IsNullOrEmpty(_settings.QueryText))
        {
            Log.Warning("Database QueryText is empty, skipping database source");
            return result;
        }

        var connStr = _settings.BuildConnectionString();
        Log.Information("Reading from database: {Server}.{Database}", _settings.Server, _settings.Database);

        try
        {
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(cancellationToken);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = _settings.QueryText;
            cmd.CommandTimeout = _settings.CommandTimeoutSeconds;

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var doc = MapRowToDocument(reader);
                if (doc != null)
                    result.Add(doc);
            }

            Log.Information("Read {Count} documents from database", result.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Database read error");
        }

        return result;
    }

    private static SourceDocument? MapRowToDocument(SqlDataReader reader)
    {
        string title = "";
        string content = "";
        var metadata = new Dictionary<string, string>();

        for (int i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            var value = reader.IsDBNull(i) ? "" : reader.GetValue(i)?.ToString() ?? "";

            switch (name.ToLowerInvariant())
            {
                case "title":
                case "name":
                case "summary":
                    title = value;
                    break;
                case "content":
                case "body":
                case "description":
                case "text":
                    if (string.IsNullOrEmpty(content))
                        content = value;
                    break;
                case "id":
                    metadata["Id"] = value;
                    break;
                case "jira_project":
                case "jiraproject":
                    metadata["JiraProject"] = value;
                    break;
                case "jira_issue_type":
                case "jiraissuetype":
                    metadata["JiraIssueType"] = value;
                    break;
                case "wiki_space":
                case "wikispace":
                    metadata["WikiSpace"] = value;
                    break;
                default:
                    metadata[name] = value;
                    break;
            }
        }

        if (string.IsNullOrEmpty(title))
            title = $"DB-Record-{metadata.GetValueOrDefault("Id", Guid.NewGuid().ToString()[..8])}";

        if (string.IsNullOrEmpty(content))
            content = string.Join("\n", metadata.Select(kv => $"**{kv.Key}**: {kv.Value}"));

        return new SourceDocument
        {
            Title = title,
            Content = content,
            SourceType = "database",
            SourceLocation = "sql-query",
            Metadata = metadata,
            JiraProjectKey = metadata.GetValueOrDefault("JiraProject"),
            JiraIssueType = metadata.GetValueOrDefault("JiraIssueType"),
            WikiSpaceKey = metadata.GetValueOrDefault("WikiSpace")
        };
    }
}
