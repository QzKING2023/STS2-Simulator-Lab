#requires -Version 7.0
[CmdletBinding()]
param([string]$EvidenceRoot = '.build/runs/m2-5-kaguya-20261112')
$ErrorActionPreference = 'Stop'

function Read-Json([string]$Path) { Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json }
function Require([bool]$Condition, [string]$Message) { if (!$Condition) { throw $Message } }

$root = [IO.Path]::GetFullPath($EvidenceRoot)
$summary = Read-Json (Join-Path $root 'content-run-summary.json')
$content = Read-Json (Join-Path $root 'content-matrix.json')
$index = Read-Json (Join-Path $root 'evidence-index.json')
$foundation = Read-Json (Join-Path $root 'foundation-gate.json')
$primary = Read-Json '.build/runs/m2-5-kaguya-20261112-primary-final/run-manifest.json'
$repeat = Read-Json '.build/runs/m2-5-kaguya-20261112-repeat-final/run-manifest.json'
$replay = Read-Json '.build/runs/m2-5-kaguya-20261112-replay-final/run-manifest.json'
$failure = Read-Json '.build/runs/m2-5-kaguya-20261112-failure-final/run-manifest.json'
$primaryProbe = Read-Json '.build/runs/m2-5-kaguya-20261112-primary-final/kaguya-stage8-probe.json'
$eventProbe = Read-Json '.build/runs/m2-5-kaguya-20261112-event-primary-final/kaguya-stage8-probe.json'
$inventory = Read-Json '.build/runs/m2-5-kaguya-20261112-event-primary-final/kaguya-content-inventory.json'

Require ($summary.status -eq 'PARTIAL') 'Kaguya stage summary must remain PARTIAL'
Require ($summary.flowGate -eq 'PASS' -and $summary.stabilityGate -eq 'PASS') 'flow or stability gate is not PASS'
Require ($foundation.status -eq 'PASS') 'foundation gate is not PASS'
Require ($content.denominator.total -eq 640) 'content denominator drifted'
Require (($content.denominator.covered + $content.denominator.partial + $content.denominator.unsupported + $content.denominator.unrun + $content.denominator.runtimeFailure) -eq 640) 'content states do not sum to denominator'
Require (@($inventory.registeredUnits).Count -eq 220) 'runtime registration count drifted'
Require (@($inventory.declaredUnits).Count -eq 639) 'declared content count drifted'

foreach ($run in @($primary, $repeat, $replay)) {
    Require ($run.terminal -eq 'Death' -and $run.stop -eq 'Completed') 'successful cohort did not reach native Death'
    Require ($run.errorCode -eq 'Completed') 'successful cohort error classification drifted'
}
foreach ($field in @('stateDigest', 'rngDigest', 'traceDigest', 'routeDigest', 'choiceDigest')) {
    Require ($primary.$field -eq $repeat.$field) "primary/repeat $field mismatch"
    Require ($primary.$field -eq $replay.$field) "primary/replay $field mismatch"
}
Require ($failure.terminal -eq 'Unknown' -and $failure.stop -eq 'BudgetExceeded') 'failure control was not retained as BudgetExceeded'
Require ($failure.terminal -notin @('Death', 'RunVictory')) 'failure control emitted a false terminal'

foreach ($event in @('new-run', 'kaguya-encounter-entered', 'kaguya-enemy-intent', 'kaguya-enemy-behavior', 'death', 'kaguya-death-boundary')) {
    Require (@($primaryProbe.events) -contains $event) "primary probe missing $event"
}
foreach ($event in @('new-run', 'kaguya-event-entered', 'kaguya-event-option', 'kaguya-event-result', 'kaguya-resource-ledger', 'kaguya-cleanup', 'kaguya-teardown')) {
    Require (@($eventProbe.events) -contains $event) "event probe missing $event"
}
Require ($primaryProbe.nativeRuntimeAvailable -and $primaryProbe.terminal -eq 'run-death') 'custom encounter did not prove native run death'
Require ($primaryProbe.encounterId -eq 'ENCOUNTER.MIXED_SOLDIERS_ELITE_ENCOUNTER') 'custom encounter identity drifted'
Require ($primaryProbe.enemyId -eq 'MONSTER.BATTLE_SOLDIER_A') 'custom enemy identity drifted'
Require ($eventProbe.nativeRuntimeAvailable -and $eventProbe.eventId -eq 'EVENT.TAKETORI_MONOGATARI_EVENT') 'event identity was not witnessed'
Require ($index.runs.Count -eq 6) 'evidence index run count drifted'

$failureLog = Get-Content -Raw '.build/runs/m2-5-kaguya-20261112-r3-event-primary/result.json'
Require ($failureLog -match 'DuplicateModelException') 'registration failure evidence missing DuplicateModelException'
Require ((Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object Extension -in @('.dll', '.pck')).Count -eq 0) 'aggregate evidence copied Mod binaries'

$fixedManifest = 'local-mods/workshop-snapshot-20261106-163352.manifest.json'
Require ((Get-FileHash -LiteralPath $fixedManifest -Algorithm SHA256).Hash -eq '09A553D42F860A7AD22FA7D62BD66D4614CC618D06756218E6BE068181E8E5D3') 'fixed manifest changed'
Require ((Get-Content -Raw $fixedManifest | ConvertFrom-Json).SnapshotContentSha256 -eq '7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73') 'fixed snapshot content digest drifted'

Write-Output "PASS Kaguya M2-5 content=$($content.denominator.total) covered=$($content.denominator.covered) partial=$($content.denominator.partial) unsupported=$($content.denominator.unsupported) unrun=$($content.denominator.unrun) primary=$($primary.terminal)/$($primary.stop) replayExact=true failure=$($failure.terminal)/$($failure.stop)"
