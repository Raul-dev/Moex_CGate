# Документация по проекту Moex_CGate

## 1. Описание проекта и его назначение

**Moex_CGate** — это интеграционная система для обработки финансовых сообщений Московской биржи (MOEX). Проект обеспечивает:

- Приём сообщений из очередей сообщений (RabbitMQ, Kafka)
- Сохранение данных в базу данных (MS SQL Server, PostgreSQL)
- Предоставление REST API для работы с метаданными
- Логирование и аудит операций
- Поддержка CLR-процедур SQL Server для интеграции с RabbitMQ

Проект является backend-системой, работающей в Docker-контейнерах, и предназначен для высоконагруженной обработки финансовых транзакций в реальном времени.

---

## 2. Стек и технологии

### Основные языки и фреймворки

| Компонент | Технология | Версия |
|-----------|------------|--------|
| Backend | .NET | 10.0 |
| Web API | ASP.NET Core | 10.0 |
| ORM | Entity Framework Core | 9.0 |
| Логирование | Serilog | 4.2.0 |

### Базы данных

| Тип | Назначение |
|-----|------------|
| MS SQL Server | Основное хранилище (таблицы msgqueue, metamap, session, metadata) |
| PostgreSQL | Альтернативная БД (поддерживаются миграции) |
| MongoDB | Опционально для расширенного логирования |

### Очереди сообщений

| Технология | Назначение |
|------------|------------|
| RabbitMQ | Основная очередь сообщений |
| Kafka | Альтернативная очередь (для высокой пропускной способности) |

### Контейнеризация и инфраструктура

| Инструмент | Назначение |
|------------|------------|
| Docker | Контейнеризация сервисов |
| Docker Compose | Оркестрация контейнеров |
| Zipkin | Трейсинг распределённых запросов |
| ELK (Elasticsearch, Logstash, Kibana) | Централизованное логирование |
| massTransit.RabbitMQ | Интеграция с RabbitMQ |

### Средства разработки

- Visual Studio / VS Code
- SQL Server Data Tools (SSDT)
- dacpac для деплоймента БД

---

## 3. Архитектура и структура проекта

### Общая структура директорий

```
src/
├── services/
│   ├── mq/                    # Основное приложение
│   │   ├── MQ/                # Консольное приложение (MQ.exe)
│   │   ├── MQ.bll/            # Бизнес-логика
│   │   ├── MQ.dal/            # Доступ к данным (EF Core)
│   │   ├── MQ.Service/        # Windows Service / BackgroundService
│   │   ├── MQ.WebService/     # REST API (ASP.NET Core)
│   │   └── MQ.Share           # Общие компоненты
│   └── repl/                  # REPL-инструмент для тестирования
├── CLR/
│   ├── RabbitMQSqlClr4/       # CLR-сборка для SQL Server 2014+
│   ├── RabbitMQSqlClr48/      # CLR-сборка для SQL Server 2008-2012
│   └── RabbitMQTestApp/       # Тестовое приложение
├── dbprojects/
│   └── dbmssql/               # SSDT-проекты баз данных (CGate, Log)
├── images/                    # Docker-образы (RabbitMQ, MSSQL, Filebeat, Logstash)
└── benchmarking-performance/  # Тесты производительности
```

---

### 3.1 Контроллеры (MQ.WebService)

Находятся в: `services/mq/MQ.WebService/Controllers/`

| Контроллер | Описание |
|------------|----------|
| `HomeController.cs` | Главная страница API |
| `MetaMapsController.cs` | REST API для работы с метаданными (metamap) |

**Примеры endpoints:**

```
GET    /api/MetaMaps           # Получить все метамаппинги
GET    /api/MetaMaps/{id}      # Получить метамаппинг по ID
PUT    /api/MetaMaps/{id}      # Обновить метамаппинг
DELETE /api/MetaMaps/{id}      # Удалить метамаппинг
```

---

### 3.2 Сервисы (MQ.bll)

