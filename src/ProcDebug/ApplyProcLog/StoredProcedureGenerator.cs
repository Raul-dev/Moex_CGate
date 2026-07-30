using ApplyProcLog.dal;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;

namespace ApplyProcLog
{
    public class StoredProcedureGenerator
    {
        private const string CreateEmptyFolderName = "CreateEmpty";

        /// <summary>
        /// Формирует безопасное имя файла для процедуры.
        /// </summary>
        private static string MakeProcFileName(string schema, string procedureName)
        {
            // Определяем версию до замены ; на ;__
            var verMatch = Regex.Match(procedureName, @";(\d+)$");
            bool noVersion = !verMatch.Success;
            bool hasVersion1 = verMatch.Success && verMatch.Groups[1].Value == "1";

            string safeName = procedureName
                .Replace("::", "_")
                .Replace(":", "_")
                .Replace(";", ";__");

            // Добавляем ;__1 только если версии не было вообще (необнумерованная)
            if (noVersion)
                safeName += ";__1";

            string fileName = $"{schema}.{safeName}.sql";
            foreach (char c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');
            return fileName;
        }

        /// <summary>
        /// Формирует SQL Имя процедуры.
        /// </summary>
        private static string MakeProcSqlName(string schema, string procedureName)
        {
            // Определяем версию до замены ; на ;__
            var verMatch = Regex.Match(procedureName, @";(\d+)$");
            bool noVersion = !verMatch.Success;
            bool hasVersion1 = verMatch.Success && verMatch.Groups[1].Value == "1";
            string sqlName = "";
            if (noVersion)
            {
                sqlName = $"[{schema}].[{procedureName}]";
            }
            else {
                sqlName = $"[{schema}].[{procedureName.Substring(0, procedureName.IndexOf(';'))}];{verMatch.Groups[1]}";
            }
            return sqlName;
        }
        /// <summary>
        /// Создает SQL файлы для каждой процедуры в текущей директории.
        /// Одновременно генерирует пустые заглушки в подпапке CreateEmpty.
        /// </summary>
        public async Task CreateProcedureFilesAsync(IEnumerable<StoredProcedureInfo> procedures, string targetDirectory = "PROC", bool withReturn = false)
        {
            string resolvedPath = FilePathHelper.GetProcDirectory(targetDirectory);
            Log.Information("Целевая папка: {TargetDir}", resolvedPath);

            if (!Directory.Exists(resolvedPath))
                Directory.CreateDirectory(resolvedPath);

            string originalDirectory = Path.Combine(resolvedPath, "Original");
            if (!Directory.Exists(originalDirectory))
                Directory.CreateDirectory(originalDirectory);

            string createEmptyDirectory = Path.Combine(resolvedPath, CreateEmptyFolderName);
            if (!Directory.Exists(createEmptyDirectory))
                Directory.CreateDirectory(createEmptyDirectory);

            var procList = procedures.ToList();

            foreach (var proc in procList)
            {
                string fileName = MakeProcFileName(proc.SchemaName, proc.ProcedureName);
                string filePath = Path.Combine(resolvedPath, fileName);

                try
                {
                    string body = WrapProcedureWithAudit(proc.ProcedureBody, proc.SchemaName, proc.ProcedureName, proc.ProcedureParams, proc.AuditEnabledCode);
                    if (withReturn)
                        body = WrapReturnStatements(body);
                    body = Regex.Replace(body, @"(?i)\bCREATE\s+PROCEDURE\b", "ALTER PROCEDURE");
                    // Добавляем метаданные в начало файла (опционально)
                    string content = $"-- Object ID: {proc.ObjectId}\n" +
                                     $"-- Created: {proc.CreateDate}\n" +
                                     $"-- Modified: {proc.ModifyDate}\n" +
                                     $"{body}";

                    string originalBody = Regex.Replace(proc.ProcedureBody, @"(?i)\bCREATE\s+PROCEDURE\b", "ALTER PROCEDURE");
                    string originalContent = $"{originalBody}";
                    string originalFilePath = Path.Combine(originalDirectory, fileName);
                    await File.WriteAllTextAsync(originalFilePath, originalContent, Encoding.UTF8);

                    // Асинхронная запись в файл с кодировкой UTF-8
                    await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);

                    // Генерация пустой заглушки в CreateEmpty
                    string emptyContent = GenerateEmptyProcedure(proc.ProcedureBody, proc.ProcedureName);
                    string emptyFilePath = Path.Combine(createEmptyDirectory, fileName);
                    await File.WriteAllTextAsync(emptyFilePath, emptyContent, Encoding.UTF8);

                    Log.Information($"Успешно создан: {fileName} (Original + CreateEmpty)");

                }
                catch (Exception ex)
                {
                    Log.Error($"Ошибка при сохранении {fileName}: {ex.Message}");
                }
            }

            // Генерируем заглушки для missing version 1: если в списке есть ;3, но нет ;1
            var missingStubs = GenerateMissingVersion1Stubs(procList);
            Log.Information("GenerateMissingVersion1Stubs: stubCount={Count}", missingStubs.Count);
            foreach (var stubBody in missingStubs)
            {
                var sig = ParseSignatureShort(stubBody);
                if (sig == null) continue;
                var (schema, name, version) = sig.Value;

                string stubFileName = MakeProcFileName(schema, $"{name};{version}");
                string stubPath = Path.Combine(createEmptyDirectory, stubFileName);

                string emptyContent = GenerateEmptyProcedure(stubBody, $"[{schema}].[{name}];{version}");
                File.WriteAllText(stubPath, emptyContent, Encoding.UTF8);
                Log.Information($"Auto-stub создан: {stubFileName}");
            }
        }
        /// <summary>
        /// Оборачивает тело процедуры вызовами sp_Print и сохраняет в файлы.
        /// </summary>
        public async Task CreateModifiedProcedureFilesAsync(IEnumerable<StoredProcedureInfo> procedures)
        {
            foreach (var proc in procedures)
            {
                // Модифицируем тело перед сохранением
                proc.ProcedureBody = WrapProcedureWithAudit(proc.ProcedureBody, proc.SchemaName, proc.ProcedureName, auditEnabledCode: proc.AuditEnabledCode);

                string fileName = $"{proc.SchemaName}.{proc.ProcedureName}.sql";
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);

                await File.WriteAllTextAsync(filePath, proc.ProcedureBody, Encoding.UTF8);
            }
        }

