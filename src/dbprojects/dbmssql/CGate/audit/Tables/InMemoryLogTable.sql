CREATE TABLE [audit].[InMemoryLogTable] (
    [LogID] BIGINT IDENTITY(1,1) NOT NULL,
    [ObjectId] INT NULL,
    [KeyField] VARCHAR(128) COLLATE Latin1_General_100_BIN2 NULL,
    [KeyValue] BIGINT NULL,
    [MessageCode] VARCHAR(50) COLLATE Latin1_General_100_BIN2 NULL,
    [Message] NVARCHAR(MAX) COLLATE Latin1_General_100_BIN2 NULL, 
    [TransactionCount] INT NULL,
    [DateCreate] DATETIME2(4) NOT NULL,
    [SysDbName] NVARCHAR(128) COLLATE Latin1_General_100_BIN2 NULL,
    [SysUserName] VARCHAR(256) COLLATE Latin1_General_100_BIN2 NULL,
    [SysHostName] VARCHAR(128) COLLATE Latin1_General_100_BIN2 NULL,
    [SysAppName] VARCHAR(128) COLLATE Latin1_General_100_BIN2 NULL,
    [SPID] INT NULL,

    -- В .sqlproj для In-Memory таблиц объявляем некластеризованный первичный ключ прямо здесь
    CONSTRAINT [PK_InMemoryLogTable] PRIMARY KEY NONCLUSTERED ([LogID] ASC)
) 
WITH (
    MEMORY_OPTIMIZED = ON, 
    DURABILITY = SCHEMA_AND_DATA
);