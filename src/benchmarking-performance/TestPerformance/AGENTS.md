# Документация проекта TestPerformance

## 1. Назначение проекта

**TestPerformance** — это инструмент для бенчмаркинга (измерения производительности) различных методов логирования событий в системе CGate (Московская биржа).

Проект решает следующие задачи:

- Сравнение производительности трёх методов записи логов:
  - **LocalTable** — запись в локальную таблицу SQL Server
  - **LinkedServerTable** — запись в таблицу на связанном сервере
  - **RabbitMQPost** — отправка сообщений через RabbitMQ
- Определение наиболее эффективного способа логирования для высоконагруженных систем
- Получение метрик производительности (время выполнения, потребление памяти)

---

## 2. Стек и технологии

| Компонент | Технология | Версия |
|-----------|------------|--------|
| Язык программирования | C# (.NET) | 9.0 |
| Фреймворк для бенчмарков | BenchmarkDotNet | 0.15.8 |
| ORM | Entity Framework Core | 9.0.11 |
| Провайдер БД | Entity Framework Core SqlServer | 9.0.11 |
| Конфигурация | Microsoft.Extensions.Configuration | 9.0.11 |
| Целевая ОС | Linux (Docker) | — |
| СУБД | SQL Server | — |
| Брокер сообщений | RabbitMQ | — |

---

## 3. Архитектура и структура проекта

### Структура файлов

```
TestPerformance/
├── .dockerignore                    # Исключения для Docker
├── .editorconfig                    # Настройки форматирования кода
├── start.ps1                        # Скрипт запуска
├── TestPerformance.sln              # Файл решения
└── TestPerformance/
    ├── .editorconfig                # Локальные настройки форматирования
    ├── appsettings.json             # Конфигурация приложения
    ├── AuditParserBenchmarks.cs     # Класс бенчмарков
    ├── BenchmarkSettings.cs         # Модель настроек бенчмарка
    ├── DBContext.cs                 # Контекст Entity Framework
    ├── DBHelper.cs                  # Утилита для работы с БД
    ├── Dockerfile                   # Конфигурация Docker
    ├── Program.cs                   # Точка входа
    ├── Properties/
    │   └── launchSettings.json      # Настройки запуска
    └── TestPerformance.csproj       # Файл проекта
```

### Основные компоненты

#### 3.1 AuditParserBenchmarks.cs

**Назначение:** Класс бенчмарков с атрибутами BenchmarkDotNet.

**Ключевые методы:**

- `Add1000LogMessage(LogType logType)` — добавляет 1000 тестовых сообщений указанным способом
- `LogLocalTable()` — бенчмарк записи в локальную таблицу
- `LogLinkedServerTable()` — бенчмарк записи в таблицу связанного сервера
- `LogRabbitMQPost()` — бенчмарк отправки в RabbitMQ (базовый)

**Особенности:**
- Использует атрибут `[MemoryDiagnoser]` для анализа потребления памяти
- Базовый метод `LogRabbitMQPost` помечен как `[Benchmark(Baseline = true)]`

#### 3.2 BenchmarkSettings.cs

**Назначение:** Модель конфигурации бенчмарка.

**Свойства:**

- `InputPath` — путь к входным данным
- `IterationCount` — количество итераций измерения
- `InvocationCount` — количество вызовов метода за итерацию
- `ConnectionString` — строка подключения к БД

#### 3.3 DBHelper.cs

**Назначение:** Утилита для работы с базой данных через Entity Framework Core.

**Основные компоненты:**

- `SqlServerType` (enum) — типы SQL-серверов: mssql, psql, osql, sqlite, clickhouse, xdto, unknown
- `LogType` (enum) — типы логирования:
  - `LocalTable = 1`
  - `LinkedServerTable = 2`
  - `RabbitMQPost = 3`

**Конструкторы:**
- `DBHelper(string strConnection)` — подключение через строку подключения
- `DBHelper(string server, string databasename, int port, SqlServerType type, string user, string pwd)` — подключение с указанием параметров

**Методы:**

- `SetLogType(LogType logType)` — устанавливает тип логирования в настройках БД и инициализирует RabbitMQ
- `AddLogMessage(...)` — добавляет тестовое сообщение лога через хранимую процедуру `[audit].[sp_LogText_Add]`

#### 3.4 DBContext.cs

**Назначение:** Контекст Entity Framework Core для доступа к БД.

**Особенности:**
- Наследует `DbContext`
- Использует провайдер `UseSqlServer`
- Класс定义为 `partial` для разделения на несколько файлов

#### 3.5 Program.cs

**Назначение:** Точка входа приложения, настройка и запуск бенчмарков.

**Особенности:**