		/// <summary>
		/// Вставляет EXEC audit.sp_Log... в начало и конец тела процедуры.
		/// </summary>
		public string WrapProcedureWithAudit(string body, string  schemaName, string procName, string procedureParams = "", string auditEnabledCode = "FullAuditEnabled")
        {
            if (string.IsNullOrWhiteSpace(body)) return body;

            // Если аудит-вызовы уже есть — не добавляем повторно
            if (body.Contains("[audit].[sp_LogStart]") || body.Contains("[audit].[sp_LogFinish]"))
                return body;

            string procNameSql = MakeProcSqlName(schemaName, procName);
            string startAudit = $"\r\nDECLARE @AuditLogID int, @AuditProcedureName varchar(510), @AuditProcedureParams varchar(max), @AuditProcedureInfo varchar(max), @AuditErrorMessage varchar(max), @AuditRowCount int = 0, @AuditEnable nvarchar(256)\r\nSET @AuditEnable = [audit].[fn_GetAuditEnableSP]('{auditEnabledCode}')\r\nIF @AuditEnable IS NOT NULL \r\nBEGIN\r\n  SET @AuditProcedureName = '{procNameSql}'\r\n  IF OBJECT_ID('tempdb..#LogProc') IS NULL\r\n     SELECT * INTO #LogProc FROM [audit].[Template_LogProc]()\r\n  \r\n  SET @AuditProcedureParams = {procedureParams} \r\n  EXEC [audit].[sp_LogStart] @AuditEnable = @AuditEnable, @ProcedureName = @AuditProcedureName, @ProcedureParams = @AuditProcedureParams, @LogID = @AuditLogID OUTPUT\r\nEND\r\n";
            string endAudit = $"\r\n    EXEC [audit].[sp_LogFinish] @LogID = @AuditLogID, @RowCount = @AuditRowCount;\r\n";
            string endAuditErr = $"\r\n  SET @AuditErrorMessage = ERROR_MESSAGE() \r\n  EXEC [audit].[sp_LogFinish] @LogID = @AuditLogID, @RowCount = @AuditRowCount, @ErrorMessage = @AuditErrorMessage;\r\n";

            // Ищем ключевое слово AS (игнорируя регистр)
            // Если есть WITH EXECUTE AS — ищем AS только после него
            var regex = new Regex(@"(?i)\bAS\b");
            Match match;

            int searchStartIndex = 0;
            var executeAsMatch = Regex.Match(body, @"(?i)\bWITH\s+EXECUTE\s+AS\b");
            if (executeAsMatch.Success)
            {
                searchStartIndex = executeAsMatch.Index + executeAsMatch.Length;
                match = regex.Match(body, searchStartIndex);
            }
            else
            {
                match = regex.Match(body);
            }

            if (match.Success)
            {
                int insertIndex = match.Index + match.Length;

                // Вставляем начало
                body = body.Insert(insertIndex, startAudit);

                // Ищем последнее END TRY (без END CATCH сразу после) для вставки endPrint
                int lastEndTryIndex = -1;
                int searchPos = 0;

                while ((searchPos = body.IndexOf("END TRY", searchPos, StringComparison.OrdinalIgnoreCase)) != -1)
                {
                    int afterEndTry = searchPos + 7;
                    string after = body.Substring(afterEndTry, Math.Min(50, body.Length - afterEndTry)).TrimStart();

                    // Если после END TRY идёт END CATCH — это не то, нам нужно перед финальным END TRY
                    if (!after.StartsWith("END CATCH", StringComparison.OrdinalIgnoreCase) &&
                        !after.StartsWith("END CATCH;"))
                    {
                        lastEndTryIndex = searchPos;
                    }
                    searchPos += 7;
                }

                if (lastEndTryIndex != -1)
                    body = body.Insert(lastEndTryIndex, endAudit);
                else
                    body += endAudit;
            }

            // Ищем главный (самый внешний) блок CATCH — с конца тела процедуры
            int catchEndIdx = body.LastIndexOf("END CATCH", StringComparison.OrdinalIgnoreCase);
            if (catchEndIdx < 0)
                return body;

            int catchStartIdx = body.LastIndexOf("BEGIN CATCH", catchEndIdx, StringComparison.OrdinalIgnoreCase);
            if (catchStartIdx < 0)
                return body;

            // Ищем начало области поиска: от BEGIN CATCH до END CATCH
            int searchRegionEnd = catchEndIdx;

            int insertBeforeIdx = -1;

            // Ищем ReRaiseError — но нужно начало строки с EXEC
            int raiseErrIdx = body.IndexOf("[System].[ReRaiseError]", catchStartIdx, searchRegionEnd - catchStartIdx, StringComparison.OrdinalIgnoreCase);
            if (raiseErrIdx >= 0 && raiseErrIdx < searchRegionEnd)
            {
                // Двигаемся назад до начала строки и ищем там EXEC
                int lineStart = raiseErrIdx;
                while (lineStart > 0 && body[lineStart - 1] != '\n')
                    lineStart--;
                string lineBefore = body.Substring(lineStart, raiseErrIdx - lineStart);
                if (lineBefore.Contains("EXEC"))
                {
                    insertBeforeIdx = lineStart;
                }
            }

            // Ищем THROW (начало строки)
            if (insertBeforeIdx == -1)
            {
                int throwIdx = body.IndexOf("THROW", catchStartIdx, searchRegionEnd - catchStartIdx, StringComparison.OrdinalIgnoreCase);
                if (throwIdx >= 0 && throwIdx < searchRegionEnd)
                {
                    int lineStart = throwIdx;
                    while (lineStart > 0 && body[lineStart - 1] != '\n')
                        lineStart--;
                    insertBeforeIdx = lineStart;
                }
            }

            // Fallback на END CATCH
            if (insertBeforeIdx == -1)
                insertBeforeIdx = catchEndIdx;

            if (insertBeforeIdx >= 0)
                body = body.Insert(insertBeforeIdx, endAuditErr);

            return body;
        }

