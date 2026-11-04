param([Parameter(Mandatory)][string]$BuildManifest,[Parameter(Mandatory)][string]$OutputDirectory,
 [string]$Seed='LAB_V0111_NATIVE_DEV_01',[string]$ReplayPath='',[ValidateSet('','async-fault','stale','budget','subchoice')][string]$Control='',
 [ValidateSet('','root-extra','unknown-choice-field','missing-choice-file','invalid-route')][string]$InputControl='',
 [string]$BranchPlan='', [string]$SolverPlan='',
 [ValidateRange(1,480)][int]$ProcessTimeoutSeconds=120,
 [string]$Scenario='slice-skip',[ValidateSet('off','minimal','detailed')][string]$Observation='detailed')
$ErrorActionPreference='Stop'
$lab=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(!$out.StartsWith((Join-Path $lab '.build/runs')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Output must be a fresh Lab runs directory'}
if(Test-Path $out){throw 'Refusing to overwrite previous evidence'}
$buildPath=[IO.Path]::GetFullPath($BuildManifest);$build=Split-Path $buildPath
$m=Get-Content $buildPath -Raw|ConvertFrom-Json
function CheckBinding {
 foreach($row in @($m.manifest.source)+@($m.manifest.inputs)) {if((Get-FileHash -LiteralPath $row.path).Hash -ne $row.sha256){throw "Changed source/input: $($row.path)"}}
 foreach($row in @($m.manifest.compiled)+@($m.manifest.artifacts)) {if((Get-FileHash -LiteralPath (Join-Path $build $row.path)).Hash -ne $row.sha256){throw "Changed compiled source/artifact: $($row.path)"}}
}
CheckBinding
if($ProcessTimeoutSeconds -gt 120){
 if(!$SolverPlan){throw 'Long process envelope requires an explicit full-run plan'}
 $fullPlan=Get-Content -LiteralPath $SolverPlan -Raw|ConvertFrom-Json
 if($fullPlan.Run.Schema -ne 'native-full-run/v2' -or $fullPlan.Run.WallSeconds -gt 450 -or $fullPlan.Run.WallSeconds+15 -gt $ProcessTimeoutSeconds -or $Scenario -notin @('solver-native-darv-act2','solver-native-darv-act2-replay')){throw 'Invalid long full-run/process budget binding'}
}
New-Item -ItemType Directory $out|Out-Null
Copy-Item $buildPath (Join-Path $out 'build-manifest.json')
$executable=if($m.manifest.executable){$m.manifest.executable}else{'SimulatorLab.NativeRun.dll'}
$exe=Join-Path $build ('bin/Release/net9.0/'+$executable)
$arguments=@($exe,$out,$Seed,$ReplayPath,$Control,$Scenario,$Observation,$InputControl,$(if($SolverPlan){$SolverPlan}else{$BranchPlan}))
$start=[DateTime]::UtcNow
. (Join-Path $PSScriptRoot 'NativeProcessBudget.ps1')
$psi=[Diagnostics.ProcessStartInfo]::new('dotnet');$psi.UseShellExecute=$false;$psi.CreateNoWindow=$true
$psi.RedirectStandardOutput=$true;$psi.RedirectStandardError=$true
foreach($argument in $arguments){$psi.ArgumentList.Add([string]$argument)}
$clock=[Diagnostics.Stopwatch]::StartNew()
$child=[Diagnostics.Process]::Start($psi)
$stdout=$child.StandardOutput.ReadToEndAsync();$stderr=$child.StandardError.ReadToEndAsync()
$gate=if($ProcessTimeoutSeconds -gt 120){Wait-NativeFullRunProcessBudget -Process $child -Clock $clock -Seconds $ProcessTimeoutSeconds}else{@{Expired=(Wait-NativeProcessBudget -Process $child -Clock $clock -Seconds $ProcessTimeoutSeconds);Reason='legacy-time'}}
$timedOut=$gate.Expired
$stdout.GetAwaiter().GetResult()|Set-Content (Join-Path $out 'console.log') -Encoding utf8
$stderr.GetAwaiter().GetResult()|Set-Content (Join-Path $out 'stderr.log') -Encoding utf8
$code=$child.ExitCode
$resultPath=Join-Path $out 'result.json'
$report=if(Test-Path $resultPath){Get-Content $resultPath -Raw|ConvertFrom-Json}else{$null}
$consistent=$null -ne $report -and $code -eq $report.rawExit
$timingPath=Join-Path $out 'timing.json'
$timing=if(Test-Path $timingPath){Get-Content $timingPath -Raw|ConvertFrom-Json}else{$null}
@{processTimeoutSeconds=$ProcessTimeoutSeconds;elapsedSeconds=$clock.Elapsed.TotalSeconds;schema='native-process/v1';buildId=$m.buildId;seed=$Seed;scenario=$Scenario;observation=$Observation;peakWorkingSet=if($timing){$timing.peakWorkingSetBeforeExit}else{$null};peakWorkingSetSource='runtime-before-exit; unavailable when timing report missing';provenance=$report.provenance;scenarioKind=$report.provenance.ScenarioKind;executionKind=$report.provenance.ExecutionKind;control=$Control;rawExit=$code;watchdogExpired=$timedOut;watchdogReason=$(if($timedOut){$gate.Reason}else{''});memoryCapBytes=$(if($ProcessTimeoutSeconds -gt 120){4294967296}else{$null});reportPresent=($null-ne$report);exitConsistent=$consistent;startedUtc=$start.ToString('o');endedUtc=[DateTime]::UtcNow.ToString('o');stop=if($report){$report.result.Stop}else{'Interrupted'};replay=$ReplayPath} |ConvertTo-Json|Set-Content (Join-Path $out 'process.json') -Encoding utf8
CheckBinding
if(!$consistent){throw "Missing/contradictory result; raw child exit $code"}
exit $code
