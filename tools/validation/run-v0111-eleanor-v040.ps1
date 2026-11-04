param(
    [string]$GamePath = $env:STS2_GAME_PATH,
    [string]$CombatSolverSource = $env:STS2_COMBAT_SOLVER_SOURCE,
    [string]$GodotPath = $env:STS2_GODOT_PATH,
    [string]$BaseLibManifestPath = $env:STS2_BASELIB_MANIFEST,
    [string]$RitsuManifestPath = $env:STS2_RITSU_MANIFEST,
    [string]$PinnedModDirectory = 'eleanor-v0.4.0-forms-echo.foundation',
    [string]$ExpectedModVersion = 'v0.4.0-forms-echo.foundation',
    [string]$VictoryReplayPlanPath = '',
    [ValidateSet('Mentor','Reverse')][string]$VictoryReplayForm = 'Reverse',
    [ValidateSet('','Pool','Loop')][string]$NativeLoopStage = '',
    [ValidateSet('skip','take')][string]$NativeRewardPolicy = 'skip',
    [switch]$SecondBattle,
    [switch]$BuildOnly
)

$ErrorActionPreference = 'Stop'
foreach ($required in @('GamePath','CombatSolverSource','GodotPath')) {
    if ([string]::IsNullOrWhiteSpace((Get-Variable -Name $required -ValueOnly))) { throw "Provide -$required or its STS2_* environment variable; see docs/VALIDATION.md." }
}
$labRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$gamePathFull = [IO.Path]::GetFullPath($GamePath)
$dataDir = Join-Path $gamePathFull 'data_sts2_windows_x86_64'
$releaseInfo = Get-Content -LiteralPath (Join-Path $gamePathFull 'release_info.json') -Raw | ConvertFrom-Json
$sts2Dll = Join-Path $dataDir 'sts2.dll'
$gameHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sts2Dll).Hash
if ($releaseInfo.version -ne 'v0.111.0' -or $releaseInfo.commit -ne '41cef1ea' -or
    $releaseInfo.main_assembly_hash -ne 222455745 -or
    $gameHash -ne '0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9') {
    throw 'Installed game files do not match pinned v0.111.0 / 41cef1ea.'
}

$pinRoot = Join-Path (Join-Path $labRoot '.build\pinned-inputs') $PinnedModDirectory
$lockPath = Join-Path $pinRoot 'input-lock.json'
$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
if ($lock.schema -ne 'simulatorlab-pinned-mod-inputs/v1' -or $lock.modId -ne 'MyMod' -or
    $lock.modVersion -ne $ExpectedModVersion -or $lock.gameMinVersion -ne '0.111.0') {
    throw "Pinned input lock has the wrong Mod/game identity: $($lock.modId) $($lock.modVersion)."
}
foreach ($file in $lock.files) {
    $path = Join-Path $pinRoot $file.relativePath
    if (-not (Test-Path -LiteralPath $path)) { throw "Pinned v0.4 input missing: $($file.relativePath)" }
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash
    if ($actual -ne $file.sha256 -or (Get-Item -LiteralPath $path).Length -ne $file.length) {
        throw "Pinned v0.4 input hash/length mismatch: $($file.relativePath)"
    }
}
$modDll = Join-Path $pinRoot 'MyMod.dll'
$manifestPath = Join-Path $pinRoot 'MyMod.json'
$baseLibDll = Join-Path $pinRoot 'dependencies\BaseLib.dll'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.version -ne $lock.modVersion -or $manifest.version -ne $ExpectedModVersion -or $manifest.id -ne 'MyMod' -or
    $manifest.min_game_version -ne '0.111.0' -or -not $manifest.affects_gameplay) {
    throw 'Pinned Mod manifest identity check failed.'
}
$runTag = switch ($manifest.version) {
    'v0.4.0-forms-echo.foundation' { 'v040' }
    'v0.4.1-forms-cardpool.fix' { 'v041' }
    default { throw "Unsupported v0.4 validation input version: $($manifest.version)" }
}

