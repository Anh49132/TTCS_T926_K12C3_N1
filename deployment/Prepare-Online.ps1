[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$stamp = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
$context = Join-Path $repo "data/online/$stamp"
$publish = Join-Path $context 'app'
New-Item -ItemType Directory -Path $publish -Force | Out-Null

# SkiaSharp is MIT licensed; no build-time license key is required.
& dotnet publish (Join-Path $repo 'QL_PhongTro/QL_PhongTro.csproj') `
    -c Release -r linux-x64 --no-self-contained -p:UseAppHost=false --nologo -o $publish `
    "-p:CustomAfterMicrosoftCommonTargets=$(Join-Path $PSScriptRoot 'ReleasePublish.targets')"
if ($LASTEXITCODE -ne 0) {
    throw 'Publish Release thất bại; chưa tạo bộ triển khai. Kiểm tra lỗi restore/build.'
}

# Web SDK may include local content. Reject private files before any upload.
# Do not delete or overwrite local source data to make this check pass.
$privateFiles = @(Get-ChildItem $publish -File -Recurse | Where-Object {
    $relative = $_.FullName.Substring($publish.Length).Replace('\', '/')
    $_.Name -match '(?i)\.(sqlite3?|db)(-|\.|$)|\.bak$|\.lic$|^\.env|^access\.json$|^appsettings\.(?!json$).*\.json$' -or
    $relative -match '(?i)/(App_Data|uploads|keys|email-pickup)/'
})
if ($privateFiles.Count -gt 0) {
    throw 'Publish chứa dữ liệu/cấu hình local. Không upload. Cần loại nội dung local bằng cấu hình publish trước khi chạy lại.'
}

foreach ($name in @('Dockerfile', 'start.sh', 'railway.json')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $context $name)
}
Write-Output "Bộ triển khai Release (không chứa database): $context"
