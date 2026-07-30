CREATE TABLE [mq].[Session] (
    [SessionId]       BIGINT         IDENTITY (1, 1) NOT NULL,
    [DataSourceId]    TINYINT        NOT NULL,
    [SessionStateId]  TINYINT        NOT NULL,
    [ErrorMessage]    VARCHAR (4000) NULL,
    [UpdatedAt]       DATETIME2 (4)  CONSTRAINT [DF_mq_Session_UpdatedAt] DEFAULT (SYSDATETIME()) NOT NULL,
    [CreatedAt]       DATETIME2 (4)  CONSTRAINT [DF_mq_Session_CreatedAt] DEFAULT (SYSDATETIME()) NOT NULL,
    CONSTRAINT [PK_mq_Session] PRIMARY KEY CLUSTERED ([SessionId] ASC),
    CONSTRAINT [FK_mq_Session_DataSource] FOREIGN KEY ([DataSourceId]) REFERENCES [mq].[DataSource] ([DataSourceId]),
    CONSTRAINT [FK_mq_Session_SessionState] FOREIGN KEY ([SessionStateId]) REFERENCES [mq].[SessionState] ([SessionStateId])
);
