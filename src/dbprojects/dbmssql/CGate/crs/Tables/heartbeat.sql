CREATE TABLE [crs].[heartbeat] (
    [replID]      BIGINT        NULL,
    [replRev]     BIGINT        NULL,
    [replAct]     BIGINT        NULL,
    [server_time] DATETIME2 (3) NULL
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IDX_heartbeat_REV_U]
    ON [crs].[heartbeat]([replRev] ASC);

