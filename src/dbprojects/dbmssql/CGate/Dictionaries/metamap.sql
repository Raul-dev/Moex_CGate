IF NOT EXISTS(SELECT 1 FROM [dbo].[metaadapter] )
BEGIN
    INSERT INTO [dbo].[metaadapter] ([metaadapter_id], [name]) 
    SELECT 1, N'CGateJson'
    UNION ALL SELECT 5, N'UnknownJsonXml'
END
DECLARE @metamap TABLE
(
    [metamap_id]            smallint       NOT NULL,
    [msg_key]               nvarchar(256)  NOT NULL,
    [table_name]            nvarchar(128)  NOT NULL,
    [metaadapter_id]        tinyint        NULL,
    [namespace]             nvarchar (256) NULL,
    [namespace_ver]         nvarchar (256) NULL,
    [etl_query]             nvarchar (256) NULL,
    [import_query]          nvarchar (256) NULL,
    [is_enable]             bit            NULL
)
INSERT @metamap ([metamap_id], [msg_key], [table_name], [metaadapter_id], [namespace], [namespace_ver], [etl_query], [import_query], [is_enable])
VALUES
(1, N'Unknown', 'msgqueue', 5, CAST(N'https://nevadwh.ru/CatalogObject.Unknown' AS varchar(255)), CAST('https://nevadwh.ru/CatalogObject.Unknown/version1' AS varchar(255)), NULL, NULL, 1),
(2, N'crs.orders_log', N'[crs].[orders_log_buffer]', 1, N'crs.orders_log', N'crs.orders_log/version2.17', N'[crs].[load_orders_log]', NULL, 1)

IF EXISTS ( 
    SELECT 1 FROM [dbo].[metamap] d 
    LEFT OUTER JOIN @metamap s ON s.[metamap_id] = d.[metamap_id]
    WHERE s.[metamap_id] IS NULL) THROW 60000, N'The table [dbo].[metamap] was change.', 1;



MERGE INTO [dbo].[metamap] trg
USING 
@metamap src ON src.[metamap_id] = trg.[metamap_id]
WHEN MATCHED THEN UPDATE SET 
    [msg_key]        = src.[msg_key],
    [table_name]     = src.[table_name],
    [metaadapter_id] = src.[metaadapter_id],
    [namespace]      = src.[namespace],
    [namespace_ver]  = src.[namespace_ver],
    [etl_query]      = src.[etl_query],
    [import_query]   = src.[import_query],
    [is_enable]      = src.[is_enable]
WHEN NOT MATCHED BY TARGET THEN 
INSERT ([metamap_id], [msg_key], [table_name], [metaadapter_id], [namespace], [namespace_ver], [etl_query], [import_query], [is_enable])
    VALUES (
        src.[metamap_id],
        src.[msg_key],
        src.[table_name],
        src.[metaadapter_id],
        src.[namespace],
        src.[namespace_ver],
        src.[etl_query],
        src.[import_query],
        src.[is_enable]
    )
WHEN NOT MATCHED BY SOURCE THEN DELETE;

GO
