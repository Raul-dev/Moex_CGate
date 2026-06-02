using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Serilog;

namespace ApplyProcLog;

/// <summary>
/// Выполняет SQL-файлы из указанной папки на целевой базе данных.
/// </summary>
public class SqlFileExecutor
{
    private readonly string _connectionString;
    private readonly int _commandTimeout;

    public SqlFileExecutor(string connectionString, int commandTimeout = 300)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _commandTimeout = commandTimeout;
    }

    /// <summary>
    /// Применяет все .sql файлы из папки к базе данных.
    /// </summary>
    /// <param name="folder">Путь к папке с .sql файлами</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат выполнения</returns>
    public async Task<SqlExecutionResult> ExecuteFolderAsync(string folder, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folder))
        {
            Log.Error("Папка не найдена: {Folder}", folder);
            return new SqlExecutionResult
            {
                TotalFiles = 0,
                Applied = 0,
                Skipped = 0,
                Errors = 0,
                ErrorMessages = new List<string> { $"Папка не найдена: {folder}" }
            };
        }

        var files = Directory.GetFiles(folder, "*.sql", SearchOption.TopDirectoryOnly)
                             .OrderBy(f => f, new NumberedProcedureComparer())
                             .ToList();

        Log.Information("Найдено {Count} SQL файлов в {Folder}", files.Count, folder);

        var result = new SqlExecutionResult { TotalFiles = files.Count };

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(file);
            Log.Information("[{FileName}] ->", fileName);

            try
            {
                await ExecuteFileAsync(file, cancellationToken);
                result.Applied++;
                Log.Information("[{FileName}] -> OK (applied)", fileName);
            }
            catch (SqlException ex)
            {
                if (IsAlreadyExistsError(ex))
                {
                    result.Skipped++;
                    Log.Warning("[{FileName}] -> SKIP (уже существует): {Message}", fileName, ex.Message);
                }
                else
                {
                    result.Errors++;
                    var errorMsg = $"[{fileName}] ERROR: {ex.Message}";
                    result.ErrorMessages.Add(errorMsg);
                    Log.Error("[{FileName}] -> ERROR: {Message}", fileName, ex.Message);
                    MoveToWithErrorFolder(file, folder);
                }
            }
            catch (Exception ex)
            {
                result.Errors++;
                var errorMsg = $"[{fileName}] ERROR: {ex.Message}";
                result.ErrorMessages.Add(errorMsg);
                Log.Error("[{FileName}] -> ERROR: {Message}", fileName, ex.Message);
                MoveToWithErrorFolder(file, folder);
            }
        }

        Log.Information("Готово: applied={Applied}, skipped={Skipped}, errors={Errors}",
            result.Applied, result.Skipped, result.Errors);

        return result;
    }

    /// <summary>
    /// Перемещает файл с ошибкой в подпапку WithError.
    /// </summary>
    private static void MoveToWithErrorFolder(string filePath, string baseFolder)
    {
        try
        {
            var errorFolder = Path.Combine(baseFolder, "WithError");
            if (!Directory.Exists(errorFolder))
                Directory.CreateDirectory(errorFolder);

            var fileName = Path.GetFileName(filePath);
            var destPath = Path.Combine(errorFolder, fileName);

            // Если файл уже есть в WithError — удаляем старый
            if (File.Exists(destPath))
                File.Delete(destPath);

            File.Move(filePath, destPath);
            Log.Warning("[{FileName}] перемещён в WithError", fileName);
        }
        catch (Exception ex)
        {
            Log.Error("Не удалось переместить {File} в WithError: {Error}", filePath, ex.Message);
        }
    }

    /// <summary>
    /// Удаляет блоковые комментарии /*...*/, содержащие GO в начале строки.
    /// Это нужно для корректного сплита по GO-разделителю, когда GO стоит внутри
    /// тестового блока /* ... GO ... */ перед CREATE PROCEDURE.
    /// </summary>
    private static string StripTestBlockComments(string sql)
    {
        // (?s) — dotall: . матчит переводы строк
        // \/\* — открывающий /* комментарий
        // (.*?) — минимальный захват содержимого (non-greedy)
        // \*\/\s* — закрывающий */ комментарий
        // Внутри блока ищем GO в начале строки (аналогично основному regex)
        const string pattern = @"(?ms)\/\*.*?(?:^|\r?\n)[ \t]*(?:--[ \t]*)?GO[ \t]*(?:\r?\n|$).*?\*\/\s*";
        return Regex.Replace(sql, pattern, string.Empty);
    }

    /// <summary>
    /// Выполняет один SQL-файл. Разбивает по GO и выполняет каждый батч отдельно.
    /// </summary>
    public async Task ExecuteFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var sql = await File.ReadAllTextAsync(filePath, new System.Text.UTF8Encoding(false), cancellationToken);

        // Предобработка: удалить блок-комментарии /*...GO...*/ (тестовые примеры запуска)
        sql = StripTestBlockComments(sql);

        // GO как разделитель батчей: только в начале строки, в конце строки.
        // (?=[ \t]*(?:\r?\n|$)) — после GO только пробелы/табы до \n или конца файла.
        const string separator = "SPLIT_MARKER_EXEC";
        var batchSql = Regex.Replace(sql,
            @"(?im)^(?:[ \t]*--[ \t]*)?GO(?=[ \t]*(?:\r?\n|$))",
            separator);

        var batches = batchSql.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);

        // Файл без GO — выполняем целиком
        if (batches.Length == 0)
        {
            batches = new[] { sql };
        }

        // Если в файле был GO — пропускаем первый батч (до первого GO может быть обрезанным)
        var batchesToExecute = batches.Length > 1
            ? batches.Skip(1)
            : batches;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        foreach (var batch in batchesToExecute)
        {
            var trimmedBatch = batch.Trim();
            if (string.IsNullOrWhiteSpace(trimmedBatch)) continue;

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = trimmedBatch;
            cmd.CommandTimeout = _commandTimeout;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static bool IsAlreadyExistsError(SqlException ex)
    {
        return ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("There is already an object", StringComparison.OrdinalIgnoreCase);
    }
}

public class SqlExecutionResult
{
    public int TotalFiles { get; set; }
    public int Applied { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public bool HasErrors => Errors > 0;
}

/// <summary>
/// Сравнивает имена файлов хранимых процедур: сначала по схеме/имени, затем по номеру версии.
/// Безномерные (base) идут первыми, затем по возрастанию номера.
/// Это нужно для numbered procedures: ;1 должна создаться до ;2 и ;23.
/// </summary>
public class NumberedProcedureComparer : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        // Извлекаем базовое имя (без расширения) и номер версии
        var (baseX, verX) = ParseName(Path.GetFileNameWithoutExtension(x));
        var (baseY, verY) = ParseName(Path.GetFileNameWithoutExtension(y));

        int baseCmp = string.Compare(baseX, baseY, StringComparison.OrdinalIgnoreCase);
        if (baseCmp != 0) return baseCmp;

        return verX.CompareTo(verY);
    }

    private static (string baseName, int version) ParseName(string name)
    {
        // Имя вида: BackOffice.Commisses__Depo__View;23 или BackOffice.Commisses__Depo__View
        int semi = name.LastIndexOf(';');
        if (semi < 0) return (name, 0); // без номера — базовая, first
        if (int.TryParse(name.Substring(semi + 1), out int ver))
            return (name.Substring(0, semi), ver);
        return (name, int.MaxValue); // нечисловой суффикс — в конец
    }
}
