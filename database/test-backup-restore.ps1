[CmdletBinding()]
param(
    [string]$Image = "neo4j:5.26.30-community",
    [int]$BoltPort = 17687
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$password = $env:FLOWBB_NEO4J_TEST_PASSWORD
$confirmed = $env:FLOWBB_NEO4J_TEST_CONFIRM_DISPOSABLE
if ([string]::IsNullOrWhiteSpace($password) -or $confirmed -ne "true") {
    throw "Ustaw FLOWBB_NEO4J_TEST_PASSWORD oraz FLOWBB_NEO4J_TEST_CONFIRM_DISPOSABLE=true dla jednorazowej bazy."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runId = [Guid]::NewGuid().ToString("N")
$containerName = "flowbb-backup-test-$runId"
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$backupPath = [IO.Path]::GetFullPath((Join-Path $tempRoot $containerName))
if (-not $backupPath.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Katalog roboczy backupu musi znajdowac sie w katalogu tymczasowym."
}

$testResultsPath = Join-Path $backupPath "test-results"
$previousEnvironment = @{
    Uri      = $env:FLOWBB_NEO4J_TEST_URI
    Database = $env:FLOWBB_NEO4J_TEST_DATABASE
    Username = $env:FLOWBB_NEO4J_TEST_USERNAME
}

function Invoke-DockerChecked {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & docker @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Polecenie Docker zakonczylo sie kodem $LASTEXITCODE."
    }
}

function Wait-Neo4j {
    $deadline = (Get-Date).AddSeconds(60)
    do {
        & docker exec $containerName cypher-shell -u neo4j -p $password "RETURN 1" *> $null
        if ($LASTEXITCODE -eq 0) {
            return
        }

        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    throw "Neo4j nie uruchomil sie w ciagu 60 sekund."
}

function Invoke-CypherText {
    param([Parameter(Mandatory)][string]$Cypher)

    $Cypher | & docker exec -i $containerName cypher-shell -u neo4j -p $password -d neo4j *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "cypher-shell zakonczyl sie kodem $LASTEXITCODE."
    }
}

function Initialize-DemoData {
    Get-ChildItem (Join-Path $PSScriptRoot "migrations") -Filter "*.cypher" |
        Sort-Object Name |
        ForEach-Object { Invoke-CypherText (Get-Content -Raw -LiteralPath $_.FullName) }

    $seed = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "flowbb-demo-seed.cypher")
    $markerIndex = $seed.IndexOf("// __FLOWBB_SEED_END__", [StringComparison]::Ordinal)
    if ($markerIndex -lt 0) {
        throw "Nie znaleziono znacznika konca seedu."
    }

    Invoke-CypherText $seed.Substring(0, $markerIndex)
}

function Get-DatabaseSnapshot {
    $query = @"
CALL { MATCH (n) RETURN count(n) AS nodes }
CALL { MATCH ()-[r]->() RETURN count(r) AS relationships }
MATCH (version:SchemaVersion {Key: 'flowbb'})
RETURN toString(nodes) + '|' + toString(relationships) + '|' + toString(version.Version) + '|' + version.Name AS Snapshot
"@
    $output = & docker exec $containerName cypher-shell -u neo4j -p $password -d neo4j --format plain $query
    if ($LASTEXITCODE -ne 0) {
        throw "Nie udalo sie odczytac migawki bazy."
    }

    $snapshot = $output | Where-Object { $_ -match '^"?\d+\|\d+\|\d+\|[^"\r\n]+"?$' } | Select-Object -Last 1
    if ($null -eq $snapshot) {
        throw "Nie rozpoznano wyniku migawki bazy."
    }

    return $snapshot.Trim('"')
}

function Assert-DatabaseIsEmpty {
    $output = & docker exec $containerName cypher-shell -u neo4j -p $password -d neo4j --format plain "MATCH (n) RETURN count(n) AS Nodes"
    if ($LASTEXITCODE -ne 0 -or $output -notcontains "0") {
        throw "Jednorazowa baza nie zostala wyczyszczona przed restore."
    }
}

function Invoke-AdapterTests {
    New-Item -ItemType Directory -Path $testResultsPath -Force | Out-Null
    $env:FLOWBB_NEO4J_TEST_URI = "neo4j://127.0.0.1:$BoltPort"
    $env:FLOWBB_NEO4J_TEST_DATABASE = "neo4j"
    $env:FLOWBB_NEO4J_TEST_USERNAME = "neo4j"
    Push-Location $repositoryRoot
    try {
        & dotnet test backend/tests/FlowBB.Infrastructure.Tests/FlowBB.Infrastructure.Tests.csproj --logger "trx;LogFileName=restore.trx" --results-directory $testResultsPath | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Testy adapterow po restore nie przeszly."
        }
    }
    finally {
        Pop-Location
    }

    [xml]$trx = Get-Content -Raw -LiteralPath (Join-Path $testResultsPath "restore.trx")
    $counters = $trx.TestRun.ResultSummary.Counters
    $failed = [int]$counters.GetAttribute("failed")
    $skipped = [int]$counters.GetAttribute("skipped")
    if ($failed -ne 0 -or $skipped -ne 0) {
        throw "Testy adapterow: failed=$failed, skipped=$skipped."
    }

    return [int]$counters.GetAttribute("passed")
}

New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
try {
    Invoke-DockerChecked @(
        "run", "-d", "--name", $containerName,
        "-p", "127.0.0.1:${BoltPort}:7687",
        "--mount", "type=bind,source=$backupPath,target=/backups",
        "-e", "NEO4J_AUTH=neo4j/$password", $Image)
    Wait-Neo4j
    Initialize-DemoData
    $before = Get-DatabaseSnapshot

    Invoke-DockerChecked @("stop", $containerName)
    Invoke-DockerChecked @("run", "--rm", "--volumes-from", $containerName, $Image,
        "neo4j-admin", "database", "dump", "neo4j", "--to-path=/backups", "--overwrite-destination=true")

    Invoke-DockerChecked @("start", $containerName)
    Wait-Neo4j
    Invoke-CypherText "MATCH (n) DETACH DELETE n;"
    Assert-DatabaseIsEmpty
    Invoke-DockerChecked @("stop", $containerName)
    Invoke-DockerChecked @("run", "--rm", "--volumes-from", $containerName, $Image,
        "neo4j-admin", "database", "load", "neo4j", "--from-path=/backups", "--overwrite-destination=true")

    Invoke-DockerChecked @("start", $containerName)
    Wait-Neo4j
    $restored = Get-DatabaseSnapshot
    if ($restored -ne $before) {
        throw "Restore zmienil stan: przed=$before, po=$restored."
    }

    $passedTests = Invoke-AdapterTests
    $afterTests = Get-DatabaseSnapshot
    if ($afterTests -ne $before) {
        throw "Testy adapterow zmienily odtworzony stan: przed=$before, po=$afterTests."
    }

    Write-Output "BACKUP_RESTORE snapshot=$restored adapter_tests_passed=$passedTests adapter_tests_skipped=0"
}
finally {
    & docker rm -f -v $containerName *> $null
    Remove-Item -LiteralPath $backupPath -Recurse -Force -ErrorAction SilentlyContinue
    $env:FLOWBB_NEO4J_TEST_URI = $previousEnvironment.Uri
    $env:FLOWBB_NEO4J_TEST_DATABASE = $previousEnvironment.Database
    $env:FLOWBB_NEO4J_TEST_USERNAME = $previousEnvironment.Username
}
