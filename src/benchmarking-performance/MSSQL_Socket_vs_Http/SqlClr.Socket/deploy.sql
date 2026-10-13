-- Развертывание SQL CLR Tcp-клиента (PerfClrTcp) — отказ от HTTP в пользу Socket.
--
-- Требования:
--   1. SQL Server 2019+ на хосте с .NET Framework 4.8 (для SQL Server 2014-2017 сборку 4.8
--      поддерживать изоляцией версии см. документацию; сборка строится под net48).
--   2. Включенный CLR: EXEC sp_configure 'clr enabled', 1; RECONFIGURE;
--   3. Либо отключенный "clr strict security", либо подписанная сборка (sp_add_trusted_assembly),
--      либо TRUSTWORTHY база на доверенном окружении.
-- Выполнять под sysadmin в контексте целевой базы.

USE [CGate] -- целевая база
GO

EXEC sp_configure 'show advanced options', 1; RECONFIGURE;
EXEC sp_configure 'clr enabled', 1; RECONFIGURE;
GO

IF OBJECT_ID('dbo.fn_GetPositionFast') IS NOT NULL
    DROP FUNCTION dbo.fn_GetPositionFast
GO

IF EXISTS (SELECT 1 FROM sys.assemblies WHERE name=N'PerfClrTcp') DROP ASSEMBLY [PerfClrTcp]
GO

-- Собирать solution после: dotnet build MSSQL_Socket_Http_Performance.sln -c Release
CREATE ASSEMBLY [PerfClrTcp]
FROM '<.realpath>\MSSQL_Socket_Http_Performance\SqlClr.Socket\bin\Release\SqlClr.Socket.dll'
WITH PERMISSION_SET = UNSAFE; -- прямой доступ к сокетам -> UNSAFE
GO

CREATE FUNCTION dbo.fn_GetPositionFast (@positionId int)
RETURNS nvarchar(max)
AS EXTERNAL NAME [PerfClrTcp].[SqlClr.SqlSocketClient].[SqlGetPositionFast];
GO

-- Проверка одной позиции:
--   SET STATISTICS TIME ON;
--   SELECT dbo.fn_GetPositionFast(1) AS payload;
--   SELECT LEN(dbo.fn_GetPositionFast(1)) AS len;
GO
