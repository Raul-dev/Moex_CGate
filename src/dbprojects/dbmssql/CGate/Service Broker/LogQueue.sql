CREATE QUEUE [audit].[LogQueue]
    WITH ACTIVATION (STATUS = ON, PROCEDURE_NAME = [audit].[sp_ServiceBrokerLogQueueProcessor], MAX_QUEUE_READERS = 5, EXECUTE AS OWNER);

