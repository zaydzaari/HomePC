#Requires -RunAsAdministrator
$ErrorActionPreference='Stop'
$service=Get-Service HomePCAgent -ErrorAction SilentlyContinue
if (-not $service) { Write-Host 'HomePCAgent is not installed.'; exit 0 }
if ($service.Status -ne 'Stopped') { Stop-Service HomePCAgent -Force }
sc.exe delete HomePCAgent | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Windows failed to delete HomePCAgent.' }
Write-Host 'HomePCAgent uninstalled.'

