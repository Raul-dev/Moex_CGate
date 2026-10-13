CREATE PROCEDURE [audit].[sp_ServiceBrokerLogQueueProcessor]
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @TargetDlgHandle UNIQUEIDENTIFIER;
    DECLARE @MessageBody XML;
    DECLARE @MessageType NVARCHAR(256);

    -- Буферная таблица в памяти для накопления пачки логов за один проход цикла
    DECLARE @LogBatch TABLE (
        [ObjectId] INT, [KeyField] VARCHAR(128), [KeyValue] BIGINT, [MessageCode] VARCHAR(50), [Message] VARCHAR(MAX),
        [TransactionCount] INT, [DateCreate] DATETIME2(4), [SysUserName] VARCHAR(256), [SysHostName] VARCHAR(100),
        [SysDbName] VARCHAR(128), [SysAppName] VARCHAR(128), [SPID] INT
    );

    WHILE (1=1)
    BEGIN
        BEGIN TRAN;

        -- Вычитываем сообщение из очереди с таймаутом ожидания 1 секунда
        WAITFOR (
            RECEIVE TOP(1)
                @TargetDlgHandle = conversation_handle,
                @MessageBody = CAST(message_body AS XML),
                @MessageType = message_type_name
            FROM [audit].[LogQueue]
        ), TIMEOUT 1000;

        -- Если сообщений больше нет — завершаем транзакцию и выходим
        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRAN;
            BREAK;
        END

        -- Если пришло наше ожидаемое XML сообщение
        IF @MessageType = '//CGate/Audit/LogMessage'
        BEGIN
            -- Парсим XML и складываем в буферную пачку
            INSERT INTO @LogBatch
            SELECT 
                X.item.value('(ObjectId)[1]', 'INT'),
                X.item.value('(KeyField)[1]', 'VARCHAR(128)'),
                X.item.value('(KeyValue)[1]', 'BIGINT'),
                X.item.value('(MessageCode)[1]', 'VARCHAR(50)'),
                X.item.value('(Message)[1]', 'VARCHAR(MAX)'),
                X.item.value('(TransactionCount)[1]', 'INT'),
                X.item.value('(DateCreate)[1]', 'DATETIME2(4)'),
                X.item.value('(SysUserName)[1]', 'VARCHAR(256)'),
                X.item.value('(SysHostName)[1]', 'VARCHAR(100)'),
                X.item.value('(SysDbName)[1]', 'VARCHAR(128)'),
                X.item.value('(SysAppName)[1]', 'VARCHAR(128)'),
                X.item.value('(SPID)[1]', 'INT')
            FROM @MessageBody.nodes('/Log') AS X(item);

            -- Закрываем диалог со стороны получателя, так как мы работаем в режиме Fire-and-Forget
            END CONVERSATION @TargetDlgHandle;
        END
        -- Обработка системных сообщений об ошибках или завершении диалога
        ELSE IF @MessageType = 'http://microsoft.com' 
             OR @MessageType = 'http://microsoft.com'
        BEGIN
            END CONVERSATION @TargetDlgHandle;
        END

        -- Если накопились логи — сбрасываем их одним быстрым INSERT в основную таблицу логов
        IF EXISTS(SELECT 1 FROM @LogBatch)
        BEGIN
            INSERT INTO [audit].[LogText] (
                [ObjectId], [KeyField], [KeyValue], [MessageCode], [Message], 
                [TransactionCount], [DateCreate], [SysUserName], [SysHostName], [SysDbName], [SysAppName], [SPID]
            )
            SELECT * FROM @LogBatch;
            
            DELETE FROM @LogBatch; -- Очищаем буфер для следующей итерации
        END

        COMMIT TRAN;
    END
END