using System.Text.RegularExpressions;

namespace MQ.dal;

public static partial class BufferTableSqlHelper
{
    private static readonly Regex IdentifierPattern = IdentifierRegex();

    public static (string Schema, string Table) ParseQualifiedName(string qualifiedName)
    {
        var trimmed = qualifiedName.Trim().Trim('[', ']');
        var dotIndex = trimmed.IndexOf('.');
        if (dotIndex >= 0)
        {
            return (
                ValidateIdentifier(trimmed[..dotIndex]),
                ValidateIdentifier(trimmed[(dotIndex + 1)..]));
        }

        return ("mq", ValidateIdentifier(trimmed));
    }

    public static string AppendBufferSuffix(string qualifiedName)
    {
        var (schema, table) = ParseQualifiedName(qualifiedName);
        if (table.EndsWith("Buffer", StringComparison.OrdinalIgnoreCase))
            return $"{schema}.{table}";

        return $"{schema}.{table}Buffer";
    }

    public static string BuildCreateBufferTableSql(string schema, string table)
    {
        var pkName = $"PK_{schema}_{table}";
        var dfError = $"DF_{schema}_{table}_is_error";
        var dfCreate = $"DF_{schema}_{table}_dt_create";
        var dfUpdate = $"DF_{schema}_{table}_dt_update";

        return $@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'{schema}' AND t.name = N'{table}'
)
BEGIN
    CREATE TABLE [{schema}].[{table}] (
        [BufferId] BIGINT IDENTITY(1,1) NOT NULL,
        [SessionId] BIGINT NOT NULL,
        [MessageKey] NVARCHAR(256) NOT NULL,
        [MessageId] UNIQUEIDENTIFIER NOT NULL,
        [MessageBody] VARCHAR(MAX) NULL,
        [MessageTypeId] TINYINT NULL,
        [IsError] BIT NOT NULL CONSTRAINT [{dfError}] DEFAULT (0),
        [CreatedAt] DATETIME2(4) NOT NULL CONSTRAINT [{dfCreate}] DEFAULT (SYSDATETIME()),
        [UpdatedAt] DATETIME2(4) NOT NULL CONSTRAINT [{dfUpdate}] DEFAULT ('1900-01-01'),
        CONSTRAINT [{pkName}] PRIMARY KEY CLUSTERED ([BufferId] ASC)
    );
END";
    }

    public static string BuildTruncateSql(string schema, string table) =>
        $"TRUNCATE TABLE [{schema}].[{table}];";

    private static string ValidateIdentifier(string value)
    {
        if (!IdentifierPattern.IsMatch(value))
            throw new ArgumentException($"Invalid SQL identifier: '{value}'.", nameof(value));

        return value;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex IdentifierRegex();
}
