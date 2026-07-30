CREATE   PROCEDURE [audit].[sp_LogInfo] 
    @LogID         int          = NULL,
    @ProcedureInfo varchar(max) = NULL
AS 
BEGIN
                    
    IF @LogID IS NULL RETURN 0
    DECLARE @AuditTypeID int
    SELECT @AuditTypeID = [audit].[fn_GetAuditTypeSP](NULL)
    IF @AuditTypeID = 1
    --SNAPSHOT ISOLATION LEVEL Remote access is not supported for transaction isolation level "SNAPSHOT".
        EXEC [audit].sp_LnkUpdate
            @LogID         = @LogID,
            @ProcedureInfo = @ProcedureInfo

    IF @AuditTypeID = 2
        EXEC [$(LinkSRVLog)].[$(DatabaseName)].[audit].sp_LnkUpdate
            @LogID         = @LogID,
            @ProcedureInfo = @ProcedureInfo

    RETURN 0
END