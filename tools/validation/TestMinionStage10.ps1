param(
    [string]$ReportPath = '.build/runs/minion-stage10-evaluation-20261108/stage10-report.json',
    [string]$PrimaryProbePath = '.build/runs/minion-stage10-native-primary-20261108e/minion-stage10-probe.json',
    [string]$ReplayProbePath = '.build/runs/minion-stage10-native-replay-20261108e/minion-stage10-probe-replay.json',
    [string]$FailureProbePath = '.build/runs/minion-stage10-native-failure-20261108e/minion-stage10-probe.json',
    [string]$SnapshotDirectory = 'local-mods/workshop-snapshot-20261106-163352',
    [string]$SnapshotManifestPath = 'local-mods/workshop-snapshot-20261106-163352.manifest.json'
)
$ErrorActionPreference = 'Stop'
function Require([bool]$condition, [string]$message) { if (!$condition) { throw $message } }
Require (Test-Path -LiteralPath $ReportPath) 'stage 10 report is missing'
Require (Test-Path -LiteralPath $PrimaryProbePath) 'primary real-combat probe is missing'
Require (Test-Path -LiteralPath $ReplayProbePath) 'replay real-combat probe is missing'
Require (Test-Path -LiteralPath $FailureProbePath) 'failure probe is missing'
Require (Test-Path -LiteralPath $SnapshotDirectory -PathType Container) 'fixed snapshot directory is missing'
Require (Test-Path -LiteralPath $SnapshotManifestPath -PathType Leaf) 'fixed snapshot manifest is missing'

$document = Get-Content -Raw $ReportPath | ConvertFrom-Json
$report = $document.report
$primary = Get-Content -Raw $PrimaryProbePath | ConvertFrom-Json
$replay = Get-Content -Raw $ReplayProbePath | ConvertFrom-Json
$failure = Get-Content -Raw $FailureProbePath | ConvertFrom-Json

Require ($report.status -eq 'Pass') "stage 10 report is not Pass: $($report.status)"
Require ($report.schema -eq 'minion-stage10/v1') 'stage 10 report schema drifted'
Require ($report.targetGameVersion -eq '0.111.0') 'target game version drifted'
Require ($report.snapshotId -eq 'workshop-snapshot-20261106-163352') 'snapshot identity drifted'
Require ($report.snapshotContentSha256 -eq '7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73') 'snapshot content hash drifted'
Require ($report.minionLibVersion -eq '0.6.3' -and $report.shadowverseVersion -eq '1.6.0') 'fixed mod versions drifted'
Require ($report.ritsuLibVariant -eq 'compat/0.111.0') 'RitsuLib exact compatibility variant was not selected'
Require ($report.deterministic -eq $true -and $report.failureIsolated -eq $true) 'determinism or failure isolation was not closed'
Require ($report.snapshotWriteBlocked -and $report.formalDataWriteBlocked) 'write isolation was not asserted'
Require (@($report.errors).Count -eq 0) 'stage 10 report contains errors'

foreach ($probe in @($primary, $replay)) {
    Require ($probe.schema -eq 'minion-stage10-runtime-probe/v1') 'real-combat probe schema drifted'
    Require ($probe.nativeRuntimeAvailable -and $probe.nativeGameplayObserved) 'real native gameplay was not observed'
    Require ($probe.minionLibInitialized -and $probe.shadowverseContentRegistered) 'MinionLib/Shadowverse registration was not observed'
    Require ($probe.snapshotWriteBlocked -and $probe.formalDataWriteBlocked) 'probe write isolation was not asserted'
    foreach ($event in @('minion-real-register','minion-real-summon','minion-component-real-result','minion-real-damaged','minion-real-death','minion-real-removal','minion-combat-end-cleanup')) {
        Require (@($probe.events) -contains $event) "probe is missing event $event"
    }
    $combat = $probe.realCombat
    Require ($combat.minionType -eq 'shadowversebydmod.Scripts.Monsters.KnightSoldierMinion') 'real Minion type drifted'
    Require ($combat.summonedHp -eq 3 -and $combat.hpAfterDamage -lt $combat.summonedHp) 'real Minion damage result is missing'
    Require (!$combat.aliveAfterDeath -and $combat.hpAfterDeath -eq 0) 'real Minion death result is missing'
    Require ($combat.removedFromCombat -and $combat.removedFromPets) 'real Minion removal result is missing'
    Require ($combat.componentCardType -eq 'shadowversebydmod.Scripts.Cards.CardCraft.Portalcraft.AncientArtifact') 'real component card type drifted'
    Require ($combat.componentCardId -eq 'CARD.ANCIENT_ARTIFACT' -and $combat.componentId -eq 'shadowversebydmod.StrikeComponent') 'real component identity drifted'
    Require ($combat.componentAmount -eq 11 -and $combat.componentApplied -and $combat.targetHpAfterComponent -lt $combat.targetHpBeforeComponent) 'real component result is missing'
    Require ($combat.combatEnded -and $combat.cleanupConfirmed) 'combat end cleanup was not confirmed'
}
Require ($primary.primaryDigest -eq $replay.primaryDigest) 'primary/replay normalized digest mismatch'
Require ($failure.schema -eq 'minion-stage10-runtime-probe/v1' -and $failure.terminal -eq 'runtime-failed') 'failure probe did not isolate a runtime failure'
Require (!$failure.nativeRuntimeAvailable -and !$failure.nativeGameplayObserved -and $null -eq $failure.realCombat) 'failure probe contaminated real gameplay'
Require ($failure.snapshotWriteBlocked -and $failure.formalDataWriteBlocked -and -not [string]::IsNullOrWhiteSpace($failure.failureMessage)) 'failure probe lacks isolation evidence'
foreach ($kind in @('UI','Map','Save','ComplexComponent','ReplayNetwork')) {
    Require ((@($report.capabilities | Where-Object { $_.kind -eq $kind -and $_.status -eq 'Unsupported' })).Count -eq 1) "unsupported capability evidence missing: $kind"
}
Require ((Get-ChildItem -LiteralPath (Split-Path -Parent $ReportPath) -Recurse -File | Where-Object Extension -in @('.dll','.pck')).Count -eq 0) 'evaluation output copied Mod binaries'

$coreText = (Get-ChildItem src/SimulatorLab.Core -Recurse -Filter '*.cs' | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
Require ($coreText -notmatch '(?m)^\s*using\s+(Godot|MinionLib|shadowversebydmod)') 'core has an external Mod/Godot using directive'
Require ((Get-Content src/SimulatorLab.Core/SimulatorLab.Core.csproj -Raw) -notmatch 'MinionLib|Shadowverse|Godot') 'core project references an external Mod/Godot assembly'

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
Write-Output "PASS minion-stage10 deterministic=$($report.determinismDigest) primary=$($primary.primaryDigest) component=$($primary.realCombat.componentId)"
