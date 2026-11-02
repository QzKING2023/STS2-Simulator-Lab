param([Parameter(Mandatory)][string]$HarnessRoot,[string]$OutputDirectory='',[string]$GamePath=$env:STS2_GAME_PATH,[switch]$SecondBattle,[switch]$ReviewOnly)
$ErrorActionPreference='Stop'
$labRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$harness=[IO.Path]::GetFullPath($HarnessRoot)
if (-not $harness.StartsWith((Join-Path $labRoot '.build\local-reference')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Harness must be an isolated Lab build.' }
$dll=Join-Path $harness 'bin\Release\net9.0\OfflineSearchHarness.dll'
foreach ($name in @('EleanorV040Probe.cs','EleanorModBootstrap.cs','NativeModRunLoop.cs','NativeModProgression.cs','NativeSecondBattle.cs')) {
    if ((Get-FileHash (Join-Path $harness $name)).Hash -ne (Get-FileHash (Join-Path $PSScriptRoot $name)).Hash) { throw "Harness source stale: $name; build a fresh cache." }
    if (-not $ReviewOnly -and (Get-Item (Join-Path $harness $name)).LastWriteTimeUtc -gt (Get-Item $dll).LastWriteTimeUtc) { throw 'Harness DLL predates its source.' }
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
if (-not $OutputDirectory) { $OutputDirectory=Join-Path $labRoot ('.build\runs\eleanor-native-loop-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')) }
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if (-not $OutputDirectory.StartsWith((Join-Path $labRoot '.build\runs')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence must stay in Lab runs.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$savedSummary=if($ReviewOnly){Get-Content (Join-Path $OutputDirectory 'repeat-summary.json') -Raw|ConvertFrom-Json}else{$null}
$variables=@{
SIMULATORLAB_ELEANOR_MOD='1';SIMULATORLAB_ELEANOR_MOD_DLL=(Join-Path $pin 'MyMod.dll');SIMULATORLAB_ELEANOR_BASELIB_DLL=(Join-Path $pin 'dependencies\BaseLib.dll');
SIMULATORLAB_ELEANOR_MANIFEST=(Join-Path $pin 'MyMod.json');SIMULATORLAB_ELEANOR_MOD_VERSION='v0.4.1-forms-cardpool.fix';
SIMULATORLAB_ELEANOR_V040_PROBE='1';SIMULATORLAB_ELEANOR_V040_FORM='Reverse';SIMULATORLAB_NATIVE_PROFILE=$profile;SIMULATORLAB_NATIVE_LOOP_STAGE='Loop';SIMULATORLAB_NATIVE_REWARD_POLICY='skip';SIMULATORLAB_NATIVE_RECORD_FIXTURE='0';SIMULATORLAB_NATIVE_FIXED_PLAN='';SIMULATORLAB_NATIVE_FIXED_PLAN_SHA=''
SIMULATORLAB_NATIVE_SECOND_BATTLE= $(if($SecondBattle){'1'}else{'0'});SIMULATORLAB_NATIVE_SECOND_PLAN='';SIMULATORLAB_NATIVE_SECOND_PLAN_SHA=''
}
$previous=@{}; foreach($name in $variables.Keys){$previous[$name]=[Environment]::GetEnvironmentVariable($name);[Environment]::SetEnvironmentVariable($name,$variables[$name])}
$cases=@();$reports=@{};$secondPlans=@{}
try {
    foreach($name in @('skip-a','skip-b','take-a','take-b','record-reset')) {
        $policy=if($name-eq'record-reset'){'skip'}else{$name.Split('-')[0]};$env:SIMULATORLAB_NATIVE_REWARD_POLICY=$policy
        $env:SIMULATORLAB_NATIVE_RECORD_FIXTURE=if($name-eq'record-reset'){'1'}else{'0'}
        $env:SIMULATORLAB_NATIVE_SECOND_PLAN=if($secondPlans.ContainsKey($policy)){$secondPlans[$policy]}else{''}
        $env:SIMULATORLAB_NATIVE_SECOND_PLAN_SHA=if($secondPlans.ContainsKey($policy)){(Get-FileHash $secondPlans[$policy]).Hash}else{''}
        $out=Join-Path $OutputDirectory $name
        if($ReviewOnly){$code=@($savedSummary.cases|Where-Object {$_.name-eq$name})[0].exit}
        else {
            & dotnet $dll --character MyMod --encounter FUZZY_WURM_CRAWLER_WEAK --seed LAB_V0111_ELEANOR_V040_FORM_01 --profile Low --beam 8 --nodes 1000 --dop 1 --budget-ms 5000 --search-mode Evaluate --milestone M2 --label "native-$name" --out $out *>&1 | Tee-Object -FilePath (Join-Path $OutputDirectory "$name.log") | Out-Null
            $code=$LASTEXITCODE
        }
        $path=Join-Path $out 'native-run-loop.json'
        $report=if(Test-Path $path){Get-Content $path -Raw | ConvertFrom-Json}else{$null}
        $reports[$name]=$report
        if($SecondBattle -and $report.status-eq'passed' -and -not$secondPlans.ContainsKey($policy)){$secondPlans[$policy]=Join-Path $out 'second-battle\chunk-plan.json'}
        if($name-eq'skip-a' -and $report.status-eq'passed') {
            $env:SIMULATORLAB_NATIVE_FIXED_PLAN=Join-Path $out 'battle\route.json'
            $env:SIMULATORLAB_NATIVE_FIXED_PLAN_SHA=(Get-FileHash $env:SIMULATORLAB_NATIVE_FIXED_PLAN).Hash
        }
        $cases += [ordered]@{name=$name;exit=$code;status=if($report){$report.status}else{'not-run'};phase=$report.phase;reason=$report.reason;report=$path}
        Write-Output "$name exit=$code status=$($report.status) phase=$($report.phase)"
    }
} finally {foreach($name in $previous.Keys){[Environment]::SetEnvironmentVariable($name,$previous[$name])}}
$checks=@()
foreach($case in $cases) {
    $r=$reports[$case.name]
    $bad=@($r.battle.steps|Where-Object{-not $_.exactStateMatch-or-not $_.parentBranchUnchanged-or$_.nativeContinuation-cne$_.simulatorContinuation})
    $checks += [ordered]@{name="$($case.name) actual per-action differential and parent isolation";passed=($r.battle.status-eq'passed'-and$bad.Count-eq0-and$r.battle.steps.Count-gt0-and$r.battle.nativeCombatWonEvents-eq1-and-not$r.battle.nativeCombatStillInProgress)}
    $checks += [ordered]@{name="$($case.name) native next root exact";passed=($r.nextBoundary.status-eq'passed'-and($r.nextBoundary.actual|ConvertTo-Json -Depth 100 -Compress)-ceq($r.nextBoundary.predicted|ConvertTo-Json -Depth 100 -Compress)-and$r.nextBoundary.actualContinuation-ceq$r.nextBoundary.predictedContinuation-and$r.nextBoundary.actualContinuation-ceq$r.nextBoundary.parentContinuation)}
    $checks += [ordered]@{name="$($case.name) isolated writes and frozen discovery";passed=($r.offlineGuards.saveStore-eq'MemorySaveStore'-and$r.offlineGuards.testMode-and-not$r.offlineGuards.steamInitialized-and-not$r.offlineGuards.persistentWritesAllowed-and$r.offlineGuards.interceptedReplayWrites-ge1-and$r.offlineGuards.interceptedProgressSaves-ge2-and$r.pool.lateActualHarmonyPatchRejected)}
}
foreach($policy in @('skip','take')) {
    $a=$reports["$policy-a"];$b=$reports["$policy-b"]
    $checks += [ordered]@{name="$policy deterministic native lifecycle and rewards";passed=($null-ne$a -and $null-ne$b -and ($a.lifecycle | ConvertTo-Json -Depth 100 -Compress) -ceq ($b.lifecycle | ConvertTo-Json -Depth 100 -Compress) -and ($a.rewards | ConvertTo-Json -Depth 100 -Compress) -ceq ($b.rewards | ConvertTo-Json -Depth 100 -Compress))}
    $checks += [ordered]@{name="$policy exact battle and first actionable root";passed=($a.battle.status -eq 'passed' -and $b.battle.status -eq 'passed' -and $a.nextBoundary.status -eq 'passed' -and $b.nextBoundary.status -eq 'passed')}
    $checks += [ordered]@{name="$policy identical fixed action plan and every battle boundary";passed=($a.battle.planSha256-eq$b.battle.planSha256 -and ($a.battle.steps|ConvertTo-Json -Depth 100 -Compress)-ceq($b.battle.steps|ConvertTo-Json -Depth 100 -Compress))}
}
$checks += [ordered]@{name='same reward options before policy split';passed=(($reports['skip-a'].rewards.offered | ConvertTo-Json -Depth 100 -Compress) -ceq ($reports['take-a'].rewards.offered | ConvertTo-Json -Depth 100 -Compress))}
$checks += [ordered]@{name='all policies replay same first-battle plan';passed=($reports['skip-a'].battle.planSha256-eq$reports['take-a'].battle.planSha256)}
$fixture=$reports['record-reset']
$checks += [ordered]@{name='real nonzero Recording Echo fixture and native teardown';passed=($fixture.recordingFixture.after.Echo-gt0 -and $fixture.battle.nativeFinal.Echo-gt0 -and @($fixture.lifecycle|Where-Object{$_.phase-eq'post-combat-cleanup'-and$_.mana.Echo-eq0-and$_.powers.Count-eq0}).Count-eq1)}
if($SecondBattle){
    foreach($case in $cases){
        $r=$reports[$case.name];$s=$r.secondBattle
        $bad=@($s.chunks|Where-Object{$_.status-notin@('boundary','passed')})+@($s.chunks.steps|Where-Object{-not$_.exactStateMatch-or-not$_.parentBranchUnchanged-or$_.nativeContinuation-cne$_.simulatorContinuation})
        $checks += [ordered]@{name="$($case.name) actual second Victory and every native/sim boundary";passed=($s.status-eq'passed'-and$s.terminal-eq'Victory'-and$bad.Count-eq0-and$s.chunks[-1].nativeCombatWonEvents-eq1)}
        $badProof=@($s.rewardPlayProvenance|Where-Object{-not$_.inNativeHand-or-not$_.actualRewardReferenceMatched})
        $checks += [ordered]@{name="$($case.name) acquired ManaShield drawn and actually played";passed=($r.policy-ne'take'-or($s.shieldPlayed-and$s.shieldDrawTurn-ge1-and$s.rewardPlayProvenance.Count-ge1-and$badProof.Count-eq0))}
        $end=$r.lifecycle[-1];$rewardState=@($r.lifecycle|Where-Object {$_.phase-eq'rewards-completed'})[0]
        $checks += [ordered]@{name="$($case.name) post-second-combat persistence and reset";passed=($end.phase-eq'second-post-combat-cleanup'-and$end.hp-eq$s.playerHp-and$end.gold-eq$rewardState.gold-and($end.deck|ConvertTo-Json -Depth 100 -Compress)-ceq($rewardState.deck|ConvertTo-Json -Depth 100 -Compress)-and($end.relics|ConvertTo-Json -Depth 100 -Compress)-ceq($rewardState.relics|ConvertTo-Json -Depth 100 -Compress)-and$end.mana.Mana-eq0-and$end.mana.Echo-eq0-and$end.mana.Stance-eq0-and$end.powers.Count-eq0-and$end.block-eq0)}
    }
    foreach($policy in @('skip','take')){
        $a=$reports["$policy-a"].secondBattle;$b=$reports["$policy-b"].secondBattle
        $checks += [ordered]@{name="$policy second-battle frozen chunks and full trace repeat";passed=($a.planSha256-eq$b.planSha256-and($a.trace|ConvertTo-Json -Depth 100 -Compress)-ceq($b.trace|ConvertTo-Json -Depth 100 -Compress))}
    }
}
$summary=[ordered]@{schema='native-loop-repeat-review/v1';status=if(@($cases|Where-Object{$_.status-ne'passed'-or$_.exit-ne0}).Count -or @($checks|Where-Object{-not $_.passed}).Count){'failed'}else{'passed'};reviewOnly=[bool]$ReviewOnly;harness=$dll;harnessSha256=if($ReviewOnly){$savedSummary.harnessSha256}else{(Get-FileHash $dll).Hash};inputLock=(Join-Path $pin 'input-lock.json');profileLock=(Join-Path $profile 'input-lock.json');cases=$cases;checks=$checks}
$summary | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $OutputDirectory 'repeat-summary.json') -Encoding utf8
Write-Output "Evidence: $OutputDirectory; status=$($summary.status)"
if($summary.status-ne'passed'){exit 1}
