CREATE TABLE [dbo].[Setting] (
    [SettingID] VARCHAR (50)   NOT NULL,
    [StrValue]  NVARCHAR (256) NULL,
    CONSTRAINT [PK_Setting] PRIMARY KEY NONCLUSTERED ([SettingID] ASC)
);

