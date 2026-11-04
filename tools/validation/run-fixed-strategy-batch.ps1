param([Parameter(Mandatory)][string]$HarnessRoot,[string]$GamePath=$env:STS2_GAME_PATH,[string]$OutputDirectory='',[string]$ReplayPlansFrom='',[ValidateSet('legacy-v1','reward-v2')][string]$CoverageProfile='legacy-v1')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'ClassifyBatchResults.ps1')
$lab=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$harness=[IO.Path]::GetFullPath($HarnessRoot)
if(-not$harness.StartsWith((Join-Path $lab '.build\local-reference')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Harness must remain inside the Lab cache.'}
$dll=Join-Path $harness 'bin\Release\net9.0\OfflineSearchHarness.dll'
foreach($name in @('EleanorV040Probe.cs','ManaShieldBoundaryProbe.cs','NativeModRunLoop.cs','NativeModProgression.cs','NativeSecondBattle.cs','NativeCombatDecision.cs','VanillaCombatRootAdapter.cs')){
    if((Get-FileHash (Join-Path $harness $name)).Hash-ne(Get-FileHash (Join-Path $PSScriptRoot $name)).Hash){throw "Stale harness source: $name"}
    if((Get-Item (Join-Path $harness $name)).LastWriteTimeUtc-gt(Get-Item $dll).LastWriteTimeUtc){throw 'Harness binary predates source.'}
}
$pin=Join-Path $lab '.build\pinned-inputs\eleanor-v0.4.1-forms-cardpool.fix'
foreach($file in (Get-Content (Join-Path $pin 'input-lock.json') -Raw|ConvertFrom-Json).files){if((Get-FileHash (Join-Path $pin $file.relativePath)).Hash-ne$file.sha256){throw 'Pinned input changed.'}}
if((Get-FileHash (Join-Path $GamePath 'data_sts2_windows_x86_64\sts2.dll')).Hash-ne'0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9'){throw 'Game hash mismatch.'}
$profile=Join-Path $lab '.build\pinned-inputs\eleanor-v041-native-bootstrap'
foreach($file in (Get-Content (Join-Path $profile 'input-lock.json') -Raw|ConvertFrom-Json).files){if((Get-FileHash $file.path).Hash-ne$file.sha256){throw 'Bootstrap input changed.'}}
$declaration=Join-Path $lab 'tests\fixtures\fixed-strategy-batch.json'
if($CoverageProfile-eq'reward-v2'){$declaration=Join-Path $lab 'tests/fixtures/reward-card-batch.json'}
$spec=Get-Content $declaration -Raw|ConvertFrom-Json
if($spec.schema-notin@('fixed-strategy-batch/v1','fixed-strategy-batch/v2')-or$spec.seeds.Count-ne3-or$spec.policies.Count-ne2-or$spec.repeats-ne2){throw 'Expected the declared 3 x 2 x 2 matrix.'}
if(-not$OutputDirectory){$OutputDirectory=Join-Path $lab ('.build\runs\fixed-strategy-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(-not$OutputDirectory.StartsWith((Join-Path $lab '.build\runs')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Reports must stay in Lab runs.'}
New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
if(Test-Path (Join-Path $OutputDirectory 'declaration.json')){throw 'Use a new output directory; do not overwrite earlier trials.'}
Copy-Item $declaration (Join-Path $OutputDirectory 'declaration.json')
if($ReplayPlansFrom){
    $ReplayPlansFrom=[IO.Path]::GetFullPath($ReplayPlansFrom)
    if(-not$ReplayPlansFrom.StartsWith((Join-Path $lab '.build\runs')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Replay cohort must stay in Lab runs.'}
    if((Get-FileHash (Join-Path $ReplayPlansFrom 'declaration.json')).Hash-ne(Get-FileHash $declaration).Hash){throw 'Replay declaration differs from this fixed cohort.'}
}
$vars=@{SIMULATORLAB_ELEANOR_MOD='1';SIMULATORLAB_ELEANOR_MOD_DLL=(Join-Path $pin 'MyMod.dll');SIMULATORLAB_ELEANOR_BASELIB_DLL=(Join-Path $pin 'dependencies\BaseLib.dll');SIMULATORLAB_ELEANOR_MANIFEST=(Join-Path $pin 'MyMod.json');SIMULATORLAB_ELEANOR_MOD_VERSION='v0.4.1-forms-cardpool.fix';SIMULATORLAB_ELEANOR_V040_PROBE='1';SIMULATORLAB_ELEANOR_V040_FORM='Reverse';SIMULATORLAB_NATIVE_PROFILE=$profile;SIMULATORLAB_NATIVE_LOOP_STAGE='Loop';SIMULATORLAB_NATIVE_REWARD_POLICY='skip';SIMULATORLAB_NATIVE_DECISION_MODE='coverage-plan';SIMULATORLAB_NATIVE_RECORD_FIXTURE='0';SIMULATORLAB_NATIVE_SECOND_BATTLE='1';SIMULATORLAB_NATIVE_FIXED_PLAN='';SIMULATORLAB_NATIVE_FIXED_PLAN_SHA='';SIMULATORLAB_NATIVE_SECOND_PLAN='';SIMULATORLAB_NATIVE_SECOND_PLAN_SHA='';SIMULATORLAB_SHIELD_FIXTURE='0'}
$previous=@{};foreach($key in $vars.Keys){$previous[$key]=[Environment]::GetEnvironmentVariable($key);[Environment]::SetEnvironmentVariable($key,$vars[$key])}
$cases=@();$checks=@();$reports=@{}
try{
    for($seedIndex=0;$seedIndex-lt$spec.seeds.Count;$seedIndex++){
        $seed=[string]$spec.seeds[$seedIndex]
        foreach($policy in $spec.policies){
            $env:SIMULATORLAB_NATIVE_FIXED_PLAN='';$env:SIMULATORLAB_NATIVE_FIXED_PLAN_SHA='';$env:SIMULATORLAB_NATIVE_SECOND_PLAN='';$env:SIMULATORLAB_NATIVE_SECOND_PLAN_SHA=''
            $env:SIMULATORLAB_NATIVE_REWARD_POLICY=$policy
            if($ReplayPlansFrom){
                $source=Join-Path $ReplayPlansFrom "seed$($seedIndex+1)-$policy-a"
                $first=Join-Path $source 'battle\route.json';$second=Join-Path $source 'second-battle\chunk-plan.json'
                if(Test-Path $first){$env:SIMULATORLAB_NATIVE_FIXED_PLAN=$first;$env:SIMULATORLAB_NATIVE_FIXED_PLAN_SHA=(Get-FileHash $first).Hash}
                if(Test-Path $second){$env:SIMULATORLAB_NATIVE_SECOND_PLAN=$second;$env:SIMULATORLAB_NATIVE_SECOND_PLAN_SHA=(Get-FileHash $second).Hash}
            }
            foreach($repeat in 'a','b'){
                $id="seed$($seedIndex+1)-$policy-$repeat";$out=Join-Path $OutputDirectory $id
                & dotnet $dll --character MyMod --encounter FUZZY_WURM_CRAWLER_WEAK --seed $seed --profile $spec.profile --beam $spec.beam --nodes $spec.nodes --dop $spec.dop --budget-ms $spec.budgetMilliseconds --search-mode Evaluate --milestone M2 --out $out *>&1|Set-Content (Join-Path $OutputDirectory "$id.log")
                $code=$LASTEXITCODE;$path=Join-Path $out 'native-run-loop.json'
                $r=if(Test-Path $path){Get-Content $path -Raw|ConvertFrom-Json}else{$null};$reports[$id]=$r
                $status=if($r){$r.status}else{'failed'}
                $cases+=[ordered]@{id=$id;seed=$seed;policy=$policy;repeat=$repeat;exit=$code;status=$status;phase=$r.phase;reason=$r.reason;report=$path;nativeHp=if($r.lifecycle){$r.lifecycle[-1].hp}else{$null};nativeDeathObserved=($r.battle.nativeDeath-eq$true-or$r.secondBattle.terminal-eq'Death')}
                if($repeat-eq'a'){
                    $first=Join-Path $out 'battle\route.json';$second=Join-Path $out 'second-battle\chunk-plan.json'
                    if(Test-Path $first){$env:SIMULATORLAB_NATIVE_FIXED_PLAN=$first;$env:SIMULATORLAB_NATIVE_FIXED_PLAN_SHA=(Get-FileHash $first).Hash}
                    if(Test-Path $second){$env:SIMULATORLAB_NATIVE_SECOND_PLAN=$second;$env:SIMULATORLAB_NATIVE_SECOND_PLAN_SHA=(Get-FileHash $second).Hash}
                }
                Write-Output "$id exit=$code status=$status phase=$($r.phase)"
            }
        }
    }
}finally{foreach($key in $previous.Keys){[Environment]::SetEnvironmentVariable($key,$previous[$key])}}
foreach($case in $cases){
    $r=$reports[$case.id]
    $steps=@($r.battle.steps)+@($r.secondBattle.chunks|ForEach-Object{$_.steps})
    $bad=@($steps|Where-Object{$null-ne$_-and(-not$_.exactStateMatch-or-not$_.parentBranchUnchanged-or$_.nativeContinuation-cne$_.simulatorContinuation)})
    $checks+=[ordered]@{name="$($case.id) recorded actions exact and isolated";passed=($null-ne$r-and$steps.Count-gt0-and$bad.Count-eq0-and$r.sourceSeed-eq$case.seed)}
    $checks+=[ordered]@{name="$($case.id) offline guards and discovery";passed=($r.offlineGuards.saveStore-eq'MemorySaveStore'-and$r.offlineGuards.testMode-and-not$r.offlineGuards.steamInitialized-and-not$r.offlineGuards.persistentWritesAllowed-and$r.pool.lateActualHarmonyPatchRejected)}
}
for($i=1;$i-le3;$i++){foreach($policy in $spec.policies){
    $a=$reports["seed$i-$policy-a"];$b=$reports["seed$i-$policy-b"]
    $checks+=[ordered]@{name="seed$i-$policy repeated outcome/lifecycle/rewards/actions";passed=($null-ne$a-and$null-ne$b-and$a.status-eq$b.status-and$a.phase-eq$b.phase-and($a.lifecycle|ConvertTo-Json -Depth 100 -Compress)-ceq($b.lifecycle|ConvertTo-Json -Depth 100 -Compress)-and($a.rewards|ConvertTo-Json -Depth 100 -Compress)-ceq($b.rewards|ConvertTo-Json -Depth 100 -Compress)-and($a.battle.steps|ConvertTo-Json -Depth 100 -Compress)-ceq($b.battle.steps|ConvertTo-Json -Depth 100 -Compress)-and($a.secondBattle.trace|ConvertTo-Json -Depth 100 -Compress)-ceq($b.secondBattle.trace|ConvertTo-Json -Depth 100 -Compress))}
}}
$expectedIds=@(foreach($i in 1..3){foreach($policy in $spec.policies){foreach($rep in 'a','b'){"seed$i-$policy-$rep"}}})
$gaps=Get-Content (Join-Path $lab 'tests/fixtures/stage-one-known-gaps.json') -Raw|ConvertFrom-Json
if($CoverageProfile-eq'reward-v2'){$gaps=@()}
$classification=Get-BatchClassification $cases $reports $expectedIds $gaps $checks
$summary=[ordered]@{schema='fixed-strategy-results/v2';status=$classification.status;correctnessChecksPassed=$classification.correctnessChecksPassed;classification=$classification;counts=$classification.counts;replayPlansFrom=$ReplayPlansFrom;declarationSha256=(Get-FileHash $declaration).Hash;harnessSha256=(Get-FileHash $dll).Hash;cases=$cases;checks=$checks;interpretation='Finite declared fixture set, no win-rate estimate. Budget stops are incomplete, execution faults fail, only declared gaps permit exit 2. Death requires actual matched terminal evidence.'}
$summary|ConvertTo-Json -Depth 10|Set-Content (Join-Path $OutputDirectory 'summary.json') -Encoding utf8
Write-Output "Evidence: $OutputDirectory; status=$($summary.status)"
exit $classification.exitCode