$ritsuRoot = Join-Path $pinRoot 'dependencies\ritsu'
$solverProjectSource = Join-Path $CombatSolverSource 'CombatSolver.csproj'
$solverSourceText = [IO.File]::ReadAllText($solverProjectSource, [Text.Encoding]::UTF8)
if ($solverSourceText -notmatch '<Version>\s*0\.50\.1\s*</Version>') {
    throw 'Local CombatSolver source is not version 0.50.1.'
}
$required = @(
    $solverProjectSource,
    (Join-Path $CombatSolverSource 'LICENSE'),
    (Join-Path $ritsuRoot 'RitsuLib.References.props'),
    (Join-Path $ritsuRoot 'compat\0.111.0\STS2-RitsuLib.dll'),
    (Join-Path $ritsuRoot 'compat\0.111.0\STS2-RitsuLib.Runtime.dll'),
    (Join-Path $ritsuRoot 'shared\STS2-RitsuLib.Shared.dll'),
    (Join-Path $ritsuRoot 'shared\STS2-RitsuLib.Ui.dll'),
    (Join-Path $ritsuRoot 'shared\STS2-RitsuLib.Settings.dll'),
    $GodotPath
)
foreach ($path in $required) { if (-not (Test-Path -LiteralPath $path)) { throw "Required offline input missing: $path" } }

$sourceNames = @(
    'EleanorV040Probe.cs', 'ManaShieldBoundaryProbe.cs', 'EleanorModBootstrap.cs', 'NativeModRunLoop.cs', 'NativeModProgression.cs', 'NativeSecondBattle.cs', 'NativeCombatDecision.cs', 'ApplyNativeRunLoopOverlay.ps1', 'HarmonyPatchMetadataProbe.cs',
    'VanillaCombatRootAdapter.cs', 'VanillaGameActionProbe.cs',
    'ApplyCombatSolverNoPatchCardMirrorOverlay.ps1', 'ApplyCombatSolverNoPatchCardResultLocationOverlay.ps1',
    'ApplyCombatSolverNoPatchPowerHookOverlay.ps1', 'ApplyCombatSolverNoPatchPowerTurnStartOverlay.ps1',
    'AdaptedRelicBeforeHandDrawMirrors.cs', 'ApplyCombatSolverRelicTurnStartOverlay.ps1', 'run-v041-victory-replay.ps1',
    'run-v0111-eleanor-v040.ps1'
)
$sourceNames += @('StageOneProbe.cs','ApplyStageOneHarnessOverlay.ps1','VanillaBattleReplayProbe.cs','VanillaRandomTargetProbe.cs','NativeRunLoopProbe.cs')
$sourceNames += @('AdapterCardSelection.cs','ApplyAdapterCardSelectionOverlay.ps1','EleanorRewardCardMirrors.cs','RewardCardSelectionBridge.cs','RewardCardBoundaryProbe.cs','RewardCardIdentityProbe.cs')
$sourcePaths = @($sourceNames | ForEach-Object { Join-Path $PSScriptRoot $_ })
$sourcePaths += @(Get-ChildItem -LiteralPath (Join-Path $labRoot 'src/SimulatorLab.Core') -Filter '*.cs' -File | Sort-Object Name | ForEach-Object FullName)
$revisionInput = (($sourcePaths | ForEach-Object { (Get-FileHash -Algorithm SHA256 -LiteralPath $_).Hash }) -join '')
$revision = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($revisionInput))).Substring(0, 12).ToLowerInvariant()
$cacheRoot = Join-Path $labRoot ".build\local-reference\CombatSolver-v040-$revision"
if (-not (Test-Path -LiteralPath $cacheRoot)) {
    New-Item -ItemType Directory -Path $cacheRoot -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $CombatSolverSource 'src') -Destination $cacheRoot -Recurse
    $toolsRoot = Join-Path $cacheRoot 'tools'
    New-Item -ItemType Directory -Path $toolsRoot -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $CombatSolverSource 'tools\search') -Destination $toolsRoot -Recurse
    foreach ($name in @('CombatSolver.csproj', 'CombatSolver.json', 'LICENSE')) {
        Copy-Item -LiteralPath (Join-Path $CombatSolverSource $name) -Destination $cacheRoot
    }
} elseif (-not (Test-Path -LiteralPath (Join-Path $cacheRoot 'LICENSE'))) {
    throw 'Existing ignored CombatSolver v0.4 copy lacks its MIT license.'
}

