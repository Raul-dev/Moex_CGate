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

            // Папка для оригинальных процедур
            string originalDirectory = Path.Combine(targetDirectory, "Original");
            if (!Directory.Exists(originalDirectory))
            {
                Directory.CreateDirectory(originalDirectory);
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
                    // CREATE OR ALTER вместо CREATE PROCEDURE (CREATE с 1-3 пробелами)
                    body = Regex.Replace(body, @"(?i)\bCREATE\s+PROCEDURE\b", "CREATE OR ALTER PROCEDURE");
                    // Добавляем метаданные в начало файла (опционально)
                    string content = $"-- Object ID: {proc.ObjectId}\n" +
                                     $"-- Created: {proc.CreateDate}\n" +
                                     $"-- Modified: {proc.ModifyDate}\n" +
                                     $"{body}";

                    // Сохраняем оригинальную процедуру (без обёртки аудита) с CREATE OR ALTER
                    string originalBody = Regex.Replace(proc.ProcedureBody, @"(?i)\bCREATE\s+PROCEDURE\b", "CREATE OR ALTER PROCEDURE");
                    string originalContent = $"{originalBody}";
                    string originalFilePath = Path.Combine(originalDirectory, fileName);
                    await File.WriteAllTextAsync(originalFilePath, originalContent, Encoding.UTF8);

                    // Асинхронная запись в файл с кодировкой UTF-8
                    await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
                    Log.Information($"Успешно создан: {fileName} (и Original)");

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
		/// Вставляет EXEC audit.sp_log_... в начало и конец тела процедуры.
		/// </summary>
		public string WrapProcedureWithPrint(string body, string procName, string procedureParams = "")
        {
            if (string.IsNullOrWhiteSpace(body)) return body;

            // Если аудит-вызовы уже есть — не добавляем повторно
            if (body.Contains("[audit].[sp_log_Start]") || body.Contains("[audit].[sp_log_Finish]"))
                return body;

            string startPrint = $"\r\nDECLARE @LogID int, @ProcedureName varchar(510), @ProcedureParams varchar(max), @ProcedureInfo varchar(max), @ErrorMessageLog varchar(max), @RowCountLog int = 0, @AuditEnable nvarchar(256)\r\nSET @AuditEnable = [dbo].[fn_GetSettingValue]('FullAuditEnabled')\r\nSET @ProcedureName = '[' + OBJECT_SCHEMA_NAME(@@PROCID)+'].['+OBJECT_NAME(@@PROCID)+']'\r\nIF @AuditEnable IS NOT NULL \r\nBEGIN\r\n  IF OBJECT_ID('tempdb..#LogProc') IS NULL\r\n     SELECT * INTO #LogProc FROM [audit].[Template_LogProc]()\r\n  \r\n  SET @ProcedureParams = {procedureParams} \r\nEND\r\nIF @AuditEnable IS NOT NULL\r\n  EXEC [audit].[sp_log_Start] @AuditEnable = @AuditEnable, @ProcedureName = @ProcedureName, @ProcedureParams = @ProcedureParams, @LogID = @LogID OUTPUT\r\n";
            string endPrint = $"\r\n    EXEC [audit].[sp_log_Finish] @LogID = @LogID, @RowCount = @RowCountLog;\r\n";
            string endPrintErr = $"\r\n  SET @ErrorMessageLog = ERROR_MESSAGE() \r\n  EXEC [audit].[sp_log_Finish] @LogID = @LogID, @RowCount = @RowCountLog, @ErrorMessage = @ErrorMessageLog;\r\n";

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
                body = body.Insert(insertIndex, startPrint);

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
                {
                    body = body.Insert(lastEndTryIndex, endPrint);
                }
                else
                {
                    // Fallback: ищем последнее END перед END CATCH или в конце
                    int catchIndex = body.LastIndexOf("END CATCH", StringComparison.OrdinalIgnoreCase);
                    if (catchIndex != -1)
                    {
                        body = body.Insert(catchIndex, endPrint);
                    }
                    else
                    {
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
                }
            }

            // Ищем блок BEGIN CATCH и вставляем endPrintErr перед ReRaiseError/THROW или перед END CATCH
            int catchStartIdx = body.IndexOf("BEGIN CATCH", StringComparison.OrdinalIgnoreCase);
            if (catchStartIdx >= 0)
            {
                // Ищем начало области поиска: от BEGIN CATCH до END CATCH
                int catchEndIdx = body.IndexOf("END CATCH", catchStartIdx, StringComparison.OrdinalIgnoreCase);
                int searchRegionEnd = catchEndIdx >= 0 ? catchEndIdx : body.Length;

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
                if (insertBeforeIdx == -1 && catchEndIdx >= 0)
                    insertBeforeIdx = catchEndIdx;

                if (insertBeforeIdx >= 0)
                    body = body.Insert(insertBeforeIdx, endPrintErr);
            }

            return body;
        }
    }
}

