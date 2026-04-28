# ProcDebug

Набор инструментов для работы с хранимыми процедурами SQL Server и аудита их выполнения.

## Проекты

### ApplyProcLog (основной инструмент)

Консольное приложение .NET 10 для генерации, модификации и накатывания SQL-процедур с обёрткой аудита.

**Зависимости:** `ApplyProcLog.dal`

**Команды:**

```
ApplyProcLog.exe generate [--clean] [--filter <маска>] [--except <схема>] [--use-config]
                          [-s <сервер>] [-d <база>]

    Генерация процедур из БД в папку PROC.
    --clean       — очистить Proc и Original перед генерацией
    --filter      — маска процедур (умолчание: %)
    --except      — маска исключения схем (умолчание: audit%)
    --use-config  — читать список процедур из appsettings.json
    -s / -d       — сервер и база (по умолчанию: localhost / DBTest)

    Примеры:
      ApplyProcLog.exe generate --clean --filter "Proc1%"
      ApplyProcLog.exe generate --filter "%ProcError%" -s localhost -d DBTest
      ApplyProcLog.exe generate --use-config

ApplyProcLog.exe exec-file -f <путь> [-s <сервер>] [-d <база>] [-t <таймаут>]

    Выполнить один SQL-файл на сервере.
    По умолчанию: localhost / DBTest, таймаут 300 сек.

    Пример:
      ApplyProcLog.exe exec-file -f "D:\Temp\MyProc.sql"

ApplyProcLog.exe exec-folder [-f <папка>] [-s <сервер>] [-d <база>] [-t <таймаут>]

    Выполнить все .sql файлы из папки.
    По умолчанию: папка PROC, localhost / DBTest.

    Пример:
      ApplyProcLog.exe exec-folder -s localhost -d DBTest

ApplyProcLog.exe exec-all [-s <сервер>] [-d <база>] [-p <папка PROC>]
                          [--no-table] [--no-base] [--no-original] [-t <таймаут>]

    Последовательно применить Table + Base + Original из папки PROC.
    Полезно для CI/CD: сначала структуры таблиц, затем base-процедуры,
    затем оригиналы (без аудита).

    Пример:
      ApplyProcLog.exe exec-all -s localhost -d DBTest -t 600

ApplyProcLog.exe export-data [-s <сервер>] [-d <база>] [-t <таблицы>] [-o <выход>]
                             [--max-size <КБ>] [--batch-size <строк>] [--append-go]
                             [--include-schema] [--exclude-types <типы>]

    Экспорт данных таблиц в SQL-файлы (INSERT INTO).
    Поддерживает маски: dbo.Orders, DBTest.%, %.Accounts, %.
    Ограничивает размер файла, пишет батчами.

    Примеры:
      ApplyProcLog.exe export-data -t "DBTest.Orders, dbo.Users" -o D:\Export
      ApplyProcLog.exe export-data -t "DBTest.%" --max-size 500 --batch-size 5000
```

**Обёртка аудита (WrapProcedureWithPrint):**

При генерации каждая процедура оборачивается вызовами `audit.sp_log_Start` / `audit.sp_log_Finish`:

- `DECLARE` переменные аудита вставляются после `AS`
- `sp_log_Start` — после инициализации `@AuditEnable`
- `endPrint` — перед последним `END TRY` (без `END CATCH` сразу после); если `END TRY` нет — в конец тела
- `endPrintErr` — в главном блоке `CATCH`:  перед `THROW`, или перед `END CATCH`

Дубликаты не добавляются (проверяется наличие `[audit].[sp_log_Start]`).

---

### ApplyProcLog.dal

Библиотека доступа к данным. Зависимость `ApplyProcLog`.

| Класс | Назначение |
|-------|------------|
| `DBContext` | EF Core контекст (таблицы, процедуры, настройки) |
| `DBHelper` | Запросы процедур из БД (`GetSqlProcedures`, `GetSqlProceduresByNamesAsync`) |
| `StoredProcedureInfo` | Модель процедуры (ObjectId, SchemaName, ProcedureName, Body, Params, Dates) |
| `TableDataExtractor` | Экспорт данных таблиц в INSERT-файлы с батчами и ограничением размера |

---

### ApplyProcLog.Smo

Консольное приложение .NET 10 для генерации скриптов структуры таблиц через SMO (SQL Server Management Objects).

**Имя на диске:** `ApplyProcLog.Smo.exe` (rename после сборки).

```
ApplyProcLog.Smo.exe -s <сервер> -d <база> -t "Schema.Table1,Schema.Table2" [-o <папка>]

    Генерирует CREATE TABLE + FK + defaults + indexes для указанных таблиц.
    -t обязателен. Выходная папка по умолчанию: .\TableScripts

    Примеры:
      ScriptTables.exe -s localhost -d DBTest \
        -t "DBTest.Orders,DBTest.Test" \
        -o D:\Temp\Schema
      ScriptTables.exe -s localhost -d DBTest -t "DBTest.Orders"
```

---

### ProcLog

SSDT-проект (SQL Server Database Project) — схема БД аудита. Создаёт таблицы и процедуры в схемах `dbo` и `audit`.

**Таблицы:**
- `dbo.Setting` — настройки (ключ-значение)
- `audit.AuditTypeSP` — типы аудита
- `audit.LogProcedures` — основной лог вызовов процедур (время, параметры, ошибки, RowCount)
- `audit.Setting` — настройки аудита

**Функции:**
- `dbo.fn_GetSettingValue / fn_GetSettingInt` — чтение настроек
- `audit.fn_GetAuditTypeSP` — тип аудита
- `audit.fn_log_IsLnk` — проверка линковки
- `audit.Template_LogProc` — шаблон таблицы #LogProc
- `audit.fn_BuildProcedureParams` — сериализация параметров
- `audit.fn_BuildExceptType` — построение исключений
- `audit.fn_GetEstimatedStringLength` — оценка длины строки

**Процедуры:**
- `audit.sp_log_Start` — начало логирования (запись в LogProcedures, проверка типа аудита, линковка)
- `audit.sp_log_Finish` — конец логирования (обновление Duration, RowCount, ErrorMessage)
- `audit.sp_log_Info` — дополнительная информация
- `audit.sp_lnk_Update / sp_lnk_Insert` — управление линками
- `audit.sp_Print` — вывод отладочной информации (с отключённым QuotedIdentifier)

**Безопасность:** схема `audit`, пользователь `audit`
---

## Структура папок PROC

```
PROC/
├── Schema.Table1.sql          — скрипты таблиц (от ApplyProcLog.Smo)
├── Schema.Table2.sql
├── DBTest.Proc__Name.sql  — процедуры с аудитом (от ApplyProcLog generate)
├── DBTest.Proc__Name.sql
└── Original/                  — оригиналы процедур БЕЗ аудита
    ├── DBTest.Proc__Name.sql
    └── ...
```

---

## Сборка

```powershell
# Все проекты
dotnet build ApplyProcLog\ApplyProcLog.csproj
dotnet build ApplyProcLog.Smo\ApplyProcLog.Smo.csproj
```

## Зависимости между проектами

```
ApplyProcLog (Exe)
  └── ApplyProcLog.dal (Library)
        └── Entity Framework Core + SqlClient
```
