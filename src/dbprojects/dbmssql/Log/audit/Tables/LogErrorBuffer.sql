CREATE TABLE [audit].[LogErrorBuffer] (
    [BufferId]      bigint           IDENTITY (1, 1) NOT NULL,
    [SessionId]     bigint           NOT NULL,
    [MessageId]     uniqueidentifier NOT NULL,
    [MessageBody]   varchar (MAX)    NULL,
    [MessageTypeId] tinyint          NULL,
    [IsError]       bit              CONSTRAINT [DF_audit_LogErrorBuffer_IsError] DEFAULT ((0)) NOT NULL,
    [CreatedAt]     datetime2 (4)    CONSTRAINT [DF_audit_LogErrorBuffer_CreatedAt] DEFAULT (getdate()) NOT NULL,
    [UpdatedAt]     datetime2 (4)    CONSTRAINT [DF_audit_LogErrorBuffer_UpdatedAt] DEFAULT (datefromparts((1900),(1),(1))) NOT NULL,
    CONSTRAINT [PK_audit_LogErrorBuffer] PRIMARY KEY CLUSTERED ([BufferId] ASC) WITH (ALLOW_PAGE_LOCKS = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = ON)
);
