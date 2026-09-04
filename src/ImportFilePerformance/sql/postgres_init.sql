-- Create database ImportFile on PostgreSQL (run as superuser / postgres)
-- Role password matches Moex_CGate default (MyPassword321)

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'CGateUser') THEN
        CREATE ROLE "CGateUser" WITH LOGIN PASSWORD 'MyPassword321';
        RAISE NOTICE 'Created ROLE CGateUser.';
    ELSE
        ALTER ROLE "CGateUser" WITH LOGIN PASSWORD 'MyPassword321';
        RAISE NOTICE 'ROLE CGateUser already exists — password refreshed.';
    END IF;
END
$$;

SELECT 'CREATE DATABASE "ImportFile" OWNER "CGateUser"'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'ImportFile')\gexec

\c ImportFile

-- If DB already existed under another owner, still grant full access.
GRANT CONNECT ON DATABASE "ImportFile" TO "CGateUser";
GRANT ALL ON SCHEMA public TO "CGateUser";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO "CGateUser";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO "CGateUser";

CREATE TABLE IF NOT EXISTS stg_order_log (
    sess_id       bigint,
    ticker        text,
    buysell       text,
    time_str      text,
    orderno       bigint,
    action        integer,
    price         numeric(18,4),
    volume        bigint,
    tradeno       bigint,
    tradeprice    numeric(18,4)
);

CREATE TABLE IF NOT EXISTS stg_futures_xml (
    report_date      text,
    board_id         text,
    base_asset_type  text,
    base_asset_code  text,
    base_asset_isin  text,
    futures_code     text,
    futures_name     text,
    delivery_type    text,
    currency_id      text,
    lot              numeric(28,8),
    min_step         numeric(28,8),
    step_price       numeric(28,8),
    trade_lot        numeric(28,8),
    point_rate       numeric(28,8),
    total_amount     numeric(28,8),
    total_volume     numeric(28,8),
    total_deal_count bigint,
    max_deal_price   numeric(28,8),
    min_deal_price   numeric(28,8),
    last_deal_price  numeric(28,8),
    clearing_price   numeric(28,8),
    current_price    numeric(28,8)
);

GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO "CGateUser";
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO "CGateUser";
