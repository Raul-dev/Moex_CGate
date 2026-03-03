# Документация проекта MQ (Message Queue Service)

## 1. Описание проекта и его назначение

**MQ (Message Queue Service)** — это система обработки и транспортировки сообщений, разработанная для интеграции с торговой системой MOEX (Московская Биржа). Проект обеспечивает надёжную доставку сообщений между базой данных и очередями сообщений.

### Основные задачи

- **Извлечение сообщений из базы данных** (SQL Server, PostgreSQL) и отправка их в очереди RabbitMQ или Kafka
- **Потребление сообщений из очередей** и сохранение в базу данных
- **Обеспечение отказоустойчивости** с поддержкой различных режимов работы (буферизация, немедленная обработка)
- **Мониторинг состояния** через REST API
- **Конвертация данных** между различными форматами (включая метаданные映射)

---

## 2. Стек и технологии

### Платформа и языки

| Компонент | Технология |
|-----------|------------|
| Платформа | .NET 9.0 / .NET 10.0 |
| Язык | C# 12 (с поддержкой Nullable reference types) |
| Минимальная ОС | Windows / Linux (через Docker) |

### Основные библиотеки и фреймворки

| Назначение | Библиотека | Версия |
|------------|------------|--------|
| Очередь сообщений (RabbitMQ) | RabbitMQ.Client | 7.0.0 |
| Очередь сообщений (Kafka) | Confluent.Kafka | 2.6.1 |
| ORM | Entity Framework Core | 9.0.0 |
| SQL Server EF | Microsoft.EntityFrameworkCore.SqlServer | 9.0.0 |
| PostgreSQL EF | Npgsql.EntityFrameworkCore.PostgreSQL | 9.0.2 |
| Логирование | Serilog | 4.2.0 |
| JSON | Newtonsoft.Json | 13.0.3 |
| MongoDB | MongoDB.Driver | 3.1.0 |
| Консольный парсер | CommandLineParser | 2.9.1 |
| Docker | Microsoft.VisualStudio.Azure.Containers.Tools.Targets | 1.21.0 |
| Swagger | Swashbuckle | 7.2.0 |

### Базы данных

- **Microsoft SQL Server** — основная база данных
- **PostgreSQL** — поддерживается
- **MongoDB** — для отдельных сценариев
- **ClickHouse** — поддерживается через EFCore.BulkExtensions

---

## 3. Архитектура и структура проекта

Проект представляет собой **многослойную архитектуру** с разделением на отдельные сборки (проекты):

```
MQ.sln
├── MQ                      # Консольное приложение (CLI)
├── MQ.Service              # Windows-служба
├── MQ.WebService           # REST API (ASP.NET Core)
├── MQ.bll                  # Бизнес-логика
├── MQ.dal                  # Слой доступа к данным
└── MQ.Share                # Общие компоненты
```

### 3.1 MQ (Консольное приложение)

**Назначение:** CLI-интерфейс для ручного запуска операций с очередями.

**Точка входа:** `Program.cs`

**Команды:**
- `SendMsg` — отправка сообщений из БД в очередь
- `GetMsg` — получение сообщений из очереди в БД
- `ConfigMsg` — тестирование конфигурации

**Примеры использования:**
```bash
# Отправка в RabbitMQ
MQ.exe SendMsg -d CGate -t mssql -i 2

# Получение из Kafka
MQ.exe GetMsg -d CGate -t mssql -k $true -g whileget
```

### 3.2 MQ.Service (Windows Service)

**Назначение:** Фоновый сервис для непрерывной обработки сообщений.

**Основной файл:** `Worker.cs`

**Особенности:**
- Может работать как Windows-служба
- Поддерживает systemd на Linux

### 3.3 MQ.WebService (REST API)

**Назначение:** HTTP-интерфейс для управления и мониторинга.

**Контроллеры:**

| Контроллер | Маршрут | Описание |
|------------|---------|----------|
| `HomeController` | `/api/home` | Управление сервисом (Start/Stop/Reset/Status) |
| `MetaMapsController` | `/api/metamaps` | CRUD-операции для метаданных映射 |

**Эндпоинты HomeController:**

| Метод | Маршрут | Описание |
|-------|---------|----------|
| GET | `/api/home/Start` | Запуск сервиса |
| GET | `/api/home/Stop` | Остановка сервиса |
| GET | `/api/home/Reset` | Перезапуск с указанием режима |
| GET | `/api/home/Status` | Проверка статуса |
| GET | `/api/home/Config` | Получение текущей конфигурации |

