param([Parameter(Mandatory)][string]$BuildManifest,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(!$out.StartsWith((Join-Path $root '.build/runs')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or(Test-Path $out)){throw 'Fresh Lab output required'}
New-Item -ItemType Directory $out|Out-Null
$m=Get-Content $BuildManifest -Raw|ConvertFrom-Json
$dll=Join-Path (Split-Path ([IO.Path]::GetFullPath($BuildManifest))) 'bin/Release/net9.0/SimulatorLab.NativeRun.dll'
$checks=[Collections.Generic.List[object]]::new();$processes=[Collections.Generic.List[object]]::new()
function Check($name,$passed,$detail=''){$checks.Add(@{name=$name;passed=[bool]$passed;detail=$detail});if(!$passed){Write-Host "FAILED $name $detail"}}
function Run($id,$scenario,$expected=0,$replay='',$mode='detailed',$seed='LAB_V0111_NATIVE_DEV_01'){
 $dir=Join-Path $out $id
 & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'run-native-slice.ps1') -BuildManifest $BuildManifest -OutputDirectory $dir -Scenario $scenario -ReplayPath $replay -Observation $mode -Seed $seed *> (Join-Path $out ($id+'.log'))
 $raw=$LASTEXITCODE;$r=Get-Content (Join-Path $dir 'result.json') -Raw|ConvertFrom-Json;$p=Get-Content (Join-Path $dir 'process.json') -Raw|ConvertFrom-Json
 Check ($id+' actual exit/freeze') ($raw-eq$expected-and$r.rawExit-eq$expected-and$p.rawExit-eq$expected-and$p.exitConsistent-and$p.buildId-eq$m.buildId-and!$p.watchdogExpired) $r.result.Error
 $origin=if($scenario-eq'rooms-natural'){'natural-run'}elseif($scenario-in@('room-heal','room-cap','room-smith','room-cancel','room-abstain','room-phases','room-exit')){'api-fixture'}else{'fault-control'}
 Check ($id+' origin/mode') ($p.scenarioKind-eq$origin-and$r.provenance.ScenarioKind-eq$origin-and$p.executionKind-eq$(if($mode-ne'detailed'){'observation-control'}elseif($replay){'frozen-replay'}else{'live'}))
 $processes.Add(@{id=$id;scenario=$scenario;seed=$seed;rawExit=$raw;expected=$expected;confirmed=$r.result.Confirmed;dispatched=$r.result.Dispatched;terminal=$r.result.Terminal;stop=$r.result.Stop})
 if($expected-eq0){& dotnet $dll --review $dir *> (Join-Path $out ($id+'-review.log'));Check ($id+' production review') ($LASTEXITCODE-eq0) (Get-Content (Join-Path $out ($id+'-review.log')) -Raw)}
 Write-Host "Completed $id exit=$raw prefix=$($r.result.Confirmed)/$($r.result.Dispatched) $($r.result.Terminal)/$($r.result.Stop)"
 return $dir
}
function Same($id,$a,$b){foreach($f in @('route.json','choices.json','final-state.json')){Check ($id+' repeat/fullRNG '+$f) ((Get-FileHash (Join-Path $a $f)).Hash-eq(Get-FileHash (Join-Path $b $f)).Hash)}}
foreach($s in @('room-heal','room-cap','room-smith','room-cancel','room-abstain','room-phases','room-exit')){
 $a=Run ($s+'-1') $s;$b=Run ($s+'-2') $s 0 (Join-Path $a 'route.json');Same $s $a $b
}
foreach($s in @('room-heal','room-phases')){foreach($mode in @('minimal','off')){$a=Join-Path $out ($s+'-1');$b=Run ($s+'-'+$mode) $s 0 (Join-Path $a 'route.json') $mode;Same ($s+' '+$mode) $a $b}}
foreach($s in @('room-negative','room-stale','room-duplicate','room-extra','room-disabled','room-early-exit','room-task-fault','room-budget','room-invalid-child')){
 $expected=if($s-eq'room-budget'){3}else{1};$dir=Run $s $s $expected
 $r=Get-Content (Join-Path $dir 'result.json') -Raw|ConvertFrom-Json
 $stop=if($s-eq'room-budget'){'BudgetExceeded'}elseif($s-eq'room-task-fault'){'EngineError'}else{'InvalidAction'}
 $c=if($s-eq'room-early-exit'){4}else{3};$d=if($s-in@('room-task-fault','room-invalid-child')){4}else{$c}
 Check ($s+' failure classification/prefix') ($r.result.Stop-eq$stop-and$r.result.Confirmed-eq$c-and$r.result.Dispatched-eq$d-and!$r.result.DataComplete-and!$r.trace.complete)
}
foreach($n in @(1,2,3)){
 $seed='LAB_V0111_NATIVE_DEV_0'+$n;$expected=if($n-eq3){2}else{0};$dir=Run ('natural-0'+$n) rooms-natural $expected '' detailed $seed
 $r=Get-Content (Join-Path $dir 'result.json') -Raw|ConvertFrom-Json
 if($n-eq1){Check 'DEV01 actual natural goal' ($r.result.Terminal-eq'Ongoing'-and$r.result.Stop-eq'SliceBoundary')}
 if($n-eq2){Check 'DEV02 actual death explicit' ($r.result.Terminal-eq'Death'-and$r.result.Stop-eq'Completed')}
 if($n-eq3){Check 'DEV03 unsupported retained' ($r.result.Stop-eq'Unsupported'-and$r.result.Dispatched-eq($r.result.Confirmed+1)-and!$r.result.DataComplete)}
 if($expected-eq0){$b=Run ('natural-0'+$n+'-replay') rooms-natural 0 (Join-Path $dir 'route.json') detailed $seed;Same ('natural-0'+$n) $dir $b}
}
foreach($mode in @('minimal','off')){$a=Join-Path $out 'natural-01';$b=Run ('natural-01-'+$mode) rooms-natural 0 (Join-Path $a 'route.json') $mode;Same ('natural-01 '+$mode) $a $b}
$controls=[Collections.Generic.List[object]]::new()
$controlRows=@(
 @('room-heal-1','missing-room-state',''),@('room-abstain-1','null-event-rng','Missing room event identity/RNG'),
 @('room-abstain-1','wrong-event-identity','Room event model identity mismatch'),@('room-heal-1','wrong-instance','Room operation instance association mismatch'),
 @('room-heal-1','duplicate-complete','Duplicate room completion'),@('room-heal-1','duplicate-native-return','Room native return without start/duplicate'),
 @('room-heal-1','missing-exit-return','Room confirmed before native return/state mismatch'),@('room-heal-1','missing-option-return','Room confirmed before native return/state mismatch'),
 @('room-heal-1','missing-assertion','Incomplete room fixture lifecycle'),@('room-heal-1','missing-candidates','Room dispatch changed request state/session'),
 @('room-heal-1','false-assertion','Room assertion snapshot/valid mismatch'),@('room-heal-1','static-new-field','Room complete confirmed state mismatch'),
 @('room-heal-1','detached-return','Room active action owner mismatch'),@('room-phases-1','wrong-boundary-finished','Room boundary before native return/result mismatch'),
 @('room-cancel-1','fake-cancel-success','Successful rest did not consume option'),@('room-smith-1','wrong-dispatch','Room dispatch option mismatch'),
 @('room-heal-1','coherent-wrong-heal','Independent rest heal mismatch'),@('room-phases-1','unfinished-event-complete','Room completed with unfinished event'),
 @('room-heal-1','early-complete','Room static observation owner mismatch'),@('natural-01','missing-rewards','Combat room completed before native victory/rewards'),
 @('natural-01','wrong-persistent','Natural room goal missing actual battles/upgrade'),
 @('room-phases-1','no-change',''),@('natural-01','property-order','')
 )
$controlRows+=@(
 @('room-cancel-1','repair|remove|room-option-boundary','Missing room choice result boundary'),
 @('room-cancel-1','repair|duplicate|room-option-boundary','Room choice boundary request/owner/unique mismatch'),
 @('room-cancel-1','repair|stale-boundary','Room choice boundary request/owner/unique mismatch'),
 @('room-cancel-1','repair|stale-return','Room native return without start/duplicate'),
 @('room-cancel-1','repair|early-boundary','Room static observation owner mismatch'),
 @('room-cancel-1','repair|wrong-session','Room session association mismatch'),
 @('natural-01','repair|detach|battle-victory-before-cleanup','Battle victory active action owner mismatch'),
 @('natural-01','repair|detach|battle-victory-cleanup','Room static observation owner mismatch'),
 @('natural-01','repair|detach|rewards-completed','Room static observation owner mismatch'),
 @('natural-01','repair|detach|reward-offer','Reward offer lifecycle owner/order mismatch'),
 @('natural-01','repair|remove|room-exit-return','Room confirmed before native return/state mismatch'),
 @('natural-01','repair|duplicate|room-exit-return','Room exit return owner/order mismatch'),
 @('natural-01','repair|remove|battle-victory-before-cleanup','Missing/duplicate native battle cleanup'),
 @('natural-01','repair|duplicate|battle-victory-before-cleanup','Duplicate/misplaced battle victory'),
 @('natural-01','repair|remove|battle-victory-cleanup','Reward offer lifecycle owner/order mismatch'),
 @('natural-01','repair|duplicate|battle-victory-cleanup','Missing/duplicate native battle cleanup'),
 @('natural-01','repair|remove|reward-offer','Reward action without current room offer'),
 @('natural-01','repair|duplicate|reward-offer','Reward offer lifecycle owner/order mismatch'),
 @('natural-01','repair|duplicate|rewards-completed','Rewards completion without actual victory/collection'),
 @('natural-01','repair|early-rewards','Room static observation owner mismatch'),
 @('natural-01','repair|unclosed-rewards','Rewards completion before collection closed'),
 @('natural-01','repair|cross-set','Reward collection set mismatch'),
 @('natural-01','repair|equivalent-extension','')
)
foreach($kind in @('room-option-boundary','room-exit-return','battle-victory-cleanup','rewards-completed')){
 foreach($field in @('gold','deck-upgrade','rng','extension','missing-extension')){
  $diagnostic=if($kind-eq'room-exit-return'){'Room confirmed before native return/state mismatch'}else{'Room complete confirmed state mismatch'}
  $controlRows+=,@('natural-01',('repair|state|'+$kind+'|'+$field),$diagnostic)
 }
}
# These starts precede native execution and must equal their own dispatch in full.
# Exercise both Travel and RoomExit, plus the adjacent already-bound choice start.
foreach($kind in @('room-exit-start','room-option-start')){
 foreach($field in @('gold','deck-upgrade','rng','player-rng','extension','missing-extension')){
  $diagnostic=if($kind-eq'room-exit-start'){'Room exit start dispatch state mismatch'}else{'Disabled/stale room dispatch'}
  $controlRows+=,@('natural-01',('repair|state|'+$kind+'|'+$field),$diagnostic)
 }
}
$controlRows+=,@('room-heal-1','repair|state|room-exit-start|gold','Room exit start dispatch state mismatch')
foreach($row in $controlRows){
 $dir=Join-Path $out ('control-room-'+$row[1].Replace('|','-')+'-'+$row[0]);New-Item -ItemType Directory $dir|Out-Null
 foreach($f in @('trace.jsonl','trace-seal.json','result.json','process.json')){Copy-Item (Join-Path $out ($row[0]+'/'+$f)) $dir}
 & dotnet $dll --mutate $dir ('room-review:'+$row[1]) *> (Join-Path $dir 'mutation.log');$mutate=$LASTEXITCODE
 Check ($row[1]+' mutation') ($mutate-eq0)
 & dotnet $dll --review $dir *> (Join-Path $dir 'review.log');$code=$LASTEXITCODE;$message=Get-Content (Join-Path $dir 'review.log') -Raw
 $expected=if($row[1]-in@('no-change','property-order','repair|equivalent-extension')){0}else{1}
 $ok=$mutate-eq0-and$code-eq$expected-and(!$row[2]-or$message.Contains($row[2]))
 Check ($row[1]+' actual production gate') $ok $message
 $controls.Add(@{source=$row[0];control=$row[1];expectedExit=$expected;rawExit=$code;message=$message;passed=$ok})
}
@{buildId=$m.buildId;controls=$controls;count=$controls.Count;failures=@($controls|Where-Object {!$_.passed}).Count}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $out 'room-controls.json')
@{schema='native-n3b-suite/v1';buildId=$m.buildId;processCount=$processes.Count;processes=$processes;checkCount=$checks.Count;checks=$checks;failures=@($checks|Where-Object {!$_.passed}).Count;normalStartFullRun=$false}|ConvertTo-Json -Depth 15|Set-Content (Join-Path $out 'summary.json')
Write-Output "N3B processes=$($processes.Count) checks=$($checks.Count) failures=$(@($checks|Where-Object {!$_.passed}).Count)"
if(@($checks|Where-Object {!$_.passed}).Count){exit 1}
