CREATE TABLE [audit].[AuditTypeSP] (
    [AuditTypeID] INT           NOT NULL,
    [Code]        VARCHAR (50)  NOT NULL,
    [Description] VARCHAR (256) NULL,
    CONSTRAINT [PK_AuditTypeSP] PRIMARY KEY CLUSTERED ([AuditTypeID] ASC)
);

