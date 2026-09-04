# ImportFilePerformance

Бенчмарк-стенд для поиска самого быстрого способа загрузки файлов (XML / CSV / OrderLog) в **MS SQL Server** и **PostgreSQL** с лимитом ~2 ГБ RAM.

## Идея

Главный принцип — **ноль тяжёлых аллокаций** (без `DataTable` / `List<T>` на весь файл):

1. **StreamingBulk (рекомендуемый)** — `XmlReader` / `StreamReader` → кастомный `IDataReader` → `SqlBulkCopy` (MSSQL) или `COPY BINARY` (Postgres).
2. **ParseOnly** — чистая скорость парсера без записи в БД.
3. **MaterializeThenBulk** — антипаттерн (загрузка всех строк в память), только для файлов &lt; 200 МБ.

Тестовые файлы читаются из относительного каталога `Import/` (см. `TestFilesRoot` в `appsettings.json`). Локальные абсолютные пути и дополнительные файлы (XML) задаются только в `appsettings_local.json` (не в git).

## Структура

```
ImportFilePerformance/
├── ImportFilePerformance.sln
├── start.ps1
├── AGENTS.md
├── Import/                 # CSV / OrderLog для общего запуска
├── sql/
│   ├── mssql_init.sql
│   └── postgres_init.sql
└── ImportFilePerformance/
    ├── Program.cs
    ├── appsettings.json            # относительные пути, файлы из Import/
    ├── appsettings_local.json      # локальный override, в git не входит
    ├── Properties/launchSettings.json
    ├── Readers/          # Streaming IDataReader
    ├── Importers/        # SqlBulkCopy / Npgsql COPY
    ├── Runner/           # E2E timer + peak RAM
    └── Benchmarks/       # BenchmarkDotNet (малые файлы)
```

## Подготовка БД

```powershell
# MS SQL
sqlcmd -S localhost -U sa -P <pwd> -i sql\mssql_init.sql

# Postgres (порт как в Moex_CGate: 54321)
psql -h localhost -p 54321 -U postgres -f sql\postgres_init.sql
```

Схема также создаётся автоматически при первом запуске (`EnsureSchemaAsync`).

Строки подключения — в `appsettings.json` → `BenchmarkSettings`. Локальные пути и extra-файлы — в `appsettings_local.json`.

## Запуск

```powershell
# E2E: CSV → MSSQL (ParseOnly + StreamingBulk), 3 повтора
dotnet run -c Release --project ImportFilePerformance -- --mode=e2e --db=mssql --file=CsvTradeResult

# CSV → обе БД
dotnet run -c Release --project ImportFilePerformance -- --mode=e2e --db=both --file=CsvTradeResult --strategy=ParseOnly,StreamingBulk,MaterializeThenBulk

# Большой order log (~1.3 GB) — только streaming / parse
dotnet run -c Release --project ImportFilePerformance -- --mode=e2e --db=mssql --file=OrderLogMedium --strategy=ParseOnly,StreamingBulk

# Огромный (~8.4 GB) — осторожно по времени
dotnet run -c Release --project ImportFilePerformance -- --mode=e2e --db=both --file=OrderLogHuge --strategy=StreamingBulk

# Локальный override (appsettings_local.json): XML и абсолютный TestFilesRoot
dotnet run -c Release --project ImportFilePerformance -- --mode=e2e --db=mssql --file=XmlMedium --settings=local

# BenchmarkDotNet (малые файлы, HTML-отчёт)
dotnet run -c Release --project ImportFilePerformance -- --mode=bench
```

Или: `.\start.ps1` / `.\start.ps1 -Local`

В Visual Studio два профиля:

| Профиль | Настройки |
|---------|-----------|
| `ImportFilePerformance` | `appsettings.json`, файл `CsvTradeResult` |
| `Local (appsettings_local.json)` | overlay `appsettings_local.json` (`DOTNET_ENVIRONMENT=Local`) |

## Ключи файлов (appsettings.json)

Файлы лежат в `Import/` относительно проекта:

| Ключ | Файл |
|------|------|
| CsvTradeResult | SPB_TradeResult_EQF_2023-07-14.csv |
| OrderLogMedium | orders-PUBLIC_ORDER_LOG_EQF-2019-02-07.txt |
| OrderLogHuge | orders-PUBLIC_ORDER_LOG_EQF-2021-06-03.txt |

Ключи `XmlSmall` / `XmlMedium` / `XmlLarge` доступны только через `appsettings_local.json` (профиль Local).

## Метрики

- Время (сек)
- Rows/s и MB/s
- Пиковый Working Set (RAM)
- Цель: **RAM &lt; 300–400 МБ** на любом объёме файла

## Что сравнивать

| Если… | Вывод |
|-------|--------|
| Streaming ≈ ParseOnly по времени | Упор в диск / сеть / приём БД |
| Materialize >> Streaming по RAM | Подтверждение стриминга |
| Postgres COPY vs SqlBulkCopy | Выбор СУБД / драйвера |
| RAM &gt; 400 МБ | Искать скрытые аллокации строк/буферов |

Rust pipe-парсер (вариант Б из плана) — следующий этап, если .NET ParseOnly окажется узким местом.
