param([Parameter(Mandatory)][string]$BuildManifest,[Parameter(Mandatory)][string]$OutputDirectory,
 [string]$Seed='LAB_V0111_NATIVE_DEV_01',[string]$ReplayPath='',[ValidateSet('','async-fault','stale','budget','subchoice','strategy-stop','runtime-failure','environment-blocked','crash')][string]$Control='',
 [ValidateSet('','root-extra','unknown-choice-field','missing-choice-file','invalid-route')][string]$InputControl='',
 [string]$BranchPlan='', [string]$SolverPlan='',
 [ValidateRange(1,480)][int]$ProcessTimeoutSeconds=120,
 [string]$Scenario='slice-skip',[ValidateSet('off','minimal','detailed')][string]$Observation='detailed',
 [ValidateSet('primary','repeat','replay','failure')][string]$RunKind='primary',
 [string]$SnapshotId='workshop-snapshot-20261106-163352',
 [string]$SnapshotContentSha256='7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73',
 [string]$ModId='vanilla',[string]$ModVersion='0.111.0',[string[]]$DependencyClosure=@(),
 [string]$WorkerIdentity='SimulatorLab.NativeRun',[string]$Adapter='native-run/v1',
 [string]$Collector='isolated NativeRun/GameProbe; native-trace/v1')
$ErrorActionPreference='Stop'
$lab=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(!$out.StartsWith((Join-Path $lab '.build/runs')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Output must be a fresh Lab runs directory'}
if(Test-Path $out){throw 'Refusing to overwrite previous evidence'}
$buildPath=[IO.Path]::GetFullPath($BuildManifest);$build=Split-Path $buildPath
$m=Get-Content $buildPath -Raw|ConvertFrom-Json
$snapshotManifestPath=Join-Path $lab 'local-mods/workshop-snapshot-20261106-163352.manifest.json'
if(!(Test-Path -LiteralPath $snapshotManifestPath -PathType Leaf)){throw "Fixed snapshot manifest is missing: $snapshotManifestPath"}
if($SnapshotId -ne 'workshop-snapshot-20261106-163352' -or $SnapshotContentSha256.ToLowerInvariant() -ne '7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73'){throw 'Run input is not bound to the fixed M2 snapshot'}
if($m.manifest.game.version -ne 'v0.111.0' -or $m.manifest.game.commit -ne '41cef1ea'){throw 'Native build is not bound to the pinned 0.111.0 game'}
function CheckBinding {
 foreach($row in @($m.manifest.source)+@($m.manifest.inputs)) {if((Get-FileHash -LiteralPath $row.path).Hash -ne $row.sha256){throw "Changed source/input: $($row.path)"}}
 foreach($row in @($m.manifest.compiled)+@($m.manifest.artifacts)) {if((Get-FileHash -LiteralPath (Join-Path $build $row.path)).Hash -ne $row.sha256){throw "Changed compiled source/artifact: $($row.path)"}}
}
CheckBinding

function Get-TextSha256([string]$Text) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Text))).ToLowerInvariant()
}
function Get-OptionalFileSha256([string]$Path) {
    if(Test-Path -LiteralPath $Path -PathType Leaf) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
    return $null
}
function Get-TraceKinds([string]$Path) {
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $kinds=[Collections.Generic.List[string]]::new()
    if(!(Test-Path -LiteralPath $Path -PathType Leaf)){return @()}
    foreach($line in Get-Content -LiteralPath $Path){
        try { $row=$line|ConvertFrom-Json; $kind=[string]$row.payload.kind; if($kind -and $seen.Add($kind)){[void]$kinds.Add($kind)} } catch { }
    }
    return @($kinds)
}
function Get-SemanticTraceDigest([string]$Path) {
    if(!(Test-Path -LiteralPath $Path -PathType Leaf)){return $null}
    $payloads=[Collections.Generic.List[string]]::new()
    foreach($line in Get-Content -LiteralPath $Path){
        try { $row=$line|ConvertFrom-Json; if([string]$row.payload.kind -ne 'header'){[void]$payloads.Add(($row.payload|ConvertTo-Json -Compress -Depth 30))} } catch { }
    }
    if(!$payloads.Count){return $null}
    return Get-TextSha256 ($payloads -join "`n")
}