        /// <summary>
        /// Из тела процедуры извлекает схему, имя, версию и параметры.
        /// Параметры парсятся с учётом вложенных скобок и кавычек.
        /// </summary>
        private static (string fullName, int version, string parameters, int schemaDotIdx)?
            ParseProcedureSignature(string body)
        {
            // Ищем ALTER/CREATE PROCEDURE [Schema].[Name];N ... AS
            var match = Regex.Match(body, @"(?i)(?:CREATE|ALTER)\s+PROCEDURE\s+(\[[^\]]+\])\.(\[[^\]]+\])(?:;(\d+))?", RegexOptions.Singleline);
            if (!match.Success) return null;

            string fullName = match.Groups[1].Value + "." + match.Groups[2].Value;  // [Schema].[Name]
            string versionStr = match.Groups[3].Value;
            int version = string.IsNullOrEmpty(versionStr) ? 0 : int.Parse(versionStr);

            int procEnd = match.Index + match.Length;

            // Ищем начало параметров: '(' после конца сигнатуры
            int parenStart = -1;
            for (int i = procEnd; i < body.Length; i++)
            {
                if (char.IsWhiteSpace(body[i])) continue;
                if (body[i] == '(') { parenStart = i; break; }
                if (body[i] == 'A' || body[i] == 'a') break; // AS
            }

            // Ищем конец параметров: ')', с учётом вложенности и кавычек
            string parameters = "";
            if (parenStart >= 0)
            {
                int depth = 0;
                bool inQuotes = false;
                int parenEnd = -1;
                for (int i = parenStart; i < body.Length; i++)
                {
                    char c = body[i];
                    if (c == '\'' && (i == 0 || body[i - 1] != '\''))
                        inQuotes = !inQuotes;
                    else if (!inQuotes)
                    {
                        if (c == '(') depth++;
                        else if (c == ')')
                        {
                            depth--;
                            if (depth == 0) { parenEnd = i; break; }
                        }
                    }
                }
                if (parenEnd > parenStart)
                    parameters = body.Substring(parenStart, parenEnd - parenStart + 1);
            }

            // Ищем schema.name boundary
            int schemaEnd = fullName.IndexOf('.');
            return (fullName, version, parameters, schemaEnd);
        }

