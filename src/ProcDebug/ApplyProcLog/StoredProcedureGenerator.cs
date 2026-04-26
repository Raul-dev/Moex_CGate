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
        /// <summary>
        /// Создает SQL файлы для каждой процедуры в текущей директории.
        /// </summary>
        public async Task CreateProcedureFilesAsync(IEnumerable<StoredProcedureInfo> procedures, string targetDirectory = "PROC")
        {
            // Получаем путь к текущей директории приложения
            if (targetDirectory == "PROC")
            {
                targetDirectory = Path.Combine(Directory.GetCurrentDirectory(), targetDirectory);
                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }
            }

            foreach (var proc in procedures)
            {
                // Формируем безопасное имя файла: Schema.Name.sql
                // Используем GetInvalidFileNameChars на случай специфических символов в именах
                string fileName = $"{proc.SchemaName}.{proc.ProcedureName}.sql";
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    fileName = fileName.Replace(c, '_');
                }

                string filePath = Path.Combine(targetDirectory, fileName);

                try
                {
                    string body = WrapProcedureWithPrint(proc.ProcedureBody, proc.ProcedureName, proc.ProcedureParams);
                    // Добавляем метаданные в начало файла (опционально)
                    string content = $"-- Object ID: {proc.ObjectId}\n" +
                                     $"-- Created: {proc.CreateDate}\n" +
                                     $"-- Modified: {proc.ModifyDate}\n" +
                                     $"{body}";

                    // Асинхронная запись в файл с кодировкой UTF-8
                    await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
                    Log.Information($"Успешно создан: {fileName}");
                    
                }
                catch (Exception ex)
                {
                    Log.Error($"Ошибка при сохранении {fileName}: {ex.Message}");
                }
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
                proc.ProcedureBody = WrapProcedureWithPrint(proc.ProcedureBody, proc.ProcedureName);

                string fileName = $"{proc.SchemaName}.{proc.ProcedureName}.sql";
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);

                await File.WriteAllTextAsync(filePath, proc.ProcedureBody, Encoding.UTF8);
            }
        }

        /// <summary>
        /// Вставляет EXEC sp_Print в начало и конец тела процедуры.
        /// </summary>
        public string WrapProcedureWithPrint(string body, string procName, string procedureParams = "")
        {
            if (string.IsNullOrWhiteSpace(body)) return body;

            string startPrint = $"\r\nDECLARE @LogID int, @ProcedureName varchar(510), @ProcedureParams varchar(max), @ProcedureInfo varchar(max), @ErrorMessageLog varchar(max), @RowCountLog int = 0, @AuditEnable nvarchar(256)\r\nSET @AuditEnable = [dbo].[fn_GetSettingValue]('FullAuditEnabled')\r\nSET @ProcedureName = '[' + OBJECT_SCHEMA_NAME(@@PROCID)+'].['+OBJECT_NAME(@@PROCID)+']'\r\nIF @AuditEnable IS NOT NULL \r\nBEGIN\r\n  IF OBJECT_ID('tempdb..#LogProc') IS NULL\r\n     SELECT * INTO #LogProc FROM [audit].[Template_LogProc]()\r\n  \r\n  SET @ProcedureParams = {procedureParams} \r\nEND\r\nIF @AuditEnable IS NOT NULL\r\n  EXEC [audit].[sp_log_Start] @AuditEnable = @AuditEnable, @ProcedureName = @ProcedureName, @ProcedureParams = @ProcedureParams, @LogID = @LogID OUTPUT\r\n";
            string endPrint = $"\r\n    EXEC [audit].[sp_log_Finish] @LogID = @LogID, @RowCount = @RowCountLog;\r\n";
            string endPrintErr = $"\r\n SET @ErrorMessageLog = ERROR_MESSAGE() \r\n    EXEC [audit].[sp_log_Finish] @LogID = @LogID, @RowCount = @RowCountLog, , @ErrorMessage = @ErrorMessageLog;\r\n";

            // Регулярное выражение ищет ключевое слово AS (игнорируя регистр)
            // и вставляет принт сразу после него.
            var regex = new Regex(@"(?i)\bAS\b");
            var match = regex.Match(body);

            if (match.Success)
            {
                int insertIndex = match.Index + match.Length;

                // Вставляем начало
                body = body.Insert(insertIndex, startPrint);

                // Ищем последнее вхождение END (если оно есть) или просто добавляем в конец
                int lastEndIndex = body.LastIndexOf("END", StringComparison.OrdinalIgnoreCase);
                if (lastEndIndex != -1)
                {
                    body = body.Insert(lastEndIndex, endPrint);
                }
                else
                {
                    body += endPrint;
                }
            }

            // Регулярное выражение ищет ключевое слово BEGIN CATCH (игнорируя регистр)
            // и вставляет принт сразу после него.
            var regexErr = new Regex(@"(?i)\bBEGIN CATCH\b");
            var matchErr = regexErr.Match(body);
            if (matchErr.Success)
            {
                int insertIndex = matchErr.Index + matchErr.Length;

                // Вставляем начало
                body = body.Insert(insertIndex, endPrintErr);

            }
            return body;
        }
    }
}
