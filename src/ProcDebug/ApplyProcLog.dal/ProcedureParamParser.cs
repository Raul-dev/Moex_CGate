using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ApplyProcLog.dal;

/// <summary>
/// Парсер параметров процедуры.
/// Конструктор 1: (int objectId, string connectionString) — загрузка из БД через sys.parameters + sys.types.
/// Конструктор 2: (string procedureBody) — извлечение параметров из тела процедуры (аналог ExtractParamsFromBody).
/// </summary>
public class ProcedureParamParser
{
    public List<ProcedureParameter> Parameters { get; } = new();

    /// <summary>
    /// Конструктор 1: загрузка параметров из БД по object_id.
    /// SELECT p.*, t.*, IIF(EXC.TypeName IS NULL, 0, 1) AS is_ignore
    /// FROM sys.parameters p
    /// JOIN sys.types t ON p.user_type_id = t.user_type_id
    /// LEFT JOIN [audit].[fn_BuildExceptType]() EXC ON t.[name] = EXC.TypeName
    /// WHERE p.object_id = @objectId
    /// </summary>
    public ProcedureParamParser(int objectId, string connectionString)
    {
        using var ctx = new TestDBContext(
            new DbContextOptionsBuilder<TestDBContext>()
                .UseSqlServer(connectionString)
                .Options);

        ctx.Database.SetCommandTimeout(120);

        string sql = @"
SELECT
    p.object_id            AS ObjectId,
    p.name                 AS Name,
    p.parameter_id         AS ParameterId,
    p.user_type_id         AS UserTypeId,
    p.system_type_id       AS SystemTypeId,
    p.max_length           AS MaxLength,
    p.precision            AS Precision,
    p.scale                AS Scale,
    p.is_output            AS IsOutput,
    p.is_cursor_ref        AS IsCursorRef,
    p.is_readonly          AS IsReadOnly,
    p.has_default_value    AS HasDefaultValue,
    p.default_value        AS DefaultValue,
    t.name                 AS TypeName,
    t.max_length           AS TypeMaxLength,
    t.precision            AS TypePrecision,
    t.scale                AS TypeScale,
    t.is_table_type        AS IsTableType,
    t.is_user_defined      AS IsUserDefined,
    t.is_assembly_type     AS IsAssemblyType,
    t.is_nullable          AS IsNullable,
    IIF(EXC.TypeName IS NULL, 0, 1) AS IsIgnore
FROM sys.parameters p
JOIN sys.types t ON p.user_type_id = t.user_type_id
LEFT JOIN [audit].[fn_BuildExceptType]() EXC ON t.name = EXC.TypeName
WHERE p.object_id = @objectId
ORDER BY p.parameter_id;";

        Parameters = ctx.Database
            .SqlQueryRaw<ProcedureParameter>(sql, new SqlParameter("@objectId", objectId))
            .ToList();

        foreach (var p in Parameters)
        {
            if (AuditExceptSqlTypes.IsExcept(p.TypeName))
                p.IsIgnore = 1;
        }
    }

