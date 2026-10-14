param([string]$Directory)
$ErrorActionPreference='Stop'
$repoPath=Split-Path $PSScriptRoot -Parent
if(-not $Directory){$Directory=Join-Path $repoPath ('data/s404-task2-demo/'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,6))}
$Directory=[System.IO.Path]::GetFullPath($Directory)
if(Test-Path -LiteralPath $Directory){throw 'Refusing to overwrite an existing demo directory.'}
$demoProject=Join-Path $PSScriptRoot 'S404Demo/S404Demo.csproj'
$generatorOutput=Join-Path $repoPath ('data/s404-generator/'+[guid]::NewGuid().ToString('N'))
dotnet restore $demoProject --source https://api.nuget.org/v3/index.json
if($LASTEXITCODE -ne 0){throw 'Dependency restore failed.'}
dotnet build $demoProject --no-restore -o $generatorOutput
if($LASTEXITCODE -ne 0){throw 'Demo generator build failed.'}
dotnet (Join-Path $generatorOutput 'S404Demo.dll') (Join-Path $repoPath 'QL_PhongTro') $Directory
if($LASTEXITCODE -ne 0){throw 'Demo creation failed.'}
$demoRoot=Join-Path $repoPath 'data/s404-task2-demo'
New-Item -ItemType Directory -Force -Path $demoRoot | Out-Null
Set-Content -LiteralPath (Join-Path $demoRoot 'latest.txt') -Value $Directory
