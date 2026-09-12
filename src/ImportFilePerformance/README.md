# ImportFilePerformance

Бенчмарк-стенд (.NET 9) для сравнения способов загрузки файлов (CSV / OrderLog / XML) в **MS SQL Server** и **PostgreSQL** с контролем времени, throughput и peak RAM.

## Результаты: Full load (MSSQL)

Файл: `Import/SPB_TradeResult_EQF_2023-07-14.csv`  
Размер: **7.29 MB** · строк данных: **15 467** · колонок: **73** · разделитель `;`  
БД: `ImportFile` · `RepeatCount=3` · режим e2e

// Full load — comparable end-to-end (file → rows in DB)

| Method | Dest | Mean | Error | StdDev | Rows/s | Peak RAM |
| ------ | ---- | ---: | ----: | -----: | -----: | -------: |
| MsSql_StreamingBulk | stg_trade_result_csv | 300.446 ms | 137.894 ms | 121.856 ms | 91 661 | 65.5 MB |
| MsSql_StreamingBulkTyped | stg_trade_result_typed_csv | 325.656 ms | 52.111 ms | 46.050 ms | 56 727 | 67.4 MB |
| MsSql_MessageBufferThenLoad | stg_trade_result_target | 2.108 s | 102.374 ms | 90.468 ms | 7 570 | 138.7 MB |
| MsSql_MessageBufferShortThenLoad | stg_trade_result_target | 1.242 s | 39.767 ms | 35.142 ms | 12 745 | 99.8 MB |
| MsSql_MessageBufferShortTypedThenLoad | stg_trade_result_typed_target | 1.831 s | 20.911 ms | 18.479 ms | 8 546 | 99.8 MB |
| MsSql_MessageStringBufferThenLoad | stg_trade_result_typed_target | 992.236 ms | 20.042 ms | 17.711 ms | 15 829 | 72.6 MB |

**Краткий вывод по этому прогону**

| Место | Метод | Почему |
| ----: | ----- | ------ |
| 1 | **StreamingBulk** | один проход CSV → `SqlBulkCopy`, без JSON и без второй фазы |
| 2 | **StreamingBulkTyped** | тот же bulk, но конвертация строк → date/decimal/bigint на стороне клиента |
| 3 | **MessageStringBufferThenLoad** | bulk в wide-string staging + один `EXEC` с `TRY_CONVERT` |
| 4 | **MessageBufferShortThenLoad** | JSON без имён полей (`$[n]`) дешевле named, но дороже string-bulk |
| 5 | **MessageBufferShortTypedThenLoad** | short JSON + `TRY_CONVERT` в typed target |
| 6 | **MessageBufferThenLoad** | named JSON (`$.col`) — самый тяжёлый payload и парсинг |

### Chunks vs построчный string-buffer (оба → typed target)

**MessageStringBufferThenLoad (~0.98 s)** примерно **в 1.8–2× быстрее**, чем **MessageBufferShortTypedThenLoad (~1.83 s)** — оба пишут в `stg_trade_result_typed_target` с `TRY_CONVERT`.

| | ShortTyped (chunks) | StringBuffer (построчный bulk) |
|--|---------------------|--------------------------------|
| Клиент | CSV → JSON-массивы чанками по 100 + сериализация | CSV → сразу `SqlBulkCopy` строк |
| Buffer | мало строк msg_buffer, но толстый `nvarchar(max)` JSON | много строк, широкие `nvarchar`, зато без JSON |
| Load | `OPENJSON` + `TRY_CONVERT` | простой `SELECT TRY_CONVERT` по колонкам |
| Mean (этот прогон) | **~1.83 s** | **~0.98 s** |
| Peak RAM | ~100 MB | ~73 MB |

**Итог:** для этого файла и MSSQL **построчный string-bulk + convert на SQL** дешевле, чем **JSON-chunks + OPENJSON + convert**. Chunks выигрывают в другой модели (очередь/MQ, повторная доставка, один `MessageBody`), а не в чистом throughput «файл → typed table».

Отдельно в отчёте есть таблицы **ParseOnly** (только парсер) и **Load procedure only** (только `EXEC load_*`; у StreamingBulk\* = **0**).

---

## Запуск

```powershell
# БД с нуля (login/user + таблицы + load_* процедуры)
cd sql
.\init-mssql.ps1 -Server localhost -User sa -Password <pwd>

# E2E: 6 comparable full loads (+ ParseOnly baselines)
cd ..
.\start.ps1
# или:
.\start.ps1 -Strategy "ParseOnly,StreamingBulk,StreamingBulkTyped,MessageBufferThenLoad,MessageBufferShortThenLoad,MessageBufferShortTypedThenLoad,MessageStringBufferThenLoad"
```

Подключение и ключи файлов — `ImportFilePerformance/appsettings.json` (`BenchmarkSettings`).

---

## Методы Full load — подробно

Ниже для каждого метода: куда пишем, из чего складывается стоимость, и последовательность вызовов в C# (как в `EndToEndRunner.ExecuteAsync`).

