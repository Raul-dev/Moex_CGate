CREATE TABLE [mq].[MessageQueue] (
    [BufferId]    BIGINT           IDENTITY (1, 1) NOT NULL,
    [SessionId]   BIGINT           NOT NULL,
    [MessageId]   UNIQUEIDENTIFIER NULL,
    [MessageBody] NVARCHAR (MAX)   NULL,
    [MessageKey]  NVARCHAR (256)   NULL,
    [CreatedAt]   DATETIME2 (4)    CONSTRAINT [DF_mq_MessageQueue_CreatedAt] DEFAULT (SYSDATETIME()) NOT NULL,
    CONSTRAINT [PK_mq_MessageQueue] PRIMARY KEY CLUSTERED ([BufferId] ASC)
);
