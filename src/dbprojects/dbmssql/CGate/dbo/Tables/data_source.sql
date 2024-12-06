CREATE TABLE [dbo].[data_source] (
    [data_source_id] TINYINT       NOT NULL,
    [name]           VARCHAR (100) COLLATE Cyrillic_General_CI_AS NULL,
    CONSTRAINT [PK_data_source] PRIMARY KEY CLUSTERED ([data_source_id] ASC)
);
