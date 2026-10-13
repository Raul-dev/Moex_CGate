using ContextSync.dal.Settings;
using Microsoft.EntityFrameworkCore;

namespace ContextSync.dal;

public class ContextSyncDbContext : DbContext
{
    private readonly DatabaseSettings _settings;

    public ContextSyncDbContext(DatabaseSettings settings)
    {
        _settings = settings;
    }

    public ContextSyncDbContext(DatabaseSettings settings, DbContextOptions<ContextSyncDbContext> options)
        : base(options)
    {
        _settings = settings;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer(_settings.BuildConnectionString());
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> GetTableNamesAsync(string? tableFilter = null, CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT t.name AS table_name
            FROM sys.tables t
            WHERE (@tableFilter IS NULL OR t.name LIKE @tableFilter)
              AND t.is_ms_shipped = 0
            ORDER BY t.name;";

        var filterParam = new Microsoft.Data.SqlClient.SqlParameter("@tableFilter", tableFilter ?? (object)DBNull.Value);
        return await Database.SqlQueryRaw<string>(sql, filterParam)
            .ToListAsync(cancellationToken);
    }
}
