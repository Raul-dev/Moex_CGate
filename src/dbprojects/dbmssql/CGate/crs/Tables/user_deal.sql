CREATE TABLE [crs].[user_deal] (
    [replID]                BIGINT          NULL,
    [replRev]               BIGINT          NULL,
    [replAct]               BIGINT          NULL,
    [sess_id]               INT             NULL,
    [isin_id]               INT             NULL,
    [id_deal]               BIGINT          NULL,
    [id_deal_multileg]      BIGINT          NULL,
    [id_repo]               BIGINT          NULL,
    [xpos]                  BIGINT          NULL,
    [xamount]               BIGINT          NULL,
    [public_order_id_buy]   BIGINT          NULL,
    [public_order_id_sell]  BIGINT          NULL,
    [price]                 DECIMAL (16, 5) NULL,
    [moment]                DATETIME2 (3)   NULL,
    [moment_ns]             DECIMAL (20)    NULL,
    [nosystem]              TINYINT         NULL,
    [xstatus_buy]           BIGINT          NULL,
    [xstatus_sell]          BIGINT          NULL,
    [ext_id_buy]            INT             NULL,
    [ext_id_sell]           INT             NULL,
    [code_buy]              NVARCHAR (7)    NULL,
    [code_sell]             NVARCHAR (7)    NULL,
    [comment_buy]           NVARCHAR (20)   NULL,
    [comment_sell]          NVARCHAR (20)   NULL,
    [fee_buy]               DECIMAL (26, 2) NULL,
    [fee_sell]              DECIMAL (26, 2) NULL,
    [login_buy]             NVARCHAR (20)   NULL,
    [login_sell]            NVARCHAR (20)   NULL,
    [code_rts_buy]          NVARCHAR (7)    NULL,
    [code_rts_sell]         NVARCHAR (7)    NULL,
    [id_ord_buy]            BIGINT          NULL,
    [id_ord_sell]           BIGINT          NULL,
    [reason_buy]            INT             NULL,
    [reason_sell]           INT             NULL,
    [private_order_id_buy]  BIGINT          NULL,
    [private_order_id_sell] BIGINT          NULL
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IDX_user_deal_REV_U]
    ON [crs].[user_deal]([replRev] ASC);

