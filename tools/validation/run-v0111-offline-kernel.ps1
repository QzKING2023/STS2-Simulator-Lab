param(
    [string]$GamePath = $env:STS2_GAME_PATH,
    [string]$CombatSolverSource = $env:STS2_COMBAT_SOLVER_SOURCE,
    [string]$RitsuWorkshopRoot = $env:STS2_RITSU_ROOT,
    [string]$GodotPath = $env:STS2_GODOT_PATH
)

$ErrorActionPreference = 'Stop'
foreach ($required in @('GamePath','CombatSolverSource','RitsuWorkshopRoot','GodotPath')) {
    if ([string]::IsNullOrWhiteSpace((Get-Variable -Name $required -ValueOnly))) { throw "Provide -$required or its STS2_* environment variable; see docs/VALIDATION.md." }
}
$labRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$gamePathFull = [IO.Path]::GetFullPath($GamePath)
$dataDir = Join-Path $gamePathFull 'data_sts2_windows_x86_64'
$releaseInfoPath = Join-Path $gamePathFull 'release_info.json'
$sts2Dll = Join-Path $dataDir 'sts2.dll'
$releaseInfo = Get-Content -LiteralPath $releaseInfoPath -Raw | ConvertFrom-Json
$dllHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sts2Dll).Hash
if ($releaseInfo.version -ne 'v0.111.0' -or $releaseInfo.commit -ne '41cef1ea' -or $releaseInfo.main_assembly_hash -ne 222455745 -or
    $dllHash -ne '0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9') {
    throw 'Installed game files do not match the pinned v0.111.0 build.'
}

$sourceProject = Join-Path $CombatSolverSource 'CombatSolver.csproj'
$solverVersion = [xml](Get-Content -LiteralPath $sourceProject -Raw)
if (($solverVersion.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1) -ne '0.50.1') {
    throw 'Local CombatSolver source is not version 0.50.1.'
}
$ritsuLibDir = Join-Path $RitsuWorkshopRoot 'compat\0.111.0'
$ritsuProps = Join-Path $RitsuWorkshopRoot 'RitsuLib.References.props'
foreach ($required in @(
    $sourceProject,
    (Join-Path $CombatSolverSource 'LICENSE'),
    $ritsuProps,
    (Join-Path $ritsuLibDir 'STS2-RitsuLib.dll'),
    (Join-Path $ritsuLibDir 'STS2-RitsuLib.Runtime.dll'),
    (Join-Path $RitsuWorkshopRoot 'shared\STS2-RitsuLib.Shared.dll'),
    (Join-Path $RitsuWorkshopRoot 'shared\STS2-RitsuLib.Ui.dll'),
    (Join-Path $RitsuWorkshopRoot 'shared\STS2-RitsuLib.Settings.dll'),
    $GodotPath
)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required local build input is missing: $required" }
}

$pinnedModRoot = Join-Path $labRoot '.build\pinned-inputs\eleanor-v0.3.0-m3.burst23'
$inputLockPath = Join-Path $pinnedModRoot 'input-lock.json'
$inputLock = Get-Content -LiteralPath $inputLockPath -Raw | ConvertFrom-Json
if ($inputLock.schema -ne 'simulatorlab-pinned-mod-inputs/v1' -or
    $inputLock.modId -ne 'MyMod' -or
    $inputLock.modVersion -ne 'v0.3.0-m3.burst23' -or
    $inputLock.gameMinVersion -ne '0.111.0') {
    throw 'Pinned Lab Eleanor input lock does not match MyMod v0.3.0-m3.burst23 for game v0.111.0.'
}
$myModDll = Join-Path $pinnedModRoot 'MyMod.dll'
$myModManifestPath = Join-Path $pinnedModRoot 'MyMod.json'
$myModDepsPath = Join-Path $pinnedModRoot 'MyMod.deps.json'
$baseLibDll = Join-Path $pinnedModRoot 'BaseLib.dll'
foreach ($required in @($myModDll, $myModManifestPath, $myModDepsPath, $baseLibDll)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required local Eleanor test input is missing: $required" }
}
$lockedFiles = @{}
foreach ($file in $inputLock.files) { $lockedFiles[[string]$file.name] = [string]$file.sha256 }
foreach ($name in @('MyMod.dll', 'MyMod.json', 'MyMod.deps.json', 'BaseLib.dll')) {
    $path = Join-Path $pinnedModRoot $name
    $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash
    if (-not $lockedFiles.ContainsKey($name) -or $actualHash -ne $lockedFiles[$name]) {
        throw "Pinned Lab Eleanor input hash mismatch: $name. Refusing a live project or altered binary input."
    }
}
$myModManifest = Get-Content -LiteralPath $myModManifestPath -Raw | ConvertFrom-Json
$myModVersion = [string]$myModManifest.version
if ($myModManifest.id -ne 'MyMod' -or $myModVersion -ne 'v0.3.0-m3.burst23' -or $myModManifest.min_game_version -ne '0.111.0') {
    throw 'The local Eleanor test input is not the pinned MyMod v0.3.0-m3.burst23 build.'
}
$modDllHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $myModDll).Hash

$nativeProbeSource = Join-Path $PSScriptRoot 'VanillaGameActionProbe.cs'
$rootAdapterSource = Join-Path $PSScriptRoot 'VanillaCombatRootAdapter.cs'
$probeRevisionSources = @($nativeProbeSource, $rootAdapterSource)
$probeRevisionSources += @(
    (Join-Path $PSScriptRoot 'run-v0111-offline-kernel.ps1'),
    (Join-Path $PSScriptRoot 'VanillaBattleReplayProbe.cs'),
    (Join-Path $PSScriptRoot 'VanillaRandomTargetProbe.cs'),
    (Join-Path $PSScriptRoot 'ApplyCombatSolverNoPatchCardMirrorOverlay.ps1'),
    (Join-Path $PSScriptRoot 'ApplyCombatSolverRelicTurnStartOverlay.ps1'),
    (Join-Path $PSScriptRoot 'AdaptedRelicBeforeHandDrawMirrors.cs'),
    (Join-Path $PSScriptRoot 'EleanorTurnStartMirrors.cs'),
    (Join-Path $PSScriptRoot 'EleanorCardMirrors.cs'),
    (Join-Path $PSScriptRoot 'EleanorModBootstrap.cs'),
    (Join-Path $PSScriptRoot 'EleanorCombatProbe.cs'),
    (Join-Path $PSScriptRoot 'NativeRunLoopProbe.cs'))
