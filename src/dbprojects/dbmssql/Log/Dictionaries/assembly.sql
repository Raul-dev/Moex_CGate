CREATE DATABASE CGate45
USE [CGate45]
CREATE ROLE rmq
CREATE schema rmq
EXEC dbo.sp_configure 'show advanced options', 1;
RECONFIGURE;
EXEC dbo.sp_configure 'clr enabled', 1;
RECONFIGURE;

ALTER DATABASE CGate45 SET TRUSTWORTHY ON;

  CREATE ASSEMBLY [RabbitMQSqlClr4]
  AUTHORIZATION rmq
  FROM 'D:\\Temp\\RabbitMQSqlClr4.dll'
  WITH PERMISSION_SET = UNSAFE;  

  CREATE ASSEMBLY [RabbitMQ.Client]
  AUTHORIZATION rmq
  FROM 'D:\\Temp\\RabbitMQ.Client.dll'
  WITH PERMISSION_SET = UNSAFE;  

  USE [CGate]
GO

/****** Object:  StoredProcedure [rmq].[sp_clr_InitialiseRabbitMq]    Script Date: 2/28/2026 5:41:43 PM ******/
SET ANSI_NULLS OFF
GO

SET QUOTED_IDENTIFIER OFF
GO

CREATE PROCEDURE [rmq].[sp_clr_InitialiseRabbitMq]
WITH EXECUTE AS CALLER
AS
EXTERNAL NAME [RabbitMQSqlClr4].[RabbitMQSqlClr.RabbitMQSqlServer].[sp_clr_InitialiseRabbitMq]
GO

SET ANSI_NULLS OFF
GO

SET QUOTED_IDENTIFIER OFF
GO

CREATE PROCEDURE [rmq].[sp_clr_PostRabbitMsg]
	@EndpointID [int],
	@Message [nvarchar](max)
WITH EXECUTE AS CALLER
AS
EXTERNAL NAME [RabbitMQSqlClr4].[RabbitMQSqlClr.RabbitMQSqlServer].[sp_clr_PostRabbitMsg]
GO


SET ANSI_NULLS OFF
GO

SET QUOTED_IDENTIFIER OFF
GO

CREATE PROCEDURE [rmq].[sp_clr_ReloadRabbitEndpoints]
WITH EXECUTE AS CALLER
AS
EXTERNAL NAME [RabbitMQSqlClr4].[RabbitMQSqlClr.RabbitMQSqlServer].[sp_clr_ReloadRabbitEndpoints]
GO