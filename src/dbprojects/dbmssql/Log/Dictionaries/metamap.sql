
IF NOT EXISTS(SELECT 1 FROM [mq].[MetaAdapter] )
BEGIN
    INSERT INTO [mq].[MetaAdapter] ([MetaAdapterId], [Name])
    SELECT 1, N'CGateJson'
    UNION ALL SELECT 2, N'CGateAuditSP'
    UNION ALL SELECT 3, N'CGateAuditLT'
    UNION ALL SELECT 4, N'CGateAuditErr'
    UNION ALL SELECT 5, N'UnknownJsonXml'

END
DECLARE @metamap TABLE
(
    [MetaMapId]        SMALLINT       NOT NULL,
    [MessageKey]       NVARCHAR(256)  NOT NULL,
    [TableName]        NVARCHAR(128)  NOT NULL,
    [MetaAdapterId]    TINYINT        NULL,
    [Namespace]        NVARCHAR (256) NULL,
    [NamespaceVersion] NVARCHAR (256) NULL,
    [EtlProcedure]     NVARCHAR (256) NULL,
    [ImportQuery]      NVARCHAR (256) NULL,
    [IsEnabled]        BIT            NULL
)
INSERT @metamap ([MetaMapId], [MessageKey], [TableName], [MetaAdapterId], [Namespace], [NamespaceVersion], [EtlProcedure], [ImportQuery], [IsEnabled])
VALUES
(3, N'Unknown', N'[audit].[LogTextBuffer]', 3, N'audit.LogText', N'audit.AuditLT/version1.01', N'[audit].[load_LogText]', NULL, 1),
(4, N'Unknown', N'[audit].[LogErrorBuffer]', 4, N'audit.LogError', N'audit.AuditErr/version1.01', N'[audit].[load_LogError]', NULL, 0)

IF EXISTS ( 
    SELECT 1 FROM [mq].[MetaMap] d
    LEFT OUTER JOIN @metamap s ON s.[MetaMapId] = d.[MetaMapId]
    WHERE s.[MetaMapId] IS NULL) THROW 60000, N'The table [mq].[MetaMap] was change.', 1;



MERGE INTO [mq].[MetaMap] trg
USING 
@metamap src ON src.[MetaMapId] = trg.[MetaMapId]
WHEN MATCHED THEN UPDATE SET 
    [MessageKey]       = src.[MessageKey],
    [TableName]        = src.[TableName],
    [MetaAdapterId]    = src.[MetaAdapterId],
    [Namespace]        = src.[Namespace],
    [NamespaceVersion] = src.[NamespaceVersion],
    [EtlProcedure]     = src.[EtlProcedure],
    [ImportQuery]      = src.[ImportQuery],
    [IsEnabled]        = src.[IsEnabled]
WHEN NOT MATCHED BY TARGET THEN 
INSERT ([MetaMapId], [MessageKey], [TableName], [MetaAdapterId], [Namespace], [NamespaceVersion], [EtlProcedure], [ImportQuery], [IsEnabled])
    VALUES (
        src.[MetaMapId],
        src.[MessageKey],
        src.[TableName],
        src.[MetaAdapterId],
        src.[Namespace],
        src.[NamespaceVersion],
        src.[EtlProcedure],
        src.[ImportQuery],
        src.[IsEnabled]
    )
WHEN NOT MATCHED BY SOURCE THEN DELETE;

GO
