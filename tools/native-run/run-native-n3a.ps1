param([Parameter(Mandatory)][string]$BuildManifest,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(!$out.StartsWith((Join-Path $root '.build/runs')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or(Test-Path $out)){throw 'Fresh Lab output required'}
New-Item -ItemType Directory $out|Out-Null
$m=Get-Content $BuildManifest -Raw|ConvertFrom-Json
$dll=Join-Path (Split-Path ([IO.Path]::GetFullPath($BuildManifest))) 'bin/Release/net9.0/SimulatorLab.NativeRun.dll'
$checks=[Collections.Generic.List[object]]::new();$processes=[Collections.Generic.List[object]]::new()
function Check($name,$passed,$detail=''){$checks.Add(@{name=$name;passed=[bool]$passed;detail=$detail});if(!$passed){Write-Output "FAILED $name $detail"}}
function Run($id,$scenario,$expected,$replay='',$mode='detailed'){
 $dir=Join-Path $out $id
 & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'run-native-slice.ps1') -BuildManifest $BuildManifest -OutputDirectory $dir -Scenario $scenario -ReplayPath $replay -Observation $mode *> (Join-Path $out ($id+'.log'))
 $raw=$LASTEXITCODE;$r=Get-Content (Join-Path $dir 'result.json') -Raw|ConvertFrom-Json;$p=Get-Content (Join-Path $dir 'process.json') -Raw|ConvertFrom-Json
 Check ($id+' actual exit/build') ($raw-eq$expected-and$r.rawExit-eq$expected-and$p.rawExit-eq$expected-and$p.exitConsistent-and$p.buildId-eq$m.buildId) $r.result.Error
 $origin=if($scenario-eq'natural-death'){'natural-run'}elseif($scenario-in@('tail-next-act','tail-architect')){'api-fixture'}else{'fault-control'}
 Check ($id+' origin/execution') ($p.scenarioKind-eq$origin-and$r.provenance.ScenarioKind-eq$origin-and$p.executionKind-eq$(if($mode-ne'detailed'){'observation-control'}elseif($replay){'frozen-replay'}else{'live'}))
 $processes.Add(@{id=$id;scenario=$scenario;rawExit=$raw;expected=$expected;confirmed=$r.result.Confirmed;dispatched=$r.result.Dispatched;terminal=$r.result.Terminal;stop=$r.result.Stop})
 if($expected-eq0){& dotnet $dll --review $dir *> (Join-Path $out ($id+'-review.log'));Check ($id+' production review') ($LASTEXITCODE-eq0) (Get-Content (Join-Path $out ($id+'-review.log')) -Raw)}
 Write-Output "Completed $id exit=$raw confirmed=$($r.result.Confirmed) dispatched=$($r.result.Dispatched)"
}
foreach($scenario in @('natural-death','tail-next-act','tail-architect')){
 Run ($scenario+'-1') $scenario 0
 $route=Join-Path $out ($scenario+'-1/route.json')
 Run ($scenario+'-2') $scenario 0 $route
 foreach($mode in @('off','minimal')){Run ($scenario+'-'+$mode) $scenario 0 $route $mode}
 foreach($suffix in @('2','off','minimal')){foreach($file in @('route.json','choices.json','final-state.json')){Check ($scenario+' replay/noninterference '+$suffix+'/'+$file) ((Get-FileHash (Join-Path $out ($scenario+'-1/'+$file))).Hash-eq(Get-FileHash (Join-Path $out ($scenario+'-'+$suffix+'/'+$file))).Hash)}}
}
foreach($scenario in @('natural-death-budget','natural-death-fault','tail-architect-prefix-fault','tail-architect-win-fault')){
 Run $scenario $scenario $(if($scenario-eq'natural-death-budget'){3}else{1})
 $r=Get-Content (Join-Path $out ($scenario+'/result.json')) -Raw|ConvertFrom-Json
 $trace=@(Get-Content (Join-Path $out ($scenario+'/trace.jsonl'))|ForEach-Object {(ConvertFrom-Json $_).payload})
 $wins=@($trace|Where-Object kind -eq 'run-win-completed').Count
 if($scenario-eq'natural-death-budget'){Check 'budget stops after one actual enemy turn' ($r.result.Stop-eq'BudgetExceeded'-and$r.result.Confirmed-eq4-and$r.result.Dispatched-eq4-and$r.result.Terminal-eq'Ongoing')}
 else{Check ($scenario+' unconfirmed prefix/failed terminal') ($r.result.Stop-eq'EngineError'-and$r.result.Dispatched-eq($r.result.Confirmed+1)-and$r.result.Terminal-eq'Unknown'-and$wins-eq0)}
 if($scenario-eq'tail-architect-prefix-fault'){Check 'OnEnded prefix is not successful native end' (@($trace|Where-Object kind -eq 'run-end-start').Count-eq1-and@($trace|Where-Object kind -eq 'run-end-return').Count-eq0)}
 if($scenario-eq'tail-architect-win-fault'){Check 'native cleanup hp zero with failed wrapper not reclassified Death' ((Get-Content (Join-Path $out ($scenario+'/final-state.json')) -Raw|ConvertFrom-Json).hp-eq0-and@($trace|Where-Object kind -eq 'run-end-return').Count-eq1)}
}
$deathTrace=@(Get-Content (Join-Path $out 'natural-death-1/trace.jsonl')|ForEach-Object {(ConvertFrom-Json $_).payload})
$damage=@($deathTrace|Where-Object {$_.kind-eq'combat-history'-and$_.detail.facts.type-eq'DamageReceivedEntry'})
Check 'independent death 80 HP accounted by actual native damage' ((($damage|ForEach-Object {$_.detail.facts.facts.Result.UnblockedDamage}|Measure-Object -Sum).Sum)-eq80-and@($damage|Where-Object {$_.detail.facts.side-ne'Enemy'}).Count-eq0)
Check 'natural death has no victory cleanup or reward' (@($deathTrace|Where-Object {$_.kind-in@('battle-victory-before-cleanup','battle-victory-cleanup','reward-offer','run-end-return')}).Count-eq0)
$winTrace=@(Get-Content (Join-Path $out 'tail-architect-1/trace.jsonl')|ForEach-Object {(ConvertFrom-Json $_).payload})
Check 'native OnEnded before player kill then original WinRun complete' (($winTrace|Where-Object kind -eq 'run-end-return').detail.state.hp-eq80-and($winTrace|Where-Object kind -eq 'run-win-completed').detail.state.hp-eq0)
Check 'offline original Steam refresh guarded once' (@($winTrace|Where-Object kind -eq 'platform-offline-guard').Count-eq1)
foreach($row in @(
 @('natural-death','lifecycle-remove:terminal-stamp','Terminal contract before terminal stamp'),
 @('natural-death','lifecycle-remove:battle-loss-confirmed','Lifecycle counter mismatch: Losses'),
 @('tail-architect','lifecycle-remove:run-win-completed','Lifecycle counter mismatch: WinReturns'),
 @('tail-architect','lifecycle-remove:run-end-return','Win completion without successful OnEnded'),
 @('tail-architect','lifecycle-missing-rng','Missing event model RNG'),
 @('tail-next-act','lifecycle-remove:act-transition-completed','Lifecycle counter mismatch: ActReturns'),
 @('tail-next-act','lifecycle-remove:tail-contract','Incomplete tail fixture lifecycle'),
 @('tail-next-act','lifecycle-false-contract','Failed tail contract')
 )){
 $control=$row[1];$dir=Join-Path $out ('control-'+$control.Replace(':','-'));New-Item -ItemType Directory $dir|Out-Null
 foreach($f in @('trace.jsonl','trace-seal.json','result.json','process.json')){Copy-Item (Join-Path $out ($row[0]+'-1/'+$f)) $dir}
 & dotnet $dll --mutate $dir $control *> (Join-Path $dir 'mutation.log');Check ($control+' mutation') ($LASTEXITCODE-eq0)
 & dotnet $dll --review $dir *> (Join-Path $dir 'review.log');$code=$LASTEXITCODE;$message=Get-Content (Join-Path $dir 'review.log') -Raw
 Check ($control+' actual semantic rejection') ($code-eq1-and$message.Contains($row[2])) $message
}
$terminalControls=[Collections.Generic.List[object]]::new()
$terminalCases=@(
 @('natural-death','death-stamp-alive',1,'Terminal stamp requires actual dead player snapshot'),
 @('tail-architect','architect-zero-authority',1,'Lifecycle counter mismatch: ActStarts'),
 @('tail-architect','terminal-contract-wrong-terminal',1,'Terminal contract/stamp mismatch'),
 @('tail-architect','architect-missing-event-identity',1,'Missing event model identity/completion'),
 @('tail-next-act','next-act-snapshot-contradiction',1,'Tail current snapshot mismatch'),
 @('tail-architect','architect-detached-lifecycle',1,'Lifecycle active action owner mismatch'),
 @('tail-architect','terminal-contract-before-native-win',1,'Lifecycle confirmed boundary owner mismatch'),
 @('natural-death','duplicate-terminal-stamp',1,'Duplicate lifecycle event: terminal-stamp'),
 @('natural-death','missing-terminal-state',1,'Missing required state snapshot: terminal-stamp'),
 @('tail-architect','null-win-state',1,'Null state snapshot: run-win-completed'),
 @('tail-next-act','nonobject-transition-state',1,'Nonobject state snapshot: act-transition-completed'),
 @('tail-architect','missing-terminal-counter',1,'Lifecycle counter mismatch: WinReturns'),
 @('tail-architect','wrong-win-context',1,'Lifecycle active action owner mismatch'),
 @('tail-architect','wrong-stamp-facts',1,'Terminal facts/snapshot authority mismatch'),
 @('tail-architect','unclosed-terminal-state',1,'Lifecycle Won/Closed mismatch'),
 @('tail-architect','unfinished-architect',1,'Architect option not finished at terminal stamp'),
 @('tail-architect','wrong-event-model',1,'Event model identity mismatch'),
 @('tail-architect','wrong-registered-event-instance',1,'Event model identity mismatch'),
 @('tail-architect','duplicate-win-completion',1,'Duplicate lifecycle event: run-win-completed'),
 @('tail-architect','early-terminal-stamp',1,'Lifecycle confirmed boundary owner mismatch'),
 @('tail-architect','post-terminal-dispatch',1,'Action after terminal stamp'),
 @('tail-architect','alive-win-completion',1,'Native terminal completion snapshot is alive'),
 @('natural-death','alive-terminal-continuation',1,'Terminal continuation snapshot is alive'),
 @('tail-architect','lost-pre-cleanup-hp',1,'OnEnded changed pre-cleanup HP snapshot'),
 @('tail-architect','no-change',0,''),
 @('tail-architect','equivalent-property-order',0,''),
 @('tail-architect','architect-stamp-wrong-act',1,'Confirmed lifecycle state mismatch: terminal-stamp'),
 @('natural-death','death-stamp-gold-created',1,'Confirmed lifecycle state mismatch: terminal-stamp'),
 @('tail-architect','architect-contract-rng-changed',1,'Confirmed lifecycle state mismatch: terminal-contract'),
 @('tail-next-act','next-act-tail-rng-changed',1,'Confirmed lifecycle state mismatch: tail-contract'),
 @('natural-death','death-completion-wrong-root',1,'Lifecycle active action owner mismatch'),
 @('tail-architect','win-completion-before-end-return',1,'Win completion without successful OnEnded'),
 @('tail-next-act','duplicate-act-completion',1,'Duplicate lifecycle event: act-transition-completed')
 )
# These are static post-confirmation observation boundaries, never pre-cleanup callbacks.
foreach($boundary in @(
 @('natural-death','death'),@('natural-death','terminal-stamp'),@('natural-death','terminal-contract'),
 @('tail-architect','terminal-stamp'),@('tail-architect','terminal-contract'),@('tail-architect','tail-contract'),
 @('tail-next-act','tail-contract')
 )){
 foreach($field in @('block','deck-upgrade','deck-instance','player-rng','added-field','lifecycle-extra','missing-extension')){
  $diagnostic=if($boundary[0]-eq'tail-next-act'-and$field-in@('deck-upgrade','deck-instance')){'Tail current snapshot mismatch'}else{'Confirmed lifecycle state mismatch: '+$boundary[1]}
  $terminalCases+=,@($boundary[0],('confirmed-state|'+$boundary[1]+'|'+$field),1,$diagnostic)
 }
}
foreach($source in @('natural-death','tail-architect','tail-next-act')){
 foreach($positive in @('equivalent-state-property-order','equivalent-state-extension')){$terminalCases+=,@($source,$positive,0,'')}
}
foreach($row in $terminalCases){
 $control='terminal-review:'+$row[1];$dir=Join-Path $out ('control-'+$control.Replace(':','-').Replace('|','-')+'-'+$row[0]);New-Item -ItemType Directory $dir|Out-Null
 foreach($f in @('trace.jsonl','trace-seal.json','result.json','process.json')){Copy-Item (Join-Path $out ($row[0]+'-1/'+$f)) $dir}
 & dotnet $dll --mutate $dir $control *> (Join-Path $dir 'mutation.log');$mutated=$LASTEXITCODE-eq0;Check ($control+' mutation') $mutated
 & dotnet $dll --review $dir *> (Join-Path $dir 'review.log');$raw=$LASTEXITCODE;$message=Get-Content (Join-Path $dir 'review.log') -Raw
 $passed=$mutated-and$raw-eq$row[2]-and($row[2]-eq0-or$message.Contains($row[3]))
 Check ($control+' production semantic gate') $passed $message
 $terminalControls.Add(@{control=$row[1];source=$row[0];expectedExit=$row[2];rawExit=$raw;expectedDiagnostic=$row[3];message=$message;passed=$passed;scope=if($row[2]-eq0){'equivalent-positive'}else{'evidence-corruption-control'}})
}
@{buildId=$m.buildId;controls=$terminalControls;count=$terminalControls.Count;failures=@($terminalControls|Where-Object {!$_.passed}).Count}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $out 'terminal-review-controls.json')
@{schema='native-n3a-suite/v1';buildId=$m.buildId;processCount=$processes.Count;processes=$processes;checkCount=$checks.Count;checks=$checks;failures=@($checks|Where-Object {!$_.passed}).Count;normalStartFullRun=$false}|ConvertTo-Json -Depth 15|Set-Content (Join-Path $out 'summary.json')
Write-Output "N3A processes=$($processes.Count) checks=$($checks.Count) failures=$(@($checks|Where-Object {!$_.passed}).Count)"
if(@($checks|Where-Object {!$_.passed}).Count){exit 1}
