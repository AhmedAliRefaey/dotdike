#requires -Version 5.1
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskSetup = Join-Path $taskRoot 'tools\configure-odoo-local.ps1'
$taskRunRoot = Join-Path $taskRoot ('artifacts\odoo-local-config-check-' + [guid]::NewGuid().ToString('N'))
$taskAppData = Join-Path $taskRunRoot 'profile'
New-Item -ItemType Directory -Path $taskAppData -Force | Out-Null
[xml]$taskProject = Get-Content -LiteralPath (Join-Path $taskRoot 'src\Dike.Web\Dike.Web.csproj') -Raw
$taskSecretId = [string]$taskProject.Project.PropertyGroup.UserSecretsId
$taskStore = Join-Path $taskAppData "Microsoft\UserSecrets\$taskSecretId\secrets.json"
$taskSettingsPath = Join-Path $taskRoot 'src\Dike.Web\appsettings.json'
$taskSettingsHash = (Get-FileHash -LiteralPath $taskSettingsPath -Algorithm SHA256).Hash
$taskPreviousAppData = $env:APPDATA
$taskPreviousBaseUrl = $env:Odoo__BaseUrl
$script:taskOverrideBaseUrl = $null
$script:taskFakeKey = 'synthetic-odoo-key-for-local-config-check'
$script:taskChecks = 0

function Check([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw "FAIL $Name" }
    $script:taskChecks++
    Write-Output "PASS $Name"
}

# Simulate only the private input. Storage and Secret Manager are real.
function Read-Host {
    param([string]$Prompt, [switch]$AsSecureString)
    if (-not $AsSecureString) { throw 'The API-key prompt must be masked.' }
    $taskValue = New-Object System.Security.SecureString
    foreach ($taskCharacter in $taskFakeKey.ToCharArray()) { $taskValue.AppendChar($taskCharacter) }
    return $taskValue
}

function Invoke-Setup([switch]$CheckOnly) {
    $taskLines = New-Object 'System.Collections.Generic.List[string]'
    $taskFailed = $false
    $taskArguments = @{}
    if ($script:taskOverrideBaseUrl) { $taskArguments.BaseUrl = $script:taskOverrideBaseUrl }
    try {
        if ($CheckOnly) { & $taskSetup -Check @taskArguments *>&1 | ForEach-Object { $taskLines.Add([string]$_) } }
        else { & $taskSetup @taskArguments *>&1 | ForEach-Object { $taskLines.Add([string]$_) } }
    }
    catch { $taskFailed = $true; $taskLines.Add($_.Exception.Message) }
    return [pscustomobject]@{ Failed = $taskFailed; Text = [string]::Join("`n", $taskLines) }
}

