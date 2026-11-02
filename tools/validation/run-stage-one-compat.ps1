param([Parameter(Mandatory)][string]$HarnessRoot,[Parameter(Mandatory)][string]$OutputDirectory,[ValidateSet('Vanilla','EleanorVictory')][string]$Mode='Vanilla',[string]$PinnedPlan='')
$ErrorActionPreference='Stop';$lab=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'));$h=[IO.Path]::GetFullPath($HarnessRoot);$out=[IO.Path]::GetFullPath($OutputDirectory)
if(-not$out.StartsWith((Join-Path $lab '.build/runs')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Use Lab runs.'}
New-Item -ItemType Directory -Path $out -ErrorAction Stop|Out-Null
$saved=@{};Get-ChildItem Env:SIMULATORLAB_*|ForEach-Object{$saved[$_.Name]=$_.Value;[Environment]::SetEnvironmentVariable($_.Name,$null)}
$dll=Join-Path $h 'bin/Release/net9.0/OfflineSearchHarness.dll';$rows=@()
try{
 if($Mode-eq'EleanorVictory'){
  if(-not(Test-Path $PinnedPlan)){throw 'Provide the retained original route.'}
  $pin=Join-Path $lab '.build/pinned-inputs/eleanor-v0.4.1-forms-cardpool.fix'
  $env:SIMULATORLAB_ELEANOR_MOD='1';$env:SIMULATORLAB_ELEANOR_MOD_DLL=Join-Path $pin 'MyMod.dll';$env:SIMULATORLAB_ELEANOR_BASELIB_DLL=Join-Path $pin 'dependencies/BaseLib.dll';$env:SIMULATORLAB_ELEANOR_MANIFEST=Join-Path $pin 'MyMod.json';$env:SIMULATORLAB_ELEANOR_MOD_VERSION='v0.4.1-forms-cardpool.fix';$env:SIMULATORLAB_ELEANOR_V040_PROBE='1';$env:SIMULATORLAB_ELEANOR_V040_FORM='Reverse';$env:SIMULATORLAB_ELEANOR_V041_VICTORY_REPLAY='1';$env:SIMULATORLAB_ELEANOR_V041_PLAN_PATH=[IO.Path]::GetFullPath($PinnedPlan)
  & dotnet $dll --character MyMod --encounter FUZZY_WURM_CRAWLER_WEAK --seed LAB_V0111_ELEANOR_V040_FORM_01 --profile Low --beam 8 --nodes 1000 --dop 1 --budget-ms 5000 --search-mode Evaluate --milestone M2 --out (Join-Path $out 'reverse') *> (Join-Path $out 'console.log')
  if($LASTEXITCODE-ne0){throw "Retained Victory failed: $out"}
  & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'TestEleanorVictoryReplayEvidence.ps1') -ReportPath (Join-Path $out 'reverse/eleanor-v041-fixed-route-victory-replay.json')
  if($LASTEXITCODE-ne0){throw 'Victory artifact checks failed.'}
 }else{
  foreach($kind in @('battle','rng')){foreach($repeat in 1..2){
   $env:SIMULATORLAB_NATIVE_BATTLE_TRACE=if($kind-eq'battle'){'1'}else{'0'};$env:SIMULATORLAB_NATIVE_RUNLOOP=$env:SIMULATORLAB_NATIVE_BATTLE_TRACE;$env:SIMULATORLAB_NATIVE_RANDOM_TARGET=if($kind-eq'rng'){'1'}else{'0'}
   $encounter=if($kind-eq'rng'){'CULTISTS_NORMAL'}else{'FUZZY_WURM_CRAWLER_WEAK'};$seed=if($kind-eq'rng'){'LAB_V0111_IRONCLAD_TARGETS_01'}else{'LAB_V0111_IRONCLAD_M1_01'};$dir=Join-Path $out "$kind-$repeat"
   & dotnet $dll --character IRONCLAD --encounter $encounter --seed $seed --profile Low --beam 8 --nodes 1000 --dop 1 --budget-ms 5000 --search-mode Evaluate --milestone M2 --out $dir *> (Join-Path $out "$kind-$repeat.log")
   $code=$LASTEXITCODE;$rows+=@{kind=$kind;repeat=$repeat;exit=$code;path=$dir};if($code-ne0){throw "Vanilla $kind failed: $dir"}
  }}
 }
 [ordered]@{mode=$Mode;status='passed';harnessSha256=(Get-FileHash $dll).Hash;cases=$rows}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $out 'compat-summary.json') -Encoding utf8
}finally{Get-ChildItem Env:SIMULATORLAB_*|ForEach-Object{[Environment]::SetEnvironmentVariable($_.Name,$null)};foreach($k in $saved.Keys){[Environment]::SetEnvironmentVariable($k,$saved[$k])}}
