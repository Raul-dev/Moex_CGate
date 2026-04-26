
GO

GO
PRINT N'Creating User [CGateUser]...';


GO
CREATE USER [CGateUser] FOR LOGIN [CGateUser];


GO
REVOKE CONNECT TO [CGateUser];


GO
PRINT N'Creating Role Membership [db_owner] for [CGateUser]...';


GO
EXECUTE sp_addrolemember @rolename = N'db_owner', @membername = N'CGateUser';


GO
PRINT N'Creating Schema [audit]...';


GO
CREATE SCHEMA [audit]
    AUTHORIZATION [dbo];


GO
PRINT N'Creating Table [audit].[LogProcedures]...';


GO
CREATE TABLE [audit].[LogProcedures] (
    [LogID]            BIGINT        IDENTITY (1, 1) NOT NULL,
    [MainID]           BIGINT        NULL,
    [ParentID]         BIGINT        NULL,
    [StartTime]        DATETIME2 (4) NOT NULL,
    [EndTime]          DATETIME2 (4) NULL,
    [Duration]         INT           NULL,
    [RowCount]         INT           NULL,
    [SysUserName]      VARCHAR (256) NOT NULL,
    [SysHostName]      VARCHAR (100) NOT NULL,
    [SysDbName]        VARCHAR (128) NOT NULL,
    [SysAppName]       VARCHAR (128) NOT NULL,
    [SPID]             INT           NOT NULL,
    [ProcedureName]    VARCHAR (512) NULL,
    [ProcedureParams]  VARCHAR (MAX) NULL,
    [ProcedureInfo]    VARCHAR (MAX) NULL,
    [ErrorMessage]     VARCHAR (MAX) NULL,
    [TransactionCount] INT           NULL,
    CONSTRAINT [PK_audit_LogProcedures] PRIMARY KEY CLUSTERED ([LogID] ASC)
);


GO
PRINT N'Creating Table [audit].[AuditTypeSP]...';


GO
CREATE TABLE [audit].[AuditTypeSP] (
    [AuditTypeID] INT           NOT NULL,
    [Code]        VARCHAR (50)  NOT NULL,
    [Description] VARCHAR (256) NULL,
    CONSTRAINT [PK_AuditTypeSP] PRIMARY KEY CLUSTERED ([AuditTypeID] ASC)
);


GO
PRINT N'Creating Table [audit].[Setting]...';


GO
CREATE TABLE [audit].[Setting] (
    [ID]       INT          NOT NULL,
    [IntValue] INT          NULL,
    [Code]     VARCHAR (50) NOT NULL,
    [StrValue] VARCHAR (50) NULL,
    CONSTRAINT [PK_Audit_Setting] PRIMARY KEY CLUSTERED ([ID] ASC)
);


GO
PRINT N'Creating Default Constraint [audit].[DF_LogProcedures_start_datetime]...';


GO
ALTER TABLE [audit].[LogProcedures]
    ADD CONSTRAINT [DF_LogProcedures_start_datetime] DEFAULT (getdate()) FOR [StartTime];


GO
PRINT N'Creating Default Constraint [audit].[DF_LogProcedures_SysUserName]...';


GO
ALTER TABLE [audit].[LogProcedures]
    ADD CONSTRAINT [DF_LogProcedures_SysUserName] DEFAULT (original_login()) FOR [SysUserName];


GO
PRINT N'Creating Default Constraint [audit].[DF_LogProcedures_SysHostName]...';


GO
ALTER TABLE [audit].[LogProcedures]
    ADD CONSTRAINT [DF_LogProcedures_SysHostName] DEFAULT (host_name()) FOR [SysHostName];


GO
PRINT N'Creating Default Constraint [audit].[DF_LogProcedures_SysAppName]...';


GO
ALTER TABLE [audit].[LogProcedures]
    ADD CONSTRAINT [DF_LogProcedures_SysAppName] DEFAULT (app_name()) FOR [SysAppName];


GO
PRINT N'Creating Default Constraint [audit].[DF_LogProcedures_spid]...';


GO
ALTER TABLE [audit].[LogProcedures]
    ADD CONSTRAINT [DF_LogProcedures_spid] DEFAULT (@@spid) FOR [SPID];


GO
PRINT N'Creating Function [audit].[fn_log_IsLnk]...';