$probeRevisionInput = (($probeRevisionSources | ForEach-Object {
    (Get-FileHash -Algorithm SHA256 -LiteralPath $_).Hash
}) -join '')
$probeRevision = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($probeRevisionInput))).Substring(0, 12).ToLowerInvariant()
$cacheRoot = Join-Path $labRoot ".build\local-reference\CombatSolver-$probeRevision"
if (-not (Test-Path -LiteralPath $cacheRoot)) {
    New-Item -ItemType Directory -Path $cacheRoot -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $CombatSolverSource 'src') -Destination $cacheRoot -Recurse
    $toolsRoot = Join-Path $cacheRoot 'tools'
    New-Item -ItemType Directory -Path $toolsRoot -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $CombatSolverSource 'tools\search') -Destination $toolsRoot -Recurse
    foreach ($file in @('CombatSolver.csproj', 'CombatSolver.json', 'LICENSE')) {
        Copy-Item -LiteralPath (Join-Path $CombatSolverSource $file) -Destination $cacheRoot
    }
} elseif (-not (Test-Path -LiteralPath (Join-Path $cacheRoot 'LICENSE'))) {
    throw 'The existing ignored local reference copy has no LICENSE; refusing to use it.'
}
$solverProject = Join-Path $cacheRoot 'CombatSolver.csproj'
$cachedSolverVersion = [xml](Get-Content -LiteralPath $solverProject -Raw)
if (($cachedSolverVersion.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1) -ne '0.50.1') {
    throw 'The ignored local build copy is not CombatSolver version 0.50.1.'
}
$noPatchOverlay = Join-Path $PSScriptRoot 'ApplyCombatSolverNoPatchCardMirrorOverlay.ps1'
& $noPatchOverlay -TargetFile (Join-Path $cacheRoot 'src\Prediction\AdaptedCardOnPlayMirrors.cs')
$relicTurnStartOverlay = Join-Path $PSScriptRoot 'ApplyCombatSolverRelicTurnStartOverlay.ps1'
& $relicTurnStartOverlay -SourceRoot (Join-Path $cacheRoot 'src') `
    -AdapterSource (Join-Path $PSScriptRoot 'AdaptedRelicBeforeHandDrawMirrors.cs')

$harnessSourceRoot = Join-Path $cacheRoot 'tools\search\OfflineSearchHarness'
$forkProbe = Join-Path $harnessSourceRoot 'ForkIsolationChecks.cs'
$forkProbeSource = Join-Path $PSScriptRoot 'ForkIsolationChecks.cs'
if (Test-Path -LiteralPath $forkProbe) {
    $cachedText = [IO.File]::ReadAllText($forkProbe, [Text.Encoding]::UTF8)
    $sourceText = [IO.File]::ReadAllText($forkProbeSource, [Text.Encoding]::UTF8)
    if ($cachedText -cne $sourceText) { throw 'The local fork probe differs from the reviewed Lab source; use a fresh ignored build cache.' }
} else {
    Copy-Item -LiteralPath $forkProbeSource -Destination $forkProbe
}

$nativeProbe = Join-Path $harnessSourceRoot 'VanillaGameActionProbe.cs'
if (Test-Path -LiteralPath $nativeProbe) {
    $cachedText = [IO.File]::ReadAllText($nativeProbe, [Text.Encoding]::UTF8)
    $sourceText = [IO.File]::ReadAllText($nativeProbeSource, [Text.Encoding]::UTF8)
    if ($cachedText -cne $sourceText) { throw 'The local native action probe differs from the reviewed Lab source; use a fresh ignored build cache.' }
} else {
    Copy-Item -LiteralPath $nativeProbeSource -Destination $nativeProbe
}

$rootAdapter = Join-Path $harnessSourceRoot 'VanillaCombatRootAdapter.cs'
if (Test-Path -LiteralPath $rootAdapter) {
    $cachedText = [IO.File]::ReadAllText($rootAdapter, [Text.Encoding]::UTF8)
    $sourceText = [IO.File]::ReadAllText($rootAdapterSource, [Text.Encoding]::UTF8)
    if ($cachedText -cne $sourceText) { throw 'The local root adapter differs from the reviewed Lab source; use a fresh ignored build cache.' }
} else {
    Copy-Item -LiteralPath $rootAdapterSource -Destination $rootAdapter
}

$battleReplaySource = Join-Path $PSScriptRoot 'VanillaBattleReplayProbe.cs'
$battleReplay = Join-Path $harnessSourceRoot 'VanillaBattleReplayProbe.cs'
if (Test-Path -LiteralPath $battleReplay) {
    $cachedText = [IO.File]::ReadAllText($battleReplay, [Text.Encoding]::UTF8)
    $sourceText = [IO.File]::ReadAllText($battleReplaySource, [Text.Encoding]::UTF8)
    if ($cachedText -cne $sourceText) { throw 'The local battle replay probe differs from the reviewed Lab source; use a fresh ignored build cache.' }
} else {
    Copy-Item -LiteralPath $battleReplaySource -Destination $battleReplay
}

$randomTargetSource = Join-Path $PSScriptRoot 'VanillaRandomTargetProbe.cs'
$randomTargetProbe = Join-Path $harnessSourceRoot 'VanillaRandomTargetProbe.cs'
if (Test-Path -LiteralPath $randomTargetProbe) {
    $cachedText = [IO.File]::ReadAllText($randomTargetProbe, [Text.Encoding]::UTF8)
    $sourceText = [IO.File]::ReadAllText($randomTargetSource, [Text.Encoding]::UTF8)
    if ($cachedText -cne $sourceText) { throw 'The local random-target probe differs from the reviewed Lab source; use a fresh ignored build cache.' }
} else {
    Copy-Item -LiteralPath $randomTargetSource -Destination $randomTargetProbe
}

foreach ($probeName in @('EleanorTurnStartMirrors.cs', 'EleanorCardMirrors.cs', 'EleanorModBootstrap.cs', 'EleanorCombatProbe.cs', 'NativeRunLoopProbe.cs')) {
    $source = Join-Path $PSScriptRoot $probeName
    $cached = Join-Path $harnessSourceRoot $probeName
    if (Test-Path -LiteralPath $cached) {
        $cachedText = [IO.File]::ReadAllText($cached, [Text.Encoding]::UTF8)
        $sourceText = [IO.File]::ReadAllText($source, [Text.Encoding]::UTF8)
        if ($cachedText -cne $sourceText) { throw "The local $probeName differs from the reviewed Lab source; use a fresh ignored build cache." }
    } else {
        Copy-Item -LiteralPath $source -Destination $cached
    }
}

$programPath = Join-Path $harnessSourceRoot 'Program.cs'
$program = [IO.File]::ReadAllText($programPath, [Text.Encoding]::UTF8)
$forkMarker = 'if (Environment.GetEnvironmentVariable("OFFLINE_HARNESS_FORK_ISOLATION_CHECKS") == "1")'
if (-not $program.Contains($forkMarker, [StringComparison]::Ordinal)) {
    $existingMarker = 'if (Environment.GetEnvironmentVariable("OFFLINE_HARNESS_INFUSED_CORE_CHECKS") == "1")'
    $existingCall = 'InfusedCoreChecks.Run(combat!, options.OutputDirectory);'
    $callIndex = $program.IndexOf($existingCall, [StringComparison]::Ordinal)
    if (-not $program.Contains($existingMarker, [StringComparison]::Ordinal) -or $callIndex -lt 0) {
        throw 'Pinned offline harness source has no recognized insertion point for the fork check.'
    }
    $insertAt = $callIndex + $existingCall.Length
    $addition = "`r`n            if (Environment.GetEnvironmentVariable(`"OFFLINE_HARNESS_FORK_ISOLATION_CHECKS`") == `"1`")`r`n                ForkIsolationChecks.Run(combat!);"
    $program = $program.Insert($insertAt, $addition)
    [IO.File]::WriteAllText($programPath, $program, [Text.UTF8Encoding]::new($false))
}

$nativeMarker = 'if (Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_GAME_ACTION_PROBE") == "1")'
$nativeAnchor = 'payload["root"] = OfflineCombat.DescribeRoot(combat!);'
if (-not $program.Contains($nativeMarker, [StringComparison]::Ordinal)) {
    $rootIndex = $program.IndexOf($nativeAnchor, [StringComparison]::Ordinal)
    if ($rootIndex -lt 0) { throw 'Pinned offline harness source has no recognized M1 root-capture insertion point for the Lab action probe.' }
    $insertAt = $rootIndex + $nativeAnchor.Length
    $addition = [Environment]::NewLine + '            if (Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_GAME_ACTION_PROBE") == "1")' + [Environment]::NewLine + '                payload["nativeGameActionProbe"] = VanillaGameActionProbe.Run(combat!, loop, options.OutputDirectory);'
    $program = $program.Insert($insertAt, $addition)
    [IO.File]::WriteAllText($programPath, $program, [Text.UTF8Encoding]::new($false))
}

$battleMarker = 'if (Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_BATTLE_TRACE") == "1")'
$battleAnchor = '                payload["search"] = new Dictionary<string, object?>'
if (-not $program.Contains($battleMarker, [StringComparison]::Ordinal)) {
    $searchIndex = $program.IndexOf($battleAnchor, [StringComparison]::Ordinal)
    if ($searchIndex -lt 0) { throw 'Pinned offline harness source has no recognized post-search insertion point for the full battle probe.' }
    $addition = '            if (Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_BATTLE_TRACE") == "1")' + [Environment]::NewLine + '                payload["nativeBattleReplay"] = VanillaBattleReplayProbe.Run(combat!, loop, outcome!, options.OutputDirectory);' + [Environment]::NewLine + '            if (Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_RUNLOOP") == "1")' + [Environment]::NewLine + '                payload["nativeRunLoop"] = NativeRunLoopProbe.Run(loop, options.OutputDirectory);' + [Environment]::NewLine
    $program = $program.Insert($searchIndex, $addition)
    [IO.File]::WriteAllText($programPath, $program, [Text.UTF8Encoding]::new($false))
}

$randomTargetMarker = 'if (Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_RANDOM_TARGET") == "1")'
$randomTargetAnchor = 'outcome = ModRuntime.RunSearchDetailed(combat!, options, loop, session);'
if (-not $program.Contains($randomTargetMarker, [StringComparison]::Ordinal)) {
    $searchIndex = $program.IndexOf($randomTargetAnchor, [StringComparison]::Ordinal)
    if ($searchIndex -lt 0) { throw 'Pinned offline harness source has no recognized pre-search insertion point for the random-target probe.' }
    $addition = '                    if (Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_RANDOM_TARGET") == "1")' + [Environment]::NewLine + '                        payload["nativeRandomTargetProbe"] = VanillaRandomTargetProbe.Run(combat!, loop, options.OutputDirectory);' + [Environment]::NewLine + '                    '
    $program = $program.Insert($searchIndex, $addition)
    [IO.File]::WriteAllText($programPath, $program, [Text.UTF8Encoding]::new($false))
}

$eleanorBootstrapMarker = 'if (Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD") == "1")'
$modelDbInitAnchor = 'Step(steps, "M0.2'
if (-not $program.Contains($eleanorBootstrapMarker, [StringComparison]::Ordinal)) {
    $initIndex = $program.IndexOf($modelDbInitAnchor, [StringComparison]::Ordinal)
    if ($initIndex -lt 0) { throw 'Pinned offline harness source has no recognized pre-ModelDb point for loading MyMod.' }
    $addition = '            if (Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD") == "1")' + [Environment]::NewLine + '                payload["eleanorMod"] = EleanorModBootstrap.LoadFromEnvironment();' + [Environment]::NewLine + '            '
    $program = $program.Insert($initIndex, $addition)
    [IO.File]::WriteAllText($programPath, $program, [Text.UTF8Encoding]::new($false))
}

