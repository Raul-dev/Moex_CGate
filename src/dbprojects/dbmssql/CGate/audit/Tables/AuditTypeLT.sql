CREATE TABLE [audit].[AuditTypeLT] (
    [AuditTypeID] int           NOT NULL,
    [Code]        varchar (50)  NOT NULL,
    [Description] varchar (256) NULL,
    CONSTRAINT [PK_AuditTypeLT] PRIMARY KEY CLUSTERED ([AuditTypeID] ASC)
);

