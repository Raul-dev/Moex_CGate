# mq-copy

Копирование сообщений из очереди RabbitMQ в buffer-таблицу MS SQL Server.

Аналог команды `MQ.exe CopyMsg` на Python.

## Установка

```powershell
cd src/Tools/mq-copy
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -e .
```

Требуется **ODBC-драйвер SQL Server** (18, 17, 13 или встроенный `SQL Server`).
Если в конфиге `database.driver` пустой — утилита выберет первый установленный автоматически.

Проверить установленные драйверы:

```powershell
python -c "import pyodbc; print('\n'.join(pyodbc.drivers()))"
```

Если список пустой или нет SQL Server — установите
[Microsoft ODBC Driver 18 for SQL Server](https://learn.microsoft.com/sql/connect/odbc/download-odbc-driver-for-sql-server).

## Где настройки RabbitMQ и SQL

### 1. Файл `copy-config.json` (рекомендуется)

```powershell
copy copy-config.example.json copy-config.json
```

`copy-config.json` с тестовыми паролями Docker-среды **лежит в репозитории** — копировать шаблон нужно только если файла нет.

Структура:

```json
{
  "rabbitmq": {
    "host": "localhost",
    "port": 5672,
    "virtual_host": "/",
    "username": "admin",
    "password": "admin",
    "queue": "Cgate_FORTS_TRADE_REPL"
  },
  "database": {
    "server": "localhost,1433",
    "database": "CGate",
    "user": "CGateUser",
    "password": "MyPassword321",
    "driver": "ODBC Driver 18 for SQL Server"
  },
  "copy": {
    "target_table": "dbo.Upload",
    "truncate_before_copy": false
  }
}
```

| Секция | Поле | Описание |
|--------|------|----------|
| `rabbitmq` | `host` | Хост RabbitMQ |
| `rabbitmq` | `port` | Порт (обычно 5672) |
| `rabbitmq` | `virtual_host` | VHost (обычно `/`) |
| `rabbitmq` | `username` | Логин |
| `rabbitmq` | `password` | Пароль |
| `rabbitmq` | `queue` | Имя очереди |
| `database` | `server` | SQL Server (`host,port`; локально часто `localhost,1433`, Docker — `localhost,1434`) |
| `database` | `database` | Имя БД |
| `database` | `user` | SQL-пользователь |
| `database` | `password` | SQL-пароль |
| `database` | `driver` | ODBC-драйвер |
| `copy` | `target_table` | Целевая таблица |
| `copy` | `truncate_before_copy` | Очистить SQL-таблицу перед копированием |
| `copy` | `clear_queue` | Очищать очередь Rabbit: удалять сообщение после записи (`true`/`false`) |

### 2. Параметры командной строки (перекрывают JSON)

**Copy:**

| Параметр | Описание |
|----------|----------|
| `-g` / `--clear-queue` | Очищать очередь Rabbit после записи (`true`/`false`, по умолчанию `true`) |
| `-r` / `--ack` | Алиас для `-g` |
| `-f` / `--truncate` | `TRUNCATE` SQL-таблицы перед копированием |
| `-q` / `--target-table` | Целевая таблица |
| `-n` / `--max-messages` | Лимит сообщений |

**RabbitMQ:**

| Параметр | Описание |
|----------|----------|
| `--rabbit-host` | Хост |
| `--rabbit-port` | Порт |
| `--rabbit-vhost` | Virtual host |
| `--rabbit-user` | Логин |
| `--rabbit-password` | **Пароль RabbitMQ** |
| `--rabbit-queue` | Очередь |

**MS SQL:**

| Параметр | Описание |
|----------|----------|
| `-s` / `--db-server` | Сервер |
| `-d` / `--db-name` | База |
| `-u` / `--db-user` | Пользователь |
| `-w` / `--db-password` | Пароль SQL |
| `--db-driver` | ODBC driver |

Если `copy-config.json` отсутствует, можно передать всё через CLI.

## Примеры запуска

```powershell
# Конфиг + переопределить пароль Rabbit из командной строки
python -m mq_copy -c copy-config.json --rabbit-password admin -q dbo.Upload

# Без файла конфига — все параметры в CLI
python -m mq_copy `
  --rabbit-host localhost `
  --rabbit-port 5672 `
  --rabbit-user admin `
  --rabbit-password admin `
  --rabbit-queue Cgate_FORTS_TRADE_REPL `
  -s "localhost,1433" `
  -d CGate `
  -u CGateUser `
  -w MyPassword321 `
  -q dbo.Upload

# Копировать без удаления из RabbitMQ
python -m mq_copy -c copy-config.json --rabbit-password admin -q dbo.Upload -g false

# Очистить buffer-таблицу перед загрузкой
python -m mq_copy -c copy-config.json --rabbit-password admin -q dbo.Upload -f true
```

## C# аналог

```powershell
.\MQ.exe CopyMsg -t mssql -s "localhost,1433" -d CGate -u CGateUser -w MyPassword321 -q dbo.Upload
```

## SQL Server не запущен / connection refused

Ошибка `10061` или `Login timeout` — на указанном порту нет SQL Server.

**Docker (как в проекте):** порт **1434** (`docker-compose.sqldacpac.yml`).

**Локальный SQL Server (Windows):** обычно порт **1433**.

```powershell
# проверка — какой порт отвечает:
sqlcmd -S localhost,1433 -U CGateUser -P MyPassword321 -d CGate -Q "SELECT 1" -C
sqlcmd -S localhost,1434 -U CGateUser -P MyPassword321 -d CGate -Q "SELECT 1" -C
```

Запуск Docker SQL:

```powershell
cd src
.\start.ps1 -IsDockerSql $true
docker inspect -f "{{.State.Health.Status}}" cgatemssql
```

Если SQL на другом порту — измените `database.server` в `copy-config.json` (формат `host,port`) или `-s "host,port"`.

Настройки RabbitMQ в C# берутся из `services\mq\MQ\appsettings.json` → секция `RabbitMQSettings`.
