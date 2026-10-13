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

GO-- 1. Переключаемся на вашу базу данных (замените на ваше реальное имя БД)
USE [CGate];
GO

-- 2. Включаем поддержку CLR на уровне всего экземпляра SQL Server
EXEC sp_configure 'clr enabled', 1;
RECONFIGURE;
GO

-- 3. Разрешаем базе данных выполнять сборки с повышенными правами безопасности
-- Это необходимо, так как наш код использует оптимизированный SqlBulkCopy внутри
ALTER DATABASE CGate SET TRUSTWORTHY ON;
GO
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_clr_ReloadRabbitEndpoints' AND schema_id = SCHEMA_ID('rmq'))
    DROP PROCEDURE [rmq].[sp_clr_ReloadRabbitEndpoints]
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_clr_InitialiseRabbitMq' AND schema_id = SCHEMA_ID('rmq'))
    DROP PROCEDURE [rmq].[sp_clr_InitialiseRabbitMq]

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_clr_PostRabbitMsg' AND schema_id = SCHEMA_ID('rmq'))
    DROP PROCEDURE [rmq].[sp_clr_PostRabbitMsg]
GO

IF EXISTS (SELECT * FROM sys.assemblies WHERE name = 'RabbitMQSqlClr48')
    DROP ASSEMBLY RabbitMQSqlClr48;

    
IF EXISTS (SELECT * FROM sys.assemblies WHERE name = 'RabbitMQ.Client')
DROP ASSEMBLY [RabbitMQ.Client]
GO

  CREATE ASSEMBLY [RabbitMQ.Client]
  AUTHORIZATION rmq
  FROM 'F:\Work\GitMy\Moex_CGate\src\CLR\RabbitMQSqlClr48\bin\Release\RabbitMQ.Client.dll'
  WITH PERMISSION_SET = UNSAFE;  	

  CREATE ASSEMBLY [RabbitMQSqlClr48]
  AUTHORIZATION rmq
  FROM 'F:\Work\GitMy\Moex_CGate\src\CLR\RabbitMQSqlClr48\bin\Release\RabbitMQSqlClr48.dll'
  WITH PERMISSION_SET = UNSAFE;  	
   
   
GO
CREATE OR ALTER PROCEDURE [rmq].[sp_clr_InitialiseRabbitMq]
WITH EXECUTE AS CALLER
AS
EXTERNAL NAME [RabbitMQSqlClr48].[RabbitMQSqlClr.RabbitMQSqlServer].[sp_clr_InitialiseRabbitMq]
GO


CREATE OR ALTER PROCEDURE [rmq].[sp_clr_PostRabbitMsg]
	@EndpointID [int],
	@Message [nvarchar](max)
WITH EXECUTE AS CALLER
AS
EXTERNAL NAME [RabbitMQSqlClr48].[RabbitMQSqlClr.RabbitMQSqlServer].[sp_clr_PostRabbitMsg]
GO

CREATE OR ALTER PROCEDURE [rmq].[sp_clr_ReloadRabbitEndpoints]
WITH EXECUTE AS CALLER
AS
EXTERNAL NAME [RabbitMQSqlClr48].[RabbitMQSqlClr.RabbitMQSqlServer].[sp_clr_ReloadRabbitEndpoints]
GO

