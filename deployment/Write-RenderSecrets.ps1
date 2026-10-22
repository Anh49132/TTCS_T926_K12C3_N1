[CmdletBinding()]
param([Parameter(Mandatory)][string]$Sender)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskDb = Get-Content -LiteralPath (Join-Path $taskRoot 'data/postgres-port/main-connection.json') -Raw | ConvertFrom-Json
$taskStorage = (Get-Content -LiteralPath (Join-Path $taskRoot 'data/postgres-port/storage-main.json') -Raw | ConvertFrom-Json).ObjectStorage
$taskUri = [uri]$taskDb.ConnectionUri
$taskUser = $taskUri.UserInfo.Split(':',2)
function Quote-ConnectionValue([string]$Value) { return '"' + $Value.Replace('"','""') + '"' }
$taskConnection = 'Host=' + (Quote-ConnectionValue $taskUri.DnsSafeHost) + ';Database=' + (Quote-ConnectionValue $taskUri.AbsolutePath.Trim('/')) + ';Username=' + (Quote-ConnectionValue ([uri]::UnescapeDataString($taskUser[0]))) + ';Password=' + (Quote-ConnectionValue ([uri]::UnescapeDataString($taskUser[1]))) + ';SSL Mode=VerifyFull;Include Error Detail=false;Maximum Pool Size=10'
$taskBrevo = [Environment]::GetEnvironmentVariable('Brevo__ApiKey','User')
if ([string]::IsNullOrWhiteSpace($taskBrevo)) { throw 'Brevo key not configured in User environment.' }
$taskEnv = [ordered]@{
    ASPNETCORE_ENVIRONMENT = 'Production'
    ASPNETCORE_HTTP_PORTS = '10000'
    DatabaseProvider = 'PostgreSql'
    DatabasePath = '/tmp/nhatot/cache-scope'
    IdentityImagePath = '/tmp/nhatot/identity'
    RoomImagesPath = '/tmp/nhatot/rooms'
    ConnectionStrings__PostgreSql = $taskConnection
    ObjectStorage__Enabled = 'true'
    ObjectStorage__Endpoint = $taskStorage.Endpoint
    ObjectStorage__Bucket = $taskStorage.Bucket
    ObjectStorage__Region = $taskStorage.Region
    ObjectStorage__AccessKey = $taskStorage.AccessKey
    ObjectStorage__SecretKey = $taskStorage.SecretKey
    ObjectStorage__KeyEncryptionKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    Jwt__SecretKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
    PasswordReset__Provider = 'Brevo'
    PasswordReset__From = $Sender
    PasswordReset__PublicBaseUrl = 'https://nhatot.onrender.com'
    PasswordReset__PickupDirectory = ''
    Brevo__ApiKey = $taskBrevo
    Brevo__SenderName = 'NhàTốt'
    Deployment__RenderProxy = 'true'
    Deployment__DisableBackgroundJobs = 'true'
    AllowedHosts = 'nhatot.onrender.com'
    'Logging__LogLevel__Microsoft.EntityFrameworkCore' = 'Critical'
}
$taskOutput = Join-Path $taskRoot 'data/postgres-port/render-env.json'
if (Test-Path -LiteralPath $taskOutput) { throw 'Host secrets already exist. Preserve key-ring master key across deployments.' }
@($taskEnv.GetEnumerator() | ForEach-Object { @{ key = $_.Key; value = $_.Value } }) | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $taskOutput -Encoding utf8
Write-Output 'Render secrets saved privately; values are not displayed. Background jobs disabled during verification.'
