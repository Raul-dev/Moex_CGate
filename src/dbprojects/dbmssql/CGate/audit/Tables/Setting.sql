CREATE TABLE [audit].[Setting] (
    [ID]       int          NOT NULL,
    [IntValue] int          NULL,
    [Code]     varchar (50) NOT NULL,
    [StrValue] varchar (50) NULL,
    CONSTRAINT [PK_Audit_Setting] PRIMARY KEY CLUSTERED ([ID] ASC)
);

