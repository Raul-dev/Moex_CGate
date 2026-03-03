# Документация по проекту CGate Database

## 1. Описание проекта и его назначение

**CGate** — это SQL Server Database проект (SSDT), разработанный для интеграции с торговой системой Московской биржи (MOEX). Проект обеспечивает:

- Приём и обработку данных о заявках (orders) и сделках (deals) из торговой системы
- Буферизация входящих сообщений с использованием механизма snapshot isolation для высокой производительности
- Аудит всех операций с хранимыми процедурами
- Интеграция с RabbitMQ для асинхронного обмена сообщениями
- Хранение метаданных о сообщениях и адаптерах

## 2. Технологический стек

| Компонент | Технология |
|-----------|------------|
| СУБД | SQL Server 2022 (Sql160 schema provider) |
| Среда разработки | Visual Studio 2022 + SSDT |
| Build System | MSBuild + MSBuild.Sdk.SqlProj |
| Deployment | SqlPackage.exe (DACPAC) |
| Скрипты развёртывания | PowerShell |
| CLR Assemblies | RabbitMQ.Client, RabbitMQSqlClr4 |
| Модель безопасности | Windows Authentication / SQL Authentication |
| Isolation Level | SNAPSHOT (для высокой производительности) |

## 3. Архитектура и структура проекта

### 3.1 Схемы базы данных

Проект использует 4 схемы для логического разделения объектов:

| Схема | Назначение |
|-------|------------|
| `dbo` | Основные служебные таблицы, настройки, функции |
| `crs` | Данные торговой системы (заявки, сделки, события) |
| `audit` | Логирование аудита операций |
| `rmq` | Интеграция с RabbitMQ |

### 3.2 Таблицы (Tables)

#### Схема `dbo`

| Таблица | Назначение |
|---------|------------|
| `Setting` | Системные настройки (ключ-значение) |
| `session_state` | Состояние сессий обработки |
| `session` | Информация о сессиях |
| `session_log` | Логи выполнения сессий |
| `msgqueue` | Очередь сообщений |
| `metamap` | Метаданные сообщений (ключ → таблица) |
| `metaadapter` | Конфигурация адаптеров метаданных |
| `msgtype` | Типы сообщений |
| `data_source` | Источники данных |
| `DataGeneration` | Данные для генерации тестов |

#### Схема `crs` (Trading Data)

| Таблица | Назначение |
|---------|------------|
| `orders_log` | Основной журнал заявок (публичные данные) |
| `orders_log_buffer` | Буфер входящих заявок (буферизация перед обработкой) |
| `multileg_orders_log` | МультиLEG заявки |
| `user_deal` | Сделки пользователей |
| `user_multileg_deal` | МультиLEG сделки |
| `heartbeat` | heartbeat-мониторинг |
| `sys_events` | Системные события |

#### Схема `audit`

| Таблица | Назначение |
|---------|------------|
| `LogProcedures` | Журнал выполнения хранимых процедур |
| `LogText` | Журнал текстовых логов |
| `LogText_buffer` | Буфер текстовых логов |
| `LogError` | Журнал ошибок |
| `LogError_buffer` | Буфер ошибок |
| `AuditTypeSP` | Типы аудита хранимых процедур |
| `AuditTypeLT` | Типы аудита логов текста |
| `Setting` | Настройки аудита |

#### Схема `rmq` (RabbitMQ)

| Таблица | Назначение |
|---------|------------|
| `RabbitSetting` | Настройки RabbitMQ |
| `RabbitEndpoint` | Конечные точки подключения к RabbitMQ |

### 3.3 Хранимые процедуры (Stored Procedures)

#### Схема `dbo`

| Процедура | Назначение |
|-----------|------------|
| `sp_SaveSessionState` | Сохранение состояния сессии |
| `sp_GenerationRandomArray` | Генерация тестовых данных |

#### Схема `crs`

| Процедура | Назначение |
|-----------|------------|
| `load_orders_log` | Загрузка заявок из буфера в основную таблицу (с MERGE) |
| `load_orders_log_array` | Пакетная загрузка заявок |

#### Схема `audit`

