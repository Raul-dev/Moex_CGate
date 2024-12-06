
DECLARE @database VARCHAR(200) = DB_NAME(),
    @server NVARCHAR(200) = '$(LinkSRVLog)';

IF EXISTS (SELECT * FROM sys.servers WHERE NAME = @server )    
    EXECUTE sp_dropserver @server = @server

IF NOT EXISTS (SELECT * FROM sys.servers WHERE NAME = @server )
BEGIN
    
    EXECUTE sp_addlinkedserver @server = @server,  
                               @srvproduct = ' ',
                               @provider = 'SQLNCLI', 
                               @datasrc = @@SERVERNAME, 
                               @catalog = @database
END

EXEC sp_serveroption LinkSRVLog, 'RPC OUT', 'TRUE'
EXEC sp_serveroption LinkSRVLog, 'remote proc transaction promotion', 'FALSE'

