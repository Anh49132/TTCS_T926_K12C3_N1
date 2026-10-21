param([string]$Password, [int]$Port = 5247)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$demoRoot = Join-Path $repoRoot 'data\sprint2-demo'
$token = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6)
$output = Join-Path $demoRoot $token
$runtime = Join-Path (Join-Path $repoRoot 'data\sprint2-runtime') $token
Push-Location $repoRoot
try {
    if ([string]::IsNullOrWhiteSpace($Password)) {
        $randomBytes = New-Object byte[] 8
        $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $generator.GetBytes($randomBytes) } finally { $generator.Dispose() }
        $random = [BitConverter]::ToString($randomBytes).Replace('-', '')
        $Password = "Demo-$random-a1"
    }
    dotnet build .\QL_PhongTro\QL_PhongTro.csproj -o $runtime
    if ($LASTEXITCODE -ne 0) { throw 'Build thất bại; chưa tạo dữ liệu mẫu.' }
    dotnet (Join-Path $runtime 'QL_PhongTro.dll') --contentRoot (Join-Path $repoRoot 'QL_PhongTro') --environment Development --Sprint2Demo:Directory $output --Sprint2Demo:Password $Password --Sprint2Demo:Url "http://localhost:$Port" --create-sprint2-demo
    if ($LASTEXITCODE -ne 0) { throw "Tạo dữ liệu mẫu thất bại. Giữ thư mục để kiểm tra: $output" }

    $accessPath = Join-Path $output 'access.json'
    $access = Get-Content -LiteralPath $accessPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $access | Add-Member -NotePropertyName runtime -NotePropertyValue (Join-Path $runtime 'QL_PhongTro.dll')
    $access | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $accessPath -Encoding UTF8
    $stdout = Join-Path $output 'prepare-server.out.log'
    $stderr = Join-Path $output 'prepare-server.err.log'
    $serverArgs = @(
        (Join-Path $runtime 'QL_PhongTro.dll'), '--environment', 'Development', '--urls', $access.url,
        '--DatabasePath', $access.database, '--RoomImagesPath', $access.roomImagesPath,
        '--DataProtectionKeysPath', (Join-Path $output 'keys'),
        '--PasswordReset:PickupDirectory', (Join-Path $output 'mail'),
        '--Logging:EventLog:LogLevel:Default', 'None'
    )
    $server = Start-Process dotnet -ArgumentList $serverArgs -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    try {
        python .\verification\upload_sprint2_demo_images.py $accessPath
        if ($LASTEXITCODE -ne 0) { throw 'Upload ảnh mẫu qua HTTP thất bại.' }
        python .\verification\verify_sprint2_demo.py --access $accessPath
        if ($LASTEXITCODE -ne 0) { throw 'Xác minh fixture Sprint 2 thất bại.' }
    } finally {
        if (!$server.HasExited) { Stop-Process -Id $server.Id }
    }

    Set-Content -LiteralPath (Join-Path $demoRoot 'latest.txt') -Value $output -Encoding utf8
    $access = Get-Content -LiteralPath $accessPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Write-Host "`nDữ liệu mẫu: $output"
    $access.accounts | Format-Table email,role
    Write-Host "Email, role và mật khẩu chung: $accessPath"
    Write-Host "Báo cáo dữ liệu: $(Join-Path $output 'report.md')"
    Write-Host "Chạy: .\verification\Start-Sprint2Demo.ps1"
} finally { Pop-Location }
