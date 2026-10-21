param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$runtime = Join-Path $repoRoot ('data/s405-runtime/' + [guid]::NewGuid().ToString('N'))
$directory = Join-Path $repoRoot ('data/s405-demo/' + [guid]::NewGuid().ToString('N'))
dotnet build (Join-Path $repoRoot 'verification/S404Demo/S404Demo.csproj') -p:NuGetAudit=false -o $runtime
if ($LASTEXITCODE -ne 0) { throw 'Build demo không thành công.' }
& dotnet (Join-Path $runtime 'S404Demo.dll') (Join-Path $repoRoot 'QL_PhongTro') $directory task4
if ($LASTEXITCODE -ne 0) { throw 'Không tạo được demo S4-05.' }
Set-Content -LiteralPath (Join-Path $repoRoot 'data/s405-demo/latest.txt') -Value $directory -Encoding UTF8
Write-Host "Demo mới: $directory"
Write-Host "Tài khoản/mật khẩu: $(Join-Path $directory 'access.json')"
Write-Host 'Chạy web: ./verification/Start-S405Demo.ps1'
