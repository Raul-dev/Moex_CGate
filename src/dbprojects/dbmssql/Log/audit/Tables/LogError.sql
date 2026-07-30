CREATE TABLE [audit].[LogError] (
    [LogID]            bigint        IDENTITY (1, 1) NOT NULL,
    [ObjectId]         int           NULL,
    [Message]          varchar (MAX) NULL,
    [TransactionCount] int           NULL,
    [DateCreate]       datetime2 (4) CONSTRAINT [DF_LogError_DateCreate] DEFAULT (getdate()) NOT NULL,
    [SysUserName]      varchar (256) CONSTRAINT [DF_LogError_SysUserName] DEFAULT (original_login()) NOT NULL,
    [SysHostName]      varchar (100) CONSTRAINT [DF_LogError_SysHostName] DEFAULT (host_name()) NOT NULL,
    [SysDbName]        varchar (128) CONSTRAINT [DF_LogError_SysDbName] DEFAULT (db_name()) NOT NULL,
    [SysAppName]       varchar (128) CONSTRAINT [DF_LogError_SysAppName] DEFAULT (app_name()) NOT NULL,
    [SPID]             int           CONSTRAINT [DF_LogError_spid] DEFAULT (@@spid) NOT NULL,
    CONSTRAINT [PK_audit_LogError] PRIMARY KEY CLUSTERED ([LogID] ASC)
);

