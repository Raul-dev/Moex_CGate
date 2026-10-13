# ContextSync

CLI-инструмент синхронизации контента: из локальных `.txt`-файлов с front matter или произвольного SELECT-запроса к SQL Server в **Jira**, **Confluence** и локальные **HTML**-файлы. Плюс генератор HTML-документации по схеме таблиц SQL Server.

> **Место в Moex_CGate:** вспомогательный, отдельно стоящий инструмент. Связи с MOEX CGate / Plaza2 / RabbitMQ / Kafka в коде нет ни одной — grep по `moex|cgate|plaza|kafka|rabbit` даёт 0 совпадений. Инструмент автономен и в пайплайн биржи не входит.
>
> **Статус в git:** папка `src/ContextSync/` не отслеживается репозиторием (`?? src/ContextSync/`) — в основное решение не чекинится.
>
> *Документация составлена по анализу кода (04.09.2026).*

---

## 1. Состав решения

```
src/ContextSync/
├── ContextSync.sln            # 3 проекта
├── ContextSync.slnx           # только dal + tests (exe-проект НЕ добавлен)
├── ContextSync/               # Консольное приложение (Spectre.Console.Cli)
├── ContextSync.dal/           # Доступ к SQL Server (EF Core + SqlClient)
└── ContextSync.Tests/         # xUnit (заготовка)
```

Граф зависимостей: `ContextSync (exe)` → `ContextSync.dal (lib)` ← `ContextSync.Tests`.

| Ключевой файл                             | Назначение                                                                                                                                          |
| ----------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ContextSync/Program.cs`                            | Точка входа, регистрация 5 CLI-команд (`Program.cs:31-56`), собственный DI-adaptor `TypeRegistrar`/`TypeResolver` |
| `ContextSync/Infrastructure/DependencyInjection.cs` | Builder'ы`ConfigureServices` / `ConfigureServicesWithDbReader` (`DependencyInjection.cs:15-58`, `:60-112`)                                           |
| `ContextSync/Services/SyncOrchestrator.cs`          | Полный пайплайн Jira → Confluence → HTML (`SyncOrchestrator.cs:23-61`)                                                                      |
| `ContextSync.dal/Readers/SchemaReader.cs`           | Схема таблиц из`sys.*` (`:26-31`, `:67-91`)                                                                                                |
| `ContextSync.dal/Readers/DatabaseSourceReader.cs`   | Произвольный SQL →`SourceDocument`                                                                                                             |
| `ContextSync/Templates/table-schema.sbnhtml`        | Scriban-шаблон для команды`schema`                                                                                                          |

---

## 2. Технологии

| Компонент | Технология                                                                   | Версия  |
| ------------------ | -------------------------------------------------------------------------------------- | ------------- |
| Рантайм     | .NET                                                                                   | net10.0       |
| CLI                | Spectre.Console.Cli                                                                    | 0.49.1        |
| Шаблоны     | Scriban                                                                                | 7.2.5         |
| Данные       | EF Core / EF Core.SqlServer                                                            | 10.0.7        |
| ADO.NET            | Microsoft.Data.SqlClient                                                               | 6.1.1         |
| Логи           | Serilog (Console + File), Serilog.Expressions                                          | 4.3.1 / 5.0.0 |
| HTTP               | Microsoft.Extensions.Http + Http.Resilience                                            | 10.0.x        |
| Тесты         | xUnit 2.9.3, Moq 4.20.72 (подключён, не используется), coverlet | —            |

---

## 3. Архитектура и поток данных

Паттерн — **Vertical Slice Architecture + Command-Handler** (каждая команда = папка в `Features/`). DI-контейнер строится на каждый запуск команды внутри `ExecuteAsync`; DI-адаптер Spectre (`TypeRegistrar`) к сервисам не подключён.

```
[Источник 1] input/*.txt (FileSourceReader, YAML-подобный front matter)
[Источник 2] SQL Server — произвольный SELECT (DatabaseSourceReader, QueryText из --from-db)
[Источник 3] схема SQL Server (SchemaReader: sys.tables / sys.columns / sys.foreign_key_columns)
        ↓  SourceDocument / TableSchema (в памяти)
[Конфиг]   appsettings.json + env-переменные + CLI-override (ApplyOverrides)
[Выход 1]  Jira REST API v2 — POST rest/api/2/issue (Basic auth: username:apiToken)
[Выход 2]  Confluence REST API — POST rest/api/content (Basic auth)
[Выход 3]  output/**/*.html — 3 встроенных шаблона, для schema — Scriban (.sbnhtml)
[Логи]     Serilog → консоль + logs/contextsync-.log (ротация по дню)
```

---

## 4. Команды CLI

| Команда | Класс                                    | Что делает                                                         |
| -------------- | --------------------------------------------- | --------------------------------------------------------------------------- |
| `jira`       | `Features/SyncJira/SyncJiraCommand`         | Документы → задачи Jira                                     |
| `wiki`       | `Features/SyncWiki/SyncWikiCommand`         | Документы → страницы Confluence                           |
| `html`       | `Features/GenerateHtml/GenerateHtmlCommand` | Документы → HTML-файлы                                       |
| `all`        | `Features/SyncAll/SyncAllCommand`           | Полный пайплайн (флаги`--no-jira/--no-wiki/--no-html`) |
| `schema`     | `Features/SchemaHtml/SchemaHtmlCommand`     | Схема таблиц SQL Server → HTML через Scriban               |

Каждая команда перед изменениями показывает **превью-таблицу** (Spectre) и запрашивает `_console.Confirm`.

### Общие опции источников

```
-i, --input <dir>        каталог с .txt (по умолчанию input)
    --pattern <glob>     фильтр файлов (по умолчанию *.txt)
    --from-db            брать документы из SQL Server
    -s, --server         сервер БД (--from-db)
    -d, --database       база (--from-db)
    -q, --query          SQL-запрос (--from-db)
