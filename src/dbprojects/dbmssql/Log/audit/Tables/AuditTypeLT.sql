CREATE TABLE [audit].[AuditTypeLT] (
    [AuditTypeID] INT           NOT NULL,
    [Code]        VARCHAR (50)  NOT NULL,
    [Description] VARCHAR (256) NULL,
    CONSTRAINT [PK_LTAuditTypeLT] PRIMARY KEY CLUSTERED ([AuditTypeID] ASC)
);