- Загружает конфигурацию из `appsettings.json`
- Настраивает BenchmarkDotNet:
  - `LaunchCount = 1` — один запуск процесса
  - `WarmupCount = 2` — две итерации разогрева
  - `UnrollFactor = 10` — фактор развёртывания
  - `IterationCount` и `InvocationCount` из конфига
- В режиме `DEBUG` использует `DebugInProcessConfig`
- В режиме `RELEASE` — экспортирует HTML-отчёт

#### 3.6 Конфигурация (appsettings.json)

```json
{
  "BenchmarkSettings": {
    "InputPath": "data/input.txt",
    "IterationCount": "200",
    "InvocationCount": "20",
    "ConnectionString": "Server=.;Database=Test;Trusted_Connection=True;"
  }
}
```

---

## 4. CI/CD

### Docker

Проект полностью контейнеризирован:

**Многостадийная сборка:**

1. **base** — runtime .NET 9.0 для запуска
2. **build** — SDK для компиляции
3. **publish** — публикация приложения
4. **final** — финальный образ

**Особенности:**

- Использует официальные образы Microsoft (`mcr.microsoft.com/dotnet/`)
- Нацелен на Linux
- Копирует только опубликованные артефакты

### Запуск

```powershell
# Запуск через PowerShell
.\start.ps1
```

Скрипт запускает скомпилированный exe-файл.

---

## 5. Миграции данных

**Миграции не используются.**

Проект предполагает, что база данных и хранимые процедуры уже существуют:

- Таблица `[audit].[Setting]` — настройки логирования
- Хранимая процедура `[audit].[sp_LogText_Add]` — добавление логов
- Хранимая процедура `[rmq].[sp_clr_InitialiseRabbitMq]` — инициализация RabbitMQ

**Требования к БД:**

- SQL Server с настроенной базой данных CGate
- Наличие схемы `audit` и `rmq`
- Доступ к связанному серверу (для `LinkedServerTable`)

---

## 6. Правила разработки

### 6.1 Стиль кода

Проект использует `.editorconfig` с следующими ключевыми настройками:

- **Отступы:** 4 пробела, CRLF
- **Форматирование:** фигурные скобки `always_for_clarity`
- **Именование:**
  - Интерфейсы — с префиксом `I` (PascalCase)
  - Типы — PascalCase
  - Публичные члены — PascalCase
- **using:** размещаются вне namespace
- **Типы:** предпочтение `var` когда тип очевиден
- **Модификаторы доступа:** явное указание для не-интерфейсных членов
- **Свойства:** предпочтение auto-properties с throwing behavior при null

### 6.2 Рекомендации

1. **Конфиденциальность**
   - Не хардкодить пароли и строки подключения в коде
   - Использовать переменные окружения или secrets

2. **Бенчмарки**
   - Не добавлять `Console.WriteLine` внутри benchmark-методов
   - Использовать атрибуты BenchmarkDotNet
   - Помечать один метод как `Baseline` для корректного сравнения

3. **EF Core**
   - Использовать параметризованные запросы для защиты от SQL-инъекций
   - Избегать `ExecuteSqlRaw` с динамическим SQL

4. **Docker**
   - Не включать в образ чувствительные файлы (использовать `.dockerignore`)
   - Использовать multi-stage build для минимизации размера образа

5. **Тестирование**
   - Проверять работу в обоих режимах (Debug/Release)
   - Учитывать разницу между InProcess и OutOfProcess бенчмарками

### 6.3 Структура коммитов

Рекомендуется использовать conventional commits:

```
feat: добавлен новый бенчмарк
fix: исправлена ошибка в DBHelper
docs: обновлена документация
chore: обновлена зависимость
```

---

## 7. Запуск проекта

### Локально

```bash
dotnet build
dotnet run --configuration Release
```

### Docker

```bash
docker build -t testperformance .
docker run testperformance
```

### Конфигурация

Настройки бенчмарка редактируются в `appsettings.json`:

- `IterationCount` — количество прогонов
- `InvocationCount` — количество вызовов метода за прогон
- `ConnectionString` — подключение к БД

---

## 8. Зависимости

| Пакет | Версия | Назначение |
|-------|--------|------------|
| BenchmarkDotNet | 0.15.8 | Фреймворк бенчмарков |
| Microsoft.EntityFrameworkCore | 9.0.11 | ORM |
| Microsoft.EntityFrameworkCore.SqlServer | 9.0.11 | Провайдер SQL Server |
| Microsoft.Extensions.Configuration | 9.0.11 | Конфигурация |
| Microsoft.Extensions.Configuration.Json | 9.0.11 | JSON-конфигурация |
| Microsoft.VisualStudio.Azure.Containers.Tools.Targets | 1.22.1 | Docker-инструменты |
