[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$installRoot=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\HomePC'))
$expected=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs'))
if (-not $installRoot.StartsWith($expected,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe installation path.' }
Get-Process 'HomePC.Tray','HomePC.Agent','HomePC.Dashboard' -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item -LiteralPath (Join-Path ([Environment]::GetFolderPath('Startup')) 'HomePC.lnk') -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path ([Environment]::GetFolderPath('Programs')) 'HomePC.lnk') -Force -ErrorAction SilentlyContinue
if (Test-Path -LiteralPath $installRoot) { Remove-Item -LiteralPath $installRoot -Recurse -Force }
Write-Host 'HomePC application files were removed. Encrypted configuration remains in LocalAppData\HomePC for recovery.'
