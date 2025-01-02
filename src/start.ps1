# By default: ./start.ps1
# This script build and deploy database CGate to local MSSQL server
# Run Rabbit docker container and MQ service docker container
#
# Parameters:
# $IsDockerSql=$true - Run docker MSSQL server container with database CGate from \images\mssql\dacpac\CGate.dacpac
#
# Send test messages FROM table [CGate].[dbo].[msgqueue] to Rabbit by command:
# .\services\mq\MQ\bin\Release\net9.0\MQ.exe SendMsg -d CGate -t mssql

Param (
    [parameter(Mandatory=$false)][bool]$IsUpdate=$false,
	[parameter(Mandatory=$false)][bool]$IsDockerSql=$false,
	[parameter(Mandatory=$false)][bool]$IsRecreateDockerContainer=$true
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

function Test-Docker
{  
    [OutputType([bool])]
    param()
	if((docker ps 2>&1) -match '^(?!error)'){
     Write-Host "Docker is running"
	 return $true
    }
	return $false
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

try{
	$CurrentPath = Get-Location
	if(-Not (Test-Docker)){
		throw "DOCKER is not running. Visit and download https://docs.docker.com/docker-for-windows/install/ "
	}
	$TargetServerName="localhost";
    $TargetDBname="CGate";
	$ErrorActionPreference = "Stop";
	

	if($IsUpdate -eq $true){
		try{
			Invoke-RestMethod  -Uri http://localhost:8090/api/Home/Stop -ErrorAction SilentlyContinue
		} catch {
		}
	}

	Set-Location $CurrentPath
	if(-Not (Test-Path  .\services\mq\MQ\bin\Release\net9.0\MQ.exe)){
		dotnet build .\services\mq\MQ\MQ.csproj -c Release
	}

	Set-Location "./dbprojects/dbmssql/CGate/ScriptsFolder"

	if ($IsDockerSql ) {
		./dbdeploy -TargetServerName $TargetServerName -TargetDBname $TargetDBname -PublishMode "Build" -IsRebuild $true
	} else {
	    ./dbdeploy -TargetServerName $TargetServerName -TargetDBname $TargetDBname -PublishMode "DeployOnly" -IsRebuild $true
	}
	
	if ($LASTEXITCODE -eq -1)
	{
	  Set-Location $CurrentPath
	  exit
	}
	Set-Location $CurrentPath
	Copy-Item -Path .\dbprojects\dbmssql\CGate\bin\Release\*.* -Destination .\images\mssql\dacpac\ -Force
	if (-Not $IsDockerSql ) {
		$res = MergeUser $TargetServerName $TargetDBname "CGateUser" "MyPassword321"
		IF ($LASTEXITCODE -ne 0 -or $res -ne 0){
			throw "Create user CGateUser failed."
		}
		Set-Location $CurrentPath
	}

	if($IsUpdate -eq $true){
		try{
			Invoke-RestMethod  -Uri http://localhost:8090/api/Home/Start -ErrorAction SilentlyContinue
		} catch {
		}
		exit
	}

	Set-Location $CurrentPath
	if ($IsRecreateDockerContainer){
	    docker compose -f docker-compose.sqldacpac.yml down -v
		docker compose down -v
	}
	if (-Not $IsDockerSql ) {
		
		docker compose up
	} else {
		
		docker compose -f docker-compose.sqldacpac.yml up -d
		Start-Sleep -Seconds 5
		docker inspect -f '{{.State.Health.Status}}' cgatemssql
		do {
			Start-Sleep -Seconds 10
			docker logs --tail 20 cgatemssql
		} while((docker inspect -f '{{.State.Health.Status}}' cgatemssql) -eq "unhealthy" )
		#MSSQL server IP
		#docker inspect -f "{{.NetworkSettings.IPAddress}}" cgatemssql
		
		docker compose --env-file .env.sqlimage up 
	
	}
} catch {
  
  Write-Host "An error occurred:" -fore red
  Write-Host $_ -fore red
  Write-Host "Stack:"
  Write-Host $_.ScriptStackTrace
  $ExitCode = -1
}
finally {
	Set-Location -Path $CurrentPath
	exit $ExitCode
}	
