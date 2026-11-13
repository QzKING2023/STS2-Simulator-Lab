param(
    [string]$EvaluationReportPath = '.build/runs/m2-5-shadowverse-minion-20261113-evaluation/stage10-report.json',
    [string]$PrimaryDirectory = '.build/runs/m2-5-shadowverse-minion-20261113-primary',
    [string]$RepeatDirectory = '.build/runs/m2-5-shadowverse-minion-20261113-repeat',
    [string]$ReplayDirectory = '.build/runs/m2-5-shadowverse-minion-20261113-replay',
    [string]$FailureDirectory = '.build/runs/m2-5-shadowverse-minion-20261113-failure',
    [string]$SnapshotDirectory = 'local-mods/workshop-snapshot-20261106-163352',
    [string]$SnapshotManifestPath = 'local-mods/workshop-snapshot-20261106-163352.manifest.json'
)
$ErrorActionPreference = 'Stop'

function Require([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

Require (Test-Path -LiteralPath $EvaluationReportPath -PathType Leaf) 'M2-5 Minion evaluation report is missing'
Require (Test-Path -LiteralPath $SnapshotDirectory -PathType Container) 'fixed snapshot directory is missing'
Require (Test-Path -LiteralPath $SnapshotManifestPath -PathType Leaf) 'fixed snapshot manifest is missing'

$evaluation = Get-Content -Raw $EvaluationReportPath | ConvertFrom-Json
$report = $evaluation.Report
Require ($report.Status -eq 'Pass') "M2-5 Minion evaluation is not Pass: $($report.Status)"
Require ($report.TargetGameVersion -eq '0.111.0') 'target game version drifted'
Require ($report.SnapshotId -eq 'workshop-snapshot-20261106-163352') 'snapshot identity drifted'
Require ($report.SnapshotContentSha256 -eq '7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73') 'snapshot content hash drifted'
Require ($report.MinionLibVersion -eq '0.6.3' -and $report.ShadowverseVersion -eq '1.6.0') 'fixed Mod versions drifted'
Require ($report.RitsuLibVariant -eq 'compat/0.111.0') 'RitsuLib exact variant drifted'
Require ($report.Deterministic -eq $true -and $report.FailureIsolated -eq $true) 'stability or failure isolation was not closed'

$runDirectories = @{
    primary = $PrimaryDirectory
    repeat = $RepeatDirectory
    replay = $ReplayDirectory
}
$manifests = @{}
$probes = @{}
foreach ($name in $runDirectories.Keys) {
    $directory = $runDirectories[$name]
    $manifestPath = Join-Path $directory 'run-manifest.json'
    $resultPath = Join-Path $directory 'result.json'
    $probePath = if ($name -eq 'replay') {
        Join-Path $directory 'minion-stage10-probe-replay.json'
    } else {
        Join-Path $directory 'minion-stage10-probe.json'
    }
    Require (Test-Path -LiteralPath $manifestPath -PathType Leaf) "$name run manifest is missing"
    Require (Test-Path -LiteralPath $resultPath -PathType Leaf) "$name result is missing"
    Require (Test-Path -LiteralPath $probePath -PathType Leaf) "$name Minion probe is missing"
    $manifest = Get-Content -Raw $manifestPath | ConvertFrom-Json
    $result = Get-Content -Raw $resultPath | ConvertFrom-Json
    $probe = Get-Content -Raw $probePath | ConvertFrom-Json
    Require ($result.result.Terminal -eq 'Death' -and $result.result.Stop -eq 'Completed' -and $result.result.ExitCode -eq 0) "$name did not reach native Death"
    Require ($manifest.terminal -eq 'Death' -and $manifest.stop -eq 'Completed') "$name manifest terminal drifted"
    Require ($probe.NativeRuntimeAvailable -and $probe.NativeGameplayObserved) "$name did not observe native gameplay"
    Require ($probe.Terminal -eq 'Death') "$name probe did not retain native Death"
    Require ($probe.RealCombat -ne $null -and $probe.RealCombat.ComponentApplied -and $probe.RealCombat.CleanupConfirmed) "$name real Minion combat evidence is incomplete"
    foreach ($event in @('minion-real-register','minion-real-summon','minion-component-real-result','minion-real-damaged','minion-real-death','minion-real-removal','minion-combat-end-cleanup','death')) {
        Require (@($probe.Events) -contains $event) "$name probe is missing event $event"
    }
    $manifests[$name] = $manifest
    $probes[$name] = $probe
}

$referenceManifest = $manifests.primary
$referenceProbe = $probes.primary
foreach ($name in @('repeat','replay')) {
    $manifest = $manifests[$name]
    $probe = $probes[$name]
    Require ($probe.PrimaryDigest -eq $referenceProbe.PrimaryDigest) "$name Minion digest differs from primary"
    foreach ($field in @('stateDigest','rngDigest','traceDigest','routeDigest','choiceDigest')) {
        Require ($manifest.$field -eq $referenceManifest.$field) "$name $field differs from primary"
    }
}

$failureManifestPath = Join-Path $FailureDirectory 'run-manifest.json'
$failureResultPath = Join-Path $FailureDirectory 'result.json'
$failureProbePath = Join-Path $FailureDirectory 'minion-stage10-probe.json'
Require (Test-Path -LiteralPath $failureManifestPath -PathType Leaf) 'failure run manifest is missing'
Require (Test-Path -LiteralPath $failureResultPath -PathType Leaf) 'failure result is missing'
Require (Test-Path -LiteralPath $failureProbePath -PathType Leaf) 'failure Minion probe is missing'
$failureManifest = Get-Content -Raw $failureManifestPath | ConvertFrom-Json
$failureResult = Get-Content -Raw $failureResultPath | ConvertFrom-Json
$failureProbe = Get-Content -Raw $failureProbePath | ConvertFrom-Json
Require ($failureResult.result.Stop -eq 'BudgetExceeded' -and $failureResult.result.ExitCode -eq 3) 'failure control was not retained as BudgetExceeded'
Require ($failureProbe.Terminal -eq 'runtime-failed' -and !$failureProbe.NativeRuntimeAvailable -and !$failureProbe.NativeGameplayObserved) 'failure control was promoted to gameplay'
Require ($failureProbe.SnapshotWriteBlocked -and $failureProbe.FormalDataWriteBlocked) 'failure isolation is incomplete'

foreach ($kind in @('UI','Map','Save','ComplexComponent','ReplayNetwork')) {
    Require ((@($report.Capabilities | Where-Object { $_.Kind -eq $kind -and $_.Status -eq 'Unsupported' })).Count -eq 1) "retained Unsupported status missing: $kind"
}
Require ((Get-ChildItem -LiteralPath (Split-Path -Parent $EvaluationReportPath) -Recurse -File | Where-Object Extension -in @('.dll','.pck')).Count -eq 0) 'evaluation output copied Mod binaries'

$expected = @{
    $SnapshotManifestPath = '09A553D42F860A7AD22FA7D62BD66D4614CC618D06756218E6BE068181E8E5D3'
    (Join-Path $SnapshotDirectory '3747595039/MinionLib/MinionLib.dll') = 'CB4CB7D57CD47D318CE485E67C19BCE3E06CF91CC77B76B6FD0E520D868A4113'
    (Join-Path $SnapshotDirectory '3747595039/MinionLib/MinionLib.pck') = 'E3D343C4A6556C6F906428509846F793B380B72098FB2A4BD88F0ABE32B7B540'
    (Join-Path $SnapshotDirectory '3747644378/shadowversebydmod.dll') = '638D89DCCE30139EE4E978A170FB4625AEDFAF2CF38E4F3BB5C17D0BD6338749'
    (Join-Path $SnapshotDirectory '3747644378/shadowversebydmod.pck') = '6B948B2961E896A2904A333A5710448BD60396BB72523493C37C73F319C9F2A9'
}
foreach ($pair in $expected.GetEnumerator()) {
    Require ((Get-FileHash -LiteralPath $pair.Key -Algorithm SHA256).Hash -eq $pair.Value) "fixed snapshot changed: $($pair.Key)"
}

Write-Output "PASS minion-m2-5 terminal=Death digest=$($referenceProbe.PrimaryDigest) state=$($referenceManifest.stateDigest)"
