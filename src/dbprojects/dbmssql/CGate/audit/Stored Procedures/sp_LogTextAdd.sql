
/*
BEGIN TRAN
SELECT * FROM [Setting] 
SELECT [audit].[fn_GetAuditTypeLT]('FullAuditEnabled')
EXEC [audit].[sp_LogTextAdd]    @AuditEnable ='FullAuditEnabled', @Message ='test'
SELECT * FROM [audit].[LogText]

SELECT * FROM [dbo].[Setting]
SELECT [dbo].[fn_GetSettingValue]('FullAuditEnabled')
SELECT * FROM [audit].[LogProcedures]
ROLLBACk

*/

CREATE   PROCEDURE [audit].[sp_LogTextAdd]   
    @AuditEnable nvarchar(256) = NULL,
    @ObjectId        int           = NULL,
    @KeyField        varchar (128) = NULL,
    @KeyValue        bigint        = NULL,
    @MessageCode     varchar (50)  = NULL,
    @Message         varchar (MAX) = NULL
AS 
BEGIN
    SET NOCOUNT ON 
    DECLARE @AuditTypeID int
    SELECT @AuditTypeID = [audit].[fn_GetAuditTypeLT](@AuditEnable)
    
    IF @AuditTypeID is NULL
        RETURN 0
    
    DECLARE 
        @LogID       bigint,
        @DateCreate  datetime2(4)  = GetDate(),  
        @SysDbName   nvarchar(128) = DB_NAME(),
        @SysUserName varchar(256)  = original_login(),
        @SysHostName varchar(128)  = CAST(@@SERVERNAME as varchar(100)),
        @SysAppName  varchar(128)  = app_name()

    -- [Тип 1]: Локальная таблица
    IF @AuditTypeID = 1
        EXEC [audit].sp_LnkLtInsert
            @ObjectId         = @ObjectId,
            @KeyField         = @KeyField,
            @KeyValue         = @KeyValue,
            @MessageCode      = @MessageCode,
            @Message          = @Message,
            @TransactionCount = @@TRANCOUNT,
            @DateCreate       = @DateCreate,
            @SysDbName        = @SysDbName,
            @SysUserName      = @SysUserName,
            @SysHostName      = @SysHostName,
            @SysAppName       = @SysAppName,
            @SPID             = @@SPID

    -- [Тип 2]: Линкед сервер
    IF @AuditTypeID = 2
        --SNAPSHOT ISOLATION LEVEL Remote access is not supported for transaction isolation level "SNAPSHOT".
        EXEC [$(LinkSRVLog)].[$(DatabaseName)].[audit].sp_LnkLtInsert
            @ObjectId         = @ObjectId,
            @KeyField         = @KeyField,
            @KeyValue         = @KeyValue,
            @MessageCode      = @MessageCode,
            @Message          = @Message,
            @TransactionCount = @@TRANCOUNT,
            @DateCreate       = @DateCreate,
            @SysDbName        = @SysDbName,
            @SysUserName      = @SysUserName,
            @SysHostName      = @SysHostName,
            @SysAppName       = @SysAppName,
            @SPID             = @@SPID
    
    -- [Тип 3]: Очередь RabbitMQ
    IF @AuditTypeID = 3
        EXEC [audit].sp_RmqPostLt
            @ObjectId         = @ObjectId,
            @KeyField         = @KeyField,
            @KeyValue         = @KeyValue,
            @MessageCode      = @MessageCode,
            @Message          = @Message,
            @TransactionCount = @@TRANCOUNT,
            @DateCreate       = @DateCreate,
            @SysDbName        = @SysDbName,
            @SysUserName      = @SysUserName,
            @SysHostName      = @SysHostName,
            @SysAppName       = @SysAppName,
            @SPID             = @@SPID

    -- ?? [НОВЫЙ Тип 4]: In-Memory Таблица (Lock-Free RAM)
    IF @AuditTypeID = 4
        EXEC [audit].sp_InMemoryLtInsert
            @ObjectId         = @ObjectId, @KeyField = @KeyField, @KeyValue = @KeyValue,
            @MessageCode      = @MessageCode, @Message = @Message, @TransactionCount = @@TRANCOUNT,
            @DateCreate       = @DateCreate, @SysDbName = @SysDbName, @SysUserName = @SysUserName,
            @SysHostName      = @SysHostName, @SysAppName = @SysAppName, @SPID = @@SPID

    -- ?? [НОВЫЙ Тип 5]: Оптимизированный дисковый лог (Sequential Key)
    IF @AuditTypeID = 5
        EXEC [audit].sp_SeqDiskLtInsert
            @ObjectId         = @ObjectId, @KeyField = @KeyField, @KeyValue = @KeyValue,
            @MessageCode      = @MessageCode, @Message = @Message, @TransactionCount = @@TRANCOUNT,
            @DateCreate       = @DateCreate, @SysDbName = @SysDbName, @SysUserName = @SysUserName,
            @SysHostName      = @SysHostName, @SysAppName = @SysAppName, @SPID = @@SPID

END