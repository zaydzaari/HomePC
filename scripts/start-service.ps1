#Requires -RunAsAdministrator
$ErrorActionPreference='Stop'
Start-Service HomePCAgent
Get-Service HomePCAgent

