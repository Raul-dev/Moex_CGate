using ContextSync.dal.Models;

namespace ContextSync.dal.Abstractions;

public interface ISchemaReader
{
    Task<List<TableSchema>> ReadSchemaAsync(
        string? tableName = null,
        string? tableFilter = null,
        CancellationToken cancellationToken = default);

    Task<List<string>> GetTableNamesAsync(
        string? tableFilter = null,
        CancellationToken cancellationToken = default);
}