### 1. StreamingBulk → `stg_trade_result_csv`

**Идея:** максимально прямой путь — стрим CSV → `IDataReader` → `SqlBulkCopy`.  
**Dest:** все колонки `nvarchar(512)` (типов нет).  
**Load proc:** нет (= 0 в таблице Load procedure).

**Стоимость**

| Этап | Что происходит | Дорого? |
|------|----------------|---------|
| Parse | `DelimitedCsvDataReader` читает файл построчно | низкая |
| Convert | нет | — |
| Write | `SqlBulkCopy` (TableLock, streaming) | основная |
| Load SQL | нет | — |

**Вызовы**

```csharp
await using var reader = DelimitedCsvDataReader.Create(filePath, ';');
// SqlServerBulkImporter.BulkInsertAsync:
await using var conn = new SqlConnection(cs);
await conn.OpenAsync(ct);
using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.TableLock, null)
{
    DestinationTableName = "dbo.[stg_trade_result_csv]",
    BatchSize = batchSize,
    EnableStreaming = true,
};
// map columns by name...
await bulk.WriteToServerAsync(reader, ct);
```

---

### 2. StreamingBulkTyped → `stg_trade_result_typed_csv`

**Идея:** тот же streaming bulk, но dest с типами (`date` / `decimal(28,8)` / `bigint` / `nvarchar(1024)` = **2×** ожидаемой ширины строк).  
**Convert:** SqlClient при bulk приводит `string` → typed (как BCP в typed table).  
**Load proc:** нет (= 0).

**Стоимость**

| Этап | Что происходит | Дорого? |
|------|----------------|---------|
| Parse | тот же CSV stream | низкая |
| Convert | coercion на каждом значении (date/decimal/…) | средняя (+~8% Mean vs untyped) |
| Write | `SqlBulkCopy` в typed table | основная |
| Load SQL | нет | — |

Флаги вроде `IsAddress=Y/N` должны быть **строками**, не `tinyint` (иначе `FormatException`).

**Вызовы**

```csharp
// TradeResultMessageBufferPipeline.WriteTypedCsvBulkAsync
await using var reader = DelimitedCsvDataReader.Create(filePath, ';');
await using var conn = new SqlConnection(cs);
await conn.OpenAsync(ct);
using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.TableLock, null)
{
    DestinationTableName = "dbo.[stg_trade_result_typed_csv]",
    BatchSize = batchSize,
    EnableStreaming = true,
};
await bulk.WriteToServerAsync(reader, ct); // string → typed coercion
```

---

### 3. MessageBufferThenLoad → `stg_trade_result_target`

**Идея:** модель MQ Imp — CSV → JSON-чанки с **именами полей** → message-buffer → `OPENJSON` → target (все `nvarchar(512)`).  
**ChunkRowSize:** 100 (appsettings).

**Стоимость**

| Этап | Что происходит | Дорого? |
|------|----------------|---------|
| Parse + JSON | `Dictionary` на строку + `JsonSerializer` чанков `[{"tradedate":…},…]` | высокая (RAM ~139 MB) |
| Write buffer | `SqlBulkCopy` в `stg_trade_result_msg_buffer` (`MessageBody nvarchar(max)`) | средняя |
| Load SQL | `EXEC load_trade_result_from_buffer` — `OPENJSON … '$.col'` | высокая |
| Convert | нет (остаёмся в nvarchar) | — |

**Вызовы**

```csharp
await TradeResultMessageBufferPipeline.WriteChunksAsync(
    db, cs, filePath, chunkRowSize: 100, MessageBodyFormat.NamedObjects, ct);

var sw = Stopwatch.StartNew();
var rows = await TradeResultMessageBufferPipeline.LoadFromBufferAsync(
    db, cs, MessageBodyFormat.NamedObjects, ct); // EXEC dbo.load_trade_result_from_buffer
sw.Stop(); // → LoadProcedureSeconds
```

Внутри write: CSV → `List<Dictionary<string,string?>>` → `JsonSerializer.Serialize` → bulk в msg_buffer.

---

### 4. MessageBufferShortThenLoad → `stg_trade_result_target`

**Идея:** как п.3, но MessageBody = **массив массивов** без имён: `[["2023-07-14","VKCO@GR",…],…]`.  
Парсинг как в `[crs].[load_OrdersLog]`: `OPENJSON … WITH (col '$[0]', …)`.

**Стоимость**

| Этап | Что происходит | Дорого? |
|------|----------------|---------|
| Parse + JSON | `string?[]` вместо Dictionary — меньше аллокаций/текста | ниже named |
| Write buffer | тот же msg_buffer | средняя |
| Load SQL | `EXEC load_trade_result_from_buffer_short` | ниже named OPENJSON |
| Convert | нет | — |

**Вызовы**

```csharp
await TradeResultMessageBufferPipeline.WriteChunksAsync(
    db, cs, filePath, chunkRowSize: 100, MessageBodyFormat.PositionalArrays, ct);

var sw = Stopwatch.StartNew();
var rows = await TradeResultMessageBufferPipeline.LoadFromBufferAsync(
    db, cs, MessageBodyFormat.PositionalArrays, ct); // EXEC …_short
sw.Stop();
```

