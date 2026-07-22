[CmdletBinding()]
param([string]$ConfigPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'generated\homepc.json'))
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$sourceConfig=[IO.Path]::GetFullPath($ConfigPath)
if (-not (Test-Path -LiteralPath $sourceConfig)) { throw 'Run tools/bootstrap.ps1 first or pass -ConfigPath.' }
$installRoot=Join-Path $env:LOCALAPPDATA 'Programs\HomePC'
$configRoot=Join-Path $env:LOCALAPPDATA 'HomePC\config'
Get-Process 'HomePC.Tray','HomePC.Agent','HomePC.Dashboard' -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item -ItemType Directory -Force -Path $installRoot,$configRoot | Out-Null
foreach ($project in 'HomePC.Agent','HomePC.Dashboard','HomePC.Tray') {
  dotnet publish (Join-Path $root "src\$project\$project.csproj") -c Release -r win-x64 --self-contained true -o $installRoot
  if ($LASTEXITCODE -ne 0) { throw "Publishing $project failed." }
}
$installedConfig=Join-Path $configRoot 'homepc.json'
Copy-Item -LiteralPath $sourceConfig -Destination $installedConfig -Force
$shell=New-Object -ComObject WScript.Shell
$startup=[Environment]::GetFolderPath('Startup')
$startMenu=Join-Path ([Environment]::GetFolderPath('Programs')) 'HomePC.lnk'
foreach ($shortcutPath in @((Join-Path $startup 'HomePC.lnk'),$startMenu)) {
  $shortcut=$shell.CreateShortcut($shortcutPath)
  $shortcut.TargetPath=Join-Path $installRoot 'HomePC.Tray.exe'
  $shortcut.Arguments="--config `"$installedConfig`""
  $shortcut.WorkingDirectory=$installRoot
  $shortcut.Description='HomePC Google Home control for Windows'
  $shortcut.Save()
}
Start-Process -FilePath (Join-Path $installRoot 'HomePC.Tray.exe') -ArgumentList @('--config',$installedConfig) -WorkingDirectory $installRoot -WindowStyle Hidden
Write-Host "HomePC installed for the current user in $installRoot"
