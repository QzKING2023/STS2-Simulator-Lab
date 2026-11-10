using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Platform.Steam;
using MegaCrit.Sts2.Core.TestSupport;
using SimulatorLab.Core;
using Environment = System.Environment;

namespace OfflineSearchHarness;

// Configuration-driven shared foundation fixture.  It deliberately knows only
// the manifest/assembly seams common to independent mod families; package
// identity and exact paths live in the run input JSON.
internal static class NativeFoundationModLoop
{
    internal static bool Enabled => Environment.GetEnvironmentVariable("SIMULATORLAB_FOUNDATION_FIXTURE") == "1";
    internal static bool IsScenario(string scenario) => scenario is "foundation-fixture" or "foundation-fixture-replay" or "foundation-failure" or "foundation-reload";
    internal static string ConfigPath => RequiredFile("SIMULATORLAB_FOUNDATION_CONFIG");
    internal static FoundationConfig Config => _config ??= ReadConfig();
    internal static string TargetAssembly => Config.TargetAssembly;
    internal static CharacterModel SelectedCharacter => _selected ??= FindCharacter();
    internal static readonly Harmony Patches = new("simulatorlab.native.foundation.fixture");
    internal static readonly Dictionary<string, Assembly> Loaded = new(StringComparer.Ordinal);
    internal static readonly List<string> LoadOrder = [];
    internal static readonly List<object> Events = [];
    internal static readonly List<object> CallbackEvents = [];
    internal static readonly List<object> LoaderRows = [];
    internal static readonly List<object> PatchRowsBefore = [];
    internal static readonly List<object> PatchRowsAfter = [];
    internal static readonly List<string> MissingSeams = [];
    internal static readonly List<string> StaticClearFailures = [];
    internal static bool Initialized;
    internal static bool DuplicateObserved;
    internal static bool DuplicateRolledBack;
    internal static bool ReloadObserved;
    internal static bool ReloadSucceeded;
    internal static bool RegistrationFailureObserved;
    internal static bool ObserverDetached;
    internal static bool RegistryCleared;
    internal static bool CallbackFailureObserved;
    internal static string? InitializationError;
    private static FoundationAssemblyLoadContext? _fixtureAlc;
    private static WeakReference? _fixtureAlcWeak;
    private static WeakReference? _fixtureAssemblyWeak;
    private static bool _runtimeAlcTeardown;
    private static FoundationConfig? _config;
    private static CharacterModel? _selected;

    internal static Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
    {
        if (!Enabled) return null;
        var simple = name.Name ?? "";
        if (!Config.AssemblyPaths.TryGetValue(simple, out var relative)) return null;
        var path = Path.IsPathRooted(relative) ? relative : Path.Combine(Config.SnapshotRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) return null;
        var owner = context;
        var assembly = owner.Assemblies.FirstOrDefault(a => a.GetName().Name == simple)
            ?? owner.LoadFromAssemblyPath(Path.GetFullPath(path));
        Loaded[simple] = assembly;
        if (!LoadOrder.Contains(simple, StringComparer.Ordinal)) LoadOrder.Add(simple);
        return assembly;
    }

    internal static void Initialize()
    {
        if (!Enabled) return;
        if (!TestMode.IsOn || SteamInitializer.Initialized)
            throw new InvalidOperationException("foundation fixture requires offline TestMode");
        if (!Directory.Exists(Config.SnapshotRoot)) throw new DirectoryNotFoundException(Config.SnapshotRoot);
        _fixtureAlc = new FoundationAssemblyLoadContext(Config);
        _fixtureAlcWeak = new WeakReference(_fixtureAlc, trackResurrection: false);
        Patch(typeof(OS), nameof(OS.GetExecutablePath), nameof(ExecutablePath));
        Patch(typeof(OS), nameof(OS.IsDebugBuild), nameof(NotDebug));
        Patch(typeof(OS), nameof(OS.GetCmdlineArgs), nameof(CommandLine));
        Patch(typeof(OS), nameof(OS.GetName), nameof(OsName));
        Patch(typeof(ModManager), nameof(ModManager.CopyUnmoddedSaveFilesIfNeeded), nameof(RejectCopy));
        var scriptLookup = AccessTools.Method(AccessTools.TypeByName("Godot.Bridge.ScriptManagerBridge"), "LookupScriptsInAssembly");
        if (scriptLookup is not null)
            Patches.Patch(scriptLookup, prefix: new HarmonyMethod(typeof(NativeFoundationModLoop), nameof(SkipGodotScriptLookup)));
        var init = AccessTools.Method(typeof(ModManager), nameof(ModManager.Initialize))
            ?? throw new MissingMethodException(typeof(ModManager).FullName, nameof(ModManager.Initialize));
        var machine = init.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new InvalidOperationException("ModManager.Initialize has no async state machine");
        Patches.Patch(AccessTools.Method(machine, "MoveNext")!, transpiler: new HarmonyMethod(typeof(NativeFoundationModLoop), nameof(CommandLineQueryBridge)));

        ModManager.ResetForTests();
        var settings = new ModSettings { PlayerAgreedToModLoading = true };
        foreach (var mod in Config.Mods)
        {
            settings.ModList.Add(new SettingsSaveMod { Id = mod.Id, IsEnabled = true });
            ModManager.TestInitializers[mod.Id] = () => InitializeAssembly(mod);
        }
        var task = ModManager.Initialize(new SnapshotFileIo(Config), settings,
            SemanticVersion.FromString(Config.TargetGameVersion));
        ((MainLoopContext)SynchronizationContext.Current!).RunUntilCompleted(task, TimeSpan.FromSeconds(45), "foundation ModManager.Initialize");
        CaptureLoaderState("initial");
        if (Config.FailurePoint == "initializer" && ModManager.Mods.Any(m => m.state == ModLoadState.Failed))
            RegistrationFailureObserved = true;
        if (ModManager.Mods.Any(m => m.state != ModLoadState.Loaded || m.errors?.Count > 0))
            throw new InvalidOperationException("foundation loader rejected a configured mod: " + string.Join(" || ", ModManager.Mods.Select(DescribeMod)));
        Initialized = true;

        ObserveDuplicateRegistration();
        if (Config.Mode == "reload") ObserveReload(init);
        InstallCallbackObservers();
        CapturePatchOwnership(PatchRowsAfter);
        Events.Add(new { kind = "foundation-loader-complete", mods = ModManager.Mods.Count, loadOrder = LoadOrder.ToArray() });
    }

    internal static Type[] IncludeModels(Type[] baseModels)
    {
        var abstractModel = typeof(AbstractModel);
        var result = baseModels.ToList();
        foreach (var assembly in Loaded.Values.Distinct())
        {
            foreach (var type in SafeGetTypes(assembly))
            {
                if (type.IsClass && !type.IsAbstract && abstractModel.IsAssignableFrom(type)) result.Add(type);
            }
        }
        return result.Distinct().ToArray();
    }

