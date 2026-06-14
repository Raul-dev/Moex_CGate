using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ApplyProcLog.dal;
using Xunit;

namespace ApplyProcLog.Tests.Integration;

/// <summary>
/// xUnit Collection Definition для интеграционных тестов с БД.
/// Тесты запускаются только когда БД доступна.
/// </summary>
[CollectionDefinition("DatabaseIntegration", DisableParallelization = true)]
public class DatabaseIntegrationCollection : ICollectionFixture<DatabaseFixture>
{
}

public class DatabaseNotAvailableException : Exception
{
    public DatabaseNotAvailableException(string message) : base(message) { }
}

/// <summary>
/// Fixture для интеграционных тестов с базой данных.
/// Кеширует список процедур для ускорения тестов.
/// </summary>
public class DatabaseFixture : IDisposable
{
    private const string DefaultConnectionString = "Server=localhost;Database=DBAuditTest;Integrated Security=true;TrustServerCertificate=true;Encrypt=false;";

    public string ConnectionString { get; private set; }
    public bool IsDatabaseAvailable { get; private set; }
    public List<StoredProcedureObjecId> Procedures { get; private set; } = new();
    private bool _proceduresLoaded;

    public DatabaseFixture()
    {
        ConnectionString = GetConnectionString();
        IsDatabaseAvailable = CheckDatabaseAvailability();

        if (!IsDatabaseAvailable)
        {
            throw new DatabaseNotAvailableException($"Database at '{ConnectionString}' is not available. Integration tests skipped.");
        }
    }

    /// <summary>
    /// Возвращает кешированный список процедур. Загружается один раз при первом вызове.
    /// </summary>
    public List<StoredProcedureObjecId> GetProcedures()
    {
        if (_proceduresLoaded)
            return Procedures;

        var db = new DBHelper(ConnectionString);
        Procedures = db.GetSqlProceduresObjecIdAsync().GetAwaiter().GetResult();
        _proceduresLoaded = true;
        return Procedures;
    }

    private static string GetConnectionString()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        return config["ConnectionStrings:TestDatabase"]
            ?? config["ConnectionStrings:DefaultConnection"]
            ?? DefaultConnectionString;
    }

    private bool CheckDatabaseAvailability()
    {
        try
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.ExecuteScalar();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Dispose()
    {
    }
}
