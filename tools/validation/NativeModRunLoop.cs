using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.CompilerServices;
using System.Reflection.Emit;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Debug;
using Environment = System.Environment;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Platform.Steam;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;

namespace OfflineSearchHarness;

/// <summary>Bounded native ModManager test profile. No full framework, UI or persistent IO initialization.</summary>
internal static partial class NativeModRunLoop
{
    public static bool Enabled => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_LOOP_STAGE"));
    private static readonly List<string> Events = [];
    private static string ProfileRoot => Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_PROFILE")
        ?? throw new InvalidOperationException("Native bootstrap profile path missing.");
    private static readonly Harmony EnvironmentPatches = new("simulatorlab.native-mod.environment");
    private static string? DiscoveryStamp;
    private static bool DiscoveryNegativePassed;

    public static void AssertDiscoveryStable()
    {
        string signature = string.Join("\n", new[] { "AllCharacters", "AllSharedCardPools" }.Select(name =>
        {
            var method=AccessTools.PropertyGetter(typeof(ModelDb),name);
            var info=Harmony.GetPatchInfo(method);
            return name+":"+string.Join("|", (info?.Prefixes.AsEnumerable() ?? Enumerable.Empty<Patch>()).Concat(info?.Postfixes.AsEnumerable() ?? Enumerable.Empty<Patch>()).Concat(info?.Transpilers.AsEnumerable() ?? Enumerable.Empty<Patch>()).Concat(info?.Finalizers.AsEnumerable() ?? Enumerable.Empty<Patch>())
                .Select(p=>$"{p.owner}:{p.priority}:{p.PatchMethod.Module.ModuleVersionId}:{p.PatchMethod.MetadataToken}:{string.Join(',',p.before)}:{string.Join(',',p.after)}").OrderBy(s=>s,StringComparer.Ordinal));
        }));
        string current=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
        if (DiscoveryStamp!=null && DiscoveryStamp!=current) throw new InvalidOperationException("Native discovery patch profile changed after freeze.");
        DiscoveryStamp=current;
    }

    public static void Initialize()
    {
        if (!TestMode.IsOn || SteamInitializer.Initialized) throw new InvalidOperationException("Unsafe host.");
        typeof(EleanorV040Probe).GetMethod("InstallOfflineWriteGuards", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null);
        _=EleanorV040Probe.CaptureOfflineWriteEvidence();
        Patch(typeof(OS), nameof(OS.GetExecutablePath), nameof(ExecutablePath));
        Patch(typeof(OS), nameof(OS.IsDebugBuild), nameof(NotDebug));
        Patch(typeof(OS), nameof(OS.GetCmdlineArgs), nameof(CommandLine));
        Patch(typeof(ModManager), nameof(ModManager.CopyUnmoddedSaveFilesIfNeeded), nameof(RejectCopy));
        MethodInfo init = AccessTools.Method(typeof(ModManager), nameof(ModManager.Initialize));
        Type machine = init.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
        EnvironmentPatches.Patch(AccessTools.Method(machine, "MoveNext"), transpiler: new HarmonyMethod(typeof(NativeModRunLoop), nameof(CommandLineQueryBridge)));
        ModManager.ResetForTests();
        var settings = new ModSettings { PlayerAgreedToModLoading = true };
        string[] ids = ["BaseLib", "STS2-RitsuLib", "MyMod"];
        foreach (string id in ids)
        {
            settings.ModList.Add(new SettingsSaveMod { Id = id, IsEnabled = true });
            ModManager.TestInitializers[id] = () => InitializeRealAssembly(id);
        }
        var task = ModManager.Initialize(new PinnedFileIo(ProfileRoot), settings, SemanticVersion.FromString("0.111.0"));
        ((MainLoopContext)SynchronizationContext.Current!).RunUntilCompleted(task, TimeSpan.FromSeconds(20), "Native manifest discovery");
        foreach (var mod in ModManager.Mods)
        {
            Events.Add($"loader:{mod.manifest?.id}:{mod.state}:{mod.assemblies.Count}");
            if (mod.state.ToString() != "Loaded" || mod.errors?.Count > 0 || mod.assemblies.Count == 0)
                throw new InvalidOperationException($"Native loader rejected {mod.manifest?.id}: {mod.state}.");
        }
        if (ModManager.Mods.Count != 3) throw new InvalidOperationException("Unexpected native Mod inventory.");
    }

