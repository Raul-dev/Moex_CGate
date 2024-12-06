CREATE TABLE [dbo].[session_state] (
    [session_state_id] TINYINT       NOT NULL,
    [name]             VARCHAR (100) NULL,
    CONSTRAINT [PK_session_state] PRIMARY KEY CLUSTERED ([session_state_id] ASC)
);

