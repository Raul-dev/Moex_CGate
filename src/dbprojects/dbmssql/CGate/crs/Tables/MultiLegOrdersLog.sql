CREATE TABLE [crs].[MultiLegOrdersLog] (
    [ReplId]                BIGINT          NULL,
    [ReplRev]               BIGINT          NULL,
    [ReplAct]               BIGINT          NULL,
    [PublicOrderId]       BIGINT          NULL,
    [SessionId]               INT             NULL,
    [IsinId]               INT             NULL,
    [PublicAmount]         BIGINT          NULL,
    [PublicAmountRest]    BIGINT          NULL,
    [DealId]               BIGINT          NULL,
    [XStatus]               BIGINT          NULL,
    [Price]                 DECIMAL (16, 5) NULL,
    [Moment]                DATETIME2 (3)   NULL,
    [MomentNs]             DECIMAL (20)    NULL,
    [Direction]                   TINYINT         NULL,
    [PublicAction]         TINYINT         NULL,
    [DealPrice]            DECIMAL (16, 5) NULL,
    [RatePrice]            DECIMAL (16, 5) NULL,
    [SwapPrice]            DECIMAL (16, 5) NULL,
    [ClientCode]           NVARCHAR (7)    NULL,
    [LoginFrom]            NVARCHAR (20)   NULL,
    [Comment]               NVARCHAR (20)   NULL,
    [ExternalId]                INT             NULL,
    [BrokerTo]             NVARCHAR (7)    NULL,
    [BrokerToRts]         NVARCHAR (7)    NULL,
    [BrokerFromRts]       NVARCHAR (7)    NULL,
    [ExpirationDate]              DATETIME2 (3)   NULL,
    [OrderId1]               BIGINT          NULL,
    [AsPref]                INT             NULL,
    [OrderId]                BIGINT          NULL,
    [XAmount]               BIGINT          NULL,
    [XAmountRest]          BIGINT          NULL,
    [VarianceAmount]       BIGINT          NULL,
    [DiscloseConstAmount] BIGINT          NULL,
    [Action]                TINYINT         NULL,
    [Reason]                INT             NULL,
    [PrivateOrderId]      BIGINT          NULL,
    [PrivateAmount]        BIGINT          NULL,
    [PrivateAmountRest]   BIGINT          NULL,
    [PrivateAction]        TINYINT         NULL
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IDX_multileg_orders_log_REV_U]
    ON [crs].[MultiLegOrdersLog]([ReplRev] ASC);

