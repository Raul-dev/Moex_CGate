CREATE LOGIN [CGateUser] WITH PASSWORD = N'MyPassword321', DEFAULT_DATABASE=[master], DEFAULT_LANGUAGE=[us_english], CHECK_EXPIRATION=OFF, CHECK_POLICY=OFF
CREATE USER [CGateUser] FOR LOGIN [CGateUser] WITH DEFAULT_SCHEMA=[dbo]
ALTER ROLE db_owner ADD MEMBER [CGateUser];

--Build
--https://learn.microsoft.com/en-us/azure-data-studio/extensions/sql-database-project-extension-build-from-command-line?view=sql-server-ver15
--https://stackoverflow.com/questions/76367835/how-to-locate-sqlpackage-tool-after-global-installation-in-docker-container
C:\Program Files\dotnet\sdk\
C:\Program Files\dotnet\sdk\8.0.106
-f net8.0
dotnet  build "CGate.sqlproj"  /p:NetCoreBuild=true /p:NETCoreTargetsPath="C:\Program Files\Azure Data Studio\resources\app\extensions\mssql\sqltoolsservice\Windows\4.10.1.3" -c Realease -o /build -v diag


https://github.com/rr-wfm/MSBuild.Sdk.SqlProj
Download and install Azure Data Studio
C:\Program Files\Azure Data Studio\resources\app\extensions
https://learn.microsoft.com/en-us/azure-data-studio/download-azure-data-studio?tabs=linux-install%2Cwin-user-install%2Cubuntu-install%2Cwindows-uninstall%2Credhat-uninstall
cd ~
cp ~/Downloads/azuredatastudio-linux-<version string>.tar.gz ~
tar -xvf ~/azuredatastudio-linux-<version string>.tar.gz
echo 'export PATH="$PATH:~/azuredatastudio-linux-x64"' >> ~/.bashrc
source ~/.bashrc

<ItemGroup>
<PackageReference Condition="'$(NetCoreBuild)' == 'true'" Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.0" PrivateAssets="All" />
</ItemGroup>

publish script
https://stackoverflow.com/questions/20513530/how-to-publish-dacpac-file-to-a-sql-server-database-project-via-sqlpackage-exe-o

sqlcmd -S localhost -U sa -P fff -C -d master -i CGate.publish.sql;
docker-compose -f docker-compose.sql.yml up
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=yourStrong(!)Password" -p 1443:1433 -d mcr.microsoft.com/mssql/server:2022-latest

TRANSACTION ISOLATION LEVEL READ COMMITTED
--Performance buffer
SELECT CONVERT(DATETIME2(0) , dt_create) [Time],  count(DISTINCT msg_id) [Messages], count(*) [Orders], min(dt_create), max(dt_create)
FROM [crs].[orders_log_buffer]
GROUP BY  CONVERT(DATETIME2(0), dt_create)
ORDER BY 1

--Performance Load
SELECT CONVERT(DATETIME2(0) , dt_update) [Time],  count(DISTINCT msg_id) [Messages], count(*) [Orders], min(dt_update), max(dt_update)
FROM [crs].[orders_log_buffer]
GROUP BY  CONVERT(DATETIME2(0), dt_update)
ORDER BY 1