```

### Специфичные опции

| Команда | Опции                                                                                                                                                                                                                                                                                                                       |
| -------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `jira`       | `-p\|--project`, `--type`, `-a\|--assignee`, `-e\|--epic`, `--priority`, `-l\|--labels`                                                                                                                                                                                                                                  |
| `wiki`       | `-s\|--space`, `-p\|--parent`                                                                                                                                                                                                                                                                                                  |
| `html`       | `-o\|--output` (по умолч. `output/html`), `-t\|--template` (`default\|minimal\|report`)                                                                                                                                                                                                                               |
| `all`        | `--no-jira`, `--no-wiki`, `--no-html`, `--html-output`                                                                                                                                                                                                                                                                   |
| `schema`     | `-s\|--server` (обязателен), `-d\|--database` (обязателен), `-t\|--table`, `-f\|--filter` (LIKE), `-o\|--output` (по умолч. `output/schema`), `--template` (по умолч. `table-schema.sbnhtml`), `--templates-dir`, `--user`, `--password`, `--timeout` (по умолч. 60) |

### Примеры

```bash
# файлы → Jira
ContextSync jira -i docs -p PROJ --type Task

# SQL → Jira
ContextSync jira --from-db -s localhost -d Tasks -q "SELECT * FROM Tasks"

# файлы → Confluence
ContextSync wiki -i docs -s SPACE -p "Parent Page"

# HTML с шаблоном report
ContextSync html -i docs -o output -t report

# полный пайплайн без HTML
ContextSync all -i docs --no-html

# схема таблиц → HTML
ContextSync schema -s localhost -d GateDB -f "Orders%"
```

---

## 5. Источники данных

### 5.1 Файлы с front matter (`FileSourceReader.cs:65-130`)

Входные ключи front matter:

| Ключ                                                                                  | Мапинг                                              |
| ----------------------------------------------------------------------------------------- | --------------------------------------------------------- |
| `title`                                                                                 | `SourceDocument.Title`                                  |
| `jira-project`, `jira-issuetype`, `jira-assignee`, `jira-epic`, `jira-priority` | мета-Jira (`JiraProjectKey`, `JiraIssueType`, …) |
| `jira-labels`                                                                           | CSV → labels                                             |
| `wiki-space`, `wiki-parent`                                                           | `WikiSpaceKey`, `WikiParentTitle`                     |
| `html-template`, `html-output`                                                        | `HtmlTemplate`, `HtmlOutputPath`                      |
| остальные ключи                                                             | `Metadata`                                              |

Пример:

```
---
title: My Task
jira-project: PROJ
jira-priority: High
wiki-space: DOC
wiki-parent: Parent Page
html-template: report
---