    /// <summary>
    /// Конструктор 2: извлечение параметров из тела процедуры (procedureBody).
    /// Параметры — всё между ALTER/CREATE PROCEDURE [Schema].[Name];N и первым неquoted словом AS.
    /// Концом объявления считается: строка "WITH EXECUTE AS &lt;role&gt;" и далее отдельное слово AS.
    /// </summary>
    public ProcedureParamParser(string procedureBody)
    {
        if (string.IsNullOrWhiteSpace(procedureBody))
            return;

        string clean = RemoveSqlComments(procedureBody);

        var match = Regex.Match(
            clean,
            @"(ALTER|CREATE)\s+PROCEDURE\s+\[[\w:.]+\]\.\[[\w:.]+\](;\d+)?[\s\S]*?(?=\n\s*WITH\s+EXECUTE\s+AS\s+\w+\s*\n\s*AS\b)",
            RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            match = Regex.Match(
                clean,
                @"(ALTER|CREATE)\s+PROCEDURE\s+\[[\w:.]+\]\.\[[\w:.]+\](;\d+)?[\s\S]*?(?=\n[ \t]*AS\b)",
                RegexOptions.IgnoreCase);
        }

        if (!match.Success)
            return;

        string rawDeclaration = match.Value;

        var paramMatches = Regex.Matches(rawDeclaration,
            @"@(\w+)\s+(\w+)\s*(?:\(([^)]+)\))?\s*(?:=\s*(\S+))?\s*(OUTPUT)?",
            RegexOptions.IgnoreCase);

        foreach (Match m in paramMatches)
        {
            string typeName = m.Groups[2].Value.Trim();
            string argsStr = m.Groups[3].Value;
            bool hasDefault = !string.IsNullOrEmpty(m.Groups[4].Value);
            bool isOutput = !string.IsNullOrEmpty(m.Groups[5].Value);

            ParseTypeArgs(argsStr, typeName, out byte precision, out byte scale, out short maxLength);

            bool isBuiltIn = DefaultSqlTypes.IsBuiltIn(typeName);

            Parameters.Add(new ProcedureParameter
            {
                Name = "@" + m.Groups[1].Value,
                TypeName = typeName,
                Precision = precision,
                Scale = scale,
                MaxLength = maxLength,
                HasDefaultValue = hasDefault,
                IsOutput = isOutput,
                IsUserDefined = !isBuiltIn,
                IsNullable = DefaultSqlTypes.IsNullableByDefault(typeName),
                IsIgnore = (!isBuiltIn || AuditExceptSqlTypes.IsExcept(typeName)) ? 1 : 0
            });
        }
    }

    /// <summary>
    /// Парсит аргументы типа из скобок: (precision,scale), (max), (length).
    /// Типы с precision/scale: numeric, decimal, datetime2, datetimeoffset, time.
    /// Типы с length: varchar, nvarchar, char, nchar, varbinary, binary.
    /// </summary>
    private static void ParseTypeArgs(string argsStr, string typeName,
        out byte precision, out byte scale, out short maxLength)
    {
        precision = 0;
        scale = 0;
        maxLength = 0;

        if (string.IsNullOrWhiteSpace(argsStr))
            return;

        string upper = typeName.ToUpperInvariant();

        if (upper is "NUMERIC" or "DECIMAL")
        {
            string[] parts = argsStr.Split(',');
            if (parts.Length >= 1 && byte.TryParse(parts[0].Trim(), out byte p))
                precision = p;
            if (parts.Length >= 2 && byte.TryParse(parts[1].Trim(), out byte s))
                scale = s;
        }
        else if (upper is "DATETIME2" or "DATETIMEOFFSET" or "TIME")
        {
            if (byte.TryParse(argsStr.Trim(), out byte p))
                precision = p;
        }
        else if (upper is "VARCHAR" or "CHAR" or "VARBINARY" or "BINARY")
        {
            maxLength = argsStr.ToUpperInvariant().Trim() == "MAX"
                ? (short)-1
                : short.TryParse(argsStr.Trim(), out short ml) ? ml : (short)0;
        }
        else if (upper is "NVARCHAR" or "NCHAR" or "NTEXT")
        {
            maxLength = argsStr.ToUpperInvariant().Trim() == "MAX"
                ? (short)-1
                : short.TryParse(argsStr.Trim(), out short n) ? (short)(n * 2) : (short)0;
        }
    }

