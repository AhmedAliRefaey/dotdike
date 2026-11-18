$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskTool = Join-Path $taskRoot 'tools\Dike.NetSuiteValidation\bin\Debug\net10.0\Dike.NetSuiteValidation.dll'
$taskExamples = Join-Path $taskRoot 'tools\Dike.NetSuiteValidation\examples'
$taskOutput = Join-Path $taskRoot ('artifacts\gl-validation-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskOutput | Out-Null

function Invoke-Validation([int]$expected, [string[]]$toolArguments) {
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & dotnet $taskTool @toolArguments 2>&1 | Out-Null
        $actual = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previousPreference }
    if ($actual -ne $expected) { throw "Expected exit $expected but got $actual." }
}
function Check([bool]$condition, [string]$name) {
    if (-not $condition) { throw "FAIL $name" }
    Write-Output "PASS $name"
}

$extract = Join-Path $taskExamples 'extract.synthetic.json'
$reference = Join-Path $taskExamples 'reference.synthetic.json'
$contractPath = Join-Path $taskOutput 'contract.json'
$reportPath = Join-Path $taskOutput 'comparison.json'
Invoke-Validation 0 @('contract', $contractPath)
$contract = Get-Content -LiteralPath $contractPath -Raw -Encoding UTF8 | ConvertFrom-Json
Check ($contract.Status -eq 'CandidateUnverified' -and $contract.QuerySha256.Length -eq 64) 'CLI writes candidate contract without approval'

Invoke-Validation 0 @('compare', $extract, $reference, $reportPath)
$report = Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
Check ($report.Result.Matches -and $report.Result.ExtractTotals.Rows -eq 2 -and $report.Result.ExtractTotals.SignedAmount -eq 100) 'CLI compares all synthetic rows and totals'
Check ($report.Result.AcceptanceStatus -eq 'AwaitingLiveValidationAndFinanceReview') 'CLI agreement keeps live acceptance pending'
Check ($report.ExtractFileSha256 -eq (Get-FileHash -LiteralPath $extract -Algorithm SHA256).Hash -and $report.ReferenceFileSha256 -eq (Get-FileHash -LiteralPath $reference -Algorithm SHA256).Hash) 'CLI pins exact input file hashes'

$mismatchPath = Join-Path $taskOutput 'reference-mismatch.json'
$mismatchReport = Join-Path $taskOutput 'mismatch-report.json'
(Get-Content -LiteralPath $reference -Raw -Encoding UTF8).Replace('SYNTHETIC-RECEIPT', 'CHANGED-REFERENCE') | Set-Content -LiteralPath $mismatchPath -Encoding UTF8
Invoke-Validation 1 @('compare', $extract, $mismatchPath, $mismatchReport)
$mismatch = Get-Content -LiteralPath $mismatchReport -Raw -Encoding UTF8 | ConvertFrom-Json
Check (-not $mismatch.Result.Matches -and 'ROW_MISMATCH' -in $mismatch.Result.Issues.Code) 'CLI fails row mismatch despite equal totals'

$invalidPath = Join-Path $taskOutput 'reference-invalid.json'
$invalidReport = Join-Path $taskOutput 'invalid-report.json'
(Get-Content -LiteralPath $reference -Raw -Encoding UTF8).Replace('125.50', '125.50000000000000000000000000000001') | Set-Content -LiteralPath $invalidPath -Encoding UTF8
Invoke-Validation 2 @('compare', $extract, $invalidPath, $invalidReport)
Check (-not (Test-Path -LiteralPath $invalidReport)) 'CLI rejects precision loss without writing a success report'

$reportHash = (Get-FileHash -LiteralPath $reportPath -Algorithm SHA256).Hash
Invoke-Validation 2 @('compare', $extract, $reference, $reportPath)
Check ((Get-FileHash -LiteralPath $reportPath -Algorithm SHA256).Hash -eq $reportHash) 'CLI preserves an existing comparison report'

$inputHash = (Get-FileHash -LiteralPath $extract -Algorithm SHA256).Hash
Invoke-Validation 2 @('compare', $extract, $reference, $extract)
Check ((Get-FileHash -LiteralPath $extract -Algorithm SHA256).Hash -eq $inputHash) 'CLI cannot overwrite an input'

$contractHash = (Get-FileHash -LiteralPath $contractPath -Algorithm SHA256).Hash
Invoke-Validation 2 @('contract', $contractPath)
Check ((Get-FileHash -LiteralPath $contractPath -Algorithm SHA256).Hash -eq $contractHash) 'CLI preserves an existing contract'

Invoke-Validation 2 @('unsupported-command')
Check $true 'CLI returns usage failure for an unsupported command'
Write-Output 'All NetSuite validation CLI smoke checks passed. Synthetic artifacts are isolated; no provider calls were made.'