GO
CREATE FUNCTION [audit].[fn_log_IsLnk](
)RETURNS BIT
AS
BEGIN
  RETURN IIF( EXISTS(SELECT * from sys.databases WITH(nolock) WHERE database_id = DB_ID() AND snapshot_isolation_state_desc = 'ON')  
              OR EXISTS(SELECT * FROM sys.dm_exec_sessions WITH(nolock) WHERE session_id = @@SPID AND transaction_isolation_level = 5),
              0,1
            )
END
GO
PRINT N'Creating Function [audit].[fn_GetAuditTypeSP]...';


GO
CREATE   FUNCTION [audit].[fn_GetAuditTypeSP](
    @AuditEnable nvarchar(256) = NULL
)RETURNS int
AS
BEGIN

    IF @AuditEnable = 'FullAuditEnabled'
        RETURN ISNULL((SELECT [IntValue] FROM [audit].[Setting] WHERE [ID] = 1), 0)
    ELSE
        RETURN ISNULL((SELECT [IntValue] FROM [audit].[Setting] WHERE [Code] = @AuditEnable), 0)
    RETURN 0 
END
GO
PRINT N'Creating Function [audit].[fn_GetEstimatedStringLength]...';


GO
CREATE FUNCTION [audit].fn_GetEstimatedStringLength 
(
    @user_type_id INT,
    @max_length SMALLINT,
    @precision TINYINT
)
RETURNS VARCHAR(10)
AS
BEGIN
    DECLARE @typeName NVARCHAR(128) = TYPE_NAME(@user_type_id);
    DECLARE @result VARCHAR(10);

    SET @result = CASE 
        -- Юникод строки (делим на 2, так как max_length в байтах)
        WHEN @typeName IN ('nvarchar', 'nchar', 'sysname') THEN 
            -- 'MAX' = 4000
            CASE WHEN @max_length = -1 THEN '4000' ELSE CAST(@max_length / 2 AS VARCHAR(10)) END
        
        -- Обычные строки
        WHEN @typeName IN ('varchar', 'char') THEN 
            -- 'MAX' = 8000
            CASE WHEN @max_length = -1 THEN '8000' ELSE CAST(@max_length AS VARCHAR(10)) END
        
        -- Точные числа (добавляем запас под знак '-' и точку '.')
        WHEN @typeName IN ('decimal', 'numeric') THEN 
            CAST(@precision + 2 AS VARCHAR(10))
        
        -- Целые числа (максимальное кол-во символов для каждого типа)
        WHEN @typeName = 'bigint'   THEN '20'
        WHEN @typeName = 'int'      THEN '11'
        WHEN @typeName = 'smallint' THEN '6'
        WHEN @typeName = 'tinyint'  THEN '3'
        
        -- Дата и время (форматы ISO)
        WHEN @typeName IN ('datetime', 'datetime2') THEN '27'
        WHEN @typeName = 'date'     THEN '10'
        WHEN @typeName = 'time'     THEN '16'
        
        -- Уникальные идентификаторы (GUID)
        WHEN @typeName = 'uniqueidentifier' THEN '36'
        
        -- Значение по умолчанию для прочих типов
        ELSE '4000' 
    END;

    RETURN @result;
END;
GO
PRINT N'Creating Function [dbo].[fn_GetSettingInt]...';


GO
CREATE Function [dbo].[fn_GetSettingInt](
    @SettingID    varchar(50)
) RETURNS INT
AS
BEGIN
  RETURN (SELECT CAST(LTRIM(StrValue) AS INT) FROM [dbo].[Setting] WITH( NOLOCK ) WHERE SettingID = @SettingID)
END
GO
PRINT N'Creating Function [dbo].[fn_GetSettingValue]...';


GO

CREATE       Function [dbo].[fn_GetSettingValue](
    @SettingID    varchar(50)
) RETURNS nvarchar(256)
AS
BEGIN
    RETURN (SELECT StrValue FROM [dbo].[Setting] WITH( NOLOCK ) WHERE SettingID = @SettingID)
END
GO
PRINT N'Creating Function [audit].[Template_LogProc]...';


