param([Parameter(Mandatory)][string]$RunDirectory)
$ErrorActionPreference='Stop';$root=[IO.Path]::GetFullPath($RunDirectory)
. (Join-Path $PSScriptRoot 'StageOneEvidenceRules.ps1')
. (Join-Path $PSScriptRoot 'ClassifyBatchResults.ps1')
function Read($path){Get-Content -LiteralPath $path -Raw|ConvertFrom-Json}
function Same($a,$b){($a|ConvertTo-Json -Depth 100 -Compress)-ceq($b|ConvertTo-Json -Depth 100 -Compress)}
$checks=[Collections.Generic.List[object]]::new()
function Check($name,$pass){$checks.Add([ordered]@{name=$name;passed=[bool]$pass})}
$summary=Read (Join-Path $root 'summary.json');$manifest=Read (Join-Path $root 'build-manifest.json');$cases=Read (Join-Path $root 'case-manifest.json')
$required=@('vanilla','retained-victory','two-battle','shield','batch','online','slimed-replay','core','owned-worker-watchdog','failure-propagation-controls')
$expectedJobs=@($required)+@(foreach($c in $cases.cases){foreach($repeat in 1..$cases.repeats){$c.id+'-'+$repeat}})
Check 'all declared jobs returned their expected exits' ($summary.completed-and@($summary.results|Where-Object{-not$_.passed-or$_.exit-ne$_.expectedExit}).Count-eq0-and$summary.results.Count-eq$expectedJobs.Count-and-not$summary.targetedOnly)
foreach($id in $expectedJobs){Check ("required job present exactly once: $id") (@($summary.results|Where-Object{$_.id-eq$id}).Count-eq1)}
Check 'summary references frozen build' ($summary.buildId-ceq$manifest.buildId)
foreach($kind in @('identity','artifact','input','source')){
 $copy=Read (Join-Path $root 'build-manifest.json')
 if($kind-eq'identity'){$copy.buildId='0'*64}else{
  switch($kind){'artifact'{$copy.manifest.artifacts[0].sha256='0'*64};'input'{$copy.manifest.inputs[0].sha256='0'*64};'source'{$copy.manifest.source[0].sha256='0'*64}}
  $body=$copy.manifest|ConvertTo-Json -Depth 7 -Compress;$copy.buildId=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($body)))
 }
 $bad=Join-Path $root ("gate-control-$kind.json");$copy|ConvertTo-Json -Depth 9|Set-Content $bad -Encoding utf8
 & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'TestStageOneBuildManifest.ps1') -BuildManifest $bad *> (Join-Path $root "gate-control-$kind.log")
 Check ($kind+' gate rejects controlled wrong metadata without changing original files') ($LASTEXITCODE-eq1)
}
foreach($case in $cases.cases){
 $a=Read (Join-Path $root ($case.id+'-1/stage-one-case.json'));$b=Read (Join-Path $root ($case.id+'-2/stage-one-case.json'))
 Check ($case.id+' semantic action replay is deterministic') (Same $a.replay.steps $b.replay.steps)
 Check ($case.id+' native lifecycle is deterministic') (Same $a.lifecycle $b.lifecycle)
 Check ($case.id+' classified outcome/control is deterministic') ((Same $a.outcome $b.outcome)-and(Same $a.control $b.control))
 foreach($r in @($a,$b)){
  if($case.id-in'decision-budget','turn-budget'){
   Check ($case.id+' nonempty exact living boundary and one confirmed action') ((Test-ConfirmedBoundary $r.replay)-and$r.outcome.ConfirmedActions-eq1-and$r.outcome.StopReason-eq1-and$r.lifecycle.inProgress-and$r.lifecycle.hp-gt0)
  }
  if($case.id-in'enemy-death','upkeep-death'){
   Check ($case.id+' actual matched pending loss, no victory') ($r.replay.nativeDeath-and$r.replay.simulatorDeath-and$r.lifecycle.ended-eq1-and$r.lifecycle.won-eq0-and-not$r.lifecycle.inProgress)
   Check ($case.id+' observed residue, not invented cleanup') ($r.lifecycle.mana.echo-gt0-and$r.lifecycle.listening)
  }
  foreach($step in $r.replay.steps){
   Check ($case.id+' actual full terminal/action differential') ($step.exactStateMatch-and$step.nativeAccepted-and$step.simulatedAccepted-and$step.parentBranchUnchanged-and(Same $step.nativeRoot $step.simulatorRoot)-and(Same $step.parentRoot $r.replay.simulatorOpeningRoot)-and$step.nativeContinuation-ceq$step.simulatorContinuation)
  }
 }
}
foreach($repeat in 1..2){
 $q=Read (Join-Path $root "budget-query-$repeat/query/decision-query.json")
 Check 'actual solver NodeLimit with legal candidates and unchanged source' ($q.predictedBoundary-eq'NodeLimit'-and$q.expandedNodes-eq100-and$q.actualPolicy.profile.maxExpandedNodes-eq100-and$q.candidateActionCount-gt0-and$q.nativeSourceUnchangedByQuery)
}
foreach($case in (Read (Join-Path $root 'shield/summary.json')).cases){
 $r=Read $case.report;$paid=if($r.requestedMana-ge1){1}else{0};$extra=if($r.upgraded){8}else{5};$block=5+$paid*$extra
 Check ($case.id+' independent literal payment/block contract') ($r.energyBefore-$r.energyAfter-eq1-and$r.blockAfter-$r.blockBefore-eq$block-and$r.manaAfter.mana-eq$r.requestedMana-$paid)
}
$two=Read (Join-Path $root 'two-battle/repeat-summary.json')
foreach($case in $two.cases){
 $r=Read $case.report
 Check ($case.name+' ended and cross-combat ticket guards exercised') ($r.stageOneTicketChecks.Count-eq2-and@($r.stageOneTicketChecks|Where-Object{-not$_.nativeContinuationUnchanged-or$_.code-ne'ticket-combat'}).Count-eq0)
}
foreach($dir in @('batch','slimed-replay')){
 $s=Read (Join-Path $root "$dir/summary.json")
 Check ($dir+' keeps twelve paths and four unsupported') ($s.cases.Count-eq12-and@($s.cases|Where-Object{$_.status-eq'unsupported'}).Count-eq4)
  $reports=@{};foreach($c in $s.cases){$reports[$c.id]=Read $c.report}
  $spec=Read (Join-Path $root "$dir/declaration.json")
  $ids=@(foreach($i in 1..3){foreach($p in $spec.policies){foreach($rep in 'a','b'){"seed$i-$p-$rep"}}})
  $classification=Get-BatchClassification $s.cases $reports $ids (Read (Join-Path $PSScriptRoot '../../tests/fixtures/stage-one-known-gaps.json')) $s.checks
  Check ($dir+' independently recomputed typed aggregation and raw exits') ($classification.exitCode-eq2-and$classification.correctnessChecksPassed-and$classification.countedPaths-eq12-and$classification.counts.passed-eq8)
}
$online=Read (Join-Path $root 'online/summary.json')
foreach($case in $online.cases){
 $r=Read $case.report;$steps=@($r.secondBattle.chunks|ForEach-Object {$_.steps})
 Check ($case.name+' confirmed cumulative counts') ($r.secondBattle.outcome.ConfirmedActions-eq$steps.Count-and$r.outcome.ConfirmedActions-eq$r.battle.outcome.ConfirmedActions+$steps.Count)
 if($case.name-like'fault-*'){
  Check ($case.name+' actual prefix then injected query error retains uncertainty') ($case.exit-eq1-and$r.secondBattle.failure.error-like'*stage-one-injected-query-fault-after-confirmed-prefix*'-and$steps.Count-eq2-and$r.secondBattle.outcome.NativeOutcome-eq0-and$r.secondBattle.outcome.SimulatorOutcome-eq0-and$r.secondBattle.outcome.StopReason-eq4-and-not$r.secondBattle.outcome.VerificationPassed-and$r.secondBattle.lastConfirmedBoundary.nativeContinuation-ceq$steps[-1].nativeContinuation)
 }
}
$watch=Read (Join-Path $root 'watchdog.json')
 $controls=Read (Join-Path $root 'failure-propagation-controls.json')
 Check 'failure propagation negative controls pass' ($controls.checkCount-ge25-and$controls.failureCount-eq0)
Check 'watchdog stopped only owned Lab control after barrier' ($watch.barrierReached-and$watch.timedOut-and$watch.exitConfirmed-and$watch.nativeOutcome-eq'Unknown')
$review=[ordered]@{schema='stage-one-evidence-review/v1';buildId=$manifest.buildId;verifierSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;checkCount=$checks.Count;failureCount=@($checks|Where-Object{-not$_.passed}).Count;checks=$checks;semantics='Read-only evidence review. Verifier identity is separate from frozen execution artifacts.'}
$review|ConvertTo-Json -Depth 8|Set-Content (Join-Path $root 'evidence-review.json') -Encoding utf8
Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $root 'evidence-verifier.ps1') -Force
$review|Select-Object buildId,checkCount,failureCount|Format-List
if($review.failureCount){$checks|Where-Object{-not$_.passed}|Format-Table;exit 1}
