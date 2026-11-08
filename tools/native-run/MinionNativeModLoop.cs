using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Platform.Steam;
using MegaCrit.Sts2.Core.TestSupport;
using SimulatorLab.Core;
using SimulatorLab.Core.Mods;
using Environment = System.Environment;

namespace OfflineSearchHarness;

/// <summary>
/// Stage 9 native profile. It proves the exact dependency loader boundary and
/// MinionLib initialization in a fresh process, then emits scalar evidence for
/// the independent lifecycle fixture. A real Shadowverse combat is deliberately
/// outside this bounded slice and is reported as unsupported by the adapter.
/// </summary>
internal static class MinionNativeModLoop
{
    internal static bool Enabled => Environment.GetEnvironmentVariable("SIMULATORLAB_MINION_STAGE9") == "1";
    internal static bool IsScenario(string scenario) => scenario is "minion-stage9" or "minion-stage9-replay" or "minion-stage10" or "minion-stage10-replay";
    internal static bool IsRealCombatScenario(string scenario) => scenario is "minion-stage10" or "minion-stage10-replay";
    internal static Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
    {
        var simple = name.Name ?? "";
        var root = Environment.GetEnvironmentVariable("SIMULATORLAB_MINION_SNAPSHOT_ROOT");
        if (string.IsNullOrWhiteSpace(root)) return null;
        string[] candidates = simple switch
        {
            "MinionLib" => [Path.Combine(root, "3747595039", "MinionLib", "MinionLib.dll")],
            "shadowversebydmod" => [Path.Combine(root, "3747644378", "shadowversebydmod.dll")],
            "STS2-RitsuLib" => [Path.Combine(root, "3747602295", "compat/0.111.0/STS2-RitsuLib.dll")],
            "STS2-RitsuLib.Runtime" => [Path.Combine(root, "3747602295", "compat/0.111.0/STS2-RitsuLib.Runtime.dll")],
            "STS2-RitsuLib.Shared" => [Path.Combine(root, "3747602295", "shared/STS2-RitsuLib.Shared.dll")],
            "STS2-RitsuLib.Ui" => [Path.Combine(root, "3747602295", "shared/STS2-RitsuLib.Ui.dll")],
            "STS2-RitsuLib.Settings" => [Path.Combine(root, "3747602295", "shared/STS2-RitsuLib.Settings.dll")],
            _ => Array.Empty<string>()
        };
        var path = candidates.FirstOrDefault(File.Exists);
        if (path is null) return null;
        var assembly = context.LoadFromAssemblyPath(path);
        Loaded[simple] = assembly;
        if (!LoadOrder.Contains(simple, StringComparer.Ordinal)) LoadOrder.Add(simple);
        return assembly;
    }
    internal static string SnapshotRoot => Required("SIMULATORLAB_MINION_SNAPSHOT_ROOT");
    internal static readonly Harmony Patches = new("simulatorlab.minion.stage9");
    internal static readonly Dictionary<string, Assembly> Loaded = new(StringComparer.Ordinal);
    internal static readonly List<string> Events = [];
    internal static readonly List<string> LoadOrder = [];
    internal static MinionLifecycleObservation? MinionObservation;
    internal static MinionComponentObservation? ComponentObservation;
    internal static MinionRealCombatObservation? RealCombatObservation;
    internal static readonly List<MinionCombatEvent> RealCombatEvents = [];
    internal static bool MinionInitialized;
    internal static bool ShadowverseLoaded;
    internal static bool ShadowverseRegistered;

    internal static void Initialize()
    {
        if (!Enabled) return;
        if (!TestMode.IsOn || SteamInitializer.Initialized)
            throw new InvalidOperationException("Minion stage 9 requires offline TestMode");
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
            transpiler: new HarmonyMethod(typeof(MinionNativeModLoop), nameof(CommandLineQueryBridge)));
        var scriptLookup = AccessTools.Method(AccessTools.TypeByName("Godot.Bridge.ScriptManagerBridge"), "LookupScriptsInAssembly");
        if (scriptLookup is not null)
            Patches.Patch(scriptLookup, prefix: new HarmonyMethod(typeof(MinionNativeModLoop), nameof(SkipGodotScriptLookup)));

