#Install-Module VSSetup -Scope AllUsers
function Get-MSVsInfo
{
  Param 
  (
    [bool] $allowPreviewVersions = $false
  )
  
  $valuesToLookFor = @(
    'Visual Studio Community 2019',
    'Visual Studio Professional 2019')
    if ($allowPreviewVersions) {
       Write-Host "We are looking only among these versions: " 
       Write-Host $valuesToLookFor
       $latestVsInstallationInfo = Get-VSSetupInstance -All -Prerelease | Sort-Object -Property InstallationVersion -Descending | Where-Object -FilterScript {$valuesToLookFor -contains $_.DisplayName} | Select-Object -First 1
    } else {
      $latestVsInstallationInfo = Get-VSSetupInstance -All | Sort-Object -Property InstallationVersion -Descending | Select-Object -First 1
    }

    if($null -eq $latestVsInstallationInfo) {
        Write-Host "Visual Studio not found." -foregroundcolor red
    } else {
        Write-Host "Visual Studio was successfully found." -foregroundcolor green
        Write-Host "VS: $($latestVsInstallationInfo.DisplayName). Version installed is $($latestVsInstallationInfo.InstallationVersion)"
    }
   
    return $latestVsInstallationInfo
}

function Get-MsBuildPath
{
  Param 
  (
    [bool] $AllowPreviewVersions = $false
  )

    $latestVsInstallationInfo = Get-MSVsInfo($AllowPreviewVersions)
    if ($latestVsInstallationInfo.InstallationVersion -like "15.*") {
      $MsBuildPath = "$($latestVsInstallationInfo.InstallationPath)\MSBuild\15.0\Bin\msbuild.exe"
    
      Write-Host "Located msbuild for Visual Studio 2017 in $MsBuildPath"
    } else {
      $MsBuildPath = "$($latestVsInstallationInfo.InstallationPath)\MSBuild\Current\Bin\msbuild.exe"
      Write-Host "Located msbuild in $MsBuildPath"
    }

    return $MsBuildPath
}
    
function DropDatabase
{
  Param (
    [string]$lTargetDBname,
    [string]$lTargetServerName,
    [string]$lSQLuser,
    [string]$lSQLpwd
    )
    try{

      Write-Host  "Drop database: "$lTargetDBname" on server: $lTargetServerName"
      
      $lSqlCmd = "SET NOCOUNT ON; SELECT res=count(*) FROM sys.databases WHERE name='"+$lTargetDBname+"'"
      $lDataCount1 = sqlcmd -S $lTargetServerName  -d master  -Q $lSqlCmd -h -1
      $lDataCount = $lDataCount1.Trim()
      Write-Host "Record count: " $lDataCount
      if($lDataCount -ne 0) {
          Write-Host "Drop database  "$lTargetDBname
        $lSqlCmd = "
        DECLARE @Spid INT
        DECLARE @ExecSQL VARCHAR(255)
        
        DECLARE KillCursor CURSOR LOCAL STATIC READ_ONLY FORWARD_ONLY
        FOR
        SELECT DISTINCT SPID
        FROM MASTER..SysProcesses
        WHERE DBID = DB_ID('"+$lTargetDBname+"')
        OPEN KillCursor
        
        -- Grab the first SPID
        FETCH NEXT
        FROM KillCursor
        INTO @Spid
        
        WHILE @@FETCH_STATUS = 0
          BEGIN
            SET @ExecSQL = 'KILL ' + CAST(@Spid AS VARCHAR(50))
        
            EXEC (@ExecSQL)
        
            -- Pull the next SPID
            FETCH NEXT 
            FROM KillCursor 
            INTO @Spid  
          END
        
        CLOSE KillCursor
        DEALLOCATE KillCursor
        GO
        ALTER DATABASE "+$lTargetDBname+"
          SET SINGLE_USER;
          GO
          DROP DATABASE "+$lTargetDBname
          $lDataCount1 = sqlcmd -S $lTargetServerName  -d master  -Q $lSqlCmd -h -1
      }

    return 0

    }
    catch {
        
      Write-Host "An error occurred:" -fore red
      Write-Host $_ -fore red
      return -1
    }    

}
