using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Platform.Steam;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using SimulatorLab.Core;
using SimulatorLab.Core.Mods;
using Environment = System.Environment;

namespace OfflineSearchHarness;

/// <summary>
/// The stage 8 profile loads only BaseLib and the fixed Kaguya snapshot. The
/// native host sees no Mod DTOs: it emits scalar identity, intent, lifecycle,
/// reward, and ledger evidence for one Nay encounter.
/// </summary>
internal static class KaguyaNativeModLoop
{
    internal static bool Enabled => Environment.GetEnvironmentVariable("SIMULATORLAB_KAGUYA_STAGE8") == "1";
    internal static bool IsScenario(string scenario) => scenario is
        "kaguya-stage8" or "kaguya-stage8-replay" or
        "kaguya-m2-5" or "kaguya-m2-5-replay";
    internal static bool IsContentProbeScenario(string scenario) =>
        scenario.StartsWith("kaguya-card-probe:", StringComparison.Ordinal) ||
        scenario.StartsWith("kaguya-relic-probe:", StringComparison.Ordinal) ||
        scenario.StartsWith("kaguya-power-probe:", StringComparison.Ordinal) ||
        scenario.StartsWith("kaguya-potion-probe:", StringComparison.Ordinal) ||
        scenario.StartsWith("kaguya-enchantment-probe:", StringComparison.Ordinal) ||
        scenario.StartsWith("kaguya-encounter-probe:", StringComparison.Ordinal) ||
        scenario.StartsWith("kaguya-enemy-probe:", StringComparison.Ordinal) ||
        scenario.StartsWith("kaguya-event-probe:", StringComparison.Ordinal) ||
        scenario.StartsWith("kaguya-character-probe:", StringComparison.Ordinal);
    internal static bool IsRunContentProbeScenario(string scenario) =>
        scenario.StartsWith("kaguya-encounter-probe:", StringComparison.Ordinal) ||
        scenario.StartsWith("kaguya-enemy-probe:", StringComparison.Ordinal) ||
        scenario.StartsWith("kaguya-character-probe:", StringComparison.Ordinal);
    internal static string SnapshotRoot => Required("SIMULATORLAB_KAGUYA_SNAPSHOT_ROOT");
    internal static readonly Harmony Patches = new("simulatorlab.kaguya.stage8");
    internal static readonly Dictionary<string, Assembly> Loaded = new(StringComparer.Ordinal);
    internal static readonly List<string> Events = [];
    internal static int GoldBefore;
    internal static string? EncounterId;
    internal static string? EnemyId;
    internal static string? SelectedEncounterType;
    internal static string? SelectedEnemyType;
    internal static string? SelectedEventType;
    internal static KaguyaEventObservation? EventObservation;
    internal static KaguyaEnemyObservation? EnemyObservation;
    internal static KaguyaRewardObservation? RewardObservation;
    private static MegaCrit.Sts2.Core.Audio.Debug.NDebugAudioManager? DebugAudioStub;
    private static int NextAudioHandle;
    private static readonly HashSet<int> ActiveAudioHandles = [];
    private static readonly Dictionary<int, string> AudioStreams = [];
    private static readonly ConditionalWeakTable<Godot.Timer, TimerState> TimerStates = new();

