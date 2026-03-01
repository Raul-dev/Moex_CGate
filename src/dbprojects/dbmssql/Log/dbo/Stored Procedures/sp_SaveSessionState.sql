
CREATE   PROCEDURE [dbo].[sp_SaveSessionState] 
    @session_id       bigint = NULL,
    @data_source_id   tinyint = 1,
    @session_state_id tinyint = 1,
    @error_message    varchar(4000) = NULL
AS
SET CONCAT_NULL_YIELDS_NULL ON
    DECLARE @LogID int, @ProcedureName varchar(510), @ProcedureParams varchar(max), @ProcedureInfo varchar(max), @AuditEnable nvarchar(256), @RowCount int
    SET @AuditEnable = [dbo].[fn_GetSettingValue]('FullAuditEnabled')
    IF @AuditEnable IS NOT NULL 
    BEGIN
        IF OBJECT_ID('tempdb..#LogProc') IS NULL
             SELECT * INTO #LogProc FROM [audit].[Template_LogProc]()
        SET @ProcedureName = '[' + OBJECT_SCHEMA_NAME(@@PROCID)+'].['+OBJECT_NAME(@@PROCID)+']'                        
        SET @ProcedureParams =
            '@session_id='+ISNULL(LTRIM(STR(@session_id)),'NULL') + ', ' +
            '@data_source_id='+ISNULL(LTRIM(STR(@data_source_id)),'NULL') + ', ' +
            '@session_state_id='+ISNULL(LTRIM(STR(@session_state_id)),'NULL')

            
        EXEC [audit].[sp_log_Start] @AuditEnable = @AuditEnable, @ProcedureName = @ProcedureName, @ProcedureParams = @ProcedureParams, @LogID = @LogID OUTPUT
    END
    IF(@session_id IS NULL)
    BEGIN
        DECLARE @IdentityOutput table ( [session_id] bigint )
        INSERT [session] ([data_source_id],    [session_state_id],    [error_message])
        OUTPUT inserted.[session_id] into @IdentityOutput
        VALUES(@data_source_id, @session_state_id, @error_message)

        INSERT [dbo].[session_log] ([session_id], [session_state_id], [error_message])
        SELECT [session_id], @session_state_id, @error_message FROM @IdentityOutput

        SELECT * FROM @IdentityOutput
    END 
    ELSE
    BEGIN
        UPDATE [session] SET 
            [session_state_id] = @session_state_id,    
            [error_message]    = @error_message,
            [dt_update]        = GetDate()
        WHERE [session_id] = @session_id
        
        INSERT [dbo].[session_log] ([session_id], [session_state_id], [error_message])
        VALUES(@session_id, @session_state_id, @error_message )

    END 
    EXEC [audit].[sp_log_Finish] @LogID = @LogID, @RowCount = @RowCount