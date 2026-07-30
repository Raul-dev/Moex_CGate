CREATE TABLE [audit].[LogProcedures] (
    [LogID]            bigint        IDENTITY (1, 1) NOT NULL,
    [MainID]           bigint        NULL,
    [ParentID]         bigint        NULL,
    [StartTime]        datetime2 (4) CONSTRAINT [DF_LogProcedures_start_datetime] DEFAULT (getdate()) NOT NULL,
    [EndTime]          datetime2 (4) NULL,
    [Duration]         int           NULL,
    [RowCount]         int           NULL,
    [SysUserName]      varchar (256) CONSTRAINT [DF_LogProcedures_SysUserName] DEFAULT (original_login()) NOT NULL,
    [SysHostName]      varchar (100) CONSTRAINT [DF_LogProcedures_SysHostName] DEFAULT (host_name()) NOT NULL,
    [SysDbName]        varchar (128) NOT NULL,
    [SysAppName]       varchar (128) CONSTRAINT [DF_LogProcedures_SysAppName] DEFAULT (app_name()) NOT NULL,
    [SPID]             int           CONSTRAINT [DF_LogProcedures_spid] DEFAULT (@@spid) NOT NULL,
    [ProcedureName]    varchar (512) NULL,
    [ProcedureParams]  varchar (MAX) NULL,
    [ProcedureInfo]    varchar (MAX) NULL,
    [ErrorMessage]     varchar (MAX) NULL,
    [TransactionCount] int           NULL,
    CONSTRAINT [PK_audit_LogProcedures] PRIMARY KEY CLUSTERED ([LogID] ASC)
);

