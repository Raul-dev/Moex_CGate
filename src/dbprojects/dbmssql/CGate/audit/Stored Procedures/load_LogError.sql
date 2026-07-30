/*

*/
CREATE   PROCEDURE [audit].[load_LogError]
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
    [BufferId] bigint Primary key,
    [MessageId] uniqueidentifier,
    [MessageTypeId] tinyint
)


BEGIN TRY
  BEGIN TRANSACTION

    IF @AuditEnable IS NOT NULL
        EXEC [audit].[sp_LogStart] @AuditEnable = @AuditEnable, @ProcedureName = @ProcedureName, @ProcedureParams = @ProcedureParams, @LogID = @LogID OUTPUT

    IF ISNULL(@BufferId, 0) = 0
      INSERT INTO #LockedList
      SELECT TOP 200000 [BufferId], [MessageId], [MessageTypeId]
      FROM [audit].[LogErrorBuffer] b
      WHERE b.[UpdatedAt] = @MinDate
      ORDER BY [BufferId]
    ELSE
      INSERT INTO #LockedList
      SELECT TOP 200000 [BufferId], [MessageId], [MessageTypeId]
      FROM [audit].[LogErrorBuffer] b
      WHERE [BufferId] >= @BufferId
        AND b.[UpdatedAt] = @MinDate
      ORDER BY [BufferId]

    SET @RowCount = @@ROWCOUNT;
    IF @Debug = 1 BEGIN
      SELECT [@RowCount] = @RowCount, [@BufferId] = @BufferId
      SELECT '#LockedList', * FROM #LockedList
    END
    IF @RowCount = 0
    BEGIN

        EXEC [audit].[sp_LogFinish] @LogID = @LogID, @RowCount = 0, @ProcedureInfo = 'Empty buffer'
        COMMIT TRANSACTION
        RETURN 0
    END

    SET @BufferId = (SELECT MAX([BufferId]) FROM #LockedList)
    IF @Debug = 1 BEGIN
      SELECT [@BufferId] = @BufferId
    END

/*   Not implemented
    -- Update buffer table
    UPDATE b SET
        [UpdatedAt] = @UpdateDate
    FROM [audit].[LogErrorBuffer] AS b
    INNER JOIN #LockedList l ON l.[BufferId] = b.[BufferId]
*/
    EXEC [audit].[sp_LogFinish] @LogID = @LogID, @RowCount = @RowCount

  COMMIT TRANSACTION

  IF @BufferHistoryMode = 1 AND NOT EXISTS (SELECT 1 FROM [audit].[LogErrorBuffer] WHERE [IsError] = 1)
  BEGIN
      DELETE b
      FROM [audit].[LogErrorBuffer] b
      INNER JOIN #LockedList t ON b.[BufferId] = t.[BufferId]
  END

  IF @BufferHistoryMode >= 2 AND NOT EXISTS (SELECT 1 FROM [audit].[LogErrorBuffer] WHERE [IsError] = 1)
    DELETE b
    FROM [audit].[LogErrorBuffer] b
    WHERE DATEDIFF(DD, @UpdateDate, [UpdatedAt]) > @BufferHistoryDays

END TRY
BEGIN CATCH
  SET @ErrorMessage = ERROR_MESSAGE()
  IF XACT_STATE() <> 0 AND @@TRANCOUNT > 0
    ROLLBACK TRANSACTION

  DECLARE @err_session_id bigint;
  SET @err_session_id = ISNULL(@SessionId, 0)
  INSERT [mq].[SessionLog] ([SessionId], [SessionStateId], [ErrorMessage])
  SELECT
    [SessionId] = @err_session_id,
    [SessionStateId] = 3,
    [ErrorMessage] = 'Table [audit].[LogErrorBuffer]. Error: ' + @ErrorMessage

  UPDATE b SET
    [SessionId] = @err_session_id,
    [IsError]   = 1,
    [UpdatedAt]  = ISNULL(@UpdateDate, GetDate())
  FROM [audit].[LogErrorBuffer] b
  INNER JOIN #LockedList l ON b.[BufferId] = l.[BufferId]
  WHERE [IsError] = 0

  EXEC [audit].[sp_LogFinish] @LogID = @LogID, @RowCount = @RowCount, @ErrorMessage = @ErrorMessage
  EXEC [audit].[sp_Print] @StrPrint = @ErrorMessage
  RETURN -1
END CATCH

END