| Процедура | Назначение |
|-----------|------------|
| `sp_log_Start` | Начало логирования выполнения процедуры |
| `sp_log_Finish` | Завершение логирования |
| `sp_log_Info` | Логирование информационного сообщения |
| `sp_lnk_Insert` | Логирование в связанную таблицу (INSERT) |
| `sp_lnk_Update` | Логирование в связанную таблицу (UPDATE) |
| `sp_lnkLT_Insert` | Логирование текстовых сообщений |
| `sp_LogText_Add` | Добавление текстового лога |
| `load_LogText` | Загрузка буферизованных логов |
| `load_LogError` | Загрузка буферизованных ошибок |
| `sp_rmq_PostSp` | Отправка лога процедуры в RabbitMQ |
| `sp_rmq_PostLT` | Отправка текстового лога в RabbitMQ |
| `sp_Initialise` | Инициализация аудита |

#### Схема `rmq`

| Процедура | Назначение |
|-----------|------------|
| `sp_clr_InitialiseRabbitMq` | Инициализация RabbitMQ (CLR) |
| `sp_clr_PostRabbitMsg` | Отправка сообщения (CLR) |
| `sp_clr_ReloadRabbitEndpoints` | Перезагрузка endpoints (CLR) |
| `sp_PostRabbitMsg` | Отправка сообщения |
| `sp_UpsertRabbitEndpoint` | Создание/обновление endpoint |
| `sp_GetRabbitEndpoints` | Получение списка endpoints |
| `sp_GetLocalDBConnString` | Получение строки подключения |

### 3.4 Функции (Functions)

#### Схема `dbo`

| Функция | Назначение |
|---------|------------|
| `fn_GetSettingValue` | Получение значения настройки (строка) |
| `fn_GetSettingInt` | Получение значения настройки (число) |
| `fn_GetBufferingDays` | Получение количества дней буферизации |
| `fn_GenerationRandomField` | Генерация случайного значения |

#### Схема `audit`

| Функция | Назначение |
|---------|------------|
| `fn_log_IsLnk` | Проверка типа связанного лога |
| `fn_GetAuditTypeSP` | Получение типа аудита процедуры |
| `fn_GetAuditTypeLT` | Получение типа аудита текстового лога |

### 3.5 Безопасность

| Объект | Описание |
|--------|----------|
| `CGateUser` | Основной пользователь БД (db_owner) |
| Роль `crs` | Права на схему crs |
| Роль `audit` | Права на схему audit |
| Роль `rmq` | Права на схему rmq |
| Роль `rmqRole` | Роль для CLR-процедур RabbitMQ |

### 3.6 CLR Assembly

Проект использует SQL CLR для интеграции с RabbitMQ:
- `RabbitMQ.Client` — официальный клиент RabbitMQ
- `RabbitMQSqlClr4` — кастомная CLR-библиотека для работы с RabbitMQ из SQL Server

**Важно:** Для использования CLR необходимо включить `clr enabled` и установить `TRUSTWORTHY ON`.

## 4. CI/CD

### 4.1 Сборка (Build)

Проект собирается с помощью MSBuild:

```powershell
dotnet build "CGate.sqlproj" /p:NetCoreBuild=true /p:Configuration=Release
```

или через Visual Studio.

Результат сборки — DACPAC файл (`CGate.dacpac`).

### 4.2 Скрипт развёртывания

Основной скрипт: `ScriptsFolder/dbdeploy.ps1`

**Параметры:**
- `-TargetServerName` — сервер БД (по умолчанию localhost)
- `-TargetDBname` — имя БД (по умолчанию cgate_uts_tmp)
- `-PublishMode` — режим: Build, Deploy, DeployOnly
- `-IsRebuild` — принудительная пересборка
- `-SqlPassword` — пароль SQL

**Процесс развёртывания:**
1. Сборка проекта (MSBuild)
2. Генерация DACPAC
3. Публикация через SqlPackage.exe

**Ключевые параметры SqlPackage:**
- `/p:BlockOnPossibleDataLoss=False` — игнорировать предупреждения о потере данных
- `/p:IgnorePermissions=True` — не применять права доступа
- Переменная `LinkSRVLog` — linked server для логирования

### 4.3 Pre/Post Deployment

- **Pre-deployment:** `Dictionaries/Script.PreDeployment1.sql`
- **Post-deployment:** `Dictionaries/Script.PostDeployment1.sql`

Post-deployment скрипт автоматически выполняет:
- `:r .\AuditSetup.sql`
- `:r .\session_state.sql`
- `:r .\metamap.sql`

## 5. Миграции данных

### 5.1 Механизм миграций

Проект использует **DACPAC-модель** (декларативный подход):
- Все изменения описываются в исходном коде (таблицы, процедуры, функции)
- SqlPackage автоматически генерирует инкрементный скрипт миграции при publish

### 5.2 Буферизация данных

