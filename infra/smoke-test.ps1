#Requires -Version 7
<#
.SYNOPSIS
  Smoke test krytycznego scenariusza demo FlowBB przeciwko uruchomionemu API (issue #22).

.DESCRIPTION
  Wykonuje po kolei kroki z AGENTS.md sekcja 2 przez HTTP: health, wydarzenia, Attendance (idempotencja),
  PULSE (KPI, mapa heksagonow, prywatnosc), trasa, Crew, hub SignalR, sprzatanie.
  Kazdy krok konczy sie wynikiem PASS, FAIL albo SKIP. SKIP oznacza, ze endpoint nie jest jeszcze podpiety
  w API (404 bez ProblemDetails) - to nie jest blad, ale scenariusz nie jest wtedy w pelni sprawdzony.
  Kod wyjscia: 0 gdy brak FAIL, 1 gdy jest choc jeden FAIL, 2 gdy API jest nieosiagalne.

  Skrypt nie sprawdza samego komunikatu SignalR (patrz docs/DEMO_RUNBOOK.md); sprawdza tylko dostepnosc huba.
  Nie zapisuje sekretow ani danych osobowych. Uzywaj syntetycznego uzytkownika z seedu.

.EXAMPLE
  pwsh infra/smoke-test.ps1 -BaseUrl http://localhost:8080
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:8080',
    [Guid]$EventId = '11111111-1111-1111-1111-111111111111',
    [Guid]$UserId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    # Wydarzenie z mikrogrupami (w seedzie tylko 1111...); uzytkownik demo nie nalezy do zadnej grupy.
    [Guid]$CrewEventId = '11111111-1111-1111-1111-111111111111',
    [ValidateSet('Walking', 'PublicTransport', 'Bike', 'Car', 'Unknown')]
    [string]$TransportMode = 'PublicTransport',
    [double]$OriginLatitude = 49.82245,
    [double]$OriginLongitude = 19.04431,
    [switch]$KeepData
)

$ErrorActionPreference = 'Stop'
$script:Results = [System.Collections.Generic.List[object]]::new()

function Add-Result {
    param([string]$Step, [ValidateSet('PASS', 'FAIL', 'SKIP')][string]$Status, [string]$Detail = '')
    $script:Results.Add([pscustomobject]@{ Step = $Step; Status = $Status; Detail = $Detail })
    $color = @{ PASS = 'Green'; FAIL = 'Red'; SKIP = 'Yellow' }[$Status]
    Write-Host ("[{0}] {1}{2}" -f $Status, $Step, $(if ($Detail) { " - $Detail" } else { '' })) -ForegroundColor $color
}

function Invoke-Api {
    param([string]$Method, [string]$Path, $Body = $null)
    $request = @{
        Uri                = "$($BaseUrl.TrimEnd('/'))$Path"
        Method             = $Method
        SkipHttpErrorCheck = $true
        TimeoutSec         = 15
    }
    if ($null -ne $Body) {
        $request.Body = $Body | ConvertTo-Json -Depth 6
        $request.ContentType = 'application/json'
    }

    try {
        $response = Invoke-WebRequest @request
    }
    catch {
        return [pscustomobject]@{ Status = 0; Reachable = $false; Mounted = $false; Json = $null; Raw = ''; ContentType = ''; Error = $_.Exception.Message }
    }

    $contentType = ($response.Headers['Content-Type'] | Select-Object -First 1) ?? ''
    # application/geo+json nie jest rozpoznawany jako tekst: PowerShell zwraca wtedy byte[].
    $raw = if ($response.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($response.Content) } else { [string]$response.Content }
    $json = $null
    if ($raw) { try { $json = $raw | ConvertFrom-Json -Depth 20 } catch { $json = $null } }

    # 404 bez ProblemDetails = trasa nie istnieje. 404 z ProblemDetails = poprawna odpowiedz domenowa.
    $unmounted = $response.StatusCode -eq 404 -and $contentType -notlike '*problem+json*'
    [pscustomobject]@{
        Status = [int]$response.StatusCode; Reachable = $true; Mounted = -not $unmounted
        Json = $json; Raw = $raw; ContentType = $contentType; Error = ''
    }
}

