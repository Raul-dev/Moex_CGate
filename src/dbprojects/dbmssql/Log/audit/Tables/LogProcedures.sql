CREATE TABLE [audit].[LogProcedures] (
    [LogID]            BIGINT        IDENTITY (1, 1) NOT NULL,
    [MainID]           BIGINT        NULL,
    [ParentID]         BIGINT        NULL,
    [StartTime]        DATETIME2 (4) CONSTRAINT [DF_LogProcedures_start_datetime] DEFAULT (getdate()) NOT NULL,
    [EndTime]          DATETIME2 (4) NULL,
    [Duration]         INT           NULL,
    [RowCount]         INT           NULL,
    [SysUserName]      VARCHAR (256) CONSTRAINT [DF_LogProcedures_SysUserName] DEFAULT (original_login()) NOT NULL,
    [SysHostName]      VARCHAR (100) CONSTRAINT [DF_LogProcedures_SysHostName] DEFAULT (host_name()) NOT NULL,
    [SysDbName]        VARCHAR (128) NOT NULL,
    [SysAppName]       VARCHAR (128) CONSTRAINT [DF_LogProcedures_SysAppName] DEFAULT (app_name()) NOT NULL,
    [SPID]             INT           CONSTRAINT [DF_LogProcedures_spid] DEFAULT (@@spid) NOT NULL,
    [ProcedureName]    VARCHAR (512) NULL,
    [ProcedureParams]  VARCHAR (MAX) NULL,
    [ProcedureInfo]    VARCHAR (MAX) NULL,
    [ErrorMessage]     VARCHAR (MAX) NULL,
    [TransactionCount] INT           NULL,
    CONSTRAINT [PK_audit_LogProcedures] PRIMARY KEY CLUSTERED ([LogID] ASC)
);

