-- Security/PostDeploy/EnableUsers.sql
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'CGateUser')
BEGIN
    -- Если логина нет, создаем (на случай, если DACPAC пропустил)
    CREATE LOGIN [CGateUser] WITH PASSWORD = 'MyPassword321!';
	
END
ELSE
BEGIN
    -- Если есть, просто включаем
    ALTER LOGIN [CGateUser] ENABLE;
END
IF USER_ID('CGateUser') IS NULL
	CREATE USER [CGateUser] FOR LOGIN [CGateUser];

ALTER ROLE db_owner ADD MEMBER [CGateUser];
GRANT CONNECT TO [CGateUser]; 

IF NOT EXISTS(SELECT 1 FROM [dbo].[Setting] WHERE SettingID = 'FullAuditEnabled' )
INSERT INTO [dbo].[Setting] (SettingID, StrValue) values('FullAuditEnabled', N'FullAuditEnabled')

IF NOT EXISTS(SELECT 1 FROM [audit].[Setting] WHERE ID = 1 )
INSERT [audit].[Setting](ID,IntValue,Code,StrValue)
VALUES(1,1,1,1)
