IF USER_ID('CGateUser') IS NULL
	CREATE USER [CGateUser] FOR LOGIN [CGateUser];

ALTER ROLE db_owner ADD MEMBER [CGateUser];
GRANT CONNECT TO [CGateUser]; 

IF NOT EXISTS(SELECT 1 FROM [dbo].[Setting] WHERE SettingID = 'FullAuditEnabled' )
INSERT INTO [dbo].[Setting] (SettingID, StrValue) values('FullAuditEnabled', N'FullAuditEnabled')


IF NOT EXISTS(SELECT * FROM [audit].[AuditTypeLT] )
INSERT [audit].[AuditTypeLT]([AuditTypeID],[Code],[Description])
VALUES 
  (1,'LocalTable', 'Лог добавляется в локальную таблицу'),
  (2,'LinkedServer', 'Лог добавляется добавляется в таблицу линкед сервера'),
  (3,'Rabbit', 'Лог отправляется в очередь RabbitMQ')


IF NOT EXISTS(SELECT 1 FROM [audit].[AuditTypeLT] WHERE [AuditTypeID] = 4)
    INSERT [audit].[AuditTypeLT]([AuditTypeID], [Code], [Description])
    VALUES (4, 'InMemoryTable', 'Лог добавляется в ультрабыструю таблицу в оперативной памяти');

IF NOT EXISTS(SELECT 1 FROM [audit].[AuditTypeLT] WHERE [AuditTypeID] = 5)
    INSERT [audit].[AuditTypeLT]([AuditTypeID], [Code], [Description])
    VALUES (5, 'SequentialDisk', 'Лог добавляется в дисковую таблицу с оптимизацией последовательного ключа');

IF NOT EXISTS(SELECT 1 FROM [audit].[AuditTypeLT] WHERE [AuditTypeID] = 6)
    INSERT [audit].[AuditTypeLT]([AuditTypeID], [Code], [Description])
    VALUES (6, 'ServiceBroker', 'Лог асинхронно отправляется во внутреннюю очередь Service Broker');

IF NOT EXISTS(SELECT * FROM [audit].[AuditTypeSP] )
INSERT [audit].[AuditTypeSP]([AuditTypeID],[Code],[Description])
VALUES 
  (1,'LocalTable','Лог добавляется/меняется построчтно sp_LogStart sp_LogInfo sp_LogFinish'),
  (2,'LinkedServer', 'Лог добавляется/меняется построчтно sp_LogStart sp_LogInfo sp_LogFinish'),
  (3,'Rabbit','Лог добавляется только в sp_LogStart, режим отладки'),
  (4,'LocalTablePackage', 'Лог добавляется пакетом в sp_LogFinish'),
  (5,'LinkedServerPackage', 'Лог добавляется пакетом в sp_LogFinish'),
  (6,'RabbitPackage', 'Лог добавляется пакетом в sp_LogFinish')
