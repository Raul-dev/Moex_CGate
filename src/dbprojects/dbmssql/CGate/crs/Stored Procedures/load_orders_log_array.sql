/*
SPECTRA 730
SELECT * FROM [crs].[orders_log_buffer]
SELECT * FROM [crs].[orders_log]
UPDATE [crs].[orders_log_buffer] SET [dt_update] = '19000101'

EXEC [crs].[load_orders_log_array] @SessionId = 0, @Debug = 1

*/
CREATE   PROCEDURE [crs].[load_orders_log_array]
  @SessionId         bigint         = NULL,
  @BufferHistoryMode  tinyint        = 0,  -- 0 - Do not delete the buffering history.
                                           -- 1 - Delete the buffering history.
                                           -- 2 - Keep the buffering history for 1 days.
                                           -- 3 - Keep the buffering history for a days from setup.
  @RowCount           int            = NULL OUTPUT,
  @BufferId           bigint         = NULL OUTPUT,
  @ErrorMessage       varchar(4000)  = NULL OUTPUT,
  @Debug              bit            = 0
AS
BEGIN
  
SET CONCAT_NULL_YIELDS_NULL ON
SET NOCOUNT ON
DECLARE @LogID int, @ProcedureName varchar(510), @ProcedureParams varchar(max), @ProcedureInfo varchar(max), @AuditProcEnable nvarchar(256)
SET @AuditProcEnable = [dbo].[fn_GetSettingValue]('AuditProcAll')
SET @ProcedureName = '[' + OBJECT_SCHEMA_NAME(@@PROCID)+'].['+OBJECT_NAME(@@PROCID)+']'
IF @AuditProcEnable IS NOT NULL 
BEGIN
  IF OBJECT_ID('tempdb..#LogProc') IS NULL
    CREATE TABLE #LogProc(LogID int Primary Key NOT NULL)
  
  SET @ProcedureParams =
    '@SessionId=' + ISNULL(LTRIM(STR(@SessionId)),'NULL') + ', ' +
    '@BufferHistoryMode=' + ISNULL(LTRIM(STR(@BufferHistoryMode)),'NULL')
END
SET XACT_ABORT OFF

--SET TRANSACTION ISOLATION LEVEL READ COMMITTED
--SET DEADLOCK_PRIORITY LOW
SET TRANSACTION ISOLATION LEVEL SNAPSHOT
DECLARE @MinDate datetime2(4) = DATEFROMPARTS(1900, 01, 01),
    @UpdateDate datetime2(4)  = GetDate(),
    @BufferHistoryDays int

IF @BufferHistoryMode = 2
  SET @BufferHistoryDays = 1
ELSE
  SET @BufferHistoryDays = dbo.fn_GetBufferingDays(@ProcedureName)

CREATE TABLE #LockedList (
    [buffer_id] bigint Primary key,
    [msg_id] uniqueidentifier,
    [RefID] bigint,
    [msgtype_id] tinyint
)

CREATE TABLE #LockedListUniq(
    [buffer_id] bigint Primary key,
    [RefID] bigint
)

