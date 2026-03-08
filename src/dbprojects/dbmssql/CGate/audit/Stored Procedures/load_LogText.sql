/*
*/
CREATE     PROCEDURE [audit].[load_LogText]
  @SessionId         bigint         = NULL,
  @BufferHistoryMode tinyint        = 0,   -- 0 - Do not delete the buffering history.
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
DECLARE @LogID int, @ProcedureName varchar(510), @ProcedureParams varchar(max), @ProcedureInfo varchar(max), @AuditEnable nvarchar(256)
SET @AuditEnable = [dbo].[fn_GetSettingValue]('FullAuditEnabled')
SET @ProcedureName = '[' + OBJECT_SCHEMA_NAME(@@PROCID)+'].['+OBJECT_NAME(@@PROCID)+']'
IF @AuditEnable IS NOT NULL 
BEGIN
  IF OBJECT_ID('tempdb..#LogProc') IS NULL
     SELECT * INTO #LogProc FROM [audit].[Template_LogProc]()
  
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

BEGIN TRY
  BEGIN TRANSACTION

    IF @AuditEnable IS NOT NULL 
        EXEC [audit].[sp_log_Start] @AuditEnable = @AuditEnable, @ProcedureName = @ProcedureName, @ProcedureParams = @ProcedureParams, @LogID = @LogID OUTPUT

    IF ISNULL(@BufferId, 0) = 0
      INSERT INTO #LockedList
      SELECT TOP 200000 [buffer_id], [msg_id], [msgtype_id]
      FROM [audit].[LogText_buffer] b 
      WHERE b.[dt_update] = @MinDate
      ORDER BY [buffer_id]
    ELSE
      INSERT INTO #LockedList
      SELECT TOP 200000 [buffer_id], [msg_id], [msgtype_id]
      FROM [audit].[LogText_buffer] b 
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

    INSERT [audit].[LogText](
        [ObjectId],
        [KeyField],
        [KeyValue],
        [MessageCode],
        [Message],
        [TransactionCount],
        [DateCreate],
        [SysDbName],
        [SysUserName],
        [SysHostName],
        [SysAppName],
        [SPID] 
    )
    SELECT 
        [ObjectId]      = TRY_CAST(ObjectId AS int),
        [KeyField]      = NULLIF([KeyField],'NULL'),
        [KeyValue]      = NULLIF([KeyValue],'NULL'),
        [MessageCode]   = NULLIF([MessageCode],'NULL'),
        [Message]       = NULLIF([Message],'NULL'),
        [TransactionCount] = TRY_CAST([TransactionCount] AS int),
        [DateCreate]    = [DateCreate],
        [SysDbName]     = NULLIF([SysDbName],'NULL'),
        [SysUserName]   = NULLIF([SysUserName],'NULL'),
        [SysHostName]   = NULLIF([SysHostName],'NULL'),
        [SysAppName]    = NULLIF([SysAppName],'NULL'),
        [SPID]          = TRY_CAST([SPID] AS int)
    FROM [audit].[LogText_buffer] b
    OUTER APPLY OPENJSON(b.msg,'$') 
    WITH 
    (
        [ObjectId]     varchar(50)   '$[0]',
        [KeyField]     varchar(128)  '$[1]',
        [KeyValue]     varchar(128)  '$[2]',
        [MessageCode]  varchar(50)   '$[3]',
        [Message]      varchar(50)   '$[4]',
        [TransactionCount] varchar(50)  '$[5]',
        [DateCreate]   datetime2(4)  '$[6]',
        [SysDbName]    varchar(128)  '$[7]',
        [SysUserName]  varchar(256)  '$[8]',
        [SysHostName]  varchar(128)  '$[9]',
        [SysAppName]   varchar(128)  '$[10]',
        [SPID]         varchar(50)   '$[11]'
    ) M

    SET @BufferId = (SELECT MAX([buffer_id]) FROM #LockedList)
    IF @Debug = 1 BEGIN
        SELECT 
            [ObjectId]      = TRY_CAST(ObjectId AS int),
            [KeyField]      = NULLIF([KeyField],'NULL'),
            [KeyValue]      = NULLIF([KeyValue],'NULL'),
            [MessageCode]   = NULLIF([MessageCode],'NULL'),
            [Message]       = NULLIF([Message],'NULL'),
            [TransactionCount] = TRY_CAST([TransactionCount] AS int),
            [DateCreate]    = [DateCreate],
            [SysDbName]     = NULLIF([SysDbName],'NULL'),
            [SysUserName]   = NULLIF([SysUserName],'NULL'),
            [SysHostName]   = NULLIF([SysHostName],'NULL'),
            [SysAppName]    = NULLIF([SysAppName],'NULL'),
            [SPID]          = TRY_CAST([SPID] AS int)
        FROM [audit].[LogText_buffer] b
        OUTER APPLY OPENJSON(b.msg,'$') 
        WITH 
        (
            [ObjectId]     varchar(50)   '$[0]',
            [KeyField]     varchar(128)  '$[1]',
            [KeyValue]     varchar(128)  '$[2]',
            [MessageCode]  varchar(50)   '$[3]',
            [Message]      varchar(50)   '$[4]',
            [TransactionCount] varchar(50)  '$[5]',
            [DateCreate]   datetime2(4)  '$[6]',
            [SysDbName]    varchar(128)  '$[7]',
            [SysUserName]  varchar(256)  '$[8]',
            [SysHostName]  varchar(128)  '$[9]',
            [SysAppName]   varchar(128)  '$[10]',
            [SPID]         varchar(50)   '$[11]'
        ) M

        SELECT [@BufferId] = @BufferId
    END    
    
    -- Update buffer table
    UPDATE b SET
        [dt_update] = @UpdateDate
    FROM [audit].[LogText_buffer] AS b
    INNER JOIN #LockedList l ON l.[buffer_id] = b.[buffer_id]

    EXEC [audit].[sp_log_Finish] @LogID = @LogID, @RowCount = @RowCount

  COMMIT TRANSACTION
  
  IF @BufferHistoryMode = 1 AND NOT EXISTS (SELECT 1 FROM [audit].[LogText_buffer] WHERE [is_error] = 1)
  BEGIN
      DELETE b
      FROM [audit].[LogText_buffer] b
      INNER JOIN #LockedList t ON b.[buffer_id] = t.[buffer_id]
  END
   
  IF @BufferHistoryMode >= 2 AND NOT EXISTS (SELECT 1 FROM [audit].[LogText_buffer] WHERE [is_error] = 1)
    DELETE b
    FROM [audit].[LogText_buffer] b
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
    [error_message] = 'Table [audit].[LogText_buffer]. Error: ' + @ErrorMessage

  UPDATE b SET 
    [session_id] = @err_session_id,
    [is_error]   = 1,
    [dt_update]  = ISNULL(@UpdateDate, GetDate())
  FROM [audit].[LogText_buffer] b
  INNER JOIN #LockedList l ON b.[buffer_id] = l.[buffer_id]
  WHERE [is_error] = 0

  EXEC [audit].[sp_log_Finish] @LogID = @LogID, @RowCount = @RowCount, @ErrorMessage = @ErrorMessage
  EXEC [audit].[sp_Print] @StrPrint = @ErrorMessage
  RETURN -1
END CATCH

END