param([string]$Directory, [int]$Port = 5254)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Directory)) {
    $Directory = (Get-Content -LiteralPath (Join-Path $repoRoot 'data/s4054-demo/latest.txt') -Raw -Encoding UTF8).Trim()
}
$database = Join-Path $Directory 's404.sqlite'
if (!(Test-Path -LiteralPath $database)) { throw 'Chưa có demo. Chạy ./verification/New-S4054Demo.ps1 trước.' }
$runtime = Join-Path $repoRoot ('data/s4054-runtime/' + [guid]::NewGuid().ToString('N'))
dotnet build (Join-Path $repoRoot 'QL_PhongTro/QL_PhongTro.csproj') -p:NuGetAudit=false -o $runtime
if ($LASTEXITCODE -ne 0) { throw 'Build web không thành công.' }
& dotnet (Join-Path $runtime 'QL_PhongTro.dll') --contentRoot (Join-Path $repoRoot 'QL_PhongTro') --environment Development --DatabasePath $database --check-database
if ($LASTEXITCODE -ne 0) { throw 'Demo không khớp schema. Tạo bộ demo mới; không nâng cấp CSDL gốc.' }
Write-Host "Web: http://localhost:$Port/TatToan/XacNhan/1"
& dotnet (Join-Path $runtime 'QL_PhongTro.dll') --contentRoot (Join-Path $repoRoot 'QL_PhongTro') --environment Development --urls "http://localhost:$Port" --DatabasePath $database --DataProtectionKeysPath (Join-Path $Directory 'keys') --PasswordReset:PickupDirectory (Join-Path $Directory 'mail') --PasswordReset:From noreply@example.test --PasswordReset:PublicBaseUrl "http://localhost:$Port" --Logging:EventLog:LogLevel:Default None
