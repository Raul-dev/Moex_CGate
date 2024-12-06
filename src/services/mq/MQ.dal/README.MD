1) mssql: Scaffold-DbContext "Server=localhost;Database=metastorage;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=true;Encrypt=False" Microsoft.EntityFrameworkCore.SqlServer -OutputDir Models
psql: Scaffold-DbContext "Host=localhost;Port=54321;Database=metastorage;Username=postgres;Password=postgres" Npgsql.EntityFrameworkCore.PostgreSQL -OutputDir Modelsp

2) add-migration InitialCreate -StartupProject "MQ.WebService" -Project "MQ.dal" -OutputDir "Data/PsqlMigrations" -Context "MetastorageContext"
remove-migration InitialCreate -StartupProject "MQ.WebService" -Project "MQ.dal" -Context "MetastorageContext"
Update-Database -StartupProject "MQ.WebService" -Project "MQ.dal" -Context "MetastorageContext"

-Context "ApplicationDbContext"
-StartupProject "EntryProject.Name" 
-Project "MigrationProject.Name"
