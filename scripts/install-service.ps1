#Requires -RunAsAdministrator
[CmdletBinding()]
param([string]$ConfigPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'generated\homepc.json'))
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $root 'src\HomePC.Agent\bin\Release\net8.0\win-x64\publish\HomePC.Agent.exe'
if (-not (Test-Path $exe)) { throw "Agent executable not found. Run: dotnet publish src\HomePC.Agent -c Release -r win-x64 --self-contained false" }
if (-not (Test-Path $ConfigPath)) { throw "Configuration not found: $ConfigPath. Run tools\bootstrap.ps1 first." }
if (Get-Service HomePCAgent -ErrorAction SilentlyContinue) { throw 'HomePCAgent already exists. Uninstall it before reinstalling.' }
$binary='"' + $exe + '" --config "' + (Resolve-Path $ConfigPath) + '"'
New-Service -Name HomePCAgent -BinaryPathName $binary -DisplayName 'HomePC Agent' -Description 'Secure outbound Google Home control agent' -StartupType Automatic
Write-Host 'HomePCAgent installed. Run scripts\start-service.ps1.'
