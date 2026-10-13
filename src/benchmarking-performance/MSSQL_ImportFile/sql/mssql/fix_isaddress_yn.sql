USE ImportFile;
GO
-- isaddress in SPB CSV is Y/N — must be string, not tinyint
IF COL_LENGTH(N'dbo.stg_trade_result_typed_csv', N'isaddress') IS NOT NULL
  ALTER TABLE dbo.stg_trade_result_typed_csv ALTER COLUMN [isaddress] nvarchar(1024) NULL;
IF COL_LENGTH(N'dbo.stg_trade_result_typed_target', N'isaddress') IS NOT NULL
  ALTER TABLE dbo.stg_trade_result_typed_target ALTER COLUMN [isaddress] nvarchar(512) NULL;
IF COL_LENGTH(N'dbo.stg_trade_result_string_buffer', N'isaddress') IS NOT NULL
  ALTER TABLE dbo.stg_trade_result_string_buffer ALTER COLUMN [isaddress] nvarchar(1536) NULL;
GO
