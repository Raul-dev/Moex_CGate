--не добавлен в накат, тоесть просто скрипт
USE [master];
GO
-- Включаем Service Broker на базе данных, если он выключен
IF (SELECT is_broker_enabled FROM sys.databases WHERE name = 'CGate') = 0
BEGIN
    -- ALTER DATABASE требует монопольного доступа, сбрасываем активные сессии
    ALTER DATABASE [CGate] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    ALTER DATABASE [CGate] SET ENABLE_BROKER;
    ALTER DATABASE [CGate] SET MULTI_USER;
END
GO

USE [CGate];
GO

-- 1. Создаем тип сообщения (будем передавать структурированный XML)
IF NOT EXISTS (SELECT * FROM sys.service_message_types WHERE name = '//CGate/Audit/LogMessage')
    CREATE MESSAGE TYPE [//CGate/Audit/LogMessage] VALIDATION = WELL_FORMED_XML;

-- 2. Создаем контракт
IF NOT EXISTS (SELECT * FROM sys.service_contracts WHERE name = '//CGate/Audit/LogContract')
    CREATE CONTRACT [//CGate/Audit/LogContract] ([//CGate/Audit/LogMessage] SENT BY INITIATOR);

-- 3. Создаем процедуру обработки (её код описан в Шаге 4), пока создаем пустую заглушку, если её нет
IF OBJECT_ID('[audit].[sp_ServiceBrokerLogQueueProcessor]', 'P') IS NULL
    EXEC('CREATE PROCEDURE [audit].[sp_ServiceBrokerLogQueueProcessor] AS BEGIN RETURN; END');
GO

-- 4. Создаем очередь с автоматической активацией фонового обработчика
IF NOT EXISTS (SELECT * FROM sys.service_queues WHERE name = 'LogQueue' AND schema_id = SCHEMA_ID('audit'))
BEGIN
    CREATE QUEUE [audit].[LogQueue]
    WITH STATUS = ON,
    ACTIVATION (
        PROCEDURE_NAME = [audit].[sp_ServiceBrokerLogQueueProcessor], -- Наша процедура чтения
        MAX_QUEUE_READERS = 5,                                       -- До 5 параллельных потоков чтения при росте нагрузки
        EXECUTE AS OWNER
    );
END
ELSE
BEGIN
    -- Если очередь была, просто обновляем настройки активации
    ALTER QUEUE [audit].[LogQueue]
    WITH ACTIVATION (
        PROCEDURE_NAME = [audit].[sp_ServiceBrokerLogQueueProcessor],
        MAX_QUEUE_READERS = 5,
        STATUS = ON
    );
END
GO

-- 5. Создаем службу (Service)
IF NOT EXISTS (SELECT * FROM sys.services WHERE name = '//CGate/Audit/LogService')
    CREATE SERVICE [//CGate/Audit/LogService] ON QUEUE [audit].[LogQueue] ([//CGate/Audit/LogContract]);
GO
