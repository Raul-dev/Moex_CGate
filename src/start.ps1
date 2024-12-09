Param (
    [parameter(Mandatory=$false)][string]$TargetServerName="localhost",
    [parameter(Mandatory=$false)][string]$TargetDBname="CGate", 
    [parameter(Mandatory=$false)][string]$IsUpdate=$false
  )
function Test-Administrator  
{  
    [OutputType([bool])]
    param()
    process {
        [Security.Principal.WindowsPrincipal]$user = [Security.Principal.WindowsIdentity]::GetCurrent();
        return $user.IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator);
    }
}

function MergeUser
{
  Param (
    [string]$lTargetServerName,
    [string]$lTargetDBname,
    [string]$lSQLuser,
    [string]$lSQLpwd
    )
    try{
        $lSqlCmd = "
            IF SUSER_ID('"+$lSQLuser+"') IS NULL
                CREATE LOGIN ["+$lSQLuser+"] WITH PASSWORD = N'"+$lSQLpwd+"', DEFAULT_DATABASE=[master], DEFAULT_LANGUAGE=[us_english], CHECK_EXPIRATION=OFF, CHECK_POLICY=OFF
            
            IF USER_ID('"+$lSQLuser+"') IS NULL
                 CREATE USER ["+$lSQLuser+"] FOR LOGIN ["+$lSQLuser+"] WITH DEFAULT_SCHEMA=[dbo]
            ALTER ROLE db_owner ADD MEMBER ["+$lSQLuser+"];
            "
        sqlcmd -S $lTargetServerName  -d $lTargetDBname -Q $lSqlCmd
        return 0
    }
    catch {
        Write-Host "An error occurred:" -fore red
        Write-Host $_ -fore red
        return -1
    }
}

if(-not $IsUpdate) {
    if(-not (Test-Administrator))
    {
        # TODO: define proper exit codes for the given errors 
        Write-Error "This script must be executed as Administrator.";
        exit 1;
    }
}

$ErrorActionPreference = "Stop";
$CurrentPath = Get-Location
Set-Location "./dbprojects/dbmssql/CGate/ScriptsFolder"

if($IsUpdate -eq $true){
    try{
        Invoke-RestMethod  -Uri http://localhost:8090/api/Home/Stop -ErrorAction SilentlyContinue
    } catch {
    }
}
./dbdeploy -TargetServerName $TargetServerName -TargetDBname $TargetDBname -PublishOnly $true -IsRebuild $true
if ($LASTEXITCODE -eq -1)
{
  Set-Location $CurrentPath

  exit
}
$res = MergeUser $TargetServerName $TargetDBname "CGateUser" "MyPassword321"
IF ($LASTEXITCODE -ne 0 -or $res -ne 0){
    throw "Create user CGateUser failed."
}
Set-Location $CurrentPath

if($IsUpdate -eq $true){
    try{
        Invoke-RestMethod  -Uri http://localhost:8090/api/Home/Start -ErrorAction SilentlyContinue
    } catch {
    }
    exit
}

Set-Location $CurrentPath
docker compose up
