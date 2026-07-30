# init.sql — bootstrap SQL для Docker

## Рекомендуемый способ развёртывания схемы

Используйте **dacpac** (актуальная схема `mq` / PascalCase):

```powershell
cd src
docker compose -f docker-compose.sqldacpac.yml up -d
```

или `dbprojects/dbmssql/CGate/ScriptsFolder/dbdeploy.ps1`.

## Файл init.sql

`init.sql` — legacy-скрипт для `docker-compose.sqlscript.yml`.  
После рефакторинга схемы (**mq**, **PascalCase**) его нужно **перегенерировать** из dacpac:

```powershell
SqlPackage.exe /Action:Script /SourceFile:CGate.dacpac /OutputPath:init.sql
```

Либо опубликовать dacpac напрямую — предпочтительно для dev/test.

## Именование (текущий стандарт)

| Было | Стало |
|------|-------|
| `dbo.metamap`, `dbo.msgqueue` | `mq.MetaMap`, `mq.MessageQueue` |
| `crs.orders_log_buffer` | `crs.OrdersLogBuffer` |
| `crs.load_orders_log` | `crs.load_OrdersLog` |
| столбец `msg` | `MessageBody` |
