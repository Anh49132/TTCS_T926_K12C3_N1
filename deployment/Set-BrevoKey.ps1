[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$secret = Read-Host 'Dán Brevo API key (nội dung được ẩn)' -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
try {
    $value = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    if ([string]::IsNullOrWhiteSpace($value)) { throw 'Key rỗng; chưa lưu.' }
    [Environment]::SetEnvironmentVariable('Brevo__ApiKey', $value, 'User')
    $env:Brevo__ApiKey = $value
    Write-Output 'Đã lưu Brevo__ApiKey vào User environment. Không ghi key ra file hoặc nhật ký.'
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    $value = $null
    $secret.Dispose()
}
