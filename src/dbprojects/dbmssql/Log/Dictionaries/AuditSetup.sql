IF NOT EXISTS(SELECT 1 FROM [dbo].[Setting] WHERE SettingID = 'FullAuditEnabled' )
INSERT INTO [dbo].[Setting] (SettingID, StrValue) values('FullAuditEnabled', N'FullAuditEnabled')


IF NOT EXISTS(SELECT * FROM [audit].[AuditTypeLT] )
INSERT [audit].[AuditTypeLT]([AuditTypeID],[Code],[Description])
VALUES 
  (1,'LocalTable', 'Лог добавляется в локальную таблицу'),
  (2,'LinkedServer', 'Лог добавляется добавляется в таблицу линкед сервера'),
  (3,'Rabbit', 'Лог отправляется в очередь RabbitMQ')
  
IF NOT EXISTS(SELECT * FROM [audit].[AuditTypeSP] )
INSERT [audit].[AuditTypeSP]([AuditTypeID],[Code],[Description])
VALUES 
  (1,'LocalTable','Лог добавляется/меняется построчтно sp_log_Start sp_log_Info sp_log_Finish'),
  (2,'LinkedServer', 'Лог добавляется/меняется построчтно sp_log_Start sp_log_Info sp_log_Finish'),
  (3,'Rabbit','Лог добавляется только в sp_log_Start, режим отладки'),
  (4,'LocalTablePackage', 'Лог добавляется пакетом в sp_log_Finish'),
  (5,'LinkedServerPackage', 'Лог добавляется пакетом в sp_log_Finish'),
  (6,'RabbitPackage', 'Лог добавляется пакетом в sp_log_Finish')

DECLARE @database VARCHAR(200) = DB_NAME(),
    @Dbserver NVARCHAR(200) = 'HOMEST', --  '$(LinkSRVLog)' --- TODO Addres variable
    @RabbitServer NVARCHAR(200) = 'HOMEST', --  '$(LinkSRVLog)' --- TODO Addres variable
	@DbName NVARCHAR(200) = DB_NAME(),
    @SqlStr  NVARCHAR(MAX);

SET @SqlStr = 'server=' + @Dbserver + '; database=' + @DbName + '; uid=CGateUser; pwd=MyPassword321'
IF NOT EXISTS(SELECT * FROM [rmq].[RabbitSetting])
     INSERT [rmq].[RabbitSetting] (SettingID, SettingIntValue, SettingStringValue)
     VALUES (1,	NULL, @SqlStr)

EXEC rmq.sp_UpsertRabbitEndpoint @Alias = 'CGateAuditSP',
								 @ServerName = @RabbitServer,   --- TODO Addres of Rabbitserver
								 @Port = 5672,
								 @VHost = '/',
								 @LoginName = 'admin',
								 @LoginPassword = 'admin',
								 @Exchange = 'amq.direct',
								 @RoutingKey = 'Audit.SP.CGate',
								 @ConnectionChannels = 1,
								 @IsEnabled = 1

EXEC rmq.sp_UpsertRabbitEndpoint @Alias = 'CGateAuditLT',
								 @ServerName = @RabbitServer,   --- TODO Addres of Rabbitserver
								 @Port = 5672,
								 @VHost = '/',
								 @LoginName = 'admin',
								 @LoginPassword = 'admin',
								 @Exchange = 'amq.direct',
								 @RoutingKey = 'Audit.LT.CGate',
								 @ConnectionChannels = 1,
								 @IsEnabled = 1
EXEC rmq.sp_UpsertRabbitEndpoint @Alias = 'CGateAuditErr',
								 @ServerName = @RabbitServer,   --- TODO Addres of Rabbitserver
								 @Port = 5672,
								 @VHost = '/',
								 @LoginName = 'admin',
								 @LoginPassword = 'admin',
								 @Exchange = 'amq.direct',
								 @RoutingKey = 'Audit.Err.CGate',
								 @ConnectionChannels = 1,
								 @IsEnabled = 1

EXEC audit.sp_Initialise @AuditType = 'Table'