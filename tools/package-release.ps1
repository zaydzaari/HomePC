[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$release=Join-Path $root 'release'
$zip=Join-Path $release 'HomePC-Google-Home.zip'
New-Item -ItemType Directory -Force -Path $release | Out-Null
if (Test-Path $zip) { Remove-Item -LiteralPath $zip }
$tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$staging=Join-Path $tempRoot ('homepc-package-' + [guid]::NewGuid())
$resolvedStaging=[IO.Path]::GetFullPath($staging)
if (-not $resolvedStaging.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolvedStaging -Leaf) -notlike 'homepc-package-*') { throw 'Unsafe staging path.' }
New-Item -ItemType Directory -Path $staging | Out-Null
try {
  $destination=Join-Path $staging 'HomePC'; New-Item -ItemType Directory -Path $destination | Out-Null
  robocopy $root $destination /E /XD generated logs node_modules bin obj .wrangler release .git work /XF *.user *.log HomePC-Google-Home-Link-QR.png | Out-Null
  if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
  $unexpected=Get-ChildItem $destination -Recurse -File | Where-Object { $_.Name -eq 'worker-secrets.json' -or $_.FullName -match '\\generated\\' }
  if ($unexpected) { throw 'Secret/generated files entered the release staging folder.' }
  Compress-Archive -LiteralPath $destination -DestinationPath $zip -CompressionLevel Optimal
} finally { if (Test-Path $staging) { Remove-Item -LiteralPath $staging -Recurse -Force } }
Write-Host "Created $zip"
exit 0
