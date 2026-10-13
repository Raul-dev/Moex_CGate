# ImportFilePerformance

Бенчмарк-стенд для поиска самого быстрого способа загрузки файлов (XML / CSV / OrderLog) в **MS SQL Server** и **PostgreSQL** с лимитом ~2 ГБ RAM.

## Идея

Главный принцип — **ноль тяжёлых аллокаций** (без `DataTable` / `List<T>` на весь файл):

1. **StreamingBulk (рекомендуемый)** — `XmlReader` / `StreamReader` → кастомный `IDataReader` → `SqlBulkCopy` (MSSQL) или `COPY BINARY` (Postgres) → таблица `stg_*`.
2. **ParseOnly** — shorthand: раскрывается в **три** parser-baseline (без записи в БД):
   - `ParseOnly_StreamingBulk` — только стрим CSV
   - `ParseOnly_MessageBufferThenLoad` — CSV → named JSON-чанки (discard)
   - `ParseOnly_MessageBufferShortThenLoad` — CSV → positional JSON (discard)
3. **MaterializeThenBulk** — антипаттерн (загрузка всех строк в память), только для файлов &lt; 200 МБ.
4. **MessageBufferChunks** (только TradeResultCsv) — CSV → JSON-объекты с именами полей → `stg_trade_result_msg_buffer`.
5. **MessageBufferThenLoad** — п.4 + `EXEC load_trade_result_from_buffer` → `stg_trade_result_target`.
6. **MessageBufferShortChunks** — как п.4, но MessageBody = массив массивов без имён: `[["2023-07-14","VKCO@GR",…],…]`.
7. **MessageBufferShortThenLoad** — п.6 + `load_trade_result_from_buffer_short` → `stg_trade_result_target` (nvarchar).
8. **MessageBufferShortTypedThenLoad** — п.6 + `load_trade_result_from_buffer_short_typed` → `stg_trade_result_typed_target` (`TRY_CONVERT`).
9. **MessageStringBufferThenLoad** — CSV → `SqlBulkCopy` в `stg_trade_result_string_buffer` → `load_trade_result_from_string_buffer` → `stg_trade_result_typed_target`.

### Куда пишутся данные (ваш `./start.ps1`)

| Стратегия | БД | Таблица / действие |
|-----------|-----|-------------------|
| ParseOnly | — | ничего не пишет |
| StreamingBulk | `ImportFile` (MSSQL из `appsettings.json`) | `dbo.stg_trade_result_csv` (колонки = header CSV, `nvarchar(512)`) |
| MessageBufferChunks | та же | `dbo.stg_trade_result_msg_buffer` (`MessageBody` = JSON-объекты ~100 строк) |
| MessageBufferThenLoad | та же | buffer + `dbo.stg_trade_result_target` через `load_trade_result_from_buffer` |
| MessageBufferShortChunks | та же | buffer, `MessageBody` = positional arrays |
| MessageBufferShortThenLoad | та же | buffer → `stg_trade_result_target` via `load_…_short` |
| MessageBufferShortTypedThenLoad | та же | buffer → `stg_trade_result_typed_target` via `load_…_short_typed` |
| MessageStringBufferThenLoad | та же | `stg_trade_result_string_buffer` (bulk) → `stg_trade_result_typed_target` via `load_trade_result_from_string_buffer` |

Строка подключения по умолчанию: `Server=localhost;Database=ImportFile;...`.

Строки лога `MsSql/StreamingBulk/...#1..#3` = СУБД / стратегия / файл / повтор; SUMMARY — лучший из `RepeatCount` повторов; в конце — таблица Mean/Error/StdDev в стиле BenchmarkDotNet.

Тестовые файлы читаются из относительного каталога `Import/` (см. `TestFilesRoot` в `appsettings.json`). Локальные абсолютные пути и дополнительные файлы (XML) задаются только в `appsettings_local.json` (не в git).

## Структура

```
ImportFilePerformance/
├── ImportFilePerformance.sln
├── start.ps1
├── AGENTS.md
├── Import/                 # CSV / OrderLog для общего запуска
├── sql/
│   ├── mssql_init.sql / postgres_init.sql
│   ├── init-mssql.ps1
│   ├── mssql/          # TradeResult buffer/target + load_* procs
│   └── postgres/
└── ImportFilePerformance/
    ├── Program.cs
    ├── appsettings.json
    ├── Readers/
    ├── Importers/
    ├── Runner/
    └── Benchmarks/
```

## Подготовка БД

```powershell
# MS SQL — база + TradeResult buffer/target + load_* процедуры
cd sql
.\init-mssql.ps1 -Server localhost -User sa -Password <pwd>
# или по отдельности:
# sqlcmd -S localhost -U sa -P <pwd> -i mssql_init.sql
# sqlcmd -S localhost -U sa -P <pwd> -i mssql\stg_trade_result.sql
# sqlcmd -S localhost -U sa -P <pwd> -i mssql\load_trade_result_from_buffer.sql
# sqlcmd -S localhost -U sa -P <pwd> -i mssql\load_trade_result_from_buffer_short.sql

# Postgres (порт как в Moex_CGate: 54321)
psql -h localhost -p 54321 -U postgres -f sql\postgres_init.sql
psql -h localhost -p 54321 -U postgres -d ImportFile -f sql\postgres\stg_trade_result.sql
psql -h localhost -p 54321 -U postgres -d ImportFile -f sql\postgres\load_trade_result_from_buffer.sql
psql -h localhost -p 54321 -U postgres -d ImportFile -f sql\postgres\load_trade_result_from_buffer_short.sql
```

Схема также создаётся/обновляется автоматически при первом запуске бенча (`EnsureSchemaAsync`), но для «базы с нуля» используйте скрипты выше.

Структура SQL:

```
sql/
├── mssql_init.sql
├── postgres_init.sql
├── init-mssql.ps1
├── mssql/
│   ├── stg_trade_result.sql                      # buffer + target + csv staging
│   ├── load_trade_result_from_buffer.sql         # named JSON ($.col)
│   └── load_trade_result_from_buffer_short.sql   # positional ($[n], как crs.load_OrdersLog)
└── postgres/
    ├── stg_trade_result.sql
    ├── load_trade_result_from_buffer.sql
    └── load_trade_result_from_buffer_short.sql
```

Строки подключения — в `appsettings.json` → `BenchmarkSettings`. Локальные пути и extra-файлы — в `appsettings_local.json`.

## Запуск

```powershell
# E2E: baseline ParseOnly, then 3 comparable full loads
dotnet run -c Release --project ImportFilePerformance -- --mode=e2e --db=mssql --file=CsvTradeResult --strategy=ParseOnly,StreamingBulk,MessageBufferThenLoad,MessageBufferShortThenLoad

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
| MessageBufferThenLoad vs StreamingBulk | Цена JSON-chunk + OPENJSON load vs прямой bulk |
| MessageBufferShort* vs MessageBuffer* | Экономия размера JSON (без имён полей) vs named objects |
| RAM &gt; 400 МБ | Искать скрытые аллокации строк/буферов |

Rust pipe-парсер (вариант Б из плана) — следующий этап, если .NET ParseOnly окажется узким местом.
