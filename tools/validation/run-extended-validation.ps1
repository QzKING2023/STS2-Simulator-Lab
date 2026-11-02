param([Parameter(Mandatory)][string]$HarnessRoot,[ValidateSet('ShieldMatrix')][string]$Stage='ShieldMatrix',[string]$GamePath=$env:STS2_GAME_PATH,[string]$OutputDirectory='')
$ErrorActionPreference='Stop'
$lab=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$harness=[IO.Path]::GetFullPath($HarnessRoot)
if(-not$harness.StartsWith((Join-Path $lab '.build\local-reference')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Harness must stay in the Lab cache.'}
$dll=Join-Path $harness 'bin\Release\net9.0\OfflineSearchHarness.dll'
foreach($name in @('EleanorV040Probe.cs','ManaShieldBoundaryProbe.cs','NativeModProgression.cs','NativeSecondBattle.cs','NativeCombatDecision.cs')){
    if((Get-FileHash (Join-Path $harness $name)).Hash-ne(Get-FileHash (Join-Path $PSScriptRoot $name)).Hash){throw "Stale compiled source: $name"}
    if((Get-Item (Join-Path $harness $name)).LastWriteTimeUtc-gt(Get-Item $dll).LastWriteTimeUtc){throw 'Rebuild harness before execution.'}
}
$pin=Join-Path $lab '.build\pinned-inputs\eleanor-v0.4.1-forms-cardpool.fix'
$lock=Get-Content (Join-Path $pin 'input-lock.json') -Raw|ConvertFrom-Json
foreach($file in $lock.files){if((Get-FileHash (Join-Path $pin $file.relativePath)).Hash-ne$file.sha256){throw 'Pinned input changed.'}}
if((Get-FileHash (Join-Path $GamePath 'data_sts2_windows_x86_64\sts2.dll')).Hash-ne'0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9'){throw 'Game hash mismatch.'}
$profile=Join-Path $lab '.build\pinned-inputs\eleanor-v041-native-bootstrap'
foreach($file in (Get-Content (Join-Path $profile 'input-lock.json') -Raw|ConvertFrom-Json).files){if((Get-FileHash $file.path).Hash-ne$file.sha256){throw 'Bootstrap input changed.'}}
if(-not$OutputDirectory){$OutputDirectory=Join-Path $lab ('.build\runs\extended-'+$Stage+'-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(-not$OutputDirectory.StartsWith((Join-Path $lab '.build\runs')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Reports must remain in Lab runs.'}
New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
$vars=@{SIMULATORLAB_ELEANOR_MOD='1';SIMULATORLAB_ELEANOR_MOD_DLL=(Join-Path $pin 'MyMod.dll');SIMULATORLAB_ELEANOR_BASELIB_DLL=(Join-Path $pin 'dependencies\BaseLib.dll');SIMULATORLAB_ELEANOR_MANIFEST=(Join-Path $pin 'MyMod.json');SIMULATORLAB_ELEANOR_MOD_VERSION='v0.4.1-forms-cardpool.fix';SIMULATORLAB_ELEANOR_V040_PROBE='1';SIMULATORLAB_ELEANOR_V040_FORM='Reverse';SIMULATORLAB_NATIVE_PROFILE=$profile;SIMULATORLAB_NATIVE_LOOP_STAGE='Loop';SIMULATORLAB_NATIVE_REWARD_POLICY='skip';SIMULATORLAB_NATIVE_DECISION_MODE='coverage-plan';SIMULATORLAB_NATIVE_RECORD_FIXTURE='0';SIMULATORLAB_NATIVE_SECOND_BATTLE='0';SIMULATORLAB_NATIVE_FIXED_PLAN='';SIMULATORLAB_NATIVE_FIXED_PLAN_SHA='';SIMULATORLAB_SHIELD_FIXTURE='1';SIMULATORLAB_SHIELD_MANA='0';SIMULATORLAB_SHIELD_UPGRADED='0'}
$previous=@{};foreach($key in $vars.Keys){$previous[$key]=[Environment]::GetEnvironmentVariable($key);[Environment]::SetEnvironmentVariable($key,$vars[$key])}
$cases=@();$checks=@();$reports=@{}
try{
    foreach($upgrade in 0,1){foreach($mana in 0,1,2){foreach($repeat in 'a','b'){
        $id="upgrade$upgrade-mana$mana-$repeat";$out=Join-Path $OutputDirectory $id
        $env:SIMULATORLAB_SHIELD_UPGRADED=[string]$upgrade;$env:SIMULATORLAB_SHIELD_MANA=[string]$mana
        & dotnet $dll --character MyMod --encounter FUZZY_WURM_CRAWLER_WEAK --seed LAB_V0111_ELEANOR_V040_FORM_01 --profile Low --beam 8 --nodes 1000 --dop 1 --budget-ms 5000 --search-mode Evaluate --milestone M1 --out $out *>&1|Set-Content (Join-Path $OutputDirectory "$id.log")
        $exit=$LASTEXITCODE;$path=Join-Path $out 'shield-boundary.json'
        $r=if(Test-Path $path){Get-Content $path -Raw|ConvertFrom-Json}else{$null};$reports[$id]=$r
        $cases+=[ordered]@{id=$id;exit=$exit;status=if($r){$r.status}else{'not-run'};report=$path}
        Write-Output "$id exit=$exit status=$($r.status)"
    }}}
}finally{foreach($key in $previous.Keys){[Environment]::SetEnvironmentVariable($key,$previous[$key])}}
foreach($upgrade in 0,1){foreach($mana in 0,1,2){
    $a=$reports["upgrade$upgrade-mana$mana-a"];$b=$reports["upgrade$upgrade-mana$mana-b"]
    $checks+=[ordered]@{name="upgrade$upgrade-mana$mana exact repeat";passed=($null-ne$a-and$null-ne$b-and($a.replay.steps|ConvertTo-Json -Depth 100 -Compress)-ceq($b.replay.steps|ConvertTo-Json -Depth 100 -Compress)-and($a.manaAfter|ConvertTo-Json -Compress)-ceq($b.manaAfter|ConvertTo-Json -Compress))}
}}
$summary=[ordered]@{schema='extended-validation/v1';stage=$Stage;status=if(@($cases|Where-Object{$_.exit-ne0-or$_.status-ne'passed'}).Count-or@($checks|Where-Object{-not$_.passed}).Count){'failed'}else{'passed'};harnessSha256=(Get-FileHash $dll).Hash;inputLock=(Join-Path $pin 'input-lock.json');cases=$cases;checks=$checks}
$summary|ConvertTo-Json -Depth 8|Set-Content (Join-Path $OutputDirectory 'summary.json') -Encoding utf8
Write-Output "Evidence: $OutputDirectory; status=$($summary.status)"
if($summary.status-ne'passed'){exit 1}
