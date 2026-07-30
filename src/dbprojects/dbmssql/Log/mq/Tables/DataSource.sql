CREATE TABLE [mq].[DataSource] (
    [DataSourceId] TINYINT       NOT NULL,
    [Name]         VARCHAR (100) COLLATE Cyrillic_General_CI_AS NULL,
    CONSTRAINT [PK_mq_DataSource] PRIMARY KEY CLUSTERED ([DataSourceId] ASC)
);