$eleanorProbeMarker = 'if (Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_PROBE") == "1")'
$eleanorProbeAnchor = 'payload["root"] = OfflineCombat.DescribeRoot(combat!);'
if (-not $program.Contains($eleanorProbeMarker, [StringComparison]::Ordinal)) {
    $rootIndex = $program.IndexOf($eleanorProbeAnchor, [StringComparison]::Ordinal)
    if ($rootIndex -lt 0) { throw 'Pinned offline harness source has no recognized root-capture insertion point for Eleanor.' }
    $insertAt = $rootIndex + $eleanorProbeAnchor.Length
    $addition = [Environment]::NewLine + '            if (Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_PROBE") == "1")' + [Environment]::NewLine + '                payload["eleanorProbe"] = EleanorCombatProbe.Run(combat!, loop, options.OutputDirectory);'
    $program = $program.Insert($insertAt, $addition)
    [IO.File]::WriteAllText($programPath, $program, [Text.UTF8Encoding]::new($false))
}
$fullBattleProbeMarker = 'if (Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_FULL_BATTLE") == "1")'
$fullBattleProbeAnchor = 'payload["eleanorProbe"] = EleanorCombatProbe.Run(combat!, loop, options.OutputDirectory);'
if (-not $program.Contains($fullBattleProbeMarker, [StringComparison]::Ordinal)) {
    if (-not $program.Contains($fullBattleProbeAnchor, [StringComparison]::Ordinal)) { throw 'Pinned offline harness has no Eleanor probe insertion point for the full-battle validator.' }
    $insertAt = $program.IndexOf($fullBattleProbeAnchor, [StringComparison]::Ordinal) + $fullBattleProbeAnchor.Length
    $addition = [Environment]::NewLine + '            if (Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_FULL_BATTLE") == "1")' + [Environment]::NewLine + '                payload["eleanorFullBattle"] = EleanorCombatProbe.Run(combat!, loop, options.OutputDirectory);'
    $program = $program.Insert($insertAt, $addition)
    [IO.File]::WriteAllText($programPath, $program, [Text.UTF8Encoding]::new($false))
}

$runLoopArmCallMarker = 'NativeRunLoopProbe.Arm();'
if (-not $program.Contains($runLoopArmCallMarker, [StringComparison]::Ordinal)) {
    $rootIndex = $program.IndexOf($nativeAnchor, [StringComparison]::Ordinal)
    if ($rootIndex -lt 0) { throw 'Pinned offline harness has no recognized root-capture point for the run-loop probe.' }
    $insertAt = $rootIndex + $nativeAnchor.Length
    $addition = [Environment]::NewLine + '            if (Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_RUNLOOP") == "1")' + [Environment]::NewLine + '                NativeRunLoopProbe.Arm();'
    $program = $program.Insert($insertAt, $addition)
    [IO.File]::WriteAllText($programPath, $program, [Text.UTF8Encoding]::new($false))
}

$gameBootstrapPath = Join-Path $harnessSourceRoot 'GameBootstrap.cs'
$gameBootstrap = [IO.File]::ReadAllText($gameBootstrapPath, [Text.Encoding]::UTF8)
$modTypeMarker = 'EleanorModBootstrap.RegisterReflectionTypesAfterHarnessReset();'
$modTypeAnchor = 'AccessTools.Field(typeof(ReflectionHelper), "_modTypes").SetValue(null, Array.Empty<Type>());'
if (-not $gameBootstrap.Contains($modTypeMarker, [StringComparison]::Ordinal)) {
    $registryIndex = $gameBootstrap.IndexOf($modTypeAnchor, [StringComparison]::Ordinal)
    if ($registryIndex -lt 0) { throw 'Pinned harness bootstrap has no recognized Mod type-registry reset point.' }
    $insertAt = $registryIndex + $modTypeAnchor.Length
    $addition = [Environment]::NewLine + '        if (Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD") == "1")' + [Environment]::NewLine + '            EleanorModBootstrap.RegisterReflectionTypesAfterHarnessReset();'
    $gameBootstrap = $gameBootstrap.Insert($insertAt, $addition)
    [IO.File]::WriteAllText($gameBootstrapPath, $gameBootstrap, [Text.UTF8Encoding]::new($false))
}
$modelInitMarker = 'ModelDb.Init(EleanorModBootstrap.IncludeModels(AbstractModelSubtypes.All.ToArray()));'
$modelInitAnchor = 'ModelDb.Init(AbstractModelSubtypes.All.ToArray());'
if (-not $gameBootstrap.Contains($modelInitMarker, [StringComparison]::Ordinal)) {
    if (-not $gameBootstrap.Contains($modelInitAnchor, [StringComparison]::Ordinal)) { throw 'Pinned harness has no recognized ModelDb initialization line.' }
    $gameBootstrap = $gameBootstrap.Replace($modelInitAnchor, $modelInitMarker)
    [IO.File]::WriteAllText($gameBootstrapPath, $gameBootstrap, [Text.UTF8Encoding]::new($false))
}

$offlineCombatPath = Join-Path $harnessSourceRoot 'OfflineCombat.cs'
$offlineCombat = [IO.File]::ReadAllText($offlineCombatPath, [Text.Encoding]::UTF8)
$loadoutSetupMarker = 'EleanorModBootstrap.ConfigureValidationLoadout(runState);'
$loadoutSetupAnchor = '        await RunManager.Instance.FinalizeStartingRelics();'
if (-not $offlineCombat.Contains($loadoutSetupMarker, [StringComparison]::Ordinal)) {
    if (-not $offlineCombat.Contains($loadoutSetupAnchor, [StringComparison]::Ordinal)) { throw 'Pinned harness has no recognized post-relic setup point for the complete-orb fixture.' }
    $offlineCombat = $offlineCombat.Replace($loadoutSetupAnchor, $loadoutSetupAnchor + [Environment]::NewLine + '        await EleanorModBootstrap.ConfigureValidationLoadout(runState);')
    [IO.File]::WriteAllText($offlineCombatPath, $offlineCombat, [Text.UTF8Encoding]::new($false))
}
$characterResolveAnchor = 'ResolveUnique(ModelDb.AllCharacters, scenario.CharacterId,'
$characterResolveReplacement = 'ResolveUnique(ModelDb.All.OfType<CharacterModel>(), scenario.CharacterId,'
if (-not $offlineCombat.Contains($characterResolveReplacement, [StringComparison]::Ordinal)) {
    if (-not $offlineCombat.Contains($characterResolveAnchor, [StringComparison]::Ordinal)) { throw 'Pinned harness has no recognized character resolver line.' }
    $offlineCombat = $offlineCombat.Replace($characterResolveAnchor, $characterResolveReplacement)
    [IO.File]::WriteAllText($offlineCombatPath, $offlineCombat, [Text.UTF8Encoding]::new($false))
}

