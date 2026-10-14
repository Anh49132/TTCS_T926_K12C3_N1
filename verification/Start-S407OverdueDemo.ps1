param([string]$Directory)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (!$Directory) { $Directory = (Get-Content -LiteralPath (Join-Path $repoRoot 'data\s407-overdue\latest.txt') -Raw -Encoding utf8).Trim() }
& (Join-Path $PSScriptRoot 'Start-S406Demo.ps1') -Directory $Directory
