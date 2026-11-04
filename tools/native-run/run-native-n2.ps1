param([Parameter(Mandatory)][string]$BuildManifest,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(!$out.StartsWith((Join-Path $root '.build/runs')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path $out)){throw 'Fresh Lab runs root required'}
New-Item -ItemType Directory $out|Out-Null
$m=Get-Content $BuildManifest -Raw|ConvertFrom-Json
$dll=Join-Path (Split-Path ([IO.Path]::GetFullPath($BuildManifest))) 'bin/Release/net9.0/SimulatorLab.NativeRun.dll'
$checks=[Collections.Generic.List[object]]::new();$rows=[Collections.Generic.List[object]]::new()
function Check($name,$ok,$detail=''){$checks.Add(@{name=$name;passed=[bool]$ok;detail=$detail});if(!$ok){Write-Host "FAILED $name : $detail"}}
function Run($id,$scenario,$expected=0,$replay='',$mode='detailed',$control='',$inputControl=''){
 $dir=Join-Path $out $id
 & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'run-native-slice.ps1') -BuildManifest $BuildManifest -OutputDirectory $dir -Scenario $scenario -ReplayPath $replay -Observation $mode -Control $control -InputControl $inputControl *> (Join-Path $out ($id+'.log'))
 $code=$LASTEXITCODE
 $r=Get-Content (Join-Path $dir 'result.json') -Raw|ConvertFrom-Json
 Check ($id+' raw exit') ($code-eq$expected -and $r.rawExit-eq$expected) "actual=$code expected=$expected $($r.result.Error)"
 $p=Get-Content (Join-Path $dir 'process.json') -Raw|ConvertFrom-Json
 $origin=if($control -or $inputControl -or $scenario.Contains(':') -or $scenario-in@('event-fault','sink','quota','softlock')){'fault-control'}elseif($scenario-in@('slice-skip','slice-take')){'natural-run'}else{'api-fixture'}
 $execution=if($mode-ne'detailed'){'observation-control'}elseif($replay){'frozen-replay'}else{'live'}
 $traceHeader=((Get-Content (Join-Path $dir 'trace.jsonl') -TotalCount 1)|ConvertFrom-Json).payload.detail
 Check ($id+' origin/execution cross-binding') ($p.scenarioKind-eq$origin -and $p.executionKind-eq$execution -and $r.provenance.ScenarioKind-eq$origin -and $traceHeader.provenance.ScenarioKind-eq$origin -and $traceHeader.provenance.ExecutionKind-eq$execution -and $r.provenance.ExecutionKind-eq$execution -and $p.provenance.Scenario-eq$scenario -and $p.provenance.InputControl-eq$inputControl)
 $rows.Add(@{id=$id;scenario=$scenario;mode=$mode;rawExit=$code;expected=$expected;confirmed=$r.result.Confirmed;dispatched=$r.result.Dispatched;stop=$r.result.Stop})
 if($expected-eq0){
  & dotnet $dll --review $dir *> (Join-Path $out ($id+'-review.log'))
  Check ($id+' production trace review') ($LASTEXITCODE-eq0) (Get-Content (Join-Path $out ($id+'-review.log')) -Raw)
 }
 return $dir
}
$positive=@('slice-skip','slice-take','grid','multi','empty','single','manual-single','bundle','bundle-shared','armaments','headbutt','headbutt-empty','headbutt-single','deck-upgrade','deck-generic','sequential','hand','choose-card','baseline-first','provider-first')
foreach($scenario in $positive){
 $a=Run ($scenario+'-1') $scenario
 $b=Run ($scenario+'-2') $scenario 0 (Join-Path $a 'route.json')
 foreach($f in @('route.json','choices.json','final-state.json')){Check ($scenario+' repeated '+$f) ((Get-FileHash (Join-Path $a $f)).Hash-eq(Get-FileHash (Join-Path $b $f)).Hash)}
 Write-Output "COMPLETED $scenario natural/fixture + replay"
}
Check 'provider/native automation baseline state identical' ((Get-FileHash (Join-Path $out 'baseline-first-1/final-state.json')).Hash-eq(Get-FileHash (Join-Path $out 'provider-first-1/final-state.json')).Hash)
foreach($scenario in @('grid:invalid','grid:missing','grid:duplicate','grid:too-few','grid:too-many','grid:stale','grid:wrong-request','grid:cross-room','grid:reused','grid:extra','grid:unknown-field','multi:too-few','multi:too-many','multi:duplicate','choose-card:too-few','bundle:invalid','bundle:missing','bundle:extra','armaments:invalid','event-fault','sink','quota','softlock')){
 $id=$scenario.Replace(':','-')
 $dir=Run $id $scenario 1
 $r=Get-Content (Join-Path $dir 'result.json') -Raw|ConvertFrom-Json
 $expectedStop=if($scenario-in@('sink','quota')){'DataError'}elseif($scenario-in@('event-fault','softlock')){'EngineError'}else{'InvalidAction'}
 Check ($id+' classification') ($r.result.Stop-eq$expectedStop)
 if($scenario-eq'armaments:invalid'){
  $state=Get-Content (Join-Path $dir 'final-state.json') -Raw|ConvertFrom-Json
  Check 'invalid Armaments preserves paid energy/block and in-flight prefix' ($r.result.Confirmed-eq4 -and $r.result.Dispatched-eq5 -and $r.choicesSubmitted-eq0 -and $state.block-eq5 -and $state.combat.energy-eq2)
 }
}
foreach($control in @('async-fault','stale','budget','subchoice')){$expected=if($control-eq'budget'){3}elseif($control-eq'subchoice'){2}else{1};$null=Run ('legacy-control-'+$control) 'slice-skip' $expected '' 'detailed' $control}
foreach($scenario in @('slice-skip','armaments')){foreach($mode in @('off','minimal')){
 $a=Join-Path $out ($scenario+'-1');$b=Run ($scenario+'-'+$mode) $scenario 0 (Join-Path $a 'route.json') $mode
 foreach($f in @('route.json','choices.json','final-state.json')){Check ($scenario+' noninterference '+$mode+' '+$f) ((Get-FileHash (Join-Path $a $f)).Hash-eq(Get-FileHash (Join-Path $b $f)).Hash)}
}}
$rootPlan=Join-Path $out 'control-root-extra-plan';New-Item -ItemType Directory $rootPlan|Out-Null
$route=Get-Content (Join-Path $out 'slice-skip-1/route.json') -Raw|ConvertFrom-Json
$route[0].action.decision|Add-Member -NotePropertyName extra -NotePropertyValue 'must-reject'
$route|ConvertTo-Json -Depth 30|Set-Content (Join-Path $rootPlan 'route.json');Copy-Item (Join-Path $out 'slice-skip-1/choices.json') $rootPlan
$bad=Run 'root-extra-replay' 'slice-skip' 1 (Join-Path $rootPlan 'route.json') 'detailed' '' 'root-extra'
$badResult=Get-Content (Join-Path $bad 'result.json') -Raw|ConvertFrom-Json
Check 'extra root field zero dispatch' ($badResult.result.Stop-eq'InvalidAction' -and $badResult.result.Dispatched-eq0)
foreach($case in @('unknown-choice-field','missing-choice-file')){
 $plan=Join-Path $out ('control-'+$case+'-plan');New-Item -ItemType Directory $plan|Out-Null
 Copy-Item (Join-Path $out 'grid-1/route.json') $plan
 if($case-eq'unknown-choice-field'){
  $choice=Get-Content (Join-Path $out 'grid-1/choices.json') -Raw|ConvertFrom-Json
  $choice[0].answer|Add-Member -NotePropertyName extra -NotePropertyValue 'reject'
  ConvertTo-Json -InputObject @($choice) -Depth 30|Set-Content (Join-Path $plan 'choices.json')
 }
 $bad=Run $case 'grid' 1 (Join-Path $plan 'route.json') 'detailed' '' $case
 $r=Get-Content (Join-Path $bad 'result.json') -Raw|ConvertFrom-Json
 Check ($case+' InvalidAction') ($r.result.Stop-eq'InvalidAction')
}
# Controlled corruptions are isolated copies; semantic controls rebuild hashes so rejection is not just a hash check.
foreach($control in @('gap','repeat','truncate','wronghash','dangling','missing-response','cleanup','emptyrng','inner-fault','wrong-exit','header-schema','seal-schema','result-schema','result-outcome','unknown-event','missing-state-field','missing-rng-field','null-run-rng','null-player-rng','null-event-rng','false-fixture-contract','missing-fixture-assertion','wrong-fixture-scenario','missing-history','missing-choice-return','wrong-choice-root','early-root-confirm','wrong-choice-object','wrong-returned-object','history-duplicate','history-reorder','missing-checkpoint','missing-root-request','wrong-task-root','process-origin','process-execution','mode-mismatch')){
 $source=Join-Path $out $(if($control-in@('dangling','missing-response','false-fixture-contract','missing-fixture-assertion','wrong-fixture-scenario','missing-choice-return','wrong-choice-root','early-root-confirm','wrong-choice-object','wrong-returned-object')){'grid-1'}else{'slice-skip-1'})
 $copy=Join-Path $out ('control-'+$control);New-Item -ItemType Directory $copy|Out-Null
 foreach($f in @('trace.jsonl','trace-seal.json','result.json','process.json')){Copy-Item (Join-Path $source $f) $copy}
 if($control-in@('process-origin','process-execution','mode-mismatch')){$p=Get-Content (Join-Path $copy 'process.json') -Raw|ConvertFrom-Json;if($control-eq'process-origin'){$p.scenarioKind='natural-run';$p.provenance.ScenarioKind='wrong-origin'}elseif($control-eq'process-execution'){$p.executionKind='live';$p.provenance.ExecutionKind='wrong-mode'}else{$p.observation='off'};$p|ConvertTo-Json -Depth 20|Set-Content (Join-Path $copy 'process.json')}
 elseif($control-eq'wrong-exit'){$p=Get-Content (Join-Path $copy 'process.json') -Raw|ConvertFrom-Json;$p.rawExit=2;$p|ConvertTo-Json|Set-Content (Join-Path $copy 'process.json')}
 elseif($control-eq'seal-schema'){$p=Get-Content (Join-Path $copy 'trace-seal.json') -Raw|ConvertFrom-Json;$p.schema='unknown';$p|ConvertTo-Json|Set-Content (Join-Path $copy 'trace-seal.json')}
 elseif($control-eq'result-schema'){$p=Get-Content (Join-Path $copy 'result.json') -Raw|ConvertFrom-Json;$p.result.Schema='unknown';$p|ConvertTo-Json -Depth 20|Set-Content (Join-Path $copy 'result.json')}
 elseif($control-eq'result-outcome'){$p=Get-Content (Join-Path $copy 'result.json') -Raw|ConvertFrom-Json;$p.result.Stop='unknown';$p|ConvertTo-Json -Depth 20|Set-Content (Join-Path $copy 'result.json')}
 else{& dotnet $dll --mutate $copy $control *> (Join-Path $out ($control+'-mutation.log'));Check ($control+' mutation executed') ($LASTEXITCODE-eq0)}
 & dotnet $dll --review $copy *> (Join-Path $out ($control+'-rejection.log'))
 $rejectedCode=$LASTEXITCODE
 $rejection=Get-Content (Join-Path $out ($control+'-rejection.log')) -Raw
 Check ($control+' production reviewer rejects') ($rejectedCode-eq1) $rejection
 $specific=@{'null-run-rng'='Null/nonobject runtime RNG';'null-player-rng'='Null/nonobject runtime RNG';'null-event-rng'='Missing event synchronizer RNG';'false-fixture-contract'='Failed/duplicate fixture assertion';'missing-fixture-assertion'='Missing/unexpected fixture assertions';'wrong-fixture-scenario'='Fixture scenario mismatch';'missing-history'='Missing/unexpected detailed history';'missing-choice-return'='Confirmation before child';'wrong-choice-root'='Choice owner/order mismatch';'early-root-confirm'='Choice root/context mismatch';'wrong-choice-object'='Resolved candidate instance mismatch';'wrong-returned-object'='Returned candidate instance/order mismatch';'history-duplicate'='Missing/duplicate/reordered history';'history-reorder'='Missing/duplicate/reordered history';'missing-checkpoint'='Unclosed roots/choices/tasks/history';'missing-root-request'='Dispatch without matching root';'wrong-task-root'='Task owner/order mismatch';'process-origin'='Scenario provenance mismatch';'process-execution'='Scenario provenance mismatch';'mode-mismatch'='Observation mode mismatch'}
 if($specific.ContainsKey($control)){Check ($control+' specific semantic gate') ($rejectedCode-eq1 -and $rejection.Contains($specific[$control])) $rejection}
}
# Snapshot presence controls use registered normal native records where available.
# death/sink-failure-trigger are schema-only substitutions; they do not claim native lifecycle execution.
$snapshotKinds=@('dispatch','confirmed','new-run','event-candidates','opening-finished','map-candidates','first-battle-opening','combat-candidates','combat-history','battle-victory-before-cleanup','battle-victory-cleanup','reward-offer','rewards-completed','card-reward-acquired','next-combat-first-play','fixture-setup-declared','fixture-assertion','fixture-contract','sink-failure-trigger','death')
$snapshotControls=[Collections.Generic.List[object]]::new()
function SnapshotControl($control,$source,$kind,$expectedMessage,$schemaOnly=$false){
 $label=$control.Replace(':','-')
 $copy=Join-Path $out ('control-'+$label);New-Item -ItemType Directory $copy|Out-Null
 foreach($f in @('trace.jsonl','trace-seal.json','result.json','process.json')){Copy-Item (Join-Path $out "$source/$f") $copy}
 & dotnet $dll --mutate $copy $control *> (Join-Path $out ($label+'-mutation.log'))
 $mutationExit=$LASTEXITCODE
 Check ($label+' mutation executed') ($mutationExit-eq0)
 & dotnet $dll --review $copy *> (Join-Path $out ($label+'-rejection.log'))
 $code=$LASTEXITCODE;$message=Get-Content (Join-Path $out ($label+'-rejection.log')) -Raw
 Check ($label+' production reviewer rejects') ($code-eq1) $message
 Check ($label+' specific snapshot gate') ($code-eq1 -and $message.Contains($expectedMessage)) $message
 $snapshotControls.Add(@{control=$control;kind=$kind;source=$source;schemaOnly=$schemaOnly;mutationExit=$mutationExit;reviewExit=$code;expected=$expectedMessage;observed=$message;passed=($mutationExit-eq0 -and $code-eq1 -and $message.Contains($expectedMessage))})
}
foreach($kind in $snapshotKinds){
 $source=if($kind.StartsWith('fixture-')){'grid-1'}elseif($kind-eq'card-reward-acquired'){'slice-take-1'}else{'slice-skip-1'}
 foreach($shape in @('missing','null','array')){
  $message=if($shape-eq'missing'){'Missing required state snapshot: '}elseif($shape-eq'null'){'Null state snapshot: '}else{'Nonobject state snapshot: '}
  SnapshotControl ("snapshot-$shape"+':'+$kind) $source $kind ($message+$kind) ($kind-in@('death','sink-failure-trigger'))
 }
}
SnapshotControl 'missing-all-state' 'slice-skip-1' 'all' 'Missing required state snapshot: new-run'
SnapshotControl 'missing-one-boundary-state' 'slice-skip-1' 'confirmed' 'Missing required state snapshot: confirmed'
foreach($shape in @('string','number','boolean')){SnapshotControl ("snapshot-$shape"+':confirmed') 'slice-skip-1' 'confirmed' 'Nonobject state snapshot: confirmed'}
# Fault metadata has no state by contract; inject into a natural evidence copy so
# the earlier fault-control provenance gate does not obscure the intended semantic gate.
foreach($control in @('inner-fault','selection-fault-record')){
 $copy=Join-Path $out ('control-metadata-'+$control);New-Item -ItemType Directory $copy|Out-Null
 foreach($f in @('trace.jsonl','trace-seal.json','result.json','process.json')){Copy-Item (Join-Path $out "slice-skip-1/$f") $copy}
 & dotnet $dll --mutate $copy $control *> (Join-Path $out ('metadata-'+$control+'-mutation.log'))
 Check ($control+' metadata mutation executed') ($LASTEXITCODE-eq0)
 & dotnet $dll --review $copy *> (Join-Path $out ('metadata-'+$control+'-review.log'))
 $code=$LASTEXITCODE;$message=Get-Content (Join-Path $out ('metadata-'+$control+'-review.log')) -Raw
 Check ($control+' metadata reader rejects') ($code-eq1) $message
 Check ($control+' metadata accepted before semantic fault rejection') ($code-eq1 -and $message.Contains('Faulted inner task')) $message
}
@{schema='native-snapshot-controls/v1';buildId=$m.buildId;requiredKinds=$snapshotKinds;controlCount=$snapshotControls.Count;controls=$snapshotControls;failures=@($snapshotControls|Where-Object {!$_.passed}).Count}|ConvertTo-Json -Depth 12|Set-Content (Join-Path $out 'snapshot-controls.json')
$summary=@{schema='native-n2-suite/v1';buildId=$m.buildId;processCount=$rows.Count;processes=$rows;checkCount=$checks.Count;checks=$checks;failures=@($checks|Where-Object {!$_.passed}).Count;fullM1Complete=$false}
$summary|ConvertTo-Json -Depth 12|Set-Content (Join-Path $out 'summary.json')
Write-Output "N2 processes=$($rows.Count) checks=$($checks.Count) failures=$($summary.failures)"
if($summary.failures){exit 1}
