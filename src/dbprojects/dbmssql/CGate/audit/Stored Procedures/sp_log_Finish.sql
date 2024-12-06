

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
        @TranCount int          = @@TRANCOUNT
    
    IF OBJECT_ID('tempdb..#LogProc') IS NULL
        CREATE TABLE #LogProc(LogID int Primary Key NOT NULL)

    IF EXISTS ( SELECT 1 FROM sys.dm_exec_sessions WITH(nolock)
        WHERE session_id = @@SPID AND transaction_isolation_level = 5)
        --SNAPSHOT ISOLATION LEVEL Remote access is not supported for transaction isolation level "SNAPSHOT".

        EXEC [audit].sp_lnk_Update
            @LogID         = @LogID,
            @EndTime       = @EndTime,
            @RowCount      = @RowCount,
            @TranCount     = @TranCount,
            @ProcedureInfo = @ProcedureInfo,
            @ErrorMessage  = @ErrorMessage
    
    ELSE
        EXEC [$(LinkSRVLog)].[$(DatabaseName)].[audit].sp_lnk_Update
            @LogID         = @LogID,
            @EndTime       = @EndTime,
            @RowCount      = @RowCount,
            @TranCount     = @TranCount,
            @ProcedureInfo = @ProcedureInfo,
            @ErrorMessage  = @ErrorMessage

    DELETE FROM #LogProc WHERE LogID >= @LogID
END