Находятся в: `services/mq/MQ.bll/`

#### Основные компоненты:

| Файл/Директория | Назначение |
|-----------------|------------|
| `IQueueChannel.cs` | Интерфейс для работы с очередями |
| `RabbitMQ/` | Реализация RabbitMQ-канала |
| `Kafka/` | Реализация Kafka-канала |
| `MQSession.cs` | Управление сессиями обработки |
| `ReceiveAllMessages.cs` | Приём сообщений из очереди |
| `SendAllUnknownMsg.cs` | Отправка неизвестных сообщений |
| `ThreadManagerAsync.cs` | Менеджер асинхронных потоков |
| `Common/BllOption.cs` | Конфигурация бизнес-логики |
| `Common/Extensions/` | Расширения для логирования |

---

### 3.3 Модели (MQ.dal)

Находятся в: `services/mq/MQ.dal/Models/`

| Модель | Описание | Таблица БД |
|--------|----------|------------|
| `Metamap.cs` | Метамаппинг сообщений | `metamap` |
| `Msgqueue.cs` | Очередь сообщений | `msgqueue` |
| `Metadata.cs` | Метаданные | `metadata` |
| `SessionId.cs` | Идентификатор сессии | `session` |
| `OrdersLogBuffer.cs` | Буфер логов ордеров | `orders_log_buffer` |
| `MessageBuffer.cs` | Буфер сообщений | — |

#### Контекст базы данных

```csharp
// MetastorageContext.cs
public partial class MetastorageContext : DbContext
{
    public virtual DbSet<Metadata> Metadata { get; set; }
    public virtual DbSet<Metamap> Metamaps { get; set; }
    public virtual DbSet<MsgQueue> MsgQueues { get; set; }
    public virtual DbSet<OrdersLogBuffer> OrdersLogBuffers { get; set; }
}
```

---

### 3.4 DAL (Data Access Layer)

Находятся в: `services/mq/MQ.dal/`

| Файл | Назначение |
|------|------------|
| `DBConnection.cs` | Управление подключениями к БД |
| `DBHelper.cs` | Вспомогательные методы для работы с БД |
| `MongoHelper.cs` | Интеграция с MongoDB |
| `SqlServerTypeHelper.cs` | Определение типа SQL Server |
| `Data/PsqlMigrations/` | Миграции PostgreSQL |

---

### 3.5 API

REST API построен на ASP.NET Core с использованием:

- **Swagger/Swashbuckle** — документация API (доступен в Development-режиме)
- **Serilog** — структурированное логирование
- **Entity Framework Core** — работа с данными

**Конфигурация:** `appsettings.json`

---

## 4. CI/CD

### Docker Compose

Проект использует несколько compose-файлов для разных сценариев:

| Файл | Назначение |
|------|------------|
| `docker-compose.yml` | Основной стек (RabbitMQ + WebService + Zipkin) |
| `docker-compose.kafka.yml` | Стек с Kafka вместо RabbitMQ |
| `docker-compose.elk.yml` | Стек с ELK для логирования |
| `docker-compose.sqldacpac.yml` | Деплой БД через dacpac |
| `docker-compose.rabbit.yml` | Только RabbitMQ |
| `docker-compose.sqlscript.yml` | Выполнение SQL-скриптов |

### Сборка и запуск

```powershell
# Основной запуск (MSSQL в Docker)
.\start.ps1 -IsDockerSql $true

# Запуск без Docker SQL
.\start.ps1
```

### Dockerfile

Основной образ: `MQ.WebService/Dockerfile`

- Multi-stage build
- Base: `mcr.microsoft.com/dotnet/aspnet:10.0`
- Build: `mcr.microsoft.com/dotnet/sdk:10.0`

---

## 5. Миграции данных

### MS SQL Server

Используется **dacpac**-подход:
- Проект: `dbprojects/dbmssql/CGate/ScriptsFolder/CGate.Build.csproj`
- Деплой: через `sqlpackage` в Docker

