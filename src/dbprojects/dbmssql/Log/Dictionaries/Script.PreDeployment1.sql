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
USE [$(DatabaseName)];
GO

IF EXISTS (SELECT * FROM sys.database_principals WHERE name = 'CGateUser')
BEGIN
    -- Принудительно сшиваем SID логина и пользователя до начала деплоя схемы
    ALTER USER [CGateUser] WITH LOGIN = [CGateUser];
END
:r .\LinkedServ.sql