**Эндпоинты MetaMapsController:**

| Метод | Маршрут | Описание |
|-------|---------|----------|
| GET | `/api/metamaps` | Получить все映射 |
| GET | `/api/metamaps/{id}` | Получить映射 по ID |
| PUT | `/api/metamaps/{id}` | Обновить映射 |
| DELETE | `/api/metamaps/{id}` | Удалить映射 |

### 3.4 MQ.bll (Бизнес-логика)

**Основные компоненты:**

| Файл/Папка | Назначение |
|------------|------------|
| `IQueueChannel.cs` | Интерфейс абстракции очереди |
| `Kafka/` | Реализация Kafka-канала |
| `RabbitMQ/` | Реализация RabbitMQ-канала |
| `SendAllUnknownMsg.cs` | Отправка сообщений в очередь |
| `ReceiveAllMessages.cs` | Получение сообщений из очереди |
| `ThreadManagerAsync.cs` | Управление асинхронными потоками |
| `Common/` | Настройки и конфигурации |

**Ключевые классы:**

```csharp
// Интерфейс канала очереди
public interface IQueueChannel: IDisposable
{
    Task InitSetup(MQSession? mqSession = null, bool isSend = true, bool isSubscription = false);
    Task CloseAsync();
    Task<long> MessageCountAsync();
    Task<BasicGetResult?> GetMessageAsync();
    Task PublishMessageAsync(string msgKey, string msg);
    Task AcknowledgeMessageAsync(ulong offsetId, bool multiple = false);
    Task RejectMessageAsync(ulong offsetId, bool requeue = true);
    void Flush();
}
```

### 3.5 MQ.dal (Слой доступа к данным)

**Модели (сущности БД):**

| Модель | Таблица | Описание |
|--------|---------|----------|
| `MsgQueue` | `msgqueue` | Очередь сообщений |
| `OrdersLogBuffer` | `orders_log_buffer` | Буфер логов заказов |
| `Metamap` | `metamap` | Метаданные映射 |
| `Metadata` | `metadata` | Метаданные |
| `SessionId` | `session_id` | Идентификаторы сессий |

**DbContext:**

```csharp
public partial class MetastorageContext : DbContext
{
    public virtual DbSet<Metadata> Metadata { get; set; }
    public virtual DbSet<Metamap> Metamaps { get; set; }
    public virtual DbSet<MsgQueue> MsgQueues { get; set; }
    public virtual DbSet<OrdersLogBuffer> OrdersLogBuffers { get; set; }
}
```

**Утилиты:**

| Файл | Назначение |
|------|------------|
| `DBHelper.cs` | Помощник для работы с БД |
| `DBConnection.cs` | Управление подключениями |
| `MongoHelper.cs` | Работа с MongoDB |
| `SqlServerTypeHelper.cs` | Определение типа SQL-сервера |

### 3.6 MQ.Share (Общие компоненты)

**Содержит:**
- `CustomPropertyEnricher.cs` — обогащение логов
- `ThreadManager.cs` — управление потоками

---

## 4. CI/CD

### Docker Compose

Проект использует `docker-compose.yml` для развертывания:

```yaml
services:
  mq.webservice:
    image: ${DOCKER_REGISTRY-}mqwebservice
    build:
      context: .
      dockerfile: MQ.WebService/Dockerfile
```

### Конфигурация запуска

**launchSettings.json** содержит профили запуска для различных сценариев:

- Локальная отладка
- Docker-развертывание
- Запуск как Windows-сервис

### Сборка проекта

```bash
# Восстановление зависимостей
dotnet restore MQ.sln

# Сборка
dotnet build MQ.sln
```

### Запуск в Docker

```bash
# Сборка и запуск
docker-compose build
docker-compose up

# Остановка
docker-compose down
```

---

## 5. Миграции данных

Проект использует **Entity Framework Core Code-First миграции**.

### Расположение миграций

```
MQ.dal/
└── Data/
    └── PsqlMigrations/
        ├── 20230510133909_InitialCreate.cs
        ├── 20230510133909_InitialCreate.Designer.cs
        └── MetastorageContextModelSnapshot.cs
```

### Управление миграциями

Миграции выполняются через EF Core Tools:

