CREATE TABLE [crs].[UserDeal] (
    [ReplId]                BIGINT          NULL,
    [ReplRev]               BIGINT          NULL,
    [ReplAct]               BIGINT          NULL,
    [SessionId]               INT             NULL,
    [IsinId]               INT             NULL,
    [DealId]               BIGINT          NULL,
    [MultiLegDealId]      BIGINT          NULL,
    [RepoId]               BIGINT          NULL,
    [xpos]                  BIGINT          NULL,
    [XAmount]               BIGINT          NULL,
    [PublicOrderIdBuy]   BIGINT          NULL,
    [PublicOrderIdSell]  BIGINT          NULL,
    [Price]                 DECIMAL (16, 5) NULL,
    [Moment]                DATETIME2 (3)   NULL,
    [MomentNs]             DECIMAL (20)    NULL,
    [NoSystem]              TINYINT         NULL,
    [XStatusBuy]           BIGINT          NULL,
    [XStatusSell]          BIGINT          NULL,
    [ExternalIdBuy]            INT             NULL,
    [ExternalIdSell]           INT             NULL,
    [CodeBuy]              NVARCHAR (7)    NULL,
    [CodeSell]             NVARCHAR (7)    NULL,
    [CommentBuy]           NVARCHAR (20)   NULL,
    [CommentSell]          NVARCHAR (20)   NULL,
    [FeeBuy]               DECIMAL (26, 2) NULL,
    [FeeSell]              DECIMAL (26, 2) NULL,
    [LoginBuy]             NVARCHAR (20)   NULL,
    [LoginSell]            NVARCHAR (20)   NULL,
    [CodeRtsBuy]          NVARCHAR (7)    NULL,
    [CodeRtsSell]         NVARCHAR (7)    NULL,
    [OrderIdBuy]            BIGINT          NULL,
    [OrderIdSell]           BIGINT          NULL,
    [ReasonBuy]            INT             NULL,
    [ReasonSell]           INT             NULL,
    [PrivateOrderIdBuy]  BIGINT          NULL,
    [PrivateOrderIdSell] BIGINT          NULL
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IDX_user_deal_REV_U]
    ON [crs].[UserDeal]([ReplRev] ASC);

