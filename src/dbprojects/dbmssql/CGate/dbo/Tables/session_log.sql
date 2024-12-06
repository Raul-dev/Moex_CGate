CREATE TABLE [dbo].[session_log] (
    [session_log_id]   BIGINT         IDENTITY (1, 1) NOT NULL,
    [session_id]       BIGINT         NOT NULL,
    [session_state_id] TINYINT        NOT NULL,
    [error_message]    VARCHAR (4000) NULL,
    [dt_create]        DATETIME2 (4)  CONSTRAINT [DF_session_log_date_DEFAULT] DEFAULT (getdate()) NOT NULL,
    CONSTRAINT [PK_session_log] PRIMARY KEY CLUSTERED ([session_log_id] ASC),
    CONSTRAINT [FK_session_log_session] FOREIGN KEY ([session_id]) REFERENCES [dbo].[session] ([session_id]),
    CONSTRAINT [FK_session_log_session_state] FOREIGN KEY ([session_state_id]) REFERENCES [dbo].[session_state] ([session_state_id])
);

