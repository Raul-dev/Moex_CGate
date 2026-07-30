CREATE TABLE [mq].[SessionLog] (
    [SessionLogId]   BIGINT         IDENTITY (1, 1) NOT NULL,
    [SessionId]      BIGINT         NOT NULL,
    [SessionStateId] TINYINT        NOT NULL,
    [ErrorMessage]   VARCHAR (4000) NULL,
    [CreatedAt]      DATETIME2 (4)  CONSTRAINT [DF_mq_SessionLog_CreatedAt] DEFAULT (SYSDATETIME()) NOT NULL,
    CONSTRAINT [PK_mq_SessionLog] PRIMARY KEY CLUSTERED ([SessionLogId] ASC),
    CONSTRAINT [FK_mq_SessionLog_Session] FOREIGN KEY ([SessionId]) REFERENCES [mq].[Session] ([SessionId]),
    CONSTRAINT [FK_mq_SessionLog_SessionState] FOREIGN KEY ([SessionStateId]) REFERENCES [mq].[SessionState] ([SessionStateId])
);