function Ensure-SentryDisabledGameRoot([Parameter(Mandatory)][string]$SourceRoot) {
    $source = [IO.Path]::GetFullPath($SourceRoot)
    $parent = Split-Path -Parent $source
    $leaf = Split-Path -Leaf $source
    $target = Join-Path $parent ($leaf + '-sentry-disabled')
    $overrideText = @"
[rendering]
rendering_device/driver.windows="d3d12"

[sentry]
config/dsn=""
options/auto_init=false
"@
    $markerPath = Join-Path $target '.simulatorlab-sentry-disabled.json'
    $overridePath = Join-Path $target 'override.cfg'
    if (Test-Path -LiteralPath $target -PathType Container) {
        if (!(Test-Path -LiteralPath $markerPath -PathType Leaf) -or !(Test-Path -LiteralPath $overridePath -PathType Leaf)) {
            throw "Sentry-disabled overlay exists without a valid marker: $target"
        }
        $marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
        $expected = Get-TextSha256 $overrideText
        $sourceExeSha = (Get-FileHash -LiteralPath (Join-Path $source 'SlayTheSpire2.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
        $sourcePckSha = (Get-FileHash -LiteralPath (Join-Path $source 'SlayTheSpire2.pck') -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($marker.sourceRoot -ne $source -or $marker.overrideSha256 -ne $expected -or $marker.sourceExecutableSha256 -ne $sourceExeSha -or $marker.sourcePckSha256 -ne $sourcePckSha) {
            throw "Sentry-disabled overlay does not match the requested source or override: $target"
        }
        return [ordered]@{ root=$target; sourceRoot=$source; overrideSha256=$expected; created=$false }
    }
    if (!(Test-Path -LiteralPath $source -PathType Container)) { throw "Game root is missing: $source" }
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    foreach ($file in Get-ChildItem -LiteralPath $source -Recurse -File -Force) {
        $relative = $file.FullName.Substring($source.Length).TrimStart('\')
        $destination = Join-Path $target $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        if ($relative -ieq 'override.cfg') { continue }
        New-Item -ItemType HardLink -Path $destination -Target $file.FullName | Out-Null
    }
    Set-Content -LiteralPath $overridePath -Value $overrideText -Encoding utf8
    $overrideSha = Get-TextSha256 $overrideText
    [ordered]@{
        schema='simulatorlab-sentry-disabled-overlay/v1'; sourceRoot=$source
        sourceExecutableSha256=(Get-FileHash -LiteralPath (Join-Path $source 'SlayTheSpire2.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
        sourcePckSha256=(Get-FileHash -LiteralPath (Join-Path $source 'SlayTheSpire2.pck') -Algorithm SHA256).Hash.ToLowerInvariant()
        overrideSha256=$overrideSha; createdUtc=[DateTime]::UtcNow.ToString('o')
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $markerPath -Encoding utf8
    return [ordered]@{ root=$target; sourceRoot=$source; overrideSha256=$overrideSha; created=$true }
}

function Write-NativeRunEvidenceManifest {
    param($Report,[Parameter(Mandatory)]$Process,[Parameter(Mandatory)][int]$RawExit,
          [Parameter(Mandatory)][datetime]$Started,[Parameter(Mandatory)][datetime]$Finished,
          [Parameter(Mandatory)][int]$ProcessId,[Parameter(Mandatory)][bool]$TimedOut)
    $snapshotManifest=Join-Path $lab 'local-mods/workshop-snapshot-20261106-163352.manifest.json'
    $result=if($Report){$Report.result}else{$null}
    $stop=if($TimedOut){'BudgetExceeded'}elseif(!$Report){'ProcessCrashed'}elseif($result){[string]$result.Stop}else{'ProcessCrashed'}
    $terminal=if($result){[string]$result.Terminal}else{'Unknown'}
    $errorText=if($TimedOut){"native process watchdog expired: $($Process.watchdogReason)"}elseif(!$Report){"native process exited without result.json (exit $RawExit)"}elseif($result){[string]$result.Error}else{$null}
    $stateDigest=Get-OptionalFileSha256 (Join-Path $out 'final-state.json')
    $traceSeal=if(Test-Path (Join-Path $out 'trace-seal.json')){Get-Content -Raw (Join-Path $out 'trace-seal.json')|ConvertFrom-Json}else{$null}
    $traceChain=if($traceSeal){[string]$traceSeal.chain}else{$null}
    $traceDigest=Get-SemanticTraceDigest (Join-Path $out 'trace.jsonl')
    $rngDigest=if($stateDigest){Get-TextSha256 "rng:$stateDigest"}else{$null}
    $kinds=Get-TraceKinds (Join-Path $out 'trace.jsonl')
    $lifecycleDigest=if($kinds.Count){Get-TextSha256 ($kinds -join "`n")}else{$null}
    $canonical=[ordered]@{
        schema='canonical-run-input/v1';targetGameVersion='0.111.0';gameCommit='41cef1ea'
        snapshotId=$SnapshotId;snapshotContentSha256=$SnapshotContentSha256.ToLowerInvariant()
        snapshotManifestSha256=(Get-OptionalFileSha256 $snapshotManifest)
        modId=$ModId;modVersion=$ModVersion;dependencyClosure=@($DependencyClosure|Sort-Object -Unique)
        seed=$Seed;workerIdentity=$WorkerIdentity
        configuration=([ordered]@{scenario=$Scenario;observation=$Observation;control=$Control;inputControl=$InputControl;processTimeoutSeconds=$ProcessTimeoutSeconds}|ConvertTo-Json -Compress)
        adapter=$Adapter;collector=$Collector
    }
    $canonicalJson=$canonical|ConvertTo-Json -Depth 12
    $inputDigest=Get-TextSha256 $canonicalJson
    $canonicalJson|Set-Content -LiteralPath (Join-Path $out 'canonical-input.json') -Encoding utf8
    [ordered]@{schema='native-input-summary/v1';inputDigest=$inputDigest;canonicalInput=$canonical;buildId=$m.buildId;buildManifest=(Resolve-Path $buildPath).Path;runKind=$RunKind;replayPath=$ReplayPath;replaySha256=if($ReplayPath -and (Test-Path $ReplayPath)){Get-OptionalFileSha256 $ReplayPath}else{$null}}|
        ConvertTo-Json -Depth 16|Set-Content -LiteralPath (Join-Path $out 'input-summary.json') -Encoding utf8
    [ordered]@{
        schema='native-run-manifest/v1';runId=[guid]::NewGuid().ToString('N');evidenceKind=$RunKind
        startedUtc=$Started.ToUniversalTime().ToString('o');finishedUtc=$Finished.ToUniversalTime().ToString('o');processId=$ProcessId
        inputDigest=$inputDigest;canonicalInput=$canonical;terminal=$terminal;stop=$stop;errorCode=$stop;error=$errorText
        lastBoundary=if($result){[string]$result.LastBoundary}else{'process-exit'};lifecycleBoundaries=$kinds
        stateDigest=$stateDigest;rngDigest=$rngDigest;traceDigest=$traceDigest;traceChain=$traceChain;lifecycleDigest=$lifecycleDigest
        routeDigest=Get-OptionalFileSha256 (Join-Path $out 'route.json');choiceDigest=Get-OptionalFileSha256 (Join-Path $out 'choices.json')
        completeStateEvidence=($null-ne$stateDigest);completeRngEvidence=($null-ne$rngDigest);completeTraceEvidence=($null-ne$traceDigest)
        process=[ordered]@{rawExit=$RawExit;watchdogExpired=$TimedOut;reportPresent=($null-ne$Report);exitConsistent=if($Process){[bool]$Process.exitConsistent}else{$false}}
        evidence=[ordered]@{result='result.json';process='process.json';trace='trace.jsonl';traceSeal='trace-seal.json';finalState='final-state.json';route='route.json';choices='choices.json'}
    }|ConvertTo-Json -Depth 20|Set-Content -LiteralPath (Join-Path $out 'run-manifest.json') -Encoding utf8
}

function Invoke-WatcherGodotSlice {
    param(
        [Parameter(Mandatory)][string]$Output,
        [Parameter(Mandatory)][string]$GameRoot,
        [Parameter(Mandatory)][string]$BuildId,
        [Parameter(Mandatory)][string]$ContentKey,
        [Parameter(Mandatory)][string]$Seed,
        [Parameter(Mandatory)][string]$Scenario,
        [Parameter(Mandatory)][string]$Observation,
        [string]$Replay = ''
    )

    $sourceGameRoot = [IO.Path]::GetFullPath($GameRoot)
    $overlay = Ensure-SentryDisabledGameRoot -SourceRoot $sourceGameRoot
    $runtimeGameRoot = [string]$overlay.root
    $exe = Join-Path $runtimeGameRoot 'SlayTheSpire2.exe'
    $pck = Join-Path $runtimeGameRoot 'SlayTheSpire2.pck'
    $probeDll = Join-Path $runtimeGameRoot 'mods/SimulatorLab.GameProbe/SimulatorLab.GameProbe.dll'
    $probeJson = Join-Path $runtimeGameRoot 'mods/SimulatorLab.GameProbe/SimulatorLab.GameProbe.json'
    $baseDll = Join-Path $runtimeGameRoot 'mods/BaseLib/BaseLib.dll'
    $watcherDll = Join-Path $runtimeGameRoot 'mods/Watcher/Watcher.dll'
    foreach($path in @($exe,$pck,$probeDll,$probeJson,$baseDll,$watcherDll)) {
        if(!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Watcher Godot input is missing: $path" }
    }
    if((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne '8602C26BFFD2937E3841835FD8360EF8E974624A543E05977229FD3D062BE231') { throw 'Watcher Godot executable hash is not the pinned 0.111.0 input' }
    if((Get-FileHash -LiteralPath $pck -Algorithm SHA256).Hash -ne 'C60F672EE7804E6AEFA1E19A582FA1C80B126A7B0EEF4D084D3ABF110DF2EAB7') { throw 'Watcher Godot PCK hash is not the pinned 0.111.0 input' }

    $gameOutput = Join-Path $Output 'godot-probe'
    $appData = Join-Path $Output 'game-appdata'
    $localAppData = Join-Path $Output 'game-localappdata'
    New-Item -ItemType Directory -Path $gameOutput,$appData,$localAppData -Force | Out-Null
    $seedProfile = Join-Path $lab '.build/native-game/appdata/SlayTheSpire2/default/1'
    if(Test-Path -LiteralPath $seedProfile -PathType Container) {
        $settingsDir = Join-Path $appData 'SlayTheSpire2/default/1'
        New-Item -ItemType Directory -Path $settingsDir -Force | Out-Null
        Copy-Item -Path (Join-Path $seedProfile '*') -Destination $settingsDir -Recurse -Force
    }

    $psi = [Diagnostics.ProcessStartInfo]::new($exe)
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.WorkingDirectory = $runtimeGameRoot
    # Godot writes its complete diagnostic stream to godot.log.  Do not keep
    # inherited stdout/stderr pipes open: the native launcher can keep a
    # console handle alive after the game process has completed, which would
    # make the parent wait forever while reading redirected streams.
    $psi.RedirectStandardOutput = $false
    $psi.RedirectStandardError = $false
    foreach($arg in @('--headless','--quit-after','300','--log-file',(Join-Path $gameOutput 'godot.log'),'--force-steam','off')) { $psi.ArgumentList.Add($arg) }
    $psi.Environment['APPDATA'] = $appData
    $psi.Environment['LOCALAPPDATA'] = $localAppData
    $psi.Environment['SIMULATORLAB_WATCHER_GAME_OUTPUT'] = $gameOutput
    $psi.Environment['SIMULATORLAB_WATCHER_BUILD_ID'] = $BuildId
    $psi.Environment['SIMULATORLAB_WATCHER_CONTENT_KEY'] = $ContentKey
    $startedUtc = [DateTime]::UtcNow
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $child = [Diagnostics.Process]::Start($psi)
    if(!$child.WaitForExit($ProcessTimeoutSeconds * 1000)) {
        try { $child.Kill($true) } catch { }
        throw "Watcher Godot process exceeded ${ProcessTimeoutSeconds}s"
    }
    $clock.Stop()
    [IO.File]::WriteAllText((Join-Path $Output 'console.log'),'external Godot process; see godot-probe/godot.log',[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $Output 'stderr.log'),'external Godot process; see godot-probe/godot.log',[Text.UTF8Encoding]::new($false))
    $rawExit = $child.ExitCode
    $rawExit | Set-Content -LiteralPath (Join-Path $Output 'godot-raw-exit.txt') -Encoding utf8
    $probePath = Join-Path $gameOutput 'gameprobe.json'
    if($rawExit -ne 0 -or !(Test-Path -LiteralPath $probePath -PathType Leaf)) {
        throw "Watcher Godot runtime failed: raw exit $rawExit; probe present $([bool](Test-Path -LiteralPath $probePath))"
    }
    $probe = Get-Content -LiteralPath $probePath -Raw | ConvertFrom-Json
    if($probe.schema -ne 'watcher-runtime-probe/v1' -or !$probe.nativeRuntimeAvailable -or $probe.terminal -ne 'battle-complete') { throw 'Watcher Godot probe did not prove a native battle boundary' }
    $requiredEvents = @('character-discovered','character-selected','initial-state','battle-victory','reward-boundary','leave-combat','cleanup','replay')
    foreach($event in $requiredEvents) { if(@($probe.events) -notcontains $event) { throw "Watcher Godot probe is missing event $event" } }
    Copy-Item -LiteralPath $probePath -Destination (Join-Path $Output 'watcher-runtime-probe.json')
    $identityCount = @($probe.identities).Count
    $actionCount = @($probe.evidence.combat).Count
    if($identityCount -lt 1 -or $actionCount -lt 1) { throw 'Watcher Godot probe is missing identities or combat actions' }
    $inputBinding = [ordered]@{
        schema = 'watcher-native-input-binding/v1'
        snapshot = $env:SIMULATORLAB_WATCHER_SNAPSHOT_ROOT
        snapshotContentSha256 = '7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73'
        targetGameVersion = '0.111.0'; gameCommit = '41cef1ea'; seed = $Seed
        sourceGameRoot = $sourceGameRoot; gameRoot = $runtimeGameRoot; nativeBuildId = $BuildId; probePath = $probePath
        sentryDisabled = $true; sentryOverrideSha256 = $overlay.overrideSha256
        executableSha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
        pckSha256 = (Get-FileHash -LiteralPath $pck -Algorithm SHA256).Hash
        baseLibSha256 = (Get-FileHash -LiteralPath $baseDll -Algorithm SHA256).Hash
        watcherSha256 = (Get-FileHash -LiteralPath $watcherDll -Algorithm SHA256).Hash
        gameProbeSha256 = (Get-FileHash -LiteralPath $probeDll -Algorithm SHA256).Hash
        gameProbeSourceSha256 = (Get-FileHash -LiteralPath (Join-Path $lab '.build/diag/gameprobe/MainFile.cs') -Algorithm SHA256).Hash
        gameProbeProjectSha256 = (Get-FileHash -LiteralPath (Join-Path $lab '.build/diag/gameprobe/GameProbe.csproj') -Algorithm SHA256).Hash
        formalSteamDataWritten = $false
    }
    $inputBinding | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $Output 'input-binding.json') -Encoding utf8
    $provenance = [ordered]@{ScenarioKind='WatcherGodotNative';ExecutionKind='RealGodotProcess';Scenario=$Scenario;Replay=([bool]$Replay);Evidence='watcher-runtime-probe/v1';BuildId=$BuildId}
    $result = [ordered]@{
        schema='native-run-result/v1'; terminal='Ongoing'; stop='SliceBoundary'; comparison='NotCompared'
        dispatched=$actionCount; confirmed=$actionCount; dataComplete=$true
        lastBoundary='watcher-battle-reward-leave-cleanup'; error=$null
    }
    $trace = [ordered]@{schema='native-trace/v1';complete=$true;source='real-godot-process';probeDigest=$probe.primaryDigest;events=$probe.events}
    $report = [ordered]@{result=$result;validation=@();rawExit=0;scope=$Scenario;provenance=$provenance;trace=$trace;watcherProbe=(Resolve-Path (Join-Path $Output 'watcher-runtime-probe.json')).Path;inputBinding=(Resolve-Path (Join-Path $Output 'input-binding.json')).Path}
    $report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $Output 'result.json') -Encoding utf8
    $report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $Output 'runtime-result.json') -Encoding utf8
    [ordered]@{processTimeoutSeconds=$ProcessTimeoutSeconds;elapsedSeconds=$clock.Elapsed.TotalSeconds;schema='native-process/v1';buildId=$BuildId;seed=$Seed;scenario=$Scenario;observation=$Observation;peakWorkingSet=$null;peakWorkingSetSource='unavailable for external Godot process';provenance=$provenance;scenarioKind=$provenance.ScenarioKind;executionKind=$provenance.ExecutionKind;control='';rawExit=0;watchdogExpired=$false;watchdogReason='';memoryCapBytes=$null;reportPresent=$true;exitConsistent=$true;startedUtc=$startedUtc.ToString('o');endedUtc=[DateTime]::UtcNow.ToString('o');stop='SliceBoundary';replay=$Replay} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $Output 'process.json') -Encoding utf8
    [ordered]@{schema='native-timing/v1';elapsedSeconds=$clock.Elapsed.TotalSeconds;peakWorkingSetBeforeExit=$null;source='external-godot-process'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Output 'timing.json') -Encoding utf8
    Copy-Item -LiteralPath $buildPath -Destination (Join-Path $Output 'build-manifest.json')
    return 0
}

if($Scenario -eq 'watcher-stage6' -and $env:SIMULATORLAB_WATCHER_STAGE6 -eq '1') {
    $watcherGameRoot = Join-Path $lab '.build/native-game/watcher-stage6'
    New-Item -ItemType Directory -Path $out -Force | Out-Null
    $watcherExit = Invoke-WatcherGodotSlice -Output $out -GameRoot $watcherGameRoot -BuildId $m.buildId -ContentKey $m.manifest.contentKey -Seed $Seed -Scenario $Scenario -Observation $Observation -Replay $ReplayPath
    CheckBinding
    exit $watcherExit
}
if($ProcessTimeoutSeconds -gt 120){
 if(!$SolverPlan){throw 'Long process envelope requires an explicit registered plan'}
 $fullPlan=Get-Content -LiteralPath $SolverPlan -Raw|ConvertFrom-Json
 $study=$Scenario -eq 'solver-character-pilot' -and $fullPlan.SearchStudy -eq $true -and $null -eq $fullPlan.Run -and $fullPlan.PrefixCount -eq 0 -and $fullPlan.MaxActions -eq 64 -and $ProcessTimeoutSeconds -eq 240
 if(!$study -and ($fullPlan.Run.Schema -ne 'native-full-run/v2' -or $fullPlan.Run.WallSeconds -gt 450 -or $fullPlan.Run.WallSeconds+15 -gt $ProcessTimeoutSeconds -or $Scenario -notin @('solver-native-darv-act2','solver-native-darv-act2-replay'))){throw 'Invalid long full-run/process budget binding'}
}
New-Item -ItemType Directory $out|Out-Null
Copy-Item $buildPath (Join-Path $out 'build-manifest.json')
$executable=if($m.manifest.executable){$m.manifest.executable}else{'SimulatorLab.NativeRun.dll'}
$exe=Join-Path $build ('bin/Release/net9.0/'+$executable)
$arguments=@($exe,$out,$Seed,$ReplayPath,$Control,$Scenario,$Observation,$InputControl,$(if($SolverPlan){$SolverPlan}else{$BranchPlan}))
$start=[DateTime]::UtcNow
. (Join-Path $PSScriptRoot 'NativeProcessBudget.ps1')
$psi=[Diagnostics.ProcessStartInfo]::new('dotnet');$psi.UseShellExecute=$false;$psi.CreateNoWindow=$true
$psi.RedirectStandardOutput=$true;$psi.RedirectStandardError=$true
if($env:SIMULATORLAB_KAGUYA_STAGE8 -eq '1'){$psi.Environment['SIMULATORLAB_KAGUYA_OUTPUT']=$out}
if($env:SIMULATORLAB_MINION_STAGE9 -eq '1'){$psi.Environment['SIMULATORLAB_MINION_OUTPUT']=$out}
foreach($argument in $arguments){$psi.ArgumentList.Add([string]$argument)}
$clock=[Diagnostics.Stopwatch]::StartNew()
$child=[Diagnostics.Process]::Start($psi)
$stdout=$child.StandardOutput.ReadToEndAsync();$stderr=$child.StandardError.ReadToEndAsync()
$gate=if($ProcessTimeoutSeconds -gt 120){Wait-NativeFullRunProcessBudget -Process $child -Clock $clock -Seconds $ProcessTimeoutSeconds}else{@{Expired=(Wait-NativeProcessBudget -Process $child -Clock $clock -Seconds $ProcessTimeoutSeconds);Reason='legacy-time'}}
$timedOut=$gate.Expired
$stdout.GetAwaiter().GetResult()|Set-Content (Join-Path $out 'console.log') -Encoding utf8
$stderr.GetAwaiter().GetResult()|Set-Content (Join-Path $out 'stderr.log') -Encoding utf8
$code=$child.ExitCode
$resultPath=Join-Path $out 'result.json'
$report=if(Test-Path $resultPath){Get-Content $resultPath -Raw|ConvertFrom-Json}else{$null}
$consistent=$null -ne $report -and $code -eq $report.rawExit
$timingPath=Join-Path $out 'timing.json'
$timing=if(Test-Path $timingPath){Get-Content $timingPath -Raw|ConvertFrom-Json}else{$null}
$provenance=if($report){$report.provenance}else{$null}
$processRecord=[ordered]@{processTimeoutSeconds=$ProcessTimeoutSeconds;elapsedSeconds=$clock.Elapsed.TotalSeconds;schema='native-process/v1';buildId=$m.buildId;seed=$Seed;scenario=$Scenario;observation=$Observation;peakWorkingSet=if($timing){$timing.peakWorkingSetBeforeExit}else{$null};peakWorkingSetSource='runtime-before-exit; unavailable when timing report missing';provenance=$provenance;scenarioKind=if($provenance){$provenance.ScenarioKind}else{'unknown'};executionKind=if($provenance){$provenance.ExecutionKind}else{'unknown'};control=$Control;rawExit=$code;watchdogExpired=$timedOut;watchdogReason=$(if($timedOut){$gate.Reason}else{''});memoryCapBytes=$(if($ProcessTimeoutSeconds -gt 120){4294967296}else{$null});reportPresent=($null-ne$report);exitConsistent=$consistent;startedUtc=$start.ToString('o');endedUtc=[DateTime]::UtcNow.ToString('o');stop=if($report){$report.result.Stop}elseif($timedOut){'BudgetExceeded'}else{'ProcessCrashed'};replay=$ReplayPath;runKind=$RunKind}
$processRecord |ConvertTo-Json -Depth 16|Set-Content (Join-Path $out 'process.json') -Encoding utf8
Write-NativeRunEvidenceManifest -Report $report -Process $processRecord -RawExit $code -Started $start -Finished ([DateTime]::UtcNow) -ProcessId $child.Id -TimedOut $timedOut | Out-Null
CheckBinding
if(!$consistent){throw "Missing/contradictory result; raw child exit $code"}
exit $code
