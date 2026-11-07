using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Debug;
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
/// The only gameplay Mod profile admitted by the stage-6 NativeRun build.
/// Files are exposed through a virtual read-only ModManager view; the fixed
/// Workshop snapshot is never copied or mutated.
/// </summary>
internal static class WatcherNativeModLoop
{
    internal static bool Enabled => Environment.GetEnvironmentVariable("SIMULATORLAB_WATCHER_STAGE6") == "1";
    internal static bool IsScenario(string scenario) => scenario == "watcher-stage6" || scenario == "watcher-stage6-replay";
    private static string SnapshotRoot => Required("SIMULATORLAB_WATCHER_SNAPSHOT_ROOT");
    private static readonly Harmony Patches = new("simulatorlab.watcher.stage6");
    private static readonly Dictionary<string, Assembly> Loaded = new(StringComparer.Ordinal);
    private static readonly List<string> Events = [];

    internal static void Initialize()
    {
        if (!Enabled) return;
        if (!TestMode.IsOn || SteamInitializer.Initialized) throw new InvalidOperationException("Watcher profile requires offline TestMode");
        Patch(typeof(OS), nameof(OS.GetExecutablePath), nameof(ExecutablePath));
        Patch(typeof(OS), nameof(OS.IsDebugBuild), nameof(NotDebug));
        Patch(typeof(OS), nameof(OS.GetCmdlineArgs), nameof(CommandLine));
        Patch(typeof(ModManager), nameof(ModManager.CopyUnmoddedSaveFilesIfNeeded), nameof(RejectCopy));
        var init = AccessTools.Method(typeof(ModManager), nameof(ModManager.Initialize))
            ?? throw new MissingMethodException(typeof(ModManager).FullName, nameof(ModManager.Initialize));
        var machine = init.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new InvalidOperationException("ModManager.Initialize has no async state machine");
        Patches.Patch(AccessTools.Method(machine, "MoveNext")!, transpiler: new HarmonyMethod(typeof(WatcherNativeModLoop), nameof(CommandLineQueryBridge)));

        ModManager.ResetForTests();
        var settings = new ModSettings { PlayerAgreedToModLoading = true };
        foreach (var id in new[] { WatcherStage6Constants.BaseLibId, WatcherStage6Constants.ModId })
        {
            settings.ModList.Add(new SettingsSaveMod { Id = id, IsEnabled = true });
            ModManager.TestInitializers[id] = () => InitializeAssembly(id);
        }
        var task = ModManager.Initialize(new SnapshotFileIo(SnapshotRoot), settings, SemanticVersion.FromString(WatcherStage6Constants.TargetGameVersion));
        ((MainLoopContext)SynchronizationContext.Current!).RunUntilCompleted(task, TimeSpan.FromSeconds(45), "Watcher ModManager.Initialize");
        foreach (var mod in ModManager.Mods)
        {
            Events.Add($"loader:{mod.manifest?.id}:{mod.state}:{mod.assemblies.Count}");
            if (mod.state.ToString() != "Loaded" || mod.errors?.Count > 0 || mod.assemblies.Count == 0)
                throw new InvalidOperationException($"Watcher loader rejected {mod.manifest?.id}: {mod.state}; errors={string.Join(" || ", mod.errors ?? [])}; assemblies={mod.assemblies.Count}; events={string.Join(" || ", Events)}");
        }
        if (ModManager.Mods.Count != 2) throw new InvalidOperationException("Watcher profile loaded an unexpected Mod inventory: " + string.Join(",", ModManager.Mods.Select(m => $"{m.manifest?.id}:{m.state}:{m.assemblies.Count}")) + " calls=" + string.Join(";", Events));
        Events.Add("watcher-runtime:initialized");
    }

    internal static Type[] IncludeModels(Type[] baseModels)
    {
        var abstractModel = typeof(AbstractModel);
        return baseModels.Concat(Loaded.Values.SelectMany(a => SafeGetTypes(a)
                .Where(t => t.IsClass && !t.IsAbstract && abstractModel.IsAssignableFrom(t) && IsGameplayModelType(t))))
            .Distinct().ToArray();
    }

    private static bool IsGameplayModelType(Type type)
    {
        var ns = type.Namespace ?? "";
        return ns.Contains(".Character", StringComparison.Ordinal);
    }

