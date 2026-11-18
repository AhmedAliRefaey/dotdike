$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$testRoot = Join-Path $PSScriptRoot '..\artifacts\registration-check'
$testRoot = [IO.Path]::GetFullPath($testRoot)
$testDb = Join-Path $testRoot ('smoke-' + [Guid]::NewGuid().ToString('N') + '.db')
$appRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\Dike.Web'))
$process = Start-Process dotnet -ArgumentList @((Join-Path $testRoot 'Dike.Web.dll'), '--environment', 'Development', '--urls', 'http://localhost:5081', '--contentRoot', $appRoot, '--NetSuite:StoragePath', (Join-Path $testRoot 'netsuite-fixtures'), '--ConnectionStrings:Identity', ('"Data Source=' + $testDb + '"')) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $testRoot 'smoke.stdout.log') -RedirectStandardError (Join-Path $testRoot 'smoke.stderr.log')
function Client {
    $handler = New-Object System.Net.Http.HttpClientHandler
    $handler.AllowAutoRedirect = $false
    $handler.CookieContainer = New-Object Net.CookieContainer
    $client = New-Object System.Net.Http.HttpClient($handler)
    $client.BaseAddress = [Uri]'http://localhost:5081'
    return $client
}
function Get($client, $path) { return $client.GetAsync($path).GetAwaiter().GetResult() }
function Body($response) { return $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() }
function Check($condition, $name) { if (-not $condition) { throw "FAIL $name" }; Write-Output "PASS $name" }
function Post($client, $path, $values, $tokenPage = $path) {
    $page = Body (Get $client $tokenPage)
    $token = [regex]::Match($page, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
    if (-not $token) { throw "Missing antiforgery token: $tokenPage" }
    $data = New-Object 'System.Collections.Generic.Dictionary[string,string]'
    foreach ($key in $values.Keys) { $data.Add($key, [string]$values[$key]) }
    $data.Add('__RequestVerificationToken', [Net.WebUtility]::HtmlDecode($token))
    $content = New-Object System.Net.Http.FormUrlEncodedContent($data)
    return $client.PostAsync($path, $content).GetAwaiter().GetResult()
}
try {
    $a = Client
    $b = Client
    for ($i=0; $i -lt 60; $i++) {
        try { $ready = Get $a '/health'; if ([int]$ready.StatusCode -eq 200) { break } } catch { }
        Start-Sleep -Milliseconds 250
    }
    Check ([int](Get $a '/Company').StatusCode -eq 302) 'Anonymous company access blocked'
    Check ([int](Get $a '/Company/Staff').StatusCode -eq 302) 'Anonymous staff directory access blocked'
    Check ([int](Get $a '/Accounts/Odoo').StatusCode -eq 302) 'Anonymous Odoo access blocked'
    $empty = New-Object System.Net.Http.StringContent('')
    Check ([int]$a.PostAsync('/Auth/Register', $empty).GetAwaiter().GetResult().StatusCode -eq 400) 'Missing CSRF token rejected'
    $password = 'Test-Only!' + [Guid]::NewGuid().ToString('N')
    $registration = @{ 'Input.CompanyName'='Smoke Company A'; 'Input.ConnectionProvider'='NetSuite'; 'Input.FullName'='Smoke Owner'; 'Input.Email'='owner-a@example.test'; 'Input.Password'=$password; 'Input.ConfirmPassword'=$password; 'Input.Authorized'='true' }
    $invalidProvider = $registration.Clone(); $invalidProvider.Remove('Input.ConnectionProvider')
    Check ((Body (Post $a '/Auth/Register' $invalidProvider)).Contains('Choose NetSuite or Odoo.')) 'Registration requires a provider'
    $invalidProvider['Input.ConnectionProvider'] = 'Both'
    Check ((Body (Post $a '/Auth/Register' $invalidProvider)).Contains('Choose NetSuite or Odoo.')) 'Registration rejects unsupported providers'
    $result = Post $a '/Auth/Register' $registration
    Check ($result.Headers.Location.OriginalString -eq '/Company/Setup') 'Registration creates company and signs in'
    Check ((Body (Get $a '/Company/Setup')).Contains('Smoke Company A')) 'Owner sees own onboarding'
    $result = Post $a '/Company/Setup' @{ 'Input.Name'='Smoke Company A'; 'Input.Country'='Saudi Arabia'; 'Input.ConnectionProvider'='NetSuite'; 'CompanyId'=[Guid]::NewGuid().ToString() }
    Check ([int]$result.StatusCode -eq 302) 'Company setup completes'
    Check ((Body (Get $a '/Company')).Contains('Smoke Company A')) 'Company dashboard available'
    Check (-not (Body (Get $a '/Company')).Contains('Odoo')) 'NetSuite company hides all Odoo navigation and overview content'
    Check ([int](Get $a '/Accounts/Odoo/Connection').StatusCode -eq 404) 'NetSuite company cannot open a direct Odoo URL'
    Check ([int](Post $a '/Accounts/Odoo/Connection?handler=Local' @{} '/Company').StatusCode -eq 404) 'NetSuite company cannot post an Odoo action'
    $staffA = Get $a '/Company/Staff'
    $staffBodyA = Body $staffA
    Check ([int]$staffA.StatusCode -eq 200 -and $staffBodyA.Contains('owner-a@example.test') -and $staffBodyA.Contains('Smoke Owner')) 'Administrator sees own staff directory'
    Check ($staffA.Headers.CacheControl.NoStore) 'Staff directory prohibits response caching'
    Check ($staffBodyA.Contains('1 member') -and $staffBodyA.Contains('View only') -and $staffBodyA.Contains('Page 1 of 1')) 'Staff count and read-only state are displayed'
    Check ([int](Get $a '/Company/Staff?pageNumber=0').StatusCode -eq 400) 'Invalid staff page number rejected'
    Check ([int](Get $a '/Company/Staff?pageNumber=invalid').StatusCode -eq 400) 'Malformed staff page number rejected'
    Check ([int](Get $a '/Company/Staff?pageNumber=2').StatusCode -eq 404) 'Staff page beyond the directory returns not found'
    Check ([int](Post $a '/Company/Staff' @{}).StatusCode -eq 405) 'Staff directory does not accept mutations'
    $duplicate = Post $b '/Auth/Register' $registration
    Check ((Body $duplicate).Contains('Registration could not be completed')) 'Duplicate registration rejected'
    $registration['Input.CompanyName']='Smoke Company B'
    $registration['Input.ConnectionProvider']='Odoo'
    $registration['Input.Email']='owner-b@example.test'
    $registration['Input.FullName']='Smoke <script>alert(1)</script> Owner'
    $result = Post $b '/Auth/Register' $registration
    Check ([int]$result.StatusCode -eq 302) 'Second company registers'
    $result = Post $b '/Company/Setup' @{ 'Input.Name'='Smoke Company B'; 'Input.Country'='Saudi Arabia'; 'Input.ConnectionProvider'='Odoo' }
    $pageB = Body (Get $b '/Company')
    Check ($pageB.Contains('Smoke Company B') -and -not $pageB.Contains('Smoke Company A')) 'Company B cannot see Company A profile'
    Check ($pageB.Contains('Odoo') -and -not $pageB.Contains('NetSuite')) 'Odoo company hides all NetSuite navigation and overview content'
    Check ((Get $b '/Accounts').Headers.Location.OriginalString -eq '/Accounts/Odoo') 'Generic connections entry follows the saved provider'
    Check ([int](Get $b '/Accounts/NetSuite').StatusCode -eq 404) 'Odoo company cannot open a direct NetSuite URL'
    Check ([int](Post $b '/Accounts?handler=LoadAccounts' @{} '/Company').StatusCode -eq 404) 'Odoo company cannot start a NetSuite import'
    $odoo = Get $b '/Accounts/Odoo'
    Check ([int]$odoo.StatusCode -eq 200 -and $odoo.Headers.CacheControl.NoStore -and (Body $odoo).Contains('Your Odoo accounting workspace.')) 'Odoo overview renders without caching'
    $odooConnection = Body (Get $b '/Accounts/Odoo/Connection')
    Check (-not $odooConnection.Contains('Use existing local connection')) 'Unbound Dike company cannot import the shared local key'
    Check ([int]$b.PostAsync('/Accounts/Odoo/Connection?handler=Local', (New-Object System.Net.Http.StringContent(''))).GetAwaiter().GetResult().StatusCode -eq 400) 'Odoo local key import requires CSRF token'
    $deniedOdoo = Body (Post $b '/Accounts/Odoo/Connection?handler=Local' @{} '/Accounts/Odoo/Connection')
    Check ($deniedOdoo.Contains('not assigned to this Dike workspace')) 'Forged local key import rejected'
    $badOdoo = Body (Post $b '/Accounts/Odoo/Connection' @{ 'Input.ApiKey'='fixture secret with spaces'; 'Input.Database'='' })
    Check ($badOdoo.Contains('Check the API key') -and -not $badOdoo.Contains('fixture secret with spaces')) 'Invalid Odoo key rejected without reflection or API request'
    Check ((Body (Get $b '/Accounts/Odoo/Ledger')).Contains('Load accounts first')) 'Odoo GL page requires loaded accounts'
    $staffBodyB = Body (Get $b '/Company/Staff?CompanyId=ignored')
    Check ($staffBodyB.Contains('owner-b@example.test') -and -not $staffBodyB.Contains('owner-a@example.test') -and $staffBodyB.Contains('1 member')) 'Staff rows and counts stay within the authenticated company'
    Check ($staffBodyB.Contains('&lt;script&gt;alert(1)&lt;/script&gt;') -and -not $staffBodyB.Contains('<script>alert(1)</script>')) 'Staff display names are HTML encoded'
    $ns = @{ 'Input.AccountId'='7654321_SB2'; 'Input.Mode'='TBA'; 'Input.ClientId'='fixture-consumer'; 'Input.ClientSecret'='fixture-secret-not-real'; 'Input.TokenId'='fixture-token'; 'Input.TokenSecret'='fixture-token-secret' }
    $result = Post $a '/Accounts/NetSuite' $ns
    Check ([int]$result.StatusCode -eq 302) 'Company-scoped NetSuite configuration saved without network calls'
    $nsPage = Body (Get $a '/Accounts/NetSuite')
    Check ($nsPage.Contains('7654321_SB2') -and -not $nsPage.Contains('fixture-secret-not-real')) 'Saved credentials never reflected in HTML'
    Check (-not (Body (Get $b '/Accounts/NetSuite')).Contains('7654321_SB2')) 'Other company cannot view NetSuite configuration'
    $invalidChange = Body (Post $b '/Company/Setup' @{ 'Input.Name'='Smoke Company B'; 'Input.Country'='Saudi Arabia'; 'Input.ConnectionProvider'='Both' })
    Check ($invalidChange.Contains('Choose NetSuite or Odoo.') -and [int](Get $b '/Accounts/Odoo').StatusCode -eq 200) 'Invalid provider change preserves the selected provider'
    $result = Post $b '/Company/Setup' @{ 'Input.Name'='Smoke Company B'; 'Input.Country'='Saudi Arabia'; 'Input.ConnectionProvider'='NetSuite' }
    Check ([int]$result.StatusCode -eq 302 -and [int](Get $b '/Accounts/Odoo').StatusCode -eq 404) 'Changing provider immediately disables the previous provider'
    $switched = Body (Get $b '/Accounts/NetSuite')
    Check ($switched.Contains('NetSuite') -and -not $switched.Contains('7654321_SB2')) 'Provider switch retains company isolation'
    $result = Post $b '/Company/Setup' @{ 'Input.Name'='Smoke Company B'; 'Input.Country'='Saudi Arabia'; 'Input.ConnectionProvider'='Odoo' }
    Check ([int]$result.StatusCode -eq 302 -and [int](Get $b '/Accounts/NetSuite').StatusCode -eq 404) 'Company can switch back to Odoo'
    $ns['Input.AccountId']='https://example.org'
    $invalidNs = Body (Post $a '/Accounts/NetSuite' $ns)
    Check ($invalidNs.Contains('not a URL') -and -not $invalidNs.Contains('fixture-secret-not-real')) 'Invalid host rejected without reflecting secrets'
    $result = Post $a '/Auth/Logout' @{} '/Company'
    Check ([int](Get $a '/Company').StatusCode -eq 302) 'Logout clears access'
    $bad = Post $a '/Auth/Login' @{ 'Input.Email'='owner-a@example.test'; 'Input.Password'='Wrong-password1!' }
    Check ((Body $bad).Contains('Unable to sign in')) 'Wrong password rejected'
    $result = Post $a '/Auth/Login' @{ 'Input.Email'='owner-a@example.test'; 'Input.Password'=$password; 'ReturnUrl'='https://example.org/steal' }
    Check ($result.Headers.Location.OriginalString -eq '/Company') 'Login rejects external return URL'
    $newPassword = 'Changed-Only!' + [Guid]::NewGuid().ToString('N')
    $result = Post $a '/Auth/ChangePassword' @{ 'Input.Current'=$password; 'Input.New'=$newPassword; 'Input.Confirm'=$newPassword }
    Check ([int]$result.StatusCode -eq 302) 'Password change succeeds'
    $result = Post $a '/Auth/Logout' @{} '/Company'
    $old = Post $a '/Auth/Login' @{ 'Input.Email'='owner-a@example.test'; 'Input.Password'=$password }
    Check ((Body $old).Contains('Unable to sign in')) 'Old password no longer works'
    $result = Post $a '/Auth/Login' @{ 'Input.Email'='owner-a@example.test'; 'Input.Password'=$newPassword }
    Check ([int]$result.StatusCode -eq 302) 'New password signs in'
    # Language changes are public POSTs protected by the same antiforgery boundary.
    Check ([int]$a.PostAsync('/Language', (New-Object System.Net.Http.StringContent(''))).GetAwaiter().GetResult().StatusCode -eq 400) 'Language switch requires CSRF token'
    $result = Post $a '/Language' @{ 'culture'='ar'; 'returnUrl'='/Company/Staff?pageNumber=1' } '/Company'
    Check ($result.Headers.Location.OriginalString -eq '/Company/Staff?pageNumber=1') 'Language switch preserves a local route and query'
    $cultureCookie = $result.Headers.GetValues('Set-Cookie') -join ' '
    Check ($cultureCookie.Contains('.AspNetCore.Culture') -and $cultureCookie.Contains('httponly') -and $cultureCookie.Contains('samesite=lax')) 'Language cookie is persistent and protected'
    $arabic = Get $a '/Company'
    $arabicBody = [Net.WebUtility]::HtmlDecode((Body $arabic))
    $strings = [xml](Get-Content -Raw -Encoding UTF8 (Join-Path $appRoot 'Resources/Localization.UiText.ar.resx'))
    function Arabic($key) { return ($strings.root.data | Where-Object { $_.name -eq $key }).value }
    Check ($arabicBody.Contains('lang="ar" dir="rtl"') -and $arabicBody.Contains((Arabic 'Workspace overview'))) 'Arabic choice persists with translated RTL overview'
    Check (($arabic.Content.Headers.ContentLanguage -join ',') -eq 'ar') 'Arabic response advertises its content language'
    $staffArabic = [Net.WebUtility]::HtmlDecode((Body (Get $a '/Company/Staff')))
    Check ($staffArabic.Contains((Arabic 'Staff directory')) -and $staffArabic.Contains((Arabic 'Administrator')) -and $staffArabic.Contains('dir="ltr">owner-a@example.test')) 'Arabic staff labels and roles keep email addresses isolated LTR'
    $badPassword = [Net.WebUtility]::HtmlDecode((Body (Post $a '/Auth/ChangePassword' @{ 'Input.Current'='Wrong-Current!123'; 'Input.New'=$newPassword; 'Input.Confirm'=$newPassword })))
    Check ($badPassword.Contains((Arabic 'The current password is incorrect.'))) 'Identity password validation is translated'
    $badProfile = [Net.WebUtility]::HtmlDecode((Body (Post $a '/Company/Setup' @{ 'Input.Name'=''; 'Input.Country'='Saudi Arabia' })))
    $requiredMessage = (Arabic 'The {0} field is required.') -f (Arabic 'Company legal name')
    Check ($badProfile.Contains($requiredMessage)) 'Required-field validation translates both message and display name'
    $arabicNs = [Net.WebUtility]::HtmlDecode((Body (Post $a '/Accounts/NetSuite' $ns)))
    Check ($arabicNs.Contains((Arabic 'Enter a NetSuite account ID, not a URL (for example 1234567_SB1).')) -and -not $arabicNs.Contains('fixture-secret-not-real')) 'Arabic connector validation preserves secret redaction'
    $badPeriod = [Net.WebUtility]::HtmlDecode((Body (Post $a '/Accounts?handler=Import' @{ 'Period.Account'='1'; 'Period.Subsidiary'='1'; 'Period.Book'='1'; 'Period.From'='invalid-date'; 'Period.To'='2026-11-16' } '/Accounts')))
    $dateMessage = (Arabic 'Enter a valid value for {0}.') -f (Arabic 'From')
    Check ($badPeriod.Contains($dateMessage)) 'Arabic model-binding errors preserve Gregorian date validation without provider calls'

    $result = Post $a '/Language' @{ 'culture'='unsupported'; 'returnUrl'='/Company' } '/Company'
    Check ([int]$result.StatusCode -eq 400) 'Unsupported language is rejected'
    $result = Post $a '/Language' @{ 'culture'='en'; 'returnUrl'='https://example.org/steal' } '/Company'
    Check ($result.Headers.Location.OriginalString -eq '/') 'Language switch rejects external return URLs'
    Check ((Body (Get $a '/Company')).Contains('lang="en" dir="ltr"')) 'Switching back restores English LTR'

    Write-Output 'All company registration smoke checks passed. Synthetic data is isolated in artifacts.'
} finally {
    if ($a) { $a.Dispose() }; if ($b) { $b.Dispose() }
    if (-not $process.HasExited) { Stop-Process -Id $process.Id }
}
