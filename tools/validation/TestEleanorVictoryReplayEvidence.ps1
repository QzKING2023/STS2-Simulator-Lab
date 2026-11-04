param([Parameter(Mandatory)][string]$ReportPath)

$ErrorActionPreference = 'Stop'
$labRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$reportFullPath = [IO.Path]::GetFullPath($ReportPath)
if (-not $reportFullPath.StartsWith($labRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Evidence review is limited to reports inside the Lab.'
}
$json = [IO.File]::ReadAllText($reportFullPath)
$report = $json | ConvertFrom-Json
$nodes = [System.Text.Json.Nodes.JsonNode]::Parse($json)
$checks = [Collections.Generic.List[object]]::new()
function Check([string]$Name, [bool]$Passed) { $checks.Add([ordered]@{ name = $Name; passed = $Passed }) }
function EqualJson($Left, $Right) { [System.Text.Json.Nodes.JsonNode]::DeepEquals($Left, $Right) }
function HashText([string]$Text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Text))).ToLowerInvariant() }

Check 'v2 evidence schema' ($report.schema -eq 'eleanor-v041-fixed-route-victory-replay/v2')
if (-not $checks[0].passed) { throw 'This review requires v2 root snapshots and native terminal event evidence.' }
$pinRoot = Join-Path $labRoot '.build\pinned-inputs\eleanor-v0.4.1-forms-cardpool.fix'
$lock = Get-Content -Raw -LiteralPath (Join-Path $pinRoot 'input-lock.json') | ConvertFrom-Json
foreach ($file in $lock.files) {
    $path = Join-Path $pinRoot $file.relativePath
    Check "input hash/length: $($file.relativePath)" ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $file.sha256 -and (Get-Item -LiteralPath $path).Length -eq $file.length)
}
Check 'fixed game/Mod identity' ($report.gameVersion -eq 'v0.111.0' -and $report.gameCommit -eq '41cef1ea' -and $report.modVersion -eq 'v0.4.1-forms-cardpool.fix' -and $report.modAssemblySha256 -eq '949320f651298d34399335eb28176578d270139dff2cce60fb7fdf35e7dbad42')
Check 'fixed action plan hash' ((Get-FileHash -LiteralPath $report.planPath -Algorithm SHA256).Hash -eq $report.planSha256 -and $report.planSha256 -eq '975f7371f401ae9987ff1746e5a4d9f2a320d72be9a05e2784a26b589c034d2f')
$plan = @(Get-Content -Raw -LiteralPath $report.planPath | ConvertFrom-Json)
Check 'all planned actions executed' ($report.steps.Count -eq $plan.Count -and $report.steps.Count -eq 16 -and $report.planActionCount -eq 16)
Check 'native terminal completed' ($report.status -eq 'passed' -and $report.nativeVictory -and $report.simulatorVictory -and -not $report.nativeDeath -and -not $report.simulatorDeath -and $report.nativeCombatWonEvents -eq 1 -and $report.nativeCombatWonForExpectedRoom -and $report.nativeVictoryConditionsSatisfied -and -not $report.nativeCombatStillInProgress)
Check 'isolated host and write guards' ($report.testMode -and -not $report.steamInitialized -and $report.saveStore -eq 'MemorySaveStore' -and $report.replayAndSaveGuardsInstalled -and $report.interceptedReplayWrites -eq 1 -and $report.interceptedProgressSaves -eq 1 -and -not $report.persistentWritesAllowed)
Check 'presentation cleanup invoked once' ($report.headlessUiCleanup.target -eq 'NHoverTipSet.Clear()' -and $report.headlessUiCleanup.interceptedCalls -eq 1)
$negativeFlags = @($report.registrationEvidence | ForEach-Object { $_.PSObject.Properties | Where-Object { $_.Value -is [bool] } })
# Preserve the published nine-row baseline; the new exact ManaShield adapter adds one complete control row.
$shieldControls=@($report.registrationEvidence|Where-Object {$_.cardType-eq'MyMod.MyModCode.Cards.ManaShield'})
$shieldValid=$shieldControls.Count-eq0-or($shieldControls.Count-eq1-and$shieldControls[0].wrongTargetRejected-and$shieldControls[0].wrongAssemblyRejected-and$shieldControls[0].wrongManifestRejected-and$shieldControls[0].existingPatchRejected)
$rewardTypes=@('MyMod.MyModCode.Cards.ReturnFlow','MyMod.MyModCode.Cards.ManaConversionPrinciple','MyMod.MyModCode.Cards.ManaEssence')
$rewardControls=@($report.registrationEvidence|Where-Object{$_.cardType-in$rewardTypes})
$rewardValid=$rewardControls.Count-eq0
if($rewardControls.Count-eq6){
 $rewardValid=$true
 foreach($type in $rewardTypes){
  $source=@($rewardControls|Where-Object{$_.cardType-eq$type-and$_.PSObject.Properties.Name-contains'wrongTarget'})
  $late=@($rewardControls|Where-Object{$_.cardType-eq$type-and$_.PSObject.Properties.Name-contains'actualLatePatchChangesStampAndRejects'})
  $rewardValid=$rewardValid-and$source.Count-eq1-and$late.Count-eq1-and$source[0].wrongTarget-and$source[0].wrongAssembly-and$source[0].wrongManifest-and$source[0].existingPatchRejected-and$late[0].actualLatePatchChangesStampAndRejects-and$late[0].actualStateMachinePatchRejected
 }
}
Check 'recorded strict registration controls passed' ($shieldValid -and $rewardValid -and $report.registrationEvidence.Count -eq (9+$shieldControls.Count+$rewardControls.Count) -and $negativeFlags.Count -gt 0 -and @($negativeFlags | Where-Object { -not $_.Value }).Count -eq 0)
Check 'opening roots' (EqualJson $nodes['nativeOpeningRoot'] $nodes['simulatorOpeningRoot'])
Check 'opening continuations' ($report.nativeOpeningContinuation -ceq $report.simulatorOpeningContinuation)
$lastNative = $nodes['nativeOpeningRoot']
$lastSimulator = $nodes['simulatorOpeningRoot']
for ($i = 0; $i -lt $report.steps.Count; $i++) {
    $step = $report.steps[$i]
    $node = $nodes['steps'][$i]
    $action = $plan[$i]
    $prefix = "action $($i + 1)"
    $expectedAfterTurn = if ($step.kind -eq 'EndTurn') { $step.turn + 1 } else { $step.turn }
    Check "$prefix planned native/sim execution" ($step.index -eq $i + 1 -and $step.kind -ceq $action.kind -and $step.turn -eq $action.turn -and $step.afterTurn -eq $expectedAfterTurn -and $step.cardId -ceq $action.cardId -and $step.nativeAccepted -and $step.simulatedAccepted)
    Check "$prefix roots before/after" ((EqualJson $node['beforeNativeRoot'] $node['beforeSimulatorRoot']) -and (EqualJson $node['nativeRoot'] $node['simulatorRoot']) -and $step.nativeRootValidation.isExactReady -and $step.simulatorRootValidation.isExactReady)
    Check "$prefix continuity with preceding boundary" ((EqualJson $lastNative $node['beforeNativeRoot']) -and (EqualJson $lastSimulator $node['beforeSimulatorRoot']))
    Check "$prefix continuation text/hash" ($step.beforeNativeContinuation -ceq $step.beforeSimulatorContinuation -and $step.nativeContinuation -ceq $step.simulatorContinuation -and (HashText $step.nativeContinuation) -eq $step.nativeContinuationSha256 -and (HashText $step.simulatorContinuation) -eq $step.simulatorContinuationSha256)
    Check "$prefix complete parent root/continuation unchanged" ((EqualJson $nodes['simulatorOpeningRoot'] $node['parentRoot']) -and $step.parentContinuation -ceq $report.simulatorOpeningContinuation -and $step.parentBranchUnchanged)
    Check "$prefix actual tracked enemy/player HP" ($step.nativePlayerHp -eq $step.simulatorPlayerHp -and @($step.trackedEnemyHp | Where-Object { $_.native -ne $_.simulator }).Count -eq 0)
    $lastNative = $node['nativeRoot']
    $lastSimulator = $node['simulatorRoot']
}
Check 'last enemy actually dead' ($report.steps[-1].trackedEnemyHp.Count -gt 0 -and @($report.steps[-1].trackedEnemyHp | Where-Object { $_.native -gt 0 -or $_.simulator -gt 0 }).Count -eq 0)
$failures = @($checks | Where-Object { -not $_.passed })
$review = [ordered]@{ schema = 'eleanor-v041-victory-evidence-review/v1'; status = $(if ($failures.Count -eq 0) { 'passed' } else { 'failed' }); reportPath = $reportFullPath; reportSha256 = (Get-FileHash -LiteralPath $reportFullPath -Algorithm SHA256).Hash; inputCount = $lock.files.Count; checkCount = $checks.Count; failureCount = $failures.Count; checks = $checks }
$outputPath = Join-Path (Split-Path -Parent $reportFullPath) 'victory-evidence-review.json'
[IO.File]::WriteAllText($outputPath, ($review | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
[pscustomobject]$review | Select-Object status,inputCount,checkCount,failureCount,reportPath | Format-List
$failures | Format-Table
if ($failures.Count -gt 0) { exit 1 }
