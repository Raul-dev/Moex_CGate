CREATE TABLE [audit].[LogText] (
    [LogID]            bigint        IDENTITY (1, 1) NOT NULL,
    [ObjectId]         int           NULL,
    [KeyField]         varchar (128) NULL,
    [KeyValue]         bigint        NULL,
    [MessageCode]      varchar (50)  NULL,
    [Message]          varchar (MAX) NULL,
    [TransactionCount] int           NULL,
    [DateCreate]       datetime2 (4) CONSTRAINT [DF_LogText_DateCreate] DEFAULT (getdate()) NOT NULL,
    [SysUserName]      varchar (256) CONSTRAINT [DF_LogText_SysUserName] DEFAULT (original_login()) NOT NULL,
    [SysHostName]      varchar (100) CONSTRAINT [DF_LogText_SysHostName] DEFAULT (host_name()) NOT NULL,
    [SysDbName]        varchar (128) CONSTRAINT [DF_LogText_SysDbName] DEFAULT (db_name()) NOT NULL,
    [SysAppName]       varchar (128) CONSTRAINT [DF_LogText_SysAppName] DEFAULT (app_name()) NOT NULL,
    [SPID]             int           CONSTRAINT [DF_LogText_spid] DEFAULT (@@spid) NOT NULL,
    CONSTRAINT [PK_audit_LogText] PRIMARY KEY CLUSTERED ([LogID] ASC)
);