try {
    # Redirect only this process and its children; never read the real user store.
    $env:APPDATA = $taskAppData
    $env:Odoo__BaseUrl = 'https://setup-fixture.example.test'
    $taskResult = Invoke-Setup -CheckOnly
    Check ($taskResult.Failed -and $taskResult.Text.Contains('API key stored: no')) 'Missing configuration fails with a redacted status'
    Check (-not (Test-Path -LiteralPath $taskStore)) 'Check does not create a secrets file'

    '{"Unrelated:Setting":"synthetic-existing-value"}' | dotnet user-secrets set --id $taskSecretId | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not prepare isolated store.' }
    $taskResult = Invoke-Setup
    Check (-not $taskResult.Failed) 'Masked setup saves through the real Secret Manager'
    Check (-not $taskResult.Text.Contains($script:taskFakeKey)) 'Setup output does not disclose the key'
    $taskStored = Get-Content -LiteralPath $taskStore -Raw | ConvertFrom-Json
    Check ($taskStored.'Odoo:BaseUrl' -ceq $env:Odoo__BaseUrl -and $taskStored.'Odoo:ApiKey' -ceq $script:taskFakeKey) 'Environment target and exact key reach the isolated store'
    Check ($taskStored.'Unrelated:Setting' -ceq 'synthetic-existing-value') 'Existing unrelated settings survive setup'
    $taskHash = (Get-FileHash -LiteralPath $taskStore).Hash
    $taskResult = Invoke-Setup -CheckOnly
    Check (-not $taskResult.Failed -and $taskResult.Text.Contains('API key stored: yes') -and -not $taskResult.Text.Contains($script:taskFakeKey)) 'Check succeeds without disclosing the key'
    Check ((Get-FileHash -LiteralPath $taskStore).Hash -eq $taskHash) 'Check leaves the store unchanged'

    $script:taskFakeKey = 'synthetic-rotated-key'
    $taskResult = Invoke-Setup
    $taskStored = Get-Content -LiteralPath $taskStore -Raw | ConvertFrom-Json
    Check (-not $taskResult.Failed -and $taskStored.'Odoo:ApiKey' -ceq $script:taskFakeKey -and $taskStored.'Unrelated:Setting' -ceq 'synthetic-existing-value') 'Rotation changes only the Odoo settings'
    $taskHash = (Get-FileHash -LiteralPath $taskStore).Hash
    $script:taskFakeKey = ''
    $taskResult = Invoke-Setup
    Check ($taskResult.Failed -and (Get-FileHash -LiteralPath $taskStore).Hash -eq $taskHash) 'Empty input preserves existing credentials'
    $script:taskFakeKey = "synthetic`ninvalid-key"
    $taskResult = Invoke-Setup
    Check ($taskResult.Failed -and -not $taskResult.Text.Contains('invalid-key') -and (Get-FileHash -LiteralPath $taskStore).Hash -eq $taskHash) 'Invalid input is rejected without disclosure or mutation'

    '{"Odoo:BaseUrl":"https://different.invalid"}' | dotnet user-secrets set --id $taskSecretId | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not prepare mismatched target.' }
    $taskResult = Invoke-Setup -CheckOnly
    Check ($taskResult.Failed -and $taskResult.Text.Contains('Stored endpoint valid and matches requested target: no')) 'A different stored target fails readiness'

    $script:taskFakeKey = 'synthetic-new-endpoint-key'
    $env:Odoo__BaseUrl = 'https://another-fixture.example.test:8443/'
    $taskResult = Invoke-Setup
    $taskStored = Get-Content -LiteralPath $taskStore -Raw | ConvertFrom-Json
    Check (-not $taskResult.Failed -and $taskStored.'Odoo:BaseUrl' -ceq 'https://another-fixture.example.test:8443') 'Changing the environment updates the normalized endpoint'
    $script:taskOverrideBaseUrl = 'https://explicit-fixture.example.test'
    $taskResult = Invoke-Setup
    $taskStored = Get-Content -LiteralPath $taskStore -Raw | ConvertFrom-Json
    Check (-not $taskResult.Failed -and $taskStored.'Odoo:BaseUrl' -ceq $script:taskOverrideBaseUrl) 'Explicit BaseUrl overrides the environment during setup'
    $script:taskOverrideBaseUrl = $null
    $taskHash = (Get-FileHash -LiteralPath $taskStore).Hash
    foreach ($taskInvalidEndpoint in @('', 'http://fixture.example.test', 'https://fixture.example.test/odoo', 'https://user@fixture.example.test')) {
        $env:Odoo__BaseUrl = $taskInvalidEndpoint
        $taskResult = Invoke-Setup
        Check ($taskResult.Failed -and (Get-FileHash -LiteralPath $taskStore).Hash -eq $taskHash) 'Missing or invalid endpoint preserves existing settings'
    }
    $env:Odoo__BaseUrl = $null
    $taskResult = Invoke-Setup -CheckOnly
    Check (-not $taskResult.Failed) 'Check validates a stored endpoint when no target override is supplied'

    Set-Content -LiteralPath $taskStore -Value 'malformed-json-synthetic-secret' -Encoding UTF8
    $taskResult = Invoke-Setup -CheckOnly
    Check ($taskResult.Failed -and -not $taskResult.Text.Contains('malformed-json-synthetic-secret')) 'Corrupt-store errors do not expose stored content'
    Check ((Get-FileHash -LiteralPath $taskSettingsPath -Algorithm SHA256).Hash -eq $taskSettingsHash) 'Tracked appsettings remains unchanged'
    Write-Output "All $script:taskChecks Odoo local-configuration checks passed. Synthetic store only; no Odoo requests."
}
finally {
    $env:APPDATA = $taskPreviousAppData
    $env:Odoo__BaseUrl = $taskPreviousBaseUrl
    $script:taskFakeKey = $null
}
