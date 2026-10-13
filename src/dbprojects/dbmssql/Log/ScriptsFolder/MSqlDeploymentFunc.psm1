#Requires -Version 5.1

function Get-VsWherePath {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) {
        return $null
    }
    return $vswhere
}

function Get-VisualStudioInstallPath {
    $vswhere = Get-VsWherePath
    if ($vswhere) {
        $installPath = & $vswhere -latest -requires Microsoft.Component.MSBuild -property installationPath 2>$null
        if ($installPath -and (Test-Path $installPath)) {
            return $installPath
        }
    }

    # Fallback: VSSetup module (legacy path used by older scripts).
    if (Get-Command Get-VSSetupInstance -ErrorAction SilentlyContinue) {
        $instance = Get-VSSetupInstance -All -Prerelease |
            Sort-Object -Property InstallationVersion -Descending |
            Where-Object { $_.DisplayName -like '*Visual Studio*' } |
            Select-Object -First 1
        if ($instance) {
            return $instance.InstallationPath
        }
    }

    return $null
}

function Get-MSVsInfo {
    param([bool]$AllowPreviewVersions = $false)

    $installPath = Get-VisualStudioInstallPath
    if (-not $installPath) {
        Write-Host 'Visual Studio not found.' -ForegroundColor Red
        return $null
    }

    $vswhere = Get-VsWherePath
    if ($vswhere) {
        $displayName = & $vswhere -latest -requires Microsoft.Component.MSBuild -property displayName 2>$null
        $version = & $vswhere -latest -requires Microsoft.Component.MSBuild -property installationVersion 2>$null
        Write-Host 'Visual Studio was successfully found.' -ForegroundColor Green
        if ($displayName) {
            Write-Host "VS: $displayName. Version installed is $version"
        }
        return [PSCustomObject]@{
            InstallationPath    = $installPath
            DisplayName         = $displayName
            InstallationVersion = $version
        }
    }

    return [PSCustomObject]@{ InstallationPath = $installPath }
}

function Get-MsBuildPath {
    param([bool]$AllowPreviewVersions = $false)

    $vswhere = Get-VsWherePath
    if ($vswhere) {
        # Сначала ищем MSBuild строго там, где установлен компонент баз данных SSDT
        $msbuild = & $vswhere -latest -requires Microsoft.VisualStudio.Component.SQL.SSDT -find 'MSBuild\**\Bin\MSBuild.exe' 2>$null | Select-Object -First 1
        
        # Если такой студии нет, берем любую с MSBuild
        if (-not $msbuild) {
            $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' 2>$null | Select-Object -First 1
        }

        if ($msbuild -and (Test-Path $msbuild)) {
            Write-Host "Located msbuild in $msbuild"
            return $msbuild
        }
    }

    # Legacy fallback (VS 2017/2019 layout + VSSetup).
    $latestVsInstallationInfo = Get-MSVsInfo($AllowPreviewVersions)
    if ($null -eq $latestVsInstallationInfo) {
        return $null
    }

    if ($latestVsInstallationInfo.InstallationVersion -like '15.*') {
        $MsBuildPath = Join-Path $latestVsInstallationInfo.InstallationPath 'MSBuild\15.0\Bin\msbuild.exe'
    } else {
        $MsBuildPath = Join-Path $latestVsInstallationInfo.InstallationPath 'MSBuild\Current\Bin\msbuild.exe'
    }

    if (-not (Test-Path $MsBuildPath)) {
        Write-Host "MSBuild not found at $MsBuildPath" -ForegroundColor Red
        return $null
    }

    return $MsBuildPath
}


function Get-SqlPackagePath {
    param(
        [Parameter(Mandatory = $true)][string]$VsInstallationPath,
        [bool]$IsVS2019only = $false
    )

    if ($IsVS2019only) {
        $candidates = @(
            Join-Path $VsInstallationPath 'Common7\IDE\Extensions\Microsoft\SQLDB\DAC\150\sqlpackage.exe'
        )
    } else {
        $candidates = @(
            (Join-Path $VsInstallationPath 'Common7\IDE\Extensions\Microsoft\SQLDB\DAC\sqlpackage.exe')
            (Join-Path $VsInstallationPath 'Common7\IDE\Extensions\Microsoft\SQLDB\DAC\150\sqlpackage.exe')
        )
    }

    foreach ($candidate in $candidates) {
        if (Test-Path -Path $candidate -PathType Leaf) {
            Write-Host "Located sqlpackage in $candidate"
            return $candidate
        }
    }

    $pathEntry = $env:Path -split ';' |
        Where-Object { $_ -and (Test-Path -Path (Join-Path -Path $_ -ChildPath 'SqlPackage.exe') -PathType Leaf) } |
        Select-Object -First 1
    if ($pathEntry) {
        $fallback = Join-Path -Path $pathEntry -ChildPath 'SqlPackage.exe'
        Write-Host "Using sqlpackage from PATH: $fallback" -ForegroundColor Yellow
        return $fallback
    }

    Write-Host 'SqlPackage.exe not found.' -ForegroundColor Red
    return $null
}

