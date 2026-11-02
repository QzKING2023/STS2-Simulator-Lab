param([Parameter(Mandatory)][string]$HarnessRoot)
$ErrorActionPreference='Stop'
foreach($file in @('VanillaBattleReplayProbe.cs','VanillaRandomTargetProbe.cs','NativeRunLoopProbe.cs')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $HarnessRoot $file)}
$p=Join-Path $HarnessRoot 'Program.cs';$s=[IO.File]::ReadAllText($p)
$anchor='                payload["search"] = new Dictionary<string, object?>'
$insert=@'
            if (Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_BATTLE_TRACE") == "1")
            {
                if (Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_RUNLOOP") == "1") NativeRunLoopProbe.Arm();
                payload["nativeBattleReplay"] = VanillaBattleReplayProbe.Run(combat!, loop, outcome!, options.OutputDirectory);
            }
            if (Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_RUNLOOP") == "1")
                payload["nativeRunLoop"] = NativeRunLoopProbe.Run(loop, options.OutputDirectory);
'@
if(-not$s.Contains($anchor)){throw 'Stage-one overlay requires pinned post-search anchor.'}
if(-not$s.Contains('payload["nativeBattleReplay"]')){$s=$s.Replace($anchor,$insert+"`n"+$anchor)}
$anchor='outcome = ModRuntime.RunSearchDetailed(combat!, options, loop, session);'
if(-not$s.Contains($anchor)){throw 'Stage-one overlay requires pinned pre-search anchor.'}
if(-not$s.Contains('payload["nativeRandomTargetProbe"]')){$s=$s.Replace($anchor,'if (Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_RANDOM_TARGET") == "1") payload["nativeRandomTargetProbe"] = VanillaRandomTargetProbe.Run(combat!,loop,options.OutputDirectory);'+"`n"+$anchor)}
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
$p=Join-Path $HarnessRoot 'OfflineCombat.cs';$s=[IO.File]::ReadAllText($p)
if(-not$s.Contains('stage_one_vanilla_map_entry')){
 $a=$s.IndexOf('        await RunManager.Instance.EnterRoomDebug(');$end=$s.IndexOf('            mutableEncounter);',$a)
 if($a-lt0-or$end-lt0){throw 'Missing pinned vanilla room-entry anchor.'}
 $end+='            mutableEncounter);'.Length
 $insert=@'
        if (Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_RUNLOOP") == "1")
        {
            var map=runState.Map ?? throw new InvalidOperationException("Run loop requires a real map.");
            var start=map.startMapPoints.OrderBy(p=>p.coord.col).First();
            if(start.PointType!=MegaCrit.Sts2.Core.Map.MapPointType.Monster) throw new InvalidOperationException("Start is not a native monster node.");
            await RunManager.Instance.EnterMapCoordDebug(start.coord, RoomType.Monster, MegaCrit.Sts2.Core.Map.MapPointType.Monster, mutableEncounter, showTransition:false);
            HarnessLog.Trace("stage_one_vanilla_map_entry");
        }
        else
        {
            await RunManager.Instance.EnterRoomDebug(RoomType.Monster, MegaCrit.Sts2.Core.Map.MapPointType.Unassigned,mutableEncounter);
        }
'@
 $s=$s.Substring(0,$a)+$insert+$s.Substring($end);[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
}
