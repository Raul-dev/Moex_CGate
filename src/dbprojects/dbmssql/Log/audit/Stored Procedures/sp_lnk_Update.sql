
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