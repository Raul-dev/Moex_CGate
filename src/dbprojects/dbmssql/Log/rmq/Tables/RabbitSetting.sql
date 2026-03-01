CREATE TABLE [rmq].[RabbitSetting] (
    [SettingID]          INT             NOT NULL,
    [SettingIntValue]    INT             NULL,
    [SettingStringValue] NVARCHAR (4000) NULL,
    CONSTRAINT [pk_RabbitSetting] PRIMARY KEY CLUSTERED ([SettingID] ASC)
);

