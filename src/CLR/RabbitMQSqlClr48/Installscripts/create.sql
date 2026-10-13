-- 1. Переключаемся на вашу базу данных (замените на ваше реальное имя БД)
USE taxlot_portfolio;
GO

-- 2. Включаем поддержку CLR на уровне всего экземпляра SQL Server
EXEC sp_configure 'clr enabled', 1;
RECONFIGURE;
GO

-- 3. Разрешаем базе данных выполнять сборки с повышенными правами безопасности
-- Это необходимо, так как наш код использует оптимизированный SqlBulkCopy внутри
ALTER DATABASE taxlot_portfolio SET TRUSTWORTHY ON;
GO

-- 4. Если процедура и сборка уже существовали, удаляем их для чистого обновления
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'ProcessPositionClr' AND schema_id = SCHEMA_ID('pnl'))
    DROP PROCEDURE pnl.ProcessPositionClr;
GO

IF EXISTS (SELECT * FROM sys.assemblies WHERE name = 'TaxLotManagerSqlClr')
    DROP ASSEMBLY ;
GO

  CREATE ASSEMBLY [System.Threading]
  AUTHORIZATION rmq
  FROM 'F:\Work\GitLab\gitlab26.neva.loc\shop\publicdwh_nodejs\src\dbprojects\TaxLotDB\services\CLR_Rabbit\RabbitMQSqlClr48\bin\Debug\RabbitMQSqlClr48.dll'
  WITH PERMISSION_SET = UNSAFE;  	

  CREATE ASSEMBLY [RabbitMQ.Client]
  AUTHORIZATION rmq
  FROM 'F:\Work\GitLab\gitlab26.neva.loc\shop\publicdwh_nodejs\src\dbprojects\TaxLotDB\services\CLR_Rabbit\RabbitMQSqlClr48\bin\Debug\RabbitMQ.Client.dll'
  WITH PERMISSION_SET = UNSAFE;  	

  CREATE ASSEMBLY [RabbitMQSqlClr48]
  AUTHORIZATION rmq
  FROM 'F:\Work\GitLab\gitlab26.neva.loc\shop\publicdwh_nodejs\src\dbprojects\TaxLotDB\services\CLR_Rabbit\RabbitMQSqlClr48\bin\Debug\RabbitMQSqlClr48.dll'
  WITH PERMISSION_SET = UNSAFE;  	
  