    internal static void Initialize()
    {
        if (!Enabled) return;
        if (!TestMode.IsOn || SteamInitializer.Initialized)
            throw new InvalidOperationException("Kaguya profile requires offline TestMode");
        Patch(typeof(OS), nameof(OS.GetExecutablePath), nameof(ExecutablePath));
        Patch(typeof(OS), nameof(OS.IsDebugBuild), nameof(NotDebug));
        Patch(typeof(OS), nameof(OS.GetCmdlineArgs), nameof(CommandLine));
        Patch(typeof(OS), nameof(OS.GetUserDataDir), nameof(UserDataDir));
        Patch(typeof(ModManager), nameof(ModManager.CopyUnmoddedSaveFilesIfNeeded), nameof(RejectCopy));
        var init = AccessTools.Method(typeof(ModManager), nameof(ModManager.Initialize))
            ?? throw new MissingMethodException(typeof(ModManager).FullName, nameof(ModManager.Initialize));
        var machine = init.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new InvalidOperationException("ModManager.Initialize has no async state machine");
        Patches.Patch(AccessTools.Method(machine, "MoveNext")!,
            transpiler: new HarmonyMethod(typeof(KaguyaNativeModLoop), nameof(CommandLineQueryBridge)));
        var scriptLookup = AccessTools.Method(AccessTools.TypeByName("Godot.Bridge.ScriptManagerBridge"), "LookupScriptsInAssembly");
        if (scriptLookup is not null)
            Patches.Patch(scriptLookup, prefix: new HarmonyMethod(typeof(KaguyaNativeModLoop), nameof(SkipGodotScriptLookup)));
        var move = AccessTools.Method(typeof(MonsterModel), nameof(MonsterModel.PerformMove))
            ?? throw new MissingMethodException(typeof(MonsterModel).FullName, nameof(MonsterModel.PerformMove));
        Patches.Patch(move,
            prefix: new HarmonyMethod(typeof(KaguyaNativeModLoop), nameof(EnemyMoveBefore)),
            postfix: new HarmonyMethod(typeof(KaguyaNativeModLoop), nameof(EnemyMoveAfter)));
        var relicHoverTips = AccessTools.PropertyGetter(typeof(RelicModel), "HoverTipsExcludingRelic");
        if (relicHoverTips is not null)
        {
            Patches.Patch(relicHoverTips, prefix: new HarmonyMethod(typeof(KaguyaNativeModLoop), nameof(SkipRelicHoverTips)));
            Events.Add("kaguya-platform-bypass:relic-hover-tips-headless");
        }
        var debugAudio = AccessTools.TypeByName("MegaCrit.Sts2.Core.Audio.Debug.NDebugAudioManager");
        if (debugAudio is not null)
        {
            var audioPlayAdapter = IsContentProbeScenario(Program.CurrentScenario) ? nameof(PlayAudioAdapter) : nameof(SkipKaguyaAudio);
            var audioStopAdapter = IsContentProbeScenario(Program.CurrentScenario) ? nameof(StopAudioAdapter) : nameof(SkipKaguyaAudio);
            var audioStopAllAdapter = IsContentProbeScenario(Program.CurrentScenario) ? nameof(StopAllAudioAdapter) : nameof(SkipKaguyaAudio);
            var instance = AccessTools.PropertyGetter(debugAudio, "Instance");
            if (instance is not null)
                Patches.Patch(instance, prefix: new HarmonyMethod(typeof(KaguyaNativeModLoop), nameof(DebugAudioInstance)));
            Patches.Patch(AccessTools.Method(debugAudio, "Play", [typeof(string), typeof(float), typeof(PitchVariance)])!,
                prefix: new HarmonyMethod(typeof(KaguyaNativeModLoop), audioPlayAdapter));
            Patches.Patch(AccessTools.Method(debugAudio, "Stop", [typeof(int), typeof(float)])!,
                prefix: new HarmonyMethod(typeof(KaguyaNativeModLoop), audioStopAdapter));
            Patches.Patch(AccessTools.Method(debugAudio, "StopAll", Type.EmptyTypes)!,
                prefix: new HarmonyMethod(typeof(KaguyaNativeModLoop), audioStopAllAdapter));
            Events.Add("kaguya-platform-bypass:debug-audio-headless");
        }
        var mainLoop = AccessTools.Method(typeof(Godot.Engine), nameof(Godot.Engine.GetMainLoop));
        if (mainLoop is not null)
        {
            Patches.Patch(mainLoop, prefix: new HarmonyMethod(typeof(KaguyaNativeModLoop), nameof(HeadlessMainLoop)));
            Events.Add("kaguya-platform-bypass:engine-main-loop-headless");
        }
        var simplifyPath = AccessTools.Method(typeof(Godot.StringExtensions), "SimplifyPath", [typeof(string)]);
        if (simplifyPath is not null)
        {
            Patches.Patch(simplifyPath, prefix: new HarmonyMethod(typeof(KaguyaNativeModLoop), nameof(SkipSimplifyPath)));
            Events.Add("kaguya-platform-bypass:string-path-headless");
        }
        var randRange = AccessTools.Method(typeof(Godot.GD), "RandRange", [typeof(double), typeof(double)]);
        if (randRange is not null)
        {
            Patches.Patch(randRange, prefix: new HarmonyMethod(typeof(KaguyaNativeModLoop), nameof(KaguyaRandRange)));
            Events.Add("kaguya-platform-bypass:gd-rand-range-headless");
        }
        var assetCache = AccessTools.TypeByName("MegaCrit.Sts2.Core.Assets.AssetCache");
        var loadAsset = assetCache is null ? null : AccessTools.Method(assetCache, "LoadAsset", [typeof(string)]);
        if (loadAsset is not null)
        {
            Patches.Patch(loadAsset, prefix: new HarmonyMethod(typeof(KaguyaNativeModLoop), nameof(SkipAssetLoad)));
            Events.Add("kaguya-platform-bypass:asset-cache-headless");
        }
        var powerIcon = AccessTools.PropertyGetter(typeof(PowerModel), "Icon");
        if (powerIcon is not null)
        {
            Patches.Patch(powerIcon, prefix: new HarmonyMethod(typeof(KaguyaNativeModLoop), nameof(SkipPowerIcon)));
            Events.Add("kaguya-platform-bypass:power-icon-headless");
        }

        ModManager.ResetForTests();
        var settings = new ModSettings { PlayerAgreedToModLoading = true };
        foreach (var id in new[] { KaguyaStage8Constants.BaseLibId, KaguyaStage8Constants.ModId })
        {
            settings.ModList.Add(new SettingsSaveMod { Id = id, IsEnabled = true });
            ModManager.TestInitializers[id] = () => InitializeAssembly(id);
        }
        var task = ModManager.Initialize(new SnapshotFileIo(SnapshotRoot), settings,
            SemanticVersion.FromString(KaguyaStage8Constants.TargetGameVersion));
        ((MainLoopContext)SynchronizationContext.Current!).RunUntilCompleted(task,
            TimeSpan.FromSeconds(45), "Kaguya ModManager.Initialize");
        foreach (var mod in ModManager.Mods)
        {
            Events.Add($"loader:{mod.manifest?.id}:{mod.state}:{mod.assemblies.Count}");
            if (mod.state.ToString() != "Loaded" || mod.errors?.Count > 0 || mod.assemblies.Count == 0)
                throw new InvalidOperationException($"Kaguya loader rejected {mod.manifest?.id}: {mod.state}; errors={string.Join(" || ", mod.errors ?? [])}; assemblies={mod.assemblies.Count}");
        }
        if (ModManager.Mods.Count != 2)
            throw new InvalidOperationException("Kaguya profile loaded an unexpected Mod inventory: " +
                string.Join(",", ModManager.Mods.Select(m => $"{m.manifest?.id}:{m.state}:{m.assemblies.Count}")));
        InstallTimerSeam();
        InstallEndingSeam();
        Events.Add("kaguya-runtime:initialized");
    }

