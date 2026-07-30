CREATE TABLE [mq].[UploadBuffer] (
    [BufferId]      BIGINT           IDENTITY (1, 1) NOT NULL,
    [SessionId]     BIGINT           NOT NULL,
    [MessageKey]    NVARCHAR (256)   NOT NULL,
    [MessageId]     UNIQUEIDENTIFIER NOT NULL,
    [MessageBody]   VARCHAR (MAX)    NULL,
    [MessageTypeId] TINYINT          NULL,
    [IsError]       BIT              CONSTRAINT [DF_mq_UploadBuffer_IsError] DEFAULT ((0)) NOT NULL,
    [CreatedAt]     DATETIME2 (4)    CONSTRAINT [DF_mq_UploadBuffer_CreatedAt] DEFAULT (SYSDATETIME()) NOT NULL,
    [UpdatedAt]     DATETIME2 (4)    CONSTRAINT [DF_mq_UploadBuffer_UpdatedAt] DEFAULT ('1900-01-01') NOT NULL,
    CONSTRAINT [PK_mq_UploadBuffer] PRIMARY KEY CLUSTERED ([BufferId] ASC)
);
