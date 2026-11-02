using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace OfflineSearchHarness;

/// <summary>Loads the hash-locked Lab snapshot of the existing Mod into this isolated harness; it never deploys it.</summary>
internal static class EleanorModBootstrap
{
    private static Assembly? _modAssembly;
    private static string? _manifestJson;

    public static string ManifestJson => _manifestJson
        ?? throw new InvalidOperationException("MyMod manifest has not been loaded.");

    public static async Task ConfigureValidationLoadout(RunState runState)
    {
        if (Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_COMPLETE_ORB") != "1") return;
        Player player = runState.Players.Single();
        RelicModel cracked = player.Relics.Single(relic => relic.GetType().Name == "CrackedCrystalOrb");
        RelicModel complete = ModelDb.All.OfType<RelicModel>()
            .Single(relic => relic.GetType().Name == "CompleteCrystalOrb").ToMutable();
        await RelicCmd.Replace(cracked, complete);
    }

    public static object LoadFromEnvironment()
    {
        string modPath = RequiredPath("SIMULATORLAB_ELEANOR_MOD_DLL");
        string baseLibPath = RequiredPath("SIMULATORLAB_ELEANOR_BASELIB_DLL");
        string manifestPath = RequiredPath("SIMULATORLAB_ELEANOR_MANIFEST");
        string expectedVersion = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_VERSION") ?? "";
        string manifestJson = File.ReadAllText(manifestPath, Encoding.UTF8);
        using JsonDocument manifestDocument = JsonDocument.Parse(manifestJson);
        JsonElement manifest = manifestDocument.RootElement;
        string manifestId = manifest.GetProperty("id").GetString() ?? "";
        string manifestName = manifest.GetProperty("name").GetString() ?? "";
        string manifestVersion = manifest.GetProperty("version").GetString() ?? "";
        bool affectsGameplay = manifest.GetProperty("affects_gameplay").GetBoolean();
        AssemblyLoadContext context = AssemblyLoadContext.Default;
        Assembly? baseLib = context.Assemblies.FirstOrDefault(assembly => assembly.GetName().Name == "BaseLib")
            ?? context.LoadFromAssemblyPath(baseLibPath);
        Assembly mod = context.LoadFromAssemblyPath(modPath);
        if (mod.GetName().Name != "MyMod" || manifestId != mod.GetName().Name
            || manifestVersion != expectedVersion || !affectsGameplay)
            throw new InvalidOperationException($"Loaded assembly/manifest identity mismatch: assembly={mod.GetName().Name}, id={manifestId}, version={manifestVersion}, gameplay={affectsGameplay}.");

        Type initializer = mod.GetType("MyMod.MyModCode.MainFile", throwOnError: true)!;
        MethodInfo initialize = initializer.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(initializer.FullName, "Initialize");
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_LOOP_STAGE"))) initialize.Invoke(null, null);
        _modAssembly = mod;
        _manifestJson = manifestJson;
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(modPath))).ToLowerInvariant();
        string manifestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifestJson))).ToLowerInvariant();
        return new
        {
            modId = manifestId,
            modName = manifestName,
            modVersion = manifestVersion,
            assemblyVersion = mod.GetName().Version?.ToString(),
            assemblyFullName = mod.FullName,
            assemblyMvid = mod.ManifestModule.ModuleVersionId,
            assemblyPath = Path.GetFullPath(modPath),
            assemblySha256 = hash,
            manifestPath = Path.GetFullPath(manifestPath),
            manifestSha256 = manifestHash,
            baseLibPath = Path.GetFullPath(baseLibPath),
            baseLibAssembly = baseLib.GetName().Version?.ToString(),
            initializer = initializer.FullName + ".Initialize",
            gameInstallWrites = false,
        };
    }

    public static void RegisterReflectionTypesAfterHarnessReset()
    {
        Assembly mod = _modAssembly ?? throw new InvalidOperationException("MyMod must be loaded before registering its model types.");
        using JsonDocument manifest = JsonDocument.Parse(ManifestJson);
        string manifestId = manifest.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Pinned Mod manifest has no id.");
        // The headless bootstrap bypasses ModManager's manifest loader. Associate after the
        // harness has reset model discovery and immediately before ModelDb.Init scans this assembly.
        ModManager.AssociateAssemblyWithMod(manifestId, mod);
        Assembly game = typeof(MegaCrit.Sts2.Core.Combat.CombatState).Assembly;
        Type modelType = game.GetType("MegaCrit.Sts2.Core.Models.AbstractModel", throwOnError: true)!;
        Type[] modModels = mod.GetTypes().Where(type => type.IsClass && !type.IsAbstract && modelType.IsAssignableFrom(type)).ToArray();
        Type helper = game.GetType("MegaCrit.Sts2.Core.Helpers.ReflectionHelper", throwOnError: true)!;
        FieldInfo field = helper.GetField("_modTypes", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(helper.FullName, "_modTypes");
        if (field.FieldType != typeof(Type[]))
            throw new InvalidOperationException($"Unexpected v0.111.0 ReflectionHelper._modTypes type: {field.FieldType.FullName}.");
        field.SetValue(null, modModels);
    }


    public static Type[] IncludeModels(Type[] baseModels)
    {
        Assembly? mod = _modAssembly;
        if (mod == null) return baseModels;
        Type modelType = typeof(MegaCrit.Sts2.Core.Combat.CombatState).Assembly
            .GetType("MegaCrit.Sts2.Core.Models.AbstractModel", throwOnError: true)!;
        return baseModels.Concat(mod.GetTypes().Where(type => type.IsClass && !type.IsAbstract && modelType.IsAssignableFrom(type)))
            .Distinct().ToArray();
    }

    private static string RequiredPath(string name)
    {
        string path = Environment.GetEnvironmentVariable(name) ?? "";
        if (path.Length == 0 || !File.Exists(path))
            throw new InvalidOperationException($"Required local Mod test input is missing: {name}={path}.");
        return Path.GetFullPath(path);
    }
}
