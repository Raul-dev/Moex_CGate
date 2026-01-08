
CREATE     PROCEDURE [audit].[sp_rmq_PostLT](
    @ObjectId        int           = NULL,
    @KeyField        varchar (128) = NULL,
    @KeyValue        bigint        = NULL,
    @MessageCode     varchar (50)  = NULL,
    @Message         varchar (MAX) = NULL,
    @TransactionCount int          = NULL,
    @DateCreate      datetime2(4)  = NULL,
    @SysDbName       nvarchar(128) = NULL,
    @SysUserName     varchar(256)  = NULL,
    @SysHostName     varchar(128)  = NULL,
    @SysAppName      varchar(128)  = NULL,
    @SPID            smallint      = NULL
    
)
AS
BEGIN

    
    BEGIN TRY

    DECLARE @msg nvarchar(max) = '[[' +
    '"' + ISNULL(TRY_CAST(@ObjectId as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@KeyField as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@KeyValue as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@MessageCode as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@Message as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@TransactionCount as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@DateCreate as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@SysDbName as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@SysUserName as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@SysHostName as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@SysAppName as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@SPID as varchar(50)),'NULL') + '"' +  
    ']]'

    
    EXEC rmq.sp_PostRabbitMsg @Message = @msg, @EndpointID = 2;
    
  END TRY
  BEGIN CATCH
    DECLARE @errMsg nvarchar(max);
    DECLARE @errLine int;
    SELECT @errMsg = ERROR_MESSAGE(), @errLine = ERROR_LINE();
    RAISERROR('Error: %s at line: %d', 16, -1, @errMsg, @errLine);
  END CATCH
END;