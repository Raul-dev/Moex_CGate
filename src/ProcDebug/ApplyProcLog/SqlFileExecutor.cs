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

        var files = Directory.GetFiles(folder, "*.sql", SearchOption.AllDirectories)
                             .OrderBy(f => f)
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
                }
            }
            catch (Exception ex)
            {
                result.Errors++;
                var errorMsg = $"[{fileName}] ERROR: {ex.Message}";
                result.ErrorMessages.Add(errorMsg);
                Log.Error("[{FileName}] -> ERROR: {Message}", fileName, ex.Message);
            }
        }

        Log.Information("Готово: applied={Applied}, skipped={Skipped}, errors={Errors}",
            result.Applied, result.Skipped, result.Errors);

        return result;
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