        /// <summary>
        /// Генерирует пустую заглушку процедуры: CREATE + IF NOT EXISTS + минимальное тело.
        /// Для numbered procedures SQL Server требует сначала создать базовую версию (group number 1),
        /// иначе ALTER с group number > 1 выдаёт ошибку.
        /// </summary>
        public string GenerateEmptyProcedure(string procedureBody, string procedureName)
        {
            if (string.IsNullOrWhiteSpace(procedureBody))
                return string.Empty;

            var parsed = ParseProcedureSignature(procedureBody);
            if (parsed == null)
                return string.Empty;

            var (fullName, ver, parameters, schemaDotIdx) = parsed.Value;

            // ver=0 — необнумерованная, treated as version 1
            if (ver == 0)
                ver = 1;

            string schema = fullName.Substring(0, schemaDotIdx);
            string schemaName = StripBrackets(schema);
            string nameNoBracket = StripBrackets(fullName.Substring(schemaDotIdx + 1));

            string npCondition = ver == 1
                ? "(np.procedure_number = 0 OR np.object_id IS NULL)"
                : $"np.procedure_number = {ver - 1}";

            string fullNameWithVersion = fullName + ";" + ver;

            return $@"IF NOT EXISTS (
    SELECT 1
    FROM sys.procedures p
    INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
    LEFT JOIN sys.numbered_procedures np ON np.object_id = p.object_id
    WHERE p.name = '{nameNoBracket}' AND s.name = '{schemaName}' AND {npCondition}
)
  EXEC('CREATE PROCEDURE {fullNameWithVersion}{parameters}
    AS
    BEGIN
      SET NOCOUNT ON;
    END')
";
        }

