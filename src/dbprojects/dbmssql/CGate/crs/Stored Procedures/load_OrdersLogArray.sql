/*
SPECTRA 700
SELECT * FROM [crs].[OrdersLogBuffer]
SELECT * FROM [crs].[OrdersLog]
UPDATE [crs].[OrdersLogBuffer] SET [UpdatedAt] = '19000101'

EXEC [crs].[load_OrdersLogArray] @SessionId = 0, @Debug = 1

*/
CREATE   PROCEDURE [crs].[load_OrdersLogArray]
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
DECLARE @LogID int, @ProcedureName varchar(510), @ProcedureParams varchar(max), @ProcedureInfo varchar(max), @AuditEnable nvarchar(256)
SET @AuditEnable = [dbo].[fn_GetSettingValue]('FullAuditEnabled')
SET @ProcedureName = '[' + OBJECT_SCHEMA_NAME(@@PROCID)+'].['+OBJECT_NAME(@@PROCID)+']'
IF @AuditEnable IS NOT NULL 
BEGIN
  IF OBJECT_ID('tempdb..#LogProc') IS NULL
     SELECT * INTO #LogProc FROM [audit].[Template_LogProc]()
  
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
    [BufferId] bigint Primary key,
    [MessageId] uniqueidentifier,
    [RefId] bigint,
    [MessageTypeId] tinyint
)

CREATE TABLE #LockedListUniq(
    [BufferId] bigint Primary key,
    [RefId] bigint
)

