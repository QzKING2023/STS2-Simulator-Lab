#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$OutputRoot = '.build/runs/m2-5-kaguya-20261112',
    [string]$PrimaryRoot = '.build/runs/m2-5-kaguya-20261112-primary-final',
    [string]$RepeatRoot = '.build/runs/m2-5-kaguya-20261112-repeat-final',
    [string]$ReplayRoot = '.build/runs/m2-5-kaguya-20261112-replay-final',
    [string]$FailureRoot = '.build/runs/m2-5-kaguya-20261112-failure-final',
    [string]$EventRoot = '.build/runs/m2-5-kaguya-20261112-event-primary-final',
    [string]$RegistrationFailureRoot = '.build/runs/m2-5-kaguya-20261112-r3-event-primary'
)
$ErrorActionPreference = 'Stop'

function Read-Json([string]$Path) { Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json }
function Write-Json([string]$Path, $Value) {
    $Value | ConvertTo-Json -Depth 24 | Set-Content -LiteralPath $Path -Encoding utf8
}
function Rel([string]$Path) { [IO.Path]::GetRelativePath((Get-Location).Path, [IO.Path]::GetFullPath($Path)).Replace('\', '/') }

$root = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $root) { throw "Evidence root already exists: $root" }
New-Item -ItemType Directory -Path $root | Out-Null

$primary = Read-Json (Join-Path $PrimaryRoot 'run-manifest.json')
$repeat = Read-Json (Join-Path $RepeatRoot 'run-manifest.json')
$replay = Read-Json (Join-Path $ReplayRoot 'run-manifest.json')
$failure = Read-Json (Join-Path $FailureRoot 'run-manifest.json')
$eventManifest = Read-Json (Join-Path $EventRoot 'run-manifest.json')
$eventProbe = Read-Json (Join-Path $EventRoot 'kaguya-stage8-probe.json')
$inventory = Read-Json (Join-Path $EventRoot 'kaguya-content-inventory.json')

$registeredByType = @{}
foreach ($row in @($inventory.registeredUnits)) {
    if (!$registeredByType.ContainsKey([string]$row.type)) { $registeredByType[[string]$row.type] = $row }
}

$units = [Collections.Generic.List[object]]::new()
foreach ($row in @($inventory.declaredUnits)) {
    $type = [string]$row.type
    $registered = $registeredByType[$type]
    $status = 'Unrun'
    $reason = 'Native registration was observed, but the bounded M2-5 run did not enter this unit lifecycle.'
    $evidence = @(Rel (Join-Path $EventRoot 'kaguya-content-inventory.json'))
    $checkpoints = @('discovery', 'registration')
    if ($null -eq $registered) {
        $status = 'Unsupported'
        $reason = 'The scoped native profile did not register this declared type. A broad Card/Relic injection attempt retained a real DuplicateModelException and is not promoted to compatibility.'
        $evidence = @(Rel (Join-Path $RegistrationFailureRoot 'result.json'), Rel (Join-Path $EventRoot 'kaguya-content-inventory.json'))
        $checkpoints = @()
    }
    if ($type -eq 'Kaguya.Events.TaketoriMonogatariEvent') {
        $status = 'Covered'
        $reason = 'Current-build native event probe completed option, result, ledger, cleanup, and teardown.'
        $evidence = @(Rel (Join-Path $EventRoot 'kaguya-stage8-probe.json'))
        $checkpoints = @('discovery', 'registration', 'construction', 'entered', 'option', 'effect', 'ledger', 'cleanup', 'teardown')
    }
    if ($type -eq 'Kaguya.Encounters.MixedSoldiersEliteEncounter') {
        $status = 'Partial'
        $reason = 'Current-build full-run entered the encounter and reached native Death. Reward boundary was not reached.'
        $evidence = @(Rel (Join-Path $PrimaryRoot 'kaguya-stage8-probe.json'))
        $checkpoints = @('discovery', 'registration', 'construction', 'entered', 'death-boundary')
    }
    if ($type -eq 'Kaguya.Monsters.BattleSoldierA') {
        $status = 'Partial'
        $reason = 'Current-build full-run observed real intent and enemy behavior before native Death. Reward and post-victory cleanup were not reached.'
        $evidence = @(Rel (Join-Path $PrimaryRoot 'kaguya-stage8-probe.json'))
        $checkpoints = @('discovery', 'registration', 'construction', 'intent', 'behavior', 'death-boundary')
    }
    $units.Add([ordered]@{
        contentUnitId = if ($null -ne $registered) { [string]$registered.id } else { [string]$row.registrationIdentity }
        kind = [string]$row.kind
        type = $type
        status = $status
        registrationIdentity = if ($null -ne $registered) { [string]$registered.registrationIdentity } else { [string]$row.registrationIdentity }
        lifecycleCheckpoints = $checkpoints
        interactionContracts = @()
        evidence = $evidence
        reason = $reason
    })
}
$units.Add([ordered]@{
    contentUnitId = 'REWARD.KAGUYA_TAKETORI_OPTION'
    kind = 'Reward'
    type = 'event-result'
    status = 'Covered'
    registrationIdentity = 'REWARD.KAGUYA_TAKETORI_OPTION@0.111.0'
    lifecycleCheckpoints = @('discovery', 'resolution', 'ledger', 'cleanup')
    interactionContracts = @('event-option-to-reward', 'resource-ledger')
    evidence = @(Rel (Join-Path $EventRoot 'kaguya-stage8-probe.json'))
    reason = 'Observed in the current-build native event probe.'
})

$counts = @{}
foreach ($unit in $units) {
    if (!$counts.ContainsKey([string]$unit.status)) { $counts[[string]$unit.status] = 0 }
    $counts[[string]$unit.status]++
}
$content = [ordered]@{
    schema = 'kaguya-m2-5-content-matrix/v1'
    targetGameVersion = '0.111.0'
    snapshotId = 'workshop-snapshot-20261106-163352'
    snapshotContentSha256 = '7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73'
    modId = 'Kaguya'
    workshopId = '3747548629'
    modVersion = '3.2.4'
    inventory = [ordered]@{
        declaredModelTypes = @($inventory.declaredUnits).Count
        runtimeRegistered = @($inventory.registeredUnits).Count
        appendedObserved = 1
    }
    denominator = [ordered]@{
        total = $units.Count
        covered = $counts['Covered']
        partial = $counts['Partial']
        unsupported = $counts['Unsupported']
        unrun = $counts['Unrun']
        runtimeFailure = 0
    }
    units = @($units)
}
Write-Json (Join-Path $root 'content-matrix.json') $content

$lifecycle = [ordered]@{
    schema = 'kaguya-m2-5-lifecycle-matrix/v1'
    source = 'content-matrix.json'
    units = @($units | ForEach-Object {
        [ordered]@{
            contentUnitId = $_.contentUnitId
            kind = $_.kind
            status = $_.status
            requiredCheckpoints = $_.lifecycleCheckpoints
            observedCheckpoints = $_.lifecycleCheckpoints
            evidence = $_.evidence
            reason = $_.reason
        }
    })
}
Write-Json (Join-Path $root 'lifecycle-matrix.json') $lifecycle

$interaction = [ordered]@{
    schema = 'kaguya-m2-5-interaction-risk/v1'
    contracts = @(
        [ordered]@{ id = 'exact-baselib-closure'; status = 'Covered'; scope = 'BaseLib@3.4.7 -> Kaguya@3.2.4'; evidence = @(Rel (Join-Path $PrimaryRoot 'run-manifest.json')) }
        [ordered]@{ id = 'encounter-registration-and-entry'; status = 'Covered'; scope = 'MixedSoldiersEliteEncounter'; evidence = @(Rel (Join-Path $PrimaryRoot 'kaguya-stage8-probe.json')) }
        [ordered]@{ id = 'enemy-intent-and-behavior'; status = 'Partial'; scope = 'BattleSoldierA'; evidence = @(Rel (Join-Path $PrimaryRoot 'kaguya-stage8-probe.json')); reason = 'Native Death precedes reward and victory cleanup.' }
        [ordered]@{ id = 'event-option-result-ledger'; status = 'Covered'; scope = 'TaketoriMonogatariEvent -> REWARD.KAGUYA_TAKETORI_OPTION'; evidence = @(Rel (Join-Path $EventRoot 'kaguya-stage8-probe.json')) }
        [ordered]@{ id = 'card-relic-registration'; status = 'Unsupported'; scope = '334 Card and 73 Relic declared types'; evidence = @(Rel (Join-Path $RegistrationFailureRoot 'result.json')); reason = 'Broad injection hits native DuplicateModelException on Kaguya.Cards.Clash versus CARD.CLASH.' }
        [ordered]@{ id = 'cross-room-persistence'; status = 'Unrun'; scope = 'Kaguya content outside selected encounter/event'; evidence = @(Rel (Join-Path $EventRoot 'kaguya-content-inventory.json')) }
        [ordered]@{ id = 'visible-policy'; status = 'Unsupported'; scope = 'player-visible policy'; evidence = @(Rel (Join-Path $PrimaryRoot 'canonical-input.json')); reason = 'Native instrumentation is execution evidence and does not prove visible-policy compatibility.' }
    )
}
Write-Json (Join-Path $root 'interaction-risk.json') $interaction

$platform = [ordered]@{
    schema = 'kaguya-m2-5-platform-capabilities/v1'
    capabilities = @(
        [ordered]@{ id = 'headless-native-execution'; status = 'Covered'; evidence = @(Rel (Join-Path $PrimaryRoot 'run-manifest.json')) }
        [ordered]@{ id = 'audio'; status = 'Unrun'; evidence = @(Rel (Join-Path $PrimaryRoot 'canonical-input.json')); reason = 'No independent audio policy claim was made by this Kaguya cohort.' }
        [ordered]@{ id = 'ui-rendering'; status = 'Unsupported'; evidence = @(Rel (Join-Path $PrimaryRoot 'canonical-input.json')) }
        [ordered]@{ id = 'map-presentation'; status = 'Unsupported'; evidence = @(Rel (Join-Path $PrimaryRoot 'canonical-input.json')) }
        [ordered]@{ id = 'formal-save-load'; status = 'Unsupported'; evidence = @(Rel (Join-Path $PrimaryRoot 'canonical-input.json')) }
        [ordered]@{ id = 'network-steam-cloud'; status = 'Unsupported'; evidence = @(Rel (Join-Path $PrimaryRoot 'canonical-input.json')) }
    )
}
Write-Json (Join-Path $root 'platform-capabilities.json') $platform

$foundation = [ordered]@{
    schema = 'kaguya-m2-5-foundation-gate/v1'
    status = 'PASS'
    scope = 'exact BaseLib 3.4.7 closure for Kaguya native profile'
    dependencies = @('BaseLib@3.4.7', 'Kaguya@3.2.4')
    evidence = @(Rel (Join-Path $PrimaryRoot 'run-manifest.json'), 'docs/mods/stage-reports/m2-4-remilia-full-run-report-20261111.md', 'docs/mods/stage-reports/m2-4-lexninja2-full-run-report-20261111.md')
    reason = 'No Mod-specific adapter bypasses the exact dependency closure. Global foundation semantics remain bounded by accepted M2-4 evidence.'
}
Write-Json (Join-Path $root 'foundation-gate.json') $foundation

$canonical = Read-Json (Join-Path $PrimaryRoot 'canonical-input.json')
$evidence = [ordered]@{
    schema = 'kaguya-m2-5-evidence-index/v1'
    canonicalInput = $canonical
    buildManifest = Rel '.build/native-build/kaguya-m2-5-20261112-r4/build-manifest.json'
    runs = @(
        [ordered]@{ kind = 'primary'; directory = (Rel $PrimaryRoot); terminal = $primary.terminal; stop = $primary.stop; inputDigest = $primary.inputDigest; stateDigest = $primary.stateDigest; rngDigest = $primary.rngDigest; traceDigest = $primary.traceDigest; routeDigest = $primary.routeDigest; choiceDigest = $primary.choiceDigest }
        [ordered]@{ kind = 'repeat'; directory = (Rel $RepeatRoot); terminal = $repeat.terminal; stop = $repeat.stop; inputDigest = $repeat.inputDigest; stateDigest = $repeat.stateDigest; rngDigest = $repeat.rngDigest; traceDigest = $repeat.traceDigest; routeDigest = $repeat.routeDigest; choiceDigest = $repeat.choiceDigest }
        [ordered]@{ kind = 'replay'; directory = (Rel $ReplayRoot); terminal = $replay.terminal; stop = $replay.stop; inputDigest = $replay.inputDigest; stateDigest = $replay.stateDigest; rngDigest = $replay.rngDigest; traceDigest = $replay.traceDigest; routeDigest = $replay.routeDigest; choiceDigest = $replay.choiceDigest }
        [ordered]@{ kind = 'failure'; directory = (Rel $FailureRoot); terminal = $failure.terminal; stop = $failure.stop; inputDigest = $failure.inputDigest; stateDigest = $failure.stateDigest; rngDigest = $failure.rngDigest; traceDigest = $failure.traceDigest; routeDigest = $failure.routeDigest; choiceDigest = $failure.choiceDigest }
        [ordered]@{ kind = 'content-event'; directory = (Rel $EventRoot); terminal = $eventManifest.terminal; stop = $eventManifest.stop; inputDigest = $eventManifest.inputDigest; probeDigest = $eventProbe.primaryDigest }
        [ordered]@{ kind = 'registration-failure'; directory = (Rel $RegistrationFailureRoot); terminal = 'Unknown'; stop = 'EngineError'; errorCategory = 'DuplicateModelException'; evidence = (Rel (Join-Path $RegistrationFailureRoot 'result.json')) }
    )
}
Write-Json (Join-Path $root 'evidence-index.json') $evidence

$summary = [ordered]@{
    schema = 'kaguya-m2-5-run-summary/v1'
    modId = 'Kaguya'
    status = 'PARTIAL'
    flowGate = 'PASS'
    contentGate = 'PARTIAL'
    stabilityGate = 'PASS'
    foundationGate = 'PASS'
    contentDenominator = $content.denominator
    terminal = [ordered]@{ primary = 'Death'; repeat = 'Death'; replay = 'Death'; failure = 'Unknown/BudgetExceeded' }
    selectedSurface = [ordered]@{ encounter = 'ENCOUNTER.MIXED_SOLDIERS_ELITE_ENCOUNTER'; enemy = 'MONSTER.BATTLE_SOLDIER_A'; event = 'EVENT.TAKETORI_MONOGATARI_EVENT'; reward = 'REWARD.KAGUYA_TAKETORI_OPTION' }
    evidenceIndex = 'evidence-index.json'
    contentMatrix = 'content-matrix.json'
    lifecycleMatrix = 'lifecycle-matrix.json'
    interactionRisk = 'interaction-risk.json'
    platformCapabilities = 'platform-capabilities.json'
    foundationGateEvidence = 'foundation-gate.json'
}
Write-Json (Join-Path $root 'content-run-summary.json') $summary
Write-Output "wrote $root units=$($units.Count) covered=$($counts['Covered']) partial=$($counts['Partial']) unsupported=$($counts['Unsupported']) unrun=$($counts['Unrun'])"
