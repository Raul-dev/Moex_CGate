IF NOT EXISTS(SELECT 1 FROM [mq].[DataSource] WHERE [DataSourceId] = 1)
    INSERT [mq].[DataSource] ([DataSourceId], [Name]) VALUES (1, N'crs')
IF NOT EXISTS(SELECT 1 FROM [mq].[DataSource] WHERE [DataSourceId] = 2)
    INSERT [mq].[DataSource] ([DataSourceId], [Name]) VALUES (2, N'test2')
IF NOT EXISTS(SELECT 1 FROM [mq].[DataSource] WHERE [DataSourceId] = 3)
    INSERT [mq].[DataSource] ([DataSourceId], [Name]) VALUES (3, N'test3')
IF NOT EXISTS(SELECT 1 FROM [mq].[DataSource] WHERE [DataSourceId] = 4)
    INSERT [mq].[DataSource] ([DataSourceId], [Name]) VALUES (4, N'test4')
IF NOT EXISTS(SELECT 1 FROM [mq].[DataSource] WHERE [DataSourceId] = 5)
    INSERT [mq].[DataSource] ([DataSourceId], [Name]) VALUES (5, N'test5')

IF NOT EXISTS(SELECT 1 FROM [mq].[MessageType] WHERE [MessageTypeId] = 1)
BEGIN
    INSERT [mq].[MessageType] ([MessageTypeId], [Name]) VALUES (1, N'Bulk')
    INSERT [mq].[MessageType] ([MessageTypeId], [Name]) VALUES (2, N'Full message')
END


DECLARE @session_state AS TABLE
(
    [SessionStateId] TINYINT,
    [Name]           NVARCHAR(100)
)

INSERT @session_state ([SessionStateId], [Name]) VALUES
(1, N'Начало обработки очереди MQ'),
(2, N'Завершение обработки очереди MQ'),
(3, N'Ошибка в процедуре'),
(4, N'Ошибка в сервисе'),
(5, N'Ручной запуск процедур загрузки из буфера'),
(6, N'Удаление из архива')

IF EXISTS ( 
    SELECT 1 FROM [mq].[SessionState] d
    LEFT OUTER JOIN @session_state s ON s.[SessionStateId] = d.[SessionStateId]
    WHERE s.[SessionStateId] IS NULL) THROW 60000, N'The table [mq].[SessionState] was change. ', 1;

MERGE INTO [mq].[SessionState] trg
USING 
@session_state src ON src.[SessionStateId] = trg.[SessionStateId]
WHEN MATCHED THEN UPDATE SET 
    [Name] = src.[Name]
WHEN NOT MATCHED BY TARGET THEN 
    INSERT ([SessionStateId], [Name]) VALUES (src.[SessionStateId], src.[Name])
WHEN NOT MATCHED BY SOURCE THEN DELETE;

IF NOT EXISTS(SELECT 1 FROM [mq].[Session] WHERE [DataSourceId] = 1)
BEGIN
    SET IDENTITY_INSERT [mq].[Session] ON
    INSERT INTO [mq].[Session] ([SessionId], [DataSourceId], [SessionStateId], [ErrorMessage])
    SELECT 0, 1, 5, NULL
    SET IDENTITY_INSERT [mq].[Session] OFF
END


