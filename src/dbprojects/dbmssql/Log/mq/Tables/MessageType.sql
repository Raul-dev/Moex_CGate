CREATE TABLE [mq].[MessageType] (
    [MessageTypeId] TINYINT       NOT NULL,
    [Name]          VARCHAR (100) NOT NULL,
    CONSTRAINT [PK_mq_MessageType] PRIMARY KEY CLUSTERED ([MessageTypeId] ASC)
);