BEGIN TRY
  BEGIN TRANSACTION

    IF @AuditProcEnable IS NOT NULL 
        EXEC [audit].[sp_log_Start] @AuditProcEnable = @AuditProcEnable, @ProcedureName = @ProcedureName, @ProcedureParams = @ProcedureParams, @LogID = @LogID OUTPUT

    INSERT INTO #LockedList
    SELECT TOP 500000 [buffer_id], [msg_id], [RefID], [msgtype_id]
    FROM [crs].[orders_log_buffer] b 
    WHERE b.[dt_update] = @MinDate
    ORDER BY [buffer_id]
    

    SET @RowCount = @@ROWCOUNT;
    IF @RowCount = 0 
    BEGIN
    
        EXEC [audit].[sp_log_Finish] @LogID = @LogID, @RowCount = 0, @ProcedureInfo = 'Empty buffer'
        COMMIT TRANSACTION
        RETURN 0
    END

    INSERT INTO #LockedListUniq
    SELECT [buffer_id] = MAX([buffer_id]), [RefID]
    FROM #LockedList l
    WHERE l.[msgtype_id] = 1 
    GROUP BY [RefID]
    SET @RowCount = @@ROWCOUNT;
    
    MERGE INTO [crs].[orders_log] trg
    USING 
    (
      SELECT 
         [replID]
        ,[replRev]
        ,[replAct]
        ,[public_order_id]
        ,[sess_id]
        ,[isin_id]
        ,[public_amount]
        ,[public_amount_rest]
        ,[id_deal]
        ,[xstatus]
        ,[xstatus2]
        ,[price]
        ,[moment] = CONVERT([datetime2](3), [moment], 102)
        ,[moment_ns]
        ,[dir]
        ,[public_action]
        ,[deal_price]
        ,[client_code]
        ,[login_from]
        ,[comment]
        ,[ext_id]
        ,[broker_to]
        ,[broker_to_rts]
        ,[broker_from_rts]
        ,[date_exp] = CONVERT([datetime2](3), [date_exp], 102)
        ,[id_ord1]
        ,[aspref]
        ,[private_order_id]
        ,[private_amount]
        ,[private_amount_rest]
        ,[variance_amount]
        ,[disclose_const_amount]
        ,[private_action]
        ,[reason]
        ,[match_ref]
        ,[compliance_id]
        ,[edition]
      FROM #LockedListUniq L 
      INNER JOIN [crs].[orders_log_buffer] b ON b.[buffer_id] = L.[buffer_id]
      CROSS APPLY (
        SELECT *
        FROM OPENJSON('['+b.msg+']','$')
        WITH 
        (
	        [replID] [bigint] '$[0]',
	        [replRev] [bigint] '$[1]',
	        [replAct] [bigint] '$[2]',
	        [public_order_id] [bigint] '$[3]',
	        [sess_id] [int] '$[4]',
	        [isin_id] [int] '$[5]',
	        [public_amount] [bigint] '$[6]',
	        [public_amount_rest] [bigint] '$[7]',
	        [id_deal] [bigint] '$[8]',
	        [xstatus] [bigint] '$[9]',
	        [xstatus2] [bigint] '$[10]',
	        [price] [decimal](16, 5) '$[11]',
	        [moment] varchar(50) '$[12]',
	        [moment_ns] [decimal](20, 0) '$[13]',
	        [dir] [tinyint] '$[14]',
	        [public_action] [tinyint] '$[15]',
	        [deal_price] [decimal](16, 5) '$[16]',
	        [client_code] [nvarchar](7) '$[17]',
	        [login_from] [nvarchar](20) '$[18]',
	        [comment] [nvarchar](20) '$[19]',
	        [ext_id] [int] '$[20]',
	        [broker_to] [nvarchar](7) '$[21]',
	        [broker_to_rts] [nvarchar](7) '$[22]',
	        [broker_from_rts] [nvarchar](7) '$[23]',
	        [date_exp] varchar(50) '$[24]',
	        [id_ord1] [bigint] '$[25]',
	        [aspref] [int] '$[26]',
	        [private_order_id] [bigint] '$[27]',
            [private_amount] [bigint] '$[28]',
            [private_amount_rest] [bigint] '$[29]',
            [variance_amount] [bigint] '$[30]',
            [disclose_const_amount] [bigint] '$[31]',
            [private_action] [tinyint] '$[32]',
            [reason] [int] '$[33]',
            [match_ref] varchar(10) '$[34]',
            [compliance_id] varchar(1) '$[35]'
        ) 
      ) OL
       
    ) AS src
    ON src.[private_order_id] = trg.[private_order_id] WHEN MATCHED THEN 
    UPDATE SET
      [replID] = src.[replID],
      [replRev] = src.[replRev],
      [replAct] = src.[replAct],
      [public_order_id] = src.[public_order_id],
      [sess_id] = src.[sess_id],
      [isin_id] = src.[isin_id],
      [public_amount] = src.[public_amount],
      [public_amount_rest] = src.[public_amount_rest],
      [id_deal] = src.[id_deal],
      [xstatus] = src.[xstatus],
      [xstatus2] = src.[xstatus2],
      [price] = src.[price],
      --,[moment] = src.[moment],
      [moment_ns] = src.[moment_ns],
      [dir] = src.[dir],
      [public_action] = src.[public_action],
      [deal_price] = src.[deal_price],
      [client_code] = src.[client_code],
      [login_from] = src.[login_from],
      [comment] = src.[comment],
      [ext_id] = src.[ext_id],
      [broker_to] = src.[broker_to],
      [broker_to_rts] = src.[broker_to_rts],
      [broker_from_rts] = src.[broker_from_rts],
      [date_exp] = src.[date_exp],
      [id_ord1] = src.[id_ord1],
      [aspref] = src.[aspref],
      [private_order_id] = src.[private_order_id],
      [private_amount] = src.[private_amount],
      [private_amount_rest] = src.[private_amount_rest],
      [variance_amount] = src.[variance_amount],
      [disclose_const_amount] = src.[disclose_const_amount],
      [private_action] = src.[private_action],
      [reason] = src.[reason],
      [match_ref] = src.[match_ref],
      [compliance_id] = src.[compliance_id]
    WHEN NOT MATCHED BY TARGET
    THEN INSERT (
        [replID]
      ,[replRev]
      ,[replAct]
      ,[public_order_id]
      ,[sess_id]
      ,[isin_id]
      ,[public_amount]
      ,[public_amount_rest]
      ,[id_deal]
      ,[xstatus]
      ,[xstatus2]
      ,[price]
      ,[moment]
      ,[moment_ns]
      ,[dir]
      ,[public_action]
      ,[deal_price]
      ,[client_code]
      ,[login_from]
      ,[comment]
      ,[ext_id]
      ,[broker_to]
      ,[broker_to_rts]
      ,[broker_from_rts]
      ,[date_exp]
      ,[id_ord1]
      ,[aspref]
      ,[private_order_id]
      ,[private_amount]
      ,[private_amount_rest]
      ,[variance_amount]
      ,[disclose_const_amount]
      ,[private_action]
      ,[reason]
      ,[match_ref]
      ,[compliance_id]
    )
    VALUES
    (
       src.[replID],
      src.[replRev],
      src.[replAct],
      src.[public_order_id],
      src.[sess_id],
      src.[isin_id],
      src.[public_amount],
      src.[public_amount_rest],
      src.[id_deal],
      src.[xstatus],
      src.[xstatus2],
      src.[price],
      src.[moment],
      src.[moment_ns],
      src.[dir],
      src.[public_action],
      src.[deal_price],
      src.[client_code],
      src.[login_from],
      src.[comment],
      src.[ext_id],
      src.[broker_to],
      src.[broker_to_rts],
      src.[broker_from_rts],
      src.[date_exp],
      src.[id_ord1],
      src.[aspref],
      src.[private_order_id],
      src.[private_amount],
      src.[private_amount_rest],
      src.[variance_amount],
      src.[disclose_const_amount],
      src.[private_action],
      src.[reason],
      src.[match_ref],
      src.[compliance_id]
    );
    -- Update buffer table
    UPDATE b SET
        dt_update = @UpdateDate
    FROM [crs].[orders_log_buffer] AS b
    INNER JOIN #LockedList l ON l.[buffer_id] = b.[buffer_id]

    EXEC [audit].[sp_log_Finish] @LogID = @LogID, @RowCount = @RowCount

  COMMIT TRANSACTION
  
  IF @BufferHistoryMode = 1 AND NOT EXISTS (SELECT 1 FROM [crs].[orders_log_buffer] WHERE [is_error] = 1)
  BEGIN
      DELETE b
      FROM [crs].[orders_log_buffer] b
      INNER JOIN #LockedList t ON b.[buffer_id] = t.[buffer_id]
  END
   
  IF @BufferHistoryMode >= 2 AND NOT EXISTS (SELECT 1 FROM [crs].[orders_log_buffer] WHERE [is_error] = 1)
    DELETE b
    FROM [crs].[orders_log_buffer] b
    WHERE DATEDIFF(DD, @UpdateDate, [dt_update]) > @BufferHistoryDays

END TRY
BEGIN CATCH
  SET @ErrorMessage = ERROR_MESSAGE()
  IF XACT_STATE() <> 0 AND @@TRANCOUNT > 0 
    ROLLBACK TRANSACTION

  DECLARE @err_session_id bigint;
  SET @err_session_id = ISNULL(@SessionId, 0)
  INSERT [dbo].[session_log] ([session_id], [session_state_id], [error_message])
  SELECT
    [session_id] = @err_session_id,
    [session_state_id] = 3,
    [error_message] = 'Table [crs].[orders_log_buffer]. Error: ' + @ErrorMessage

  UPDATE b SET 
    [session_id] = @err_session_id,
    [is_error]   = 1,
    [dt_update]  = ISNULL(@UpdateDate, GetDate())
  FROM [crs].[orders_log_buffer] b
  INNER JOIN #LockedList l ON b.[buffer_id] = l.[buffer_id]
  WHERE [is_error] = 0

  EXEC [audit].[sp_log_Finish] @LogID = @LogID, @RowCount = @RowCount, @ErrorMessage = @ErrorMessage
  EXEC [audit].[sp_Print] @StrPrint = @ErrorMessage
  RETURN -1
END CATCH

END