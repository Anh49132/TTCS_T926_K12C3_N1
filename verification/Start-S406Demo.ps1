param([string]$Directory)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (!$Directory) { $Directory = (Get-Content -LiteralPath (Join-Path $repoRoot 'data\s406\latest.txt') -Raw -Encoding utf8).Trim() }
$access = Get-Content -LiteralPath (Join-Path $Directory 'access.json') -Raw -Encoding utf8 | ConvertFrom-Json
if (!(Test-Path -LiteralPath $access.runtime)) { throw 'S4-06 demo runtime not found. See docs/S4-06-bao-hong.md.' }
Write-Host "URL: $($access.url) | Database: $($access.database)"
Write-Host 'Demo accounts and passwords are in access.json in the directory above.'
dotnet $access.runtime --contentRoot (Join-Path $repoRoot 'QL_PhongTro') --environment Development --urls $access.url `
    --DatabasePath $access.database --RoomImagesPath $access.roomImagesPath `
    --DataProtectionKeysPath (Join-Path $Directory 'keys') --PasswordReset:PickupDirectory (Join-Path $Directory 'mail') `
    --Logging:EventLog:LogLevel:Default None --Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command Warning
if ($LASTEXITCODE -ne 0) { throw 'Demo server stopped with an error; see the output above.' }
