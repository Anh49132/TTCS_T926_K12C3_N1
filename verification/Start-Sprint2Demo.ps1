param([string]$Directory)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (!$Directory) { $Directory = (Get-Content -LiteralPath (Join-Path $repoRoot 'data\sprint2-demo\latest.txt') -Raw -Encoding UTF8).Trim() }
$access = Get-Content -LiteralPath (Join-Path $Directory 'access.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$runtime = if ($access.runtime) { $access.runtime } else { Join-Path $repoRoot 'data\sprint2-demo\runtime\QL_PhongTro.dll' }
if (!(Test-Path -LiteralPath $runtime)) { throw 'Chưa có bản build. Chạy New-Sprint2Demo.ps1 trước.' }

$demoUrl = $access.url.TrimEnd('/')
$smtpVariables = @(
    $env:PasswordReset__Host,
    $env:PasswordReset__Username,
    $env:PasswordReset__Password,
    $env:PasswordReset__From
)
$smtpRequested = @($smtpVariables | Where-Object { ![string]::IsNullOrWhiteSpace($_) }).Count -gt 0

if ($smtpRequested) {
    $missingSmtpVariables = @()
    if ([string]::IsNullOrWhiteSpace($env:PasswordReset__Host)) { $missingSmtpVariables += 'PasswordReset__Host' }
    if ([string]::IsNullOrWhiteSpace($env:PasswordReset__Username)) { $missingSmtpVariables += 'PasswordReset__Username' }
    if ([string]::IsNullOrWhiteSpace($env:PasswordReset__Password)) { $missingSmtpVariables += 'PasswordReset__Password' }
    if ([string]::IsNullOrWhiteSpace($env:PasswordReset__From)) { $missingSmtpVariables += 'PasswordReset__From' }
    if ($missingSmtpVariables.Count -gt 0) {
        throw 'Cấu hình SMTP chưa đủ. Thiếu: ' + ($missingSmtpVariables -join ', ')
    }
}

try {
    $runningResponse = Invoke-WebRequest -UseBasicParsing -Uri ($demoUrl + '/TimTin') -TimeoutSec 3
    $isDemoRunning = $runningResponse.StatusCode -eq 200 -and
        $runningResponse.Content -match 'Tìm phòng trọ|T&#xEC;m ph&#xF2;ng tr&#x1ECD;'
} catch {
    $isDemoRunning = $false
}

if ($isDemoRunning) {
    if ($smtpRequested) {
        throw "Bản demo đang chạy sẵn tại $demoUrl. Hãy dừng phiên cũ bằng Ctrl+C rồi chạy lại script để nạp cấu hình SMTP."
    }
    Write-Host "Bản demo đang chạy sẵn tại $demoUrl"
    Write-Host "Mở: $demoUrl"
    Write-Host "Tài khoản: owner.demo@demo.local / $($access.password)"
    exit 0
}

$runtimeDirectory = Split-Path -Parent $runtime
$projectPath = Join-Path $repoRoot 'QL_PhongTro\QL_PhongTro.csproj'
$runtimeTimestamp = (Get-Item -LiteralPath $runtime).LastWriteTimeUtc
$sourceChanged = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'QL_PhongTro') -Recurse -File |
    Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and
        $_.Extension -in @('.cs', '.cshtml', '.csproj') -and
        $_.LastWriteTimeUtc -gt $runtimeTimestamp
    } |
    Select-Object -First 1
if ($sourceChanged) {
    Write-Host 'Mã nguồn đã thay đổi. Đang cập nhật bản build demo...'
    dotnet build $projectPath --no-restore --nologo -o $runtimeDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Không thể cập nhật bản build demo; xem lỗi phía trên.' }
}

Write-Host "URL: $($access.url) | Database: $($access.database)"
Write-Host "Tài khoản: owner.demo@demo.local / $($access.password)"
Write-Host 'Giữ terminal này mở. Nhấn Ctrl+C để dừng đúng phiên demo.'
Push-Location (Join-Path $repoRoot 'QL_PhongTro')
try {
    $runArguments = @(
        $runtime,
        '--environment', 'Development',
        '--urls', $access.url,
        '--DatabasePath', $access.database,
        '--RoomImagesPath', $access.roomImagesPath,
        '--DataProtectionKeysPath', (Join-Path $Directory 'keys'),
        '--PasswordReset:PublicBaseUrl', $demoUrl,
        '--Logging:EventLog:LogLevel:Default', 'None'
    )
    if ($smtpRequested) {
        # Whitespace overrides the Development pickup directory while the
        # application treats it as empty and therefore selects SMTP.
        $runArguments += @('--PasswordReset:PickupDirectory', ' ')
        Write-Host 'Email: SMTP (sẽ gửi tới hộp thư thật)'
    } else {
        $runArguments += @('--PasswordReset:PickupDirectory', (Join-Path $Directory 'mail'))
        Write-Host 'Email: PICKUP (chỉ lưu file thử nghiệm)'
    }
    dotnet @runArguments
    if ($LASTEXITCODE -ne 0) { throw 'Server đã dừng do lỗi; xem thông báo phía trên.' }
} finally { Pop-Location }
