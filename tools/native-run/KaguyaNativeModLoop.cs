using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Platform.Steam;
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
    internal static bool IsScenario(string scenario) => scenario is "kaguya-stage8" or "kaguya-stage8-replay";
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
        Events.Add("kaguya-runtime:initialized");
    }

    internal static Type[] IncludeModels(Type[] baseModels)
    {
        var abstractModel = typeof(AbstractModel);
        return baseModels.Concat(Loaded.Values.SelectMany(SafeGetTypes)
                .Where(t => t.IsClass && !t.IsAbstract && abstractModel.IsAssignableFrom(t) &&
                    (typeof(MonsterModel).IsAssignableFrom(t) || typeof(EncounterModel).IsAssignableFrom(t) ||
                     typeof(MegaCrit.Sts2.Core.Models.EventModel).IsAssignableFrom(t) ||
                     typeof(CharacterModel).IsAssignableFrom(t) ||
                     typeof(CardPoolModel).IsAssignableFrom(t) ||
                     typeof(PotionPoolModel).IsAssignableFrom(t) ||
                     typeof(RelicPoolModel).IsAssignableFrom(t) ||
                     typeof(PowerModel).IsAssignableFrom(t))))
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
        var probe = new KaguyaRuntimeProbe(
            KaguyaStage8Constants.ProbeSchema, error is null && stop == NativeRunStop.SliceBoundary,
            KaguyaStage8Constants.TargetGameVersion, KaguyaStage8Constants.SnapshotId, seed,
            build.BuildId, build.ContentKey, KaguyaStage8Constants.ModId, EncounterId,
            SelectedEncounterType ?? KaguyaStage8Constants.EncounterType, EnemyId,
            SelectedEnemyType ?? KaguyaStage8Constants.EnemyType,
            EnemyObservation, RewardObservation?.RewardId, RewardObservation, GoldBefore,
            goldAfter, eventNames, evidence, digest, null,
            error is null && stop == NativeRunStop.SliceBoundary ? "battle-complete" : "runtime-failed",
            error is null ? null : "native-run", error,
            EventObservation?.Id, SelectedEventType, EventObservation);
        File.WriteAllText(Path.Combine(output, replay ? "kaguya-stage8-probe-replay.json" : "kaguya-stage8-probe.json"),
            JsonSerializer.Serialize(probe, ModWorkerJson.Options));
    }

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
        }
        else
        {
            // The stage is intentionally scoped to the registered Nay and
            // encounter model types. Kaguya's full initializer also installs
            // unrelated card/relic/potion pools; invoking it would expand the
            // ModelDb surface beyond this phase and require UI/save seams.
            Events.Add("kaguya-initializer:scoped-enemy-encounter-profile");
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
