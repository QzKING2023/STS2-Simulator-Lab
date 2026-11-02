using System.Reflection;
using HarmonyLib;

namespace OfflineSearchHarness;

/// <summary>Injects and restores synthetic Harmony patch metadata for a rejection-only audit check.</summary>
internal static class HarmonyPatchMetadataProbe
{
    public static IDisposable Inject(MethodInfo target, string owner)
    {
        if (Harmony.GetPatchInfo(target) is { Owners.Count: > 0 })
            throw new InvalidOperationException("Synthetic patch-metadata check requires an initially unpatched target.");
        Type sharedStateType = typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonySharedState", throwOnError: true)!;
        FieldInfo stateField = sharedStateType.GetField("state", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(sharedStateType.FullName, "state");
        System.Collections.IDictionary state = (System.Collections.IDictionary)(stateField.GetValue(null)
            ?? throw new InvalidOperationException("Harmony shared patch metadata table is not initialized."));
        bool hadPrevious = state.Contains(target);
        object? previous = hadPrevious ? state[target] : null;
        Type patchInfoType = typeof(Harmony).Assembly.GetType("HarmonyLib.PatchInfo", throwOnError: true)!;
        object patchInfo = Activator.CreateInstance(patchInfoType)
            ?? throw new InvalidOperationException("Could not create isolated Harmony patch metadata.");
        MethodInfo prefix = typeof(HarmonyPatchMetadataProbe).GetMethod(nameof(GuardPrefix), BindingFlags.Static | BindingFlags.NonPublic)!;
        Patch fakePrefix = new(prefix, 0, owner, 400, [], [], false);
        patchInfoType.GetField("prefixes", BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(patchInfo, new[] { fakePrefix });
        Type serializationType = typeof(Harmony).Assembly.GetType("HarmonyLib.PatchInfoSerialization", throwOnError: true)!;
        MethodInfo serialize = serializationType.GetMethod("Serialize", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(serializationType.FullName, "Serialize(PatchInfo)");
        byte[] payload = (byte[])(serialize.Invoke(null, [patchInfo])
            ?? throw new InvalidOperationException("Harmony metadata serialization returned null."));
        state[target] = payload;
        try
        {
            if (Harmony.GetPatchInfo(target)?.Owners.Contains(owner) != true)
                throw new InvalidOperationException("Harmony did not expose injected patch metadata.");
            return new Lease(state, target, hadPrevious, previous);
        }
        catch
        {
            if (hadPrevious) state[target] = previous;
            else state.Remove(target);
            throw;
        }
    }

    private static void GuardPrefix() { }

    private sealed class Lease(System.Collections.IDictionary state, MethodInfo target,
        bool hadPrevious, object? previous) : IDisposable
    {
        public void Dispose()
        {
            if (hadPrevious) state[target] = previous;
            else state.Remove(target);
        }
    }
}