GO
CREATE   FUNCTION [audit].[Template_LogProc](
)RETURNS @LogProc TABLE (
    [ID] [bigint] IDENTITY(1,1) NOT NULL ,
    [LogID] [bigint] NOT NULL Primary Key,
	[Msg] [varchar](max) COLLATE Cyrillic_General_CI_AS NULL
)
AS
BEGIN
  RETURN
END
GO
PRINT N'Creating Procedure [audit].[sp_Print]...';


GO
SET ANSI_NULLS ON;

SET QUOTED_IDENTIFIER OFF;


GO
/*

[audit].[sp_Print] @StrPrint = ' SELECT * FROM Security '
[audit].[sp_Print] 'ds', 8
*/

CREATE     PROCEDURE [audit].[sp_Print]
    @StrPrint   nvarchar(max),
    @PrintLevel int = 1 -- 1-Debug, 2-Info, 3-Warning, 4-Exception, 5-Test, 6-NotPrint
AS
BEGIN
    IF @PrintLevel >= 6
        RETURN 0
    DECLARE @AuditPrintLevel int
    SELECT @AuditPrintLevel = ISNULL([dbo].[fn_GetSettingInt]('AuditPrintLevel'), 0)

    IF @PrintLevel < @AuditPrintLevel
        RETURN 0

    DECLARE @StrTmp  nvarchar(4000),
        @StrPart     int = 3500,
        @StrLen      int = LEN(@StrPrint),
        @EndPart     int,
        @StrPrintTmp nvarchar(MAX)

    WHILE @StrLen > 0
        BEGIN 
            IF @StrLen <= @StrPart 
                BEGIN
                    Print @StrPrint
                    BREAK
                END
            SET @StrTmp = LEFT(@StrPrint, @StrPart)
            SET @StrPrintTmp = RIGHT(@StrPrint, @StrLen - LEN(@StrTmp))
            SET @EndPart = CHARINDEX(CHAR(13), @StrPrintTmp) +1
            SET @StrTmp = @StrTmp + LEFT(@StrPrintTmp, @EndPart)
            Print @StrTmp        

            SET @StrPrint = RIGHT(@StrPrint, @StrLen - LEN(@StrTmp))
            SET @StrLen  = LEN(@StrPrint)
        END 
END
GO
SET ANSI_NULLS, QUOTED_IDENTIFIER ON;


GO
PRINT N'Creating Procedure [audit].[sp_lnk_Insert]...';


GO

CREATE   PROCEDURE [audit].[sp_lnk_Insert](
  @MainID           bigint = NULL,
  @ParentID         bigint = NULL,
  @StartTime        datetime2(4),
  @SysUserName      varchar(256),
  @SysHostName      varchar(100),
  @SysDbName        varchar(128),
  @SysAppName       varchar(128),
  @SPID             int,
  @ProcedureName    varchar(512) ,
  @ProcedureParams  varchar(max),
  @TransactionCount int,
  @ProcedureInfo    varchar(max)  = NULL,
  @LogID            bigint OUTPUT
)
AS
BEGIN

  INSERT INTO [audit].LogProcedures (
    MainID,
    ParentID,
    StartTime,
    SysUserName,
    SysHostName,
    SysDbName,
    SysAppName,
    SPID,
    ProcedureName,
    ProcedureParams,
    TransactionCount,
    ProcedureInfo 
  ) VALUES (
    @MainID,
    @ParentID,
    @StartTime,
    @SysUserName,
    @SysHostName,
    @SysDbName,
    @SysAppName,
    @SPID,
    @ProcedureName,
    @ProcedureParams,
    @TransactionCount,
    @ProcedureInfo 
  )

  SET @LogID  = SCOPE_IDENTITY()
       
  IF @MainID IS NULL 
    UPDATE [audit].[LogProcedures]
      SET MainID   = @LogID
    WHERE LogID = @LogID
                
END;
GO
PRINT N'Creating Procedure [audit].[sp_lnk_Update]...';


GO

CREATE     PROCEDURE [audit].[sp_lnk_Update]
    @LogID          int,
    @EndTime        datetime2(4)  = NULL,
    @RowCount       int  = NULL,
    @TranCount      int  = NULL,
    @ProcedureInfo  varchar(max)  = NULL,
    @ErrorMessage   varchar(4000) = NULL