function Test-Step {
    # Uruchamia blok kroku. Blok zwraca tekst szczegolow, rzuca wyjatek przy niepowodzeniu,
    # a zwrocenie $null oznacza SKIP (endpoint niepodpiety).
    param([string]$Step, [scriptblock]$Body)
    try {
        $detail = & $Body
        if ($null -eq $detail) { Add-Result $Step 'SKIP' 'endpoint niepodpiety w API' }
        else { Add-Result $Step 'PASS' ([string]$detail) }
    }
    catch { Add-Result $Step 'FAIL' $_.Exception.Message }
}

function Assert-That {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

Write-Host "FlowBB smoke test -> $BaseUrl (event $EventId, user $UserId, tryb $TransportMode)" -ForegroundColor Cyan

# 1. Health
$health = Invoke-Api GET '/health'
if (-not $health.Reachable) {
    Add-Result 'API /health' 'FAIL' "API nieosiagalne: $($health.Error)"
    exit 2
}
Test-Step 'API /health' {
    Assert-That ($health.Status -eq 200) "oczekiwano 200, jest $($health.Status)"
    Assert-That ($health.Json.status -eq 'Healthy') 'oczekiwano status=Healthy'
    Assert-That ($null -ne $health.Json.timestamp) 'oczekiwano pola timestamp'
    'status=Healthy'
}

# 2. Wydarzenia (Events)
Test-Step 'GET /api/events zawiera wydarzenie demo' {
    $r = Invoke-Api GET '/api/events'
    if (-not $r.Mounted) { return $null }
    Assert-That ($r.Status -eq 200) "oczekiwano 200, jest $($r.Status)"
    Assert-That (@($r.Json | Where-Object { $_.id -eq "$EventId" }).Count -eq 1) "brak wydarzenia $EventId w liscie"
    "$(@($r.Json).Count) wydarzen"
}

# 3-5. Attendance: pierwszy zapis, idempotencja, zmiana trybu
$script:FirstCount = $null
$script:CreatedByThisRun = $false
$attendancePath = "/api/events/$EventId/attendance"
$attendanceBody = @{ userId = "$UserId"; transportMode = $TransportMode }

Test-Step 'POST attendance (pierwszy zapis)' {
    $r = Invoke-Api POST $attendancePath $attendanceBody
    if (-not $r.Mounted) { return $null }
    Assert-That ($r.Status -eq 200) "oczekiwano 200, jest $($r.Status): $($r.Raw)"
    Assert-That ($r.Json.transportMode -eq $TransportMode) "tryb w odpowiedzi: $($r.Json.transportMode)"
    Assert-That ($r.Json.participantsCount -ge 1) 'participantsCount < 1'
    Assert-That ([bool]$r.Json.isNew) 'uzytkownik demo ma juz deklaracje w seedzie (oczekiwano isNew=true)'
    $script:FirstCount = [int]$r.Json.participantsCount
    $script:CreatedByThisRun = [bool]$r.Json.isNew
    "isNew=$($r.Json.isNew), participantsCount=$($r.Json.participantsCount)"
}

if ($null -ne $script:FirstCount) {
    Test-Step 'POST attendance (ponowienie jest idempotentne)' {
        $r = Invoke-Api POST $attendancePath $attendanceBody
        Assert-That ($r.Status -eq 200) "oczekiwano 200, jest $($r.Status)"
        Assert-That (-not $r.Json.isNew) 'ponowienie zwrocilo isNew=true'
        Assert-That ([int]$r.Json.participantsCount -eq $script:FirstCount) "licznik wzrosl: $($script:FirstCount) -> $($r.Json.participantsCount)"
        "licznik bez zmian ($($script:FirstCount))"
    }

    Test-Step 'POST attendance (zmiana trybu nie zmienia licznika)' {
        $other = if ($TransportMode -eq 'Bike') { 'Walking' } else { 'Bike' }
        $r = Invoke-Api POST $attendancePath @{ userId = "$UserId"; transportMode = $other }
        Assert-That ($r.Status -eq 200) "oczekiwano 200, jest $($r.Status)"
        Assert-That ([int]$r.Json.participantsCount -eq $script:FirstCount) 'zmiana trybu zmienila licznik'
        $back = Invoke-Api POST $attendancePath $attendanceBody
        Assert-That ($back.Status -eq 200) 'powrot do pierwotnego trybu nie powiodl sie'
        "tryb $TransportMode -> $other -> $TransportMode, licznik $($script:FirstCount)"
    }

    Test-Step 'POST attendance (bledne dane -> 400)' {
        $r = Invoke-Api POST $attendancePath @{ userId = "$UserId"; transportMode = 'Teleport' }
        Assert-That ($r.Status -eq 400) "oczekiwano 400, jest $($r.Status)"
        '400 dla nieznanego trybu'
    }
}

# 6-7. PULSE: KPI i spojnosc z Attendance
Test-Step 'GET /api/pulse/events/{id} zgodne z Attendance' {
    $r = Invoke-Api GET "/api/pulse/events/$EventId"
    if (-not $r.Mounted) { return $null }
    Assert-That ($r.Status -eq 200) "oczekiwano 200, jest $($r.Status)"
    $split = $r.Json.modalSplit
    $sum = $split.publicTransport + $split.walking + $split.bike + $split.car + $split.unknown
    Assert-That ($sum -eq $r.Json.participantsCount) "modalSplit ($sum) != participantsCount ($($r.Json.participantsCount))"
    if ($null -ne $script:FirstCount) {
        Assert-That ($r.Json.participantsCount -eq $script:FirstCount) "PULSE $($r.Json.participantsCount) != Attendance $($script:FirstCount)"
    }
    "participantsCount=$($r.Json.participantsCount)"
}

Test-Step 'GET /api/pulse/summary' {
    $r = Invoke-Api GET '/api/pulse/summary'
    if (-not $r.Mounted) { return $null }
    Assert-That ($r.Status -eq 200) "oczekiwano 200, jest $($r.Status)"
    Assert-That ($r.Json.eventsCount -ge 1) 'eventsCount < 1'
    "eventsCount=$($r.Json.eventsCount), participantsCount=$($r.Json.participantsCount)"
}

# 8. Mapa heksagonow i prywatnosc
Test-Step 'GET /api/pulse/hexagons (GeoJSON, prywatnosc count >= 10)' {
    $r = Invoke-Api GET "/api/pulse/hexagons?eventId=$EventId"
    if (-not $r.Mounted) { return $null }
    Assert-That ($r.Status -eq 200) "oczekiwano 200, jest $($r.Status)"
    Assert-That ($r.ContentType -like '*geo+json*') "Content-Type: $($r.ContentType)"
    Assert-That ($r.Json.type -eq 'FeatureCollection') 'to nie FeatureCollection'
    foreach ($feature in $r.Json.features) {
        Assert-That ($feature.properties.participants -ge 10) "komorka $($feature.id) ma $($feature.properties.participants) < 10 osob"
        $ring = $feature.geometry.coordinates[0]
        Assert-That (($ring[0] | ConvertTo-Json -Compress) -eq ($ring[-1] | ConvertTo-Json -Compress)) "pierscien $($feature.id) niezamkniety"
    }
    Assert-That ($r.Raw -notmatch '(?i)userid') 'odpowiedz zawiera userId'
    $shown = @($r.Json.features).Count
    Assert-That ($shown -gt 0) 'brak komorek >= 10 osob - mapa demo jest pusta'
    "$shown komorek, wszystkie >= 10 osob, bez userId"
}

# 9. Trasa (Demo planner). Kontrakt: GET ?userId= (develop) lub POST z origin (develop-client) - probujemy obu.
Test-Step 'Trasa z DemoRoutePlanner (deterministyczna)' {
    $path = "/api/events/$EventId/route"
    $get = { Invoke-Api GET "$path`?userId=$UserId" }
    $post = { Invoke-Api POST $path @{ userId = "$UserId"; origin = @{ latitude = $OriginLatitude; longitude = $OriginLongitude } } }
    $r = & $get
    if ($r.Status -eq 405 -or $r.Status -eq 400) { $call = $post; $r = & $call } else { $call = $get }
    if (-not $r.Mounted) { return $null }
    Assert-That ($r.Status -eq 200) "oczekiwano 200, jest $($r.Status): $($r.Raw)"
    Assert-That ($r.Json.plannerSource -eq 'Demo') "plannerSource=$($r.Json.plannerSource), oczekiwano Demo"
    Assert-That ($null -ne $r.Json.outbound -and $null -ne $r.Json.returns) 'brak outbound lub returns'
    $again = & $call
    Assert-That ($again.Raw -eq $r.Raw) 'dwa wywolania daly rozne trasy (planer nie jest deterministyczny)'
    "plannerSource=Demo, wynik powtarzalny"
}

# 10. Crew: lista, dolaczenie (idempotentne), opuszczenie
Test-Step 'Crew: lista, dolaczenie, ponowienie i opuszczenie' {
    $groups = Invoke-Api GET "/api/events/$CrewEventId/groups?userId=$UserId"
    if (-not $groups.Mounted) { return $null }
    Assert-That ($groups.Status -eq 200) "oczekiwano 200, jest $($groups.Status)"
    $joinable = @($groups.Json | Where-Object { -not $_.joinedByCurrentUser -and $_.currentMembers -lt $_.maxMembers }) | Select-Object -First 1
    Assert-That ($null -ne $joinable) "brak grupy z wolnym miejscem dla uzytkownika $UserId w wydarzeniu $CrewEventId (seed?)"

    $membersPath = "/api/groups/$($joinable.id)/members"
    $join = Invoke-Api POST $membersPath @{ userId = "$UserId" }
    Assert-That ($join.Status -eq 200) "dolaczenie: oczekiwano 200, jest $($join.Status): $($join.Raw)"
    Assert-That ($join.Json.joinedByCurrentUser) 'joinedByCurrentUser=false po dolaczeniu'
    Assert-That ($join.Json.currentMembers -eq $joinable.currentMembers + 1) 'currentMembers nie wzroslo o 1'

    $again = Invoke-Api POST $membersPath @{ userId = "$UserId" }
    Assert-That ($again.Status -eq 200 -and $again.Json.currentMembers -eq $join.Json.currentMembers) 'ponowne dolaczenie nie jest idempotentne'

    $leave = Invoke-Api DELETE "$membersPath/$UserId"
    Assert-That ($leave.Status -eq 204) "opuszczenie: oczekiwano 204, jest $($leave.Status)"
    $leaveAgain = Invoke-Api DELETE "$membersPath/$UserId"
    Assert-That ($leaveAgain.Status -eq 204) 'ponowne opuszczenie nie zwrocilo 204'
    "grupa $($joinable.name): dolaczenie +1, ponowienie bez zmian, opuszczenie 204 x2"
}

# 11. Hub SignalR (tylko dostepnosc; sam komunikat: docs/DEMO_RUNBOOK.md)
Test-Step 'Hub /hubs/pulse (negocjacja SignalR)' {
    $r = Invoke-Api POST '/hubs/pulse/negotiate?negotiateVersion=1'
    if (-not $r.Mounted) { return $null }
    Assert-That ($r.Status -eq 200) "oczekiwano 200, jest $($r.Status)"
    Assert-That (-not [string]::IsNullOrEmpty($r.Json.connectionId)) 'brak connectionId w negocjacji'
    'negocjacja OK'
}

# 12. Sprzatanie: usuwamy tylko to, co utworzyl ten przebieg
if ($null -ne $script:FirstCount) {
    if ($KeepData -or -not $script:CreatedByThisRun) {
        Add-Result 'DELETE attendance (sprzatanie)' 'SKIP' 'pominieto (-KeepData albo deklaracja istniala przed testem)'
    }
    else {
        Test-Step 'DELETE attendance (idempotentne sprzatanie)' {
            $first = Invoke-Api DELETE "$attendancePath/$UserId"
            $second = Invoke-Api DELETE "$attendancePath/$UserId"
            Assert-That ($first.Status -eq 204) "pierwszy DELETE: oczekiwano 204, jest $($first.Status)"
            Assert-That ($second.Status -eq 204) "drugi DELETE: oczekiwano 204, jest $($second.Status)"
            $pulse = Invoke-Api GET "/api/pulse/events/$EventId"
            if ($pulse.Mounted -and $pulse.Status -eq 200) {
                Assert-That ($pulse.Json.participantsCount -eq $script:FirstCount - 1) "po usunieciu licznik $($pulse.Json.participantsCount), oczekiwano $($script:FirstCount - 1)"
            }
            '204 x2, licznik wrocil do stanu sprzed testu'
        }
    }
}

# Podsumowanie
$pass = @($script:Results | Where-Object Status -eq 'PASS').Count
$fail = @($script:Results | Where-Object Status -eq 'FAIL').Count
$skip = @($script:Results | Where-Object Status -eq 'SKIP').Count
Write-Host ''
Write-Host ("Wynik: {0} PASS, {1} FAIL, {2} SKIP" -f $pass, $fail, $skip) -ForegroundColor $(if ($fail) { 'Red' } elseif ($skip) { 'Yellow' } else { 'Green' })
if ($skip) { Write-Host 'SKIP = endpoint niepodpiety w API; scenariusz nie jest w pelni sprawdzony.' -ForegroundColor Yellow }
exit ([int]($fail -gt 0))