        /// <summary>
        /// Из тела процедуры извлекает (schema, name, version). Возвращает null если не удалось.
        /// </summary>
        private static (string schema, string name, int version)? ParseSignatureShort(string body)
        {
            var match = Regex.Match(
                body,
                @"(?i)(?:CREATE|ALTER)\s+PROCEDURE\s+(\[[^\]]+\])\.(\[[^\]]+\])(?:;(\d+))?",
                RegexOptions.Singleline);
            if (!match.Success) return null;

            string schema = StripBrackets(match.Groups[1].Value);
            string name = StripBrackets(match.Groups[2].Value);
            string verStr = match.Groups[3].Value;
            int version = string.IsNullOrEmpty(verStr) ? 0 : int.Parse(verStr);
            return (schema, name, version);
        }

        /// <summary>
        /// Находит все numbered families (схема.имя), у которых есть версии, но нет версии 1.
        /// Возвращает список stub-тел (минимальные CREATE PROCEDURE с параметрами) для версий 1.
        /// </summary>
        public List<string> GenerateMissingVersion1Stubs(IEnumerable<StoredProcedureInfo> procedures)
        {
            var procList = procedures.ToList();

            // Группируем по (schema, name)
            var families = new Dictionary<(string schema, string name), HashSet<int>>();

            foreach (var proc in procList)
            {
                if (string.IsNullOrWhiteSpace(proc.ProcedureBody)) continue;
                var sig = ParseSignatureShort(proc.ProcedureBody);
                if (sig == null) continue;
                var (schema, name, version) = sig.Value;
                var key = (schema, name);
                if (!families.TryGetValue(key, out var versions))
                {
                    versions = new HashSet<int>();
                    families[key] = versions;
                }
                versions.Add(version);
            }

            // Собираем stub-ы для семейств без версии 1
            var stubs = new List<string>();
            foreach (var kvp in families)
            {
                var (schema, name) = kvp.Key;
                var versions = kvp.Value;

                // Если нет версии 1 (и есть другие версии) — создаём stub
                if (!versions.Contains(1) && versions.Count > 0)
                {
                    Log.Information("Auto-stub: версия 1 для {Schema}.{Name} не найдена, генерирую заглушку",
                        schema, name);
                    // Генерируем минимальный stub с пустыми параметрами
                    string stubBody = $"CREATE PROCEDURE [{schema}].[{name}];1\n  AS\n  BEGIN\n    SET NOCOUNT ON;\n  END\n";
                    stubs.Add(stubBody);
                }
            }

            return stubs;
        }

        private static string StripBrackets(string s)
        {
            // "[Schema]" -> "Schema", "[Name]" -> "Name"
            return s.Trim('[', ']');
        }

        private static string StripGroupNumber(string fullName)
        {
            // [Schema].[Name;N] -> [Schema].[Name]
            int semiIdx = fullName.LastIndexOf(';');
            if (semiIdx > 0)
            {
                string after = fullName.Substring(semiIdx + 1).Trim('[', ']');
                if (int.TryParse(after, out _))
                    return fullName.Substring(0, semiIdx);
            }
            return fullName;
        }

        /// <summary>
        /// Очищает папки Proc и Original перед генерацией.
        /// </summary>
        public void CleanOutputDirectories(string targetDirectory = "PROC")
        {
            string resolvedPath = FilePathHelper.GetProcDirectory(targetDirectory);
            Log.Information("Очистка папки: {TargetDir}", resolvedPath);

            if (!Directory.Exists(resolvedPath))
            {
                Log.Information("Папка не существует, пропускаем очистку");
                return;
            }

            int totalFiles = CleanSqlFilesRecursive(resolvedPath);

            // Удаляем пустые подпапки (WithError, Original и т.д.)
            foreach (var subDir in Directory.GetDirectories(resolvedPath, "*", SearchOption.TopDirectoryOnly))
            {
                if (!Directory.EnumerateFileSystemEntries(subDir).Any())
                {
                    try
                    {
                        Directory.Delete(subDir);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("Не удалось удалить пустую папку {Dir}: {Error}", subDir, ex.Message);
                    }
                }
            }

            Log.Information("Очистка завершена: {TotalFiles} файлов", totalFiles);
        }

