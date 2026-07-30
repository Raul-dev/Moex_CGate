CREATE TABLE [crs].[SysEvents] (
    [ReplId]     BIGINT        NULL,
    [ReplRev]    BIGINT        NULL,
    [ReplAct]    BIGINT        NULL,
    [EventId]   BIGINT        NULL,
    [SessionId]    INT           NULL,
    [EventType] INT           NULL,
    [message]    NVARCHAR (64) NULL
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IDX_sys_events_ID_U]
    ON [crs].[SysEvents]([ReplId] ASC);

