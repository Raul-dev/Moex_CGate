CREATE TABLE [audit].[SequentialDiskLogTable] (
    [LogID]            BIGINT         IDENTITY (1, 1) NOT NULL,
    [ObjectId]         INT            NULL,
    [KeyField]         VARCHAR (128)  NULL,
    [KeyValue]         BIGINT         NULL,
    [MessageCode]      VARCHAR (50)   NULL,
    [Message]          NVARCHAR (MAX) NULL,
    [TransactionCount] INT            NULL,
    [DateCreate]       DATETIME2 (4)  NOT NULL,
    [SysDbName]        NVARCHAR (128) NULL,
    [SysUserName]      VARCHAR (256)  NULL,
    [SysHostName]      VARCHAR (128)  NULL,
    [SysAppName]       VARCHAR (128)  NULL,
    [SPID]             INT            NULL,
    CONSTRAINT [PK_SequentialDiskLogTable] PRIMARY KEY CLUSTERED ([LogID] ASC) WITH (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON)
);

