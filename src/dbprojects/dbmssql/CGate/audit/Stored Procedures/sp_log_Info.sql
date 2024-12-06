CREATE   PROCEDURE [audit].[sp_log_Info] 
    @LogID         int          = NULL,
    @ProcedureInfo varchar(max) = NULL
AS 
BEGIN
                    
    IF @LogID IS NULL RETURN 0
    IF EXISTS ( SELECT 1 FROM sys.dm_exec_sessions WITH(nolock)
        WHERE session_id = @@SPID AND transaction_isolation_level = 5)
        --SNAPSHOT ISOLATION LEVEL Remote access is not supported for transaction isolation level "SNAPSHOT".
        EXEC [audit].sp_lnk_Update
            @LogID         = @LogID,
            @ProcedureInfo = @ProcedureInfo
    ELSE
        EXEC [$(LinkSRVLog)].[$(DatabaseName)].[audit].sp_lnk_Update
            @LogID         = @LogID,
            @ProcedureInfo = @ProcedureInfo

    RETURN 0
END