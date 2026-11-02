param([Parameter(Mandatory)][string]$HarnessRoot,[string]$OutputDirectory='',[string]$GamePath=$env:STS2_GAME_PATH)
$ErrorActionPreference='Stop'
$labRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$harness=[IO.Path]::GetFullPath($HarnessRoot)
if (-not $harness.StartsWith((Join-Path $labRoot '.build\local-reference')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Harness must be an isolated Lab build.' }
$dll=Join-Path $harness 'bin\Release\net9.0\OfflineSearchHarness.dll'
foreach ($name in @('EleanorV040Probe.cs','EleanorModBootstrap.cs','NativeModRunLoop.cs','NativeModProgression.cs','NativeSecondBattle.cs','NativeCombatDecision.cs','VanillaCombatRootAdapter.cs')) {
    if ((Get-FileHash (Join-Path $harness $name)).Hash -ne (Get-FileHash (Join-Path $PSScriptRoot $name)).Hash) { throw "Harness source stale: $name; build a fresh cache." }
    if ((Get-Item (Join-Path $harness $name)).LastWriteTimeUtc -gt (Get-Item $dll).LastWriteTimeUtc) { throw 'Harness DLL predates its source.' }
}
$pin=Join-Path $labRoot '.build\pinned-inputs\eleanor-v0.4.1-forms-cardpool.fix'
$lock=Get-Content (Join-Path $pin 'input-lock.json') -Raw | ConvertFrom-Json
foreach ($f in $lock.files) { if ((Get-FileHash (Join-Path $pin $f.relativePath)).Hash -ne $f.sha256) { throw "Pinned input changed: $($f.name)" } }
if ([string]::IsNullOrWhiteSpace($GamePath)) { throw 'Provide -GamePath or STS2_GAME_PATH.' }
$gameDll=Join-Path $GamePath 'data_sts2_windows_x86_64\sts2.dll'
if ((Get-FileHash $gameDll).Hash -ne '0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9') { throw 'Game hash mismatch.' }
$profile=Join-Path $labRoot '.build\pinned-inputs\eleanor-v041-native-bootstrap'
$profileLock=Get-Content (Join-Path $profile 'input-lock.json') -Raw | ConvertFrom-Json
foreach ($f in $profileLock.files) { if ((Get-FileHash $f.path).Hash -ne $f.sha256) { throw 'Bootstrap manifest changed.' } }
if (-not $OutputDirectory) { $OutputDirectory=Join-Path $labRoot ('.build\runs\online-decisions-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')) }
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if (-not $OutputDirectory.StartsWith((Join-Path $labRoot '.build\runs')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence must stay in Lab runs.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
if(Test-Path (Join-Path $OutputDirectory 'online-a')){throw 'Use a fresh output directory; earlier decision evidence must remain intact.'}
$variables=@{
SIMULATORLAB_ELEANOR_MOD='1';SIMULATORLAB_ELEANOR_MOD_DLL=(Join-Path $pin 'MyMod.dll');SIMULATORLAB_ELEANOR_BASELIB_DLL=(Join-Path $pin 'dependencies\BaseLib.dll');
SIMULATORLAB_ELEANOR_MANIFEST=(Join-Path $pin 'MyMod.json');SIMULATORLAB_ELEANOR_MOD_VERSION='v0.4.1-forms-cardpool.fix';
SIMULATORLAB_ELEANOR_V040_PROBE='1';SIMULATORLAB_ELEANOR_V040_FORM='Reverse';SIMULATORLAB_NATIVE_PROFILE=$profile;SIMULATORLAB_NATIVE_LOOP_STAGE='Loop';SIMULATORLAB_NATIVE_REWARD_POLICY='skip';
SIMULATORLAB_NATIVE_RECORD_FIXTURE='0';SIMULATORLAB_NATIVE_FIXED_PLAN='';SIMULATORLAB_NATIVE_FIXED_PLAN_SHA='';SIMULATORLAB_NATIVE_SECOND_BATTLE='1';SIMULATORLAB_NATIVE_SECOND_PLAN='';SIMULATORLAB_NATIVE_SECOND_PLAN_SHA='';
SIMULATORLAB_NATIVE_DECISION_MODE='online-one-action';SIMULATORLAB_SHIELD_FIXTURE='0';SIMULATORLAB_STAGE_ONE_QUERY_FAULT_AFTER=''
}
$previous=@{};foreach($key in $variables.Keys){$previous[$key]=[Environment]::GetEnvironmentVariable($key);[Environment]::SetEnvironmentVariable($key,$variables[$key])}
$cases=@();$reports=@{}
try{
    foreach($name in 'online-a','online-b','frozen-a','fault-a','fault-b'){
        $env:SIMULATORLAB_STAGE_ONE_QUERY_FAULT_AFTER=if($name-like'fault-*'){'2'}else{''}
        if($name-like'fault-*'){$env:SIMULATORLAB_NATIVE_SECOND_PLAN='';$env:SIMULATORLAB_NATIVE_SECOND_PLAN_SHA=''}
        $env:SIMULATORLAB_NATIVE_DECISION_MODE=if($name-eq'frozen-a'){'frozen-one-action'}else{'online-one-action'}
        if($name-eq'frozen-a'){
            $env:SIMULATORLAB_NATIVE_SECOND_PLAN=Join-Path $OutputDirectory 'online-a\second-battle\chunk-plan.json'
            if(-not(Test-Path $env:SIMULATORLAB_NATIVE_SECOND_PLAN)){
                $cases+=[ordered]@{name=$name;exit=$null;status='not-run';phase='plan-input';reason='First online execution emitted no second-battle plan.';report=$null}
                continue
            }
            $env:SIMULATORLAB_NATIVE_SECOND_PLAN_SHA=(Get-FileHash $env:SIMULATORLAB_NATIVE_SECOND_PLAN).Hash
        }
        $out=Join-Path $OutputDirectory $name
        & dotnet $dll --character MyMod --encounter FUZZY_WURM_CRAWLER_WEAK --seed LAB_V0111_ELEANOR_V040_FORM_01 --profile Low --beam 8 --nodes 1000 --dop 1 --budget-ms 5000 --search-mode Evaluate --milestone M2 --out $out *>&1|Set-Content (Join-Path $OutputDirectory "$name.log")
        $code=$LASTEXITCODE;$path=Join-Path $out 'native-run-loop.json'
        $r=if(Test-Path $path){Get-Content $path -Raw|ConvertFrom-Json}else{$null};$reports[$name]=$r
        $cases+=[ordered]@{name=$name;exit=$code;expectedExit=if($name-like'fault-*'){1}else{0};status=if($r){$r.status}else{'not-run'};phase=$r.phase;reason=$r.reason;report=$path}
        if($name-eq'online-a'){
            $first=Join-Path $out 'battle\route.json'
            if(Test-Path $first){$env:SIMULATORLAB_NATIVE_FIXED_PLAN=$first;$env:SIMULATORLAB_NATIVE_FIXED_PLAN_SHA=(Get-FileHash $first).Hash}
        }
        Write-Output "$name exit=$code status=$($r.status) queries=$($r.secondBattle.decisions.Count)"
    }
}finally{foreach($key in $previous.Keys){[Environment]::SetEnvironmentVariable($key,$previous[$key])}}
$checks=@()
foreach($case in $cases){
    $r=$reports[$case.name];$second=$r.secondBattle
    $steps=@($second.chunks|ForEach-Object{$_.steps})
    $bad=@($steps|Where-Object{-not$_.exactStateMatch-or-not$_.parentBranchUnchanged-or$_.nativeContinuation-cne$_.simulatorContinuation})
    $confirmedFirst=$r.battle.outcome.ConfirmedActions
    $checks+=[ordered]@{name="$($case.name) cumulative confirmed counts retain scope";passed=($second.outcome.ConfirmedActions-eq$steps.Count-and$r.outcome.ConfirmedActions-eq$confirmedFirst+$steps.Count)}
    if($case.name-like'fault-*'){
        $checks+=[ordered]@{name="$($case.name) exact injected query failure retains two actual actions";passed=($case.exit-eq1-and$r.status-eq'engine-error'-and$second.status-eq'engine-error'-and$steps.Count-eq2-and$bad.Count-eq0-and$second.failure.stage-eq'before-query'-and$second.failure.error-like'*stage-one-injected-query-fault-after-confirmed-prefix*')}
        $checks+=[ordered]@{name="$($case.name) last confirmed full boundary retained";passed=($second.lastConfirmedBoundary.nativeContinuation-ceq$steps[-1].nativeContinuation-and$second.lastConfirmedBoundary.simulatorContinuation-ceq$steps[-1].simulatorContinuation-and($second.lastConfirmedBoundary.nativeRoot|ConvertTo-Json -Depth 100 -Compress)-ceq($steps[-1].nativeRoot|ConvertTo-Json -Depth 100 -Compress))}
        $checks+=[ordered]@{name="$($case.name) uncertain current outcome not inferred from native";passed=($second.outcome.NativeOutcome-eq0-and$second.outcome.SimulatorOutcome-eq0-and-not$second.outcome.VerificationPassed-and$second.outcome.StopReason-eq4-and$second.outcome.ExecutionState-eq'OutcomeUncertain')}
        continue
    }
    $checks+=[ordered]@{name="$($case.name) actual second Victory and exact isolated steps";passed=($r.status-eq'passed'-and$second.status-eq'passed'-and$second.terminal-eq'Victory'-and$steps.Count-gt1-and$bad.Count-eq0-and$second.chunks[-1].nativeCombatWonEvents-eq1)}
    $checks+=[ordered]@{name="$($case.name) exactly one action per boundary";passed=($second.chunks.Count-eq$steps.Count-and@($second.chunks|Where-Object{$_.steps.Count-ne1-or$_.planActionCount-ne1}).Count-eq0)}
    $checks+=[ordered]@{name="$($case.name) native persistence isolation";passed=($r.offlineGuards.saveStore-eq'MemorySaveStore'-and$r.offlineGuards.testMode-and-not$r.offlineGuards.steamInitialized-and-not$r.offlineGuards.persistentWritesAllowed-and$r.offlineGuards.interceptedReplayWrites-eq2-and$r.offlineGuards.interceptedProgressSaves-eq4)}
    if($case.name-ne'frozen-a'){
        $badQueries=@($second.decisions|Where-Object{-not$_.query.returnedSourceMatches-or-not$_.query.nativeSourceUnchangedByQuery-or-not$_.duplicateTicketRejected-or$_.executedActionCount-ne1-or$_.steps-ne1})
        $checks+=[ordered]@{name="$($case.name) fresh queries and single-use tickets";passed=($second.decisionMode-eq'online-one-action'-and$second.decisions.Count-eq$steps.Count-and$badQueries.Count-eq0)}
        $chainValid=$true
        for($i=1;$i-lt$second.decisions.Count;$i++){if($second.decisions[$i].query.sourceContinuation-cne$second.chunks[$i-1].steps[-1].nativeContinuation){$chainValid=$false}}
        $checks+=[ordered]@{name="$($case.name) requery actual previous action boundary";passed=($second.decisions.Count-gt1-and$chainValid)}
    }
}
$a=$reports['online-a'];$b=$reports['online-b'];$f=$reports['frozen-a']
$checks+=[ordered]@{name='online independently requeries repeated native input';passed=(($a.secondBattle.trace|ConvertTo-Json -Depth 100 -Compress)-ceq($b.secondBattle.trace|ConvertTo-Json -Depth 100 -Compress)-and($a.lifecycle|ConvertTo-Json -Depth 100 -Compress)-ceq($b.lifecycle|ConvertTo-Json -Depth 100 -Compress))}
$checks+=[ordered]@{name='frozen mode replays online actions with zero decision queries';passed=($f.secondBattle.decisionMode-eq'frozen-one-action'-and$f.secondBattle.decisions.Count-eq0-and($a.secondBattle.trace|ConvertTo-Json -Depth 100 -Compress)-ceq($f.secondBattle.trace|ConvertTo-Json -Depth 100 -Compress)-and$a.secondBattle.planSha256-eq$f.secondBattle.planSha256)}
$fa=$reports['fault-a'];$fb=$reports['fault-b']
$checks+=[ordered]@{name='injected query fault repeats same prefix and last boundary';passed=(($fa.secondBattle.trace|ConvertTo-Json -Depth 100 -Compress)-ceq($fb.secondBattle.trace|ConvertTo-Json -Depth 100 -Compress)-and($fa.secondBattle.outcome|ConvertTo-Json -Depth 100 -Compress)-ceq($fb.secondBattle.outcome|ConvertTo-Json -Depth 100 -Compress))}
$summary=[ordered]@{schema='online-decision-cases/v2';status=if(@($cases|Where-Object{$_.exit-ne$_.expectedExit-or($_.name-notlike'fault-*'-and$_.status-ne'passed')}).Count-or@($checks|Where-Object{-not$_.passed}).Count){'failed'}else{'passed'};harnessSha256=(Get-FileHash $dll).Hash;cases=$cases;checks=$checks;scope='Original fixed-seed card-skip scene, second combat only. Two online executions, one frozen replay and two labelled injected query-fault controls; no natural defect or global optimality claim.'}
$summary|ConvertTo-Json -Depth 8|Set-Content (Join-Path $OutputDirectory 'summary.json') -Encoding utf8
Write-Output "Evidence: $OutputDirectory; status=$($summary.status)"
if($summary.status-ne'passed'){exit 1}
