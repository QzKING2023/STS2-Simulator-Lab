param(
    [string]$ReportPath = '.build/runs/minion-stage9-evaluation-20261108c/stage9-report.json',
    [string]$PrimaryProbePath = '.build/runs/minion-stage9-native-primary-20261108d/minion-stage9-probe.json',
    [string]$ReplayProbePath = '.build/runs/minion-stage9-native-replay-20261108d/minion-stage9-probe-replay.json',
    [string]$SnapshotDirectory = 'local-mods/workshop-snapshot-20261106-163352',
    [string]$SnapshotManifestPath = 'local-mods/workshop-snapshot-20261106-163352.manifest.json'
)
$ErrorActionPreference = 'Stop'
function Require([bool]$condition, [string]$message) { if (!$condition) { throw $message } }
Require (Test-Path -LiteralPath $ReportPath) 'stage 9 report is missing'
Require (Test-Path -LiteralPath $PrimaryProbePath) 'primary Minion probe is missing'
Require (Test-Path -LiteralPath $ReplayProbePath) 'replay Minion probe is missing'
Require (Test-Path -LiteralPath $SnapshotDirectory -PathType Container) 'fixed snapshot directory is missing'
Require (Test-Path -LiteralPath $SnapshotManifestPath -PathType Leaf) 'fixed snapshot manifest is missing'

$report = Get-Content -Raw $ReportPath | ConvertFrom-Json
$primary = Get-Content -Raw $PrimaryProbePath | ConvertFrom-Json
$replay = Get-Content -Raw $ReplayProbePath | ConvertFrom-Json
$fixture = $report.fixture
$resolution = $report.resolution

Require ($report.report.status -eq 'Pass') "stage 9 report is not Pass: $($report.report.status)"
Require ($report.report.targetGameVersion -eq '0.111.0') 'stage 9 target game version drifted'
Require ($report.report.snapshotId -eq 'workshop-snapshot-20261106-163352') 'stage 9 snapshot drifted'
Require ($report.report.snapshotContentSha256 -eq '7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73') 'stage 9 snapshot hash drifted'
Require ($report.report.minionLibVersion -eq '0.6.3' -and $report.report.ritsuLibVersion -eq '0.6.10') 'stage 9 dependency versions drifted'
Require (@($resolution.dependencyClosure) -contains 'MinionLib' -and @($resolution.dependencyClosure) -contains 'STS2-RitsuLib' -and @($resolution.dependencyClosure) -contains 'shadowversebydmod') 'dependency closure is incomplete'
Require ((@($resolution.selectedVariants | Where-Object { $_.modId -eq 'STS2-RitsuLib' -and $_.relativeDirectory -eq 'compat/0.111.0' })).Count -eq 1) 'RitsuLib exact 0.111.0 variant was not selected'
Require ($primary.nativeRuntimeAvailable -and $replay.nativeRuntimeAvailable) 'native Minion probes did not complete'
Require ($primary.minionLibInitialized -and $replay.minionLibInitialized) 'MinionLib initialization was not observed'
Require ($primary.shadowverseAssemblyLoaded -and $primary.shadowverseContentRegistered) 'Shadowverse assembly/content registration was not observed'
Require ($primary.terminal -eq 'lifecycle-complete' -and $replay.terminal -eq 'lifecycle-complete') 'native lifecycle boundary is missing'
Require ($primary.primaryDigest -eq $replay.primaryDigest) 'primary/replay digest mismatch'
Require ($report.report.deterministic -eq $true) 'stage 9 determinism was not closed'
Require ($report.report.snapshotWriteBlocked -and $report.report.formalDataWriteBlocked) 'write isolation was not asserted'
foreach ($event in @('native-loader','minion-register','minion-summon','component-result','combat-end','cleanup')) {
    Require (@($primary.events) -contains $event) "primary probe is missing event $event"
    Require (@($replay.events) -contains $event) "replay probe is missing event $event"
}
Require ($fixture.minion.registered -and $fixture.minion.summoned -and $fixture.minion.componentApplied -and $fixture.minion.combatEnded -and $fixture.minion.cleanedUp) 'fixture lifecycle is incomplete'
Require ($fixture.minion.uniqueIdentity -eq 'MINION.SHADOWVERSE_FIXTURE_SCOUT#1') 'fixture minion identity is not unique/stable'
Require ($fixture.component.cardId -eq 'CARD.SHADOWVERSE_FIXTURE_COMPONENT' -and $fixture.component.amount -eq 4) 'fixture component result drifted'
Require (@($report.report.errors).Count -eq 0) 'stage 9 report contains errors'
Require (@($report.report.capabilities | Where-Object { $_.kind -eq 'Gameplay' -and $_.status -eq 'Unsupported' }).Count -eq 1) 'unsupported gameplay boundary is not explicit'
Require ((Get-ChildItem -LiteralPath (Split-Path -Parent $ReportPath) -Recurse -File | Where-Object Extension -in @('.dll','.pck')).Count -eq 0) 'evaluation output copied Mod binaries'

$before = @{}
foreach ($path in @($SnapshotManifestPath,
    (Join-Path $SnapshotDirectory '3747595039/MinionLib/MinionLib.json'),
    (Join-Path $SnapshotDirectory '3747595039/MinionLib/MinionLib.dll'),
    (Join-Path $SnapshotDirectory '3747595039/MinionLib/MinionLib.pck'),
    (Join-Path $SnapshotDirectory '3747644378/shadowversebydmod.json'),
    (Join-Path $SnapshotDirectory '3747644378/shadowversebydmod.dll'),
    (Join-Path $SnapshotDirectory '3747644378/shadowversebydmod.pck'))) {
    $before[$path] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}
foreach ($pair in $before.GetEnumerator()) {
    Require ((Get-FileHash -LiteralPath $pair.Key -Algorithm SHA256).Hash -eq $pair.Value) "fixed snapshot changed: $($pair.Key)"
}

Write-Output "PASS minion-stage9 deterministic=$($report.report.determinismDigest) fixture=$($report.report.fixtureDigest) minion=$($fixture.minion.uniqueIdentity) component=$($fixture.component.cardId)"
