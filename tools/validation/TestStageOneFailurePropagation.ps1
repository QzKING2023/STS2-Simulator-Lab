param([Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'ClassifyBatchResults.ps1')
. (Join-Path $PSScriptRoot 'StageOneEvidenceRules.ps1')
$checks=[Collections.Generic.List[object]]::new()
function Check($name,$ok){$checks.Add(@{name=$name;passed=[bool]$ok})}
function Classify($status,$exit=1,$id='a',$reason=''){
 $case=@{id=$id;status=$status;exit=$exit}
 $stopCodes=@{'passed'=0;'death'=0;'unsupported'=2;'budget-exceeded'=1;'no-candidate'=9;'invalid-action'=3;'engine-error'=4;'differential-mismatch'=5;'environment-blocked'=6;'cancelled'=7;'interrupted'=8}
 $report=@{status=$status;phase='second-battle';reason=$reason;outcome=@{VerificationPassed=$status-in'passed','death';StopReason=$stopCodes[$status];Coverage=if($status-eq'unsupported'){2}else{1};NativeOutcome=if($status-eq'death'){3}else{2};SimulatorOutcome=if($status-eq'death'){3}else{2};Comparison=1}}
 Get-BatchClassification @($case) @{$id=$report} @('a') @(@{id='a';phase='second-battle';reasonToken='EXPECTED_CARD'}) @(@{passed=$true})
}
foreach($status in 'engine-error','invalid-action','differential-mismatch','failed','not-run','unrecognized'){
 $r=Classify $status;Check "$status fails closed" ($r.exitCode-eq1-and-not$r.correctnessChecksPassed-and$r.countedPaths-eq1)
}
foreach($status in 'budget-exceeded','no-candidate','environment-blocked','cancelled','interrupted'){
 $r=Classify $status;Check "$status stays incomplete, not an execution fault or complete verification" ($r.exitCode-eq3-and$r.executionIntegrityPassed-and-not$r.correctnessChecksPassed-and$r.incompletePaths-eq1)
}
Check 'declared unsupported alone permits exit 2' ((Classify 'unsupported' 1 'a' 'EXPECTED_CARD').exitCode-eq2)
Check 'unexpected unsupported reason fails' ((Classify 'unsupported' 1 'a' 'OTHER_CARD').exitCode-eq1)
Check 'unexpected case identity fails' ((Classify 'unsupported' 1 'b' 'EXPECTED_CARD').exitCode-eq1)
Check 'successful report with nonzero child exit fails' ((Classify 'passed' 1).exitCode-eq1)
Check 'matched Death remains a verified strategy outcome' ((Classify 'death' 1).exitCode-eq0)
Check 'successful terminal and zero raw exit pass' ((Classify 'passed' 0).exitCode-eq0)
$duplicate=@(@{id='a';status='passed';exit=0},@{id='a';status='passed';exit=0})
Check 'duplicate/missing paths fail denominator gate' ((Get-BatchClassification $duplicate @{} @('a','b') @() @()).exitCode-eq1)
$boundary='{"status":"boundary","steps":[{"nativeAccepted":true,"simulatedAccepted":true,"exactStateMatch":true,"parentBranchUnchanged":true,"nativeContinuation":"same","simulatorContinuation":"same"}],"outcome":{"ConfirmedActions":1,"NativeOutcome":1,"SimulatorOutcome":1,"Comparison":1,"VerificationPassed":true}}'
Check 'one confirmed ongoing boundary accepted' (Test-ConfirmedBoundary ($boundary|ConvertFrom-Json))
foreach($status in 'engine-error','invalid-action','unsupported','differential-mismatch'){
 $r=$boundary|ConvertFrom-Json;$r.status=$status;Check "inner $status cannot pass budget evidence" (-not(Test-ConfirmedBoundary $r))
}
$r=$boundary|ConvertFrom-Json;$r.steps=@();Check 'empty steps cannot pass evidence review' (-not(Test-ConfirmedBoundary $r))
$r=$boundary|ConvertFrom-Json;$r.steps[0].simulatorContinuation='changed';Check 'mismatching boundary rejected' (-not(Test-ConfirmedBoundary $r))
$report=[ordered]@{schema='stage-one-failure-propagation-controls/v1';scenarioKind='contract-control';checks=$checks;checkCount=$checks.Count;failureCount=@($checks|Where-Object{-not$_.passed}).Count;semantics='In-memory synthetic reports exercise production aggregation and evidence gates. No native failure is implied.'}
$report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $OutputPath -Encoding utf8
if($report.failureCount){$checks|Where-Object{-not$_.passed}|ConvertTo-Json;exit 1}
Write-Output "$($checks.Count) failure propagation controls passed."
