CREATE TABLE [audit].[LogError] (
    [LogID]            BIGINT        IDENTITY (1, 1) NOT NULL,
    [ObjectId]         INT           NULL,
    [Message]          VARCHAR (MAX) NULL,
    [TransactionCount] INT           NULL,
    [DateCreate]       DATETIME2 (4) CONSTRAINT [DF_LogError_DateCreate] DEFAULT (getdate()) NOT NULL,
    [SysUserName]      VARCHAR (256) CONSTRAINT [DF_LogError_SysUserName] DEFAULT (original_login()) NOT NULL,
    [SysHostName]      VARCHAR (100) CONSTRAINT [DF_LogError_SysHostName] DEFAULT (host_name()) NOT NULL,
    [SysDbName]        VARCHAR (128) CONSTRAINT [DF_LogError_SysDbName] DEFAULT (db_name()) NOT NULL,
    [SysAppName]       VARCHAR (128) CONSTRAINT [DF_LogError_SysAppName] DEFAULT (app_name()) NOT NULL,
    [SPID]             INT           CONSTRAINT [DF_LogError_spid] DEFAULT (@@spid) NOT NULL,
    CONSTRAINT [PK_audit_LogError] PRIMARY KEY CLUSTERED ([LogID] ASC)
);