        ModManager.ResetForTests();
        var settings = new ModSettings { PlayerAgreedToModLoading = true };
        foreach (var id in new[] { MinionStage9Constants.MinionLibId, MinionStage9Constants.RitsuLibId, MinionStage9Constants.ShadowverseModId })
        {
            settings.ModList.Add(new SettingsSaveMod { Id = id, IsEnabled = true });
            ModManager.TestInitializers[id] = () => InitializeAssembly(id);
        }
        var task = ModManager.Initialize(new SnapshotFileIo(SnapshotRoot), settings,
            SemanticVersion.FromString(MinionStage9Constants.TargetGameVersion));
        ((MainLoopContext)SynchronizationContext.Current!).RunUntilCompleted(task,
            TimeSpan.FromSeconds(45), "Minion stage 9 ModManager.Initialize");
        foreach (var mod in ModManager.Mods)
        {
            Events.Add($"loader:{mod.manifest?.id}:{mod.state}:{mod.assemblies.Count}");
            if (mod.state.ToString() != "Loaded" || mod.errors?.Count > 0 || mod.assemblies.Count == 0)
                throw new InvalidOperationException($"stage 9 loader rejected {mod.manifest?.id}: {mod.state}; errors={string.Join(" || ", mod.errors ?? [])}");
        }
        if (ModManager.Mods.Count != 3)
            throw new InvalidOperationException("stage 9 loaded an unexpected Mod inventory: " +
                string.Join(",", ModManager.Mods.Select(m => $"{m.manifest?.id}:{m.state}:{m.assemblies.Count}")));
        Events.Add("stage9-loader:complete");
        Events.Add("native-loader");
    }

    internal static Type[] IncludeModels(Type[] baseModels)
    {
        var result = baseModels.ToList();
        var abstractModel = typeof(AbstractModel);
        foreach (var assembly in Loaded.Values)
        {
            foreach (var name in new[]
            {
                "shadowversebydmod.RitsuAdapters.ModMinionTemplate",
                "shadowversebydmod.RitsuAdapters.ModComponentsCardTemplate",
                MinionStage10Constants.RealMinionType,
                MinionStage10Constants.RealComponentCardType,
                "shadowversebydmod.Scripts.Cards.CardCraft.Swordcraft.NormalKnight",
                "shadowversebydmod.Scripts.Powers.SwordcraftSoldierSyncPower"
            })
            {
                var type = assembly.GetType(name, false);
                if (type is not null && type.IsClass && !type.IsAbstract && abstractModel.IsAssignableFrom(type)) result.Add(type);
            }
        }
        return result.Distinct().ToArray();
    }

    internal static void RunLifecycleFixture(string seed)
    {
        var fixture = MinionStage9Fixture.Run(seed);
        MinionObservation = fixture.Minion;
        ComponentObservation = fixture.Component;
        foreach (var item in fixture.Events)
        {
            Events.Add("minion-" + item);
            Program.Observe(item switch
            {
                "register" => "minion-register",
                "summon" => "minion-summon",
                "component-applied" => "component-result",
                "combat-end" => "combat-end",
                "cleanup" => "cleanup",
                _ => "minion-event"
            }, new { id = fixture.Minion.Id, uniqueIdentity = fixture.Minion.UniqueIdentity,
                component = fixture.Component.CardId, evidence = fixture.Minion.EvidenceRef });
        }
    }

    internal static void RecordRealCombat(MinionRealCombatObservation observation, IEnumerable<MinionCombatEvent> events)
    {
        RealCombatObservation = observation;
        RealCombatEvents.Clear();
        RealCombatEvents.AddRange(events);
        Events.AddRange(events.Select(x => x.Kind));
    }

    internal static void WriteProbe(string output, string seed, bool replay,
        NativeRunTerminal terminal, NativeRunStop stop, string? error)
    {
        if (!Enabled) return;
        var build = ReadBuildIdentity();
        var observedKinds = Program.WatcherEvents.Select(x =>
        {
            try { return JsonSerializer.SerializeToElement(x, Program.Json).GetProperty("kind").GetString() ?? ""; }
            catch { return ""; }
        }).Where(x => x.Length > 0);
        var eventNames = Events.Concat(observedKinds).Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var digestInput = JsonSerializer.Serialize(new
        {
            seed, terminal, stop, LoadOrder, MinionInitialized, ShadowverseLoaded,
            ShadowverseRegistered, MinionObservation, ComponentObservation,
            RealCombatObservation, Events = eventNames
        }, Program.Json);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(digestInput))).ToLowerInvariant();
        var probe = new MinionRuntimeProbe(
            IsRealCombatScenario(Environment.GetCommandLineArgs().ElementAtOrDefault(5) ?? "")
                ? MinionStage10Constants.ProbeSchema : MinionStage9Constants.ProbeSchema,
            error is null && stop == NativeRunStop.SliceBoundary,
            MinionStage9Constants.TargetGameVersion, MinionStage9Constants.SnapshotId, seed,
            build.BuildId, build.ContentKey, MinionInitialized, ShadowverseLoaded,
            ShadowverseRegistered, IsRealCombatScenario(Environment.GetCommandLineArgs().ElementAtOrDefault(5) ?? "") && RealCombatObservation is not null,
            LoadOrder.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            eventNames, MinionObservation, ComponentObservation, true, true, digest, null,
            error is null && stop == NativeRunStop.SliceBoundary ? "lifecycle-complete" : "runtime-failed",
            error is null ? null : "native-run", error, RealCombatObservation, RealCombatEvents.ToArray());
        var stage10 = IsRealCombatScenario(Environment.GetCommandLineArgs().ElementAtOrDefault(5) ?? "");
        File.WriteAllText(Path.Combine(output, replay
                ? (stage10 ? "minion-stage10-probe-replay.json" : "minion-stage9-probe-replay.json")
                : (stage10 ? "minion-stage10-probe.json" : "minion-stage9-probe.json")),
            JsonSerializer.Serialize(probe, ModWorkerJson.Options));
    }

    private static void InitializeAssembly(string id)
    {
        switch (id)
        {
            case MinionStage9Constants.MinionLibId:
            {
                var assembly = Load(id, Path.Combine(SnapshotRoot, "3747595039", "MinionLib", "MinionLib.dll"));
                var initializer = assembly.GetType("MinionLib.MainFile", true)!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)!;
                initializer.Invoke(null, null);
                MinionInitialized = true;
                Events.Add("minionlib-initialized");
                break;
            }
            case MinionStage9Constants.RitsuLibId:
                LoadRitsu();
                Events.Add("ritsulib-compat-0.111.0-loaded");
                break;
            case MinionStage9Constants.ShadowverseModId:
            {
                var assembly = Load(id, Path.Combine(SnapshotRoot, "3747644378", "shadowversebydmod.dll"));
                ShadowverseLoaded = true;
                var bootstrap = assembly.GetType("shadowversebydmod.RitsuLibAdapters.RitsuContentBootstrap", false);
                var register = bootstrap?.GetMethod("RegisterContent", BindingFlags.Public | BindingFlags.Static, [typeof(string), typeof(Assembly)]);
                if (register is not null)
                {
                    register.Invoke(null, [id, assembly]);
                    ShadowverseRegistered = true;
                    Events.Add("shadowverse-content-registered");
                }
                else Events.Add("shadowverse-content-registration-unavailable");
                break;
            }
            default: throw new InvalidOperationException("Unexpected stage 9 assembly: " + id);
        }
    }

    private static void LoadRitsu()
    {
        var root = Path.Combine(SnapshotRoot, "3747602295");
        foreach (var file in new[] { "shared/STS2-RitsuLib.Shared.dll", "shared/STS2-RitsuLib.Settings.dll", "shared/STS2-RitsuLib.Ui.dll", "compat/0.111.0/STS2-RitsuLib.Runtime.dll", "compat/0.111.0/STS2-RitsuLib.dll" })
        {
            var path = Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar));
            var name = Path.GetFileNameWithoutExtension(path);
            var assembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => a.GetName().Name == name)
                ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            Loaded[name] = assembly;
            LoadOrder.Add(name);
        }
        var main = Loaded["STS2-RitsuLib"];
        ModManager.AssociateAssemblyWithMod(MinionStage9Constants.RitsuLibId, main);
    }

    private static Assembly Load(string id, string path)
    {
        var assembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => a.GetName().Name == id)
            ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        Loaded[id] = assembly;
        LoadOrder.Add(id);
        ModManager.AssociateAssemblyWithMod(id, assembly);
        return assembly;
    }

    private static void Patch(Type type, string method, string prefix) =>
        Patches.Patch(AccessTools.Method(type, method)!, prefix: new HarmonyMethod(typeof(MinionNativeModLoop), prefix));
    private static bool ExecutablePath(ref string __result) { __result = Path.Combine(SnapshotRoot, "minion-stage9.exe"); return false; }
    private static bool NotDebug(ref bool __result) { __result = false; return false; }
    private static bool CommandLine(ref string[] __result) { __result = []; return false; }
    private static bool RejectCopy() => throw new InvalidOperationException("stage 9 ModManager attempted forbidden save copy");
    private static bool SkipGodotScriptLookup() => false;
    private static bool HostHasArgument(string argument) => Environment.GetCommandLineArgs().Any(value => value == argument || value == "--" + argument);
    private static IEnumerable<CodeInstruction> CommandLineQueryBridge(IEnumerable<CodeInstruction> source)
    {
        var original = AccessTools.Method(typeof(CommandLineHelper), "HasArg", [typeof(string)])!;
        var replacement = AccessTools.Method(typeof(MinionNativeModLoop), nameof(HostHasArgument))!;
        var instructions = source.ToArray(); var count = 0;
        foreach (var instruction in instructions) if (instruction.Calls(original)) { instruction.opcode = System.Reflection.Emit.OpCodes.Call; instruction.operand = replacement; count++; }
        if (count != 1) throw new InvalidOperationException($"Expected one stage 9 ModManager command-line query; found {count}");
        return instructions;
    }
    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value && Directory.Exists(value)
        ? Path.GetFullPath(value) : throw new InvalidOperationException($"Missing stage 9 profile path: {name}");
    private static bool UserDataDir(ref string __result)
    {
        var root = Environment.GetEnvironmentVariable("SIMULATORLAB_MINION_OUTPUT");
        if (string.IsNullOrWhiteSpace(root)) root = Path.Combine((string)AppContext.GetData("LabRoot")!, ".build", "runs", "minion-stage9-isolated");
        var userData = Path.Combine(Path.GetFullPath(root), "minion-userdata"); Directory.CreateDirectory(userData); __result = userData; return false;
    }

    private sealed class SnapshotFileIo(string snapshot) : IModManagerFileIo
    {
        public string[] GetFilesAt(string path)
        {
            var normalized = path.Replace('\\', '/').TrimEnd('/');
            var result = normalized.EndsWith("MinionLib", StringComparison.Ordinal) ? new[] { "MinionLib.json" } :
                normalized.EndsWith("STS2-RitsuLib", StringComparison.Ordinal) ? new[] { "mod_manifest.json" } :
                normalized.EndsWith("shadowversebydmod", StringComparison.Ordinal) ? new[] { "shadowversebydmod.json" } : Array.Empty<string>();
            Events.Add("files:" + normalized + "=" + string.Join(',', result)); return result;
        }
        public string[] GetDirectoriesAt(string path)
        {
            var normalized = path.Replace('\\', '/').TrimEnd('/');
            var result = normalized.EndsWith("mods_STEAMTEST", StringComparison.Ordinal) ? Array.Empty<string>() :
                normalized.EndsWith("/MinionLib", StringComparison.Ordinal) || normalized.EndsWith("/STS2-RitsuLib", StringComparison.Ordinal) || normalized.EndsWith("/shadowversebydmod", StringComparison.Ordinal) ? Array.Empty<string>() :
                new[] { "MinionLib", "STS2-RitsuLib", "shadowversebydmod" };
            Events.Add("dirs:" + normalized + "=" + string.Join(',', result)); return result;
        }
        public bool FileExists(string path) => Map(path) is not null;
        public bool DirectoryExists(string path) => true;
        public Stream OpenStream(string path, Godot.FileAccess.ModeFlags mode) => mode == Godot.FileAccess.ModeFlags.Read ? File.OpenRead(Map(path) ?? throw new FileNotFoundException(path)) : throw new InvalidOperationException("stage 9 profile write denied");
        public void MakeDirRecursive(string path) => throw new InvalidOperationException("stage 9 profile mkdir denied");
        public Error CopyFile(string sourcePath, string destinationPath) => throw new InvalidOperationException("stage 9 profile copy denied");
        private string? Map(string path)
        {
            var normalized = path.Replace('\\', '/');
            if (normalized.EndsWith("MinionLib/MinionLib.json", StringComparison.Ordinal)) return Path.Combine(snapshot, "3747595039/MinionLib/MinionLib.json");
            if (normalized.EndsWith("MinionLib/MinionLib.dll", StringComparison.Ordinal)) return Path.Combine(snapshot, "3747595039/MinionLib/MinionLib.dll");
            if (normalized.EndsWith("MinionLib/MinionLib.pck", StringComparison.Ordinal)) return Path.Combine(snapshot, "3747595039/MinionLib/MinionLib.pck");
            if (normalized.EndsWith("STS2-RitsuLib/mod_manifest.json", StringComparison.Ordinal)) return Path.Combine(snapshot, "3747602295/mod_manifest.json");
            if (normalized.EndsWith("shadowversebydmod/shadowversebydmod.json", StringComparison.Ordinal)) return Path.Combine(snapshot, "3747644378/shadowversebydmod.json");
            if (normalized.EndsWith("shadowversebydmod/shadowversebydmod.dll", StringComparison.Ordinal)) return Path.Combine(snapshot, "3747644378/shadowversebydmod.dll");
            if (normalized.EndsWith("shadowversebydmod/shadowversebydmod.pck", StringComparison.Ordinal)) return Path.Combine(snapshot, "3747644378/shadowversebydmod.pck");
            return null;
        }
    }

    private static (string BuildId, string ContentKey) ReadBuildIdentity()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../build-manifest.json"));
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return (doc.RootElement.GetProperty("buildId").GetString() ?? "", doc.RootElement.GetProperty("manifest").GetProperty("contentKey").GetString() ?? "");
    }
}
