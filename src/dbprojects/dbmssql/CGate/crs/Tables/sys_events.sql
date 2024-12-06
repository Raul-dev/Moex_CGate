CREATE TABLE [crs].[sys_events] (
    [replID]     BIGINT        NULL,
    [replRev]    BIGINT        NULL,
    [replAct]    BIGINT        NULL,
    [event_id]   BIGINT        NULL,
    [sess_id]    INT           NULL,
    [event_type] INT           NULL,
    [message]    NVARCHAR (64) NULL
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IDX_sys_events_ID_U]
    ON [crs].[sys_events]([replID] ASC);

