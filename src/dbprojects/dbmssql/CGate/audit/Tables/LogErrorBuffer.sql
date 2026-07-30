CREATE TABLE [audit].[LogErrorBuffer] (
    [BufferId]      BIGINT           IDENTITY (1, 1) NOT NULL,
    [SessionId]     BIGINT           NOT NULL,
    [MessageId]     UNIQUEIDENTIFIER NOT NULL,
    [MessageBody]   VARCHAR (MAX)    NULL,
    [MessageTypeId] TINYINT          NULL,
    [IsError]       BIT              CONSTRAINT [DF_audit_LogErrorBuffer_IsError] DEFAULT ((0)) NOT NULL,
    [CreatedAt]     DATETIME2 (4)    CONSTRAINT [DF_audit_LogErrorBuffer_CreatedAt] DEFAULT (getdate()) NOT NULL,
    [UpdatedAt]     DATETIME2 (4)    CONSTRAINT [DF_audit_LogErrorBuffer_UpdatedAt] DEFAULT (datefromparts((1900),(1),(1))) NOT NULL,
    [RefId]         AS               (CONVERT([bigint], json_value([MessageBody], N'$[27]'))),
    CONSTRAINT [PK_audit_LogErrorBuffer] PRIMARY KEY CLUSTERED ([BufferId] ASC) WITH (ALLOW_PAGE_LOCKS = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = ON)
);
