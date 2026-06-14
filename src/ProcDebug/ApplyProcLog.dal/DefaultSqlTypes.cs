namespace ApplyProcLog.dal;

/// <summary>
/// Встроенные типы SQL Server (is_user_defined = 0).
/// Загружено из sys.types базы DBAuditTest.
/// </summary>
public static class DefaultSqlTypes
{
    public static IReadOnlyDictionary<string, DefaultSqlTypeInfo> All { get; } =
        new Dictionary<string, DefaultSqlTypeInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["bigint"]       = new(165,  127, false),
            ["binary"]       = new(173,  173, false),
            ["bit"]          = new(104,  104, true),
            ["char"]         = new(175,  175, false),
            ["date"]         = new(40,   40,  true),
            ["datetime"]     = new(61,   61,  true),
            ["datetime2"]    = new(42,   42,  true),
            ["datetimeoffset"] = new(43,  43,  true),
            ["decimal"]      = new(106,  106, true),
            ["float"]        = new(62,   62,  true),
            ["geography"]    = new(240,  130, true),
            ["geometry"]     = new(240,  129, true),
            ["hierarchyid"]  = new(240,  128, true),
            ["image"]        = new(34,   34,  true),
            ["int"]          = new(56,   56,  true),
            ["money"]        = new(60,   60,  true),
            ["nchar"]        = new(239,  239, false),
            ["ntext"]        = new(99,   99,  true),
            ["numeric"]      = new(108,  108, true),
            ["nvarchar"]     = new(231,  231, true),
            ["real"]         = new(59,   59,  true),
            ["smalldatetime"] = new(58,  58,  true),
            ["smallint"]     = new(52,   52,  true),
            ["smallmoney"]   = new(122,  122, true),
            ["sql_variant"]  = new(98,   98,  true),
            ["sysname"]      = new(231,  256, false),
            ["text"]         = new(35,   35,  true),
            ["time"]         = new(41,   41,  true),
            ["timestamp"]    = new(189,  189, false),
            ["tinyint"]      = new(48,   48,  true),
            ["uniqueidentifier"] = new(36,  36,  true),
            ["varbinary"]    = new(165,  165, true),
            ["varchar"]      = new(167,  167, true),
            ["xml"]          = new(241,  241, true),
        };
    
    /// <summary>
    /// Проверяет, является ли тип встроенным (не пользовательским).
    /// </summary>
    public static bool IsBuiltIn(string typeName)
        => All.ContainsKey(typeName);

    /// <summary>
    /// Проверяет, является ли тип Nullable по умолчанию.
    /// </summary>
    public static bool IsNullableByDefault(string typeName)
        => All.TryGetValue(typeName, out var info) && info.IsNullable;
}

/// <summary>
/// Метаданные встроенного типа SQL Server.
/// </summary>
/// <param name="SystemTypeId">system_type_id</param>
/// <param name="UserTypeId">user_type_id</param>
/// <param name="IsNullable">допускает NULL по умолчанию</param>
public readonly record struct DefaultSqlTypeInfo(
    byte SystemTypeId,
    int UserTypeId,
    bool IsNullable);
