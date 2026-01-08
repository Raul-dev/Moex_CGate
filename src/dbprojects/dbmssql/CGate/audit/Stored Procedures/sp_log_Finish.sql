

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
        EXEC [$(LinkSRVLog)].[$(DatabaseName)].[audit].sp_lnk_Update
            @LogID         = @LogID,
            @EndTime       = @EndTime,
            @RowCount      = @RowCount,
            @TranCount     = @TranCount,
            @ProcedureInfo = @ProcedureInfo,
            @ErrorMessage  = @ErrorMessage

    DELETE FROM #LogProc WHERE LogID >= @LogID
END