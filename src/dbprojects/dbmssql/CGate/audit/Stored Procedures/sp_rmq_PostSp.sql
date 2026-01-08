
CREATE     PROCEDURE [audit].[sp_rmq_PostSp](
  @MainID           bigint = NULL,
  @ParentID         bigint = NULL,
  @StartTime        datetime2(4),
  @SysUserName      varchar(256),
  @SysHostName      varchar(100),
  @SysDbName        varchar(128),
  @SysAppName       varchar(128),
  @SPID             int,
  @ProcedureName    varchar(512) ,
  @ProcedureParams  varchar(max),
  @TransactionCount int,
  @ProcedureInfo    varchar(max)  = NULL,
  @LogID            bigint OUTPUT
)
AS
BEGIN

    
    BEGIN TRY

    DECLARE @msg nvarchar(max) = '[[' +
    '"' + ISNULL(TRY_CAST(@MainID as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@ParentID as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@StartTime as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@SysUserName as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@SysHostName as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@SysDbName as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@SysAppName as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@SPID as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@ProcedureName as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@ProcedureParams as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@TransactionCount as varchar(50)),'NULL') + '",' + 
    '"' + ISNULL(TRY_CAST(@ProcedureInfo as varchar(50)),'NULL') + '"' +  
    ']]'

    
    EXEC rmq.sp_PostRabbitMsg @Message = @msg, @EndpointID = 1;
    SET @LogID = 1
  END TRY
  BEGIN CATCH
    DECLARE @errMsg nvarchar(max);
    DECLARE @errLine int;
    SELECT @errMsg = ERROR_MESSAGE(), @errLine = ERROR_LINE();
    RAISERROR('Error: %s at line: %d', 16, -1, @errMsg, @errLine);
  END CATCH
END;