$solverProject = Join-Path $cacheRoot 'CombatSolver.csproj'
$solverProjectText = [IO.File]::ReadAllText($solverProject, [Text.Encoding]::UTF8)
if ($solverProjectText -notmatch '<Version>\s*0\.50\.1\s*</Version>') {
    throw 'Ignored v0.4 CombatSolver source copy is not version 0.50.1.'
}
$overlayLock = Join-Path $cacheRoot 'lab-overlay-lock.json'
if (Test-Path -LiteralPath $overlayLock) {
    $overlayInputs = Get-Content -LiteralPath $overlayLock -Raw | ConvertFrom-Json
    if ($overlayInputs.revision -ne $revision) { throw 'Cached overlay revision mismatch.' }
    foreach ($file in $overlayInputs.files) {
        if ((Get-FileHash -LiteralPath (Join-Path $cacheRoot $file.path) -Algorithm SHA256).Hash -ne $file.sha256) { throw "Cached overlay changed: $($file.path)" }
    }
} else {
$noPatchOverlay = Join-Path $PSScriptRoot 'ApplyCombatSolverNoPatchCardMirrorOverlay.ps1'
& $noPatchOverlay -TargetFile (Join-Path $cacheRoot 'src\Prediction\AdaptedCardOnPlayMirrors.cs')
$resultLocationOverlay = Join-Path $PSScriptRoot 'ApplyCombatSolverNoPatchCardResultLocationOverlay.ps1'
& $resultLocationOverlay -TargetRoot $cacheRoot
$powerHookOverlay = Join-Path $PSScriptRoot 'ApplyCombatSolverNoPatchPowerHookOverlay.ps1'
& $powerHookOverlay -TargetRoot $cacheRoot
$powerTurnStartOverlay = Join-Path $PSScriptRoot 'ApplyCombatSolverNoPatchPowerTurnStartOverlay.ps1'
& $powerTurnStartOverlay -TargetRoot $cacheRoot
$relicTurnStartOverlay = Join-Path $PSScriptRoot 'ApplyCombatSolverRelicTurnStartOverlay.ps1'
$relicTurnStartAdapter = Join-Path $PSScriptRoot 'AdaptedRelicBeforeHandDrawMirrors.cs'
& $relicTurnStartOverlay -SourceRoot (Join-Path $cacheRoot 'src') -AdapterSource $relicTurnStartAdapter
$overlayFiles = @(Get-ChildItem -LiteralPath (Join-Path $cacheRoot 'src') -Recurse -Filter '*.cs' -File | ForEach-Object { [ordered]@{path=[IO.Path]::GetRelativePath($cacheRoot,$_.FullName);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })
& (Join-Path $PSScriptRoot 'ApplyAdapterCardSelectionOverlay.ps1') -TargetRoot $cacheRoot
$overlayFiles = @(Get-ChildItem -LiteralPath (Join-Path $cacheRoot 'src') -Recurse -Filter '*.cs' -File | ForEach-Object { [ordered]@{path=[IO.Path]::GetRelativePath($cacheRoot,$_.FullName);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })
[IO.File]::WriteAllText($overlayLock,([ordered]@{revision=$revision;files=$overlayFiles} | ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
}
$harnessRoot = Join-Path $cacheRoot 'tools\search\OfflineSearchHarness'
foreach ($name in @('EleanorV040Probe.cs', 'ManaShieldBoundaryProbe.cs', 'EleanorModBootstrap.cs', 'NativeModRunLoop.cs', 'NativeModProgression.cs', 'NativeSecondBattle.cs', 'NativeCombatDecision.cs', 'HarmonyPatchMetadataProbe.cs', 'VanillaCombatRootAdapter.cs', 'VanillaGameActionProbe.cs')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $harnessRoot $name) -Force
}

$programPath = Join-Path $harnessRoot 'Program.cs'
foreach($name in @('EleanorRewardCardMirrors.cs','RewardCardSelectionBridge.cs','RewardCardBoundaryProbe.cs','RewardCardIdentityProbe.cs')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $harnessRoot $name) -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'StageOneProbe.cs') -Destination (Join-Path $harnessRoot 'StageOneProbe.cs') -Force
$program = [IO.File]::ReadAllText($programPath, [Text.Encoding]::UTF8)
$loadMarker = 'if (Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD") == "1")'
$loadAnchor = 'Step(steps, "M0.2'
if (-not $program.Contains($loadMarker, [StringComparison]::Ordinal)) {
    $index = $program.IndexOf($loadAnchor, [StringComparison]::Ordinal)
    if ($index -lt 0) { throw 'Harness Program lacks the expected v0.111 pre-ModelDb insertion point.' }
    $program = $program.Insert($index,
        '            if (Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD") == "1")' + [Environment]::NewLine +
        '                payload["eleanorMod"] = EleanorModBootstrap.LoadFromEnvironment();' + [Environment]::NewLine + '            ')
}
$registrationMarker = 'if (Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_V040_PROBE") == "1")' + [Environment]::NewLine + '                EleanorV040Probe.RegisterFromLoadedMod();'
$loadAnchor = 'payload["eleanorMod"] = EleanorModBootstrap.LoadFromEnvironment();'
if (-not $program.Contains($registrationMarker, [StringComparison]::Ordinal)) {
    $index = $program.IndexOf($loadAnchor, [StringComparison]::Ordinal)
    if ($index -lt 0) { throw 'Harness Program lacks the expected Mod-load registration point.' }
    $index += $loadAnchor.Length
    $program = $program.Insert($index, [Environment]::NewLine +
        '            if (Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_V040_PROBE") == "1")' + [Environment]::NewLine +
        '                EleanorV040Probe.RegisterFromLoadedMod();')
}
$probeMarker = 'payload["eleanorV041VictoryReplay"] = EleanorV040Probe.RunVictoryReplay('
$probeAnchor = 'outcome = ModRuntime.RunSearchDetailed(combat!, options, loop, session);'
if (-not $program.Contains($probeMarker, [StringComparison]::Ordinal)) {
    $index = $program.IndexOf($probeAnchor, [StringComparison]::Ordinal)
    if ($index -lt 0) { throw 'Harness Program lacks the expected v0.111 post-search insertion point.' }
    $index += $probeAnchor.Length
    $program = $program.Insert($index, [Environment]::NewLine +
        '                if (Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_V041_VICTORY_REPLAY") == "1")' + [Environment]::NewLine +
        '                    payload["eleanorV041VictoryReplay"] = EleanorV040Probe.RunVictoryReplay(combat!, loop, options.OutputDirectory,' + [Environment]::NewLine +
        '                        Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_V041_PLAN_PATH") ?? throw new InvalidOperationException("Fixed route path is missing."));' + [Environment]::NewLine +
        '                else if (Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_V040_PROBE") == "1")' + [Environment]::NewLine +
        '                    payload["eleanorV040Probe"] = EleanorV040Probe.Run(combat!, loop, options.OutputDirectory);')
}
[IO.File]::WriteAllText($programPath, $program, [Text.UTF8Encoding]::new($false))

$gameBootstrapPath = Join-Path $harnessRoot 'GameBootstrap.cs'
$gameBootstrap = [IO.File]::ReadAllText($gameBootstrapPath, [Text.Encoding]::UTF8)
$modTypeMarker = 'EleanorModBootstrap.RegisterReflectionTypesAfterHarnessReset();'
$modTypeAnchor = 'AccessTools.Field(typeof(ReflectionHelper), "_modTypes").SetValue(null, Array.Empty<Type>());'
if (-not $gameBootstrap.Contains($modTypeMarker, [StringComparison]::Ordinal)) {
    $index = $gameBootstrap.IndexOf($modTypeAnchor, [StringComparison]::Ordinal)
    if ($index -lt 0) { throw 'Harness bootstrap lacks the expected reflection registry reset point.' }
    $index += $modTypeAnchor.Length
    $gameBootstrap = $gameBootstrap.Insert($index, [Environment]::NewLine +
        '        if (Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD") == "1")' + [Environment]::NewLine +
        '            EleanorModBootstrap.RegisterReflectionTypesAfterHarnessReset();')
}
$modelInitAnchor = 'ModelDb.Init(AbstractModelSubtypes.All.ToArray());'
$modelInitMarker = 'ModelDb.Init(EleanorModBootstrap.IncludeModels(AbstractModelSubtypes.All.ToArray()));'
if (-not $gameBootstrap.Contains($modelInitMarker, [StringComparison]::Ordinal)) {
    if (-not $gameBootstrap.Contains($modelInitAnchor, [StringComparison]::Ordinal)) { throw 'Harness bootstrap lacks ModelDb.Init.' }
    $gameBootstrap = $gameBootstrap.Replace($modelInitAnchor, $modelInitMarker)
}
[IO.File]::WriteAllText($gameBootstrapPath, $gameBootstrap, [Text.UTF8Encoding]::new($false))


$offlineCombatPath = Join-Path $harnessRoot 'OfflineCombat.cs'
$offlineCombat = [IO.File]::ReadAllText($offlineCombatPath, [Text.Encoding]::UTF8)
$loadoutMarker = 'EleanorModBootstrap.ConfigureValidationLoadout(runState);'
$loadoutAnchor = '        await RunManager.Instance.FinalizeStartingRelics();'
if (-not $offlineCombat.Contains($loadoutMarker, [StringComparison]::Ordinal)) {
    if (-not $offlineCombat.Contains($loadoutAnchor, [StringComparison]::Ordinal)) { throw 'Harness combat setup lacks post-relic point.' }
    $offlineCombat = $offlineCombat.Replace($loadoutAnchor, $loadoutAnchor + [Environment]::NewLine + '        await EleanorModBootstrap.ConfigureValidationLoadout(runState);')
}
$characterAnchor = 'ResolveUnique(ModelDb.AllCharacters, scenario.CharacterId,'
$characterReplacement = 'ResolveUnique(ModelDb.All.OfType<CharacterModel>(), scenario.CharacterId,'
if (-not $offlineCombat.Contains($characterReplacement, [StringComparison]::Ordinal)) {
    if (-not $offlineCombat.Contains($characterAnchor, [StringComparison]::Ordinal)) { throw 'Harness combat setup lacks Mod character resolver anchor.' }
    $offlineCombat = $offlineCombat.Replace($characterAnchor, $characterReplacement)
}
[IO.File]::WriteAllText($offlineCombatPath, $offlineCombat, [Text.UTF8Encoding]::new($false))

$labCoreProject = Join-Path $labRoot 'src\SimulatorLab.Core\SimulatorLab.Core.csproj'
$harnessProject = Join-Path $harnessRoot 'OfflineSearchHarness.csproj'
[xml]$harnessXml = Get-Content -LiteralPath $harnessProject -Raw
if (-not ($harnessXml.Project.ItemGroup.ProjectReference | Where-Object { $_.Include -eq $labCoreProject })) {
    $group = $harnessXml.CreateElement('ItemGroup')
    $reference = $harnessXml.CreateElement('ProjectReference')
    $reference.SetAttribute('Include', $labCoreProject)
    [void]$group.AppendChild($reference)
    [void]$harnessXml.DocumentElement.AppendChild($group)
    $harnessXml.Save($harnessProject)
}
$stubDir = Join-Path $labRoot '.build\local-reference\NoOpMemoryCleaner'
$stubProject = Join-Path $stubDir 'NoOpMemoryCleaner.csproj'
if (-not (Test-Path -LiteralPath $stubProject)) {
    New-Item -ItemType Directory -Path $stubDir -Force | Out-Null
    Set-Content -LiteralPath $stubProject -Encoding utf8 -Value '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>'
}
$solverDll = Join-Path $cacheRoot '.godot\mono\temp\bin\Release\CombatSolver.dll'
$harnessDll = Join-Path $harnessRoot 'bin\Release\net9.0\OfflineSearchHarness.dll'
$commonProperties = @(
    "-p:Sts2Dir=$gamePathFull", "-p:Sts2DataDir=$dataDir", "-p:RitsuWorkshopRoot=$ritsuRoot",
    "-p:RitsuLibDir=$(Join-Path $ritsuRoot 'compat\0.111.0')", "-p:GodotPath=$GodotPath",
    '-p:CopyModOnBuild=false', '-p:DeployToGame=false', '-p:RitsuLibAutoCopy=false',
    '-p:MSBuildEnableWorkloadResolver=false', '-p:NuGetAudit=false', '-p:RestoreIgnoreFailedSources=true'
)
if ($NativeLoopStage) {
    & (Join-Path $PSScriptRoot 'ApplyNativeRunLoopOverlay.ps1') -HarnessRoot $harnessRoot
}
& (Join-Path $PSScriptRoot 'ApplyStageOneHarnessOverlay.ps1') -HarnessRoot $harnessRoot
& dotnet build $solverProject --configuration Release --verbosity minimal @commonProperties "-p:MemoryCleanerProject=$stubProject"
if ($LASTEXITCODE -ne 0) { throw "CombatSolver build failed ($LASTEXITCODE)." }
& dotnet build $harnessProject --configuration Release --verbosity minimal @commonProperties "-p:CombatSolverRepositoryRoot=$cacheRoot" "-p:CombatSolverDll=$solverDll"
if ($LASTEXITCODE -ne 0) { throw "Offline harness build failed ($LASTEXITCODE)." }
if ($BuildOnly) { Write-Output "Build only: harness=$harnessRoot"; exit 0 }

$runId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$runRoot = Join-Path $labRoot ".build\runs\eleanor-$runTag-$runId"
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$argsBase = @('--character', 'MyMod', '--encounter', 'FUZZY_WURM_CRAWLER_WEAK', '--seed', 'LAB_V0111_ELEANOR_V040_FORM_01',
    '--profile', 'Low', '--beam', '8', '--nodes', '1000', '--dop', '1', '--budget-ms', '5000', '--search-mode', 'Evaluate')
$env:SIMULATORLAB_ELEANOR_MOD = '1'
$env:SIMULATORLAB_ELEANOR_MOD_DLL = $modDll
$env:SIMULATORLAB_ELEANOR_BASELIB_DLL = $baseLibDll
$env:SIMULATORLAB_ELEANOR_MANIFEST = $manifestPath
$env:SIMULATORLAB_ELEANOR_MOD_VERSION = [string]$manifest.version
$env:SIMULATORLAB_ELEANOR_V040_PROBE = '1'
if ($NativeLoopStage) {
    $profile = Join-Path $labRoot '.build\pinned-inputs\eleanor-v041-native-bootstrap'
    $sources = @{
        'BaseLib' = $BaseLibManifestPath
        'STS2-RitsuLib' = $RitsuManifestPath
        'MyMod' = $manifestPath
    }
    $files = @()
    foreach ($id in @('BaseLib','STS2-RitsuLib','MyMod')) {
        $folder = Join-Path $profile "mods\$id"
        New-Item -ItemType Directory -Force -Path $folder | Out-Null
        $destination = Join-Path $folder "$id.json"
        if (-not (Test-Path -LiteralPath $destination)) {
            if ([string]::IsNullOrWhiteSpace($sources[$id])) { throw "Provide the authentic $id manifest path before creating a bootstrap snapshot." }
            Copy-Item -LiteralPath $sources[$id] -Destination $destination
        }
        $hash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        if ($sources[$id] -and $hash -ne (Get-FileHash -LiteralPath $sources[$id] -Algorithm SHA256).Hash) { throw "Bootstrap manifest changed: $id" }
        $files += [ordered]@{id=$id; path=$destination; sha256=$hash; source=$sources[$id]}
    }
    $profileLock = Join-Path $profile 'input-lock.json'
    if (Test-Path -LiteralPath $profileLock) {
        $savedProfile = Get-Content -LiteralPath $profileLock -Raw | ConvertFrom-Json
        foreach ($file in $savedProfile.files) {
            if ((Get-FileHash -LiteralPath $file.path -Algorithm SHA256).Hash -ne $file.sha256) { throw "Locked bootstrap input changed: $($file.id)" }
        }
    }
    if (-not (Test-Path -LiteralPath $profileLock)) {
        [IO.File]::WriteAllText($profileLock,([ordered]@{schema='native-bootstrap-inputs/v1'; modLock=$lockPath; files=$files} | ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
    }
    $env:SIMULATORLAB_NATIVE_PROFILE = $profile
    $env:SIMULATORLAB_NATIVE_LOOP_STAGE = $NativeLoopStage
    $env:SIMULATORLAB_NATIVE_REWARD_POLICY = $NativeRewardPolicy
    if($SecondBattle){$env:SIMULATORLAB_NATIVE_SECOND_BATTLE='1'}
    $env:SIMULATORLAB_ELEANOR_V040_FORM = 'Reverse'
    $milestone = if ($NativeLoopStage -eq 'Pool') { 'M1' } else { 'M2' }
    & dotnet $harnessDll @argsBase --milestone $milestone --label "native-$NativeLoopStage-$NativeRewardPolicy" --out (Join-Path $runRoot 'native') *>&1 | Tee-Object -FilePath (Join-Path $runRoot 'native-console.log')
    $nativeExit = $LASTEXITCODE
    Remove-Item Env:SIMULATORLAB_NATIVE_PROFILE,Env:SIMULATORLAB_NATIVE_LOOP_STAGE,Env:SIMULATORLAB_NATIVE_REWARD_POLICY,Env:SIMULATORLAB_NATIVE_SECOND_BATTLE -ErrorAction SilentlyContinue
    Write-Output "Native stage $NativeLoopStage exit=$nativeExit; evidence=$runRoot; harness=$harnessRoot"
    exit $nativeExit
}
$runForms = @('Mentor', 'Reverse')
$victoryReplayMode = -not [string]::IsNullOrWhiteSpace($VictoryReplayPlanPath)
if ($victoryReplayMode) {
    $VictoryReplayPlanPath = [IO.Path]::GetFullPath($VictoryReplayPlanPath)
    if (-not (Test-Path -LiteralPath $VictoryReplayPlanPath -PathType Leaf)) { throw "Fixed route plan not found: $VictoryReplayPlanPath" }
    $planSourceResult = Join-Path (Split-Path -Parent $VictoryReplayPlanPath) 'harness-result.json'
    if (-not (Test-Path -LiteralPath $planSourceResult -PathType Leaf)) { throw "Fixed route source result not found: $planSourceResult" }
    $planSource = Get-Content -LiteralPath $planSourceResult -Raw | ConvertFrom-Json
    $lockedModHash = ($lock.files | Where-Object { $_.relativePath -eq 'MyMod.dll' } | Select-Object -First 1).sha256
    $planMatches = $planSource.eleanorMod.assemblySha256 -eq $lockedModHash
    $planMatches = $planMatches -and $planSource.scenario.CharacterId -eq 'MyMod'
    $planMatches = $planMatches -and $planSource.scenario.EncounterId -eq 'FUZZY_WURM_CRAWLER_WEAK'
    $planMatches = $planMatches -and $planSource.scenario.Seed -eq 'LAB_V0111_ELEANOR_V040_FORM_01'
    if (-not $planMatches) {
        throw 'Fixed route source result does not match the locked v0.4.1 assembly and selected scenario.'
    }
    $runForms = @($VictoryReplayForm)
    $env:SIMULATORLAB_ELEANOR_V041_VICTORY_REPLAY = '1'
    $env:SIMULATORLAB_ELEANOR_V041_PLAN_PATH = $VictoryReplayPlanPath
}
$statuses = @{}
try {
    foreach ($form in $runForms) {
        $out = Join-Path $runRoot $form.ToLowerInvariant()
        $env:SIMULATORLAB_ELEANOR_V040_FORM = $form
        $label = if ($victoryReplayMode) { "eleanor-$runTag-victory-replay-$($form.ToLowerInvariant())" } else { "eleanor-$runTag-guiding-$($form.ToLowerInvariant())" }
        & dotnet $harnessDll @argsBase --milestone M2 --label $label --out $out
        $exit = $LASTEXITCODE
        $resultPath = Join-Path $out 'harness-result.json'
        if (Test-Path -LiteralPath $resultPath) {
            $harnessResult = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
            $probe = if ($victoryReplayMode) { $harnessResult.eleanorV041VictoryReplay } else { $harnessResult.eleanorV040Probe }
            if ($exit -eq 0 -and $probe -and $probe.status -eq 'passed' -and ($victoryReplayMode -or $probe.exactStateMatch)) {
                $statuses[$form] = 'passed'
            } elseif ($victoryReplayMode -and $probe -and $probe.status -eq 'death' -and $probe.nativeDeath -and $probe.simulatorDeath) {
                $statuses[$form] = 'death'
            } elseif ($probe -and $probe.status -eq 'unsupported') {
                $statuses[$form] = 'unsupported'
            } elseif ($harnessResult.error.type -like '*PredictionUnsupportedException') {
                $statuses[$form] = 'unsupported'
            } else {
                $statuses[$form] = 'failed'
            }
        } else { $statuses[$form] = 'not-run' }
    }
} finally {
    foreach ($name in @('SIMULATORLAB_ELEANOR_MOD','SIMULATORLAB_ELEANOR_MOD_DLL','SIMULATORLAB_ELEANOR_BASELIB_DLL',
        'SIMULATORLAB_ELEANOR_MANIFEST','SIMULATORLAB_ELEANOR_MOD_VERSION','SIMULATORLAB_ELEANOR_V040_PROBE','SIMULATORLAB_ELEANOR_V040_FORM',
        'SIMULATORLAB_ELEANOR_V041_VICTORY_REPLAY','SIMULATORLAB_ELEANOR_V041_PLAN_PATH')) {
        Remove-Item "Env:$name" -ErrorAction SilentlyContinue
    }
}
$summary = [ordered]@{
    schema = "simulatorlab-eleanor-$runTag-run/v1"
    status = if ($statuses.Values -contains 'failed') { 'failed' } elseif ($statuses.Values -contains 'unsupported' -or $statuses.Values -contains 'not-run') { 'unsupported' } else { 'passed' }
    gameVersion = $releaseInfo.version
    gameCommit = $releaseInfo.commit
    modVersion = $manifest.version
    modDllSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $modDll).Hash
    inputLock = $lockPath
    probeRevision = $revision
    choices = $statuses
    output = $runRoot
}
$summaryPath = Join-Path $runRoot "$runTag-summary.json"
[IO.File]::WriteAllText($summaryPath, ($summary | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
Write-Output "Pinned-input $($manifest.version) run: status=$($summary.status); $(($statuses.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join '; ')"
Write-Output "Evidence: $summaryPath"
if ($summary.status -ne 'passed') { exit 1 }
