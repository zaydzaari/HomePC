[CmdletBinding()]
param([Parameter(Mandatory)][string]$ConfigPath)
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath($PSScriptRoot)
$sourceConfig=[IO.Path]::GetFullPath($ConfigPath)
if (-not (Test-Path -LiteralPath $sourceConfig)) { throw 'Pass the DPAPI-protected homepc.json created by tools/bootstrap.ps1.' }
$installRoot=Join-Path $env:LOCALAPPDATA 'Programs\HomePC'
$configRoot=Join-Path $env:LOCALAPPDATA 'HomePC\config'
Get-Process 'HomePC.Tray','HomePC.Agent','HomePC.Dashboard' -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item -ItemType Directory -Force -Path $installRoot,$configRoot | Out-Null
Get-ChildItem -LiteralPath $source -File | Where-Object { $_.Name -notin @('install.ps1','homepc.example.json') } | Copy-Item -Destination $installRoot -Force
$installedConfig=Join-Path $configRoot 'homepc.json'
Copy-Item -LiteralPath $sourceConfig -Destination $installedConfig -Force
$shell=New-Object -ComObject WScript.Shell
foreach ($shortcutPath in @((Join-Path ([Environment]::GetFolderPath('Startup')) 'HomePC.lnk'),(Join-Path ([Environment]::GetFolderPath('Programs')) 'HomePC.lnk'))) {
  $shortcut=$shell.CreateShortcut($shortcutPath)
  $shortcut.TargetPath=Join-Path $installRoot 'HomePC.Tray.exe'
  $shortcut.Arguments="--config `"$installedConfig`""
  $shortcut.WorkingDirectory=$installRoot
  $shortcut.Description='HomePC Google Home control for Windows'
  $shortcut.Save()
}
Start-Process -FilePath (Join-Path $installRoot 'HomePC.Tray.exe') -ArgumentList @('--config',$installedConfig) -WorkingDirectory $installRoot -WindowStyle Hidden
Write-Host "HomePC installed for the current user in $installRoot"
