param([Parameter(Mandatory)][string]$BuildManifest,[Parameter(Mandatory)][string]$OutputDirectory,[switch]$TargetedOnly,[string]$RetainedVictoryPlan='')
$ErrorActionPreference='Stop'
if(-not$TargetedOnly-and[string]::IsNullOrWhiteSpace($RetainedVictoryPlan)){throw 'Full stage requires the retained sixteen-action Victory plan.'}
$lab=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'));$manifest=[IO.Path]::GetFullPath($BuildManifest)
$frozen=Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json;$h=$frozen.manifest.harness
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(-not$out.StartsWith((Join-Path $lab '.build/runs')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Use Lab runs output.'}
if(Test-Path $out){throw 'Use a fresh run directory.'}
New-Item -ItemType Directory -Path $out|Out-Null
Copy-Item -LiteralPath $manifest -Destination (Join-Path $out 'build-manifest.json')
$caseFile=Join-Path $lab 'tests/fixtures/stage-one-cases.json';Copy-Item $caseFile (Join-Path $out 'case-manifest.json')
$cases=Get-Content $caseFile -Raw|ConvertFrom-Json;$results=[Collections.Generic.List[object]]::new();$complete=$false
function CheckFrozen { & (Join-Path $PSScriptRoot 'TestStageOneBuildManifest.ps1') -BuildManifest $manifest }
function Record($id,$code,$expected,$path){$results.Add([ordered]@{id=$id;exit=$code;expectedExit=$expected;passed=($code-eq$expected);path=$path});SaveSummary}
function SaveSummary { [ordered]@{schema='stage-one-summary/v1';buildId=$frozen.buildId;completed=$complete;updatedUtc=[DateTime]::UtcNow.ToString('o');status=if(@($results|Where-Object{-not$_.passed}).Count){'failed'}elseif(-not$complete){'partial'}else{'verified-with-known-gaps'};targetedOnly=[bool]$TargetedOnly;results=$results;knownUnsupported=$cases.knownUnsupported;semantics='Verification outcomes; expected controls are not actual native faults. Known coverage gaps retain suite exit 2.'}|ConvertTo-Json -Depth 12|Set-Content (Join-Path $out 'summary.json') -Encoding utf8 }
CheckFrozen
if(-not$TargetedOnly){
 foreach($mode in @('Vanilla','EleanorVictory')){
  $id=if($mode-eq'Vanilla'){'vanilla'}else{'retained-victory'}
  & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'run-stage-one-compat.ps1') -HarnessRoot $h -OutputDirectory (Join-Path $out $id) -Mode $mode -PinnedPlan $RetainedVictoryPlan *> (Join-Path $out ($id+'.log'))
  $code=$LASTEXITCODE;Record $id $code 0 (Join-Path $out "$id/compat-summary.json")
  if($code-ne0){throw "Required compatibility case failed: $id"}
  CheckFrozen
 }
}
foreach($case in $cases.cases){foreach($repeat in 1..$cases.repeats){
 $dir=Join-Path $out ($case.id+'-'+$repeat)
 & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'run-stage-one-case.ps1') -HarnessRoot $h -Case $case.id -Nodes $case.nodes -OutputDirectory $dir
 $code=$LASTEXITCODE
 $r=if(Test-Path (Join-Path $dir 'stage-one-case.json')){Get-Content (Join-Path $dir 'stage-one-case.json') -Raw|ConvertFrom-Json}else{$null}
 if($case.expectedExit-eq1 -and ($null-eq$r -or $r.error-notlike'*stage-one-injected-before-native-dispatch*')){$code=-99}
 if($case.expectedExit-eq0 -and $r.status-ne'passed'){$code=-98}
 Record ($case.id+'-'+$repeat) $code $case.expectedExit $dir
 CheckFrozen
}}
# This worker intentionally waits at a Lab-only barrier; no native game action is interrupted.
$barrier=Join-Path $out 'watchdog-barrier.txt';$stdout=Join-Path $out 'watchdog.stdout';$stderr=Join-Path $out 'watchdog.stderr'
$worker=Start-Process -FilePath (Get-Command pwsh).Source -WindowStyle Hidden -PassThru -ArgumentList @('-NoProfile','-File',('"'+(Join-Path $PSScriptRoot 'StageOneWatchdogWorker.ps1')+'"'),'-Barrier',('"'+$barrier+'"')) -RedirectStandardOutput $stdout -RedirectStandardError $stderr
$exited=$worker.WaitForExit(2000);$barrierReached=Test-Path $barrier
if(-not$exited){Stop-Process -Id $worker.Id;$worker.WaitForExit(5000)|Out-Null}
$watchdog=[ordered]@{scenarioKind='contract-control';workerId=$worker.Id;barrierReached=$barrierReached;timedOut=(-not$exited);exitConfirmed=$worker.HasExited;nativeOutcome='Unknown';stopReason='Interrupted'}
$watchdog|ConvertTo-Json|Set-Content (Join-Path $out 'watchdog.json') -Encoding utf8
Record 'owned-worker-watchdog' $(if($barrierReached-and-not$exited-and$worker.HasExited){0}else{1}) 0 (Join-Path $out 'watchdog.json')
if(-not$TargetedOnly){
 & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'TestStageOneFailurePropagation.ps1') -OutputPath (Join-Path $out 'failure-propagation-controls.json') *> (Join-Path $out 'failure-propagation-controls.log')
 Record 'failure-propagation-controls' $LASTEXITCODE 0 (Join-Path $out 'failure-propagation-controls.json')
 & dotnet (Join-Path $lab 'tests/SimulatorLab.Core.Checks/bin/Release/net9.0/SimulatorLab.Core.Checks.dll') *> (Join-Path $out 'core.log')
 Record 'core' $LASTEXITCODE 0 (Join-Path $out 'core.log')
 $game=Split-Path (Get-Content (Join-Path $h 'bin/Release/net9.0/OfflineSearchHarness.runtimeconfig.json') -Raw|ConvertFrom-Json).runtimeOptions.configProperties.Sts2DataDir
 $env:SIMULATORLAB_STAGE_ONE_TICKETS='1'
 & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'run-v041-native-loop-cases.ps1') -HarnessRoot $h -GamePath $game -SecondBattle -OutputDirectory (Join-Path $out 'two-battle') *> (Join-Path $out 'two-battle.log')
 Record 'two-battle' $LASTEXITCODE 0 (Join-Path $out 'two-battle/repeat-summary.json')
 Remove-Item Env:SIMULATORLAB_STAGE_ONE_TICKETS
 foreach($entry in @(@{id='shield';script='run-extended-validation.ps1';expected=0},@{id='batch';script='run-fixed-strategy-batch.ps1';expected=2},@{id='online';script='run-online-decision-cases.ps1';expected=0})){
  CheckFrozen
  & pwsh -NoProfile -File (Join-Path $PSScriptRoot $entry.script) -HarnessRoot $h -GamePath $game -OutputDirectory (Join-Path $out $entry.id) *> (Join-Path $out ($entry.id+'.log'))
  Record $entry.id $LASTEXITCODE $entry.expected (Join-Path $out $entry.id)
 }
 & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'run-fixed-strategy-batch.ps1') -HarnessRoot $h -GamePath $game -OutputDirectory (Join-Path $out 'slimed-replay') -ReplayPlansFrom (Join-Path $lab '.build/runs/fixed-strategy-20261102-020903-886') *> (Join-Path $out 'slimed-replay.log')
 Record 'slimed-replay' $LASTEXITCODE 2 (Join-Path $out 'slimed-replay/summary.json')
}
CheckFrozen; $complete=$true; SaveSummary
Write-Output "Stage one: $out; build=$($frozen.buildId)"
if(@($results|Where-Object{-not$_.passed}).Count){exit 1}
