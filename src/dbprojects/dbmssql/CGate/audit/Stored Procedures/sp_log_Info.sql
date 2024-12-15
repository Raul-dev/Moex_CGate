CREATE   PROCEDURE [audit].[sp_log_Info] 
    @LogID         int          = NULL,
    @ProcedureInfo varchar(max) = NULL
AS 
BEGIN
                    
    IF @LogID IS NULL RETURN 0
    IF [audit].[fn_log_IsLnk]() = 0
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