Текст задачи (описание Jira / тело wiki-страницы).
```

### 5.2 SQL Server — произвольный SELECT (`DatabaseSourceReader.cs:40`)

Выполняется текст из `Database:QueryText` / `--query`. Маппинг колонок на документ — эвристический по имени (`DatabaseSourceReader.cs:64-127`):

- `title|name|summary` → Title
- `content|body|description|text` (первая непустая) → Content
- остальное → `Metadata`; спец-ключи `jira_project*`, `wiki_space` используются как метаданные Jira/wiki.

### 5.3 Схема БД (`SchemaReader.cs`)

Читает системные каталоги `Microsoft.Data.SqlClient` (не EF): `sys.tables`, `sys.columns`, `sys.types`, `sys.foreign_key_columns`; таблицы `is_ms_shipped = 0`; фильтр по имени — SQL LIKE. Результат: `TableSchema` + `ColumnSchema` (тип, nullable, FK → родительская таблица/колонка).

---

## 6. Конфигурация

Иерархия (приоритет от меньшего к большему):

1. `appsettings.json` (копируется в вывод всегда — `ContextSync.csproj:15-17`)
2. Env-переменные (.NET-конвенция `Section__Key`, напр. `Jira__Url`, `Database__Server`)
3. CLI-флаги команды

### appsettings.json (полный снапшот значений по умолчанию)

```json
{
  "Jira":        { "Url": "", "Username": "", "ApiToken": "", "DefaultProjectKey": "", "DefaultIssueType": "Task", "DefaultPriority": "Medium", "BatchSize": 10, "TimeoutSeconds": 30, "MaxRetries": 3 },
  "Confluence":  { "Url": "", "Username": "", "ApiToken": "", "DefaultSpaceKey": "", "DefaultParentTitle": "", "TimeoutSeconds": 30, "MaxRetries": 3 },
  "Database":    { "Server": "localhost", "Database": "", "User": "", "Password": "", "IntegratedSecurity": true, "QueryText": "", "CommandTimeoutSeconds": 60 },
  "Html":        { "OutputDirectory": "output/html", "Template": "default", "IncludeStyles": true, "GroupByMetadata": false },
  "FileSource":  { "InputDirectory": "input", "FilePattern": "*.txt", "Recursive": true, "Encoding": "UTF-8" },
  "Serilog":     { "MinimumLevel": "Information", "LogFile": "logs/ContextSync-.log" }
}
```

Значений-секретов в репо нет — поля пустые; токены задавать через env (`Jira__ApiToken`) или прямо в локальном appsettings.

### Строка подключения

Строится кодом (`ContextSync.dal/Settings/DatabaseSettings.cs:13-18`):

- при `IntegratedSecurity = true`: `Server=…;Database=…;Integrated Security=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=False`
- иначе: `Server=…;Database=…;User Id=…;Password=…;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=False`

`Encrypt=False` и `TrustServerCertificate=True` задаются всегда — шифрование TLS принудительно отключено (учитывать при подключении через сеть).

---

## 7. Выходные каналы

### 7.1 Jira (`Services/JiraService.cs`)

- REST API v2, Basic auth `Base64(username:apiToken)`, `rest/api/2/*`
- `CreateIssueAsync` (`:56-115`): POST `issue` с `project.key / summary / description / issuetype / priority / labels / assignee`
- `CreateIssuesAsync` (`:175-190`): батчи по `Jira:BatchSize`, параллельно `Task.WhenAll`
- `UpdateIssueAsync` / `IssueExistsAsync` реализованы, из команд **не вызываются** (только создание)
- Проверка соединения: GET `rest/api/2/myself`

### 7.2 Confluence (`Services/ConfluenceService.cs`)

- POST `rest/api/content` (создание, `:56-124`), PUT (обновление версии, `:126-187`), поиск родителя по заголовку `FindPageAsync` (`:189-221`)
- Тело — storage-формат; `ConvertToStorageFormat` (`:234-275`) экранирует HTML и превращает маркеры `#/-/*` в `<ul><li>`
- Создание страниц — последовательно

### 7.3 HTML

Один парсинг — два рендера:

| Рендер                | Где                                | Шаблоны                                                                                                                                                                                                                                             |
| --------------------------- | ------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `HtmlGeneratorService`    | команды`jira/wiki/html/all`  | `default` (CSS + metadata-панель), `minimal` (без CSS), `report` (градиентный header) — строятся StringBuilder'ом в `HtmlGeneratorService.cs:73-185`                                                               |
| `ScribanTemplateRenderer` | только команда`schema` | файл`Templates/table-schema.sbnhtml` (содержит `{{ table_name }}`, цикл по колонкам, бейджи FK/nullable); поиск: рабочая папка → `AppContext.BaseDirectory` (`ScribanTemplateRenderer.cs:43-46`) |

Имена файлов: `SanitizeFileName(Title).html`, UTF-8 без BOM.

---

## 8. Логирование

- `Serilog 4.3.1`, консоль + файл `logs/contextsync-.log`, ротация по дню (`Program.cs:16-19`)
- Активное использование `Serilog.Log` во всех сервисах/ридерах/командах
- Матрица CLI (прогресс, таблицы, confirm) — Spectre.Console, это интерактивный «мониторинг» рантайма

---

## 9. Сборка и запуск

```bash
# полная сборка (sln включает все 3 проекта)
dotnet build ContextSync.sln

dotnet run --project ContextSync         # из каталога src/ContextSync
dotnet run --project ContextSync -- jira --help
```

> `ContextSync.slnx` содержит только `ContextSync.dal` и `ContextSync.Tests`; exe-проект там не добавлен — для запуска CLI используйте `.sln` или проект напрямую.

Тесты:

```bash
dotnet test ContextSync.Tests
```

Покрытия реального нет — `UnitTest1.cs` содержит один пустой `Test1()` (заготовка; Moq подключён, но не используется).

---

## 10. Сценарный движок (phase 1-3 реализован)

Декларативные YAML-сценарии вместо хардкода: разбор YAML (YamlDotNet) → Scriban-шаблоны → шаги-обработчики. Референс-план: `docs/PLAN-scenario-engine.md`.

### 10.1 CLI

```
ContextSync scenario list                    # список шаблонов в каталоге шаблонов
ContextSync scenario validate <имя|путь>     # валидация YAML (без запуска)
ContextSync scenario run <имя|путь> [opts]   # запуск; dry-run по умолчанию
    --apply      # выполнить записи в Jira (по умолчанию dry-run)
    --file/-user/--weeks/--expected/--from/--to   # переопределения inputs
    --dir <DIR>  # каталог шаблонов (по умолчанию ..\..\..\WorkTimeUpload\template)
```

### 10.2 Структура

| Компонент                           | Назначение                                                                                                                                                                                              |
| -------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Scenarios/ScenarioLoader.cs`              | YAML →`ScenarioDefinition` + валидация (`ValidateExamples`-стиль: неизвестные action, дубликаты id, обязательность connection/file_format)                   |
| `Scenarios/YamlNormalizer.cs`              | YamlStream → tree`Dictionary<string,object?>` (порядоченный, строгий)                                                                                                                       |
| `Scenarios/ScenarioContext.cs`             | Scriban`ScriptObject`: `inputs`, `defaults`, `jira`, `scenario`; функции `env`, `now_utc`, `def`                                                                                           |
| `Scenarios/ScenarioRunner.cs`              | последовательный обработчик шагов;`for_each` (foreach по списку), шаговая ошибка по `on_error` (stop/continue), гарантии отрютивника |
| `Scenarios/ScribanEvaluator.cs`            | рендер строк/словарей/списков в шаблонах шагов                                                                                                                            |
| `Scenarios/Handlers/*.cs`                  | 9 действий:`jira.test_connection`, `jira.search`, `jira.get_worklogs`, `jira.add_worklog`, `file.parse_sheet`, `calendar.workdays`, `aggregate.by_day`, `report.console`, `report.link` |
| `Features/RunScenario/ScenarioCommands.cs` | CLI-команды run/list/validate                                                                                                                                                                              |

### 10.3 Контракт шага

```yaml
steps:
  - id: log
    action: jira.add_worklog
    for_each: parse.entries        # путь к списку из корня контекста
    flatten: true                  # слить ряды в единый список
    on_error: continue             # stop (default) | continue
    args:
      issue_key: "{{ item.key }}"
      hours: "{{ item.hours | def defaults.hours }}"
```

`args` — все параметры действия; результат шага попадает в `step_id` корневого контекста, доступен следующим шагам (`parse.entries`).

### 10.4 Dry-run / запись

`jira.add_worklog` в dry-run выводит `dry`-строки (без POST); `--apply` переключает на запись. Статусы ряда: `dry`, `created`, `error`. Дедупкация: перед добавлением — `GetWorklogsAsync(issue)`; если в тот же день уже есть работа того же автора — статус `skipped_duplicate`. ACCOUNT — отдельное поле тела, имя задаётся в `account_field` (см. план §Phase 0).

### 10.5 Шаблоны

Каталог по умолчанию `..\..\..\WorkTimeUpload\template`:

| Шаблон                  | Цель                                                                                                                                                                           |
| ----------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `unfilled-time-report.yaml` | Сценарий A: search → get_worklogs → aggregate.by_day → report.console (только дни со deficit) + report.link на TimesheetReport.jspa                        |
| `worklog-upload.yaml`       | Сценарий B: file.parse_sheet (регексы формата в`file_format`, dateLine `yyyyMMdd`) → jira.add_worklog по записям файла → report.console |

Тесты парсера движка: `ContextSync.Tests/Scenarios/*Tests.cs` (26 тестов).

---

## 11. Известные особенности (из анализа кода)

| # | Наблюдение                                                                                                                                                                                                                                                           | Где                                         |
| - | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------- |
| 1 | Секция`Serilog` в appsettings.json приписана, но не читается — логгер конфигурируется жёстко в коде                                                                                                             | `Program.cs:16-19`, `AppSettings.cs:12,55` |
| 2 | `.slnx` не содержит exe-проект — собирать надо через `.sln`                                                                                                                                                                              | `ContextSync.slnx:2-3`                       |
| 3 | `SyncAllAsync` пишет HTML в fallback `output/html`, игнорируя `Html:OutputDirectory` — итоговый каталог только через `--html-output`                                                                                         | `SyncOrchestrator.cs:54`                     |
| 4 | `DI TypeRegistrar` в Program.cs не связан с `DependencyInjection.ConfigureServices` — сервисы создаются локально в каждом `ExecuteAsync`; `schema` вовсе инстанцирует зависимости вручную | `SchemaHtmlCommand.cs:44-45`                 |
| 5 | `UpdateIssueAsync`/`IssueExistsAsync` — мёртвый код (не вызываются из команд)                                                                                                                                                               | `JiraService.cs`                             |
| 6 | Папка`ContextSync/Models/` пустая, README её упоминает                                                                                                                                                                                                 | `ContextSync/Models/`                        |
| 7 | Миграций EF Core нет;`ContextSyncDbContext` — оболочка без DbSet'ов (используется только `Database.SqlQueryRaw`/`CanConnect`)                                                                                                 | `ContextSync.dal/ContextSyncDbContext.cs`    |
| 8 | README внутри exe-проекта не описывает команду`schema` (5-я команда)                                                                                                                                                                  | `ContextSync/README.md:34-41`                |

---

## 12. Смежная документация

- Английский README exe-проекта: `ContextSync/README.md` (команды, примеры, front matter, конфиг)
- План развития: `docs/PLAN-scenario-engine.md` (сценарный движок YAML-шаблонов: отчёт по незаполненному времени + загрузка worklog'ов из timesheet-файла)
- Общая документация репозитория: `src/AGENTS.md` (описание Moex_CGate, стек, docker-compose, CLR)