Для высоконагруженных операций используется паттерн буферизации:

1. Данные записываются в буферную таблицу (`*_buffer`)
2. Процедура `load_*` обрабатывает данные пакетами (TOP 200000)
3. Используется `MERGE` для upsert операций
4. Изоляция SNAPSHOT предотвращает блокировки

**Пример потока данных:**
```
Входящее сообщение (JSON) → orders_log_buffer → load_orders_log → orders_log
```

### 5.3 Аудит миграций

Все критические операции записываются в журнал аудита:
- Время начала/конца выполнения
- Количество обработанных строк
- Параметры процедуры
- Ошибки

## 6. Правила разработки

### 6.1 Соглашения об именовании

| Объект | Префикс/Суффикс | Пример |
|--------|-----------------|--------|
| Таблицы | Без префикса | `orders_log` |
| Представления | `v_` | `v_OrdersSummary` |
| Хранимые процедуры | `sp_` или схема + `_` | `crs.load_orders_log` |
| Функции | `fn_` | `fn_GetSettingValue` |
| CLR-процедуры | `sp_clr_` | `rmq.sp_clr_PostRabbitMsg` |

### 6.2 Рекомендации по коду

1. **Все хранимые процедуры должны:**
   - Использовать `SET NOCOUNT ON`
   - Обрабатывать ошибки через TRY/CATCH
   - Логировать начало и завершение через `audit.sp_log_Start` / `sp_log_Finish`
   - Использовать параметры OUTPUT для возврата результатов

2. **Изоляция транзакций:**
   - Для высоконагруженных операций использовать `SET TRANSACTION ISOLATION LEVEL SNAPSHOT`
   - Использовать явные транзакции (BEGIN/COMMIT TRANSACTION)

3. **Безопасность:**
   - Не хранить пароли в открытом виде (использовать `VARBINARY` для RabbitMQ)
   - Использовать схемы для разделения прав
   - Избегать динамического SQL

4. **Производительность:**
   - Использовать MERGE для upsert операций
   - Обрабатывать данные пакетами (TOP N)
   - Индексировать ключевые поля

### 6.3 Структура нового файла

При создании новой таблицы:
```sql
CREATE TABLE [schema].[TableName] (
    [ID]            INT             IDENTITY (1, 1) NOT NULL,
    [Name]          NVARCHAR (256) NOT NULL,
    [CreatedAt]     DATETIME2 (4)  CONSTRAINT [DF_TableName_CreatedAt] DEFAULT (getdate()) NOT NULL,
    [IsActive]      BIT            CONSTRAINT [DF_TableName_IsActive] DEFAULT ((1)) NOT NULL,

    CONSTRAINT [PK_TableName] PRIMARY KEY CLUSTERED ([ID] ASC)
);

CREATE NONCLUSTERED INDEX [IX_TableName_Name] ON [schema].[TableName] ([Name]);
```

### 6.4 Структура хранимой процедуры

```sql
CREATE PROCEDURE [schema].[sp_ProcedureName]
    @Param1        INT             = NULL,
    @Param2        NVARCHAR (256) = NULL,
    @RowCount      INT             = NULL OUTPUT,
    @ErrorMessage  VARCHAR (4000)  = NULL OUTPUT,
    @Debug         BIT             = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET CONCAT_NULL_YIELDS_NULL ON;
    SET XACT_ABORT OFF;

    -- Логирование
    DECLARE @LogID INT, @ProcedureName VARCHAR(510), @ProcedureParams VARCHAR(MAX)
    SET @ProcedureName = OBJECT_SCHEMA_NAME(@@PROCID) + '.' + OBJECT_NAME(@@PROCID)

    BEGIN TRY
        BEGIN TRANSACTION
        -- Логирование начала
        -- Бизнес-логика
        -- Логирование завершения
        COMMIT TRANSACTION
        RETURN 0
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 AND @@TRANCOUNT > 0
            ROLLBACK TRANSACTION
        -- Обработка ошибки
        RETURN -1
    END CATCH
END
```

### 6.5 Версионирование

- Использовать System Versioned Tables (Temporal Tables) для критических таблиц
- Документировать изменения в коммитах
- Использовать RefactorLog для переименований

### 6.6 Тестирование

- Создавать тестовые данные в `DataGeneration` таблице
- Использовать `fn_GenerationRandomField` для генерации тестовых данных
- Документировать SQL-скрипты в `ScriptsFolder\Test001.sql`

---

**Дата создания:** 03.03.2026
**Автор:** AGENT Documentation