        private static int CleanSqlFiles(string directory)
        {
            if (!Directory.Exists(directory))
                return 0;

            var files = Directory.GetFiles(directory, "*.sql", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    Log.Warning("Не удалось удалить {File}: {Error}", file, ex.Message);
                }
            }
            return files.Length;
        }

        private static int CleanSqlFilesRecursive(string directory)
        {
            int totalFiles = CleanSqlFiles(directory);

            foreach (var subDir in Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly))
            {
                totalFiles += CleanSqlFilesRecursive(subDir);
            }

            return totalFiles;
        }

        /// <summary>
        /// Убирает суффикс версии ;N из имени процедуры.
        /// </summary>
        private static string StripVersion(string procName)
        {
            if (string.IsNullOrEmpty(procName))
                return procName;

            int versionIndex = procName.LastIndexOf(';');
            if (versionIndex > 0)
            {
                string afterSemi = procName.Substring(versionIndex + 1);
                if (int.TryParse(afterSemi, out _))
                    return procName.Substring(0, versionIndex);
            }
            return procName;
        }

        /// <summary>
        /// Оборачивает каждый RETURN блоком, печатающим номер строки.
        /// </summary>
        public string WrapReturnStatements(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return body;

            // Если уже обёрнут — не добавляем повторно
            if (body.Contains("Return line number"))
            {
                Log.Debug("WrapReturnStatements: пропуск — уже обёрнут");
                return body;
            }

            var result = new StringBuilder();
            int lineNumber = 0;
            int processedReturns = 0;

            string[] lines = body.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string rawLine = lines[i];

                // Trim trailing \r (from \r\n line endings)
                if (rawLine.EndsWith("\r"))
                    rawLine = rawLine.Substring(0, rawLine.Length - 1);

                lineNumber++;

                string lineContent = rawLine.TrimStart();
                string leading = rawLine.Substring(0, rawLine.Length - lineContent.Length);
                string trimmed = lineContent.TrimEnd();

                // Пропускаем line comments
                if (trimmed.StartsWith("--"))
                {
                    result.Append(rawLine);
                    if (i < lines.Length - 1) result.Append('\n');
                    continue;
                }

                // Убираем block comments для поиска RETURN
                string withoutBlockComments = RemoveBlockComments(trimmed);

                // RETURN на строке
                if (withoutBlockComments.TrimStart().StartsWith("RETURN", StringComparison.OrdinalIgnoreCase) &&
                    !char.IsLetterOrDigit(GetTrailingNonSpace(withoutBlockComments, "RETURN")))
                {
                    Log.Debug("WrapReturnStatements: RETURN на строке {Line}", lineNumber);
                    processedReturns++;
                    string indent = leading + "  ";
                    result.Append(leading);
                    result.Append("BEGIN");
                    result.Append('\n');
                    result.Append(indent);
                    result.Append("EXEC [audit].[sp_LogFinish] @LogID = @AuditLogID, @RowCount = @AuditRowCount, @ProcedureInfo = 'Return line number: " + lineNumber + "';");
                    result.Append('\n');
                    result.Append(indent);
                    result.Append("EXEC [audit].[sp_Print] 'Return line number: " + lineNumber + "'");
                    result.Append('\n');
                    result.Append(indent);
                    result.Append("RETURN;");
                    result.Append('\n');
                    result.Append(leading);
                    result.Append("END");
                }
                else
                {
                    result.Append(rawLine);
                }

                if (i < lines.Length - 1) result.Append('\n');
            }

            Log.Information("WrapReturnStatements: обработано RETURN={Count}, строк={Total}", processedReturns, lineNumber);
            return result.ToString();
        }

        private static string RemoveBlockComments(string s)
        {
            var sb = new StringBuilder();
            int i = 0;
            while (i < s.Length)
            {
                if (i + 1 < s.Length && s.Substring(i, 2) == "/*")
                {
                    int end = s.IndexOf("*/", i + 2);
                    if (end >= 0)
                    {
                        i = end + 2;
                    }
                    else
                    {
                        break; // незакрытый block comment
                    }
                }
                else
                {
                    sb.Append(s[i]);
                    i++;
                }
            }
            return sb.ToString();
        }

        private static char GetTrailingNonSpace(string s, string keyword)
        {
            int idx = s.LastIndexOf(keyword, StringComparison.OrdinalIgnoreCase) + keyword.Length;
            while (idx < s.Length && (s[idx] == ' ' || s[idx] == '\t')) idx++;
            return idx < s.Length ? s[idx] : '\0';
        }
    }
}
