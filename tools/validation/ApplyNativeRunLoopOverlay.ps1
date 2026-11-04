param([Parameter(Mandatory)][string]$HarnessRoot)
$ErrorActionPreference = 'Stop'
$bootstrapPath = Join-Path $HarnessRoot 'GameBootstrap.cs'
$s = [IO.File]::ReadAllText($bootstrapPath)
if ($s.Contains('if (NativeModRunLoop.Enabled) NativeModRunLoop.Initialize();')) { return }
$a = $s.IndexOf('        AccessTools.Field(typeof(ReflectionHelper), "_modTypes")')
$b = $s.IndexOf('        Trace("mod_manager_state");', $a)
if ($a -lt 0 -or $b -lt 0) { throw 'Native loader overlay lacks bootstrap anchor.' }
$old = $s.Substring($a, $b-$a)
$s = $s.Substring(0,$a) + "        if (!NativeModRunLoop.Enabled) {`n" + $old + "        }`n" + $s.Substring($b)
$anchor = '        AssemblyInfo.Init();'
$s = $s.Replace($anchor, "        if (NativeModRunLoop.Enabled) NativeModRunLoop.Initialize();`n" + $anchor)
[IO.File]::WriteAllText($bootstrapPath,$s,[Text.UTF8Encoding]::new($false))
$programPath = Join-Path $HarnessRoot 'Program.cs'
$s = [IO.File]::ReadAllText($programPath)
$anchor = '            DuplicateChoiceProbe.Install(options.OutputDirectory);'
$insert = @'
            if (NativeModRunLoop.Enabled)
            {
                object preflight = NativeModRunLoop.PoolPreflight();
                File.WriteAllText(Path.Combine(options.OutputDirectory, "native-pool-preflight.json"), JsonSerializer.Serialize(preflight, new JsonSerializerOptions { WriteIndented = true }));
                if (Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_LOOP_STAGE") == "Pool") return 0;
            }
'@
if (-not $s.Contains($anchor)) { throw 'Native preflight overlay lacks Program anchor.' }
$s = $s.Replace($anchor,$insert + "`n" + $anchor)
$anchor = '            payload["budget"] = DescribeBudget(options);'
$s = $s.Replace($anchor, @'
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIMULATORLAB_STAGE_ONE_CASE")))
            {
                payload["stageOne"] = EleanorV040Probe.RunStageOne(combat!,loop,options);
                return 0;
            }
            if (Environment.GetEnvironmentVariable("SIMULATORLAB_SHIELD_FIXTURE") == "1")
            {
                payload["shieldBoundary"] = EleanorV040Probe.RunManaShieldBoundary(combat!,loop,options.OutputDirectory);
                return 0;
            }
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIMULATORLAB_REWARD_CASE")))
            {
                payload["rewardBoundary"] = EleanorV040Probe.RunRewardBoundary(combat!,loop,options.OutputDirectory,options);
                return 0;
            }
'@ + "`n" + $anchor)
$s = $s.Replace($anchor, '            if (NativeModRunLoop.RecordFixtureEnabled) NativeModRunLoop.RecordingFixture = EleanorV040Probe.AddRecordingResetFixture(combat!, loop);' + "`n" + $anchor)
$anchor = '                if (Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_V041_VICTORY_REPLAY") == "1")'
$insert = @'
                if (Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_LOOP_STAGE") == "Loop")
                {
                    object result = NativeModRunLoop.RunLoop(combat!, loop, options.OutputDirectory, outcome.RouteActions, options);
                    payload["nativeLoop"] = result;
                    if (JsonSerializer.SerializeToElement(result).GetProperty("status").GetString() != "passed")
                        throw new InvalidOperationException("Native run loop did not pass; see native-run-loop.json.");
                }
                else
'@
if (-not $s.Contains($anchor)) { throw 'Native progression overlay lacks replay anchor.' }
$s = $s.Replace($anchor,$insert + "`n" + $anchor)
[IO.File]::WriteAllText($programPath,$s,[Text.UTF8Encoding]::new($false))
$path = Join-Path $HarnessRoot 'OfflineCombat.cs'
$s = [IO.File]::ReadAllText($path)
$anchor = '        EncounterModel mutableEncounter = encounter.ToMutable();'
$s = $s.Replace($anchor, @'
        if (NativeModRunLoop.Enabled)
        {
            NativeModRunLoop.BindScenarioSeed(scenario.Seed);
            await RunManager.Instance.EnterMapCoord(runState.Map.StartingMapPoint.coord);
            HarnessLog.Trace("native_first_map_entry");
            return;
        }
'@ + "`n" + $anchor)
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
