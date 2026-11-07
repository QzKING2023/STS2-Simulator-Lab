param(
    [string]$ReportPath = '.build/runs/ritsu-stage7-evaluation-native-20261107-final2/stage7-report.json',
    [string]$ProbePath = '.build/runs/ritsu-stage7-native-primary-20261107/game-output/gameprobe.json'
)
$ErrorActionPreference = 'Stop'

function Require([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }

$reportEnvelope = Get-Content -Raw $ReportPath | ConvertFrom-Json
$report = $reportEnvelope.Report
$probe = Get-Content -Raw $ProbePath | ConvertFrom-Json

Require ($report.Status -eq 'Pass') "stage 7 report is not Pass: $($report.Status)"
Require ($report.TargetGameVersion -eq '0.111.0') 'stage 7 target game version drifted'
Require ($report.SnapshotId -eq 'workshop-snapshot-20261106-163352') 'stage 7 snapshot drifted'
Require ($report.SnapshotWriteBlocked -and $report.FormalDataWriteBlocked) 'write boundary was not asserted'
Require ($report.Deterministic -and $report.PrimaryDigest -eq $report.ReplayDigest) 'primary/replay digest mismatch'
Require (@($report.Errors).Count -eq 0) 'stage 7 report contains errors'

$selected = @($reportEnvelope.resolution.SelectedVariants)
Require (@($selected | Where-Object { $_.ModId -eq 'STS2-RitsuLib' -and $_.RelativeDirectory -eq 'compat/0.111.0' }).Count -eq 1) 'RitsuLib exact 0.111.0 variant was not selected'
Require (@($report.CandidateScopes | Where-Object { $_.RuntimeStatus -eq 'Passed' }).Count -eq 3) 'candidate runtime scope confirmation is incomplete'

$counts = @($report.Capabilities | Group-Object kind,status | ForEach-Object { @{ key = $_.Name; count = $_.Count } })
Require (@($report.Capabilities | Where-Object { $_.Kind -eq 'Card' -and $_.Status -eq 'Mapped' -and $_.Id -in @('CARD.LEX_NINJA2_CARD_STRIKE_NINJA','CARD.LEX_NINJA2_CARD_YI_CUT','CARD.LEX_NINJA2_CARD_DEFEND_NINJA','CARD.LEX_NINJA2_CARD_SHAKE_SHAKE_HANDS') }).Count -eq 4) 'observed card effect identities are not all mapped'
Require (@($report.Capabilities | Where-Object Status -eq 'Partial').Count -gt 0) 'partial support boundary table is empty'
Require (@($report.Capabilities | Where-Object Status -eq 'Unsupported').Count -gt 0) 'unsupported content surface is empty'
foreach ($row in @($report.Capabilities | Where-Object Status -in @('Partial','Unsupported'))) {
    Require (-not [string]::IsNullOrWhiteSpace($row.Reason)) "missing stop reason for $($row.Kind)/$($row.Id)"
    Require (-not [string]::IsNullOrWhiteSpace($row.EvidenceRef)) "missing evidence reference for $($row.Kind)/$($row.Id)"
}

$combat = @($probe.evidence.combat)
$plays = @($combat | Where-Object { $_.kind -eq 'play' })
$results = @($combat | Where-Object { $_.kind -eq 'play-result' })
Require ($plays.Count -gt 0 -and $results.Count -gt 0) 'combat effect boundary did not run'
Require (@($plays | Where-Object { -not [string]::IsNullOrWhiteSpace($_.target) }).Count -gt 0) 'targeted card boundary did not run'
Require (@($plays | Where-Object { [string]::IsNullOrWhiteSpace($_.target) }).Count -gt 0) 'untargeted card boundary did not run'
$damage = @($results | Where-Object { $_.after.enemy -and $_.after.enemy.Count -gt 0 -and $_.after.enemy[0].hp -lt 46 })
Require ($damage.Count -gt 0) 'damage effect boundary did not run'
$powerDelta = @($results | ForEach-Object { @($_.powerDelta) } | Where-Object { $_.id -eq 'POWER.WEAK_POWER' })
Require ($powerDelta.Count -gt 0) 'Power acquisition boundary did not run'

$relicTimings = @($probe.evidence.relicTriggers | ForEach-Object timing)
Require ($relicTimings -contains 'combat-start' -and $relicTimings -contains 'combat-end' -and $relicTimings -contains 'reward-boundary') 'relic trigger timing boundaries are incomplete'
$powerTimings = @($probe.evidence.powerLifecycle | ForEach-Object timing)
Require ($powerTimings -contains 'combat-start' -and $powerTimings -contains 'turn-end' -and $powerTimings -contains 'cleanup') 'Power lifecycle boundaries are incomplete'

Write-Output "PASS ritsu-stage7 deterministic=$($report.DeterminismDigest) mapped=$(@($report.Capabilities | Where-Object Status -eq 'Mapped').Count) partial=$(@($report.Capabilities | Where-Object Status -eq 'Partial').Count) unsupported=$(@($report.Capabilities | Where-Object Status -eq 'Unsupported').Count)"
