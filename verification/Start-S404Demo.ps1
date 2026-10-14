param([string]$Directory,[int]$Port=5252,[switch]$Task3)
$ErrorActionPreference='Stop'
$repoPath=Split-Path $PSScriptRoot -Parent
$latestFile=if($Task3){'data/s404-task3-demo/latest.txt'}else{'data/s404-task2-demo/latest.txt'}
if(-not $Directory){$Directory=(Get-Content -LiteralPath (Join-Path $repoPath $latestFile) -Raw).Trim()}
$demoDb=Join-Path $Directory 's404.sqlite'
if(-not(Test-Path -LiteralPath $demoDb)){throw 'Demo database not found. Run New-S404Demo.ps1 first.'}
$env:DatabasePath=$demoDb
$env:ASPNETCORE_ENVIRONMENT='Development'
$runtimeOutput=Join-Path $Directory ('runtime-'+[guid]::NewGuid().ToString('N'))
dotnet build (Join-Path $repoPath 'QL_PhongTro/QL_PhongTro.csproj') --no-restore -o $runtimeOutput
if($LASTEXITCODE -ne 0){throw 'Application build failed.'}
dotnet (Join-Path $runtimeOutput 'QL_PhongTro.dll') --contentRoot (Join-Path $repoPath 'QL_PhongTro') --urls "http://localhost:$Port"
