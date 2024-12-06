CREATE TABLE [dbo].[metamap] (
    [metamap_id]     SMALLINT       NOT NULL,
    [msg_key]        NVARCHAR (256) NOT NULL,
    [table_name]     NVARCHAR (128) NOT NULL,
    [metaadapter_id] TINYINT        NULL,
    [namespace]      NVARCHAR (256) NULL,
    [namespace_ver]  NVARCHAR (256) NULL,
    [etl_query]      NVARCHAR (256) NULL,
    [import_query]   NVARCHAR (256) NULL,
    [is_enable]      BIT            NOT NULL,
    CONSTRAINT [PK_metamap] PRIMARY KEY CLUSTERED ([metamap_id] ASC)
);

