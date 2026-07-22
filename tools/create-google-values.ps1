[CmdletBinding()]
param([Parameter(Mandatory)][string]$WorkerUrl)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$path=Join-Path $root 'generated\google-home-values.txt'
if (-not (Test-Path $path)) { throw 'Run tools/bootstrap.ps1 first.' }
$content=(Get-Content -Raw -LiteralPath $path).Replace('WORKER_URL',$WorkerUrl.TrimEnd('/'))
$content | Set-Content -LiteralPath $path -Encoding utf8
Write-Host "Updated $path"

