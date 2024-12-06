CREATE TABLE [crs].[orders_log_buffer] (
    [buffer_id]  BIGINT           IDENTITY (1, 1) NOT NULL,
    [session_id] BIGINT           NOT NULL,
    [msg_id]     UNIQUEIDENTIFIER NOT NULL,
    [msg]        VARCHAR (MAX)    NULL,
    [msgtype_id] TINYINT          NULL,
    [is_error]   BIT              CONSTRAINT [DF_crs_orders_log_buffer_IS_ERROR_DEFAULT] DEFAULT ((0)) NOT NULL,
    [dt_create]  DATETIME2 (4)    CONSTRAINT [DF_crs_orders_log_buffer_dt_create_DEFAULT] DEFAULT (getdate()) NOT NULL,
    [dt_update]  DATETIME2 (4)    CONSTRAINT [DF_crs_orders_log_buffer_dt_update_DEFAULT] DEFAULT (datefromparts((1900),(1),(1))) NOT NULL,
    [RefID]      AS               (CONVERT([bigint],json_value([msg],N'$[34]'))),
    CONSTRAINT [PK_crs_orders_log_buffer] PRIMARY KEY CLUSTERED ([buffer_id] ASC) WITH (ALLOW_PAGE_LOCKS = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = ON)
);

