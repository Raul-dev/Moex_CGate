CREATE FUNCTION [audit].[fn_log_IsLnk](
)RETURNS BIT
AS
BEGIN
  RETURN IIF( EXISTS(SELECT * from sys.databases WITH(nolock) WHERE database_id = DB_ID() AND snapshot_isolation_state_desc = 'ON')  
              OR EXISTS(SELECT * FROM sys.dm_exec_sessions WITH(nolock) WHERE session_id = @@SPID AND transaction_isolation_level = 5),
              0,1
            )
END
