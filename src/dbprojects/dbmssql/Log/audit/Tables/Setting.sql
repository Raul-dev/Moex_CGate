CREATE TABLE [audit].[Setting] (
    [ID]       INT          NOT NULL,
    [IntValue] INT          NULL,
    [Code]     VARCHAR (50) NOT NULL,
    [StrValue] VARCHAR (50) NULL,
    CONSTRAINT [PK_Audit_Setting] PRIMARY KEY CLUSTERED ([ID] ASC)
);