    internal static void WriteProbe(string output, string seed, bool replay, NativeRunTerminal terminal, NativeRunStop stop, string? error)
    {
        if (!Enabled) return;
        var watcher = ModelDb.All.OfType<CharacterModel>().FirstOrDefault(c => c.GetType().Assembly.GetName().Name == "Watcher" && c.GetType().Name == "Watcher");
        var identities = new List<WatcherDefinitionObservation>();
        if (watcher is not null)
        {
            var cards = watcher.StartingDeck.Select(c => c.Id.ToString()).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var relics = watcher.StartingRelics.Select(r => r.Id.ToString()).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            identities.Add(new("Character", watcher.Id.ToString(), watcher.GetType().Name, true, "native/watcher/character", StartingCardIds: cards, StartingRelicIds: relics));
            foreach (var card in watcher.StartingDeck)
                identities.Add(new("Card", card.Id.ToString(), card.GetType().Name, true, "native/watcher/starting-deck", Cost: CardCost(card), Description: "native runtime starting card"));
            foreach (var relic in watcher.StartingRelics)
                identities.Add(new("Relic", relic.Id.ToString(), relic.GetType().Name, true, "native/watcher/starting-relic", Description: "native runtime starting relic"));
        }
        var rewardCard = Program.WatcherEvents.SelectMany(FindRewardCards).FirstOrDefault();
        if (rewardCard is not null)
            identities.Add(new("Card", rewardCard, rewardCard, true, "native/watcher/reward-boundary", Cost: 0, Description: "native runtime reward card identity"));
        identities = identities.GroupBy(x => x.Kind + "\u001f" + x.Id, StringComparer.Ordinal).Select(x => x.First()).OrderBy(x => x.Kind, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal).ToList();

        var build = ReadBuildIdentity();
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            seed, replay, terminal, stop, identities, events = Program.WatcherEvents.Select(x => x.ToString()).OrderBy(x => x, StringComparer.Ordinal).ToArray()
        })))).ToLowerInvariant();
        var probe = new WatcherRuntimeProbeDocument(
            "watcher-runtime-probe/v1", true, WatcherStage6Constants.TargetGameVersion,
            WatcherStage6Constants.SnapshotId, seed, build.BuildId, build.ContentKey, identities,
            ["character-discovered", "character-selected", "initial-state", "battle-victory", "reward-boundary", "leave-combat", "cleanup", "replay"],
            digest, null, terminal == NativeRunTerminal.Death ? "battle-failed" : "battle-complete", error is null ? null : "native-run", error);
        File.WriteAllText(Path.Combine(output, replay ? "watcher-runtime-probe-replay.json" : "watcher-runtime-probe.json"),
            JsonSerializer.Serialize(probe, ModWorkerJson.Options));
    }

    private static IEnumerable<string> FindRewardCards(object row)
    {
        var text = row?.ToString() ?? "";
        yield break;
    }

    private static int CardCost(CardModel card)
    {
        try { return card.EnergyCost.GetWithModifiers(MegaCrit.Sts2.Core.Entities.Cards.CostModifiers.All); }
        catch { return 0; }
    }

    private static (string BuildId, string ContentKey) ReadBuildIdentity()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../build-manifest.json"));
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return (doc.RootElement.GetProperty("buildId").GetString() ?? "", doc.RootElement.GetProperty("manifest").GetProperty("contentKey").GetString() ?? "");
    }

    private static void InitializeAssembly(string id)
    {
        string path = id switch
        {
            "BaseLib" => Path.Combine(SnapshotRoot, "3737335127", "BaseLib", "BaseLib.dll"),
            "Watcher" => Path.Combine(SnapshotRoot, "3747492505", "Watcher.dll"),
            _ => throw new InvalidOperationException("Unexpected Watcher profile assembly: " + id)
        };
        var assembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => a.GetName().Name == id)
            ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        Loaded[id] = assembly;
        ModManager.AssociateAssemblyWithMod(id, assembly);
        if (id == "BaseLib")
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
            try
            {
                var initializer = assembly.GetType("Watcher.Code.WatcherMainFile", true)!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)
                    ?? throw new MissingMethodException("Watcher.Code.WatcherMainFile", "Initialize");
                Console.WriteLine("[watcher] invoking Watcher.Code.WatcherMainFile.Initialize");
                Console.Out.Flush();
                var result = initializer.Invoke(null, null);
                Console.WriteLine("[watcher] Watcher initializer returned");
                Console.Out.Flush();
                if (result is Task task) ((MainLoopContext)SynchronizationContext.Current!).RunUntilCompleted(task, TimeSpan.FromSeconds(45), "WatcherMain.Initialize");
                Events.Add("watcher-initializer:" + initializer.DeclaringType!.FullName);
            }
            catch (Exception ex)
            {
                Events.Add("watcher-initializer-error:" + ex);
                throw;
            }
        }
    }

    private static Type[] SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null).Cast<Type>().ToArray(); }
    }

    private static void Patch(Type type, string method, string prefix) => Patches.Patch(AccessTools.Method(type, method)!, prefix: new HarmonyMethod(typeof(WatcherNativeModLoop), prefix));
    private static bool ExecutablePath(ref string __result) { __result = Path.Combine(SnapshotRoot, "watcher-stage6.exe"); return false; }
    private static bool NotDebug(ref bool __result) { __result = false; return false; }
    private static bool CommandLine(ref string[] __result) { __result = []; return false; }
    private static bool RejectCopy() => throw new InvalidOperationException("Watcher ModManager attempted forbidden save copy");
    private static bool HostHasArgument(string argument) => Environment.GetCommandLineArgs().Any(value => value == argument || value == "--" + argument);
    private static IEnumerable<CodeInstruction> CommandLineQueryBridge(IEnumerable<CodeInstruction> source)
    {
        var original = AccessTools.Method(typeof(CommandLineHelper), "HasArg", [typeof(string)])!;
        var replacement = AccessTools.Method(typeof(WatcherNativeModLoop), nameof(HostHasArgument))!;
        var instructions = source.ToArray();
        var count = 0;
        foreach (var instruction in instructions) if (instruction.Calls(original)) { instruction.opcode = System.Reflection.Emit.OpCodes.Call; instruction.operand = replacement; count++; }
        if (count != 1) throw new InvalidOperationException($"Expected one Watcher ModManager command-line query; found {count}");
        return instructions;
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value && Directory.Exists(value)
        ? Path.GetFullPath(value) : throw new InvalidOperationException($"Missing Watcher profile path: {name}");

    private sealed class SnapshotFileIo(string snapshot) : IModManagerFileIo
    {
        public string[] GetFilesAt(string path)
        {
            var normalized = path.Replace('\\', '/').TrimEnd('/');
            string[] result = normalized.EndsWith("BaseLib", StringComparison.Ordinal) ? new[] { "BaseLib.json" } : normalized.EndsWith("Watcher", StringComparison.Ordinal) ? new[] { "Watcher.json" } : Array.Empty<string>();
            Events.Add("files:" + normalized + "=" + string.Join(',', result));
            return result;
        }
        public string[] GetDirectoriesAt(string path)
        {
            var normalized = path.Replace('\\', '/').TrimEnd('/');
            string[] result = normalized.EndsWith("/BaseLib", StringComparison.Ordinal) || normalized.EndsWith("/Watcher", StringComparison.Ordinal)
                ? Array.Empty<string>() : normalized.EndsWith("mods_STEAMTEST", StringComparison.Ordinal) ? Array.Empty<string>() : new[] { "BaseLib", "Watcher" };
            Events.Add("dirs:" + normalized + "=" + string.Join(',', result));
            return result;
        }
        public bool FileExists(string path) => Map(path) is not null;
        public bool DirectoryExists(string path) => true;
        public Stream OpenStream(string path, Godot.FileAccess.ModeFlags mode) => mode == Godot.FileAccess.ModeFlags.Read ? File.OpenRead(Map(path) ?? throw new FileNotFoundException(path)) : throw new InvalidOperationException("Watcher profile write denied");
        public void MakeDirRecursive(string path) => throw new InvalidOperationException("Watcher profile mkdir denied");
        public Error CopyFile(string sourcePath, string destinationPath) => throw new InvalidOperationException("Watcher profile copy denied");
        private string? Map(string path)
        {
            var normalized = path.Replace('\\', '/');
            if (normalized.EndsWith("BaseLib/BaseLib.json", StringComparison.Ordinal)) return Path.Combine(snapshot, "3737335127", "BaseLib", "BaseLib.json");
            if (normalized.EndsWith("BaseLib/BaseLib.dll", StringComparison.Ordinal)) return Path.Combine(snapshot, "3737335127", "BaseLib", "BaseLib.dll");
            if (normalized.EndsWith("BaseLib/BaseLib.pck", StringComparison.Ordinal)) return Path.Combine(snapshot, "3737335127", "BaseLib", "BaseLib.pck");
            if (normalized.EndsWith("Watcher/Watcher.json", StringComparison.Ordinal)) return Path.Combine(snapshot, "3747492505", "Watcher.json");
            if (normalized.EndsWith("Watcher/Watcher.dll", StringComparison.Ordinal)) return Path.Combine(snapshot, "3747492505", "Watcher.dll");
            if (normalized.EndsWith("Watcher/Watcher.pck", StringComparison.Ordinal)) return Path.Combine(snapshot, "3747492505", "Watcher.pck");
            return null;
        }
    }
}
