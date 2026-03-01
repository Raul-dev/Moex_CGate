EXEC sp_configure 'show advanced options', 1;
RECONFIGURE;
EXEC sp_configure 'clr strict security', 0;
RECONFIGURE;
EXEC sp_configure 'clr enabled', 1;
RECONFIGURE;
EXEC sp_configure 'show advanced options', 0;
RECONFIGURE;

-- check whether CLR is enabled
DECLARE @isCLREnabled int;
DECLARE @spConfigureTab TABLE (name varchar(256), minimum int, maximum int, config_value int, run_value int)

--check if clr is enabled, if not return out - as it cannot be done dynamically

EXEC sp_Configure 'CLR Enabled';

SELECT @isCLREnabled = run_value
FROM @spConfigureTab;
/* 
IF(@isCLREnabled = 0)
BEGIN
  RAISERROR('The database has been created, BUT CLR is not enabled on this Sql Server instance. To enable, execute "sp_configure ''CLR Enabled'', 1 GO RECONFIGURE GO".', 16, -1)
  RETURN;
END
*/
--the binary representation of System.Threading

DECLARE @database VARCHAR(200) = DB_NAME(),
    @server NVARCHAR(200) = '$(LinkSRVLog)';

IF EXISTS (SELECT * FROM sys.servers WHERE NAME = @server )    
    EXECUTE sp_dropserver @server = @server

IF NOT EXISTS (SELECT * FROM sys.servers WHERE NAME = @server )
BEGIN
    
    EXECUTE sp_addlinkedserver @server = @server,  
                               @srvproduct = ' ',
                               @provider = 'SQLNCLI', 
                               @catalog = @database
END

EXEC sp_serveroption LinkSRVLog, 'RPC OUT', 'TRUE'
EXEC sp_serveroption LinkSRVLog, 'remote proc transaction promotion', 'FALSE'

