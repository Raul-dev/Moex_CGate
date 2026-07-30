CREATE TABLE [mq].[MetaMap] (
    [MetaMapId]        SMALLINT       NOT NULL,
    [MessageKey]       NVARCHAR (256) NOT NULL,
    [TableName]        NVARCHAR (128) NOT NULL,
    [MetaAdapterId]    TINYINT        NULL,
    [Namespace]        NVARCHAR (256) NULL,
    [NamespaceVersion] NVARCHAR (256) NULL,
    [EtlProcedure]     NVARCHAR (256) NULL,
    [ImportQuery]      NVARCHAR (256) NULL,
    [IsEnabled]        BIT            NOT NULL,
    CONSTRAINT [PK_mq_MetaMap] PRIMARY KEY CLUSTERED ([MetaMapId] ASC)
);
