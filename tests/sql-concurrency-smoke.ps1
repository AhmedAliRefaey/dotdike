param([switch]$Run)

# Historical LocalDB design probe. The current PostgreSQL implementation is exercised by Dike.Tests --hosted-storage.
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runId = [Guid]::NewGuid().ToString('N')
$databaseName = 'DikeSqlChecks_' + $runId
$runFolder = [IO.Path]::GetFullPath((Join-Path $workspace ('artifacts\sql-checks\' + $runId)))
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $workspace 'artifacts\sql-checks')) + [IO.Path]::DirectorySeparatorChar
if (-not $runFolder.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $databaseName -notmatch '^DikeSqlChecks_[0-9a-f]{32}$') {
    throw 'Invalid disposable SQL test location or database name.'
}

# The target is deliberately fixed to this user's development LocalDB instance.
# This script accepts no production connection string or existing database name.
if (-not $Run) {
    [pscustomobject]@{
        Target = '(localdb)\MSSQLLocalDB'
        Database = $databaseName
        Files = $runFolder
        Action = 'Use -Run to create, test and drop only this uniquely named synthetic database.'
    } | Format-List
    return
}

function New-TestConnection([string]$catalog) {
    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder['Data Source'] = '(localdb)\MSSQLLocalDB'
    $builder['Initial Catalog'] = $catalog
    $builder['Integrated Security'] = $true
    # This LocalDB-only synthetic probe uses local IPC; this instance does not support TLS.
    # Production SQL configuration is separate and must use a trusted encrypted connection.
    $builder['Encrypt'] = $false
    $builder['Pooling'] = $false
    $builder['Connect Timeout'] = 15
    $connection = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
    try { $connection.Open() } catch { $connection.Dispose(); throw }
    return $connection
}

function New-TestCommand($connection, [string]$sql, $transaction = $null, [hashtable]$parameters = @{}) {
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.CommandTimeout = 15
    if ($null -ne $transaction) { $command.Transaction = $transaction }
    foreach ($name in $parameters.Keys) { $null = $command.Parameters.AddWithValue($name, $parameters[$name]) }
    return $command
}

function Invoke-Sql($connection, [string]$sql, $transaction = $null, [hashtable]$parameters = @{}) {
    $command = New-TestCommand $connection $sql $transaction $parameters
    try { return $command.ExecuteScalar() } finally { $command.Dispose() }
}

function Require([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

function Pass([string]$message) {
    $script:passed++
    Write-Output ('PASS ' + $message)
}

function Start-GuardRead($connection, $transaction, [string]$scope) {
    $command = New-TestCommand $connection 'SELECT Generation FROM Guards WITH (UPDLOCK,HOLDLOCK) WHERE Scope = @scope' $transaction @{ '@scope' = $scope }
    return @{ Command = $command; Pending = $command.ExecuteScalarAsync() }
}

function Wait-ForBlocking($observer, $pending, [int]$blockedSession, [int]$blockingSession) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.Elapsed.TotalSeconds -lt 8) {
        if ($pending.IsCompleted) { throw 'The competing command finished before the required guard was released.' }
        $blocker = Invoke-Sql $observer 'SELECT blocking_session_id FROM sys.dm_exec_requests WHERE session_id = @session' $null @{ '@session' = $blockedSession }
        if ($null -ne $blocker -and [int]$blocker -eq $blockingSession) { return }
        Start-Sleep -Milliseconds 25
    }
    throw 'Did not observe the expected SQL blocking relationship within eight seconds.'
}

function Finish-GuardRead($read) {
    try { return $read.Pending.GetAwaiter().GetResult() } finally { $read.Command.Dispose() }
}

function Expect-Duplicate([scriptblock]$action) {
    try { & $action | Out-Null }
    catch {
        $failure = $_.Exception
        while ($null -ne $failure -and $failure -isnot [System.Data.SqlClient.SqlException]) { $failure = $failure.InnerException }
        if ($null -ne $failure -and $failure.Number -in 2601,2627) { return }
        throw
    }
    throw 'Expected SQL unique-constraint rejection was not observed.'
}

function Run-IdempotentFixtureCommand($connection, [string]$payload) {
    $transaction = $connection.BeginTransaction()
    try {
        $null = Invoke-Sql $connection "SELECT Generation FROM Guards WITH (UPDLOCK,HOLDLOCK) WHERE Scope = 'replay'" $transaction
        $existing = Invoke-Sql $connection "SELECT PayloadHash FROM Requests WHERE Scope = 'replay' AND RequestKey = 'command-1'" $transaction
        if ($null -ne $existing) {
            if ($existing -cne $payload) { throw 'PAYLOAD_CONFLICT' }
            $result = Invoke-Sql $connection "SELECT ResultId FROM Requests WHERE Scope = 'replay' AND RequestKey = 'command-1'" $transaction
        } else {
            $result = [Guid]::NewGuid()
            $null = Invoke-Sql $connection "INSERT Requests VALUES ('replay','command-1',@payload,@result); INSERT Audit VALUES ('replay','Submitted');" $transaction @{ '@payload' = $payload; '@result' = $result }
        }
        $transaction.Commit()
        return $result
    } finally { $transaction.Dispose() }
}

$created = $false
$master = $null
$connectionA = $null
$connectionB = $null
$observer = $null
$script:passed = 0
try {
    $master = New-TestConnection 'master'
    $null = New-Item -ItemType Directory -Path $runFolder -Force
    $dataFile = (Join-Path $runFolder 'checks.mdf').Replace("'", "''")
    $logFile = (Join-Path $runFolder 'checks_log.ldf').Replace("'", "''")
    $null = Invoke-Sql $master "CREATE DATABASE [$databaseName] ON PRIMARY (NAME=N'checks',FILENAME=N'$dataFile') LOG ON (NAME=N'checks_log',FILENAME=N'$logFile')"
    $created = $true
    $connectionA = New-TestConnection $databaseName
    $connectionB = New-TestConnection $databaseName
    $observer = New-TestConnection $databaseName
    $sessionA = [int](Invoke-Sql $connectionA 'SELECT @@SPID')
    $sessionB = [int](Invoke-Sql $connectionB 'SELECT @@SPID')
    $null = Invoke-Sql $connectionA @'
CREATE TABLE Guards (Scope nvarchar(64) NOT NULL PRIMARY KEY, Generation bigint NOT NULL);
CREATE TABLE ReviewGroups (Id nvarchar(64) NOT NULL PRIMARY KEY, Scope nvarchar(64) NOT NULL REFERENCES Guards(Scope), Mapping nvarchar(64) NOT NULL, Status nvarchar(32) NOT NULL, PinnedGeneration bigint NOT NULL);
CREATE TABLE Reservations (MovementId nvarchar(64) NOT NULL PRIMARY KEY, GroupId nvarchar(64) NOT NULL REFERENCES ReviewGroups(Id));
CREATE TABLE Audit (Id bigint IDENTITY PRIMARY KEY, Scope nvarchar(64) NOT NULL, Action nvarchar(32) NOT NULL);
CREATE TABLE Jobs (Id int NOT NULL PRIMARY KEY, Owner nvarchar(32) NOT NULL, Generation bigint NOT NULL, ExpiresAt datetime2 NOT NULL, State nvarchar(32) NOT NULL);
CREATE TABLE Requests (Scope nvarchar(64) NOT NULL, RequestKey nvarchar(64) NOT NULL, PayloadHash varchar(64) NOT NULL, ResultId uniqueidentifier NOT NULL, PRIMARY KEY (Scope,RequestKey));
INSERT Guards VALUES ('rollback',1),('reserve',1),('approve-first',1),('publish-first',1),('remap',1),('replay',1),('export',1);
'@

    $transactionA = $connectionA.BeginTransaction()
    try {
        $null = Invoke-Sql $connectionA "INSERT ReviewGroups VALUES ('rollback-group','rollback','mapping-1','Submitted',1); INSERT Reservations VALUES ('rollback-row','rollback-group'); INSERT Audit VALUES ('rollback','Submitted');" $transactionA
        $transactionA.Rollback()
    } finally { $transactionA.Dispose() }
    $leftovers = Invoke-Sql $observer "SELECT (SELECT COUNT(*) FROM ReviewGroups WHERE Scope='rollback') + (SELECT COUNT(*) FROM Reservations WHERE MovementId='rollback-row') + (SELECT COUNT(*) FROM Audit WHERE Scope='rollback')"
    Require ($leftovers -eq 0) 'Rollback left partial group/reservation/audit state.'
    Pass 'SQL rollback removes group, reservation and audit together'

    $null = Invoke-Sql $connectionA "INSERT ReviewGroups VALUES ('reserve-a','reserve','mapping-1','Draft',1),('reserve-b','reserve','mapping-1','Draft',1)"
    $transactionA = $connectionA.BeginTransaction()
    $transactionB = $connectionB.BeginTransaction()
    try {
        $null = Invoke-Sql $connectionA "SELECT Generation FROM Guards WITH (UPDLOCK,HOLDLOCK) WHERE Scope='reserve'; INSERT Reservations VALUES ('shared-row','reserve-a');" $transactionA
        $read = Start-GuardRead $connectionB $transactionB 'reserve'
        Wait-ForBlocking $observer $read.Pending $sessionB $sessionA
        $transactionA.Commit()
        $null = Finish-GuardRead $read
        Expect-Duplicate { Invoke-Sql $connectionB "INSERT Reservations VALUES ('shared-row','reserve-b')" $transactionB }
        $transactionB.Rollback()
    } finally { $transactionA.Dispose(); $transactionB.Dispose() }
    Require ((Invoke-Sql $observer "SELECT GroupId FROM Reservations WHERE MovementId='shared-row'") -eq 'reserve-a') 'Competing group replaced the reservation owner.'
    Pass 'Competing connections serialize and only one group reserves a logical row'

    $null = Invoke-Sql $connectionA "INSERT ReviewGroups VALUES ('approved-group','approve-first','mapping-1','Submitted',1); INSERT Reservations VALUES ('approved-row','approved-group')"
    $transactionA = $connectionA.BeginTransaction()
    $transactionB = $connectionB.BeginTransaction()
    try {
        $null = Invoke-Sql $connectionA "SELECT Generation FROM Guards WITH (UPDLOCK,HOLDLOCK) WHERE Scope='approve-first'; UPDATE ReviewGroups SET Status='Approved' WHERE Id='approved-group'; INSERT Audit VALUES ('approve-first','Approved');" $transactionA
        $read = Start-GuardRead $connectionB $transactionB 'approve-first'
        Wait-ForBlocking $observer $read.Pending $sessionB $sessionA
        $transactionA.Commit()
        $null = Finish-GuardRead $read
        $null = Invoke-Sql $connectionB "UPDATE Guards SET Generation=2 WHERE Scope='approve-first'; UPDATE ReviewGroups SET Status='Invalidated' WHERE Id='approved-group'; DELETE Reservations WHERE GroupId='approved-group'; INSERT Audit VALUES ('approve-first','Invalidated');" $transactionB
        $transactionB.Commit()
    } finally { $transactionA.Dispose(); $transactionB.Dispose() }
    Require ((Invoke-Sql $observer "SELECT Status FROM ReviewGroups WHERE Id='approved-group'") -eq 'Invalidated') 'Publication left a stale approval active.'
    Require ((Invoke-Sql $observer "SELECT COUNT(*) FROM Reservations WHERE GroupId='approved-group'") -eq 0) 'Invalidation retained a reservation.'
    Require ((Invoke-Sql $observer "SELECT COUNT(*) FROM Audit WHERE Scope='approve-first'") -eq 2) 'Approval/invalidation history is incomplete.'
    Pass 'Approval first then publication invalidates and releases in the guarded transaction'

    $null = Invoke-Sql $connectionA "INSERT ReviewGroups VALUES ('stale-group','publish-first','mapping-1','Submitted',1)"
    $transactionA = $connectionA.BeginTransaction()
    $transactionB = $connectionB.BeginTransaction()
    try {
        $null = Invoke-Sql $connectionA "SELECT Generation FROM Guards WITH (UPDLOCK,HOLDLOCK) WHERE Scope='publish-first'; UPDATE Guards SET Generation=2 WHERE Scope='publish-first'; UPDATE ReviewGroups SET Status='Invalidated' WHERE Id='stale-group';" $transactionA
        $read = Start-GuardRead $connectionB $transactionB 'publish-first'
        Wait-ForBlocking $observer $read.Pending $sessionB $sessionA
        $transactionA.Commit()
        Require ((Finish-GuardRead $read) -eq 2) 'Waiting approver read the old source generation.'
        $changed = Invoke-Sql $connectionB "UPDATE ReviewGroups SET Status='Approved' WHERE Id='stale-group' AND Status='Submitted' AND PinnedGeneration=(SELECT Generation FROM Guards WHERE Scope='publish-first'); SELECT @@ROWCOUNT;" $transactionB
        Require ($changed -eq 0) 'A stale proposal was approved after publication.'
        $transactionB.Rollback()
    } finally { $transactionA.Dispose(); $transactionB.Dispose() }
    Pass 'Publication first prevents approval of a superseded source generation'

    $null = Invoke-Sql $connectionA "INSERT ReviewGroups VALUES ('old-mapping','remap','mapping-1','Submitted',1),('new-mapping','remap','mapping-2','Draft',1); INSERT Reservations VALUES ('stable-logical-row','old-mapping')"
    Expect-Duplicate { Invoke-Sql $connectionB "INSERT Reservations VALUES ('stable-logical-row','new-mapping')" }
    Pass 'Changing mapping does not allow a second reservation of the same logical row'

    $null = Invoke-Sql $connectionA "INSERT Jobs VALUES (1,'worker-a',1,DATEADD(second,-30,SYSUTCDATETIME()),'Running')"
    $expiredRenewal = Invoke-Sql $connectionA "UPDATE Jobs SET ExpiresAt=DATEADD(second,120,SYSUTCDATETIME()) WHERE Id=1 AND Owner='worker-a' AND Generation=1 AND ExpiresAt>SYSUTCDATETIME(); SELECT @@ROWCOUNT;"
    Require ($expiredRenewal -eq 0) 'An expired worker renewed its lease.'
    $generation = Invoke-Sql $connectionB "UPDATE Jobs SET Owner='worker-b',Generation=Generation+1,ExpiresAt=DATEADD(second,120,SYSUTCDATETIME()) OUTPUT inserted.Generation WHERE Id=1 AND ExpiresAt<=SYSUTCDATETIME()"
    Require ($generation -eq 2) 'Reclaim did not advance the lease generation.'
    $stalePublication = Invoke-Sql $connectionA "UPDATE Jobs SET State='Succeeded' WHERE Id=1 AND Owner='worker-a' AND Generation=1 AND ExpiresAt>SYSUTCDATETIME(); SELECT @@ROWCOUNT;"
    Require ($stalePublication -eq 0) 'Old worker could complete a reclaimed job.'
    Require ((Invoke-Sql $observer 'SELECT State FROM Jobs WHERE Id=1') -eq 'Running') 'Stale completion overwrote the new attempt.'
    Pass 'Database-clock expiry and generation checks reject expired or reclaimed workers'

    $firstResult = Run-IdempotentFixtureCommand $connectionA ('A' * 64)
    $replayResult = Run-IdempotentFixtureCommand $connectionB ('A' * 64)
    Require ($firstResult -eq $replayResult) 'Identical command replay returned a different result.'
    Require ((Invoke-Sql $observer "SELECT COUNT(*) FROM Audit WHERE Scope='replay'") -eq 1) 'Command replay appended another financial transition.'
    Pass 'Same request and payload return one committed result'
    $conflict = $false
    try { $null = Run-IdempotentFixtureCommand $connectionB ('B' * 64) }
    catch { if ($_.Exception.Message -ne 'PAYLOAD_CONFLICT') { throw }; $conflict = $true }
    Require $conflict 'Different payload reused the earlier request result.'
    Require ((Invoke-Sql $observer "SELECT COUNT(*) FROM Requests WHERE Scope='replay'") -eq 1) 'Payload conflict changed the recorded request.'
    Pass 'Same request with changed payload conflicts without another mutation'

    $null = Invoke-Sql $connectionA "INSERT ReviewGroups VALUES ('export-group','export','mapping-1','Approved',1)"
    $transactionA = $connectionA.BeginTransaction()
    $transactionB = $connectionB.BeginTransaction()
    try {
        $capturedGeneration = Invoke-Sql $connectionA "SELECT Generation FROM Guards WITH (UPDLOCK,HOLDLOCK) WHERE Scope='export'" $transactionA
        $read = Start-GuardRead $connectionB $transactionB 'export'
        Wait-ForBlocking $observer $read.Pending $sessionB $sessionA
        $capturedStatus = Invoke-Sql $connectionA "SELECT Status FROM ReviewGroups WHERE Id='export-group'" $transactionA
        $transactionA.Commit()
        $null = Finish-GuardRead $read
        $null = Invoke-Sql $connectionB "UPDATE Guards SET Generation=2 WHERE Scope='export'; UPDATE ReviewGroups SET Status='Invalidated' WHERE Id='export-group';" $transactionB
        $transactionB.Commit()
    } finally { $transactionA.Dispose(); $transactionB.Dispose() }
    Require ($capturedGeneration -eq 1 -and $capturedStatus -eq 'Approved') 'Export capture mixed financial generations.'
    Require ((Invoke-Sql $observer "SELECT Generation FROM Guards WHERE Scope='export'") -eq 2) 'Publication failed to proceed after export capture.'
    Pass 'Guarded export capture observes one generation while publication waits'

    Write-Output ("All {0} SQL mechanism checks passed. Synthetic schema only; production services and finance acceptance remain unverified." -f $script:passed)
} finally {
    foreach ($connection in @($connectionA,$connectionB,$observer)) {
        if ($null -ne $connection) { $connection.Dispose() }
    }
    if ($created -and $null -ne $master) {
        # Cleanup can target only the generated database created by this invocation.
        if ($databaseName -notmatch '^DikeSqlChecks_[0-9a-f]{32}$') { throw 'Refusing unexpected database cleanup target.' }
        try {
            $null = Invoke-Sql $master "ALTER DATABASE [$databaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$databaseName];"
            Write-Output ('Removed disposable database ' + $databaseName)
        } finally { $master.Dispose() }
    } elseif ($null -ne $master) { $master.Dispose() }
}
