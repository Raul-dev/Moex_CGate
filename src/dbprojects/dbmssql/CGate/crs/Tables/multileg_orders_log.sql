CREATE TABLE [crs].[multileg_orders_log] (
    [replID]                BIGINT          NULL,
    [replRev]               BIGINT          NULL,
    [replAct]               BIGINT          NULL,
    [public_order_id]       BIGINT          NULL,
    [sess_id]               INT             NULL,
    [isin_id]               INT             NULL,
    [public_amount]         BIGINT          NULL,
    [public_amount_rest]    BIGINT          NULL,
    [id_deal]               BIGINT          NULL,
    [xstatus]               BIGINT          NULL,
    [price]                 DECIMAL (16, 5) NULL,
    [moment]                DATETIME2 (3)   NULL,
    [moment_ns]             DECIMAL (20)    NULL,
    [dir]                   TINYINT         NULL,
    [public_action]         TINYINT         NULL,
    [deal_price]            DECIMAL (16, 5) NULL,
    [rate_price]            DECIMAL (16, 5) NULL,
    [swap_price]            DECIMAL (16, 5) NULL,
    [client_code]           NVARCHAR (7)    NULL,
    [login_from]            NVARCHAR (20)   NULL,
    [comment]               NVARCHAR (20)   NULL,
    [ext_id]                INT             NULL,
    [broker_to]             NVARCHAR (7)    NULL,
    [broker_to_rts]         NVARCHAR (7)    NULL,
    [broker_from_rts]       NVARCHAR (7)    NULL,
    [date_exp]              DATETIME2 (3)   NULL,
    [id_ord1]               BIGINT          NULL,
    [aspref]                INT             NULL,
    [id_ord]                BIGINT          NULL,
    [xamount]               BIGINT          NULL,
    [xamount_rest]          BIGINT          NULL,
    [variance_amount]       BIGINT          NULL,
    [disclose_const_amount] BIGINT          NULL,
    [action]                TINYINT         NULL,
    [reason]                INT             NULL,
    [private_order_id]      BIGINT          NULL,
    [private_amount]        BIGINT          NULL,
    [private_amount_rest]   BIGINT          NULL,
    [private_action]        TINYINT         NULL
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IDX_multileg_orders_log_REV_U]
    ON [crs].[multileg_orders_log]([replRev] ASC);

