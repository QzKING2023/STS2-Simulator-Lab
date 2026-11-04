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
 Check ($id+' raw exit/build/isolation') ($code-eq$expected-and$a.rawExit-eq$expected-and$p.rawExit-eq$expected-and$p.exitConsistent-and!$p.watchdogExpired-and$p.buildId-eq$m.buildId-and$a.guards.isolation.backend-eq'MemorySaveStore'-and!$a.guards.isolation.steamInitialized-and!$a.guards.isolation.shouldSave-and!$a.guards.isolation.uiInstance) $a.result.Error
 $origin=if($expected){'fault-control'}else{'api-fixture'}
 Check ($id+' provenance') ($p.scenarioKind-eq$origin-and$a.provenance.ScenarioKind-eq$origin-and$p.executionKind-eq$(if($mode-ne'detailed'){'observation-control'}elseif($replay){'frozen-replay'}else{'live'}))
 if($expected-eq0){& dotnet $dll --review $dir *> (Join-Path $out ($id+'-review.log'));Check ($id+' production reader') ($LASTEXITCODE-eq0) (Get-Content (Join-Path $out ($id+'-review.log')) -Raw)}
 else{
  $task=$scenario-eq'merchant-task-fault';Check ($id+' prefix') ($a.result.Stop-eq$(if($task){'EngineError'}else{'InvalidAction'})-and$a.result.Dispatched-eq($a.result.Confirmed+$(if($task){1}else{0}))-and!$a.result.DataComplete-and!$a.trace.complete)
  if($task){$states=Get-Content (Join-Path $dir 'trace.jsonl')|ForEach-Object {$_|ConvertFrom-Json};$before=($states|Where-Object {$_.payload.kind-eq'dispatch'}|Select-Object -Last 1).payload.detail.state;$after=Get-Content (Join-Path $dir 'final-state.json') -Raw|ConvertFrom-Json;Check ($id+' actual retained acquisition/gold') ($after.deck.Count-eq($before.deck.Count+1)-and$after.gold-lt$before.gold)}
 }
 $processes.Add(@{id=$id;scenario=$scenario;expected=$expected;rawExit=$code;confirmed=$a.result.Confirmed;dispatched=$a.result.Dispatched;stop=$a.result.Stop})
 Write-Host "Completed $id raw=$code prefix=$($a.result.Confirmed)/$($a.result.Dispatched)";return $dir
}
function Same($name,$a,$b){foreach($f in @('route.json','choices.json','final-state.json')){Check ($name+' repeat/fullRNG '+$f) ((Get-FileHash (Join-Path $a $f)).Hash-eq(Get-FileHash (Join-Path $b $f)).Hash)}}
foreach($case in @('merchant-card','merchant-potion','merchant-full','merchant-poor','merchant-outstock','merchant-remove','merchant-cancel','merchant-repeat')){$a=Run ($case+'-1') $case;$b=Run ($case+'-2') $case 0 (Join-Path $a 'route.json');Same $case $a $b}
foreach($case in @('merchant-card','merchant-remove','merchant-full')){foreach($mode in @('minimal','off')){$a=Join-Path $out ($case+'-1');$b=Run ($case+'-'+$mode) $case 0 (Join-Path $a 'route.json') $mode;Same ($case+' '+$mode) $a $b}}
foreach($case in @('merchant-stale','merchant-wrong-inventory','merchant-task-fault')){$null=Run $case $case 1}
$rows=[Collections.Generic.List[object]]::new()
foreach($kind in @('n3d-open','n3d-return','n3d-boundary','n3d-contract')){foreach($change in @('remove','duplicate','missing-state','null-state','array-state','drift','detach')){$rows.Add(@{source='merchant-card-1';control=($change+'|'+$kind);expected=1})}}
foreach($kind in @('n3d-removal-marked','n3d-removal-hook')){foreach($change in @('remove','duplicate','missing-state','null-state','array-state','drift','detach')){$rows.Add(@{source='merchant-remove-1';control=($change+'|'+$kind);expected=1})}}
foreach($kind in @('n3d-open','n3d-return','n3d-boundary')){$rows.Add(@{source='merchant-card-1';control=('wrong-id|'+$kind);expected=1})}
foreach($c in @('false-contract','wrong-operation-count','false-success','wrong-cost','wrong-spent','wrong-completion','wrong-failure','old-acquired','missing-new-object','wrong-content','return-before-open','coherent-entry-cost','coherent-entry-extension')){$rows.Add(@{source='merchant-card-1';control=$c;expected=1})}
foreach($c in @('missing-removed','missing-used','wrong-removal-source')){$rows.Add(@{source='merchant-remove-1';control=$c;expected=1})}
foreach($source in @('merchant-card-1','merchant-remove-1','merchant-cancel-1','merchant-full-1','merchant-outstock-1')){foreach($c in @('property-order','equivalent-extension')){$rows.Add(@{source=$source;control=$c;expected=0})}}
# The reader must consume whole child choices and bind every static merchant edge.
foreach($change in @('missing-all','missing-requested','missing-resolved','missing-returned','duplicate-requested','duplicate-resolved','duplicate-returned','wrong-selected','before-open','after-mark-hook','after-return','extra-complete','wrong-source','wrong-parent','wrong-context','wrong-result-id','wrong-min','wrong-candidate-model','wrong-candidate-upgrade','wrong-candidate-instance')){$rows.Add(@{source='merchant-remove-1';control=('repair|choice|'+$change);expected=1})}
foreach($change in @('missing-all','nonempty-cancel')){$rows.Add(@{source='merchant-cancel-1';control=('repair|choice|'+$change);expected=1})}
$rows.Add(@{source='merchant-repeat-1';control='repair|choice|borrow-for-repeat';expected=1})
foreach($change in @('gold','hp','rng','extension','deck','removals')){$rows.Add(@{source='merchant-remove-1';control=('repair|phase|'+$change);expected=1})}
foreach($pair in @(@('MerchantSetup','merchant-card-1'),@('MerchantBuy','merchant-card-1'),@('MerchantBuy','merchant-potion-1'),@('MerchantBuy','merchant-full-1'),@('MerchantBuy','merchant-poor-1'),@('MerchantRemove','merchant-remove-1'),@('MerchantRemove','merchant-cancel-1'),@('MerchantExit','merchant-card-1'))){foreach($event in @('n3d-open','n3d-return','n3d-boundary')){foreach($change in @('missing','duplicate','detach','wrong-context','drift','wrong-id')){$rows.Add(@{source=$pair[1];control=('repair|'+$pair[0]+'|'+$event+'|'+$change);expected=1})}}}
foreach($kind in @('MerchantSetup','MerchantBuy','MerchantExit')){$rows.Add(@{source='merchant-card-1';control=('repair|'+$kind+'|n3d-open|extra-choice');expected=1})}
foreach($source in @('merchant-potion-1','merchant-poor-1','merchant-repeat-1')){foreach($c in @('property-order','equivalent-extension')){$rows.Add(@{source=$source;control=$c;expected=0})}}
foreach($row in $rows){
 $dir=Join-Path $out ('control-merchant-'+$row.control.Replace('|','-')+'-'+$row.source);New-Item -ItemType Directory $dir|Out-Null
 foreach($f in @('trace.jsonl','trace-seal.json','result.json','process.json')){Copy-Item (Join-Path $out ($row.source+'/'+$f)) $dir}
 & dotnet $dll --mutate $dir ('merchant-review:'+$row.control) *> (Join-Path $dir 'mutation.log');$mutation=$LASTEXITCODE
 & dotnet $dll --review $dir *> (Join-Path $dir 'review.log');$code=$LASTEXITCODE;$message=Get-Content (Join-Path $dir 'review.log') -Raw
 $ok=$mutation-eq0-and$code-eq$row.expected-and!$message.Contains('Trace chain/order/schema error');Check ($row.control+' production control') $ok $message
 $row.rawExit=$code;$row.passed=$ok;$row.message=$message
}
@{buildId=$m.buildId;count=$rows.Count;controls=@($rows);failures=@($rows|Where-Object {!$_.passed}).Count}|ConvertTo-Json -Depth 12|Set-Content (Join-Path $out 'merchant-controls.json')
@{schema='native-n3d1-suite/v1';buildId=$m.buildId;processCount=$processes.Count;processes=@($processes);checkCount=$checks.Count;checks=@($checks);failures=@($checks|Where-Object {!$_.passed}).Count;normalStartFullRun=$false}|ConvertTo-Json -Depth 12|Set-Content (Join-Path $out 'summary.json')
Write-Output "N3D1 processes=$($processes.Count) checks=$($checks.Count) failures=$(@($checks|Where-Object {!$_.passed}).Count)"
if(@($checks|Where-Object {!$_.passed}).Count){exit 1}