```bash
# Создание новой миграции
dotnet ef migrations add InitialCreate --project MQ.dal

# Применение миграций
dotnet ef database update --project MQ.dal

# Удаление последней миграции
dotnet ef migrations remove --project MQ.dal
```

### Поддерживаемые СУБД

- **SQL Server** — основная СУБД
- **PostgreSQL** — через Npgsql
- **SQLite** — для тестирования

Конфигурация СУБД задаётся в `appsettings.json` через `DataBaseSettings.ServerType`.

---

## 6. Правила разработки

### 6.1 Соглашения по кодированию

1. **Именование**
   - Использовать PascalCase для публичных членов
   - Использовать camelCase для локальных переменных и параметров
   - Интерфейсы именовать с префиксом `I` (например, `IQueueChannel`)

2. **Nullable Reference Types**
   - Проект использует `<Nullable>enable</Nullable>`
   - Явно указывать `?` для nullable типов
   - Использовать `required` keyword для обязательных свойств в record/class

3. **Асинхронное программирование**
   - Все операции ввода-вывода должны быть асинхронными
   - Использовать `async/await` вместо `.Result` или `.Wait()`
   - Использовать `CancellationToken` для отмены операций

4. **Логирование**
   - Использовать Serilog для структурированного логирования
   - Использовать уровни: Debug, Information, Warning, Error
   - Обогащать логи контекстной информацией (CorrelationId, ThreadId)

### 6.2 Архитектурные принципы

1. **Разделение ответственности (SOLID)**
   - Каждый проект отвечает за свой слой
   - Зависимости только от нижних слоёв

2. **Dependency Injection**
   - Использовать встроенный DI в ASP.NET Core
   - Конфигурировать сервисы в `Program.cs`

3. **Обработка ошибок**
   - Все исключения должны логироваться
   - Использовать try-catch с осмысленными сообщениями об ошибках

### 6.3 Работа с очередями

1. **Поддержка двух провайдеров**
   - Код должен поддерживать RabbitMQ и Kafka через интерфейс `IQueueChannel`
   - Выбор провайдера через конфигурацию

2. **Обработка сообщений**
   - Использовать пакетную обработку для высокой производительности
   - Реализовать retry-логику при сбоях
   - Подтверждать сообщения только после успешной обработки

3. ** производительность**
   - Пакетное сохранение в БД: до ~6900 msg/s
   - Одиночное сохранение: ~2787 msg/s

### 6.4 Тестирование

1. **Юнит-тесты**
   - Тестировать бизнес-логику изолированно
   - Использовать mock-объекты для внешних зависимостей

2. **Интеграционные тесты**
   - Тестировать взаимодействие с БД
   - Использовать Docker для тестовых СУБД

### 6.5 Конфигурация

1. **appsettings.json**
   - Хранить настройки подключения к БД
   - Настройки очередей (RabbitMQ, Kafka)
   - Уровень логирования

2. **User Secrets**
   - Использовать для конфиденциальных данных в разработке
   - Не коммитить пароли и ключи в репозиторий

### 6.6 Рекомендации по безопасности

1. Не хранить пароли в открытом виде в конфигурации
2. Использовать secrets management для production
3. При работе с Kafka использовать SSL/TLS
4. Ограничивать права доступа к БД минимально необходимыми

---

## 7. Примеры использования API

### Запуск сервиса

```bash
curl http://localhost:5000/api/home/Start
```

### Проверка статуса

```bash
curl http://localhost:5000/api/home/Status
```

### Получение конфигурации

```bash
curl http://localhost:5000/api/home/Config
```

### Получение списка метакарт

```bash
curl http://localhost:5000/api/metamaps
```

---

## 8. Устранение неполадок

### Проверка логов

Логи сохраняются в директорию `logs/` в формате JSON.

### Мониторинг RabbitMQ

- UI: http://localhost:15672
- Логин/пароль: guest/guest

### Мониторинг Kafka

- UI: http://localhost:9000 (Kafdrop)
- UI: http://localhost:8082 (Control Center)

### Мониторинг метрик БД

```sql
SELECT
  [Message per second] = COUNT(*) / DATEDIFF(ss, MIN(dt_create), MAX(dt_create)),
  [Message Count]      = COUNT(*),
  [Message Start]      = MIN(dt_create),
  [Message Finish]     = MAX(dt_create),
  [Message Avg Length] = AVG(LEN(msg))
FROM [CGate].[crs].[orders_log_buffer]
```