    internal static Type[] IncludeModels(Type[] baseModels)
    {
        var abstractModel = typeof(AbstractModel);
        var collisionIsolatedClash = Program.CurrentScenario == "kaguya-card-probe:Kaguya.Cards.Clash";
        if (collisionIsolatedClash)
            Events.Add("kaguya-registration-profile:collision-isolated-card-clash");
        var allKaguyaModels = Loaded.Values.SelectMany(SafeGetTypes)
                .Where(t => t.IsClass && !t.IsAbstract && abstractModel.IsAssignableFrom(t))
                .Where(t => t.FullName != "Kaguya.Cards.Clash")
                .ToArray();
        var gameModels = collisionIsolatedClash
            ? baseModels.Where(t => t.FullName != "MegaCrit.Sts2.Core.Models.Cards.Clash")
            : baseModels;
        var kaguyaModels = collisionIsolatedClash
            ? allKaguyaModels.Append(Loaded[KaguyaStage8Constants.ModId].GetType("Kaguya.Cards.Clash", true)!)
            : allKaguyaModels;
        return gameModels.Concat(kaguyaModels)
            .Distinct().ToArray();
    }

    internal static void WriteProbe(string output, string seed, bool replay,
        NativeRunTerminal terminal, NativeRunStop stop, string? error)
    {
        if (!Enabled) return;
        var rows = Program.WatcherEvents.Select(x => JsonSerializer.SerializeToElement(x, Program.Json)).ToArray();
        var eventNames = rows.Where(x => x.TryGetProperty("kind", out _))
            .Select(x => x.GetProperty("kind").GetString() ?? "")
            .Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        var final = Program.State();
        var goldAfter = final.TryGetProperty("gold", out var g) && g.ValueKind == JsonValueKind.Number ? g.GetInt32() : GoldBefore;
        var digestInput = JsonSerializer.Serialize(new
        {
            seed, terminal, stop, EncounterId, EnemyId, SelectedEventType,
            EnemyObservation, RewardObservation, GoldBefore, goldAfter,
            events = eventNames, evidence = rows
        }, Program.Json);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(digestInput))).ToLowerInvariant();
        var build = ReadBuildIdentity();
        WriteContentInventory(output);
        var evidence = JsonSerializer.SerializeToElement(new
        {
            events = rows,
            lifecycle = new { start = eventNames.Contains("new-run"), entered = eventNames.Contains("kaguya-encounter-entered"), cleanup = eventNames.Contains("kaguya-cleanup"), teardown = eventNames.Contains("kaguya-teardown") },
            enemy = EnemyObservation,
            @event = EventObservation,
            reward = RewardObservation,
            ledger = new { goldBefore = GoldBefore, goldAfter, delta = goldAfter - GoldBefore },
            isolation = new { snapshotWriteBlocked = true, formalDataWriteBlocked = true, modAssemblyOutsideCore = true }
        }, Program.Json);
        var stage8Completed = error is null && stop == NativeRunStop.SliceBoundary &&
            terminal == NativeRunTerminal.Ongoing;
        var fullRunCompleted = error is null &&
            (stop == NativeRunStop.Completed || (IsRunContentProbeScenario(Program.CurrentScenario) && stop == NativeRunStop.SliceBoundary)) &&
            (terminal is NativeRunTerminal.Death or NativeRunTerminal.RunVictory);
        var completed = stage8Completed || fullRunCompleted ||
            (error is null && IsContentProbeScenario(Program.CurrentScenario));
        var probe = new KaguyaRuntimeProbe(
            KaguyaStage8Constants.ProbeSchema, completed,
            KaguyaStage8Constants.TargetGameVersion, KaguyaStage8Constants.SnapshotId, seed,
            build.BuildId, build.ContentKey, KaguyaStage8Constants.ModId, EncounterId,
            SelectedEncounterType ?? KaguyaStage8Constants.EncounterType, EnemyId,
            SelectedEnemyType ?? KaguyaStage8Constants.EnemyType,
            EnemyObservation, RewardObservation?.RewardId, RewardObservation, GoldBefore,
            goldAfter, eventNames, evidence, digest, null,
            fullRunCompleted ? (terminal == NativeRunTerminal.Death ? "run-death" : "run-victory") :
                stage8Completed ? "battle-complete" : "runtime-failed",
            error is null ? null : "native-run", error,
            EventObservation?.Id, SelectedEventType, EventObservation);
        File.WriteAllText(Path.Combine(output, replay ? "kaguya-stage8-probe-replay.json" : "kaguya-stage8-probe.json"),
            JsonSerializer.Serialize(probe, ModWorkerJson.Options));
    }

    private static void WriteContentInventory(string output)
    {
        var registered = ModelDb.All
            .Where(model => model.GetType().Assembly.GetName().Name == KaguyaStage8Constants.ModId)
            .Select(model => new
            {
                id = model.Id.ToString(),
                type = model.GetType().FullName ?? model.GetType().Name,
                kind = ModelKind(model.GetType()),
                registrationIdentity = $"REG.KAGUYA.{model.Id}@{KaguyaStage8Constants.TargetGameVersion}"
            })
            .OrderBy(row => row.kind, StringComparer.Ordinal)
            .ThenBy(row => row.id, StringComparer.Ordinal)
            .ToArray();
        var declared = Loaded.TryGetValue(KaguyaStage8Constants.ModId, out var kaguyaAssembly)
            ? SafeGetTypes(kaguyaAssembly)
            .Where(type => type.IsClass && !type.IsAbstract && typeof(AbstractModel).IsAssignableFrom(type))
            .Select(type => new
            {
                id = (string?)null,
                type = type.FullName ?? type.Name,
                kind = ModelKind(type),
                registrationIdentity = $"TYPE.KAGUYA.{type.FullName}@{KaguyaStage8Constants.TargetGameVersion}"
            })
            .OrderBy(row => row.kind, StringComparer.Ordinal)
            .ThenBy(row => row.type, StringComparer.Ordinal)
            .ToArray()
            : Array.Empty<object>();
        File.WriteAllText(Path.Combine(output, "kaguya-content-inventory.json"),
            JsonSerializer.Serialize(new
            {
                schema = "kaguya-content-inventory/v1",
                targetGameVersion = KaguyaStage8Constants.TargetGameVersion,
                snapshotId = KaguyaStage8Constants.SnapshotId,
                modId = KaguyaStage8Constants.ModId,
                registrationCondition = "isolated BaseLib 3.4.7 + Kaguya loader, scoped native profile",
                registeredUnits = registered,
                declaredUnits = declared
            }, ModWorkerJson.Options));
    }

    private static string ModelKind(Type type) =>
        typeof(CharacterModel).IsAssignableFrom(type) ? "Character" :
        typeof(CardModel).IsAssignableFrom(type) ? "Card" :
        typeof(PowerModel).IsAssignableFrom(type) ? "Power" :
        typeof(RelicModel).IsAssignableFrom(type) ? "Relic" :
        typeof(EncounterModel).IsAssignableFrom(type) ? "Encounter" :
        typeof(MonsterModel).IsAssignableFrom(type) ? "Enemy" :
        typeof(EventModel).IsAssignableFrom(type) ? "Event" : "Other";

    private sealed record MoveScope(string Model, string Intent, bool Attack, int Hp, int Block);

    private static void EnemyMoveBefore(MonsterModel __instance, out MoveScope __state)
    {
        __state = new("", "", false, 0, 0);
        if (!IsKaguya(__instance)) return;
        var intent = IntentSnapshot(__instance);
        __state = new(__instance.Id.ToString(), intent.Kinds.FirstOrDefault() ?? "unknown",
            __instance.IntendsToAttack, __instance.Creature?.CurrentHp ?? 0, __instance.Creature?.Block ?? 0);
        Program.Observe("kaguya-enemy-intent", new
        {
            model = __instance.Id.ToString(),
            type = __instance.GetType().FullName,
            intentKinds = intent.Kinds,
            intentIds = intent.Ids,
            intendsToAttack = __instance.IntendsToAttack,
            hp = __instance.Creature?.CurrentHp,
            block = __instance.Creature?.Block
        });
    }

    private static void EnemyMoveAfter(MonsterModel __instance, MoveScope __state, ref Task __result)
    {
        if (__state.Model.Length == 0 || !IsKaguya(__instance)) return;
        __result = TrackEnemyMove(__result, __instance, __state);
    }

    private static async Task TrackEnemyMove(Task task, MonsterModel model, MoveScope scope)
    {
        await task;
        Program.Observe("kaguya-enemy-behavior", new
        {
            model = scope.Model,
            intent = scope.Intent,
            attack = scope.Attack,
            before = new { hp = scope.Hp, block = scope.Block },
            after = new { hp = model.Creature?.CurrentHp, block = model.Creature?.Block },
            evidence = "native/MonsterModel.PerformMove"
        });
    }

    internal static void CaptureEnemy(ICombatState state)
    {
        var creature = state.Enemies.FirstOrDefault(x => IsKaguya(x.Monster));
        if (creature?.Monster is null) throw new NotSupportedException("Kaguya Nay did not spawn in the declared encounter");
        var model = creature.Monster;
        var intent = IntentSnapshot(model);
        EnemyId = model.Id.ToString();
        EnemyObservation = new(
            model.Id.ToString(), model.GetType().Name, model.GetType().FullName ?? model.GetType().Name,
            intent.Ids, intent.Kinds, model.IntendsToAttack, creature.CurrentHp, creature.CurrentHp,
            creature.Block, creature.Block, "native/kaguya/enemy");
    }

    internal static void CompleteEnemy(ICombatState state)
    {
        if (EnemyObservation is null) return;
        var creature = state.Enemies.FirstOrDefault(x => IsKaguya(x.Monster));
        if (creature is null) return;
        EnemyObservation = EnemyObservation with { FinalHp = creature.CurrentHp, FinalBlock = creature.Block };
    }

    internal static void CaptureReward(IReadOnlyList<JsonElement> rows, int goldBefore, int goldAfter)
    {
        var offer = rows.FirstOrDefault(x => x.TryGetProperty("kind", out var k) && k.GetString() == "reward-offer");
        var card = rows.FirstOrDefault(x => x.TryGetProperty("kind", out var k) && k.GetString() == "card-reward-acquired");
        var cardId = card.ValueKind == JsonValueKind.Object && card.TryGetProperty("detail", out var cd) && cd.TryGetProperty("model", out var cm)
            ? cm.GetString() : null;
        int? offeredGold = null;
        if (offer.ValueKind == JsonValueKind.Object && offer.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.Array)
            offeredGold = detail.EnumerateArray().Where(x => x.TryGetProperty("kind", out var k) && k.GetString() == "GoldReward")
                .Select(x => x.TryGetProperty("amount", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetInt32() : (int?)null)
                .FirstOrDefault();
        RewardObservation = new(
            "REWARD.KAGUYA_NAY_ROOM_END", "native-room-end",
            offeredGold ?? (goldAfter - goldBefore), cardId, "native/kaguya/reward-boundary");
    }

    private static (string[] Ids, string[] Kinds) IntentSnapshot(MonsterModel model)
    {
        try
        {
            var intents = model.NextMove?.Intents?.ToArray() ?? [];
            var kinds = intents.Select(x => x.GetType().Name).ToArray();
            return (kinds.Select(x => "INTENT." + x.ToUpperInvariant()).ToArray(), kinds);
        }
        catch { return ([], []); }
    }

    private static bool IsKaguya(MonsterModel? monster) => monster is not null &&
        (SelectedEnemyType is null || monster.GetType().FullName == SelectedEnemyType) &&
        monster.GetType().Assembly.GetName().Name == KaguyaStage8Constants.ModId;

    private static void InitializeAssembly(string id)
    {
        string path = id switch
        {
            KaguyaStage8Constants.BaseLibId => Path.Combine(SnapshotRoot, "3737335127", "BaseLib", "BaseLib.dll"),
            KaguyaStage8Constants.ModId => Path.Combine(SnapshotRoot, "3747548629", "Kaguya.dll"),
            _ => throw new InvalidOperationException("Unexpected Kaguya profile assembly: " + id)
        };
        var assembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => a.GetName().Name == id)
            ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        Loaded[id] = assembly;
        ModManager.AssociateAssemblyWithMod(id, assembly);
        if (id == KaguyaStage8Constants.BaseLibId)
        {
            foreach (var name in new[] { "AddCustomCharacters", "ModelDbSharedCardPoolsPatch" })
            {
                var patch = assembly.GetType("BaseLib.Patches.Content." + name, true)!;
                new Harmony("BaseLib").CreateClassProcessor(patch).Patch();
                Events.Add("baselib-patch:" + patch.FullName);
            }
            var modAudio = assembly.GetType("BaseLib.Audio.ModAudio", throwOnError: false);
            if (modAudio is not null)
                foreach (var play in AccessTools.GetDeclaredMethods(modAudio).Where(method => method.Name.StartsWith("PlaySound", StringComparison.Ordinal)))
                    Patches.Patch(play, prefix: new HarmonyMethod(typeof(KaguyaNativeModLoop), nameof(SkipKaguyaAudioResult)));
            Events.Add("kaguya-platform-bypass:baselib-audio-headless");
        }
        else
        {
            var kaguyaCardType = assembly.GetType("Kaguya.HinaMods.Cards.HinaModsCard", throwOnError: false);
            if (kaguyaCardType is not null)
            {
                var playCardSfx = AccessTools.Method(kaguyaCardType, "PlayCardSfx", [typeof(MegaCrit.Sts2.Core.Entities.Cards.CardPlay)]);
                if (playCardSfx is not null)
                    Patches.Patch(playCardSfx, prefix: new HarmonyMethod(typeof(KaguyaNativeModLoop), nameof(SkipKaguyaAudio)));
                var playHitSfx = AccessTools.Method(kaguyaCardType, "PlayHitSfx", Type.EmptyTypes);
                if (playHitSfx is not null)
                    Patches.Patch(playHitSfx, prefix: new HarmonyMethod(typeof(KaguyaNativeModLoop), nameof(SkipKaguyaAudio)));
                Events.Add("kaguya-platform-bypass:card-audio-headless");
            }
            var initializerTypes = SafeGetTypes(assembly)
                .Where(type => type.GetCustomAttribute<ModInitializerAttribute>() is not null)
                .ToArray();
            if (Environment.GetEnvironmentVariable("SIMULATORLAB_KAGUYA_FULL_INIT") == "1")
            {
                foreach (var type in initializerTypes)
                {
                    var attribute = type.GetCustomAttribute<ModInitializerAttribute>()!;
                    var method = type.GetMethod(attribute.initializerMethod,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                        ?? throw new MissingMethodException(type.FullName, attribute.initializerMethod);
                    method.Invoke(null, null);
                    Events.Add($"kaguya-initializer:{type.FullName}.{method.Name}");
                }
            }
            else
            {
                Events.Add("kaguya-initializer:disabled");
            }
        }
    }

    private static Type[] SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null).Cast<Type>().ToArray(); }
    }

    private static void Patch(Type type, string method, string prefix) =>
        Patches.Patch(AccessTools.Method(type, method)!, prefix: new HarmonyMethod(typeof(KaguyaNativeModLoop), prefix));
    private static bool ExecutablePath(ref string __result) { __result = Path.Combine(SnapshotRoot, "kaguya-stage8.exe"); return false; }
    private static bool NotDebug(ref bool __result) { __result = false; return false; }
    private static bool CommandLine(ref string[] __result) { __result = []; return false; }
    private static bool RejectCopy() => throw new InvalidOperationException("Kaguya ModManager attempted forbidden save copy");
    private static bool SkipGodotScriptLookup() => false;
    private static bool SkipKaguyaAudio() => false;
    private static bool SkipKaguyaAudioResult(ref Godot.AudioStreamPlayer? __result)
    {
        __result = null;
        return false;
    }
    private static bool SkipSimplifyPath(string instance, ref string __result)
    {
        __result = instance;
        return false;
    }
    private static bool KaguyaRandRange(double from, double to, ref double __result)
    {
        var rng = Program.CurrentPlayer?.RunState.Rng.Niche ?? throw new InvalidOperationException("Kaguya GD.RandRange requires native run RNG");
        __result = rng.NextDouble(from, to);
        return false;
    }

    private static bool HeadlessMainLoop(ref Godot.MainLoop? __result)
    {
        __result = null;
        return false;
    }
    private static bool DebugAudioInstance(ref MegaCrit.Sts2.Core.Audio.Debug.NDebugAudioManager? __result)
    {
        DebugAudioStub ??= (MegaCrit.Sts2.Core.Audio.Debug.NDebugAudioManager)
            RuntimeHelpers.GetUninitializedObject(typeof(MegaCrit.Sts2.Core.Audio.Debug.NDebugAudioManager));
        __result = DebugAudioStub;
        return false;
    }

    private static bool PlayAudioAdapter(string streamName, float volume, PitchVariance variance, ref int __result)
    {
        var handle = NextAudioHandle++;
        ActiveAudioHandles.Add(handle);
        AudioStreams[handle] = streamName;
        Program.Observe("kaguya-audio-play", new { handle, streamName, volume, variance = variance.ToString(), adapter = "managed-debug-audio/v1" });
        __result = handle;
        return false;
    }

    private static bool StopAudioAdapter(int id, float fadeTime)
    {
        var removed = ActiveAudioHandles.Remove(id);
        AudioStreams.Remove(id);
        Program.Observe("kaguya-audio-stop", new { handle = id, fadeTime, removed, adapter = "managed-debug-audio/v1" });
        return false;
    }

    private static bool StopAllAudioAdapter()
    {
        var count = ActiveAudioHandles.Count;
        ActiveAudioHandles.Clear();
        AudioStreams.Clear();
        Program.Observe("kaguya-audio-stop-all", new { count, adapter = "managed-debug-audio/v1" });
        return false;
    }

    private sealed class TimerState
    {
        internal double WaitTime;
        internal bool OneShot;
        internal bool Autostart;
        internal bool Paused;
        internal bool IgnoreTimeScale;
        internal bool Stopped = true;
        internal Action? Timeout;
    }

    private static void InstallTimerSeam()
    {
        var monster = Loaded[KaguyaStage8Constants.ModId].GetType("Kaguya.Monsters.TsukitoBosatsu", true)!;
        var afterAdded = AccessTools.Method(monster, nameof(MegaCrit.Sts2.Core.Models.MonsterModel.AfterAddedToRoom))!;
        var stateMachine = afterAdded.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new InvalidOperationException("TsukitoBosatsu.AfterAddedToRoom has no async state machine");
        var moveNext = AccessTools.Method(stateMachine, "MoveNext")!;
        Patches.Patch(moveNext, transpiler: new HarmonyMethod(typeof(KaguyaNativeModLoop), nameof(TimerStateMachineTranspiler)));
        var addChild = AccessTools.Method(typeof(Godot.Node), nameof(Godot.Node.AddChild), [typeof(Godot.Node), typeof(bool), typeof(Godot.Node.InternalMode)]);
        if (addChild is not null)
            Patches.Patch(addChild, prefix: new HarmonyMethod(typeof(KaguyaNativeModLoop), nameof(AddManagedTimerChild)));
        var queueFree = AccessTools.Method(typeof(Godot.Node), nameof(Godot.Node.QueueFree), Type.EmptyTypes);
        if (queueFree is not null)
            Patches.Patch(queueFree, prefix: new HarmonyMethod(typeof(KaguyaNativeModLoop), nameof(QueueFreeManagedTimer)));
        Events.Add("kaguya-platform-bypass:timer-managed-lifecycle/v1");
    }

    private static void InstallEndingSeam()
    {
        var ending = Loaded[KaguyaStage8Constants.ModId].GetType("Kaguya.Events.KaguyaEnding", true)!;
        var proceed = AccessTools.DeclaredMethod(ending, "Proceed");
        var stateMachine = proceed?.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
        var moveNext = stateMachine is null ? null : AccessTools.Method(stateMachine, "MoveNext");
        if (moveNext is not null)
            Patches.Patch(moveNext, transpiler: new HarmonyMethod(typeof(KaguyaNativeModLoop), nameof(EndingStateMachineTranspiler)));
        var gameOver = typeof(NRun).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(method => method.Name == nameof(NRun.ShowGameOverScreen) && method.GetParameters().Length == 1);
        if (gameOver is not null)
            Patches.Patch(gameOver, prefix: new HarmonyMethod(typeof(KaguyaNativeModLoop), nameof(SkipMissingGameOverUi)));
        Events.Add("kaguya-platform-bypass:ending-ui-managed-lifecycle/v1");
    }

    private static NRun GetManagedRunNode()
    {
        var result = NRun.Instance;
        if (result is not null) return result;
        result = (NRun)RuntimeHelpers.GetUninitializedObject(typeof(NRun));
        Program.Observe("kaguya-ending-ui-node-created", new { type = "NRun", adapter = "managed-ending-ui/v1" });
        return result;
    }

    private static IEnumerable<CodeInstruction> EndingStateMachineTranspiler(IEnumerable<CodeInstruction> source)
    {
        var getter = AccessTools.PropertyGetter(typeof(NRun), nameof(NRun.Instance));
        var replacement = AccessTools.Method(typeof(KaguyaNativeModLoop), nameof(GetManagedRunNode))!;
        foreach (var instruction in source)
        {
            if (getter is not null && instruction.Calls(getter))
            {
                instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                instruction.operand = replacement;
            }
            yield return instruction;
        }
    }

    private static bool SkipMissingGameOverUi(object __instance)
    {
        Program.Observe("kaguya-ending-ui-state", new
        {
            state = "game-over",
            receiverPresent = __instance is not null,
            adapter = "managed-ending-ui/v1",
            originalRunManagerOnEnded = true
        });
        return false;
    }

    private static TimerState TimerStateFor(Godot.Timer timer) => TimerStates.GetOrCreateValue(timer);
    private static Godot.Timer CreateManagedTimer()
    {
        var timer = (Godot.Timer)RuntimeHelpers.GetUninitializedObject(typeof(Godot.Timer));
        _ = TimerStateFor(timer);
        Program.Observe("kaguya-timer-created", new { type = "Godot.Timer", adapter = "managed-timer/v1" });
        return timer;
    }
    private static void SetManagedTimerWaitTime(Godot.Timer timer, double timeSec) => TimerStateFor(timer).WaitTime = timeSec;
    private static void SetManagedTimerOneShot(Godot.Timer timer, bool enable) => TimerStateFor(timer).OneShot = enable;
    private static void SetManagedTimerAutostart(Godot.Timer timer, bool enable) => TimerStateFor(timer).Autostart = enable;
    private static void AddManagedTimerTimeout(Godot.Timer timer, Action value)
    {
        TimerStateFor(timer).Timeout += value;
        Program.Observe("kaguya-timer-handler-bound", new { type = "Godot.Timer", adapter = "managed-timer/v1" });
    }

    private static IEnumerable<CodeInstruction> TimerStateMachineTranspiler(IEnumerable<CodeInstruction> source)
    {
        var timer = typeof(Godot.Timer);
        var constructor = AccessTools.Constructor(timer, Type.EmptyTypes)!;
        var replacements = new Dictionary<MethodInfo, MethodInfo>
        {
            [AccessTools.Method(timer, "set_WaitTime")!] = AccessTools.Method(typeof(KaguyaNativeModLoop), nameof(SetManagedTimerWaitTime))!,
            [AccessTools.Method(timer, "set_OneShot")!] = AccessTools.Method(typeof(KaguyaNativeModLoop), nameof(SetManagedTimerOneShot))!,
            [AccessTools.Method(timer, "set_Autostart")!] = AccessTools.Method(typeof(KaguyaNativeModLoop), nameof(SetManagedTimerAutostart))!,
            [AccessTools.Method(timer, "add_Timeout")!] = AccessTools.Method(typeof(KaguyaNativeModLoop), nameof(AddManagedTimerTimeout))!
        };
        var addChild = AccessTools.Method(typeof(Godot.Node), nameof(Godot.Node.AddChild), [typeof(Godot.Node), typeof(bool), typeof(Godot.Node.InternalMode)]);
        var attachChild = AccessTools.Method(typeof(KaguyaNativeModLoop), nameof(AttachManagedTimerChild), [typeof(Godot.Node), typeof(Godot.Node), typeof(bool), typeof(Godot.Node.InternalMode)]);
        foreach (var instruction in source)
        {
            if (instruction.opcode == System.Reflection.Emit.OpCodes.Newobj && instruction.operand is ConstructorInfo ctor && ctor == constructor)
            {
                instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(KaguyaNativeModLoop), nameof(CreateManagedTimer))!;
            }
            else
            {
                var match = replacements.Keys.FirstOrDefault(method => instruction.Calls(method));
                if (match is not null)
                {
                    instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                    instruction.operand = replacements[match];
                }
                else if (addChild is not null && attachChild is not null && instruction.Calls(addChild))
                {
                    // The original expression can have a null NCombatRoom.Instance.
                    // A static call keeps that null as data so the managed timer seam
                    // can retain the callback and cleanup evidence without dereferencing
                    // a missing Godot parent before the adapter runs.
                    instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                    instruction.operand = attachChild;
                }
            }
            yield return instruction;
        }
    }

    private static void AttachManagedTimerChild(
        Godot.Node? node,
        Godot.Node child,
        bool forceReadableName,
        Godot.Node.InternalMode internalMode)
    {
        if (child is Godot.Timer)
        {
            Program.Observe("kaguya-timer-attached", new { type = "Godot.Timer", adapter = "managed-timer/v1", parentPresent = node is not null });
            return;
        }

        node?.AddChild(child, forceReadableName, internalMode);
    }

    private static bool AddManagedTimerChild(Godot.Node node)
    {
        if (node is not Godot.Timer) return true;
        Program.Observe("kaguya-timer-attached", new { type = "Godot.Timer", adapter = "managed-timer/v1" });
        return false;
    }
    private static bool QueueFreeManagedTimer(Godot.Node __instance)
    {
        if (__instance is not Godot.Timer timer) return true;
        var state = TimerStateFor(timer);
        state.Timeout = null;
        state.Stopped = true;
        Program.Observe("kaguya-timer-cleanup", new { type = "Godot.Timer", adapter = "managed-timer/v1" });
        return false;
    }
    private static bool SkipAssetLoad(ref Godot.Resource? __result)
    {
        __result = null;
        return false;
    }
    private static bool SkipPowerIcon(ref Godot.Texture2D? __result)
    {
        __result = null;
        return false;
    }
    private static bool SkipRelicHoverTips(ref IEnumerable<MegaCrit.Sts2.Core.HoverTips.IHoverTip> __result)
    {
        __result = Array.Empty<MegaCrit.Sts2.Core.HoverTips.IHoverTip>();
        return false;
    }
    private static bool HostHasArgument(string argument) => Environment.GetCommandLineArgs().Any(value => value == argument || value == "--" + argument);
    private static IEnumerable<CodeInstruction> CommandLineQueryBridge(IEnumerable<CodeInstruction> source)
    {
        var original = AccessTools.Method(typeof(CommandLineHelper), "HasArg", [typeof(string)])!;
        var replacement = AccessTools.Method(typeof(KaguyaNativeModLoop), nameof(HostHasArgument))!;
        var instructions = source.ToArray();
        var count = 0;
        foreach (var instruction in instructions) if (instruction.Calls(original))
        {
            instruction.opcode = System.Reflection.Emit.OpCodes.Call;
            instruction.operand = replacement;
            count++;
        }
        if (count != 1) throw new InvalidOperationException($"Expected one Kaguya ModManager command-line query; found {count}");
        return instructions;
    }

    private static (string BuildId, string ContentKey) ReadBuildIdentity()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../build-manifest.json"));
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return (doc.RootElement.GetProperty("buildId").GetString() ?? "",
            doc.RootElement.GetProperty("manifest").GetProperty("contentKey").GetString() ?? "");
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value && Directory.Exists(value)
        ? Path.GetFullPath(value) : throw new InvalidOperationException($"Missing Kaguya profile path: {name}");

    private static bool UserDataDir(ref string __result)
    {
        var root = Environment.GetEnvironmentVariable("SIMULATORLAB_KAGUYA_OUTPUT");
        if (string.IsNullOrWhiteSpace(root))
            root = Path.Combine((string)AppContext.GetData("LabRoot")!, ".build", "runs", "kaguya-stage8-isolated");
        var userData = Path.Combine(Path.GetFullPath(root), "kaguya-userdata");
        Directory.CreateDirectory(userData);
        __result = userData;
        return false;
    }

    private sealed class SnapshotFileIo(string snapshot) : IModManagerFileIo
    {
        public string[] GetFilesAt(string path)
        {
            var normalized = path.Replace('\\', '/').TrimEnd('/');
            string[] result = normalized.EndsWith("BaseLib", StringComparison.Ordinal)
                ? ["BaseLib.json"] : normalized.EndsWith("Kaguya", StringComparison.Ordinal) ? ["Kaguya.json"] : [];
            Events.Add("files:" + normalized + "=" + string.Join(',', result));
            return result;
        }
        public string[] GetDirectoriesAt(string path)
        {
            var normalized = path.Replace('\\', '/').TrimEnd('/');
            string[] result = normalized.EndsWith("/BaseLib", StringComparison.Ordinal) || normalized.EndsWith("/Kaguya", StringComparison.Ordinal) || normalized.EndsWith("mods_STEAMTEST", StringComparison.Ordinal) ? [] : ["BaseLib", "Kaguya"];
            Events.Add("dirs:" + normalized + "=" + string.Join(',', result));
            return result;
        }
        public bool FileExists(string path) => Map(path) is not null;
        public bool DirectoryExists(string path) => true;
        public Stream OpenStream(string path, Godot.FileAccess.ModeFlags mode) => mode == Godot.FileAccess.ModeFlags.Read
            ? File.OpenRead(Map(path) ?? throw new FileNotFoundException(path))
            : throw new InvalidOperationException("Kaguya profile write denied");
        public void MakeDirRecursive(string path) => throw new InvalidOperationException("Kaguya profile mkdir denied");
        public Error CopyFile(string sourcePath, string destinationPath) => throw new InvalidOperationException("Kaguya profile copy denied");
        private string? Map(string path)
        {
            var normalized = path.Replace('\\', '/');
            if (normalized.EndsWith("BaseLib/BaseLib.json", StringComparison.Ordinal)) return Path.Combine(snapshot, "3737335127", "BaseLib", "BaseLib.json");
            if (normalized.EndsWith("BaseLib/BaseLib.dll", StringComparison.Ordinal)) return Path.Combine(snapshot, "3737335127", "BaseLib", "BaseLib.dll");
            if (normalized.EndsWith("BaseLib/BaseLib.pck", StringComparison.Ordinal)) return Path.Combine(snapshot, "3737335127", "BaseLib", "BaseLib.pck");
            if (normalized.EndsWith("Kaguya/Kaguya.json", StringComparison.Ordinal)) return Path.Combine(snapshot, "3747548629", "Kaguya.json");
            if (normalized.EndsWith("Kaguya/Kaguya.dll", StringComparison.Ordinal)) return Path.Combine(snapshot, "3747548629", "Kaguya.dll");
            if (normalized.EndsWith("Kaguya/Kaguya.pck", StringComparison.Ordinal)) return Path.Combine(snapshot, "3747548629", "Kaguya.pck");
            return null;
        }
    }
}
