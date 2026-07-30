CREATE TABLE [mq].[SessionState] (
    [SessionStateId] TINYINT       NOT NULL,
    [Name]           VARCHAR (100) NULL,
    CONSTRAINT [PK_mq_SessionState] PRIMARY KEY CLUSTERED ([SessionStateId] ASC)
);
