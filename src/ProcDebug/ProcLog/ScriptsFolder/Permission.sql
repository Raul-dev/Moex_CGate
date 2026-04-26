SELECT 
    'GRANT USAGE ON SCHEMA::[audit] TO [' + DP.name + ']; ' +
    'GRANT SELECT ON SCHEMA::[audit] TO [' + DP.name + '];' AS GrantCommand
FROM sys.schemas S
JOIN sys.database_principals DP ON S.principal_id = DP.principal_id
WHERE S.name NOT IN ('guest', 'INFORMATION_SCHEMA', 'sys', 'db_owner', 'db_accessadmin', 
                     'db_securityadmin', 'db_ddladmin', 'db_backupoperator', 
                     'db_datareader', 'db_datawriter', 'db_denydatareader', 
                     'db_denydatawriter', 'audit')
      AND DP.name NOT IN ('dbo', 'sys', 'INFORMATION_SCHEMA')
GROUP BY DP.name;