function ExecAllSqlScripts {
    param(
        $Folder,
        $FileFilter,
        $TargetServerName,
        $TargetDBname,
        $SqlPassword
    )

    try {
        Write-Host "Step folder: $Folder"
        Write-Host "Filter: $FileFilter"

        $Files = Get-ChildItem $Folder -Attributes !Directory -Filter $FileFilter
        for ($i = 0; $i -lt $Files.Count; $i++) {
            $SqlFile = Join-Path $Folder $Files[$i].Name
            Write-Host "Step $i`: $SqlFile"
            if ([string]::IsNullOrEmpty($SqlPassword)) {
                Invoke-Sqlcmd -InputFile $SqlFile -ServerInstance $TargetServerName -Database $TargetDBname -QueryTimeout 65535 -ErrorAction Stop
            } else {
                Invoke-Sqlcmd -InputFile $SqlFile -OutputSqlErrors $true -ServerInstance $TargetServerName -Database $TargetDBname -QueryTimeout 65535 -ErrorAction Stop -Username 'admindb' -Password $SqlPassword
            }
        }
    } catch {
        Write-Host "Exec sqlcmd: An sql error occurred in $Folder" -ForegroundColor Red
        Write-Host $_ -ForegroundColor Red
        return -1
    }

    return 0
}

function FixedParameter {
    param($ExternalParameter)

    if ([string]::IsNullOrEmpty($ExternalParameter)) {
        return '""'
    }

    return $ExternalParameter.Trim()
}

function DropDatabase {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$TargetDatabaseName,
        [Parameter(Mandatory = $true)][string]$TargetServerName,
        [string]$SqlUser,
        [string]$SqlPassword
    )

    try {
        $escapedName = $TargetDatabaseName.Replace(']', ']]')
        $sql = @"
IF DB_ID(N'$escapedName') IS NOT NULL
BEGIN
    ALTER DATABASE [$escapedName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$escapedName];
END
"@

        Write-Host "Drop database [$TargetDatabaseName] on $TargetServerName" -ForegroundColor DarkGray

        $invokeParams = @{
            ServerInstance = $TargetServerName
            Database       = 'master'
            Query          = $sql
            ErrorAction    = 'Stop'
        }
        if ($SqlUser) {
            $invokeParams.Username = $SqlUser
            $invokeParams.Password = $SqlPassword
        }

        Invoke-Sqlcmd @invokeParams
        return 0
    } catch {
        Write-Host 'Drop database failed:' -ForegroundColor Red
        Write-Host $_ -ForegroundColor Red
        return 1
    }
}

function GetSqlValue {
    param(
        $TargetServerName,
        $TargetDBname,
        $SqlCmd,
        [ref]$res
    )

    try {
        $sqlConnectionString = "Data Source=$TargetServerName;Initial Catalog=$TargetDBname;Integrated Security=SSPI;"
        $sqlConnectionExec = New-Object System.Data.SqlClient.SqlConnection $sqlConnectionString
        $sqlConnectionExec.Open()

        $SqlCmdObj = New-Object System.Data.SqlClient.SqlCommand ($SqlCmd, $sqlConnectionExec)
        $reader = $SqlCmdObj.ExecuteReader()
        $res.Value = [int]$reader.GetValue(0)
        $reader.Dispose()
        $sqlConnectionExec.Dispose()
        return 0
    } catch {
        Write-Host 'GetSqlValue failed:' -ForegroundColor Red
        Write-Host $_ -ForegroundColor Red
        return -1
    }
}

function RunSSISPackage {
    param(
        $PackageName,
        $TargetServerName,
        $TargetFolderName,
        $ProjectName
    )

    try {
        $SSISNamespace = 'Microsoft.SqlServer.Management.IntegrationServices'
        Add-Type -AssemblyName 'Microsoft.SQLServer.Management.IntegrationServices, Version=15.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91, processorArchitecture=MSIL'

        $sqlConnectionString = "Data Source=$TargetServerName;Initial Catalog=master;Integrated Security=SSPI;"
        $sqlConnection = New-Object System.Data.SqlClient.SqlConnection $sqlConnectionString
        $integrationServices = New-Object ($SSISNamespace + '.IntegrationServices') $sqlConnection
        $catalog = $integrationServices.Catalogs['SSISDB']
        $folder = $catalog.Folders[$TargetFolderName]
        $project = $folder.Projects[$ProjectName]
        $package = $project.Packages[$PackageName]

        Write-Host "Running $PackageName ..."
        $result = $package.Execute('false', $null)
        if ($LASTEXITCODE -ne 0) {
            Write-Host $result
            throw 'SSIS Package failed.'
        }

        return 0
    } catch {
        Write-Host 'RunSSISPackage failed:' -ForegroundColor Red
        Write-Host $_ -ForegroundColor Red
        return -1
    }
}

Export-ModuleMember -Function @(
    'ExecAllSqlScripts'
    'Get-MSVsInfo'
    'Get-MsBuildPath'
    'Get-SqlPackagePath'
    'Get-VisualStudioInstallPath'
    'FixedParameter'
    'DropDatabase'
    'GetSqlValue'
    'RunSSISPackage'
)
