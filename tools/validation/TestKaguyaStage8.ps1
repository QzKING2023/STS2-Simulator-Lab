#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$ReportPath = '.build/runs/kaguya-stage8-evaluation-v24-20261107/stage8-report.json',
    [string]$RunManifestPath = '.build/runs/kaguya-stage8-evaluation-v24-20261107/run-manifest.json',
    [string]$PrimaryProbePath = '.build/runs/kaguya-stage8-native-primary-event-v22-20261107/kaguya-stage8-probe.json',
    [string]$ReplayProbePath = '.build/runs/kaguya-stage8-native-replay-event-v22-20261107/kaguya-stage8-probe-replay.json',
    [string]$SnapshotDirectory = 'local-mods/workshop-snapshot-20261106-163352',
    [string]$SnapshotManifestPath = 'local-mods/workshop-snapshot-20261106-163352.manifest.json'
)
$ErrorActionPreference = 'Stop'

function Require([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$envelope = Get-Content -Raw $ReportPath | ConvertFrom-Json
$report = $envelope.report
$manifest = Get-Content -Raw $RunManifestPath | ConvertFrom-Json
$primary = Get-Content -Raw $PrimaryProbePath | ConvertFrom-Json
$replay = Get-Content -Raw $ReplayProbePath | ConvertFrom-Json

Require ($report.Status -eq 'Pass') "stage 8 report is not Pass: $($report.Status)"
Require ($report.TargetGameVersion -eq '0.111.0') 'stage 8 target game version drifted'
Require ($report.SnapshotId -eq 'workshop-snapshot-20261106-163352') 'stage 8 snapshot drifted'
Require ($report.SnapshotWriteBlocked -and $report.FormalDataWriteBlocked) 'write boundary was not asserted'
Require ($report.Deterministic -and $report.PrimaryDigest -eq $report.ReplayDigest) 'primary/replay digest mismatch'
Require (@($report.Errors).Count -eq 0) 'stage 8 report contains errors'
Require ($primary.nativeRuntimeAvailable -and $primary.terminal -eq 'battle-complete') 'primary native event did not complete'
Require ($replay.nativeRuntimeAvailable -and $replay.terminal -eq 'battle-complete') 'replay native event did not complete'
Require ($primary.primaryDigest -eq $replay.primaryDigest) 'native event probe digest mismatch'
Require ($primary.event.id -eq 'EVENT.TAKETORI_MONOGATARI_EVENT') 'unexpected Kaguya event identity'
Require (@($primary.event.optionIds).Count -eq 2) 'Kaguya event does not expose exactly two options'
Require (-not [string]::IsNullOrWhiteSpace([string]$primary.event.selectedOptionId)) 'event option selection is missing'
foreach ($event in @('new-run','kaguya-event-entered','kaguya-event-option','kaguya-event-result','kaguya-resource-ledger','kaguya-cleanup','kaguya-teardown')) {
    Require (@($primary.events) -contains $event) "primary probe is missing lifecycle event $event"
    Require (@($replay.events) -contains $event) "replay probe is missing lifecycle event $event"
}
Require ($manifest.modId -eq 'Kaguya' -and $manifest.workshopId -eq '3747548629') 'run manifest identity mismatch'
Require (@($manifest.dependencies) -contains 'BaseLib' -and @($manifest.dependencies) -contains 'Kaguya') 'dependency closure missing Kaguya/BaseLib'
Require ($manifest.deterministic -eq $true) 'run manifest did not retain determinism'
Require ($manifest.errorDenominator.total -eq 5 -and $manifest.errorDenominator.errorCount -eq 0) 'error denominator is not closed'
Require (@($manifest.identity | Where-Object { $_.kind -eq 'Event' -and $_.id -eq 'EVENT.TAKETORI_MONOGATARI_EVENT' }).Count -eq 1) 'event identity is missing from run manifest'
Require (@($manifest.identity | Where-Object { $_.kind -eq 'Reward' -and $_.id -eq 'REWARD.KAGUYA_TAKETORI_OPTION' }).Count -eq 1) 'reward identity is missing from run manifest'
Require (@($report.Capabilities | Where-Object { $_.kind -eq 'UI' -and $_.status -eq 'Unsupported' }).Count -eq 1) 'UI Unsupported boundary missing'
Require (@($report.Capabilities | Where-Object { $_.kind -eq 'Map' -and $_.status -eq 'Unsupported' }).Count -eq 1) 'map Unsupported boundary missing'
Require (@($report.Capabilities | Where-Object { $_.kind -eq 'Save' -and $_.status -eq 'Unsupported' }).Count -eq 1) 'save Unsupported boundary missing'
Require ((Get-ChildItem -LiteralPath (Split-Path -Parent $ReportPath) -Recurse -File | Where-Object Extension -in @('.dll','.pck')).Count -eq 0) 'evaluation output copied Mod binaries'

$before = @{}
foreach ($path in @($SnapshotManifestPath, (Join-Path $SnapshotDirectory '3747548629/Kaguya.json'), (Join-Path $SnapshotDirectory '3747548629/Kaguya.dll'), (Join-Path $SnapshotDirectory '3747548629/Kaguya.pck'))) {
    $before[$path] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}
foreach ($pair in $before.GetEnumerator()) {
    Require ((Get-FileHash -LiteralPath $pair.Key -Algorithm SHA256).Hash -eq $pair.Value) "fixed snapshot changed: $($pair.Key)"
}

Write-Output "PASS kaguya-stage8 deterministic=$($report.DeterminismDigest) event=$($primary.event.id) reward=$($primary.reward.rewardId) unsupported=$(@($report.Capabilities | Where-Object status -eq 'Unsupported').Count)"
