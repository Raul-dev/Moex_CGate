--Table
SELECT * FROM [dbo].[session_log]
SELECT * FROM [dbo].[session]
TRUNCATE TABLE [dbo].[session_log]
DELETE FROM [dbo].[session]

SELECT * FROM [rmq].[RabbitSetting]
SELECT * FROM [rmq].[RabbitEndpoint]
SELECT * FROM [audit].[Setting] 
UPDATE [rmq].[RabbitSetting]
 SET SettingStringValue = 'server=localhost; database=CGate; uid=CGateUser; pwd=MyPassword321'
WHERE SettingID = 1

UPDATE [audit].[Setting] SET 
IntValue = 1
WHERE ID = 2
EXEC [rmq].[sp_clr_InitialiseRabbitMq]

DECLARE @AuditType varchar(250) = 'Table', @iterations int = 0, @DateStart datetime2(4) = GetDate(), @DateFinish datetime2(4)
WHILE @iterations < 10000 BEGIN
  EXEC [audit].[sp_LogText_Add] @AuditEnable ='FullAuditEnabled', 
    @KeyField = '@iterations',
    @KeyValue = @iterations,
    @MessageCode = @AuditType,
    @Message ='test'
  SET @iterations = @iterations + 1
END
SET @DateFinish = GetDate()
SELECT DateDiff(ms, @DateStart, @DateFinish)
GO
[audit].[LogText]
--Linkedserver
UPDATE [audit].[Setting] SET 
IntValue = 2
WHERE ID = 2
EXEC [rmq].[sp_clr_InitialiseRabbitMq]

DECLARE @AuditType varchar(250) = 'Linkedserver', @iterations int = 0, @DateStart datetime2(4) = GetDate(), @DateFinish datetime2(4)
WHILE @iterations < 10000 BEGIN
  EXEC [audit].[sp_LogText_Add] @AuditEnable ='FullAuditEnabled', 
    @KeyField = '@iterations',
    @KeyValue = @iterations,
    @MessageCode = @AuditType,
    @Message ='test'
  SET @iterations = @iterations + 1
END
SET @DateFinish = GetDate()
SELECT DateDiff(ms, @DateStart, @DateFinish)
GO

--Rabbit
UPDATE rmq.RabbitEndpoint SET IsEnabled = 0
UPDATE rmq.RabbitEndpoint SET
IsEnabled = 1
WHERE AliasName = 'CGateAuditLT'
UPDATE [audit].[Setting] SET 
IntValue = 3
WHERE Code = 'AuditLTTypeDefault'
EXEC [rmq].[sp_clr_InitialiseRabbitMq]
[CGate]
DECLARE @AuditType varchar(250) = 'Rabbit', @iterations int = 0, @DateStart datetime2(4) = GetDate(), @DateFinish datetime2(4)
WHILE @iterations < 10 BEGIN
  EXEC [audit].[sp_LogText_Add] @AuditEnable ='FullAuditEnabled', 
    @KeyField = '@iterations',
    @KeyValue = @iterations,
    @MessageCode = @AuditType,
    @Message ='test'
  SET @iterations = @iterations + 1
END
SET @DateFinish = GetDate()
SELECT DateDiff(ms, @DateStart, @DateFinish)
GO
14all41



--  http://localhost:15672/
sp_clr_InitialiseRabbitMq
--EXEC [rmq].[sp_clr_InitialiseRabbitMq]
EXEC [rmq].[sp_audit_Initialise]

EXEC [rmq].[sp_SomeProcessingStuff] @id = 1
SELECT * FROM [rmq].[RabbitEndpoint]
  @MainID           bigint = NULL,
  @ParentID         bigint = NULL,
  @StartTime        datetime2(4),
  @SysUserName      varchar(256),

DECLARE @Data varchar(max) = '[["1","2025.01.01 00:21:01.000000"],["2","2025.01.01 00:22:01.000000"],["21","2025.01.01 00:23:01.000000"]]'
SELECT * 
FROM OPENJSON(@Data,'$')
WITH (
  [MainID]  bigint '$[0]',
  [StartTime] datetime2(4) '$[1]'
)
EXEC sp_configure 'show advanced options', 1;
RECONFIGURE;
EXEC sp_configure 'clr strict security', 0;
RECONFIGURE;
EXEC sp_configure 'clr enabled', 1;
RECONFIGURE;
EXEC sp_configure 'show advanced options', 0;
RECONFIGURE;
