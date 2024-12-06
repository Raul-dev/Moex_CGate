
Param (
    [parameter(Mandatory=$false)][string]$TargetServerName="localhost",
    [parameter(Mandatory=$false)][string]$TargetDBname="cgate_uts_tmp", 
    [parameter(Mandatory=$false)][bool]$PublishOnly=$true,
    [parameter(Mandatory=$false)][bool]$IsRebuild=$true,
    [parameter(Mandatory=$false)][string]$SqlPassword=""
  )
Import-Module -Name './MSqlDeploymentFunc.psm1' -Verbose  
$Projectpath = Convert-Path ..
$ExitCode = 0
$VSProjectName = "CGate"
$LinkSRVLog = "LinkSRVLog"

try{

    $IsVS2019only = $false
    $obj= Get-MSVsInfo $IsVS2019only
    if($null -eq $obj) {
        Write-Host "Install visual studio first from https://visualstudio.microsoft.com/vs/community/"
        break
    }
    $vsLocation = $obj.InstallationPath
    Write-Host "Visual Studio Location:"
    Write-Host $vsLocation
            
    $devenv = $obj.InstallationPath + "\Common7\IDE\devenv.exe"
    $msbuildLocation = Get-MsBuildPath $IsVS2019only
	do {
		if($null -eq $msbuildLocation) {
			Write-Host "MSbuild not found"
			break
		}
		
		Write-Host "Visual Studio msbuild Location:"
		Write-Host $msbuildLocation
		
        $Project=$VSProjectName+".sqlproj"
        Set-Location -Path $Projectpath"\"


        Write-Host ""
        Write-Host "=========================================================" -foregroundcolor green
        Write-Host "Database: "$TargetDBname" Deployment" -foregroundcolor green
        Write-Host "DB Server: "$TargetServerName -foregroundcolor green
        Write-Host "=========================================================" -foregroundcolor green
    
        $Projectname = $VSProjectName #$Project.Split(".")[0]
        $sourceFile =$Projectpath+"\bin\Release\"+$Projectname+".dacpac"

        if (!(Test-Path $sourceFile) -or ($IsRebuild)) {

            Write-Host "Start BUILD "$VSProjectName
            Write-Host " -t:rebuild -p:WarningLevel=0 -p:NoWarn=SQL71562 -p:Configuration=Release "$Projectpath\$Project

            & "$msbuildLocation" -t:rebuild -p:WarningLevel=0 -p:NoWarn=SQL71562 -p:Configuration=Release $Projectpath\$Project

            IF ($LASTEXITCODE -ne 0){
                throw "Build failed."
            }
    
        
        } else {
             Write-Host ""
             Write-Host "Skip build. "$VSProjectName".dacpac exists." -foregroundcolor green
        }

        IF($PublishOnly -eq $false) {
			$res = DropDatabase $TargetDBname $TargetServerName $SQLuser $SQLpwd
			IF ($LASTEXITCODE -ne 0 -or $res -ne 0){
				throw "Drop database failed."
			}
		}
        Write-Host "Publish $($Projectname).dacpac"
        $sourceFile =$Projectpath+"\bin\Release\"+$Projectname+".dacpac"
        Write-Host $sourceFile
        Write-Host $vsLocation
        $SqlPackagePath = $env:Path -split ';'|?{$_ -and ((Test-Path -Path (Join-Path -Path $_ -ChildPath SqlPackage.exe) -PathType leaf))}
        if($SqlPackagePath){
            $SqlPackagePath = Join-Path -Path $SqlPackagePath -ChildPath SqlPackage.exe
        }else {
            if($IsVS2019only){
                #For VS2019
                $SqlPackagePath = "$vsLocation\Common7\IDE\Extensions\Microsoft\SQLDB\DAC\150\sqlpackage.exe"
            } else {
                #For VS2022
                $SqlPackagePath = "$vsLocation\Common7\IDE\Extensions\Microsoft\SQLDB\DAC\sqlpackage.exe"
            }
        }

		Write-Host "$SqlPackagePath /Action:Publish /SourceFile:$sourceFile /TargetServerName:$TargetServerName /TargetDatabaseName:$TargetDBname /TargetEncryptConnection:False /p:BlockOnPossibleDataLoss=False /p:IgnorePermissions=True /v:LinkSRVLog=$LinkSRVLog"
		& $SqlPackagePath /Action:Publish /SourceFile:$sourceFile /TargetServerName:$TargetServerName /TargetDatabaseName:$TargetDBname /TargetEncryptConnection:False /p:BlockOnPossibleDataLoss=False /p:IgnorePermissions=True /v:LinkSRVLog=$LinkSRVLog 
        IF ($LASTEXITCODE -ne 0){
            throw "Publish failed."
        }
    
        $ExitCode = 0
        
        Write-Host "Publish project "$VSProjectName" successfully"
       
        break
       
        #Write-Host -NoNewLine 'Press any key to continue...';
        #$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');

    } until( 1 -eq 0 )
}
catch {
  
    Write-Host "An error occurred:" -fore red
    Write-Host $_ -fore red
    Write-Host "Stack:"
    Write-Host $_.ScriptStackTrace
    $ExitCode = -1
}
$Projectpath = $Projectpath +"\ScriptsFolder"
Set-Location -Path $Projectpath
exit $ExitCode

