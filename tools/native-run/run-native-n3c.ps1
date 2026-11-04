param([Parameter(Mandatory)][string]$BuildManifest,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'));$out=[IO.Path]::GetFullPath($OutputDirectory)
if(!$out.StartsWith((Join-Path $root '.build/runs')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or(Test-Path $out)){throw 'Fresh Lab output required'}
New-Item -ItemType Directory $out|Out-Null
$m=Get-Content $BuildManifest -Raw|ConvertFrom-Json;$dll=Join-Path (Split-Path ([IO.Path]::GetFullPath($BuildManifest))) 'bin/Release/net9.0/SimulatorLab.NativeRun.dll'
$checks=[Collections.Generic.List[object]]::new();$processes=[Collections.Generic.List[object]]::new()
function Check($name,$ok,$detail=''){$checks.Add(@{name=$name;passed=[bool]$ok;detail=$detail});if(!$ok){Write-Host "FAILED $name $detail"}}
function Run($id,$scenario,$expected=0,$replay='',$mode='detailed'){
 $dir=Join-Path $out $id
 & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'run-native-slice.ps1') -BuildManifest $BuildManifest -OutputDirectory $dir -Scenario $scenario -ReplayPath $replay -Observation $mode *> (Join-Path $out ($id+'.log'))
 $code=$LASTEXITCODE;$a=Get-Content (Join-Path $dir 'result.json') -Raw|ConvertFrom-Json;$p=Get-Content (Join-Path $dir 'process.json') -Raw|ConvertFrom-Json
 Check ($id+' raw exit/build') ($code-eq$expected-and$a.rawExit-eq$expected-and$p.rawExit-eq$expected-and$p.exitConsistent-and!$p.watchdogExpired-and$p.buildId-eq$m.buildId) $a.result.Error
 $origin=if($scenario-eq'rewards-natural'){'natural-run'}elseif($scenario-in@('reward-missing','reward-stale','reward-cross-set','reward-invalid','reward-extra','reward-duplicate','reward-task-fault','reward-potion-stale','reward-potion-wrong-target','reward-potion-dead-target','reward-potion-timing','reward-potion-effect-fault','reward-required-skip')){'fault-control'}else{'api-fixture'}
 Check ($id+' origin/execution') ($p.scenarioKind-eq$origin-and$a.provenance.ScenarioKind-eq$origin-and$p.executionKind-eq$(if($mode-ne'detailed'){'observation-control'}elseif($replay){'frozen-replay'}else{'live'}))
 $processes.Add(@{id=$id;scenario=$scenario;expected=$expected;rawExit=$code;confirmed=$a.result.Confirmed;dispatched=$a.result.Dispatched;terminal=$a.result.Terminal;stop=$a.result.Stop})
 if($expected-eq0){& dotnet $dll --review $dir *> (Join-Path $out ($id+'-review.log'));Check ($id+' actual production reader') ($LASTEXITCODE-eq0) (Get-Content (Join-Path $out ($id+'-review.log')) -Raw)}
 else{
  $stop=if($scenario-in@('reward-task-fault','reward-potion-effect-fault')){'EngineError'}else{'InvalidAction'}
  $gap=if($scenario-in@('reward-potion-stale','reward-potion-wrong-target','reward-potion-dead-target','reward-potion-timing')){0}else{1}
  Check ($id+' failed prefix/classification') ($a.result.Stop-eq$stop-and$a.result.Dispatched-eq($a.result.Confirmed+$gap)-and!$a.result.DataComplete-and!$a.trace.complete)
 }
 Write-Host "Completed $id raw=$code prefix=$($a.result.Confirmed)/$($a.result.Dispatched) $($a.result.Stop)"
 return $dir
}
function Same($name,$a,$b){foreach($f in @('route.json','choices.json','reward-choices.json','final-state.json')){Check ($name+' repeat/fullRNG '+$f) ((Get-FileHash (Join-Path $a $f)).Hash-eq(Get-FileHash (Join-Path $b $f)).Hash)}}
$positive=@('reward-gold','reward-zero','reward-empty','reward-skip','reward-required','reward-card','reward-card-skip','reward-consecutive','reward-nested','reward-capacity','reward-relic','reward-fire','reward-block','reward-treasure','reward-treasure-skip','rewards-natural')
foreach($case in $positive){$a=Run ($case+'-1') $case;$b=Run ($case+'-2') $case 0 (Join-Path $a 'route.json');Same $case $a $b}
foreach($case in @('reward-gold','reward-nested','reward-treasure','reward-block','rewards-natural')){foreach($mode in @('minimal','off')){$a=Join-Path $out ($case+'-1');$b=Run ($case+'-'+$mode) $case 0 (Join-Path $a 'route.json') $mode;Same ($case+' '+$mode) $a $b}}
foreach($case in @('reward-missing','reward-stale','reward-cross-set','reward-invalid','reward-extra','reward-duplicate','reward-task-fault','reward-potion-stale','reward-potion-wrong-target','reward-potion-dead-target','reward-potion-timing','reward-potion-effect-fault','reward-required-skip')){$null=Run $case $case 1}
$rows=[Collections.Generic.List[object]]::new()
$kinds=@('n3c-open','n3c-answer','n3c-start','n3c-return','n3c-boundary','n3c-set','n3c-set-completed','n3c-assertion','n3c-contract','n3c-card-continuity')
foreach($kind in $kinds){
 $source=if($kind-eq'n3c-card-continuity'){'reward-card-1'}else{'reward-gold-1'}
 foreach($op in @('missing-state','null-state','array-state','remove','duplicate','drift','detach')){$rows.Add(@{source=$source;control=($op+'|'+$kind);expected=1})}
}
foreach($control in @('false-contract','wrong-answer','wrong-child-subject','coherent-gold','wrong-set-id','wrong-reward-kind','incomplete-candidates')){$rows.Add(@{source='reward-gold-1';control=$control;expected=1})}
$rows.Add(@{source='reward-relic-1';control='wrong-acquired';expected=1})
$rows.Add(@{source='reward-relic-1';control='wrong-content-model';expected=1})
foreach($op in @('treasure-normal','treasure-extra','treasure-obtain','treasure-exit')){$rows.Add(@{source='reward-treasure-1';control=('omit-operation|'+$op);expected=1})}
foreach($control in @('property-order','equivalent-extension')){$rows.Add(@{source='rewards-natural-1';control=$control;expected=0})}
foreach($control in @('repair-set-return-gold','repair-set-return-rng','repair-set-return-extension','repair-item-open-drift','repair-item-open-extension','repair-false-set','repair-false-root','repair-missing-set-complete','repair-null-set-complete','repair-wrongtype-set-complete')){$rows.Add(@{source='reward-gold-1';control=$control;expected=1})}
foreach($control in @('repair-child-before-start','repair-unknown-kind','repair-menu-upgrade','repair-menu-model','repair-source-upgrade','repair-acquired-upgrade','repair-missing-source','repair-coherent-upgrade','repair-menu-success')){$rows.Add(@{source='reward-card-1';control=$control;expected=1})}
foreach($control in @('repair-missing-menu','repair-duplicate-menu')){$rows.Add(@{source='reward-card-skip-1';control=$control;expected=1})}
foreach($control in @('repair-alternative-completion','repair-missing-alternatives','repair-alternative-source')){$rows.Add(@{source='reward-card-skip-1';control=$control;expected=1})}
foreach($control in @('repair-old-relic','repair-old-created-relic','repair-wrong-relic-model','repair-missing-created','repair-gold-negative','repair-gold-positive','repair-gold-delta','repair-gold-null')){$rows.Add(@{source='reward-treasure-1';control=$control;expected=1})}
foreach($entry in @(@('rewards-natural-1',@('EventOption','NativeTravel')), @('reward-gold-1',@('RewardOffer','reward-set','reward-item')), @('reward-card-1',@('card-menu')), @('reward-block-1',@('RewardSetup','UsePotion','potion-use')), @('reward-treasure-1',@('TreasureOpen','treasure-normal','treasure-extra','TreasurePick','treasure-choice','treasure-obtain','TreasureExit','treasure-exit')))){
 foreach($kind in $entry[1]){foreach($op in @('omit-tree','missing-start','duplicate-start','duplicate-return','wrong-owner-return','wrong-parent','return-before-start')){$rows.Add(@{source=$entry[0];control=('lifecycle|'+$kind+'|'+$op);expected=1})}}
}
foreach($entry in @(@('reward-block-1',@('potion-use')), @('reward-treasure-1',@('treasure-normal','treasure-extra','treasure-choice','treasure-exit')), @('reward-treasure-skip-1',@('treasure-choice')))){
 foreach($kind in $entry[1]){foreach($edge in @('entry','return')){foreach($change in @('gold','rng','extension')){$rows.Add(@{source=$entry[0];control=('static-edge|'+$kind+'|'+$edge+'|'+$change);expected=1})}}}
}
foreach($mode in @('old','null','missing','wrong-source')){$rows.Add(@{source='reward-card-1';control=('outer-acquired|'+$mode);expected=1})}
foreach($source in @('reward-card-skip-1','reward-gold-1')){foreach($mode in @('old','wrong-source')){$rows.Add(@{source=$source;control=('outer-acquired|'+$mode);expected=1})}}
foreach($source in @('reward-treasure-1','reward-treasure-skip-1','reward-card-1','reward-card-skip-1','reward-block-1','reward-nested-1')){foreach($control in @('property-order','equivalent-extension')){$rows.Add(@{source=$source;control=$control;expected=0})}}
foreach($row in $rows){
 $dir=Join-Path $out ('control-n3c-'+$row.control.Replace('|','-')+'-'+$row.source);New-Item -ItemType Directory $dir|Out-Null
 foreach($f in @('trace.jsonl','trace-seal.json','result.json','process.json')){Copy-Item (Join-Path $out ($row.source+'/'+$f)) $dir}
 & dotnet $dll --mutate $dir ('n3c-review:'+$row.control) *> (Join-Path $dir 'mutation.log');$mutation=$LASTEXITCODE
 & dotnet $dll --review $dir *> (Join-Path $dir 'review.log');$raw=$LASTEXITCODE;$message=Get-Content (Join-Path $dir 'review.log') -Raw
 $ok=$mutation-eq0-and$raw-eq$row.expected-and!$message.Contains('Trace chain/order/schema error')-and($row.control-ne'coherent-gold'-or$message.Contains('Independent custom gold 7 contract'))
 Check ($row.control+' actual production gate') $ok $message
 $row.rawExit=$raw;$row.passed=$ok;$row.message=$message
}
@{buildId=$m.buildId;count=$rows.Count;controls=@($rows);failures=@($rows|Where-Object {!$_.passed}).Count}|ConvertTo-Json -Depth 12|Set-Content (Join-Path $out 'reward-controls.json')
@{schema='native-n3c-suite/v1';buildId=$m.buildId;processCount=$processes.Count;processes=@($processes);checkCount=$checks.Count;checks=@($checks);failures=@($checks|Where-Object {!$_.passed}).Count;normalStartFullRun=$false}|ConvertTo-Json -Depth 12|Set-Content (Join-Path $out 'summary.json')
Write-Output "N3C processes=$($processes.Count) checks=$($checks.Count) failures=$(@($checks|Where-Object {!$_.passed}).Count)"
if(@($checks|Where-Object {!$_.passed}).Count){exit 1}
