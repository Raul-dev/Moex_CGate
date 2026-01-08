CREATE TABLE [audit].[LogText] (
    [LogID]            BIGINT        IDENTITY (1, 1) NOT NULL,
    [ObjectId]         int           NULL,
    [KeyField]         VARCHAR (128) NULL,
    [KeyValue]         bigint  NULL,
    [MessageCode]      VARCHAR (50) NULL,
    [Message]          VARCHAR (MAX) NULL,
    [TransactionCount] int       NULL,
    [DateCreate]       DATETIME2 (4) CONSTRAINT [DF_LogText_DateCreate] DEFAULT (getdate()) NOT NULL,
    [SysUserName]      VARCHAR (256) CONSTRAINT [DF_LogText_SysUserName] DEFAULT (original_login()) NOT NULL,
    [SysHostName]      VARCHAR (100) CONSTRAINT [DF_LogText_SysHostName] DEFAULT (host_name()) NOT NULL,
    [SysDbName]        VARCHAR (128) CONSTRAINT [DF_LogText_SysDbName] DEFAULT  (DB_NAME()) NOT NULL,
    [SysAppName]       VARCHAR (128) CONSTRAINT [DF_LogText_SysAppName] DEFAULT (app_name()) NOT NULL,
    [SPID]             INT           CONSTRAINT [DF_LogText_spid] DEFAULT (@@spid) NOT NULL,
    CONSTRAINT [PK_audit_LogText] PRIMARY KEY CLUSTERED ([LogID] ASC)
);

