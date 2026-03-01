CREATE TABLE [audit].[LogText] (
    [LogID]            BIGINT        IDENTITY (1, 1) NOT NULL,
    [ObjectId]         INT           NULL,
    [KeyField]         VARCHAR (128) NULL,
    [KeyValue]         BIGINT        NULL,
    [MessageCode]      VARCHAR (50)  NULL,
    [Message]          VARCHAR (MAX) NULL,
    [TransactionCount] INT           NULL,
    [DateCreate]       DATETIME2 (4) CONSTRAINT [DF_LogText_DateCreate] DEFAULT (getdate()) NOT NULL,
    [SysUserName]      VARCHAR (256) CONSTRAINT [DF_LogText_SysUserName] DEFAULT (original_login()) NOT NULL,
    [SysHostName]      VARCHAR (100) CONSTRAINT [DF_LogText_SysHostName] DEFAULT (host_name()) NOT NULL,
    [SysDbName]        VARCHAR (128) CONSTRAINT [DF_LogText_SysDbName] DEFAULT (db_name()) NOT NULL,
    [SysAppName]       VARCHAR (128) CONSTRAINT [DF_LogText_SysAppName] DEFAULT (app_name()) NOT NULL,
    [SPID]             INT           CONSTRAINT [DF_LogText_spid] DEFAULT (@@spid) NOT NULL,
    CONSTRAINT [PK_audit_LogText] PRIMARY KEY CLUSTERED ([LogID] ASC)
);