BEGIN TRY
  BEGIN TRANSACTION

    IF @AuditEnable IS NOT NULL 
        EXEC [audit].[sp_LogStart] @AuditEnable = @AuditEnable, @ProcedureName = @ProcedureName, @ProcedureParams = @ProcedureParams, @LogID = @LogID OUTPUT

    INSERT INTO #LockedList
    SELECT TOP 500000 [BufferId], [MessageId], [RefId], [MessageTypeId]
    FROM [crs].[OrdersLogBuffer] b 
    WHERE b.[UpdatedAt] = @MinDate
    ORDER BY [BufferId]
    

    SET @RowCount = @@ROWCOUNT;
    IF @RowCount = 0 
    BEGIN
    
        EXEC [audit].[sp_LogFinish] @LogID = @LogID, @RowCount = 0, @ProcedureInfo = 'Empty buffer'
        COMMIT TRANSACTION
        RETURN 0
    END

    INSERT INTO #LockedListUniq
    SELECT [BufferId] = MAX([BufferId]), [RefId]
    FROM #LockedList l
    WHERE l.[MessageTypeId] = 1 
    GROUP BY [RefId]
    SET @RowCount = @@ROWCOUNT;
    
    MERGE INTO [crs].[OrdersLog] trg
    USING 
    (
      SELECT 
         [ReplId]
        ,[ReplRev]
        ,[ReplAct]
        ,[PublicOrderId]
        ,OL.[SessionId]
        ,[IsinId]
        ,[PublicAmount]
        ,[PublicAmountRest]
        ,[DealId]
        ,[XStatus]
        ,[XStatus2]
        ,[Price]
        ,[Moment] = CONVERT([datetime2](3), [Moment], 102)
        ,[MomentNs]
        ,[Direction]
        ,[PublicAction]
        ,[DealPrice]
        ,[ClientCode]
        ,[LoginFrom]
        ,[Comment]
        ,[ExternalId]
        ,[BrokerTo]
        ,[BrokerToRts]
        ,[BrokerFromRts]
        ,[ExpirationDate] = CONVERT([datetime2](3), [ExpirationDate], 102)
        ,[OrderId1]
        ,[AsPref]
        ,[PrivateOrderId]
        ,[PrivateAmount]
        ,[PrivateAmountRest]
        ,[VarianceAmount]
        ,[DiscloseConstAmount]
        ,[PrivateAction]
        ,[Reason]
        ,[MatchRef]
        ,[ComplianceId]
        ,[Edition]
      FROM #LockedListUniq L 
      INNER JOIN [crs].[OrdersLogBuffer] b ON b.[BufferId] = L.[BufferId]
      CROSS APPLY (
        SELECT *
        FROM OPENJSON('['+b.MessageBody+']','$')
        WITH 
        (
	        [ReplId] [bigint] '$[0]',
	        [ReplRev] [bigint] '$[1]',
	        [ReplAct] [bigint] '$[2]',
	        [PublicOrderId] [bigint] '$[3]',
	        [SessionId] [int] '$[4]',
	        [IsinId] [int] '$[5]',
	        [PublicAmount] [bigint] '$[6]',
	        [PublicAmountRest] [bigint] '$[7]',
	        [DealId] [bigint] '$[8]',
	        [XStatus] [bigint] '$[9]',
	        [XStatus2] [bigint] '$[10]',
	        [Price] [decimal](16, 5) '$[11]',
	        [Moment] varchar(50) '$[12]',
	        [MomentNs] [decimal](20, 0) '$[13]',
	        [Direction] [tinyint] '$[14]',
	        [PublicAction] [tinyint] '$[15]',
	        [DealPrice] [decimal](16, 5) '$[16]',
	        [ClientCode] [nvarchar](7) '$[17]',
	        [LoginFrom] [nvarchar](20) '$[18]',
	        [Comment] [nvarchar](20) '$[19]',
	        [ExternalId] [int] '$[20]',
	        [BrokerTo] [nvarchar](7) '$[21]',
	        [BrokerToRts] [nvarchar](7) '$[22]',
	        [BrokerFromRts] [nvarchar](7) '$[23]',
	        [ExpirationDate] varchar(50) '$[24]',
	        [OrderId1] [bigint] '$[25]',
	        [AsPref] [int] '$[26]',
	        [PrivateOrderId] [bigint] '$[27]',
            [PrivateAmount] [bigint] '$[28]',
            [PrivateAmountRest] [bigint] '$[29]',
            [VarianceAmount] [bigint] '$[30]',
            [DiscloseConstAmount] [bigint] '$[31]',
            [PrivateAction] [tinyint] '$[32]',
            [Reason] [int] '$[33]',
            [MatchRef] varchar(10) '$[34]',
            [ComplianceId] varchar(1) '$[35]'
        ) 
      ) OL
       
    ) AS src
    ON src.[PrivateOrderId] = trg.[PrivateOrderId] WHEN MATCHED THEN 
    UPDATE SET
      [ReplId] = src.[ReplId],
      [ReplRev] = src.[ReplRev],
      [ReplAct] = src.[ReplAct],
      [PublicOrderId] = src.[PublicOrderId],
      [SessionId] = src.[SessionId],
      [IsinId] = src.[IsinId],
      [PublicAmount] = src.[PublicAmount],
      [PublicAmountRest] = src.[PublicAmountRest],
      [DealId] = src.[DealId],
      [XStatus] = src.[XStatus],
      [XStatus2] = src.[XStatus2],
      [Price] = src.[Price],
      --,[Moment] = src.[Moment],
      [MomentNs] = src.[MomentNs],
      [Direction] = src.[Direction],
      [PublicAction] = src.[PublicAction],
      [DealPrice] = src.[DealPrice],
      [ClientCode] = src.[ClientCode],
      [LoginFrom] = src.[LoginFrom],
      [Comment] = src.[Comment],
      [ExternalId] = src.[ExternalId],
      [BrokerTo] = src.[BrokerTo],
      [BrokerToRts] = src.[BrokerToRts],
      [BrokerFromRts] = src.[BrokerFromRts],
      [ExpirationDate] = src.[ExpirationDate],
      [OrderId1] = src.[OrderId1],
      [AsPref] = src.[AsPref],
      [PrivateOrderId] = src.[PrivateOrderId],
      [PrivateAmount] = src.[PrivateAmount],
      [PrivateAmountRest] = src.[PrivateAmountRest],
      [VarianceAmount] = src.[VarianceAmount],
      [DiscloseConstAmount] = src.[DiscloseConstAmount],
      [PrivateAction] = src.[PrivateAction],
      [Reason] = src.[Reason],
      [MatchRef] = src.[MatchRef],
      [ComplianceId] = src.[ComplianceId]
    WHEN NOT MATCHED BY TARGET
    THEN INSERT (
        [ReplId]
      ,[ReplRev]
      ,[ReplAct]
      ,[PublicOrderId]
      ,[SessionId]
      ,[IsinId]
      ,[PublicAmount]
      ,[PublicAmountRest]
      ,[DealId]
      ,[XStatus]
      ,[XStatus2]
      ,[Price]
      ,[Moment]
      ,[MomentNs]
      ,[Direction]
      ,[PublicAction]
      ,[DealPrice]
      ,[ClientCode]
      ,[LoginFrom]
      ,[Comment]
      ,[ExternalId]
      ,[BrokerTo]
      ,[BrokerToRts]
      ,[BrokerFromRts]
      ,[ExpirationDate]
      ,[OrderId1]
      ,[AsPref]
      ,[PrivateOrderId]
      ,[PrivateAmount]
      ,[PrivateAmountRest]
      ,[VarianceAmount]
      ,[DiscloseConstAmount]
      ,[PrivateAction]
      ,[Reason]
      ,[MatchRef]
      ,[ComplianceId]
    )
    VALUES
    (
       src.[ReplId],
      src.[ReplRev],
      src.[ReplAct],
      src.[PublicOrderId],
      src.[SessionId],
      src.[IsinId],
      src.[PublicAmount],
      src.[PublicAmountRest],
      src.[DealId],
      src.[XStatus],
      src.[XStatus2],
      src.[Price],
      src.[Moment],
      src.[MomentNs],
      src.[Direction],
      src.[PublicAction],
      src.[DealPrice],
      src.[ClientCode],
      src.[LoginFrom],
      src.[Comment],
      src.[ExternalId],
      src.[BrokerTo],
      src.[BrokerToRts],
      src.[BrokerFromRts],
      src.[ExpirationDate],
      src.[OrderId1],
      src.[AsPref],
      src.[PrivateOrderId],
      src.[PrivateAmount],
      src.[PrivateAmountRest],
      src.[VarianceAmount],
      src.[DiscloseConstAmount],
      src.[PrivateAction],
      src.[Reason],
      src.[MatchRef],
      src.[ComplianceId]
    );
    -- Update buffer table
    UPDATE b SET
        [UpdatedAt] = @UpdateDate
    FROM [crs].[OrdersLogBuffer] AS b
    INNER JOIN #LockedList l ON l.[BufferId] = b.[BufferId]

    EXEC [audit].[sp_LogFinish] @LogID = @LogID, @RowCount = @RowCount

  COMMIT TRANSACTION
  
  IF @BufferHistoryMode = 1 AND NOT EXISTS (SELECT 1 FROM [crs].[OrdersLogBuffer] WHERE [IsError] = 1)
  BEGIN
      DELETE b
      FROM [crs].[OrdersLogBuffer] b
      INNER JOIN #LockedList t ON b.[BufferId] = t.[BufferId]
  END
   
  IF @BufferHistoryMode >= 2 AND NOT EXISTS (SELECT 1 FROM [crs].[OrdersLogBuffer] WHERE [IsError] = 1)
    DELETE b
    FROM [crs].[OrdersLogBuffer] b
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
    [ErrorMessage] = 'Table [crs].[OrdersLogBuffer]. Error: ' + @ErrorMessage

  UPDATE b SET 
    [SessionId] = @err_session_id,
    [IsError]   = 1,
    [UpdatedAt]  = ISNULL(@UpdateDate, GetDate())
  FROM [crs].[OrdersLogBuffer] b
  INNER JOIN #LockedList l ON b.[BufferId] = l.[BufferId]
  WHERE b.[IsError] = 0

  EXEC [audit].[sp_LogFinish] @LogID = @LogID, @RowCount = @RowCount, @ErrorMessage = @ErrorMessage
  EXEC [audit].[sp_Print] @StrPrint = @ErrorMessage
  RETURN -1
END CATCH

END