### PostgreSQL

Используются **EF Core Migrations**:
- Расположение: `services/mq/MQ.dal/Data/PsqlMigrations/`
- Пример: `20230510133909_InitialCreate.cs`

### CLR-процедуры SQL Server

Проект включает CLR- сборки для интеграции RabbitMQ напрямую в SQL Server:

| Проект | Версия SQL Server |
|--------|-------------------|
| `RabbitMQSqlClr4` | SQL Server 2014+ |
| `RabbitMQSqlClr48` | SQL Server 2008-2012 |

**Хранимые процедуры:**
- `sp_clr_PostRabbitMsg` — отправка сообщений в RabbitMQ
- `sp_clr_ReloadRabbitEndpoints` — перезагрузка конфигурации endpoint'ов
- `sp_clr_InitialiseRabbitMq` — инициализация RabbitMQ

---

## 6. Правила разработки

### 6.1 Соглашения об именовании

| Тип | Правило | Пример |
|-----|---------|--------|
| Классы/Модели | PascalCase | `Metamap`, `MsgQueue` |
| Методы | PascalCase | `GetMetamaps()`, `ProcessMessage()` |
| Приватные поля | _camelCase | `_context`, `_connection` |
| Контроллеры | {Name}Controller | `MetaMapsController` |
| Таблицы БД | snake_case | `msgqueue`, `metamap` |
| API endpoints | kebab-case в URL | `/api/meta-maps` |

### 6.2 Структура проекта .NET

```
MQ.bll/
├── Common/           # Общие настройки, расширения
├── Extensions/       # Методы-расширения
├── RabbitMQ/         # Логика RabbitMQ
├── Kafka/            # Логика Kafka
└── *.cs              # Основные классы

MQ.dal/
├── Models/           # Сущности EF Core
├── Data/             # Миграции
└── *.cs              # DAL-компоненты

MQ.WebService/
├── Controllers/      # API-контроллеры
├── Extensions/       # Расширения для Web
└── Program.cs        # Точка входа
```

### 6.3 Работа с базой данных

1. **Для MS SQL Server** — используйте SSDT-проект в `dbprojects/dbmssql/`
2. **Для PostgreSQL** — создавайте миграции через:
   ```bash
   dotnet ef migrations add InitialCreate --project MQ.dal --startup-project MQ.WebService
   ```

### 6.4 Логирование

Используйте **Serilog** с структурированным логированием:

```csharp
Log.Information("RabbitMQ Host:{Host}, DefaultQueue:{Queue}", host, queue);
Log.Error(ex, "Error processing message");
```

### 6.5 Конфигурация

- Храните конфигурацию в `appsettings.json` и `appsettings.Development.json`
- Секретные данные — в переменных окружения или `.env`
- Избегайте hardcoded значений

### 6.6 Docker-разработка

```dockerfile
# Не добавляйте secrets в образы
# Используйте multi-stage build
# Минимизируйте размер образа
```

### 6.7 Тестирование

- Используйте unit-тесты для бизнес-логики
- Интеграционные тесты — через отдельный docker-compose
- Нагрузочное тестирование — в `benchmarking-performance/`

### 6.8 Работа с очередями

- Используйте интерфейс `IQueueChannel` для абстракции
- Поддерживайте как RabbitMQ, так и Kafka
- Обрабатывайте ошибки и повторные попытки

---

## 7. Ключевые конфигурационные файлы

| Файл | Назначение |
|------|------------|
| `.env` | Переменные окружения для Docker |
| `appsettings.json` | Конфигурация приложения |
| `docker-compose.yml` | Оркестрация сервисов |
| `MQ.sln` | Решение .NET |

---

## 8. Зависимости между проектами

```
MQ.WebService (ASP.NET Core)
    └── MQ.bll (Классная библиотека)
            └── MQ.dal (Классная библиотека)
```

---

*Документация сгенерирована автоматически на основе анализа структуры проекта.*
