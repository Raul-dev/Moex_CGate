CREATE   PROCEDURE [audit].[sp_InMemoryLtInsert]   
    @ObjectId         int           = NULL,
    @KeyField         varchar (128) = NULL,
    @KeyValue         bigint        = NULL,
    @MessageCode      varchar (50)  = NULL,
    @Message          varchar (MAX) = NULL,
    @TransactionCount int           = NULL,
    @DateCreate       datetime2(4)  = NULL,
    @SysDbName        nvarchar(128) = NULL,
    @SysUserName      varchar(256)  = NULL,
    @SysHostName      varchar(128)  = NULL,
    @SysAppName       varchar(128)  = NULL,
    @SPID             int           = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Вставка в вашу ин-мемори таблицу (которую мы создали ранее)
    INSERT INTO [audit].[InMemoryLogTable] (
        [ObjectId], [KeyField], [KeyValue], [MessageCode], [Message], 
        [TransactionCount], [DateCreate], [SysDbName], [SysUserName], [SysHostName], [SysAppName], [SPID]
    )
    VALUES (
        @ObjectId, @KeyField, @KeyValue, @MessageCode, @Message, 
        @TransactionCount, @DateCreate, @SysDbName, @SysUserName, @SysHostName, @SysAppName, @SPID
    );
END