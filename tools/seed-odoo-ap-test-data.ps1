#requires -Version 5.1
<#
.SYNOPSIS
Plans or posts a fixed, clearly labeled synthetic AP batch in the NApp trial.
.DESCRIPTION
Without -Apply this is read-only. -Apply creates and posts the reviewed test bills.
-IncludePayments also posts manual payment journal entries and reconciles AP lines.
No external payment service is called. Re-running validates and reuses matching references.
#>
[CmdletBinding()]
param([switch]$Apply, [switch]$IncludePayments)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskCompanyId = 1
$taskPrefix = 'DIKE-AP-TEST-2026'
$taskContext = @{ allowed_company_ids = @($taskCompanyId); lang = 'en_US' }
$taskClient = $null
$taskLease = $null
$taskManifestPath = $null
$taskCompleted = $false
$taskStage = 'local configuration'
$taskResults = New-Object 'System.Collections.Generic.List[object]'

function Invoke-Odoo([string]$Model, [string]$Method, [hashtable]$Body) {
    $script:taskStage = "$Model/$Method"
    $taskRead = $Method -in @('context_get', 'read', 'search_read', 'fields_get')
    $taskWriteAllowed = ($Model -eq 'res.partner' -and $Method -eq 'create') -or
        ($Model -eq 'account.move' -and $Method -in @('create','action_post')) -or
        ($Model -eq 'account.move.line' -and $Method -eq 'reconcile')
    if (-not $taskRead -and (-not $Apply -or -not $taskWriteAllowed)) { throw 'This operation is not allowed in the selected mode.' }
    if (-not $Body.ContainsKey('context')) { $Body['context'] = $taskContext }
    $taskRequest = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Post, "https://napp.odoo.com/json/2/$Model/$Method")
    $taskRequest.Content = New-Object System.Net.Http.StringContent(($Body | ConvertTo-Json -Depth 30 -Compress), [Text.Encoding]::UTF8, 'application/json')
    $taskResponse = $null
    try {
        $taskResponse = $taskClient.SendAsync($taskRequest).GetAwaiter().GetResult()
        if (-not $taskResponse.IsSuccessStatusCode) { throw "Odoo $Model/$Method failed with HTTP $([int]$taskResponse.StatusCode). Provider details were withheld." }
        $taskJson = $taskResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        try { $taskParsed = ConvertFrom-Json -InputObject $taskJson } catch { throw 'Odoo returned invalid JSON; response content was withheld.' }
        return $taskParsed
    }
    finally { if ($null -ne $taskResponse) { $taskResponse.Dispose() }; $taskRequest.Dispose() }
}

function Search-Odoo([string]$Model, [object[]]$Domain, [string[]]$Fields, [int]$Limit = 100) {
    $taskRows = @(Invoke-Odoo $Model 'search_read' @{ domain = $Domain; fields = $Fields; order = 'id asc'; limit = $Limit })
    if ($taskRows.Count -ge $Limit) { throw 'Discovery reached its row limit; narrow the query before proceeding.' }
    return $taskRows
}

function Test-Line([string]$Description, [decimal]$Quantity, [decimal]$UnitPrice) {
    return [pscustomobject]@{ Description = $Description; Quantity = $Quantity; UnitPrice = $UnitPrice }
}

function Save-Progress {
    if ($null -eq $taskManifestPath) { return }
    $taskManifest = [ordered]@{
        Schema = 'dike-synthetic-ap-v1'; Target = 'https://napp.odoo.com'; CompanyId = $taskCompanyId
        Synthetic = $true; Completed = $taskCompleted; IncludePayments = [bool]$IncludePayments
        RecordedAtUtc = [DateTimeOffset]::UtcNow.ToString('O'); Records = $taskResults.ToArray()
    }
    [IO.File]::WriteAllText($taskManifestPath, ($taskManifest | ConvertTo-Json -Depth 15), (New-Object Text.UTF8Encoding($false)))
}

function Create-One([string]$Model, [hashtable]$Values) {
    $taskIds = @(Invoke-Odoo $Model 'create' @{ vals_list = @($Values) })
    if ($taskIds.Count -ne 1 -or [int]$taskIds[0] -le 0) { throw 'Creation did not return one valid record ID. Inspect the reference before retrying.' }
    return [int]$taskIds[0]
}

