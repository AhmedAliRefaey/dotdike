param([string]$OutputPath)

$ErrorActionPreference = 'Stop'
if (-not $OutputPath) {
    $taskSecretFolder = Join-Path $PSScriptRoot '..\artifacts\vercel-secrets'
    $OutputPath = Join-Path $taskSecretFolder ('data-protection-' + [Guid]::NewGuid().ToString('N') + '.json')
}
$taskSecretPath = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $taskSecretPath) { throw 'The output already exists. Existing encryption keys were not replaced.' }
$null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskSecretPath))
$taskRsa = [Security.Cryptography.RSA]::Create(3072)
$taskRandom = [Security.Cryptography.RandomNumberGenerator]::Create()
$taskPasswordBytes = New-Object byte[] 32
$taskRandom.GetBytes($taskPasswordBytes)
$taskPassword = [Convert]::ToBase64String($taskPasswordBytes)
$taskCertificate = $null
try {
    $taskRequest = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
        'CN=Dike Data Protection', $taskRsa, [Security.Cryptography.HashAlgorithmName]::SHA256,
        [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $taskCertificate = $taskRequest.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddYears(5))
    $taskPfx = $taskCertificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $taskPassword)
    $taskValues = [ordered]@{
        DataProtection__CertificateBase64 = [Convert]::ToBase64String($taskPfx)
        DataProtection__CertificatePassword = $taskPassword
    }
    [IO.File]::WriteAllText($taskSecretPath, ($taskValues | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    Write-Output ('Secret values saved to ' + $taskSecretPath)
    Write-Output 'Add both values as sensitive Vercel environment variables. Keep a secure backup; reuse them for subsequent deployments.'
}
finally {
    if ($taskCertificate) { $taskCertificate.Dispose() }
    $taskRsa.Dispose()
    $taskRandom.Dispose()
    [Array]::Clear($taskPasswordBytes, 0, $taskPasswordBytes.Length)
}
