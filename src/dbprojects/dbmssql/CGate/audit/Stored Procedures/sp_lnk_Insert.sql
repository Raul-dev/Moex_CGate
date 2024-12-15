
CREATE   PROCEDURE [audit].[sp_lnk_Insert](
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

  INSERT INTO [audit].LogProcedures (
    MainID,
    ParentID,
    StartTime,
    SysUserName,
    SysHostName,
    SysDbName,
    SysAppName,
    SPID,
    ProcedureName,
    ProcedureParams,
    TransactionCount,
    ProcedureInfo 
  ) VALUES (
    @MainID,
    @ParentID,
    @StartTime,
    @SysUserName,
    @SysHostName,
    @SysDbName,
    @SysAppName,
    @SPID,
    @ProcedureName,
    @ProcedureParams,
    @TransactionCount,
    @ProcedureInfo 
  )

  SET @LogID  = SCOPE_IDENTITY()
       
  IF @MainID IS NULL 
    UPDATE [audit].[LogProcedures]
      SET MainID   = @LogID
    WHERE LogID = @LogID
                
END;