function Get-Move([string]$Reference) {
    $taskMoves = @(Search-Odoo 'account.move' @(@('company_id','=',$taskCompanyId), @('ref','=',$Reference)) @('id','name','ref','state','date','invoice_date','move_type','partner_id','currency_id','company_id','journal_id','amount_total','amount_tax','amount_residual','payment_state') 3)
    if ($taskMoves.Count -gt 1) { throw 'A synthetic reference is not unique; no duplicate will be created.' }
    if ($taskMoves.Count -eq 1) { return $taskMoves[0] }
    return $null
}

function Check-Move($Move, [string]$Reference, [string]$Date, [string]$Type, [int]$JournalId, [int]$PartnerId) {
    if ($null -eq $Move -or $Move.ref -cne $Reference -or $Move.date -cne $Date -or $Move.move_type -cne $Type -or
        $Move.company_id[0] -ne $taskCompanyId -or $Move.currency_id[0] -ne $taskCurrencyId -or
        $Move.journal_id[0] -ne $JournalId -or $Move.partner_id[0] -ne $PartnerId -or $Move.state -notin @('draft','posted')) {
        throw 'An existing or newly created synthetic move does not match its expected identity/scope.'
    }
}

function Get-MoveLines([int]$MoveId) {
    return Search-Odoo 'account.move.line' @(@('move_id','=',$MoveId), @('company_id','=',$taskCompanyId)) @('id','account_id','partner_id','name','debit','credit','balance','amount_residual','reconciled','quantity','price_unit','price_subtotal','display_type','currency_id','company_id') 20
}

function Post-Move($Move) {
    if ($Move.state -eq 'draft') { $null = Invoke-Odoo 'account.move' 'action_post' @{ ids = @([int]$Move.id) } }
    $taskPosted = Get-Move ([string]$Move.ref)
    if ($null -eq $taskPosted -or $taskPosted.state -cne 'posted') { throw 'Posting did not leave the synthetic transaction in posted state.' }
    return $taskPosted
}

$taskBatch = @(
    [pscustomobject]@{ Ref = "$taskPrefix-OCT-01"; Date = '2026-10-06'; Lines = @((Test-Line 'Synthetic supplies' 3 125.50), (Test-Line 'Synthetic delivery' 1 75)); Payment = [decimal]451.50; PaymentDate = '2026-10-08'; Bank = 118; Scenario = 'Multiple invoice lines; fully paid' },
    [pscustomobject]@{ Ref = "$taskPrefix-OCT-02"; Date = '2026-10-20'; Lines = @((Test-Line 'Synthetic services' 2 450)); Payment = [decimal]450; PaymentDate = '2026-10-23'; Bank = 119; Scenario = 'Partial payment; 450 SAR remains' },
    [pscustomobject]@{ Ref = "$taskPrefix-OCT-03"; Date = '2026-10-28'; Lines = @((Test-Line 'Synthetic maintenance' 1 1250)); Payment = [decimal]0; PaymentDate = $null; Bank = $null; Scenario = 'Unpaid' },
    [pscustomobject]@{ Ref = "$taskPrefix-NOV-01"; Date = '2026-11-03'; Lines = @((Test-Line 'Synthetic goods' 4 225)); Payment = [decimal]900; PaymentDate = '2026-11-04'; Bank = 118; Scenario = 'Same 900 SAR bill total as October, distinct identity; fully paid' },
    [pscustomobject]@{ Ref = "$taskPrefix-NOV-02"; Date = '2026-11-10'; Lines = @((Test-Line 'Synthetic training' 2 375.75)); Payment = [decimal]400; PaymentDate = '2026-11-12'; Bank = 119; Scenario = 'Fractional SAR unit price; partial payment; 351.50 SAR remains' },
    [pscustomobject]@{ Ref = "$taskPrefix-NOV-03"; Date = '2026-11-16'; Lines = @((Test-Line 'Synthetic software' 3 500)); Payment = [decimal]0; PaymentDate = $null; Bank = $null; Scenario = 'Unpaid' }
)
foreach ($taskBill in $taskBatch) {
    $taskTotal = [decimal]0
    foreach ($taskLine in $taskBill.Lines) { $taskTotal += $taskLine.Quantity * $taskLine.UnitPrice }
    $taskBill | Add-Member -NotePropertyName Total -NotePropertyValue $taskTotal
}

