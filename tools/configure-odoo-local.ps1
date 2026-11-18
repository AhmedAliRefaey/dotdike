#requires -Version 5.1
<#
.SYNOPSIS
Stores the Odoo development API key in the current user's .NET User Secrets.
.DESCRIPTION
Set Odoo__BaseUrl or pass -BaseUrl, then enter or replace a key through a masked
prompt. Use -Check to report presence only. No Odoo requests are made.
User Secrets are outside the project but are not encrypted on disk.
#>
[CmdletBinding()]
param([switch]$Check, [string]$BaseUrl = $env:Odoo__BaseUrl)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$taskProjectPath = Join-Path $PSScriptRoot '..\src\Dike.Web\Dike.Web.csproj'
[xml]$taskProject = Get-Content -LiteralPath $taskProjectPath -Raw -Encoding UTF8
$taskSecretId = [string]$taskProject.Project.PropertyGroup.UserSecretsId
if ($taskSecretId -notmatch '\A[A-Za-z0-9-]+\z') {
    throw 'The web project must declare one valid UserSecretsId.'
}
$taskDotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
function Get-OdooRoot([string]$Value) {
    $taskUri = $null
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -match '\s|\p{C}|\\' -or
        -not [Uri]::TryCreate($Value, [UriKind]::Absolute, [ref]$taskUri) -or
        $taskUri.Scheme -ne 'https' -or $taskUri.Host.Length -eq 0 -or $taskUri.Port -le 0 -or
        $taskUri.UserInfo.Length -ne 0 -or $taskUri.AbsolutePath -ne '/' -or
        $taskUri.Query.Length -ne 0 -or $taskUri.Fragment.Length -ne 0) {
        throw 'Set Odoo__BaseUrl or pass -BaseUrl with a valid HTTPS root without credentials, a path, query or fragment.'
    }
    return $taskUri.GetLeftPart([UriPartial]::Authority)
}

function Invoke-LocalSecretManager {
    param(
        [ValidateSet('set', 'list')][string]$Operation,
        [string]$Payload
    )

    $taskStart = New-Object System.Diagnostics.ProcessStartInfo
    $taskStart.FileName = $taskDotnet
    $taskStart.Arguments = "user-secrets $Operation --id $taskSecretId"
    if ($Operation -eq 'list') { $taskStart.Arguments += ' --json' }
    $taskStart.UseShellExecute = $false
    $taskStart.CreateNoWindow = $true
    $taskStart.RedirectStandardInput = $true
    $taskStart.RedirectStandardOutput = $true
    $taskStart.RedirectStandardError = $true
    $taskProcess = New-Object System.Diagnostics.Process
    $taskProcess.StartInfo = $taskStart
    try {
        if (-not $taskProcess.Start()) { throw 'Could not start Secret Manager.' }
        $taskStdout = $taskProcess.StandardOutput.ReadToEndAsync()
        $taskStderr = $taskProcess.StandardError.ReadToEndAsync()
        # The JSON travels through stdin, never command-line arguments or a temp file.
        if ($Operation -eq 'set') { $taskProcess.StandardInput.Write($Payload) }
        $taskProcess.StandardInput.Close()
        if (-not $taskProcess.WaitForExit(30000)) {
            $taskProcess.Kill()
            throw 'Secret Manager timed out.'
        }
        $taskOutput = $taskStdout.GetAwaiter().GetResult()
        $null = $taskStderr.GetAwaiter().GetResult()
        if ($taskProcess.ExitCode -ne 0) { throw 'Secret Manager failed.' }
        return $taskOutput
    }
    catch {
        # Do not forward native output: it may contain configuration values.
        throw 'Local secret operation failed. Check that the .NET 10 SDK is installed and your user profile is writable.'
    }
    finally { $taskProcess.Dispose() }
}

if ($Check) {
    try {
        $taskJsonOutput = Invoke-LocalSecretManager -Operation list
        $taskMatch = [regex]::Match($taskJsonOutput, '(?s)//BEGIN\s*(\{.*\})\s*//END')
        if (-not $taskMatch.Success) { throw 'Unexpected Secret Manager response.' }
        $taskStored = ConvertFrom-Json -InputObject $taskMatch.Groups[1].Value
        $taskUrlProperty = $taskStored.PSObject.Properties['Odoo:BaseUrl']
        $taskKeyProperty = $taskStored.PSObject.Properties['Odoo:ApiKey']
        $taskCorrectTarget = $false
        try {
            $taskStoredUrl = Get-OdooRoot ([string]$taskUrlProperty.Value)
            $taskCorrectTarget = [string]::IsNullOrEmpty($BaseUrl) -or $taskStoredUrl -ceq (Get-OdooRoot $BaseUrl)
        } catch { $taskCorrectTarget = $false }
        $taskKeyPresent = $null -ne $taskKeyProperty -and -not [string]::IsNullOrWhiteSpace([string]$taskKeyProperty.Value)
    }
    catch { throw 'Could not check local Odoo settings. No configuration values were displayed.' }
    finally { $taskJsonOutput = $null; $taskStored = $null; $taskMatch = $null; $taskKeyProperty = $null }

    Write-Output ('Stored endpoint valid and matches requested target: ' + $(if ($taskCorrectTarget) { 'yes' } else { 'no' }))
    Write-Output ('API key stored: ' + $(if ($taskKeyPresent) { 'yes' } else { 'no' }))
    if (-not ($taskCorrectTarget -and $taskKeyPresent)) {
        throw 'Local Odoo configuration is incomplete. Run this script without -Check to configure it.'
    }
    Write-Output 'Local configuration is present. This check does not test Odoo authentication.'
    return
}

$taskBaseUrl = Get-OdooRoot $BaseUrl
Write-Output 'Configure local development for the supplied Odoo HTTPS endpoint.'
Write-Output 'The API key will be stored outside the project in .NET User Secrets (not encrypted on disk).'
$taskSecureKey = Read-Host 'Paste the Odoo API key (input hidden)' -AsSecureString
$taskKeyBuffer = [IntPtr]::Zero
$taskPlainKey = $null
$taskPayload = $null
try {
    if ($null -eq $taskSecureKey -or $taskSecureKey.Length -eq 0) { throw 'No API key entered; nothing was saved.' }
    $taskKeyBuffer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($taskSecureKey)
    $taskPlainKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($taskKeyBuffer)
    if ($taskPlainKey -match '\s|\p{C}') { throw 'The API key contains whitespace or control characters; nothing was saved.' }
    $taskPayload = @{
        'Odoo:BaseUrl' = $taskBaseUrl
        'Odoo:ApiKey' = $taskPlainKey
    } | ConvertTo-Json -Compress
    $null = Invoke-LocalSecretManager -Operation set -Payload $taskPayload
}
finally {
    if ($taskKeyBuffer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($taskKeyBuffer) }
    if ($null -ne $taskSecureKey) { $taskSecureKey.Dispose() }
    $taskPlainKey = $null
    $taskPayload = $null
}
Write-Output 'Saved Odoo development settings in your user profile. Existing unrelated settings were preserved.'
Write-Output 'Restart Dike.Web in Development mode to load them. No Odoo request was sent.'
