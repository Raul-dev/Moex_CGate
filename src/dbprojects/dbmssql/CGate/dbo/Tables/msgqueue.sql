CREATE TABLE [dbo].[msgqueue] (
    [buffer_id]  BIGINT           IDENTITY (1, 1) NOT NULL,
    [session_id] BIGINT           NOT NULL,
    [msg_id]     UNIQUEIDENTIFIER NULL,
    [msg]        NVARCHAR (MAX)   NULL,
    [msg_key]    NVARCHAR (256)   NULL,
    [dt_create]  DATETIME2 (4)    CONSTRAINT [DF__msgqueue__dt_create] DEFAULT (getdate()) NOT NULL,
    PRIMARY KEY CLUSTERED ([buffer_id] ASC)
);

