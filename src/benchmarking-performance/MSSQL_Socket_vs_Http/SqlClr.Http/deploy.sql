-- Развертывание SQL CLR Http-клиента (PerfClrHttp) — эталон сравнения HTTP GET vs Socket.
--
-- Требования:
--   1. SQL Server 2019+ на хосте с .NET Framework 4.8.
--   2. Включенный CLR (см. SqlClr.Socket\deploy.sql — там включение clr enabled).
--   3. Разрешены исходящие TCP-запросы с хоста SQL Server (реальм 127.0.0.1:8080 по умолчанию).
-- Выполнять под sysadmin в контексте целевой базы.

USE [CGate] -- целевая база
GO

IF OBJECT_ID('dbo.fn_GetPositionOverHttp') IS NOT NULL
    DROP FUNCTION dbo.fn_GetPositionOverHttp
GO

IF EXISTS (SELECT 1 FROM sys.assemblies WHERE name = N'PerfClrHttp')
    DROP ASSEMBLY [PerfClrHttp]
GO

-- Собирать solution после: dotnet build MSSQL_Socket_Http_Performance.sln -c Release
CREATE ASSEMBLY [PerfClrHttp]
FROM '<.realpath>\MSSQL_Socket_Http_Performance\SqlClr.Http\bin\Release\SqlClr.Http.dll'
WITH PERMISSION_SET = UNSAFE;
GO

CREATE FUNCTION dbo.fn_GetPositionOverHttp (@positionId int)
RETURNS nvarchar(max)
AS EXTERNAL NAME [PerfClrHttp].[SqlClr.SqlHttpClient].[SqlGetPositionOverHttp];
GO

-- Проверка одной позиции:
--   SET STATISTICS TIME ON;
--   SELECT dbo.fn_GetPositionOverHttp(1) AS payload;
--   SELECT LEN(dbo.fn_GetPositionOverHttp(1)) AS len;
GO