$runLoopRoomMarker = 'if (Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_RUNLOOP") == "1")'
if (-not $offlineCombat.Contains($runLoopRoomMarker, [StringComparison]::Ordinal)) {
    $roomAnchor = '        await RunManager.Instance.EnterRoomDebug('
    $roomIndex = $offlineCombat.IndexOf($roomAnchor, [StringComparison]::Ordinal)
    if ($roomIndex -lt 0) { throw 'Pinned offline harness has no recognized encounter-room entry point for the map-start route.' }
    $endCall = $offlineCombat.IndexOf('            mutableEncounter);', $roomIndex, [StringComparison]::Ordinal)
    if ($endCall -lt 0) { throw 'Pinned offline harness has no recognized end of encounter-room entry call.' }
    $endCall += '            mutableEncounter);'.Length
    $replacement = @'
        if (Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_RUNLOOP") == "1")
        {
            ActMap map = runState.Map ?? throw new InvalidOperationException("Run-loop probe needs the native generated map.");
            MapPoint start = map.startMapPoints.OrderBy(point => point.coord.col).FirstOrDefault()
                ?? throw new InvalidOperationException("Generated map has no starting point.");
            if (start.PointType != MapPointType.Monster)
                throw new InvalidOperationException($"Expected a Monster start node for the fixed M3 encounter, found {start.PointType}.");
            await RunManager.Instance.EnterMapCoordDebug(start.coord, RoomType.Monster, MapPointType.Monster,
                mutableEncounter, showTransition: false);
            HarnessLog.Trace("enter_map_start_debug");
        }
        else
        {
            await RunManager.Instance.EnterRoomDebug(
                RoomType.Monster,
                MapPointType.Unassigned,
                mutableEncounter);
            HarnessLog.Trace("enter_room_debug");
        }
'@
    $offlineCombat = $offlineCombat.Substring(0, $roomIndex) + $replacement + $offlineCombat.Substring($endCall)
    [IO.File]::WriteAllText($offlineCombatPath, $offlineCombat, [Text.UTF8Encoding]::new($false))
}

$stubDir = Join-Path $labRoot '.build\local-reference\NoOpMemoryCleaner'
$stubProject = Join-Path $stubDir 'NoOpMemoryCleaner.csproj'
if (-not (Test-Path -LiteralPath $stubProject)) {
    New-Item -ItemType Directory -Path $stubDir -Force | Out-Null
    Set-Content -LiteralPath $stubProject -Encoding utf8 -Value '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>'
}

$solverDll = Join-Path $cacheRoot '.godot\mono\temp\bin\Release\CombatSolver.dll'
$harnessProject = Join-Path $harnessSourceRoot 'OfflineSearchHarness.csproj'
$harnessDll = Join-Path $harnessSourceRoot 'bin\Release\net9.0\OfflineSearchHarness.dll'
$labCoreProject = Join-Path $labRoot 'src\SimulatorLab.Core\SimulatorLab.Core.csproj'
[xml]$harnessXml = Get-Content -LiteralPath $harnessProject -Raw
if (-not ($harnessXml.Project.ItemGroup.ProjectReference | Where-Object { $_.Include -eq $labCoreProject })) {
    $referenceGroup = $harnessXml.CreateElement('ItemGroup')
    $reference = $harnessXml.CreateElement('ProjectReference')
    $reference.SetAttribute('Include', $labCoreProject)
    $referenceGroup.AppendChild($reference) | Out-Null
    $harnessXml.DocumentElement.AppendChild($referenceGroup) | Out-Null
    $harnessXml.Save($harnessProject)
}
$commonProperties = @(
    "-p:Sts2Dir=$gamePathFull",
    "-p:Sts2DataDir=$dataDir",
    "-p:RitsuWorkshopRoot=$RitsuWorkshopRoot",
    "-p:RitsuLibDir=$ritsuLibDir",
    "-p:GodotPath=$GodotPath",
    '-p:CopyModOnBuild=false',
    '-p:DeployToGame=false',
    '-p:RitsuLibAutoCopy=false',
    '-p:MSBuildEnableWorkloadResolver=false',
    '-p:NuGetAudit=false',
    '-p:RestoreIgnoreFailedSources=true'
)

& dotnet build $solverProject --configuration Release --verbosity minimal @commonProperties "-p:MemoryCleanerProject=$stubProject"
if ($LASTEXITCODE -ne 0) { throw "CombatSolver build failed with exit code $LASTEXITCODE." }
& dotnet build $harnessProject --configuration Release --verbosity minimal @commonProperties "-p:CombatSolverRepositoryRoot=$cacheRoot" "-p:CombatSolverDll=$solverDll"
if ($LASTEXITCODE -ne 0) { throw "Offline harness build failed with exit code $LASTEXITCODE." }

$runId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$runRoot = Join-Path $labRoot ".build\runs\vanilla-m1-$runId"
$nativeOut = Join-Path $runRoot 'native-game-action'
$forkOut = Join-Path $runRoot 'fork-isolation'
$firstOut = Join-Path $runRoot 'replay-a'
$secondOut = Join-Path $runRoot 'replay-b'
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$scenarioArgs = @('--character', 'IRONCLAD', '--encounter', 'FUZZY_WURM_CRAWLER_WEAK', '--seed', 'LAB_V0111_IRONCLAD_M1_01', '--profile', 'Low', '--beam', '8', '--nodes', '1000', '--dop', '1', '--budget-ms', '5000', '--search-mode', 'Evaluate')

$env:SIMULATORLAB_NATIVE_GAME_ACTION_PROBE = '1'
try {
    & dotnet $harnessDll @scenarioArgs --milestone M1 --label vanilla-native-action --out $nativeOut
    if ($LASTEXITCODE -ne 0) { throw "Native game action differential failed with exit code $LASTEXITCODE." }
} finally {
    Remove-Item Env:SIMULATORLAB_NATIVE_GAME_ACTION_PROBE -ErrorAction SilentlyContinue
}
$native = Get-Content -LiteralPath (Join-Path $nativeOut 'native-game-action-differential.json') -Raw | ConvertFrom-Json
& dotnet run --project (Join-Path $labRoot 'tests\SimulatorLab.Core.Checks\SimulatorLab.Core.Checks.csproj') --configuration Release -- --validate-native-differential (Join-Path $nativeOut 'native-game-action-differential.json')
if ($LASTEXITCODE -ne 0) { throw "Native differential schema validation failed with exit code $LASTEXITCODE." }
if ($native.schema -ne 'native-game-action-differential/v1' -or
    $native.gameVersion -ne 'v0.111.0' -or $native.gameCommit -ne '41cef1ea' -or
    $native.mainAssemblyHash -ne 222455745 -or $native.characterModelId -ne 'IRONCLAD' -or
    $native.action.cardInstanceId -notlike '*STRIKE_IRONCLAD' -or
    $native.action.cardHandIndex -lt 0 -or
    -not $native.testMode -or $native.steamInitialized -or
    $native.saveStore -notlike '*MemorySaveStore' -or $native.myModLoaded -or
    -not $native.usesPresentationClockShim -or
    $native.nativeRoot.sha256 -ne $native.simulatorRoot.sha256 -or
    $native.simulatorAfter.sha256 -ne $native.nativeAfter.sha256 -or
    $native.differences.Count -ne 0) {
    throw 'Native game action differential report failed its pinned-build, isolation, or state-equality assertions.'
}

$env:OFFLINE_HARNESS_FORK_ISOLATION_CHECKS = '1'
try {
    & dotnet $harnessDll @scenarioArgs --milestone M1 --label vanilla-fork --out $forkOut
    if ($LASTEXITCODE -ne 0) { throw "Fork isolation check failed with exit code $LASTEXITCODE." }
} finally {
    Remove-Item Env:OFFLINE_HARNESS_FORK_ISOLATION_CHECKS -ErrorAction SilentlyContinue
}

$env:SIMULATORLAB_NATIVE_BATTLE_TRACE = '1'
try {
    & dotnet $harnessDll @scenarioArgs --milestone M2 --label vanilla-replay-a-trace --out $firstOut
    if ($LASTEXITCODE -ne 0) { throw "Full vanilla battle differential failed with exit code $LASTEXITCODE." }
} finally {
    Remove-Item Env:SIMULATORLAB_NATIVE_BATTLE_TRACE -ErrorAction SilentlyContinue
}
& dotnet $harnessDll @scenarioArgs --milestone M2 --label vanilla-replay-b --out $secondOut
if ($LASTEXITCODE -ne 0) { throw "Second offline combat run failed with exit code $LASTEXITCODE." }

$first = Get-Content -LiteralPath (Join-Path $firstOut 'harness-result.json') -Raw | ConvertFrom-Json
$second = Get-Content -LiteralPath (Join-Path $secondOut 'harness-result.json') -Raw | ConvertFrom-Json
$samePlan = (ConvertTo-Json -InputObject $first.search.planActions -Depth 100 -Compress) -ceq (ConvertTo-Json -InputObject $second.search.planActions -Depth 100 -Compress)
$sameRoot = $first.search.rootContinuationStamp -ceq $second.search.rootContinuationStamp -and
            $first.search.rootLiveStamp -ceq $second.search.rootLiveStamp -and
            (Get-Content -LiteralPath (Join-Path $firstOut 'root-diagnostics.txt') -Raw) -ceq (Get-Content -LiteralPath (Join-Path $secondOut 'root-diagnostics.txt') -Raw)
$sameRoute = (Get-Content -LiteralPath (Join-Path $firstOut 'route.json') -Raw) -ceq (Get-Content -LiteralPath (Join-Path $secondOut 'route.json') -Raw)
if (-not ($samePlan -and $sameRoot -and $sameRoute -and $first.search.solverMetrics.totalExpanded -eq $second.search.solverMetrics.totalExpanded -and $first.search.solverMetrics.totalTransitions -eq $second.search.solverMetrics.totalTransitions)) {
    throw 'The two generated runs differ in root, route, plan, or expansion counts.'
}

$battle = Get-Content -LiteralPath (Join-Path $firstOut 'vanilla-battle-replay.json') -Raw | ConvertFrom-Json
$randomTargetArgs = @('--character', 'IRONCLAD', '--encounter', 'CULTISTS_NORMAL', '--seed', 'LAB_V0111_IRONCLAD_TARGETS_01', '--profile', 'Low', '--beam', '8', '--nodes', '1000', '--dop', '1', '--budget-ms', '5000', '--search-mode', 'Evaluate')
$randomTargetAOut = Join-Path $runRoot 'vanilla-random-target-a'
$randomTargetBOut = Join-Path $runRoot 'vanilla-random-target-b'
$env:SIMULATORLAB_NATIVE_RANDOM_TARGET = '1'
try {
    & dotnet $harnessDll @randomTargetArgs --milestone M2 --label vanilla-two-enemy-random-target-a --out $randomTargetAOut
    if ($LASTEXITCODE -ne 0) { throw "Two-enemy random-target action A failed with exit code $LASTEXITCODE." }
    & dotnet $harnessDll @randomTargetArgs --milestone M2 --label vanilla-two-enemy-random-target-b --out $randomTargetBOut
    if ($LASTEXITCODE -ne 0) { throw "Two-enemy random-target action B failed with exit code $LASTEXITCODE." }
} finally {
    Remove-Item Env:SIMULATORLAB_NATIVE_RANDOM_TARGET -ErrorAction SilentlyContinue
}
$randomA = Get-Content -LiteralPath (Join-Path $randomTargetAOut 'vanilla-random-target-differential.json') -Raw | ConvertFrom-Json
$randomB = Get-Content -LiteralPath (Join-Path $randomTargetBOut 'vanilla-random-target-differential.json') -Raw | ConvertFrom-Json
$sameRandomTarget = $randomA.schema -eq 'vanilla-random-target-differential/v1' -and
    $randomB.schema -eq 'vanilla-random-target-differential/v1' -and
    $randomA.nativeAfterRootDigest -ceq $randomB.nativeAfterRootDigest -and
    $randomA.simulatorAfterRootDigest -ceq $randomB.simulatorAfterRootDigest -and
    (ConvertTo-Json -InputObject $randomA.nativeTargetsDamaged -Depth 10 -Compress) -ceq (ConvertTo-Json -InputObject $randomB.nativeTargetsDamaged -Depth 10 -Compress) -and
    $randomA.targetRng.drawCountAfter -eq $randomB.targetRng.drawCountAfter -and
    $randomA.targetRng.stateHexAfter -ceq $randomB.targetRng.stateHexAfter
if ($battle.schema -ne 'vanilla-battle-replay/v1' -or
    $battle.gameVersion -ne 'v0.111.0' -or $battle.gameCommit -ne '41cef1ea' -or
    $battle.mainAssemblyHash -ne 222455745 -or $battle.characterModelId -ne 'IRONCLAD' -or
    -not $battle.testMode -or $battle.steamInitialized -or
    $battle.saveStore -notlike '*MemorySaveStore' -or $battle.myModLoaded -or
    -not $battle.usesPresentationClockShim -or
    -not $battle.headlessUiCleanupSuppressed -or $battle.replayFileWritesSuppressed -lt 1 -or
    $battle.steps.Count -lt 8 -or $battle.coverage.cardActions -lt 6 -or $battle.coverage.endedTurns -lt 2 -or
    -not $battle.terminal.victoryTeardownObserved -or
    $battle.terminal.outcome -ne 'Victory' -or
    $battle.terminal.terminalSha256.simulator -ne $battle.terminal.terminalSha256.native -or
    -not $battle.coverage.shuffle -or -not $battle.coverage.enemyAction -or
    -not $battle.coverage.powers -or -not $battle.coverage.relicState -or
    $battle.coverage.powerModelIdsAfterBash.Count -lt 1 -or
    @($battle.steps | Where-Object { -not $_.exactMatch -or $_.afterNativeRootDigest -ne $_.afterSimulatorRootDigest -or $_.simulatorStateText -ne $_.nativeStateText }).Count -ne 0 -or
    -not $sameRandomTarget -or $randomA.cardModelId -ne 'SWORD_BOOMERANG' -or $randomA.encounterModelId -ne 'CULTISTS_NORMAL' -or
    $randomA.targetType -notlike '*Random*' -or $randomA.validEnemyTargets -lt 2 -or
    $randomA.targetRng.drawCountAfter -le $randomA.targetRng.drawCountBefore -or
    @($randomA.nativeTargetsDamaged).Count -lt 1 -or
    (ConvertTo-Json -InputObject $randomA.nativeTargetsDamaged -Depth 10 -Compress) -cne (ConvertTo-Json -InputObject $randomA.predictedTargetsDamaged -Depth 10 -Compress) -or
    -not $randomA.testMode -or $randomA.steamInitialized -or $randomA.saveStore -notlike '*MemorySaveStore' -or $randomA.myModLoaded -or
    -not $randomA.exactStateMatch -or $randomA.nativeAfterRootDigest -cne $randomA.simulatorAfterRootDigest -or
    $randomA.nativeAfterStateText -cne $randomA.simulatorAfterStateText) {
    throw 'Full vanilla battle trace failed its pin, isolation, per-action equality, or coverage assertions.'
}

Write-Output "PASS pinned game build: $($releaseInfo.version) / $($releaseInfo.commit)"
Write-Output "PASS native game-core Strike differential: $($native.characterModelId)/$($native.action.cardInstanceId) targetIndex=$($native.action.cardHandIndex); TestMode=$($native.testMode); Steam=$($native.steamInitialized); SaveStore=$($native.saveStore); MyMod=$($native.myModLoaded); clockShim=$($native.usesPresentationClockShim); exactAfter=$($native.simulatorAfter.sha256 -eq $native.nativeAfter.sha256)"
Write-Output "PASS full native battle route: steps=$($battle.steps.Count) outcome=$($battle.terminal.outcome) turns=$($battle.coverage.endedTurns) shuffle=$($battle.coverage.shuffle) enemyAction=$($battle.coverage.enemyAction) power=$($battle.coverage.powers) relic=$($battle.coverage.relicState) exactEveryStep=$(@($battle.steps | Where-Object { -not $_.exactMatch }).Count -eq 0)"
Write-Output "PASS vanilla random-target differential: card=$($randomA.cardModelId) targets=$($randomA.validEnemyTargets) TargetRngDraws=$($randomA.targetRng.drawCountAfter - $randomA.targetRng.drawCountBefore) exactAfter=$($randomA.nativeAfterRootDigest -ceq $randomA.simulatorAfterRootDigest) deterministic=$sameRandomTarget"
Write-Output "PASS fork isolation; deterministic root and route; expanded=$($first.search.solverMetrics.totalExpanded) transitions=$($first.search.solverMetrics.totalTransitions)"
Write-Output "Results are in the ignored local directory: $runRoot"
Write-Output 'The native action comparison runs pinned game combat code in the in-memory offline harness; it is not a retail executable/UI capture.'

$eleanorOut = Join-Path $runRoot 'eleanor-integration'
$completeOrbOut = Join-Path $runRoot 'eleanor-complete-orb'
$crossTurnOut = Join-Path $runRoot 'eleanor-mentor-cross-turn'
$manaToShieldOut = Join-Path $runRoot 'eleanor-mana-to-shield'
$fullBattleAOut = Join-Path $runRoot 'eleanor-full-battle-a'
$fullBattleBOut = Join-Path $runRoot 'eleanor-full-battle-b'
$eleanorArgs = @('--character', 'MyMod', '--encounter', 'FUZZY_WURM_CRAWLER_WEAK', '--seed', 'LAB_V0111_ELEANOR_M1_01', '--profile', 'Low', '--beam', '8', '--nodes', '1000', '--dop', '1', '--budget-ms', '5000', '--search-mode', 'Evaluate', '--milestone', 'M1', '--label', 'eleanor-runtime-adapter', '--out', $eleanorOut)
$completeOrbArgs = @($eleanorArgs)
$completeOrbArgs[[Array]::IndexOf([string[]]$completeOrbArgs, '--label') + 1] = 'eleanor-complete-orb'
$completeOrbArgs[[Array]::IndexOf([string[]]$completeOrbArgs, '--out') + 1] = $completeOrbOut
$crossTurnArgs = @($completeOrbArgs)
$crossTurnArgs[[Array]::IndexOf([string[]]$crossTurnArgs, '--label') + 1] = 'eleanor-mentor-cross-turn'
$crossTurnArgs[[Array]::IndexOf([string[]]$crossTurnArgs, '--out') + 1] = $crossTurnOut
$manaToShieldArgs = @($completeOrbArgs)
$manaToShieldArgs[[Array]::IndexOf([string[]]$manaToShieldArgs, '--label') + 1] = 'eleanor-mana-to-shield'
$manaToShieldArgs[[Array]::IndexOf([string[]]$manaToShieldArgs, '--out') + 1] = $manaToShieldOut
$fullBattleAArgs = @($completeOrbArgs)
$fullBattleAArgs[[Array]::IndexOf([string[]]$fullBattleAArgs, '--label') + 1] = 'eleanor-full-battle-a'
$fullBattleAArgs[[Array]::IndexOf([string[]]$fullBattleAArgs, '--out') + 1] = $fullBattleAOut
$fullBattleBArgs = @($fullBattleAArgs)
$fullBattleBArgs[[Array]::IndexOf([string[]]$fullBattleBArgs, '--label') + 1] = 'eleanor-full-battle-b'
$fullBattleBArgs[[Array]::IndexOf([string[]]$fullBattleBArgs, '--out') + 1] = $fullBattleBOut
$env:SIMULATORLAB_ELEANOR_MOD = '1'
$env:SIMULATORLAB_ELEANOR_PROBE = '1'
$env:SIMULATORLAB_ELEANOR_MOD_DLL = $myModDll
$env:SIMULATORLAB_ELEANOR_BASELIB_DLL = $baseLibDll
$env:SIMULATORLAB_ELEANOR_MANIFEST = $myModManifestPath
$env:SIMULATORLAB_ELEANOR_MOD_VERSION = $myModVersion
try {
    & dotnet $harnessDll @eleanorArgs
    $eleanorExitCode = $LASTEXITCODE
    $env:SIMULATORLAB_ELEANOR_COMPLETE_ORB = '1'
    & dotnet $harnessDll @completeOrbArgs
    $completeOrbExitCode = $LASTEXITCODE
    $env:SIMULATORLAB_ELEANOR_CROSS_TURN = '1'
    & dotnet $harnessDll @crossTurnArgs
    $crossTurnExitCode = $LASTEXITCODE
    Remove-Item Env:SIMULATORLAB_ELEANOR_CROSS_TURN -ErrorAction SilentlyContinue
    $env:SIMULATORLAB_ELEANOR_MANA_TO_SHIELD = '1'
    & dotnet $harnessDll @manaToShieldArgs
    $manaToShieldExitCode = $LASTEXITCODE
    Remove-Item Env:SIMULATORLAB_ELEANOR_PROBE -ErrorAction SilentlyContinue
    Remove-Item Env:SIMULATORLAB_ELEANOR_MANA_TO_SHIELD -ErrorAction SilentlyContinue
    $env:SIMULATORLAB_ELEANOR_FULL_BATTLE = '1'
    & dotnet $harnessDll @fullBattleAArgs
    $fullBattleAExitCode = $LASTEXITCODE
    & dotnet $harnessDll @fullBattleBArgs
    $fullBattleBExitCode = $LASTEXITCODE
} finally {
    Remove-Item Env:SIMULATORLAB_ELEANOR_MOD -ErrorAction SilentlyContinue
    Remove-Item Env:SIMULATORLAB_ELEANOR_PROBE -ErrorAction SilentlyContinue
    Remove-Item Env:SIMULATORLAB_ELEANOR_MOD_DLL -ErrorAction SilentlyContinue
    Remove-Item Env:SIMULATORLAB_ELEANOR_BASELIB_DLL -ErrorAction SilentlyContinue
    Remove-Item Env:SIMULATORLAB_ELEANOR_MANIFEST -ErrorAction SilentlyContinue
    Remove-Item Env:SIMULATORLAB_ELEANOR_MOD_VERSION -ErrorAction SilentlyContinue
    Remove-Item Env:SIMULATORLAB_ELEANOR_COMPLETE_ORB -ErrorAction SilentlyContinue
    Remove-Item Env:SIMULATORLAB_ELEANOR_CROSS_TURN -ErrorAction SilentlyContinue
    Remove-Item Env:SIMULATORLAB_ELEANOR_MANA_TO_SHIELD -ErrorAction SilentlyContinue
    Remove-Item Env:SIMULATORLAB_ELEANOR_FULL_BATTLE -ErrorAction SilentlyContinue
}
$eleanorResultPath = Join-Path $eleanorOut 'harness-result.json'
$completeOrbResultPath = Join-Path $completeOrbOut 'harness-result.json'
$crossTurnResultPath = Join-Path $crossTurnOut 'harness-result.json'
$manaToShieldResultPath = Join-Path $manaToShieldOut 'harness-result.json'
$fullBattleAResultPath = Join-Path $fullBattleAOut 'harness-result.json'
$fullBattleBResultPath = Join-Path $fullBattleBOut 'harness-result.json'
$eleanorStatus = 'failed'
$eleanorReason = 'no machine-readable differential passed'
$completeOrbStatus = 'failed'
$completeOrbReason = 'no machine-readable complete-orb differential passed'
$completeOrb = $null
$manaToShieldStatus = 'failed'
$manaToShieldReason = 'no machine-readable ManaToShield differential passed'
if (Test-Path -LiteralPath $manaToShieldResultPath) {
    $manaToShield = Get-Content -LiteralPath $manaToShieldResultPath -Raw | ConvertFrom-Json
    $shieldProbe = $manaToShield.eleanorProbe
    $shieldTransition = $shieldProbe.manaToShieldTransition
    $shieldStrike = $shieldProbe.manaStrikeFollowUp
    if ($manaToShieldExitCode -eq 0 -and $shieldProbe.completeOrbScenario -and $shieldProbe.manaToShieldScenario -and
        $shieldProbe.opening.completeOrb -and $shieldTransition -and $shieldTransition.attempted -and
        $shieldTransition.actualEnergyPaid -eq 2 -and $shieldTransition.livePowerAmount -eq 3 -and
        $shieldTransition.simulatedPowerAmount -eq 3 -and $shieldTransition.exactStateMatch -and
        $shieldStrike -and $shieldStrike.attempted -and $shieldStrike.exactStateMatch -and
        $shieldStrike.nativeBlockAfterStrike - $shieldStrike.nativeBlockBeforeStrike -eq 3 -and
        $shieldStrike.simulatedBlockAfterStrike - $shieldStrike.nativeBlockAfterStrike -eq 0 -and
        $shieldStrike.liveManaToShieldAmount -eq 3 -and $shieldStrike.branchForkIsolation) {
        $manaToShieldStatus = 'passed'
        $manaToShieldReason = 'Native ManaToShield installation and one ManaStrike mana debit yielded the same unpowered block in native and simulator states'
        Write-Output "PASS M2 Eleanor ManaToShield interaction: installExact=$($shieldTransition.exactStateMatch) amount=$($shieldTransition.livePowerAmount) manaStrikeBlock=$($shieldStrike.nativeBlockAfterStrike - $shieldStrike.nativeBlockBeforeStrike) exactStrike=$($shieldStrike.exactStateMatch)"
    } else {
        $manaToShieldReason = if ($shieldTransition.simulationGap) { $shieldTransition.simulationGap } elseif ($shieldProbe.simulationGap) { $shieldProbe.simulationGap } elseif ($manaToShield.error.message) { $manaToShield.error.message } elseif ($shieldStrike.simulationGap) { $shieldStrike.simulationGap } else { 'ManaToShield installation or payment-triggered block failed an exact-state assertion' }
        if ($manaToShield.error.type -like '*PredictionUnsupportedException' -or $shieldProbe.simulationGap -or $shieldTransition.simulationGap -or $shieldStrike.simulationGap) { $manaToShieldStatus = 'unsupported' }
        Write-Output "GAP M2 Eleanor ManaToShield interaction: status=$manaToShieldStatus exit=$manaToShieldExitCode reached=$($manaToShield.reachedMilestone) reason=$manaToShieldReason"
        Write-Output "Eleanor ManaToShield details: $manaToShieldResultPath"
    }
} else {
    $manaToShieldReason = "no harness result (exit=$manaToShieldExitCode)"
    Write-Output "GAP M2 Eleanor ManaToShield interaction: status=$manaToShieldStatus reason=$manaToShieldReason"
}
$fullBattleStatus = 'failed'
$fullBattleReason = 'no machine-readable full-battle replay passed'
if ((Test-Path -LiteralPath $fullBattleAResultPath) -and (Test-Path -LiteralPath $fullBattleBResultPath)) {
    $fullBattleA = (Get-Content -LiteralPath $fullBattleAResultPath -Raw | ConvertFrom-Json).eleanorFullBattle
    $fullBattleB = (Get-Content -LiteralPath $fullBattleBResultPath -Raw | ConvertFrom-Json).eleanorFullBattle
    $fullBattleAExact = $fullBattleA.schema -eq 'eleanor-full-battle-differential/v1' -and
        $fullBattleA.gameVersion -eq 'v0.111.0' -and $fullBattleA.gameCommit -eq '41cef1ea' -and
        $fullBattleA.modVersion -eq $myModVersion -and $fullBattleA.testMode -and
        -not $fullBattleA.steamInitialized -and $fullBattleA.saveStore -like '*MemorySaveStore' -and
        -not $fullBattleA.persistentSaveWrites -and $fullBattleA.progressSaveCallsSuppressed -ge 1 -and $fullBattleA.opening.Mana -eq 7 -and
        $fullBattleA.opening.CompleteOrb -and $fullBattleA.openingRootDigest -and
        $fullBattleA.rootRegistrationGuard.supportedRegistrationRejected -and
        $fullBattleA.rootRegistrationGuard.stampUnchangedAfterRejectedRegistration -and
        $fullBattleA.rootRegistrationGuard.registrationMutationMadeCapturedRootStale -and
        $fullBattleA.rootRegistrationGuard.stampRestored -and
        $fullBattleA.turnStartRegistrationGuard.lateRegistrationRejected -and
        $fullBattleA.turnStartRegistrationGuard.rejectedRegistrationStampStable -and
        $fullBattleA.turnStartRegistrationGuard.capturedRootStaleAfterRegistryMutation -and
        $fullBattleA.turnStartRegistrationGuard.stampRestored -and
        $fullBattleA.turnStartPatchGuard.rootCaptureRejected -and
        $fullBattleA.turnStartPatchGuard.capturedRootMatchesAfterRestore -and
        $fullBattleA.turnCount -ge 2 -and $fullBattleA.turnBoundary.exactStateMatch -and
        $fullBattleA.turnBoundary.secondTurnCardsPresent -and $fullBattleA.parentBranchStateUnchanged -and
        $fullBattleA.exactEveryStep -and $fullBattleA.steps.Count -eq 4 -and
        @($fullBattleA.steps | Where-Object { -not $_.exactMatch }).Count -eq 0 -and
        $fullBattleA.terminal.nativeVictory -and $fullBattleA.terminal.simulatorVictory -eq 'Victory' -and
        $fullBattleA.terminal.terminalRootMatches -and $fullBattleA.terminal.terminalContinuationMatches
    $fullBattleBExact = $fullBattleB.schema -eq 'eleanor-full-battle-differential/v1' -and
        $fullBattleB.rootRegistrationGuard.supportedRegistrationRejected -and
        $fullBattleB.rootRegistrationGuard.stampUnchangedAfterRejectedRegistration -and
        $fullBattleB.rootRegistrationGuard.registrationMutationMadeCapturedRootStale -and
        $fullBattleB.rootRegistrationGuard.stampRestored -and
        $fullBattleB.turnStartRegistrationGuard.lateRegistrationRejected -and
        $fullBattleB.turnStartRegistrationGuard.rejectedRegistrationStampStable -and
        $fullBattleB.turnStartRegistrationGuard.capturedRootStaleAfterRegistryMutation -and
        $fullBattleB.turnStartRegistrationGuard.stampRestored -and
        $fullBattleB.turnStartPatchGuard.rootCaptureRejected -and
        $fullBattleB.turnStartPatchGuard.capturedRootMatchesAfterRestore -and
        $fullBattleB.turnCount -ge 2 -and $fullBattleB.turnBoundary.exactStateMatch -and
        $fullBattleB.turnBoundary.secondTurnCardsPresent -and $fullBattleB.parentBranchStateUnchanged -and
        $fullBattleB.exactEveryStep -and $fullBattleB.steps.Count -eq 4 -and
        @($fullBattleB.steps | Where-Object { -not $_.exactMatch }).Count -eq 0 -and
        -not $fullBattleB.persistentSaveWrites -and $fullBattleB.progressSaveCallsSuppressed -ge 1 -and
        $fullBattleB.terminal.nativeVictory -and $fullBattleB.terminal.simulatorVictory -eq 'Victory' -and
        $fullBattleB.terminal.terminalRootMatches -and $fullBattleB.terminal.terminalContinuationMatches
    $fullBattleRouteRepeatable = (ConvertTo-Json -InputObject $fullBattleA.route -Depth 8 -Compress) -ceq
        (ConvertTo-Json -InputObject $fullBattleB.route -Depth 8 -Compress) -and
        $fullBattleA.openingRootDigest -ceq $fullBattleB.openingRootDigest -and
        $fullBattleA.terminal.nativeRootDigest -ceq $fullBattleB.terminal.nativeRootDigest -and
        (ConvertTo-Json -InputObject @($fullBattleA.steps | ForEach-Object { $_.NativeAfterDigest }) -Depth 8 -Compress) -ceq
        (ConvertTo-Json -InputObject @($fullBattleB.steps | ForEach-Object { $_.NativeAfterDigest }) -Depth 8 -Compress)
    if ($fullBattleAExitCode -eq 0 -and $fullBattleBExitCode -eq 0 -and $fullBattleAExact -and $fullBattleBExact -and $fullBattleRouteRepeatable) {
        $fullBattleStatus = 'passed'
        $fullBattleReason = 'Two same-seed two-turn native/simulator runs matched at the turn boundary, all four ManaStrike boundaries, and terminal Victory'
        Write-Output "PASS M2 Eleanor repeatable two-turn full battle: turns=$($fullBattleA.turnCount) boundaryExact=$($fullBattleA.turnBoundary.exactStateMatch) route=$($fullBattleA.route -join ' -> ') terminalRoot=$($fullBattleA.terminal.nativeRootDigest) repeatable=$fullBattleRouteRepeatable"
    } else {
        $fullBattleReason = if ($fullBattleA.error.message) { $fullBattleA.error.message } elseif ($fullBattleB.error.message) { $fullBattleB.error.message } else { 'One replay diverged, failed terminal Victory, or differed between repeated same-seed runs' }
        if ($fullBattleA.simulationGap -or $fullBattleB.simulationGap) { $fullBattleStatus = 'unsupported' }
        Write-Output "GAP M2 Eleanor full battle: status=$fullBattleStatus exits=$fullBattleAExitCode/$fullBattleBExitCode repeatable=$fullBattleRouteRepeatable reason=$fullBattleReason"
        Write-Output "Eleanor full-battle details: $fullBattleAResultPath ; $fullBattleBResultPath"
    }
} else {
    $fullBattleReason = "no full-battle harness result (exit=$fullBattleAExitCode/$fullBattleBExitCode)"
    Write-Output "GAP M2 Eleanor full battle: status=$fullBattleStatus reason=$fullBattleReason"
}
if (Test-Path -LiteralPath $completeOrbResultPath) {
    $completeOrb = Get-Content -LiteralPath $completeOrbResultPath -Raw | ConvertFrom-Json
    $completeProbe = $completeOrb.eleanorProbe
    $burst = $completeProbe.manaBurstTransition
    $completeStrike = $completeProbe.manaStrikeFollowUp
    if ($completeOrbExitCode -eq 0 -and $completeProbe.completeOrbScenario -and
        $completeProbe.opening.completeOrb -and $completeProbe.opening.mana -eq 7 -and
        $completeProbe.exactStateMatch -and $burst -and $burst.attempted -and
        $burst.exactStateMatch -and $burst.stanceEntered -and $burst.nativeExhausted -and
        $completeStrike -and $completeStrike.attempted -and $completeStrike.exactStateMatch -and
        $completeStrike.preActionRootMatches -and $completeStrike.preActionContinuationMatches -and
        $completeStrike.branchForkIsolation) {
        $completeOrbStatus = 'passed'
        $completeOrbReason = 'CompleteCrystalOrb -> ManaGather -> ManaBurst (Mentor) -> ManaStrike exact native/runtime differential'
        Write-Output "PASS M2 Eleanor complete-orb progression: openingMana=$($completeProbe.opening.mana) manaBurstExact=$($burst.exactStateMatch) mentorGauge=$($burst.liveAfter.Gauge) exhausted=$($burst.nativeExhausted) strikeExact=$($completeStrike.exactStateMatch) forkIsolated=$($completeStrike.branchForkIsolation)"
    } else {
        $completeOrbReason = if ($burst.simulationGap) { $burst.simulationGap } elseif ($completeProbe.simulationGap) { $completeProbe.simulationGap } elseif ($completeOrb.error.message) { $completeOrb.error.message } elseif ($completeStrike.simulationGap) { $completeStrike.simulationGap } else { 'Complete-orb progression failed an exact-state, exhaustion, stance, or fork assertion' }
        if ($completeOrb.error.type -like '*PredictionUnsupportedException' -or $completeProbe.simulationGap -or $burst.simulationGap -or $completeStrike.simulationGap) { $completeOrbStatus = 'unsupported' }
        Write-Output "GAP M2 Eleanor complete-orb progression: status=$completeOrbStatus exit=$completeOrbExitCode reached=$($completeOrb.reachedMilestone) reason=$completeOrbReason"
        Write-Output "Eleanor complete-orb details: $completeOrbResultPath"
    }
} else {
    $completeOrbReason = "no harness result (exit=$completeOrbExitCode)"
    Write-Output "GAP M2 Eleanor complete-orb progression: status=$completeOrbStatus reason=$completeOrbReason"
}
$crossTurnStatus = 'failed'
$crossTurnReason = 'no machine-readable cross-turn result'
if (Test-Path -LiteralPath $crossTurnResultPath) {
    $crossTurnHarness = Get-Content -LiteralPath $crossTurnResultPath -Raw | ConvertFrom-Json
    $crossTurn = $crossTurnHarness.eleanorProbe.crossTurn
    if ($crossTurnExitCode -eq 0 -and $crossTurn.schema -eq 'eleanor-mentor-cross-turn-differential/v1' -and
        $crossTurn.gameVersion -eq 'v0.111.0' -and $crossTurn.gameCommit -eq '41cef1ea' -and
        $crossTurn.modVersion -eq $myModVersion -and $crossTurn.testMode -and -not $crossTurn.steamInitialized -and
        $crossTurn.saveStore -like '*MemorySaveStore' -and -not $crossTurn.persistentSaveWrites -and
        $crossTurn.nativeTransitionReachedNextTurn -and $crossTurn.preTurnRootMatches -and $crossTurn.preTurnContinuationMatches -and
        $crossTurn.rootRegistrationGuard.supportedRegistrationRejected -and
        $crossTurn.rootRegistrationGuard.stampUnchangedAfterRejectedRegistration -and
        $crossTurn.rootRegistrationGuard.registrationMutationMadeCapturedRootStale -and
        $crossTurn.rootRegistrationGuard.stampRestored -and
        $crossTurn.turnStartRegistrationGuard.lateRegistrationRejected -and
        $crossTurn.turnStartRegistrationGuard.rejectedRegistrationStampStable -and
        $crossTurn.turnStartRegistrationGuard.capturedRootStaleAfterRegistryMutation -and
        $crossTurn.turnStartRegistrationGuard.stampRestored -and
        $crossTurn.turnStartPatchGuard.rootCaptureRejected -and
        $crossTurn.turnStartPatchGuard.capturedRootMatchesAfterRestore) {
        if ($crossTurn.status -eq 'passed' -and $crossTurn.simulatorReachedNextTurn -and
            $crossTurn.branchForkIsolation -and $crossTurn.parentBranchStateUnchanged -and
            $crossTurn.continuationMatches -and @($crossTurn.rootDifferences).Count -eq 0 -and
            $crossTurn.firstTurnSetup.exactEveryAction) {
            $crossTurnStatus = 'passed'
            $crossTurnReason = 'Branch-local CompleteCrystalOrb Mentor upkeep, orb income, draw bonus, and second-turn hand matched native exactly'
        } elseif ($crossTurn.status -eq 'unsupported') {
            $crossTurnStatus = 'unsupported'
            $crossTurnReason = [string]$crossTurn.unsupportedReason
        } else {
            $crossTurnReason = 'Cross-turn evidence failed its native/simulator transition assertions'
        }
    } else {
        $crossTurnReason = if ($crossTurnHarness.error.message) { [string]$crossTurnHarness.error.message } else { 'Cross-turn evidence failed version, isolation, root-guard, or native-boundary checks' }
        if ($crossTurnHarness.error.type -like '*PredictionUnsupportedException' -or
            $crossTurnHarness.eleanorProbe.crossTurn.status -eq 'unsupported') { $crossTurnStatus = 'unsupported' }
    }
} else {
    $crossTurnReason = "no harness result (exit=$crossTurnExitCode)"
}
Write-Output "$(if ($crossTurnStatus -eq 'passed') { 'PASS' } elseif ($crossTurnStatus -eq 'unsupported') { 'GAP' } else { 'FAIL' }) M2 Eleanor Mentor cross-turn: status=$crossTurnStatus reason=$crossTurnReason evidence=$crossTurnResultPath"

if (Test-Path -LiteralPath $eleanorResultPath) {
    $eleanor = Get-Content -LiteralPath $eleanorResultPath -Raw | ConvertFrom-Json
    $manaStrike = $eleanor.eleanorProbe.manaStrikeFollowUp
    $registrationGuards = @($eleanor.eleanorProbe.registrationGuards)
    $registrationGuardsPassed = $registrationGuards.Count -eq 4 -and
        @($registrationGuards | Where-Object {
            -not $_.wrongTargetRejected -or -not $_.wrongAssemblyRejected -or
            -not $_.wrongManifestRejected -or -not $_.existingPatchRejected
        }).Count -eq 0
    $latePatchGuard = $eleanor.eleanorProbe.rootPatchGuard
    $rootRegistrationGuard = $eleanor.eleanorProbe.rootRegistrationGuard
    if ($eleanorExitCode -eq 0 -and $eleanor.eleanorProbe.exactStateMatch -and
        $manaStrike -and $manaStrike.attempted -and $manaStrike.exactStateMatch -and
        $manaStrike.preActionRootMatches -and $manaStrike.preActionContinuationMatches -and $manaStrike.branchForkIsolation -and
        $registrationGuardsPassed -and $latePatchGuard.stampChanged -and $latePatchGuard.stampRestored -and $latePatchGuard.rootCaptureRejected -and
        $rootRegistrationGuard.supportedRegistrationRejected -and
        $rootRegistrationGuard.stampUnchangedAfterRejectedRegistration -and
        $rootRegistrationGuard.registrationMutationMadeCapturedRootStale -and $rootRegistrationGuard.stampRestored -and
        $completeOrbStatus -eq 'passed' -and $manaToShieldStatus -eq 'passed' -and
        $fullBattleStatus -eq 'passed' -and $crossTurnStatus -eq 'passed') {
        $eleanorStatus = 'passed'
        $eleanorReason = 'ManaGather/ManaStrike exact; Complete Orb Mentor turn-start and next-hand boundary exact on an isolated branch; ManaToShield->ManaStrike exact; two same-seed two-turn Victory routes exact; strict identity, registry stability, patch, and fork-isolation guards passed'
        Write-Output "PASS M2 Eleanor native/runtime differential: mod=$myModVersion dllSha256=$modDllHash gatherExact=True strikeExact=True registrationGuards=$($registrationGuards.Count) latePatchInvalidatesRoot=$($latePatchGuard.stampChanged -and $latePatchGuard.rootCaptureRejected) registrationMutationInvalidatesRoot=$($rootRegistrationGuard.registrationMutationMadeCapturedRootStale) mana=$($manaStrike.liveAfter.mana) gauge=$($manaStrike.liveAfter.gauge) enemyHp=$($manaStrike.nativeEnemyHpBefore)->$($manaStrike.nativeEnemyHpAfter) hits=$($manaStrike.liveObservedHitCount) preRoot=$($manaStrike.preActionRootMatches) forkIsolated=$($manaStrike.branchForkIsolation) completeOrb=$completeOrbStatus manaToShield=$manaToShieldStatus fullBattle=$fullBattleStatus"
    } else {
        $eleanorReason = if ($crossTurnStatus -ne 'passed') { $crossTurnReason } elseif ($manaStrike.simulationGap) { $manaStrike.simulationGap } elseif ($eleanor.eleanorProbe.simulationGap) { $eleanor.eleanorProbe.simulationGap } elseif ($eleanor.error.message) { $eleanor.error.message } elseif ($manaStrike.gaps) { $manaStrike.gaps -join ' ' } else { 'the Mod extension or combat state did not match exactly' }
        if ($crossTurnStatus -eq 'unsupported' -or $fullBattleStatus -eq 'unsupported' -or
            $eleanor.error.type -like '*PredictionUnsupportedException' -or $eleanor.eleanorProbe.simulationGap -or $manaStrike.simulationGap) { $eleanorStatus = 'unsupported' }
        Write-Output "GAP M2 Eleanor integration: status=$eleanorStatus exit=$eleanorExitCode reached=$($eleanor.reachedMilestone) reason=$eleanorReason"
        Write-Output "Eleanor details: $eleanorResultPath"
    }
} else {
    $eleanorReason = "no harness result (exit=$eleanorExitCode)"
    Write-Output "GAP M2 Eleanor integration: status=$eleanorStatus reason=$eleanorReason; local DLL was not deployed."
}

$runLoopOut = Join-Path $runRoot 'native-run-loop'
$runLoopArgs = @($scenarioArgs) + @('--milestone', 'M2', '--label', 'vanilla-minimal-run-loop', '--out', $runLoopOut)
$env:SIMULATORLAB_NATIVE_BATTLE_TRACE = '1'
$env:SIMULATORLAB_NATIVE_RUNLOOP = '1'
try {
    & dotnet $harnessDll @runLoopArgs
    $runLoopExitCode = $LASTEXITCODE
} finally {
    Remove-Item Env:SIMULATORLAB_NATIVE_BATTLE_TRACE -ErrorAction SilentlyContinue
    Remove-Item Env:SIMULATORLAB_NATIVE_RUNLOOP -ErrorAction SilentlyContinue
}
$runLoopResultPath = Join-Path $runLoopOut 'native-run-loop-differential.json'
if ($runLoopExitCode -ne 0 -or -not (Test-Path -LiteralPath $runLoopResultPath)) {
    throw "Native minimal run-loop validation failed (exit=$runLoopExitCode); inspect $runLoopOut."
}
$runLoop = Get-Content -LiteralPath $runLoopResultPath -Raw | ConvertFrom-Json
if ($runLoop.schema -ne 'native-run-loop-differential/v1' -or
    $runLoop.gameVersion -ne 'v0.111.0' -or $runLoop.gameCommit -ne '41cef1ea' -or
    $runLoop.mainAssemblyHash -ne 222455745 -or -not $runLoop.testMode -or
    $runLoop.steamInitialized -or $runLoop.saveStore -notlike '*MemorySaveStore' -or
    -not $runLoop.rewardSetCanSkip -or $runLoop.offeredRewards.Count -lt 1 -or
    -not $runLoop.skippedPendingRewards -or
    $runLoop.floorAfterNextNode -le $runLoop.floorBeforeNextNode -or
    $runLoop.protocolPhase -ne 'NodeEntered' -or
    $runLoop.protocolState.enteredNodeId -ne $runLoop.nextNode.nodeId) {
    throw 'Native minimal run-loop evidence failed its version, isolation, reward, or next-node checks.'
}
Write-Output "PASS native minimum run loop: Victory -> native rewards resolved (pending rewards skipped) -> next node $($runLoop.nextNode.nodeId) ($($runLoop.nextNode.mapPointType)/$($runLoop.nextNode.resolvedRoomType)); no persistent save."

$milestoneSummaryPath = Join-Path $runRoot 'milestone-summary.json'
$milestoneSummary = [ordered]@{
    schema = 'simulator-lab-milestone-summary/v1'
    gameVersion = $releaseInfo.version
    gameCommit = $releaseInfo.commit
    mainAssemblyHash = $releaseInfo.main_assembly_hash
    probeRevision = $probeRevision
    runId = $runId
    milestones = [ordered]@{
        M1 = [ordered]@{
            status = 'passed'
            scope = 'vanilla single battle differential'
            replaySteps = $battle.steps.Count
            outcome = $battle.terminal.outcome
            randomTarget = [ordered]@{ status = 'passed'; encounter = $randomA.encounterModelId; livingTargets = $randomA.validEnemyTargets; damagedTargetIds = @($randomA.nativeTargetsDamaged) }
            exactEveryReplayStep = (@($battle.steps | Where-Object { -not $_.exactMatch }).Count -eq 0)
        }
        M2 = [ordered]@{
            status = $eleanorStatus
            scope = 'existing Eleanor ManaGather, ManaStrike, ManaBurst, ManaToShield; same-turn Mentor and mana-to-block interactions; two repeated one-turn Victory replays; separate Mentor cross-turn probe status reported below'
            reason = $eleanorReason
            evidence = $eleanorResultPath
            completeOrbStatus = $completeOrbStatus
            completeOrbReason = $completeOrbReason
            completeOrbEvidence = $completeOrbResultPath
            manaToShieldStatus = $manaToShieldStatus
            manaToShieldReason = $manaToShieldReason
            manaToShieldEvidence = $manaToShieldResultPath
            fullBattleStatus = $fullBattleStatus
            fullBattleReason = $fullBattleReason
            fullBattleEvidence = @($fullBattleAResultPath, $fullBattleBResultPath)
            crossTurnStatus = $crossTurnStatus
            crossTurnReason = $crossTurnReason
            crossTurnEvidence = $crossTurnResultPath
            registrationGuards = if ($eleanor) { @($eleanor.eleanorProbe.registrationGuards) } else { @() }
            rootPatchGuard = if ($eleanor) { $eleanor.eleanorProbe.rootPatchGuard } else { $null }
            rootRegistrationGuard = if ($eleanor) { $eleanor.eleanorProbe.rootRegistrationGuard } else { $null }
        }
        M3 = [ordered]@{
            status = 'side-exploration-passed'
            scope = 'vanilla Victory -> skipped pending rewards -> next node; no retail UI or full climb claim'
            evidence = $runLoopResultPath
            nextNode = $runLoop.nextNode.nodeId
        }
    }
}
[IO.File]::WriteAllText($milestoneSummaryPath, ($milestoneSummary | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
Write-Output "Milestone summary: $milestoneSummaryPath"
if ($eleanorStatus -ne 'passed') {
    throw "M2 Eleanor integration is $eleanorStatus; runner returns failure despite any successful vanilla stages. See $milestoneSummaryPath."
}