AS
    IF @EndTime IS NULL
    BEGIN
        UPDATE [audit].[LogProcedures] SET
            [ProcedureInfo] = ISNULL([ErrorMessage],'') 
                                 + ISNULL(@ErrorMessage + '; ', ''),
            [ErrorMessage]  = LEFT( ISNULL([ErrorMessage],'') 
                                 + ISNULL(@ErrorMessage + '; ', '')  , 2048)
        WHERE [LogID] = @LogID
    END
    ELSE
	    UPDATE [audit].[LogProcedures] SET
            [EndTime]       = @EndTime,
            [Duration]      = DATEDIFF(ms, [StartTime], @EndTime),
            [RowCount]      = @RowCount,
            [ProcedureInfo] = ISNULL([ProcedureInfo], '')
                                + CASE WHEN [TransactionCount] = @TranCount THEN '' 
                                ELSE 'Tran count changed to ' + ISNULL(LTRIM(STR(@TranCount, 10, 0)), 'NULL') + ';' END
                                + CASE WHEN @ProcedureInfo IS NULL THEN ''
                                ELSE 'Finish:' + CONVERT(varchar(19), @EndTime, 120) + ':' + @ProcedureInfo + ';' END, 
            [ErrorMessage]  = LEFT( ISNULL([ErrorMessage],'') 
                                     + ISNULL(@ErrorMessage + '; ', '')  , 2048)
        WHERE [LogID] = @LogID

RETURN 0
GO
PRINT N'Creating Procedure [audit].[sp_log_Info]...';


GO
CREATE   PROCEDURE [audit].[sp_log_Info] 
    @LogID         int          = NULL,
    @ProcedureInfo varchar(max) = NULL
AS 
BEGIN
                    
    IF @LogID IS NULL RETURN 0
    DECLARE @AuditTypeID int
    SELECT @AuditTypeID = [audit].[fn_GetAuditTypeSP](NULL)
    IF @AuditTypeID = 1
    --SNAPSHOT ISOLATION LEVEL Remote access is not supported for transaction isolation level "SNAPSHOT".
        EXEC [audit].sp_lnk_Update
            @LogID         = @LogID,
            @ProcedureInfo = @ProcedureInfo

    IF @AuditTypeID = 2
        EXEC [LinkSRVLog].[Log].[audit].sp_lnk_Update
            @LogID         = @LogID,
            @ProcedureInfo = @ProcedureInfo

    RETURN 0
END
GO
PRINT N'Creating Procedure [audit].[sp_log_Finish]...';


GO


CREATE     PROCEDURE [audit].[sp_log_Finish] 
    @LogID          int = NULL,    
    @RowCount       int = NULL,
    @ProcedureInfo  varchar(MAX) = NULL,
    @ErrorMessage   varchar(4000) = NULL
AS 
BEGIN
    SET NOCOUNT ON 
    IF @LogID IS NULL RETURN 0
    DECLARE 
        @EndTime   datetime2(4) = GetDate(),
        @TranCount int          = @@TRANCOUNT,
        @AuditTypeID int

    SELECT @AuditTypeID = [audit].[fn_GetAuditTypeSP](NULL)
    
    IF @AuditTypeID is NULL
        RETURN 0

    IF OBJECT_ID('tempdb..#LogProc') IS NULL
         SELECT * INTO #LogProc FROM [audit].[Template_LogProc]()

    IF @AuditTypeID = 1
    --SNAPSHOT ISOLATION LEVEL Remote access is not supported for transaction isolation level "SNAPSHOT".

        EXEC [audit].sp_lnk_Update
            @LogID         = @LogID,
            @EndTime       = @EndTime,
            @RowCount      = @RowCount,
            @TranCount     = @TranCount,
            @ProcedureInfo = @ProcedureInfo,
            @ErrorMessage  = @ErrorMessage
    
    IF @AuditTypeID = 2
        EXEC [LinkSRVLog].[Log].[audit].sp_lnk_Update
            @LogID         = @LogID,
            @EndTime       = @EndTime,
            @RowCount      = @RowCount,
            @TranCount     = @TranCount,
            @ProcedureInfo = @ProcedureInfo,
            @ErrorMessage  = @ErrorMessage

    DELETE FROM #LogProc WHERE LogID >= @LogID
END
GO
PRINT N'Creating Procedure [audit].[sp_log_Start]...';


GO