    /// <summary>
    /// Возвращает параметры в формате для EXEC-обёртки: '@param1 int, @param2 varchar(100) OUTPUT'.
    /// Пропускает параметры с IsIgnore = 1.
    /// </summary>
    public string BuildExecParams()
    {
        var visible = Parameters.Where(p => p.IsIgnore == 0).ToList();
        if (visible.Count == 0)
            return "";

        var parts = new List<string>();
        foreach (var p in visible)
        {
            var type = p.TypeName;
            if (p.MaxLength > 0 && type == "sysname")
                type = $"nvarchar({p.MaxLength})";
            else if (p.MaxLength == -1 && (type == "varchar" || type == "nvarchar"))
                type = $"{type}(max)";

            var s = $"{p.Name} {type}";
            if (p.IsOutput)
                s += " OUTPUT";
            parts.Add(s);
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Возвращает строку для вызова процедуры в обёрнутом блоке:
    /// 'EXEC [schema].[proc];N @p1, @p2' — с кавычками для sp_executesql.
    /// </summary>
    public string BuildExecCall(string fullName)
    {
        var visible = Parameters.Where(p => p.IsIgnore == 0).ToList();
        var args = string.Join(", ", visible.Select(p => p.Name));
        return $"'{fullName} {args}'";
    }

    /// <summary>
    /// Возвращает строку с except-типами для sp_executesql: 'тип1', 'тип2'.
    /// </summary>
    public string BuildExceptTypes()
    {
        var excepts = Parameters.Where(p => p.IsIgnore == 1).ToList();
        if (excepts.Count == 0)
            return "";

        return string.Join(", ", excepts.Select(p => $"'{p.TypeName}'"));
    }

    /// <summary>
    /// Возвращает строку параметров в формате объявления процедуры:
    /// '@param1 int, @param2 varchar(100), @param3 bigint OUTPUT'
    /// </summary>
    public string GetParametersForDeclare()
    {
        if (Parameters.Count == 0)
            return "";

        var parts = new List<string>();
        foreach (var p in Parameters)
        {
            string type = GetTypeNameWithLength(p);
            var s = $"{p.Name} {type}";
            if (p.IsOutput)
                s += " OUTPUT";
            parts.Add(s);
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Возвращает строку параметров в формате для fn_BuildProcedureParams / аудита.
    /// Для string-типов (varchar/nvarchar/char/nchar/text/ntext): с кавычками.
    /// Для остальных built-in: без кавычек.
    /// Для AuditExceptSqlTypes и неизвестных UDT: '@param=NULL'+' /*type*/' (без CAST).
    /// Для OUTPUT (known): '/*type*/@param=@param OUTPUT'; except/UDT OUTPUT — NULL + комментарий.
    /// Даты / bit / tinyint / uniqueidentifier — значение в кавычках.
    /// </summary>
    public string GetParametersForAudit()
    {
        if (Parameters.Count == 0)
            return "";
        int i = 0;
        string spaces = "";
        var parts = new List<string>();
        foreach (var p in Parameters)
        {
            if (i > 0)
                spaces = "    ";
            i++;

            if (ShouldSkipVarcharCast(p))
            {
                // Нет CAST: значение как NULL + закомментированное имя типа (image/geography/UDT)
                if (p.IsOutput)
                    parts.Add($"{spaces}'{p.Name}=NULL OUTPUT'+' /*{p.TypeName}*/'");
                else
                    parts.Add($"{spaces}'{p.Name}=NULL'+' /*{p.TypeName}*/'");
            }
            else if (p.IsOutput)
            {
                parts.Add($"{spaces}'/*{p.TypeName}*/{p.Name}={p.Name} OUTPUT'");
            }
            else
            {
                int strLen = GetEstimatedStringLength(p);
                string castType = strLen > 0 ? $"varchar({strLen})" : "varchar(max)";

                // Строки / даты / bit / tinyint / uniqueidentifier — значение в кавычках
                if (IsStringType(p.TypeName) || NeedsQuotedAuditValue(p.TypeName))
                    parts.Add($"{spaces}'{p.Name}='+ISNULL(''''+LTRIM(CAST({p.Name} AS {castType}))+'''','NULL')");
                else
                    parts.Add($"{spaces}'{p.Name}='+ISNULL(LTRIM(CAST({p.Name} AS {castType})),'NULL')");
            }
        }

        return string.Join("+','+\n", parts);
    }

    /// <summary>
    /// Возвращает имя типа с длиной/precision из параметра.
    /// Аналог того что возвращает sys.types.name + аргументы.
    /// </summary>
    private static string GetTypeNameWithLength(ProcedureParameter p)
    {
        string t = p.TypeName;
        string upper = t.ToUpperInvariant();

        if (upper == "SYSNAME")
            return $"nvarchar({p.MaxLength})";

        if (upper is "VARCHAR" or "NVARCHAR" or "CHAR" or "NCHAR"
            or "VARBINARY" or "BINARY")
        {
            if (p.MaxLength == -1)
                return $"{t}(max)";
            if (p.MaxLength > 0)
                return $"{t}({p.MaxLength})";
        }

        if (upper is "NUMERIC" or "DECIMAL")
        {
            if (p.Precision > 0 && p.Scale > 0)
                return $"{t}({p.Precision},{p.Scale})";
            if (p.Precision > 0)
                return $"{t}({p.Precision})";
        }

        if (upper is "DATETIME2" or "DATETIMEOFFSET" or "TIME")
        {
            if (p.Precision > 0)
                return $"{t}({p.Precision})";
        }

        return t;
    }

    /// <summary>
    /// Возвращает оценочную длину строкового представления параметра.
    /// Точная копия логики [audit].[fn_GetEstimatedStringLength].
    /// </summary>
    private static int GetEstimatedStringLength(ProcedureParameter p)
    {
        string upper = p.TypeName.ToUpperInvariant();

        return upper switch
        {
            "SYSNAME" => p.MaxLength == -1 ? 4000 : p.MaxLength / 2,

            "NVARCHAR" or "NCHAR" or "NTEXT"
                => p.MaxLength == -1 ? 4000 : p.MaxLength / 2,

            "VARCHAR" or "CHAR" or "TEXT"
                => p.MaxLength == -1 ? 8000 : p.MaxLength,

            "VARBINARY" or "BINARY" or "IMAGE"
                => 4000,

            "NUMERIC" or "DECIMAL"
                => p.Precision > 0 ? p.Precision + 2 : 38,

            "DATETIME" or "DATETIME2"
                => 27,

            "SMALLDATETIME"
                => 23,

            "DATETIMEOFFSET"
                => 34,

            "TIME"
                => 16,

            "DATE"
                => 10,

            "MONEY" or "SMALLMONEY"
                => 26,

            "BIGINT" => 20,
            "INT" => 11,
            "SMALLINT" => 6,
            "TINYINT" => 3,
            "BIT" => 27,
            "FLOAT" => 53,
            "REAL" => 24,
            "UNIQUEIDENTIFIER" => 36,

            _ => 4000,
        };
    }

    /// <summary>
    /// Нормализует имя типа: убирает precision/max_length у типов с переменной длиной,
    /// чтобы TypeName из тела процедуры совпадал с sys.types.name.
    /// </summary>
    private static string NormalizeTypeNameForAudit(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return typeName;

        int paren = typeName.IndexOf('(');
        string baseName = paren >= 0
            ? typeName[..paren].TrimEnd()
            : typeName.Trim();

        return DefaultSqlTypes.IsBuiltIn(baseName)
            ? (paren >= 0 ? typeName[..paren].TrimEnd() : typeName)
            : typeName;
    }

    /// <summary>
    /// Возвращает true для типов которые нужно оборачивать в кавычки в GetParametersForAudit.
    /// Точное совпадение с fn_BuildProcedureParams: varchar, nvarchar, char, nchar, text, ntext.
    /// </summary>
    private static bool IsStringType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return false;
        string upper = typeName.ToUpperInvariant();
        return upper is "VARCHAR" or "NVARCHAR" or "CHAR" or "NCHAR"
                   or "TEXT" or "NTEXT";
    }

    /// <summary>
    /// Типы из AuditExceptSqlTypes и неизвестные UDT (IsIgnore) — без CAST в varchar.
    /// </summary>
    private static bool ShouldSkipVarcharCast(ProcedureParameter p)
        => p.IsIgnore == 1 || AuditExceptSqlTypes.IsExcept(p.TypeName);

    /// <summary>
    /// Типы (не string), чьё строковое представление в audit-параметрах оборачивается в кавычки:
    /// даты, bit, tinyint, uniqueidentifier.
    /// </summary>
    private static bool NeedsQuotedAuditValue(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return false;
        string upper = typeName.ToUpperInvariant();
        return upper is "SMALLDATETIME" or "DATE" or "DATETIME" or "DATETIME2"
                   or "DATETIMEOFFSET"
                   or "BIT" or "TINYINT" or "UNIQUEIDENTIFIER";
    }
    
    private static string RemoveSqlComments(string s)
    {
        s = Regex.Replace(s, @"/\*[\s\S]*?\*/", "");
        s = Regex.Replace(s, @"--[^\r\n]*", "");
        s = Regex.Replace(s, @"^---[\s]*$", "", RegexOptions.Multiline);
        return s;
    }
}
