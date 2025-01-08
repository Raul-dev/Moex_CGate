/*
SPECTRA 730
SELECT * FROM [crs].[orders_log_buffer] WHERE [dt_update] = '19000101'
SELECT count(*) FROM [crs].[orders_log]
UPDATE [crs].[orders_log_buffer] SET [dt_update] = '19000101'
DECLARE @RowCount bigint, @BufferId bigint
EXEC [crs].[load_orders_log] @SessionId = 0, @RowCount = @RowCount OUTPUT, @BufferId = @BufferId OUTPUT, @Debug = 0
SELECT @RowCount, @BufferId
SELECT @@TRANCOUNT
*/
CREATE   PROCEDURE [crs].[load_orders_log]
  @SessionId         bigint         = NULL,
  @BufferHistoryMode tinyint        = 0,  -- 0 - Do not delete the buffering history.
                                           -- 1 - Delete the buffering history.
                                           -- 2 - Keep the buffering history for 1 days.
                                           -- 3 - Keep the buffering history for a days from setup.
  @RowCount          int            = NULL OUTPUT,
  @BufferId          bigint         = NULL OUTPUT,
  @ErrorMessage      varchar(4000)  = NULL OUTPUT,
  @Debug             bit            = 0
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
    '@SessionId=' + ISNULL(LTRIM(STR(@SessionId, 30)),'NULL') + ', ' +
    '@BufferHistoryMode=' + ISNULL(LTRIM(STR(@BufferHistoryMode, 30)),'NULL') + ', ' +
    '@BufferId=' + ISNULL(LTRIM(STR(@BufferId, 30)),'NULL') 
END
SET XACT_ABORT OFF


SET TRANSACTION ISOLATION LEVEL SNAPSHOT
--SNAPSHOT работает в 2 раза быстрее
--SET TRANSACTION ISOLATION LEVEL READ COMMITTED
--SET DEADLOCK_PRIORITY LOW
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
    [msgtype_id] tinyint
)

CREATE TABLE #orders_log(
    [replID]                BIGINT          NULL,
    [replRev]               BIGINT          NULL,
    [replAct]               BIGINT          NULL,
    [public_order_id]       BIGINT          NULL,
    [sess_id]               INT             NULL,
    [isin_id]               INT             NULL,
    [public_amount]         BIGINT          NULL,
    [public_amount_rest]    BIGINT          NULL,
    [id_deal]               BIGINT          NULL,
    [xstatus]               BIGINT          NULL,
    [xstatus2]              BIGINT          NULL,
    [price]                 DECIMAL (16, 5) NULL,
    [moment]                DATETIME2 (3)   NULL,
    [moment_ns]             DECIMAL (20)    NULL,
    [dir]                   TINYINT         NULL,
    [public_action]         TINYINT         NULL,
    [deal_price]            DECIMAL (16, 5) NULL,
    [client_code]           NVARCHAR (7)    NULL,
    [login_from]            NVARCHAR (20)   NULL,
    [comment]               NVARCHAR (20)   NULL,
    [ext_id]                INT             NULL,
    [broker_to]             NVARCHAR (7)    NULL,
    [broker_to_rts]         NVARCHAR (7)    NULL,
    [broker_from_rts]       NVARCHAR (7)    NULL,
    [date_exp]              DATETIME2 (3)   NULL,
    [id_ord1]               BIGINT          NULL,
    [aspref]                INT             NULL,
    [private_order_id]      BIGINT          NOT NULL,
    [private_amount]        BIGINT          NULL,
    [private_amount_rest]   BIGINT          NULL,
    [variance_amount]       BIGINT          NULL,
    [disclose_const_amount] BIGINT          NULL,
    [private_action]        TINYINT         NULL,
    [reason]                INT             NULL,
    [match_ref]             NVARCHAR (10)   NULL,
    [compliance_id]         NVARCHAR (1)    NULL,
    [edition] int
PRIMARY KEY CLUSTERED 
(
	[private_order_id] ASC
)
)

BEGIN TRY
  BEGIN TRANSACTION

    IF @AuditProcEnable IS NOT NULL 
        EXEC [audit].[sp_log_Start] @AuditProcEnable = @AuditProcEnable, @ProcedureName = @ProcedureName, @ProcedureParams = @ProcedureParams, @LogID = @LogID OUTPUT

    IF ISNULL(@BufferId, 0) = 0
      INSERT INTO #LockedList
      SELECT TOP 200000 [buffer_id], [msg_id], [msgtype_id]
      FROM [crs].[orders_log_buffer] b 
      WHERE b.[dt_update] = @MinDate
      ORDER BY [buffer_id]
    ELSE
      INSERT INTO #LockedList
      SELECT TOP 200000 [buffer_id], [msg_id], [msgtype_id]
      FROM [crs].[orders_log_buffer] b 
      WHERE [buffer_id] >= @BufferId
        AND b.[dt_update] = @MinDate
      ORDER BY [buffer_id]

    SET @RowCount = @@ROWCOUNT;
    IF @Debug = 1 BEGIN
      SELECT [@RowCount] = @RowCount, [@BufferId] = @BufferId
      SELECT '#LockedList', * FROM #LockedList
    END
    IF @RowCount = 0 
    BEGIN
    
        EXEC [audit].[sp_log_Finish] @LogID = @LogID, @RowCount = 0, @ProcedureInfo = 'Empty buffer'
        COMMIT TRANSACTION
        RETURN 0
    END


    INSERT #orders_log (
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
      ,[edition]
    )

    SELECT *
    FROM (
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
          ,[edition] = ROW_NUMBER() OVER (PARTITION BY [private_order_id] ORDER BY [replRev] DESC)
        FROM #LockedList L 
        INNER JOIN [crs].[orders_log_buffer] b ON b.[buffer_id] = L.[buffer_id]
        CROSS APPLY (
          SELECT *
          FROM OPENJSON(b.msg,'$')
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
      ) H
      WHERE [edition] = 1

    MERGE INTO [crs].[orders_log] trg
    USING #orders_log AS src
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
    
    SET @BufferId = (SELECT MAX([buffer_id]) FROM #LockedList)
    IF @Debug = 1 BEGIN
      SELECT [@BufferId] = @BufferId
    END    
    
    -- Update buffer table
    UPDATE b SET
        [dt_update] = @UpdateDate
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