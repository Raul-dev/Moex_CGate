-- Create database ImportFile on MS SQL Server (run as sa / admin)
-- Login/user password matches appsettings.json (MyPassword321)

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'CGateUser')
BEGIN
    CREATE LOGIN [CGateUser] WITH PASSWORD = N'MyPassword321', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
    PRINT N'Created LOGIN [CGateUser].';
END
ELSE
BEGIN
    ALTER LOGIN [CGateUser] ENABLE;
    PRINT N'LOGIN [CGateUser] already exists — enabled.';
END
GO

IF DB_ID(N'ImportFile') IS NULL
BEGIN
    CREATE DATABASE ImportFile;
    PRINT N'Created DATABASE [ImportFile].';
END
GO

USE ImportFile;
GO

IF USER_ID(N'CGateUser') IS NULL
BEGIN
    CREATE USER [CGateUser] FOR LOGIN [CGateUser];
    PRINT N'Created USER [CGateUser] in ImportFile.';
END
GO

IF IS_ROLEMEMBER(N'db_owner', N'CGateUser') = 0 OR IS_ROLEMEMBER(N'db_owner', N'CGateUser') IS NULL
BEGIN
    ALTER ROLE [db_owner] ADD MEMBER [CGateUser];
END
GO

GRANT CONNECT TO [CGateUser];
GO

IF OBJECT_ID(N'dbo.stg_order_log', N'U') IS NULL
CREATE TABLE dbo.stg_order_log (
    sess_id       bigint       NULL,
    ticker        nvarchar(32) NULL,
    buysell       nvarchar(1)  NULL,
    time_str      nvarchar(32) NULL,
    orderno       bigint       NULL,
    action        int          NULL,
    price         decimal(18,4) NULL,
    volume        bigint       NULL,
    tradeno       bigint       NULL,
    tradeprice    decimal(18,4) NULL
);
GO

IF OBJECT_ID(N'dbo.stg_futures_xml', N'U') IS NULL
CREATE TABLE dbo.stg_futures_xml (
    report_date      nvarchar(32)  NULL,
    board_id         nvarchar(32)  NULL,
    base_asset_type  nvarchar(64)  NULL,
    base_asset_code  nvarchar(64)  NULL,
    base_asset_isin  nvarchar(64)  NULL,
    futures_code     nvarchar(64)  NULL,
    futures_name     nvarchar(256) NULL,
    delivery_type    nvarchar(8)   NULL,
    currency_id      nvarchar(16)  NULL,
    lot              decimal(28,8) NULL,
    min_step         decimal(28,8) NULL,
    step_price       decimal(28,8) NULL,
    trade_lot        decimal(28,8) NULL,
    point_rate       decimal(28,8) NULL,
    total_amount     decimal(28,8) NULL,
    total_volume     decimal(28,8) NULL,
    total_deal_count bigint        NULL,
    max_deal_price   decimal(28,8) NULL,
    min_deal_price   decimal(28,8) NULL,
    last_deal_price  decimal(28,8) NULL,
    clearing_price   decimal(28,8) NULL,
    current_price    decimal(28,8) NULL
);
GO
