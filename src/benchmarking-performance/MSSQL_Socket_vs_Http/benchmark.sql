-- Сравнение Socket-CLR и HTTP-CLR из T-SQL: 1000 запросов x 5 КБ.
--
-- Предусловия:
--   1. Развернуты обе функции: dbo.fn_GetPositionFast (TCP) и dbo.fn_GetPositionOverHttp (HTTP).
--   2. Доступен хотя бы один из сервисов: TcpService (порт 8888), HttpService (порт 8080).
--      (Если отсутствует TCP-сервис,-httpqu будет отдавать 'ERROR: ...' — сравниваются с блокировкой первого запроса.)
-- Запускать в SSMS. Сравнение смотреть по метрике на вкладке результата:
--   "SQL Server parse and compile time" + "Wall-time первыми строк".

USE [CGate]
GO

IF OBJECT_ID('tempdb..#Numbers') IS NOT NULL DROP TABLE #Numbers;
GO

SELECT TOP (1000)
    ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
INTO   #Numbers
FROM   sys.all_objects a CROSS JOIN sys.all_objects b;
GO

SET STATISTICS TIME ON;

PRINT '=== TCP (Socket CLR): 1000 x 5KB ===';
SELECT COUNT_BIG(*) AS rows_pushed, SUM(LEN(v.payload)) AS total_chars
FROM 
    (SELECT dbo.fn_GetPositionFast(n) AS payload FROM #Numbers) v;

PRINT '=== HTTP (Kestrel CLR): 1000 x 5KB ===';
SELECT COUNT_BIG(*) AS rows_pushed, SUM(LEN(v.payload)) AS total_chars
FROM 
    (SELECT dbo.fn_GetPositionOverHttp(n) AS payload FROM #Numbers) v;

SET STATISTICS TIME OFF;
GO
