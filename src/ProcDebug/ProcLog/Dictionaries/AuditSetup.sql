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