---

### 5. MessageBufferShortTypedThenLoad → `stg_trade_result_typed_target`

**Идея:** тот же short JSON в msg_buffer, но load пишет в **typed** target через `TRY_CONVERT` (`load_trade_result_from_buffer_short_typed`).

**Стоимость**

| Этап | Что происходит | Дорого? |
|------|----------------|---------|
| Parse + JSON | как Short | средняя |
| Write buffer | как Short | средняя |
| Load SQL | OPENJSON `$[n]` + `TRY_CONVERT(date/decimal/bigint/…)` | выше Short (без convert) |
| Convert | на SQL Server в процедуре | средняя/высокая |

**Вызовы**

```csharp
await TradeResultMessageBufferPipeline.WriteChunksAsync(
    db, cs, filePath, chunkRowSize: 100, MessageBodyFormat.PositionalArrays, ct);

var sw = Stopwatch.StartNew();
var rows = await TradeResultMessageBufferPipeline.LoadFromBufferAsync(
    db, cs, MessageBodyFormat.PositionalArrays, ct, toTypedTarget: true);
    // EXEC dbo.load_trade_result_from_buffer_short_typed
    // INSERT stg_trade_result_typed_target SELECT TRY_CONVERT(...) FROM OPENJSON...
sw.Stop();
```

---

### 6. MessageStringBufferThenLoad → `stg_trade_result_typed_target`

**Идея:** построчный `SqlBulkCopy` в staging, зеркало typed target, но **все поля строковые и ×3 шире** (`nvarchar(1536)` / скаляры `nvarchar(192)`), затем `EXEC load_trade_result_from_string_buffer` с `TRY_CONVERT` в typed target.

**Стоимость**

| Этап | Что происходит | Дорого? |
|------|----------------|---------|
| Parse | CSV stream | низкая |
| Write buffer | `SqlBulkCopy` → `stg_trade_result_string_buffer` (широкие строки) | средняя (быстрый bulk) |
| Load SQL | `INSERT … SELECT TRY_CONVERT(…) FROM string_buffer` | средняя |
| Convert | на SQL (не на клиенте) | средняя |
| JSON | нет | — |

Ближе всего к StreamingBulkTyped по смыслу «bulk + типы», но типы применяются **второй фазой** на сервере.

**Вызовы**

```csharp
await TradeResultMessageBufferPipeline.WriteStringBufferAsync(
    db, cs, filePath, batchSize, ct); // SqlBulkCopy → stg_trade_result_string_buffer

var sw = Stopwatch.StartNew();
var rows = await TradeResultMessageBufferPipeline.LoadFromStringBufferAsync(db, cs, ct);
    // EXEC dbo.load_trade_result_from_string_buffer
sw.Stop();
```

---

## Схема объектов (MSSQL)

| Объект | Назначение |
|--------|------------|
| `stg_trade_result_csv` | StreamingBulk (все nvarchar) |
| `stg_trade_result_typed_csv` | StreamingBulkTyped (typed, строки ×2) |
| `stg_trade_result_msg_buffer` | MessageBody JSON chunks (named / short) |
| `stg_trade_result_target` | load named/short → все nvarchar |
| `stg_trade_result_string_buffer` | построчный string staging (×3) |
| `stg_trade_result_typed_target` | short_typed / string_buffer load |
| `load_trade_result_from_buffer` | OPENJSON `$.col` |
| `load_trade_result_from_buffer_short` | OPENJSON `$[n]` |
| `load_trade_result_from_buffer_short_typed` | `$[n]` + TRY_CONVERT |
| `load_trade_result_from_string_buffer` | string staging → typed |

Скрипты: `sql/mssql/*.sql`, прогон: `sql/init-mssql.ps1`.  
Схема также создаётся/чинит `isaddress` (Y/N) при первом e2e через `EnsureSchemaAsync`.

---

## Структура проекта

```
ImportFilePerformance/
├── start.ps1
├── README.md                 ← этот файл
├── AGENTS.md
├── Import/                   # тестовые файлы
├── sql/                      # init БД + load_* DDL
└── ImportFilePerformance/
    ├── Program.cs
    ├── appsettings.json
    ├── Readers/              # streaming IDataReader
    ├── Importers/            # SqlBulkCopy / COPY / message-buffer pipeline
    ├── Runner/               # e2e timer + Mean/Error/StdDev таблицы
    └── Benchmarks/           # BenchmarkDotNet (малые файлы)
```

## Метрики в отчёте

1. **Baseline ParseOnly × N** — только клиентский парсер (без записи в БД).  
2. **Load procedure only** — только `EXEC load_*` (StreamingBulk\* = 0).  
3. **Full load** — end-to-end file → конечная таблица (**Dest** в таблице).

Mean / Error / StdDev считаются по `RepeatCount` повторам (Error ≈ половина 95% CI).
