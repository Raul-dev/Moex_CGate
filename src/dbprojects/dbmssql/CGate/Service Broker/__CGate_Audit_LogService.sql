CREATE SERVICE [//CGate/Audit/LogService]
    AUTHORIZATION [dbo]
    ON QUEUE [audit].[LogQueue]
    ([//CGate/Audit/LogContract]);

