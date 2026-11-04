param([Parameter(Mandatory)][string]$HarnessRoot,[Parameter(Mandatory)][string]$OutputDirectory,[string]$SourcePlan='')
$ErrorActionPreference='Stop'
$lab=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'));$out=[IO.Path]::GetFullPath($OutputDirectory)
if(-not$out.StartsWith((Join-Path $lab '.build/runs')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or(Test-Path $out)){throw 'Use a fresh Lab runs directory.'}
New-Item -ItemType Directory -Path $out|Out-Null
$dll=Join-Path $HarnessRoot 'bin/Release/net9.0/OfflineSearchHarness.dll'
$source=if($SourcePlan){[IO.Path]::GetFullPath($SourcePlan)}else{Join-Path $lab '.build/runs/reward-final-02/batch/seed1-skip-a/battle/route.json'}
# A preserved nine-action native route supplies a legal prefix; only the explicitly labelled invalid field changes.
$original=Get-Content -LiteralPath $source -Raw|ConvertFrom-Json
$pin=Join-Path $lab '.build/pinned-inputs/eleanor-v0.4.1-forms-cardpool.fix'
$vars=@{SIMULATORLAB_ELEANOR_MOD='1';SIMULATORLAB_ELEANOR_MOD_DLL=(Join-Path $pin 'MyMod.dll');SIMULATORLAB_ELEANOR_BASELIB_DLL=(Join-Path $pin 'dependencies/BaseLib.dll');SIMULATORLAB_ELEANOR_MANIFEST=(Join-Path $pin 'MyMod.json');SIMULATORLAB_ELEANOR_MOD_VERSION='v0.4.1-forms-cardpool.fix';SIMULATORLAB_ELEANOR_V040_PROBE='1';SIMULATORLAB_ELEANOR_V040_FORM='Reverse';SIMULATORLAB_NATIVE_PROFILE=(Join-Path $lab '.build/pinned-inputs/eleanor-v041-native-bootstrap');SIMULATORLAB_NATIVE_LOOP_STAGE='Loop';SIMULATORLAB_NATIVE_REWARD_POLICY='skip';SIMULATORLAB_NATIVE_DECISION_MODE='coverage-plan';SIMULATORLAB_NATIVE_RECORD_FIXTURE='0';SIMULATORLAB_NATIVE_SECOND_BATTLE='0';SIMULATORLAB_NATIVE_SECOND_PLAN='';SIMULATORLAB_NATIVE_SECOND_PLAN_SHA='';SIMULATORLAB_SHIELD_FIXTURE='0';SIMULATORLAB_STAGE_ONE_CASE='';SIMULATORLAB_REWARD_CASE='';SIMULATORLAB_NATIVE_FIXED_PLAN='';SIMULATORLAB_NATIVE_FIXED_PLAN_SHA=''}
$saved=@{};foreach($key in $vars.Keys){$saved[$key]=[Environment]::GetEnvironmentVariable($key);[Environment]::SetEnvironmentVariable($key,$vars[$key])}
$rows=@();$checks=@()
try{
 foreach($mode in 'endturn-nested','endturn-turnstart','playcard-zero','playcard-prefix'){
  $plan=Get-Content -LiteralPath $source -Raw|ConvertFrom-Json
  $index=if($mode-like'endturn-*'){[Array]::FindIndex([object[]]$plan,[Predicate[object]]{param($a)$a.kind-eq'EndTurn'})}elseif($mode-eq'playcard-zero'){0}else{1}
  $bad=@{effect='ModDefined';sourcePile='Hand';cards=@();sourceId='CONTROL_INVALID_SOURCE';contextId='control/unused';timing='PlayerTurnStart'}
  if($mode-eq'endturn-nested'){$plan[$index].nestedChoices=@($bad)}else{$plan[$index].turnStartChoices=@($bad)}
  $file=Join-Path $out "$mode.json";$plan|ConvertTo-Json -Depth 100|Set-Content -LiteralPath $file -Encoding utf8
  $env:SIMULATORLAB_NATIVE_FIXED_PLAN=$file;$env:SIMULATORLAB_NATIVE_FIXED_PLAN_SHA=(Get-FileHash $file).Hash
  $dir=Join-Path $out $mode
  & dotnet $dll --character MyMod --encounter FUZZY_WURM_CRAWLER_WEAK --seed LAB_V0111_ELEANOR_V040_FORM_01 --profile Low --beam 8 --nodes 1000 --dop 1 --budget-ms 5000 --search-mode Evaluate --milestone M2 --out $dir *> (Join-Path $out "$mode.log")
  $code=$LASTEXITCODE;$r=Get-Content (Join-Path $dir 'native-run-loop.json') -Raw|ConvertFrom-Json;$b=$r.battle
  $prefix=@($b.steps);$prior=@($original|Select-Object -First $index)
  $same=$prefix.Count-eq$index
  for($i=0;$i-lt$prefix.Count;$i++){$same=$same-and$prefix[$i].kind-eq$prior[$i].kind-and$prefix[$i].cardId-eq$prior[$i].cardId-and$prefix[$i].exactStateMatch-and$prefix[$i].parentBranchUnchanged-and$prefix[$i].nativeContinuation-ceq$prefix[$i].simulatorContinuation}
  $okay=$code-eq1-and$r.status-eq'invalid-action'-and$b.status-eq'invalid-action'-and$b.invalidActionNativeUnchanged-and$b.nativeDispatchCount-eq$index-and$b.outcome.ConfirmedActions-eq$index-and$same-and-not$b.nativeVictory
  $rows+=@{id=$mode;rawExit=$code;expectedExit=1;expectedPrefix=$index;confirmedPrefix=$b.outcome.ConfirmedActions;nativeDispatchCount=$b.nativeDispatchCount;rejectedActionSourceUnchanged=$b.invalidActionNativeUnchanged;passed=[bool]$okay;report=(Join-Path $dir 'native-run-loop.json')}
  $checks+=@{name="$mode invalid before dispatch, typed failure exit and prefix preserved";passed=[bool]$okay}
 }
}finally{foreach($key in $saved.Keys){[Environment]::SetEnvironmentVariable($key,$saved[$key])}}
$okay=@($checks|Where-Object{-not$_.passed}).Count-eq0
@{schema='reward-choice-field-controls/v1';status=if($okay){'passed'}else{'failed'};harnessSha256=(Get-FileHash $dll).Hash;sourcePlanSha256=(Get-FileHash $source).Hash;cases=$rows;checks=$checks}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $out 'summary.json') -Encoding utf8
if(-not$okay){exit 1}