/*
BEGIN TRAN
DECLARE @LogID int 
EXEC [audit].sp_log_Start @AuditEnable ='FullAuditEnabled', @LogID = @LogID output
SELECT @LogID

SELECT [dbo].[fn_GetSettingValue]('FullAuditEnabled')
SELECT * FROM [audit].[LogProcedures]
ROLLBACk

*/

CREATE   PROCEDURE [audit].[sp_log_Start]   
    @AuditEnable nvarchar(256) = NULL,
    @ProcedureName   varchar(512)  = NULL,
    @ProcedureParams varchar(MAX)  = NULL,
    @LogID           int           = NULL OUTPUT
    
AS 
BEGIN
    SET NOCOUNT ON 
    DECLARE @AuditTypeID int
    SELECT @AuditTypeID = [audit].[fn_GetAuditTypeSP](@AuditEnable)
    
    IF @AuditTypeID is NULL
        RETURN 0

    IF OBJECT_ID('tempdb..#LogProc') IS NULL
        SELECT * INTO #LogProc FROM [audit].[Template_LogProc]()
                
    DECLARE 
        @ParentID    int, 
        @MainID      int, 
        @CountIds    int, 
        @StartTime   datetime2(4)  = GetDate(),  
        @SysDbName   nvarchar(128) = DB_NAME(),
        @SysUserName varchar(256)  = original_login(),
        @SysHostName varchar(128)  = CAST(@@SERVERNAME as varchar(100)),
        @SysAppName  varchar(128)  = app_name()

    SELECT @MainID    =   MIN(LogID), 
           @ParentID  =   MAX(LogID), 
           @CountIds  = COUNT(LogID) 
    FROM #LogProc
        
    SET @ProcedureName = LEFT(REPLICATE('  ', @CountIds) + LTRIM(RTRIM(@ProcedureName)), 512)

    IF @AuditTypeID = 1
    --SNAPSHOT ISOLATION LEVEL Remote access is not supported for transaction isolation level "SNAPSHOT".
        EXEC [audit].sp_lnk_Insert
            @MainID           = @MainID,
            @ParentID         = @ParentID,
            @StartTime        = @StartTime,
            @SysUserName      = @SysUserName,
            @SysHostName      = @SysHostName,
            @SysDbName        = @SysDbName,
            @SysAppName       = @SysAppName,
            @SPID             = @@SPID,
            @ProcedureName    = @ProcedureName,
            @ProcedureParams  = @ProcedureParams,
            @TransactionCount = @@TRANCOUNT,
            @LogID            = @LogID OUTPUT
    
    IF @AuditTypeID = 2
        EXEC [LinkSRVLog].[Log].[audit].sp_lnk_Insert
            @MainID           = @MainID,
            @ParentID         = @ParentID,
            @StartTime        = @StartTime,
            @SysUserName      = @SysUserName,
            @SysHostName      = @SysHostName,
            @SysDbName        = @SysDbName,
            @SysAppName       = @SysAppName,
            @SPID             = @@SPID,
            @ProcedureName    = @ProcedureName,
            @ProcedureParams  = @ProcedureParams,
            @TransactionCount = @@TRANCOUNT,
            @LogID            = @LogID OUTPUT



    IF @ParentID IS NULL OR @ParentID < @LogID 
        INSERT #LogProc(LogID) VALUES(ISNULL(@LogID,0))   

END
GO
/*
Post-Deployment Script Template							
--------------------------------------------------------------------------------------
 This file contains SQL statements that will be appended to the build script.		
 Use SQLCMD syntax to include a file in the post-deployment script.			
 Example:      :r .\myfile.sql								
 Use SQLCMD syntax to reference a variable in the post-deployment script.		
 Example:      :setvar TableName MyTable							
               SELECT * FROM [$(TableName)]					
--------------------------------------------------------------------------------------
*/
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


GO

IF NOT EXISTS(SELECT 1 FROM [dbo].[Setting] WHERE SettingID = 'FullAuditEnabled' )
INSERT INTO [dbo].[Setting] (SettingID, StrValue) values('FullAuditEnabled', N'FullAuditEnabled')

IF NOT EXISTS(SELECT 1 FROM [audit].[Setting] WHERE ID = 1 )
INSERT [audit].[Setting](ID,IntValue,Code,StrValue)
VALUES(1,1,1,1)

GO
PRINT N'Update complete.';


GO
