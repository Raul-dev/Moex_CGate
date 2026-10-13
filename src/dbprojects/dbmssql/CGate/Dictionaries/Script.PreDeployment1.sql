/*
 Pre-Deployment Script Template							
--------------------------------------------------------------------------------------
 This file contains SQL statements that will be executed before the build script.	
 Use SQLCMD syntax to include a file in the pre-deployment script.			
 Example:      :r .\myfile.sql								
 Use SQLCMD syntax to reference a variable in the pre-deployment script.		
 Example:      :setvar TableName MyTable							
               SELECT * FROM [$(TableName)]					
--------------------------------------------------------------------------------------
*/
:setvar RoleDatabase "master"
USE [$(RoleDatabase)];
GO

IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'CGateUser')
BEGIN
    CREATE LOGIN [CGateUser] WITH PASSWORD = 'MyPassword321!', CHECK_EXPIRATION = OFF, CHECK_POLICY = OFF;
END
ELSE
BEGIN
    ALTER LOGIN [CGateUser] ENABLE;
END
GO
-- ПОМОЖЕТ ТУТ: Включаем AllowInProcess для провайдера OLE DB v19 перед созданием линков
IF IS_SRVROLEMEMBER('sysadmin') = 1
BEGIN
    EXEC master.dbo.sp_MSset_oledb_prop N'MSOLEDBSQL19', N'AllowInProcess', 1;
    PRINT 'Свойство AllowInProcess для MSOLEDBSQL19 успешно включено.';
    EXEC master.dbo.sp_MSset_oledb_prop N'MSOLEDBSQL19', N'DynamicParameters', 1

END
ELSE
BEGIN
    PRINT 'ВНИМАНИЕ: Недостаточно прав (не sysadmin) для настройки свойств OLE DB провайдера.';
END
GO
-- 1. Задаем хэш вашей сборки в переменную (обязательно в формате BINARY)
DECLARE @assemblyHash BINARY(64) = 0x2F5E0553A85BA199C3C804593A5E8444CDBBA1F8E1EBD9F105148E368193CEC67A70812CD5024843FA14DB2091A210B77AD43BA503F33D1E85511AB4C78F771B

-- 2. Проверяем, нет ли уже такого хэша в списке доверенных
IF NOT EXISTS (SELECT * FROM sys.trusted_assemblies WHERE hash = @assemblyHash)
BEGIN
    EXEC sp_add_trusted_assembly 
        @hash = @assemblyHash,
        @description = N'Microsoft Diagnostics Tracing EventSource';
        
    PRINT 'Хэш сборки успешно добавлен в список доверенных.';
END
ELSE
BEGIN
    PRINT 'Хэш сборки уже находится в списке доверенных. Пропуск.';
END

-- 1. Создаем главный ключ шифрования для базы master, если его нет
IF NOT EXISTS (SELECT * FROM sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##')
BEGIN
    CREATE MASTER KEY ENCRYPTION BY PASSWORD = 'SafePassword_TaxLot_2026!';
END
GO

-- 2. Создаем асимметричный ключ безопасности, используя относительную переменную SQLCMD
IF NOT EXISTS (SELECT * FROM sys.asymmetric_keys WHERE name = 'TaxLotManagerAsymKey')
BEGIN
    -- Конструкция $(KeyPath) автоматически развернется в полный путь во время публикации
    CREATE ASYMMETRIC KEY TaxLotManagerAsymKey 
    FROM FILE = '$(KeyPath)'; 
END
GO

-- 3. Создаем системный логин, привязанный к этому ключу
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'TaxLotManagerClrLogin')
BEGIN
    CREATE LOGIN TaxLotManagerClrLogin FROM ASYMMETRIC KEY TaxLotManagerAsymKey;
END
GO

-- 4. Выдаем глобальное разрешение на запуск UNSAFE-кода
GRANT UNSAFE ASSEMBLY TO TaxLotManagerClrLogin;
GO
-- Возвращаемся в контекст нашей БД для привязки пользователя
USE [$(DatabaseName)];
GO

IF EXISTS (SELECT * FROM sys.database_principals WHERE name = 'CGateUser')
BEGIN
    -- Принудительно сшиваем SID логина и пользователя до начала деплоя схемы
    ALTER USER [CGateUser] WITH LOGIN = [CGateUser];
END
:r .\LinkedServ.sql