    internal static void WriteProbe(string output, string seed, bool replay,
        NativeRunTerminal terminal, NativeRunStop stop, string? error)
    {
        if (!Enabled) return;
        try
        {
            CapturePatchOwnership(PatchRowsBefore);
            var counts = ModelDb.All.Where(m => m.GetType().Assembly.GetName().Name == TargetAssembly)
                .GroupBy(KindOf, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            var registration = Config.RegistrationIdentity ?? $"REG.{TargetAssembly}.lib/{Config.TargetGameVersion}#1@{Config.TargetGameVersion}";
            var callbackKinds = CallbackEvents.Select(x => JsonSerializer.Serialize(x, Program.Json)).ToArray();
            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                seed, replay, terminal, stop, registration, LoadOrder, Events, CallbackEvents, counts,
                duplicate = DuplicateObserved, rollback = DuplicateRolledBack, reload = ReloadObserved && ReloadSucceeded,
                registryCleared = RegistryCleared, observerDetached = ObserverDetached
            }, Program.Json)))).ToLowerInvariant();
            var probe = new
            {
                schema = "native-foundation-fixture/v1",
                gate = "BLOCKED",
                caseId = Config.CaseId,
                snapshot = new { Config.SnapshotId, Config.SnapshotContentSha256, Config.ManifestSha256 },
                game = new { Config.TargetGameVersion, Config.GameCommit },
                worker = "SimulatorLab.NativeFoundationWorker",
                adapter = "native-foundation/v1",
                collector = "isolated NativeRun/GameProbe; foundation-trace/v1",
                seed, replay, Config.TargetAssembly, Config.TargetModId, Config.DependencyClosure,
                registrationIdentity = registration,
                contentUnits = counts,
                lifecycle = new
                {
                    registration = new { loaded = Initialized, duplicateAttempted = DuplicateObserved, duplicateRolledBack = DuplicateRolledBack, reloadAttempted = ReloadObserved, reloadSucceeded = ReloadSucceeded, failureRollbackObserved = RegistrationFailureObserved },
                    callbacks = new { callbackKinds, callbackExceptionObserved = CallbackFailureObserved, ownership = PatchRowsAfter },
                cleanup = new { observerDetached = ObserverDetached, registryCleared = RegistryCleared, runtimeAlcTeardown = _runtimeAlcTeardown, newProcessRepeatability = "separate-case" }
                },
                interactionRisk = new
                {
                    crossRoomSecondaryResource = "observed-through-real-run-boundary; registry-specific semantics require family evidence",
                    powerRelicContinuity = "content inventory and native state boundary observed",
                    registrationOwnership = "Harmony ownership and ModManager assembly list captured",
                    missingSeams = MissingSeams.ToArray()
                },
                terminal = terminal.ToString(), stop = stop.ToString(), error,
                digest,
                evidence = new[] { "foundation-fixture.json", "foundation-lifecycle.json", "foundation-cleanup.json" }
            };
            File.WriteAllText(Path.Combine(output, replay ? "foundation-fixture-replay.json" : "foundation-fixture.json"), JsonSerializer.Serialize(probe, Program.Json));
            File.WriteAllText(Path.Combine(output, "foundation-lifecycle.json"), JsonSerializer.Serialize(new { schema = "foundation-lifecycle/v1", registration, LoadOrder, LoaderRows, CallbackEvents, PatchRowsBefore, PatchRowsAfter, digest }, Program.Json));
            File.WriteAllText(Path.Combine(output, "foundation-cleanup.json"), JsonSerializer.Serialize(new { schema = "foundation-cleanup/v1", observerDetached = ObserverDetached, registryCleared = RegistryCleared, runtimeAlcTeardown = _runtimeAlcTeardown, residualModPatchOwners = PatchRowsBefore, error }, Program.Json));
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(output, "foundation-fixture-error.json"), JsonSerializer.Serialize(new { schema = "foundation-fixture-error/v1", type = ex.GetType().FullName, message = ex.Message, exception = ex.ToString() }, Program.Json));
        }
        finally
        {
            var observedRoots = Array.Empty<string>();
            try { observedRoots = FindModStaticAlcRoots(); } catch { }
            var alcRoots = Array.Empty<string>();
            object? loadedContexts = null;
            try
            {
                Patches.UnpatchAll();
                foreach (var owner in FixtureHarmonyOwners())
                {
                    try { new Harmony(owner).UnpatchAll(); } catch { }
                }
                ObserverDetached = true;
            }
            catch { ObserverDetached = false; }
            try
            {
                if (TestMode.IsOn)
                {
                    DetachModRuntimeEvents();
                    Program.ReleaseForTeardown();
                    ResetCombatStateForTeardown();
                    ResetRunManagerForTeardown();
                    DetachGameModManagerHandlers();
                    ModManager.ResetForTests();
                    RegistryCleared = ModManager.Mods.Count == 0;
                    AssemblyInfo.ClearForTests();
                    var modTypes = AccessTools.Field(typeof(ReflectionHelper), "_modTypes");
                    modTypes?.SetValue(null, null);
                    AccessTools.Field(typeof(ReflectionHelper), "_allTypes")?.SetValue(null, null);
                    AccessTools.Property(typeof(AssemblyInfo), nameof(AssemblyInfo.MockTypes))?.SetValue(null, null);
                    ModelIdSerializationCache.ResetForTest();
                    ResetModelDbForTeardown();
                    ClearGameManagedStaticCollections();
                    ClearGameManagedStaticState();
                    ClearManagedStaticCollections(typeof(AbstractModelSubtypes));
                    ResetHarmonyStateForTeardown();
                    ClearHarmonyStaticCollections();
                    ClearManagedStaticCollections(typeof(MegaCrit.Sts2.Core.Multiplayer.Serialization.MaxEnumValueCache));
                    ClearManagedStaticCollections(typeof(MegaCrit.Sts2.Core.Saves.Runs.SerializationConditionExtensions));
                    AccessTools.Field(typeof(ModManager), "OnModDetected")?.SetValue(null, null);
                    AccessTools.Field(typeof(ModManager), "OnMetricsUpload")?.SetValue(null, null);
                    ResetModStaticStateForTeardown(observedRoots);
                    ClearManagedStaticCollections(typeof(ModHelper));
                }
            }
            catch { RegistryCleared = false; }
            Thread.Sleep(1000);
            try
            {
                // Do not rescan static fields after cleanup. Reflection reads can
                // trigger a mod type initializer and recreate the very singleton
                // graph being torn down. Pre-cleanup roots and the teardown scan
                // above provide the non-invasive evidence instead.
                // Keep the collectible ALC alive until this probe writer has
                // returned.  Its finally frame contains many diagnostic locals;
                // unloading here can leave those locals visible to the GC and
                // make a live ALC look like a teardown failure.  The actual
                // unload is performed by FinalizeTeardownAfterProbe.
                loadedContexts = Loaded.Values.Distinct().Select(a => new
                {
                    name = a.GetName().Name,
                    context = AssemblyLoadContext.GetLoadContext(a)?.Name,
                    location = a.Location
                }).ToArray();
                _runtimeAlcTeardown = false;
            }
            catch { _runtimeAlcTeardown = false; }
            // Emit cleanup again so the final flags reflect the actual teardown attempt.
                try
            {
                var residual = new List<object>();
                CapturePatchOwnership(residual);
                var loadedTargetContexts = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => string.Equals(a.GetName().Name, TargetAssembly, StringComparison.Ordinal))
                    .Select(a => new { name = a.GetName().Name, context = AssemblyLoadContext.GetLoadContext(a)?.Name, location = a.Location })
                    .ToArray();
                File.WriteAllText(Path.Combine(output, "foundation-cleanup.json"), JsonSerializer.Serialize(new
                {
                    schema = "foundation-cleanup/v1",
                    observerDetached = ObserverDetached,
                    registryCleared = RegistryCleared,
                    pendingTaskStateCleared = NativeLifecycle.PendingTaskStateCleared,
                    hostReferencesCleared = NativeHost.TeardownReferencesCleared,
                    runtimeAlcTeardown = _runtimeAlcTeardown,
                    alcAlive = _fixtureAlcWeak?.IsAlive,
                    assemblyAlive = _fixtureAssemblyWeak?.IsAlive,
                    staticClearFailures = StaticClearFailures.ToArray(),
                    alcRoots,
                    loadedContexts,
                    loadedTargetContexts,
                    residualModPatchOwners = residual,
                    error
                }, Program.Json));
            }
            catch { }
        }
    }

    // The probe writer itself still has a live stack frame while its finally
    // block performs the first unload attempt.  Give the runtime one more
    // collection point after that frame has returned so collectible ALC
    // evidence reflects the post-probe process state rather than a transient
    // JIT root.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void FinalizeTeardownAfterProbe(string output)
    {
        CollectibleTeardown? teardown = null;
        try
        {
            teardown = TeardownCollectibleAlc();
        }
        catch
        {
            _runtimeAlcTeardown = false;
        }
        for (var i = 0; i < 32; i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
        Thread.Sleep(100);
        try
        {
            var cleanupPath = Path.Combine(output, "foundation-cleanup.json");
            if (!File.Exists(cleanupPath)) return;
            using var document = JsonDocument.Parse(File.ReadAllText(cleanupPath));
            var root = document.RootElement;
            var payload = new Dictionary<string, object?>();
            foreach (var property in root.EnumerateObject()) payload[property.Name] = property.Value.Clone();
            // The collectible ALC is the unload boundary. A collectible
            // Assembly object may remain visible through AppDomain metadata
            // after its ALC has unloaded, so do not treat that diagnostic
            // weak reference as a teardown failure.
            var runtime = _fixtureAlcWeak is not null && !_fixtureAlcWeak.IsAlive;
            if (teardown is not null)
            {
                payload["alcRoots"] = teardown.Roots;
                payload["loadedContexts"] = teardown.LoadedContexts;
                runtime = teardown.Runtime && runtime;
            }
            payload["runtimeAlcTeardown"] = runtime;
            payload["fixtureAlcNull"] = _fixtureAlc is null;
            payload["loadedCount"] = Loaded.Count;
            payload["alcAlive"] = _fixtureAlcWeak?.IsAlive;
            payload["assemblyAlive"] = _fixtureAssemblyWeak?.IsAlive;
            File.WriteAllText(cleanupPath, JsonSerializer.Serialize(payload, Program.Json));
            _runtimeAlcTeardown = runtime;
            UpdateFoundationFixtureGate(output, payload);
        }
        catch { }
    }

    private static void UpdateFoundationFixtureGate(string output, IReadOnlyDictionary<string, object?> cleanup)
    {
        static bool ReadBool(IReadOnlyDictionary<string, object?> values, string name) =>
            values.TryGetValue(name, out var value) && value switch
            {
                bool b => b,
                JsonElement element when element.ValueKind == JsonValueKind.True => true,
                _ => false
            };
        var runtime = ReadBool(cleanup, "runtimeAlcTeardown");
        var observer = ReadBool(cleanup, "observerDetached");
        var registry = ReadBool(cleanup, "registryCleared");
        var pending = ReadBool(cleanup, "pendingTaskStateCleared");
        var host = ReadBool(cleanup, "hostReferencesCleared");
        var residual = cleanup.TryGetValue("residualModPatchOwners", out var residualValue)
            && residualValue is JsonElement residualElement
            && residualElement.ValueKind == JsonValueKind.Array
            && residualElement.GetArrayLength() == 0;
        var gate = runtime && observer && registry && pending && host && residual;
        foreach (var name in new[] { "foundation-fixture.json", "foundation-fixture-replay.json" })
        {
            var path = Path.Combine(output, name);
            if (!File.Exists(path)) continue;
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var payload = new Dictionary<string, object?>();
                foreach (var property in document.RootElement.EnumerateObject()) payload[property.Name] = property.Value.Clone();
                payload["gate"] = gate ? "PASS" : "BLOCKED";
                File.WriteAllText(path, JsonSerializer.Serialize(payload, Program.Json));
            }
            catch { }
        }
    }

    private sealed record CollectibleTeardown(string[] Roots, object? LoadedContexts, bool Runtime);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static CollectibleTeardown TeardownCollectibleAlc()
    {
        var alc = _fixtureAlc;
        _fixtureAlc = null;
        var loadedAssemblies = alc?.Assemblies.ToArray() ?? [];
        var loadedContexts = Loaded.Values.Distinct().Select(a => new
        {
            name = a.GetName().Name,
            context = AssemblyLoadContext.GetLoadContext(a)?.Name,
            location = a.Location
        }).ToArray();
        Loaded.Clear();
        _selected = null;
        DetachGodotScriptManagerAlc(alc);
        var roots = alc is null ? Array.Empty<string>() : FindKnownAlcRoots(alc);
        ClearGodotScriptManagerTypeMaps(loadedAssemblies);
        alc?.Unload();
        alc = null;
        loadedAssemblies = Array.Empty<Assembly>();
        // Do not force a collection or inspect the weak references here. The
        // unload helper still has the ALC/assembly locals live in this frame;
        // the caller performs collection only after this frame has returned.
        return new CollectibleTeardown(roots, loadedContexts, true);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DetachGodotScriptManagerAlc(AssemblyLoadContext? target)
    {
        if (target is null) return;
        try
        {
            var bridge = typeof(Godot.GodotObject).Assembly.GetType("Godot.Bridge.ScriptManagerBridge", throwOnError: false);
            var unload = bridge?.GetMethod("OnAlcUnloading", BindingFlags.Static | BindingFlags.NonPublic);
            unload?.Invoke(null, [target]);
            if (bridge is null) return;
            if (bridge.GetField("_alcData", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) is System.Collections.IDictionary alcData)
                alcData.Remove(target);
            var pathMap = bridge.GetField("_pathTypeBiMap", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            var removePath = pathMap?.GetType().GetMethod("RemoveByScriptType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null, types: [typeof(Type)], modifiers: null);
            var scriptMap = bridge.GetField("_scriptTypeBiMap", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            var removeScript = scriptMap?.GetType().GetMethod("RemoveByScriptType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null, types: [typeof(Type), typeof(nint).MakeByRefType()], modifiers: null);
            if (removePath is null && removeScript is null) return;
            foreach (var assembly in target.Assemblies.ToArray())
            {
                foreach (var type in SafeGetTypes(assembly))
                {
                    removePath?.Invoke(pathMap, [type]);
                    if (removeScript is not null)
                    {
                        var args = new object?[] { type, (nint)0 };
                        removeScript.Invoke(scriptMap, args);
                    }
                }
            }
        }
        catch { }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ClearGodotScriptManagerTypeMaps(Assembly[] assemblies)
    {
        if (assemblies.Length == 0) return;
        try
        {
            var assemblySet = assemblies.ToHashSet();
            var bridge = typeof(Godot.GodotObject).Assembly.GetType("Godot.Bridge.ScriptManagerBridge", throwOnError: false);
            if (bridge is null) return;
            foreach (var mapFieldName in new[] { "_pathTypeBiMap", "_scriptTypeBiMap" })
            {
                var map = bridge.GetField(mapFieldName, BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
                if (map is null) continue;
                foreach (var field in map.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (field.GetValue(map) is not System.Collections.IDictionary dictionary) continue;
                    var remove = new List<object>();
                    foreach (System.Collections.DictionaryEntry entry in dictionary)
                    {
                        if (entry.Key is Type keyType && assemblySet.Contains(keyType.Assembly)
                            || entry.Value is Type valueType && assemblySet.Contains(valueType.Assembly))
                            remove.Add(entry.Key);
                    }
                    foreach (var key in remove) dictionary.Remove(key);
                }
            }
        }
        catch { }
    }

    private static void InitializeAssembly(FoundationMod mod)
    {
        var loaded = new List<Assembly>();
        foreach (var relative in mod.AssemblyPaths)
        {
            var path = Path.IsPathRooted(relative) ? relative : Path.Combine(Config.SnapshotRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            var simple = Path.GetFileNameWithoutExtension(path);
            // Godot-generated dependency assemblies register their managed
            // types with process-wide native tables. Keep that shared closure
            // in Default and make only the target mod collectible.
            var owner = string.Equals(mod.Id, Config.TargetModId, StringComparison.Ordinal)
                ? (_fixtureAlc ?? throw new InvalidOperationException("foundation collectible ALC is unavailable"))
                : AssemblyLoadContext.Default;
            var assembly = Loaded.TryGetValue(simple, out var existing)
                ? existing
                : owner.LoadFromAssemblyPath(Path.GetFullPath(path));
            Loaded[simple] = assembly;
            if (AssemblyLoadContext.GetLoadContext(assembly) == _fixtureAlc)
                _fixtureAssemblyWeak ??= new WeakReference(assembly, trackResurrection: false);
            if (!LoadOrder.Contains(simple, StringComparer.Ordinal)) LoadOrder.Add(simple);
            ModManager.AssociateAssemblyWithMod(mod.Id, assembly);
            loaded.Add(assembly);
        }
        if (!mod.Initialize)
        {
            Events.Add(new { kind = "initializer-skipped", mod = mod.Id, reason = "configured-load-only-dependency" });
            return;
        }
        if (Config.FailurePoint == "initializer" && mod.Id == Config.FailureModId)
        {
            Events.Add(new { kind = "initializer-failure-injected", mod = mod.Id });
            throw new InvalidOperationException("controlled foundation initializer failure");
        }
        foreach (var assembly in loaded)
        {
            var initializers = SafeGetTypes(assembly).Where(t => t.GetCustomAttribute<ModInitializerAttribute>() is not null).ToArray();
            if (initializers.Length == 0)
            {
                new Harmony("simulatorlab.foundation.mod." + mod.Id).PatchAll(assembly);
                Events.Add(new { kind = "patchall", mod = mod.Id, assembly = assembly.GetName().Name });
                continue;
            }
            foreach (var type in initializers)
            {
                var attr = type.GetCustomAttribute<ModInitializerAttribute>()!;
                var method = type.GetMethod(attr.initializerMethod, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new MissingMethodException(type.FullName, attr.initializerMethod);
                method.Invoke(null, null);
                Events.Add(new { kind = "initializer", mod = mod.Id, assembly = assembly.GetName().Name, type = type.FullName, method = method.Name });
            }
        }
    }

    // Static storage on a closed generic base is not exposed by the open
    // generic type returned from Assembly.GetTypes().  A mod can therefore
    // retain a factory or delegate in e.g. NodeFactory<T> even after every
    // ordinary target type has been scrubbed.  Walk the concrete target types
    // and clear those closed base slots explicitly.
    private static void ClearClosedGenericBaseStatics(AssemblyLoadContext target)
    {
        foreach (var assembly in target.Assemblies.Concat(Loaded.Values).Distinct().ToArray())
        {
            foreach (var concrete in SafeGetTypes(assembly))
            {
                for (var type = concrete.BaseType; type is not null; type = type.BaseType)
                {
                    if (!type.IsGenericType || type.ContainsGenericParameters) continue;
                    foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (field.IsLiteral || field.FieldType.IsValueType || field.FieldType == typeof(string)) continue;
                        try
                        {
                            var value = field.GetValue(null);
                        var ownerExternal = AssemblyLoadContext.GetLoadContext(field.DeclaringType?.Assembly!) != target;
                            var runtimeCollectible = value is not null && IsCollectibleFieldType(value.GetType(), target);
                            var fieldCollectible = IsCollectibleFieldType(field.FieldType, target);
                            if (value is Array array) { Array.Clear(array, 0, array.Length); if (fieldCollectible || runtimeCollectible) ClearStaticReference(field); continue; }
                            if (value is System.Collections.IDictionary dictionary) { dictionary.Clear(); if (fieldCollectible || runtimeCollectible) ClearStaticReference(field); continue; }
                            var clear = value?.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, types: Type.EmptyTypes, modifiers: null);
                            if (clear is not null) { clear.Invoke(value, null); if (fieldCollectible || runtimeCollectible) ClearStaticReference(field); continue; }
                            ClearStaticReference(field);
                        }
                        catch { }
                    }
                }
            }
        }
    }

    private static void ObserveDuplicateRegistration()
    {
        var target = ModManager.Mods.FirstOrDefault(m => m.manifest?.id == Config.TargetModId);
        var assembly = target?.assemblies.FirstOrDefault(a => a.GetName().Name == TargetAssembly);
        if (target is null || assembly is null) { MissingSeams.Add("duplicate-registration-target"); return; }
        var before = target.assemblies.Count;
        ModManager.AssociateAssemblyWithMod(Config.TargetModId, assembly);
        DuplicateObserved = target.assemblies.Count == before + 1;
        if (DuplicateObserved) { target.assemblies.RemoveAt(target.assemblies.Count - 1); DuplicateRolledBack = target.assemblies.Count == before; }
        Events.Add(new { kind = "duplicate-registration", before, after = target.assemblies.Count, DuplicateObserved, DuplicateRolledBack });
    }

    private static void ObserveReload(MethodInfo init)
    {
        ReloadObserved = true;
        try
        {
            ModManager.ResetForTests();
            var settings = new ModSettings { PlayerAgreedToModLoading = true };
            foreach (var mod in Config.Mods)
            {
                settings.ModList.Add(new SettingsSaveMod { Id = mod.Id, IsEnabled = true });
                ModManager.TestInitializers[mod.Id] = () => InitializeAssembly(mod);
            }
            var task = ModManager.Initialize(new SnapshotFileIo(Config), settings, SemanticVersion.FromString(Config.TargetGameVersion));
            ((MainLoopContext)SynchronizationContext.Current!).RunUntilCompleted(task, TimeSpan.FromSeconds(45), "foundation reload");
            ReloadSucceeded = ModManager.Mods.All(m => m.state == ModLoadState.Loaded && (m.errors?.Count ?? 0) == 0);
            CaptureLoaderState("reload");
        }
        catch (Exception ex) { InitializationError = "reload: " + ex; Events.Add(new { kind = "reload-failure", error = ex.ToString() }); }
    }

    private static void InstallCallbackObservers()
    {
        foreach (var seam in new[]
        {
            ("room-enter", "MegaCrit.Sts2.Core.Runs.RunManager", "EnterMapCoord"),
            ("turn", "MegaCrit.Sts2.Core.Combat.CombatManager", "OnEndedTurnLocally"),
            ("reward", "MegaCrit.Sts2.Core.Commands.RewardsCmd", "OfferForRoomEnd"),
            ("before-death", "MegaCrit.Sts2.Core.Combat.CombatManager", "ProcessPendingLoss"),
            ("combat-end", "MegaCrit.Sts2.Core.Combat.CombatManager", "EndCombatInternal")
        })
        {
            var type = AccessTools.TypeByName(seam.Item2);
            var method = type is null ? null : AccessTools.Method(type, seam.Item3);
            if (method is null) { MissingSeams.Add(seam.Item1); continue; }
            Patches.Patch(method,
                prefix: new HarmonyMethod(typeof(NativeFoundationModLoop), nameof(CallbackPrefix)),
                postfix: new HarmonyMethod(typeof(NativeFoundationModLoop), nameof(CallbackPostfix)));
            Events.Add(new { kind = "callback-attached", seam = seam.Item1, method = method.DeclaringType?.FullName + "." + method.Name });
        }
        CapturePatchOwnership(PatchRowsBefore);
    }

    private static void CallbackPrefix(MethodBase __originalMethod)
    {
        var seam = Seam(__originalMethod);
        CallbackEvents.Add(new { phase = "before", seam, method = __originalMethod.Name });
        Program.Observe("foundation-callback-before", new { seam, method = __originalMethod.Name });
        if (Config.FailurePoint == "callback" && seam == "reward")
        {
            CallbackFailureObserved = true;
            throw new NativeRuntimeFailureException("controlled foundation callback interruption");
        }
    }

    private static void CallbackPostfix(MethodBase __originalMethod)
    {
        var seam = Seam(__originalMethod);
        CallbackEvents.Add(new { phase = "after", seam, method = __originalMethod.Name });
        Program.Observe("foundation-callback-after", new { seam, method = __originalMethod.Name });
    }

    private static string Seam(MethodBase method) => method.DeclaringType?.Name switch
    {
        "RunManager" => "room-enter",
        "CombatManager" when method.Name == "OnEndedTurnLocally" => "turn",
        "CombatManager" when method.Name == "ProcessPendingLoss" => "before-death",
        "CombatManager" => "combat-end",
        "RewardsCmd" => "reward",
        _ => method.Name
    };

    private static CharacterModel FindCharacter()
    {
        var matches = ModelDb.All.OfType<CharacterModel>().Where(c => c.GetType().Assembly.GetName().Name == TargetAssembly).ToArray();
        if (matches.Length != 1) throw new NotSupportedException($"configured content family has {matches.Length} character models");
        return matches[0];
    }

    private static void CaptureLoaderState(string phase)
    {
        foreach (var mod in ModManager.Mods)
            LoaderRows.Add(new { phase, id = mod.manifest?.id, state = mod.state.ToString(), assemblies = mod.assemblies.Select(a => a.GetName().Name).ToArray(), errors = mod.errors?.Select(e => e.ToString()).ToArray() ?? [] });
    }

    private static void CapturePatchOwnership(List<object> destination)
    {
        destination.Clear();
        foreach (var method in Harmony.GetAllPatchedMethods())
        {
            var info = Harmony.GetPatchInfo(method);
            if (info is null) continue;
            var owners = info.Owners.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (owners.Any(x => x.Contains("foundation", StringComparison.OrdinalIgnoreCase)) || owners.Any(x => x.Contains(Config.TargetAssembly, StringComparison.OrdinalIgnoreCase)))
                destination.Add(new { method = method.DeclaringType?.FullName + "." + method.Name, owners });
        }
    }

    private static IEnumerable<string> FixtureHarmonyOwners()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Config.TargetModId,
            Config.TargetAssembly,
            Patches.Id
        };
        foreach (var mod in Config.Mods) names.Add(mod.Id);
        foreach (var key in Loaded.Keys) names.Add(key);
        return Harmony.GetAllPatchedMethods()
            .SelectMany(method => Harmony.GetPatchInfo(method)?.Owners ?? Enumerable.Empty<string>())
            .Where(owner => names.Contains(owner) || owner.Contains("foundation", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] FindKnownAlcRoots(AssemblyLoadContext target)
    {
        var roots = new List<string>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var rootsToScan = new List<Type> { typeof(AssemblyInfo), typeof(ReflectionHelper), typeof(ModelDb), typeof(ModelIdSerializationCache), typeof(AbstractModelSubtypes), typeof(ModManager), typeof(Harmony) };
        rootsToScan.AddRange(SafeGetTypes(typeof(Harmony).Assembly));
        rootsToScan.Add(typeof(ModHelper));
        rootsToScan.Add(typeof(MegaCrit.Sts2.Core.Multiplayer.Serialization.MaxEnumValueCache));
        rootsToScan.Add(typeof(MegaCrit.Sts2.Core.Saves.Runs.SerializationConditionExtensions));
        rootsToScan.Add(typeof(Program).Assembly.GetType(typeof(Program).FullName!)!);
        rootsToScan.AddRange(SafeGetTypes(typeof(Program).Assembly));
        var scriptManagerBridge = typeof(Godot.GodotObject).Assembly.GetType("Godot.Bridge.ScriptManagerBridge", throwOnError: false);
        if (scriptManagerBridge is not null) rootsToScan.Add(scriptManagerBridge);
        rootsToScan.Add(typeof(Godot.CustomGCHandle));
        var sharedHarmonyState = Type.GetType("HarmonySharedState", throwOnError: false);
        if (sharedHarmonyState is not null) rootsToScan.Add(sharedHarmonyState);
        foreach (var root in rootsToScan)
        {
            foreach (var field in root.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                try
                {
                    ScanKnownAlcTypeShape(field.FieldType, root.FullName + "." + field.Name, target, roots);
                    var value = field.GetValue(null);
                    ScanKnownAlcValue(value, root.FullName + "." + field.Name, target, seen, roots, 0);
                    if (value is not null && value is not string && !value.GetType().IsValueType)
                        ScanKnownAlcObjectFields(value, root.FullName + "." + field.Name, target, seen, roots, 0);
                }
                catch { }
                if (roots.Count >= 1000) return roots.ToArray();
            }
        }
        return roots.ToArray();
    }

    private static string[] FindModStaticAlcRoots()
    {
        var target = _fixtureAlc;
        if (target is null) return [];
        var roots = new List<string>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var assembly in Loaded.Values.Distinct())
        {
            foreach (var type in SafeGetTypes(assembly))
            {
                var staticFields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var reflectionCacheSafe = !staticFields.Any(field => field.FieldType.Assembly == typeof(Godot.GodotObject).Assembly);
                var hasNativeStatic = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Any(field => field.FieldType.Assembly == typeof(Godot.GodotObject).Assembly);
                if (typeof(Godot.GodotObject).IsAssignableFrom(type) || hasNativeStatic) continue;
                foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var fieldType = field.FieldType;
                    if (fieldType.Assembly == typeof(Godot.GodotObject).Assembly) continue;
                    var inspectable = IsCollectibleFieldType(fieldType, target)
                        || fieldType == typeof(Assembly)
                        || !fieldType.IsValueType && typeof(Type).IsAssignableFrom(fieldType)
                        || typeof(Delegate).IsAssignableFrom(fieldType)
                        || reflectionCacheSafe && (typeof(MemberInfo).IsAssignableFrom(fieldType) || IsLazyFieldType(fieldType))
                        || IsManagedCollection(fieldType)
                        || fieldType == typeof(object);
                    if (!inspectable) continue;
                    try
                    {
                        ScanKnownAlcTypeShape(field.FieldType, type.FullName + "." + field.Name, target, roots);
                        var value = field.GetValue(null);
                        var path = type.FullName + "." + field.Name;
                        ScanKnownAlcValue(value, path, target, seen, roots, 0);
                        if (value is not null && value is not string && !value.GetType().IsValueType)
                            ScanKnownAlcObjectFields(value, path, target, seen, roots, 0);
                    }
                    catch { }
                    if (roots.Count >= 1000) return roots.ToArray();
                }
            }
        }
        return roots.ToArray();
    }

    private static void ScanKnownAlcObjectFields(object? value, string path, AssemblyLoadContext target, HashSet<object> seen, List<string> roots, int depth)
    {
        if (value is null || roots.Count >= 512 || depth >= 32 || value is string || value.GetType().IsValueType || !seen.Add(value)) return;
        if (AssemblyLoadContext.GetLoadContext(value.GetType().Assembly) == target)
        {
            roots.Add(path + " => object:" + value.GetType().FullName);
            return;
        }
        if (value is Delegate or Assembly or Type or MemberInfo) { ScanKnownAlcValue(value, path, target, seen, roots, depth); return; }
        if (value is Godot.GodotObject) return;
        foreach (var field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.FieldType.Assembly == typeof(Godot.GodotObject).Assembly) continue;
            object? fieldValue;
            try { fieldValue = field.GetValue(value); } catch { continue; }
            ScanKnownAlcValue(fieldValue, path + "." + field.Name, target, seen, roots, depth + 1);
            if (roots.Count >= 1000) return;
            if (fieldValue is not null && fieldValue is not string && !fieldValue.GetType().IsValueType)
                ScanKnownAlcObjectFields(fieldValue, path + "." + field.Name, target, seen, roots, depth + 1);
            if (roots.Count >= 1000) return;
        }
    }


    private static void ScanKnownAlcValue(object? value, string path, AssemblyLoadContext target, HashSet<object> seen, List<string> roots, int depth)
    {
        if (value is null || roots.Count >= 1000) return;
        if (value is AssemblyLoadContext context)
        {
            if (ReferenceEquals(context, target)) roots.Add(path + " => alc");
            return;
        }
        ScanKnownAlcTypeShape(value.GetType(), path + ".type", target, roots);
        if (value is Assembly assembly)
        {
            if (AssemblyLoadContext.GetLoadContext(assembly) == target) roots.Add(path + " => " + assembly.GetName().Name);
            return;
        }
        if (AssemblyLoadContext.GetLoadContext(value.GetType().Assembly) == target)
        {
            roots.Add(path + " => object:" + value.GetType().FullName);
            return;
        }
        if (value is Type type)
        {
            if (AssemblyLoadContext.GetLoadContext(type.Assembly) == target) roots.Add(path + " => " + type.FullName);
            return;
        }
        if (value is MemberInfo member)
        {
            if (AssemblyLoadContext.GetLoadContext(member.Module.Assembly) == target) roots.Add(path + " => member:" + member);
            return;
        }
        if (value.GetType().IsGenericType && value.GetType().GetGenericTypeDefinition() == typeof(Lazy<>))
        {
            var valueCreated = value.GetType().GetProperty("IsValueCreated")?.GetValue(value) as bool?;
            if (valueCreated == true)
                ScanKnownAlcValue(value.GetType().GetProperty("Value")?.GetValue(value), path + ".Value", target, seen, roots, depth + 1);
            return;
        }
        if (value is Delegate del)
        {
            if (del.Method.DeclaringType is { } declaring && AssemblyLoadContext.GetLoadContext(declaring.Assembly) == target) roots.Add(path + " => " + del.Method);
            if (del.Target is not null)
                ScanKnownAlcObjectFields(del.Target, path + ".Target", target, seen, roots, depth + 1);
            return;
        }
        if (depth >= 32 || value is string || value.GetType().IsValueType || !seen.Add(value)) return;
        if (value is System.Collections.IDictionary dictionary)
        {
            foreach (System.Collections.DictionaryEntry entry in dictionary)
            {
                ScanKnownAlcValue(entry.Key, path + ".key", target, seen, roots, depth + 1);
                ScanKnownAlcValue(entry.Value, path + ".value", target, seen, roots, depth + 1);
            }
            return;
        }
        if (value is System.Collections.IEnumerable enumerable)
        {
            var index = 0;
            foreach (var item in enumerable)
            {
                ScanKnownAlcValue(item, path + "[" + index++ + "]", target, seen, roots, depth + 1);
                if (roots.Count >= 1000) break;
            }
        }
    }

    private static void ScanKnownAlcTypeShape(Type type, string path, AssemblyLoadContext target, List<string> roots)
    {
        if (roots.Count >= 1000) return;
        if (type.IsArray)
        {
            ScanKnownAlcTypeShape(type.GetElementType()!, path + "[]", target, roots);
            return;
        }
        if (!type.IsGenericType) return;
        foreach (var argument in type.GetGenericArguments())
        {
            if (AssemblyLoadContext.GetLoadContext(argument.Assembly) == target)
                roots.Add(path + " => generic:" + type);
            ScanKnownAlcTypeShape(argument, path + "<" + argument.Name + ">", target, roots);
        }
    }

    private static string DescribeMod(Mod mod) => $"{mod.manifest?.id}:{mod.state}:assemblies={mod.assemblies.Count}:errors={string.Join(";", mod.errors ?? [])}";
    private static string KindOf(AbstractModel model) => model switch { CharacterModel => "Character", CardModel => "Card", RelicModel => "Relic", PowerModel => "Power", _ => "Other" };
    private static Type[] SafeGetTypes(Assembly assembly) { try { return assembly.GetTypes(); } catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null).Cast<Type>().ToArray(); } }
    private static void Patch(Type type, string method, string prefix) => Patches.Patch(AccessTools.Method(type, method)!, prefix: new HarmonyMethod(typeof(NativeFoundationModLoop), prefix));
    private static bool ExecutablePath(ref string __result) { __result = Path.Combine(Config.SnapshotRoot, "foundation-worker.exe"); return false; }
    private static bool NotDebug(ref bool __result) { __result = false; return false; }
    private static bool CommandLine(ref string[] __result) { __result = []; return false; }
    private static bool OsName(ref string __result) { __result = "Windows"; return false; }
    private static bool RejectCopy() => throw new InvalidOperationException("foundation fixture attempted forbidden save copy");
    private static bool SkipGodotScriptLookup() => false;
    private static bool HostHasArgument(string argument) => Environment.GetCommandLineArgs().Any(value => value == argument || value == "--" + argument);
    private static IEnumerable<CodeInstruction> CommandLineQueryBridge(IEnumerable<CodeInstruction> source)
    {
        var original = AccessTools.Method(typeof(CommandLineHelper), "HasArg", [typeof(string)])!;
        var replacement = AccessTools.Method(typeof(NativeFoundationModLoop), nameof(HostHasArgument))!;
        var instructions = source.ToArray(); var count = 0;
        foreach (var instruction in instructions) if (instruction.Calls(original)) { instruction.opcode = OpCodes.Call; instruction.operand = replacement; count++; }
        if (count != 1) throw new InvalidOperationException($"Expected one foundation ModManager command-line query; found {count}");
        return instructions;
    }
    private static FoundationConfig ReadConfig() => JsonSerializer.Deserialize<FoundationConfig>(File.ReadAllText(ConfigPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("invalid foundation config");
    private static string RequiredFile(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value && File.Exists(value) ? Path.GetFullPath(value) : throw new InvalidOperationException("missing foundation path: " + name);

    private static void ResetModelDbForTeardown()
    {
        ModelDb.ResetForTest();
        foreach (var field in typeof(ModelDb).GetFields(BindingFlags.Static | BindingFlags.NonPublic))
        {
            if (field.Name == "_contentById")
            {
                (field.GetValue(null) as System.Collections.IDictionary)?.Clear();
                continue;
            }
            if (!field.IsInitOnly && !field.FieldType.IsValueType && field.FieldType != typeof(string))
            {
                try { field.SetValue(null, null); } catch { }
            }
        }
    }

    private static void DetachGameModManagerHandlers()
    {
        try { ModManager.Dispose(); } catch { }
        try
        {
            var method = typeof(ModManager).GetMethod("HandleAssemblyResolveFailure", BindingFlags.Static | BindingFlags.NonPublic);
            if (method is not null)
            {
                var handler = (ResolveEventHandler)method.CreateDelegate(typeof(ResolveEventHandler));
                AppDomain.CurrentDomain.AssemblyResolve -= handler;
            }
        }
        catch { }
    }

    private static void DetachModRuntimeEvents()
    {
        var target = _fixtureAlc;
        if (target is null) return;
        var handlerKinds = new (Type HandlerType, Action<Delegate> Remove)[]
        {
            (typeof(UnhandledExceptionEventHandler), handler => AppDomain.CurrentDomain.UnhandledException -= (UnhandledExceptionEventHandler)handler),
            (typeof(EventHandler<UnobservedTaskExceptionEventArgs>), handler => TaskScheduler.UnobservedTaskException -= (EventHandler<UnobservedTaskExceptionEventArgs>)handler),
            (typeof(EventHandler), handler => AppDomain.CurrentDomain.ProcessExit -= (EventHandler)handler),
            (typeof(AssemblyLoadEventHandler), handler => AppDomain.CurrentDomain.AssemblyLoad -= (AssemblyLoadEventHandler)handler)
        };
        foreach (var assembly in target.Assemblies.ToArray())
        foreach (var type in SafeGetTypes(assembly))
        foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (var (handlerType, remove) in handlerKinds)
            {
                try
                {
                    var handler = method.CreateDelegate(handlerType);
                    remove(handler);
                }
                catch { }
            }
        }
    }

    private static void ResetRunManagerForTeardown()
    {
        var manager = MegaCrit.Sts2.Core.Runs.RunManager.Instance;
        foreach (var field in typeof(MegaCrit.Sts2.Core.Runs.RunManager).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.IsInitOnly || field.FieldType.IsValueType) continue;
            try { field.SetValue(manager, null); } catch { }
        }
    }

    // RunManager owns several disposable synchronizers whose fields are
    // readonly.  CleanUp disposes their external registrations, but a
    // fire-and-forget task or a delegate cycle can still retain one of those
    // objects after the manager's properties are cleared.  Sever that
    // managed run graph explicitly so no queued task can retain a Player or
    // another collectible model through a readonly field.
    internal static void ReleaseRunGraphForTeardown()
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var manager = MegaCrit.Sts2.Core.Runs.RunManager.Instance;
        foreach (var field in typeof(MegaCrit.Sts2.Core.Runs.RunManager).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.FieldType.IsValueType || field.FieldType == typeof(string)) continue;
            if (field.Name.Contains("State", StringComparison.Ordinal)) continue;
            object? value;
            try { value = field.GetValue(manager); } catch { continue; }
            ClearManagedRunObject(value, seen, 0);
        }
    }

    private static void ClearManagedRunObject(object? value, HashSet<object> seen, int depth)
    {
        if (value is null || depth >= 8 || value is string || value.GetType().IsValueType
            || value is Godot.GodotObject || !seen.Add(value)) return;
        foreach (var field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.IsLiteral || field.FieldType.IsValueType || field.FieldType == typeof(string)
                || field.FieldType.Assembly == typeof(Godot.GodotObject).Assembly) continue;
            object? child;
            try { child = field.GetValue(value); } catch { continue; }
            try
            {
                if (child is Array array) Array.Clear(array, 0, array.Length);
                else if (child is System.Collections.IDictionary dictionary) dictionary.Clear();
                else if (child is System.Collections.IList list) list.Clear();
                else if (ShouldRecurseRunChild(child))
                    ClearManagedRunObject(child, seen, depth + 1);
                ClearInstanceReference(value, field);
            }
            catch { }
        }
    }

    internal static void ReleasePlayerForTeardown(object? player)
    {
        if (player is null) return;
        ClearManagedRunObject(player, new HashSet<object>(ReferenceEqualityComparer.Instance), 0);
        foreach (var field in player.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.IsLiteral || field.FieldType.IsValueType) continue;
            try
            {
                var value = field.GetValue(player);
                if (value is System.Collections.IDictionary dictionary) dictionary.Clear();
                else if (value is System.Collections.IList list) list.Clear();
                if (field.FieldType != typeof(string))
                {
                    ClearInstanceReference(player, field);
                    try
                    {
                        var remaining = field.GetValue(player);
                        if (remaining is not null && AssemblyLoadContext.GetLoadContext(remaining.GetType().Assembly) == _fixtureAlc
                            && StaticClearFailures.Count < 256)
                            StaticClearFailures.Add("player." + field.Name + ":verification:" + remaining.GetType().FullName);
                    }
                    catch { }
                }
            }
            catch { }
        }
    }

    private static bool ShouldRecurseRunChild(object? child)
    {
        if (child is null || child is string || child is Godot.GodotObject
            || child is Delegate || child is Type || child is MemberInfo || child is Assembly)
            return false;
        var assembly = child.GetType().Assembly;
        // Walk only the game/host object graph. Never enter a collectible mod
        // object or an external dependency, and never invoke arbitrary mod
        // fields while the teardown is severing references.
        return AssemblyLoadContext.GetLoadContext(assembly) == AssemblyLoadContext.Default
            && string.Equals(assembly.GetName().Name, "sts2", StringComparison.Ordinal);
    }

    private static void ResetCombatStateForTeardown()
    {
        var combat = CombatManager.Instance;
        try { combat.Reset(graceful: false); } catch { }
        foreach (var field in typeof(CombatManager).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.IsInitOnly || field.FieldType.IsValueType) continue;
            try { field.SetValue(combat, null); } catch { }
        }
        var tracker = combat.StateTracker;
        foreach (var field in tracker.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.IsInitOnly || field.FieldType.IsValueType) continue;
            try { field.SetValue(tracker, null); } catch { }
        }
        try { combat.History.Clear(); } catch { }
        try
        {
            var cardDb = MegaCrit.Sts2.Core.GameActions.Multiplayer.NetCombatCardDb.Instance;
            cardDb.ClearCardsForTesting();
            var subscriptions = AccessTools.Field(cardDb.GetType(), "_subscriptions")?.GetValue(cardDb) as System.Collections.IList;
            subscriptions?.Clear();
        }
        catch { }
    }

    private static void ResetHarmonyStateForTeardown()
    {
        var shared = typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonySharedState", throwOnError: false)
            ?? Type.GetType("HarmonyLib.HarmonySharedState, 0Harmony", throwOnError: false);
        if (shared is null) return;
        foreach (var name in new[] { "state", "originals", "originalsMono" })
        {
            var field = shared.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.GetValue(null) is System.Collections.IDictionary dictionary) dictionary.Clear();
        }
    }

    private static void ClearHarmonyStaticCollections()
    {
        foreach (var type in SafeGetTypes(typeof(Harmony).Assembly))
        {
            foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.IsLiteral) continue;
                try
                {
                    var value = field.GetValue(null);
                    if (value is Array array) { Array.Clear(array, 0, array.Length); continue; }
                    if (value is System.Collections.IDictionary dictionary) { dictionary.Clear(); continue; }
                    var clear = value?.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, types: Type.EmptyTypes, modifiers: null);
                    clear?.Invoke(value, null);
                }
                catch { }
            }
        }
    }

    // Real mod initializers may leave managed registries in their collectible
    // assembly.  Clear only ordinary managed collections; reading arbitrary
    // Godot-backed static fields can invoke native constructors during
    // teardown and is not a safe generic cleanup operation.
    private static void ResetModStaticStateForTeardown(IReadOnlyCollection<string>? observedRoots = null)
    {
        var target = _fixtureAlc;
        if (target is null) return;
        // The entry assembly is collectible; Godot-bearing dependency
        // assemblies stay in Default because Godot's native registration
        // tables retain their types. Clear both contexts' ordinary managed
        // registries so those default libraries cannot retain the target.
        foreach (var assembly in target.Assemblies.Concat(Loaded.Values).Distinct().ToArray())
        {
            foreach (var type in SafeGetTypes(assembly))
            {
                // RitsuLibDataPaths resolves Godot.OS from its type
                // initializer.  Teardown must not force that presentation
                // path in a headless worker.
                if (SkipStaticTeardownType(type)) continue;
                var staticFields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                // Compiler-generated callback caches are ordinary managed
                // delegates even when their declaring type also carries a
                // Godot field. Clear the slot without reading it: a read can
                // run the type initializer again during teardown.
                foreach (var delegateField in staticFields.Where(field =>
                    !field.IsLiteral && typeof(Delegate).IsAssignableFrom(field.FieldType)
                    && (type.Name.Contains("<>c", StringComparison.Ordinal)
                        || type.Name.Contains("DisplayClass", StringComparison.Ordinal))))
                    ClearStaticReferenceWithoutRead(delegateField);
                // Reading any static field on a type that also declares a
                // Godot field can run a native-dependent type initializer.
                // Keep those types on the safe path; their managed
                // registries are not safe to touch from headless teardown.
                if (staticFields.Any(field => field.FieldType.Assembly == typeof(Godot.GodotObject).Assembly))
                {
                    if (observedRoots is not null && type.FullName is { } typeName)
                    {
                        foreach (var field in staticFields)
                        {
                            if (field.IsLiteral || field.FieldType.IsValueType || field.FieldType == typeof(string)) continue;
                            var prefix = typeName + "." + field.Name;
                            if (observedRoots.Any(root => root.StartsWith(prefix, StringComparison.Ordinal)))
                                ClearStaticReferenceWithoutRead(field);
                        }
                    }
                    continue;
                }
                foreach (var field in staticFields)
                {
                    if (field.IsLiteral) continue;
                    // A target type can keep a Player/CardModel through a
                    // game-typed holder such as SpireField<Player, T>. The
                    // holder itself lives in the game assembly, so filtering
                    // by the declared field type misses the actual Mod-owned
                    // state. Clear every managed reference slot on the target
                    // type; native Godot fields are left untouched because
                    // reading or writing them can invoke the engine.
                    if (field.FieldType.Assembly == typeof(Godot.GodotObject).Assembly) continue;
                    var fieldType = field.FieldType;
                    if (fieldType.IsValueType || fieldType == typeof(string)) continue;
                    try
                    {
                        var value = field.GetValue(null);
                        var ownerExternal = AssemblyLoadContext.GetLoadContext(field.DeclaringType?.Assembly!) != target;
                        var runtimeCollectible = value is not null
                            && AssemblyLoadContext.GetLoadContext(value.GetType().Assembly) == target;
                        var fieldCollectible = IsCollectibleFieldType(field.FieldType, target);
                        if (value is Array array)
                        {
                            Array.Clear(array, 0, array.Length);
                            if (fieldCollectible || runtimeCollectible) ClearStaticReference(field);
                            continue;
                        }
                        if (value is Delegate) { ClearStaticReference(field); continue; }
                        if (value is System.Collections.IDictionary dictionary)
                        {
                            dictionary.Clear();
                            if (fieldCollectible || runtimeCollectible) ClearStaticReference(field);
                            continue;
                        }
                        var clear = value?.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, types: Type.EmptyTypes, modifiers: null);
                        if (clear is not null)
                        {
                            clear.Invoke(value, null);
                            if (fieldCollectible || runtimeCollectible) ClearStaticReference(field);
                            continue;
                        }
                        if (!field.FieldType.IsValueType) ClearStaticReference(field);
                    }
                    catch (Exception ex)
                    {
                        // A closed generic static can reject reflection reads
                        // before its slot is inspected. The declaring type is
                        // already on the safe managed path here, so clear the
                        // slot without reading it and keep the diagnostic.
                        ClearStaticReferenceWithoutRead(field);
                        if (StaticClearFailures.Count < 256)
                            StaticClearFailures.Add(type.FullName + "." + field.Name + ":read:" + ex.GetType().Name);
                    }
                }
            }
        }
        ClearRuntimeClosedGenericStatics(target);
        ClearCompilerGeneratedStatics(target);
        ClearClosedGenericBaseStatics(target);
    }

    private static void ClearCompilerGeneratedStatics(AssemblyLoadContext target)
    {
        foreach (var assembly in target.Assemblies.Concat(Loaded.Values).Distinct().ToArray())
        {
            foreach (var type in SafeGetTypes(assembly))
            {
                if (SkipStaticTeardownType(type)) continue;
                if (type.ContainsGenericParameters
                    || (type.Name.IndexOf("<>c", StringComparison.Ordinal) < 0
                        && type.Name.IndexOf("DisplayClass", StringComparison.Ordinal) < 0)) continue;
                foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (field.IsLiteral || field.FieldType.IsValueType || field.FieldType == typeof(string)
                        || field.FieldType.Assembly == typeof(Godot.GodotObject).Assembly) continue;
                    try
                    {
                        var value = field.GetValue(null);
                        var ownerExternal = AssemblyLoadContext.GetLoadContext(field.DeclaringType?.Assembly!) != target;
                        var runtimeCollectible = value is not null
                            && IsCollectibleFieldType(value.GetType(), target);
                        var fieldCollectible = IsCollectibleFieldType(field.FieldType, target);
                        if (value is Array array) { Array.Clear(array, 0, array.Length); if (fieldCollectible || runtimeCollectible) ClearStaticReference(field); continue; }
                        if (value is System.Collections.IDictionary dictionary) { dictionary.Clear(); if (fieldCollectible || runtimeCollectible) ClearStaticReference(field); continue; }
                        if (value is Delegate) { ClearStaticReference(field); continue; }
                        var clear = value?.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                            binder: null, types: Type.EmptyTypes, modifiers: null);
                        if (clear is not null) { clear.Invoke(value, null); if (fieldCollectible || runtimeCollectible) ClearStaticReference(field); continue; }
                        ClearStaticReference(field);
                    }
                    catch { ClearStaticReferenceWithoutRead(field); }
                }
            }
        }
    }

    private static void ClearRuntimeClosedGenericStatics(AssemblyLoadContext target)
    {
        var closedTypes = new HashSet<Type>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var assembly in target.Assemblies.Concat(Loaded.Values).Distinct().ToArray())
        {
            foreach (var type in SafeGetTypes(assembly))
            {
                if (SkipStaticTeardownType(type)) continue;
                var staticFields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (staticFields.Any(field => field.FieldType.Assembly == typeof(Godot.GodotObject).Assembly)) continue;
                foreach (var field in staticFields)
                {
                    if (field.IsLiteral) continue;
                    if (field.FieldType.Assembly == typeof(Godot.GodotObject).Assembly) continue;
                    try { CollectRuntimeClosedGenericTypes(field.GetValue(null), target, closedTypes, seen, 0); }
                    catch { }
                }
            }
        }
        foreach (var type in closedTypes)
        {
            foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.IsLiteral || field.FieldType.IsValueType || field.FieldType == typeof(string)
                    || field.FieldType.Assembly == typeof(Godot.GodotObject).Assembly) continue;
                try
                {
                    var value = field.GetValue(null);
                        var ownerExternal = AssemblyLoadContext.GetLoadContext(field.DeclaringType?.Assembly!) != target;
                        var runtimeCollectible = value is not null && IsCollectibleFieldType(value.GetType(), target);
                        var fieldCollectible = IsCollectibleFieldType(field.FieldType, target);
                        if (value is Array array) { Array.Clear(array, 0, array.Length); if (fieldCollectible || runtimeCollectible) ClearStaticReference(field); continue; }
                        if (value is System.Collections.IDictionary dictionary) { dictionary.Clear(); if (fieldCollectible || runtimeCollectible) ClearStaticReference(field); continue; }
                    if (value is Delegate) { ClearStaticReference(field); continue; }
                    var clear = value?.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        binder: null, types: Type.EmptyTypes, modifiers: null);
                        if (clear is not null) { clear.Invoke(value, null); if (fieldCollectible || runtimeCollectible) ClearStaticReference(field); continue; }
                    ClearStaticReference(field);
                }
                catch { }
            }
        }
    }

    private static void CollectRuntimeClosedGenericTypes(object? value, AssemblyLoadContext target,
        HashSet<Type> closedTypes, HashSet<object> seen, int depth)
    {
        if (value is null || depth >= 12) return;
        if (value is Delegate del)
        {
            var declaring = del.Method.DeclaringType;
            if (declaring?.IsGenericType == true && !declaring.ContainsGenericParameters
                && AssemblyLoadContext.GetLoadContext(declaring.Assembly) == target)
                closedTypes.Add(declaring);
            return;
        }
        var runtimeType = value.GetType();
        if (runtimeType.IsGenericType && !runtimeType.ContainsGenericParameters
            && AssemblyLoadContext.GetLoadContext(runtimeType.Assembly) == target)
            closedTypes.Add(runtimeType);
        if (value is string || runtimeType.IsValueType || value is Godot.GodotObject || !seen.Add(value)) return;
        if (value is System.Collections.IDictionary dictionary)
        {
            foreach (System.Collections.DictionaryEntry entry in dictionary)
            {
                CollectRuntimeClosedGenericTypes(entry.Key, target, closedTypes, seen, depth + 1);
                CollectRuntimeClosedGenericTypes(entry.Value, target, closedTypes, seen, depth + 1);
            }
            return;
        }
        if (value is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
                CollectRuntimeClosedGenericTypes(item, target, closedTypes, seen, depth + 1);
            return;
        }
        foreach (var field in runtimeType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.FieldType.Assembly == typeof(Godot.GodotObject).Assembly) continue;
            try { CollectRuntimeClosedGenericTypes(field.GetValue(value), target, closedTypes, seen, depth + 1); }
            catch { }
        }
    }

    private static bool SkipStaticTeardownType(Type type) =>
        type.FullName?.Contains("RitsuLibDataPaths", StringComparison.Ordinal) == true;

    private static bool IsCollectibleFieldType(Type fieldType, AssemblyLoadContext target)
    {
        if (AssemblyLoadContext.GetLoadContext(fieldType.Assembly) == target) return true;
        if (fieldType.IsArray || fieldType.IsByRef || fieldType.IsPointer)
            return IsCollectibleFieldType(fieldType.GetElementType()!, target);
        return fieldType.IsGenericType && fieldType.GetGenericArguments()
            .Any(argument => IsCollectibleFieldType(argument, target));
    }

    private static bool IsLazyFieldType(Type fieldType) =>
        fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(Lazy<>);


    private static void ClearManagedStaticCollections(Type root)
    {
        foreach (var field in root.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.IsLiteral) continue;
            var fieldType = field.FieldType;
            if (!typeof(System.Collections.IDictionary).IsAssignableFrom(fieldType)
                && !typeof(System.Collections.IList).IsAssignableFrom(fieldType)
                && !typeof(System.Collections.ICollection).IsAssignableFrom(fieldType)) continue;
            try
            {
                var value = field.GetValue(null);
                if (value is System.Collections.IDictionary dictionary) { dictionary.Clear(); continue; }
                var clear = value?.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, types: Type.EmptyTypes, modifiers: null);
                clear?.Invoke(value, null);
            }
            catch { }
        }
    }

    private static void ClearGameManagedStaticCollections()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("MegaCrit.Sts2", StringComparison.Ordinal) == true)
            .Distinct())
        {
            foreach (var type in SafeGetTypes(assembly))
            {
                if (typeof(Godot.GodotObject).IsAssignableFrom(type)) continue;
                foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (field.IsLiteral) continue;
                    try
                    {
                        var value = field.GetValue(null);
                        if (value is Array array) { Array.Clear(array, 0, array.Length); continue; }
                        if (value is Delegate) { ClearStaticReference(field); continue; }
                        if (value is System.Collections.IDictionary dictionary) { dictionary.Clear(); continue; }
                        if (value is System.Collections.IList list) { list.Clear(); continue; }
                        var clear = value?.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, types: Type.EmptyTypes, modifiers: null);
                        clear?.Invoke(value, null);
                    }
                    catch { }
                }
            }
        }
    }

    private static void ClearGameManagedStaticState()
    {
        foreach (var type in new[]
        {
            typeof(MegaCrit.Sts2.Core.Multiplayer.Serialization.MessageTypes),
            typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.ActionTypes)
        })
        {
            foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.IsLiteral || field.FieldType.IsValueType || field.FieldType == typeof(string)) continue;
                try { ClearStaticReference(field); } catch { }
            }
        }
    }

    private static bool IsManagedCollection(Type fieldType)
    {
        if (typeof(System.Collections.IDictionary).IsAssignableFrom(fieldType)
            || typeof(System.Collections.IList).IsAssignableFrom(fieldType)
            || typeof(System.Collections.ICollection).IsAssignableFrom(fieldType)) return true;
        return fieldType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() is var definition
            && (definition == typeof(ICollection<>) || definition == typeof(IList<>) || definition == typeof(ISet<>)));
    }

    private static void ClearStaticReference(FieldInfo field)
    {
        if (field.IsLiteral || field.FieldType.IsValueType) return;
        try
        {
            field.SetValue(null, null);
            if (field.GetValue(null) is null) return;
        }
        catch (Exception ex)
        {
            if (TryClearStaticReferenceWithDynamicMethod(field)) return;
            // .NET deliberately rejects writes to initonly static fields. A
            // normal DynamicMethod is unsuitable here because its lifetime
            // can pin the collectible ALC through the emitted field token.
            // Emit the setter into a RunAndCollect dynamic assembly instead;
            // the short-lived assembly and delegate disappear with the
            // teardown frame after the write.
            try
            {
                var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(
                    new AssemblyName("SimulatorLab.TeardownSetter." + Guid.NewGuid().ToString("N")),
                    AssemblyBuilderAccess.RunAndCollect);
                var accessCtor = typeof(System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute)
                    .GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        binder: null,
                        types: [typeof(string)],
                        modifiers: null)
                    ?? throw new MissingMethodException("IgnoresAccessChecksToAttribute(string)");
                dynamicAssembly.SetCustomAttribute(new CustomAttributeBuilder(
                    accessCtor,
                    [field.Module.Assembly.GetName().Name ?? field.Module.Assembly.FullName ?? ""]));
                var module = dynamicAssembly.DefineDynamicModule("Teardown");
                var type = module.DefineType(
                    "Setter",
                    TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed);
                var method = type.DefineMethod(
                    "Clear",
                    MethodAttributes.Static | MethodAttributes.Public,
                    typeof(void),
                    Type.EmptyTypes);
                var il = method.GetILGenerator();
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Stsfld, field);
                il.Emit(OpCodes.Ret);
                var setterType = type.CreateType()!;
                ((Action)setterType.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public)!
                    .CreateDelegate(typeof(Action)))();
                if (field.GetValue(null) is null) return;
                StaticClearFailures.Add(field.DeclaringType?.FullName + "." + field.Name + ":verification:non-null");
                return;
            }
            catch (Exception dynamicError)
            {
                StaticClearFailures.Add(field.DeclaringType?.FullName + "." + field.Name + ":reflection:" + ex.GetType().Name + ";dynamic:" + dynamicError.GetType().Name + ":" + dynamicError.Message);
            }

            }
        StaticClearFailures.Add(field.DeclaringType?.FullName + "." + field.Name + ":reflection:non-null");
    }

    private static void ClearStaticReferenceWithoutRead(FieldInfo field)
    {
        if (field.IsLiteral || field.FieldType.IsValueType) return;
        try
        {
            field.SetValue(null, null);
            return;
        }
        catch { }
        try
        {
            if (TryClearStaticReferenceWithDynamicMethod(field)) return;
            var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("SimulatorLab.TeardownDelegateSetter." + Guid.NewGuid().ToString("N")),
                AssemblyBuilderAccess.RunAndCollect);
            var accessCtor = typeof(System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute)
                .GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null, types: [typeof(string)], modifiers: null)
                ?? throw new MissingMethodException("IgnoresAccessChecksToAttribute(string)");
            dynamicAssembly.SetCustomAttribute(new CustomAttributeBuilder(accessCtor,
                [field.Module.Assembly.GetName().Name ?? field.Module.Assembly.FullName ?? ""]));
            var module = dynamicAssembly.DefineDynamicModule("TeardownDelegate");
            var type = module.DefineType("Setter", TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed);
            var method = type.DefineMethod("Clear", MethodAttributes.Static | MethodAttributes.Public,
                typeof(void), Type.EmptyTypes);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Stsfld, field);
            il.Emit(OpCodes.Ret);
            var setterType = type.CreateType()!;
            ((Action)setterType.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public)!
                .CreateDelegate(typeof(Action)))();
        }
        catch { }
    }

    private static void ClearInstanceReference(object instance, FieldInfo field)
    {
        if (field.IsLiteral || field.FieldType.IsValueType) return;
        try { field.SetValue(instance, null); return; }
        catch { }
        try
        {
            if (TryClearInstanceReferenceWithDynamicMethod(instance, field)) return;
        }
        catch { }
        try
        {
            var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("SimulatorLab.TeardownInstanceSetter." + Guid.NewGuid().ToString("N")),
                AssemblyBuilderAccess.RunAndCollect);
            var accessCtor = typeof(System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute)
                .GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null, types: [typeof(string)], modifiers: null)
                ?? throw new MissingMethodException("IgnoresAccessChecksToAttribute(string)");
            dynamicAssembly.SetCustomAttribute(new CustomAttributeBuilder(accessCtor,
                [field.Module.Assembly.GetName().Name ?? field.Module.Assembly.FullName ?? ""]));
            var module = dynamicAssembly.DefineDynamicModule("TeardownInstance");
            var type = module.DefineType("Setter", TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed);
            var method = type.DefineMethod("Clear", MethodAttributes.Static | MethodAttributes.Public,
                typeof(void), [typeof(object)]);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, field.DeclaringType!);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Stfld, field);
            il.Emit(OpCodes.Ret);
            var setterType = type.CreateType()!;
            ((Action<object>)setterType.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public)!
                .CreateDelegate(typeof(Action<object>)))(instance);
        }
        catch { }
    }

    private static bool TryClearStaticReferenceWithDynamicMethod(FieldInfo field)
    {
        try
        {
            var method = new DynamicMethod("ClearStatic", typeof(void), Type.EmptyTypes, field.Module, skipVisibility: true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Stsfld, field);
            il.Emit(OpCodes.Ret);
            ((Action)method.CreateDelegate(typeof(Action)))();
            return true;
        }
        catch { return false; }
    }

    private static bool TryClearInstanceReferenceWithDynamicMethod(object instance, FieldInfo field)
    {
        try
        {
            var method = new DynamicMethod("ClearInstance", typeof(void), [typeof(object)], field.Module, skipVisibility: true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, field.DeclaringType!);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Stfld, field);
            il.Emit(OpCodes.Ret);
            ((Action<object>)method.CreateDelegate(typeof(Action<object>)))(instance);
            return true;
        }
        catch { return false; }
    }

    private sealed class FoundationAssemblyLoadContext(FoundationConfig config) : AssemblyLoadContext("simulatorlab-foundation-" + config.CaseId, isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var simple = assemblyName.Name ?? string.Empty;
            if (!config.AssemblyPaths.TryGetValue(simple, out var relative)) return null;
            var path = Path.IsPathRooted(relative) ? relative : Path.Combine(config.SnapshotRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return null;
            // A target reference to a shared Godot assembly must bind to the
            // already-loaded Default copy; loading a second copy into the
            // collectible context leaves native type registrations behind.
            var existing = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => string.Equals(a.GetName().Name, simple, StringComparison.Ordinal));
            return existing ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(path));
        }
    }

    private sealed class SnapshotFileIo(FoundationConfig config) : IModManagerFileIo
    {
        public string[] GetFilesAt(string path)
        {
            var normalized = path.Replace('\\', '/').TrimEnd('/');
            var mod = config.Mods.FirstOrDefault(x => normalized.EndsWith("/" + x.VirtualPath, StringComparison.Ordinal) || normalized.EndsWith(x.VirtualPath, StringComparison.Ordinal));
            var result = mod is null ? [] : new[] { mod.ManifestFile };
            Events.Add(new { kind = "files", path = normalized, result });
            return result;
        }
        public string[] GetDirectoriesAt(string path)
        {
            var normalized = path.Replace('\\', '/').TrimEnd('/');
            var result = normalized.EndsWith("mods_STEAMTEST", StringComparison.Ordinal)
                ? [] : normalized.EndsWith("mods", StringComparison.Ordinal) ? config.Mods.Select(x => x.VirtualPath).ToArray() : [];
            Events.Add(new { kind = "dirs", path = normalized, result });
            return result;
        }
        public bool FileExists(string path) => Map(path) is not null;
        public bool DirectoryExists(string path) => true;
        public Stream OpenStream(string path, Godot.FileAccess.ModeFlags mode) => mode == Godot.FileAccess.ModeFlags.Read ? File.OpenRead(Map(path) ?? throw new FileNotFoundException(path)) : throw new InvalidOperationException("foundation fixture write denied");
        public void MakeDirRecursive(string path) => throw new InvalidOperationException("foundation fixture mkdir denied");
        public Error CopyFile(string sourcePath, string destinationPath) => throw new InvalidOperationException("foundation fixture copy denied");
        private string? Map(string path)
        {
            var normalized = path.Replace('\\', '/');
            foreach (var mod in config.Mods)
            {
                var prefix = mod.VirtualPath.Trim('/') + "/";
                if (normalized.EndsWith(prefix + mod.ManifestFile, StringComparison.Ordinal)) return Path.Combine(config.SnapshotRoot, mod.ManifestPath.Replace('/', Path.DirectorySeparatorChar));
                foreach (var pair in mod.VirtualFiles)
                    if (normalized.EndsWith(prefix + pair.Key, StringComparison.Ordinal)) return Path.Combine(config.SnapshotRoot, pair.Value.Replace('/', Path.DirectorySeparatorChar));
            }
            return null;
        }
    }

    internal sealed class FoundationConfig
    {
        public string CaseId { get; set; } = "foundation";
        public string SnapshotId { get; set; } = "";
        public string SnapshotRoot { get; set; } = "";
        public string SnapshotContentSha256 { get; set; } = "";
        public string ManifestSha256 { get; set; } = "";
        public string TargetGameVersion { get; set; } = "0.111.0";
        public string GameCommit { get; set; } = "";
        public string TargetModId { get; set; } = "";
        public string TargetAssembly { get; set; } = "";
        public string? RegistrationIdentity { get; set; }
        public string[] DependencyClosure { get; set; } = [];
        public string Mode { get; set; } = "normal";
        public string FailurePoint { get; set; } = "";
        public string FailureModId { get; set; } = "";
        public Dictionary<string, string> AssemblyPaths { get; set; } = new(StringComparer.Ordinal);
        public FoundationMod[] Mods { get; set; } = [];
    }
    internal sealed class FoundationMod
    {
        public string Id { get; set; } = "";
        public string VirtualPath { get; set; } = "";
        public string ManifestPath { get; set; } = "";
        public string ManifestFile { get; set; } = "";
        public string[] AssemblyPaths { get; set; } = [];
        public bool Initialize { get; set; } = true;
        public Dictionary<string, string> VirtualFiles { get; set; } = new(StringComparer.Ordinal);
    }
}




























