namespace ApplyProcLog.dal;

/// <summary>
/// Типы, которые нельзя (или небезопасно) привести к varchar через CAST
/// для строки audit-параметров. Аналог [audit].[fn_BuildExceptType] +
/// системные типы с ошибкой "Explicit conversion ... to varchar is not allowed".
/// Для них и для неизвестных UDT: '@p=NULL'+' /*type*/'.
/// </summary>
public static class AuditExceptSqlTypes
{
    public static IReadOnlySet<string> All { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Explicit conversion to varchar not allowed
            "image",
            "geography",
            "geometry",
            // Binary / large / non-scalar — в audit логируем NULL + комментарий типа
            "varbinary",
            "binary",
            "xml",
            "timestamp",
            "hierarchyid",
            "sql_variant",
        };

    public static bool IsExcept(string typeName)
        => !string.IsNullOrEmpty(typeName) && All.Contains(typeName);
}
