
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