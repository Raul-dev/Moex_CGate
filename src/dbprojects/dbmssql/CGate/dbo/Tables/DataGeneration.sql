CREATE TABLE [dbo].[DataGeneration] (
    [Id]             INT           IDENTITY (1, 1) NOT NULL,
    [column_id]      INT           NOT NULL,
    [object_id]      INT           NOT NULL,
    [system_type_id] TINYINT       NOT NULL,
    [max_length]     SMALLINT      NOT NULL,
    [Range]          VARCHAR (200) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);

