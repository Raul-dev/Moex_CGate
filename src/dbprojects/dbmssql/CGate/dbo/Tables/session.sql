CREATE TABLE [dbo].[session] (
    [session_id]       BIGINT         IDENTITY (1, 1) NOT NULL,
    [data_source_id]   TINYINT        NOT NULL,
    [session_state_id] TINYINT        NOT NULL,
    [error_message]    VARCHAR (4000) NULL,
    [dt_update]        DATETIME2 (4)  CONSTRAINT [DF_session_dt_update_DEFAULT] DEFAULT (getdate()) NOT NULL,
    [dt_create]        DATETIME2 (4)  CONSTRAINT [DF_session_dt_create_DEFAULT] DEFAULT (getdate()) NOT NULL,
    CONSTRAINT [PK_session] PRIMARY KEY CLUSTERED ([session_id] ASC),
    CONSTRAINT [FK_session_session_state] FOREIGN KEY ([session_state_id]) REFERENCES [dbo].[session_state] ([session_state_id])
);