try {
    [xml]$taskProject = Get-Content -LiteralPath (Join-Path $taskRoot 'src\Dike.Web\Dike.Web.csproj') -Raw
    $taskSecretId = [string]$taskProject.Project.PropertyGroup.UserSecretsId
    $taskNativeOutput = & dotnet user-secrets list --json --id $taskSecretId 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read the local secret store.' }
    $taskMatch = [regex]::Match(($taskNativeOutput -join "`n"), '(?s)//BEGIN\s*(\{.*\})\s*//END')
    if (-not $taskMatch.Success) { throw 'Cannot parse local secret configuration.' }
    $taskSettings = $taskMatch.Groups[1].Value | ConvertFrom-Json
    $taskConfiguredUrl = [uri]$taskSettings.'Odoo:BaseUrl'
    if ($taskConfiguredUrl.AbsoluteUri -cne 'https://napp.odoo.com/') { throw 'This test batch is restricted to the selected NApp account.' }
    $taskKey = [string]$taskSettings.'Odoo:ApiKey'
    if ($taskKey -notmatch '\A[\x21-\x7E]{1,512}\z') { throw 'The API key is missing or invalid.' }
    $taskHandler = New-Object System.Net.Http.HttpClientHandler
    $taskHandler.AllowAutoRedirect = $false
    $taskHandler.UseCookies = $false
    $taskClient = New-Object System.Net.Http.HttpClient($taskHandler)
    $taskClient.Timeout = [TimeSpan]::FromSeconds(30)
    $taskClient.MaxResponseContentBufferSize = 2 * 1024 * 1024
    $taskClient.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('bearer', $taskKey)
    $taskClient.DefaultRequestHeaders.UserAgent.ParseAdd('DikeSyntheticApSeeder/0.1')
    $taskDatabase = $taskSettings.PSObject.Properties['Odoo:Database']
    if ($null -ne $taskDatabase -and -not [string]::IsNullOrEmpty([string]$taskDatabase.Value)) {
        if ([string]$taskDatabase.Value -notmatch '\A[A-Za-z0-9_.-]{1,128}\z') { throw 'Invalid configured database name.' }
        $taskClient.DefaultRequestHeaders.Add('X-Odoo-Database', [string]$taskDatabase.Value)
    }
    $taskKey = $null; $taskSettings = $null; $taskNativeOutput = $null; $taskMatch = $null

    $taskAuth = Invoke-Odoo 'res.users' 'context_get' @{}
    $taskUsers = @(Invoke-Odoo 'res.users' 'read' @{ ids = @([int]$taskAuth.uid); fields = @('id','company_ids') })
    if ($taskUsers.Count -ne 1 -or $taskCompanyId -notin $taskUsers[0].company_ids) { throw 'The API user is not assigned to NApp company 1.' }
    $taskCompanyFields = Invoke-Odoo 'res.company' 'fields_get' @{ allfields = @('fiscalyear_lock_date','hard_lock_date','tax_lock_date','purchase_lock_date'); attributes = @('type') }
    $taskLockFields = @($taskCompanyFields.PSObject.Properties.Name)
    $taskCompanies = @(Search-Odoo 'res.company' @(,@('id','=',$taskCompanyId)) (@('id','name','currency_id') + $taskLockFields) 2)
    if ($taskCompanies.Count -ne 1 -or $taskCompanies[0].name -cne 'NApp') { throw 'The selected company does not match NApp.' }
    $taskCurrencyId = [int]$taskCompanies[0].currency_id[0]
    $taskCurrencies = @(Invoke-Odoo 'res.currency' 'read' @{ ids = @($taskCurrencyId); fields = @('id','name') })
    if ($taskCurrencies.Count -ne 1 -or $taskCurrencies[0].name -cne 'SAR') { throw 'The selected company is not in SAR.' }
    foreach ($taskLockField in $taskLockFields) {
        $taskLockValue = $taskCompanies[0].PSObject.Properties[$taskLockField].Value
        if ($taskLockValue -is [string] -and [string]$taskLockValue -ge '2026-10-06') { throw 'The proposed test period overlaps an accounting lock date.' }
    }
    $taskAccounts = @(Search-Odoo 'account.account' @(@('company_ids','in',@($taskCompanyId)), @('account_type','in',@('liability_payable','expense','asset_cash'))) @('id','code','name','account_type','company_ids','currency_id') 300)
    $taskPayable = @($taskAccounts | Where-Object { $_.account_type -eq 'liability_payable' -and $_.code -eq '220000' })
    $taskExpense = @($taskAccounts | Where-Object { $_.account_type -eq 'expense' } | Select-Object -First 1)
    if ($taskPayable.Count -ne 1 -or $taskExpense.Count -ne 1) { throw 'Could not select an unambiguous payable account and an existing expense account.' }
    foreach ($taskBankId in @(118,119)) {
        $taskBank = @($taskAccounts | Where-Object { $_.id -eq $taskBankId -and $_.account_type -eq 'asset_cash' })
        if ($taskBank.Count -ne 1 -or ($taskBank[0].currency_id -ne $false -and $taskBank[0].currency_id[0] -ne $taskCurrencyId)) { throw 'A selected bank account is missing or restricted to a foreign currency.' }
    }
    $taskJournals = @(Search-Odoo 'account.journal' @(@('company_id','=',$taskCompanyId), @('type','in',@('purchase','general'))) @('id','name','type','company_id') 50)
    $taskPurchaseJournal = @($taskJournals | Where-Object { $_.type -eq 'purchase' } | Select-Object -First 1)
    $taskGeneralJournal = @($taskJournals | Where-Object { $_.type -eq 'general' } | Select-Object -First 1)
    if ($taskPurchaseJournal.Count -ne 1 -or $taskGeneralJournal.Count -ne 1) { throw 'A purchase journal and a general journal are required.' }
    $taskExisting = @(Search-Odoo 'account.move' @(@('company_id','=',$taskCompanyId), @('ref','in',@($taskBatch.Ref))) @('id','ref','state','date','amount_total') 20)
    if (@($taskExisting | Group-Object ref | Where-Object Count -gt 1).Count -gt 0) { throw 'Duplicate test references already exist; manual review is required.' }
    $taskPlan = [ordered]@{
        Company = [ordered]@{ Id = $taskCompanyId; Name = 'NApp'; Currency = 'SAR' }
        PayableAccount = $taskPayable[0]; ExpenseAccount = $taskExpense[0]
        PurchaseJournal = $taskPurchaseJournal[0]; PaymentJournal = $taskGeneralJournal[0]
        Vendor = 'DIKE TEST VENDOR (synthetic)'; TaxTreatment = 'No taxes on this synthetic validation batch'
        IncludePayments = [bool]$IncludePayments; Bills = $taskBatch; ExistingBillCount = $taskExisting.Count
    }
    if (-not $Apply) { $taskPlan | ConvertTo-Json -Depth 10; return }

    $taskArtifacts = Join-Path $taskRoot 'artifacts'
    New-Item -ItemType Directory -Path $taskArtifacts -Force | Out-Null
    $taskLease = [IO.File]::Open((Join-Path $taskArtifacts 'odoo-ap-testdata.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $taskManifestPath = Join-Path $taskArtifacts ('odoo-ap-testdata-' + [guid]::NewGuid().ToString('N') + '.json')
    Save-Progress
    $taskVendorRef = "$taskPrefix-VENDOR"
    $taskVendors = @(Search-Odoo 'res.partner' @(@('ref','=',$taskVendorRef), @('company_id','=',$taskCompanyId)) @('id','name','ref','company_id','property_account_payable_id') 3)
    if ($taskVendors.Count -gt 1) { throw 'The synthetic vendor reference is not unique.' }
    if ($taskVendors.Count -eq 0) {
        $taskVendorId = Create-One 'res.partner' @{
            name = 'DIKE TEST VENDOR (synthetic)'; ref = $taskVendorRef; is_company = $true; supplier_rank = 1
            company_id = $taskCompanyId; property_account_payable_id = [int]$taskPayable[0].id
        }
        $taskVendors = @(Invoke-Odoo 'res.partner' 'read' @{ ids = @($taskVendorId); fields = @('id','name','ref','company_id','property_account_payable_id') })
    }
    if ($taskVendors.Count -ne 1 -or $taskVendors[0].name -cne 'DIKE TEST VENDOR (synthetic)' -or
        $taskVendors[0].company_id[0] -ne $taskCompanyId -or $taskVendors[0].property_account_payable_id[0] -ne $taskPayable[0].id) {
        throw 'The synthetic vendor does not match the approved company/payable account.'
    }
    $taskVendorId = [int]$taskVendors[0].id
    $taskResults.Add([ordered]@{ Kind = 'Vendor'; Id = $taskVendorId; Reference = $taskVendorRef })
    Save-Progress

    foreach ($taskBill in $taskBatch) {
        $taskMove = Get-Move $taskBill.Ref
        if ($null -eq $taskMove) {
            $taskCommands = New-Object 'System.Collections.Generic.List[object]'
            foreach ($taskLine in $taskBill.Lines) {
                $taskCommands.Add(@(0, 0, @{
                    name = "$($taskBill.Ref) | $($taskLine.Description)"; account_id = [int]$taskExpense[0].id
                    quantity = $taskLine.Quantity; price_unit = $taskLine.UnitPrice; tax_ids = @(,@(6,0,@()))
                }))
            }
            $taskMoveId = Create-One 'account.move' @{
                move_type = 'in_invoice'; company_id = $taskCompanyId; journal_id = [int]$taskPurchaseJournal[0].id
                partner_id = $taskVendorId; currency_id = $taskCurrencyId; ref = $taskBill.Ref
                invoice_date = $taskBill.Date; date = $taskBill.Date; invoice_date_due = $taskBill.Date
                invoice_payment_term_id = $false; auto_post = 'no'; invoice_line_ids = $taskCommands.ToArray()
            }
            $taskResults.Add([ordered]@{ Kind = 'BillCreated'; Id = $taskMoveId; Reference = $taskBill.Ref; ObservedState = 'draft' })
            Save-Progress
            $taskMove = Get-Move $taskBill.Ref
        }
        Check-Move $taskMove $taskBill.Ref $taskBill.Date 'in_invoice' ([int]$taskPurchaseJournal[0].id) $taskVendorId
        if ($taskMove.invoice_date -cne $taskBill.Date -or [decimal]$taskMove.amount_total -ne $taskBill.Total -or [decimal]$taskMove.amount_tax -ne 0) { throw 'The synthetic bill total, date or taxes do not match the plan.' }
        $taskInvoiceLines = @(Get-MoveLines ([int]$taskMove.id))
        $taskProductLines = @($taskInvoiceLines | Where-Object display_type -eq 'product')
        $taskPayableLines = @($taskInvoiceLines | Where-Object { $_.account_id[0] -eq $taskPayable[0].id })
        if ($taskProductLines.Count -ne $taskBill.Lines.Count -or $taskPayableLines.Count -ne 1 -or
            [decimal]$taskPayableLines[0].credit -ne $taskBill.Total -or [decimal]$taskPayableLines[0].debit -ne 0) { throw 'The synthetic invoice lines or payable posting differ from the plan.' }
        foreach ($taskLine in $taskBill.Lines) {
            $taskMatches = @($taskProductLines | Where-Object { $_.name -ceq "$($taskBill.Ref) | $($taskLine.Description)" })
            if ($taskMatches.Count -ne 1 -or $taskMatches[0].account_id[0] -ne $taskExpense[0].id -or
                [decimal]$taskMatches[0].quantity -ne $taskLine.Quantity -or [decimal]$taskMatches[0].price_unit -ne $taskLine.UnitPrice -or
                [decimal]$taskMatches[0].price_subtotal -ne ($taskLine.Quantity * $taskLine.UnitPrice)) { throw 'An invoice line does not match its synthetic quantity/price/account.' }
        }
        $taskMove = Post-Move $taskMove
        $taskResults.Add([ordered]@{ Kind = 'BillPosted'; Id = [int]$taskMove.id; Reference = $taskBill.Ref; Number = $taskMove.name; Date = $taskMove.date; TotalSAR = $taskBill.Total; ObservedState = $taskMove.state })
        Save-Progress

        if ($IncludePayments -and $taskBill.Payment -gt 0) {
            $taskPaymentRef = "$($taskBill.Ref)-PAY"
            $taskPaymentMove = Get-Move $taskPaymentRef
            if ($null -eq $taskPaymentMove) {
                if ([decimal]$taskMove.amount_residual -ne $taskBill.Total) { throw 'The bill already has unexpected allocations; no new test payment will be created.' }
                $taskCommands = New-Object 'System.Collections.Generic.List[object]'
                $taskCommands.Add(@(0,0,@{ name = $taskPaymentRef; account_id = [int]$taskPayable[0].id; partner_id = $taskVendorId; debit = $taskBill.Payment; credit = 0; currency_id = $taskCurrencyId; amount_currency = $taskBill.Payment }))
                $taskCommands.Add(@(0,0,@{ name = $taskPaymentRef; account_id = [int]$taskBill.Bank; partner_id = $taskVendorId; debit = 0; credit = $taskBill.Payment; currency_id = $taskCurrencyId; amount_currency = -$taskBill.Payment }))
                $taskPaymentId = Create-One 'account.move' @{
                    move_type = 'entry'; company_id = $taskCompanyId; journal_id = [int]$taskGeneralJournal[0].id
                    partner_id = $taskVendorId; currency_id = $taskCurrencyId; ref = $taskPaymentRef; date = $taskBill.PaymentDate
                    auto_post = 'no'; line_ids = $taskCommands.ToArray()
                }
                $taskResults.Add([ordered]@{ Kind = 'PaymentCreated'; Id = $taskPaymentId; Reference = $taskPaymentRef; ObservedState = 'draft' })
                Save-Progress
                $taskPaymentMove = Get-Move $taskPaymentRef
            }
            Check-Move $taskPaymentMove $taskPaymentRef $taskBill.PaymentDate 'entry' ([int]$taskGeneralJournal[0].id) $taskVendorId
            $taskPaymentLines = @(Get-MoveLines ([int]$taskPaymentMove.id))
            $taskApPaymentLines = @($taskPaymentLines | Where-Object { $_.account_id[0] -eq $taskPayable[0].id })
            $taskBankLines = @($taskPaymentLines | Where-Object { $_.account_id[0] -eq $taskBill.Bank })
            if ($taskPaymentLines.Count -ne 2 -or $taskApPaymentLines.Count -ne 1 -or $taskBankLines.Count -ne 1 -or
                [decimal]$taskApPaymentLines[0].debit -ne $taskBill.Payment -or [decimal]$taskApPaymentLines[0].credit -ne 0 -or
                [decimal]$taskBankLines[0].credit -ne $taskBill.Payment -or [decimal]$taskBankLines[0].debit -ne 0 -or
                @($taskPaymentLines | Where-Object { $_.partner_id[0] -ne $taskVendorId -or $_.company_id[0] -ne $taskCompanyId -or $_.currency_id[0] -ne $taskCurrencyId }).Count -ne 0) {
                throw 'The manual payment entry does not match its planned accounts and SAR amounts.'
            }
            $taskPaymentMove = Post-Move $taskPaymentMove
            $taskApPaymentLines = @(Get-MoveLines ([int]$taskPaymentMove.id) | Where-Object { $_.account_id[0] -eq $taskPayable[0].id })
            if ([decimal]$taskApPaymentLines[0].amount_residual -ne 0) {
                if ([decimal]$taskApPaymentLines[0].amount_residual -ne $taskBill.Payment) { throw 'The payment has an unexpected partial reconciliation.' }
                $null = Invoke-Odoo 'account.move.line' 'reconcile' @{ ids = @([int]$taskPayableLines[0].id, [int]$taskApPaymentLines[0].id) }
            }
            $taskResults.Add([ordered]@{ Kind = 'PaymentPosted'; Id = [int]$taskPaymentMove.id; Reference = $taskPaymentRef; Date = $taskPaymentMove.date; BankAccountId = $taskBill.Bank; AmountSAR = $taskBill.Payment; ObservedState = $taskPaymentMove.state })
            Save-Progress
        }
        $taskMove = Get-Move $taskBill.Ref
        $taskExpectedResidual = $taskBill.Total
        if ($IncludePayments) { $taskExpectedResidual -= $taskBill.Payment }
        if ([decimal]$taskMove.amount_residual -ne $taskExpectedResidual) { throw 'The posted bill residual does not match the planned payment scenario.' }
        $taskResults.Add([ordered]@{ Kind = 'BillVerified'; Id = [int]$taskMove.id; Reference = $taskBill.Ref; Date = $taskMove.date; TotalSAR = [decimal]$taskMove.amount_total; ResidualSAR = [decimal]$taskMove.amount_residual; PaymentState = $taskMove.payment_state; ObservedState = $taskMove.state })
        Save-Progress
        Write-Output ("Verified $($taskBill.Ref): posted; total SAR $($taskBill.Total); residual SAR $($taskMove.amount_residual).")
    }
    $taskCompleted = $true
    Save-Progress
    Write-Output "Synthetic AP batch completed. Local manifest: $taskManifestPath"
}
catch {
    # Avoid serializing exceptions, request headers, raw bodies or local configuration.
    Write-Error ("AP test-data operation stopped during $taskStage (script line $($_.InvocationInfo.ScriptLineNumber)). Details were withheld to protect configuration and provider data. Inspect the progress manifest before retrying: $taskManifestPath") -ErrorAction Continue
    exit 1
}
finally {
    if ($null -ne $taskLease) { $taskLease.Dispose() }
    if ($null -ne $taskClient) { $taskClient.Dispose() }
    $taskKey = $null; $taskSettings = $null; $taskNativeOutput = $null; $taskMatch = $null
}