    private static void InitializeRealAssembly(string id)
    {
        string modPath = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_DLL")!;
        string deps = Path.Combine(Path.GetDirectoryName(modPath)!, "dependencies");
        string path = id switch
        {
            "MyMod" => modPath,
            "BaseLib" => Path.Combine(deps, "BaseLib.dll"),
            _ => Path.Combine(deps, "ritsu", "compat", "0.111.0", "STS2-RitsuLib.dll")
        };
        Assembly assembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => a.GetName().Name == id)
            ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        ModManager.AssociateAssemblyWithMod(id, assembly);
        if (id == "BaseLib")
        {
            foreach (string name in new[] { "AddCustomCharacters", "ModelDbSharedCardPoolsPatch" })
            {
                Type patch = assembly.GetType("BaseLib.Patches.Content." + name, true)!;
                new Harmony("BaseLib").CreateClassProcessor(patch).Patch();
                Events.Add("original-discovery-patch:" + patch.FullName);
            }
        }
        if (id == "STS2-RitsuLib")
        {
            // The role uses the pure assembly registry API; framework UI/save/lifecycle startup is excluded.
            foreach (Assembly dependency in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name?.StartsWith("STS2-RitsuLib", StringComparison.Ordinal) == true))
                if (dependency != assembly) ModManager.AssociateAssemblyWithMod(id, dependency);
            Events.Add("dependency-profile:assembly-ownership;no-full-framework-initializer");
        }
        if (id == "MyMod")
            assembly.GetType("MyMod.MyModCode.MainFile", true)!.GetMethod("Initialize")!.Invoke(null, null);
        Events.Add("initialized-profile:" + id);
    }

    public static object PoolPreflight()
    {
        AssertDiscoveryStable();
        if (!DiscoveryNegativePassed)
        {
            var target=AccessTools.PropertyGetter(typeof(ModelDb),"AllCharacters");
            var probe=new Harmony("simulatorlab.native-discovery.negative");
            bool rejected=false;
            try { probe.Patch(target,postfix:new HarmonyMethod(typeof(NativeModRunLoop),nameof(DiscoveryProbe))); try { AssertDiscoveryStable(); } catch(InvalidOperationException ex) when(ex.Message=="Native discovery patch profile changed after freeze.") { rejected=true; } }
            finally { probe.Unpatch(target,HarmonyPatchType.All,probe.Id); }
            AssertDiscoveryStable();
            if(!rejected) throw new InvalidOperationException("Late discovery patch was not rejected.");
            DiscoveryNegativePassed=true;
        }
        var character = ModelDb.AllCharacters.Single(c => c.GetType().FullName == "MyMod.MyModCode.Character.MyMod");
        var guide = character.StartingDeck.Single(c => c.GetType().Name == "GuidingPower");
        var rows = ModelDb.AllCardPools.SelectMany(p => p.AllCards.Select(c => new { card = c.Id.ToString(), pool = p.GetType().FullName, resolved = c.Pool.GetType().FullName })).ToArray();
        if (guide.Pool.GetType().Name != "StarterCardPool" || guide.VisualCardPool.GetType().Name != "MyModCardPool")
            throw new InvalidOperationException("Guide pool separation failed.");
        if (character.CardPool.AllCards.Any(c => c.GetType().Name == "GuidingPower")) throw new InvalidOperationException("Guide leaked into reward pool.");
        foreach (var card in character.StartingDeck) _ = card.Pool;
        return new { status = "passed", profile = "native-test-loader-original-content-discovery/v1", events = Events, discoveryStamp=DiscoveryStamp, lateActualHarmonyPatchRejected=DiscoveryNegativePassed,
            characters = ModelDb.AllCharacters.Select(c => c.Id.ToString()).ToArray(), rows,
            starter = guide.Pool.GetType().FullName, rewardPool = character.CardPool.GetType().FullName,
            libraryScope = "BaseLib original discovery postfixes; Ritsu assembly ownership and actual role initializer; no full library startup",
            patches = Harmony.GetAllPatchedMethods().Where(m => Harmony.GetPatchInfo(m)?.Owners.Contains("BaseLib") == true).Select(m => m.DeclaringType!.FullName + "." + m.Name).ToArray() };
    }

    private static void Patch(Type type, string method, string prefix) => EnvironmentPatches.Patch(AccessTools.Method(type, method)
        ?? throw new MissingMethodException(type.FullName, method), prefix: new HarmonyMethod(typeof(NativeModRunLoop), prefix));
    private static bool ExecutablePath(ref string __result) { __result = Path.Combine(ProfileRoot, "offline-harness.exe"); return false; }
    private static bool NotDebug(ref bool __result) { __result = false; return false; }
    private static bool CommandLine(ref string[] __result) { __result = []; return false; }
    private static bool RejectCopy() => throw new InvalidOperationException("Native ModManager attempted forbidden save copy.");
    private static void DiscoveryProbe() { }
    private static bool HostHasArgument(string argument) => Environment.GetCommandLineArgs().Any(value => value == argument || value == "--" + argument);
    private static IEnumerable<CodeInstruction> CommandLineQueryBridge(IEnumerable<CodeInstruction> source)
    {
        MethodInfo original = AccessTools.Method(typeof(CommandLineHelper), "HasArg", [typeof(string)]);
        MethodInfo replacement = AccessTools.Method(typeof(NativeModRunLoop), nameof(HostHasArgument));
        int count = 0;
        var instructions = source.ToArray();
        foreach (var instruction in instructions)
            if (instruction.Calls(original)) { instruction.opcode = OpCodes.Call; instruction.operand = replacement; count++; }
        if (count != 1) throw new InvalidOperationException($"Expected exactly one native ModManager command-line query; found {count}.");
        Events.Add("environment-query:ModManager.Initialize/HasArg->actual-process-arguments");
        return instructions;
    }

    private sealed class PinnedFileIo(string root) : IModManagerFileIo
    {
        private string Check(string path)
        {
            string absolute = Path.GetFullPath(path);
            if (!absolute.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Mod IO outside profile: " + absolute);
            return absolute;
        }
        public string[] GetFilesAt(string path) => Directory.GetFiles(Check(path)).Where(p => p.EndsWith(".json", StringComparison.Ordinal)).Select(p => Path.GetFileName(p)!).ToArray();
        public string[] GetDirectoriesAt(string path) => Directory.GetDirectories(Check(path)).Select(p => Path.GetFileName(p)!).ToArray();
        public bool FileExists(string path) => File.Exists(Check(path));
        public bool DirectoryExists(string path) => Directory.Exists(Check(path));
        public Stream OpenStream(string path, Godot.FileAccess.ModeFlags mode) => mode == Godot.FileAccess.ModeFlags.Read
            ? File.OpenRead(Check(path)) : throw new InvalidOperationException("Mod profile write denied.");
        public void MakeDirRecursive(string path) => throw new InvalidOperationException("Mod profile mkdir denied.");
        public Error CopyFile(string sourcePath, string destinationPath) => throw new InvalidOperationException("Mod profile copy denied.");
    }
}
