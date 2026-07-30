CREATE TABLE [audit].[AuditTypeSP] (
    [AuditTypeID] int           NOT NULL,
    [Code]        varchar (50)  NOT NULL,
    [Description] varchar (256) NULL,
    CONSTRAINT [PK_AuditTypeSP] PRIMARY KEY CLUSTERED ([AuditTypeID] ASC)
);

