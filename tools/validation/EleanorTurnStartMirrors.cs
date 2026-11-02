using System.Reflection;
using CombatSolver;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Simulation;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SimulatorLab.Core;

namespace OfflineSearchHarness;

/// <summary>Lab adapter for the pinned CompleteCrystalOrb turn-start and draw hooks.</summary>
internal static class EleanorTurnStartMirrors
{
    private const string GuardOwner = "simulatorlab.eleanor.turn-start-registration-guard";
    private static Type? _completeCrystalOrbType;
    private static Type? _manaStateType;
    private static Assembly? _modAssembly;
    private static string? _manifestJson;
    private static MethodInfo? _beforeTarget;
    private static MethodInfo? _modifyTarget;
    private static MethodInfo? _completeGetter;
    private static int _beforeHandDrawCalls;
    private static int _modifyHandDrawCalls;
    private static int _lastBeforeHandDrawEnergy = int.MinValue;
    private static string? _lastBeforeHandDrawRelic;
    private static string? _lastBeforeHandDrawDetail;

    public static object? GuardEvidence { get; private set; }
    public static int BeforeHandDrawCalls => Volatile.Read(ref _beforeHandDrawCalls);
    public static int ModifyHandDrawCalls => Volatile.Read(ref _modifyHandDrawCalls);
    public static int LastBeforeHandDrawEnergy => Volatile.Read(ref _lastBeforeHandDrawEnergy);
    public static string? LastBeforeHandDrawRelic => Volatile.Read(ref _lastBeforeHandDrawRelic);
    public static string? LastBeforeHandDrawDetail => Volatile.Read(ref _lastBeforeHandDrawDetail);

    public static void Register(Assembly mod, string manifestJson, Type manaStateType)
    {
        if (manaStateType.Assembly != mod)
            throw new InvalidOperationException("The Mentor rule state must come from the registered MyMod assembly.");
        ValidateRuleMethods(manaStateType);
        _manaStateType = manaStateType;
        _modAssembly = mod;
        _manifestJson = manifestJson;
        Type relicType = mod.GetType("MyMod.MyModCode.Relics.CompleteCrystalOrb", throwOnError: true)!;
        Type baseRelicType = mod.GetType("MyMod.MyModCode.Relics.CrystalOrb", throwOnError: true)!;
        MethodInfo beforeTarget = ResolveBeforeHandDraw(relicType);
        MethodInfo modifyTarget = ResolveModifyHandDraw(relicType);
        _beforeTarget = beforeTarget;
        _modifyTarget = modifyTarget;
        PropertyInfo complete = baseRelicType.GetProperty("Complete",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(baseRelicType.FullName, "Complete");
        _completeGetter = complete.GetMethod
            ?? throw new MissingMethodException(baseRelicType.FullName, "get_Complete");
        if (_completeGetter.ReturnType != typeof(bool) || _completeGetter.DeclaringType != baseRelicType)
            throw new InvalidOperationException("Pinned CrystalOrb.Complete getter identity changed.");
        _completeCrystalOrbType = relicType;

        string wrongManifest = "{\"id\":\"not-MyMod\",\"name\":\"guard probe\",\"version\":\"0\",\"affects_gameplay\":true}";
        MethodInfo wrongTarget = ResolveBeforeHandDraw(typeof(MegaCrit.Sts2.Core.Models.Relics.BloodVial));
        bool wrongTargetRejected = Rejects(() => AdaptedRelicBeforeHandDrawMirrors.RegisterUnpatched(
            relicType, "eleanor-turn-start-guard/wrong-target", wrongTarget, modifyTarget,
            mod, manifestJson, BeforeHandDraw, ModifyHandDraw));
        bool wrongAssemblyRejected = Rejects(() => AdaptedRelicBeforeHandDrawMirrors.RegisterUnpatched(
            relicType, "eleanor-turn-start-guard/wrong-assembly", beforeTarget, modifyTarget,
            typeof(EleanorTurnStartMirrors).Assembly, manifestJson, BeforeHandDraw, ModifyHandDraw));
        bool wrongManifestRejected = Rejects(() => AdaptedRelicBeforeHandDrawMirrors.RegisterUnpatched(
            relicType, "eleanor-turn-start-guard/wrong-manifest", beforeTarget, modifyTarget,
            mod, wrongManifest, BeforeHandDraw, ModifyHandDraw));

        bool existingPatchRejected;
        using (InjectHarmonyPatchMetadata(modifyTarget, GuardOwner))
        {
            existingPatchRejected = Rejects(() => AdaptedRelicBeforeHandDrawMirrors.RegisterUnpatched(
                relicType, "eleanor-turn-start-guard/existing-patch", beforeTarget, modifyTarget,
                mod, manifestJson, BeforeHandDraw, ModifyHandDraw));
        }

        if (!wrongTargetRejected || !wrongAssemblyRejected || !wrongManifestRejected || !existingPatchRejected)
            throw new InvalidOperationException("CompleteCrystalOrb turn-start adapter rejected an invalid identity/patch control.");

        AdaptedRelicBeforeHandDrawMirrors.RegisterUnpatched(relicType,
            "eleanor-complete-orb-turn-start/unpatched/v1", beforeTarget, modifyTarget,
            mod, manifestJson, BeforeHandDraw, ModifyHandDraw);
        GuardEvidence = new
        {
            relicType = relicType.FullName,
            beforeTarget = beforeTarget.ToString(),
            modifyTarget = modifyTarget.ToString(),
            sourceAssembly = mod.FullName,
            wrongTargetRejected,
            wrongAssemblyRejected,
            wrongManifestRejected,
            existingPatchRejected,
            branchState = "PredictionStateStore keyed by the CompleteCrystalOrb model; state is IPredictionStateForkable",
        };
    }

    public static bool RejectLateRegistration()
    {
        Type relicType = _completeCrystalOrbType
            ?? throw new InvalidOperationException("Eleanor turn-start registration was not initialized.");
        Assembly sourceAssembly = _modAssembly
            ?? throw new InvalidOperationException("Eleanor turn-start registration was not initialized.");
        string manifestJson = _manifestJson
            ?? throw new InvalidOperationException("Eleanor turn-start registration was not initialized.");
        MethodInfo beforeTarget = _beforeTarget
            ?? throw new InvalidOperationException("Eleanor turn-start registration was not initialized.");
        MethodInfo modifyTarget = _modifyTarget
            ?? throw new InvalidOperationException("Eleanor turn-start registration was not initialized.");
        return Rejects(() => AdaptedRelicBeforeHandDrawMirrors.RegisterUnpatched(
            relicType, "eleanor-turn-start-guard/late-registration", beforeTarget, modifyTarget,
            sourceAssembly, manifestJson, BeforeHandDraw, ModifyHandDraw), typeof(InvalidOperationException));
    }

    public static MethodInfo RegisteredBeforeTarget => _beforeTarget
        ?? throw new InvalidOperationException("Eleanor turn-start registration was not initialized.");

    public static MethodInfo RegisteredModifyTarget => _modifyTarget
        ?? throw new InvalidOperationException("Eleanor turn-start registration was not initialized.");

    public static IDisposable InjectHarmonyPatchMetadata(MethodInfo target, string owner)
    {
        if (Harmony.GetPatchInfo(target) is { Owners.Count: > 0 })
            throw new InvalidOperationException("The isolated Harmony patch-metadata guard requires an initially unpatched target.");
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
        MethodInfo prefix = typeof(EleanorTurnStartMirrors).GetMethod(
            nameof(GuardPrefix), BindingFlags.Static | BindingFlags.NonPublic)!;
        Patch fakePrefix = new(prefix, 0, owner, 400, [], [], false);
        patchInfoType.GetField("prefixes", BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(patchInfo, new[] { fakePrefix });
        Type serializationType = typeof(Harmony).Assembly.GetType("HarmonyLib.PatchInfoSerialization", throwOnError: true)!;
        MethodInfo serialize = serializationType.GetMethod("Serialize", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(serializationType.FullName, "Serialize(PatchInfo)");
        byte[] payload = (byte[])(serialize.Invoke(null, [patchInfo])
            ?? throw new InvalidOperationException("Harmony patch metadata serialization returned null."));
        state[target] = payload;
        try
        {
            if (Harmony.GetPatchInfo(target)?.Owners.Contains(owner) != true)
                throw new InvalidOperationException("Harmony did not expose the isolated patch metadata through GetPatchInfo.");
            return new PatchMetadataLease(state, target, hadPrevious, previous);
        }
        catch
        {
            if (hadPrevious) state[target] = previous;
            else state.Remove(target);
            throw;
        }
    }

    private static void BeforeHandDraw(RelicModel relic, RelicBeforeHandDrawMirrorContext context)
    {
        Interlocked.Increment(ref _beforeHandDrawCalls);
        Volatile.Write(ref _lastBeforeHandDrawEnergy,
            context.Simulator.State.GetPlayerCombatState(context.Player).Energy);
        Volatile.Write(ref _lastBeforeHandDrawRelic, relic.GetType().AssemblyQualifiedName);
        if (_completeCrystalOrbType is null || relic.GetType() != _completeCrystalOrbType || _completeGetter is null)
            throw new PredictionUnsupportedException("Eleanor turn-start adapter received a different relic runtime type.");
        bool completeOrb = (bool)(_completeGetter.Invoke(relic, null)
            ?? throw new PredictionUnsupportedException("CrystalOrb.Complete returned no value."));
        EleanorManaPredictionState state = GetBranchState(context, relic);
        EleanorManaSnapshot before = state.Capture();
        if (before.Stance == "Reverse")
            throw new PredictionUnsupportedException("ReversePowerLedger is not mirrored in this branch; refusing Reverse upkeep.");

        SimPlayerCombatState playerState = context.Simulator.State.GetPlayerCombatState(context.Player);
        int energyBefore = playerState.Energy;
        EleanorStartTurnOutcome outcome = state.StartTurn(playerState.Energy, completeOrb);
        if (outcome.ManaPaid != 0 || outcome.HpLoss != 0)
            throw new PredictionUnsupportedException("This adapter does not yet mirror upkeep mana-payment or failed-upkeep damage side effects.");
        playerState.LoseEnergy(outcome.EnergyPaid);
        playerState.GainEnergy(outcome.OrbEnergy);
        EleanorManaSnapshot after = state.Capture();
        Volatile.Write(ref _lastBeforeHandDrawDetail,
            $"completeOrb={completeOrb};before={before};outcome={outcome};after={after};energy={energyBefore}->{playerState.Energy}");
    }

    private static decimal ModifyHandDraw(RelicModel relic,
        RelicBeforeHandDrawMirrorContext context, decimal count)
    {
        Interlocked.Increment(ref _modifyHandDrawCalls);
        if (_completeCrystalOrbType is null || relic.GetType() != _completeCrystalOrbType)
            throw new PredictionUnsupportedException("Eleanor draw adapter received a different relic runtime type.");
        EleanorManaPredictionState state = GetBranchState(context, relic);
        return count + state.Capture().StartDrawBonus;
    }

    private static EleanorManaPredictionState GetBranchState(
        RelicBeforeHandDrawMirrorContext context, RelicModel dispatchedRelic)
    {
        Type expectedType = _completeCrystalOrbType
            ?? throw new InvalidOperationException("CompleteCrystalOrb turn-start adapter is not registered.");
        if (dispatchedRelic.GetType() != expectedType)
            throw new PredictionUnsupportedException("Eleanor turn-start adapter received a different relic runtime type.");
        // Card mirrors key the forked extension to the owner's stable root RelicModel reference.
        // SimulatedCombatState can dispatch a structurally equivalent relic instance; use the
        // same identity key while still reading and mutating only this simulator's StateStore.
        RelicModel stateKey = context.Player.Relics.SingleOrDefault(relic => relic.GetType() == expectedType)
            ?? throw new PredictionUnsupportedException("The owning Player has no exact CompleteCrystalOrb state key.");
        return EleanorCardMirrors.GetState(context.Simulator, stateKey);
    }

    private static MethodInfo ResolveBeforeHandDraw(Type relicType)
        => AccessTools.Method(relicType, "BeforeHandDraw",
            [typeof(Player), typeof(PlayerChoiceContext), typeof(ICombatState)])
            ?? throw new MissingMethodException(relicType.FullName, "BeforeHandDraw");

    private static MethodInfo ResolveModifyHandDraw(Type relicType)
        => AccessTools.Method(relicType, "ModifyHandDraw", [typeof(Player), typeof(decimal)])
            ?? throw new MissingMethodException(relicType.FullName, "ModifyHandDraw");

    private static void ValidateRuleMethods(Type stateType)
    {
        MethodInfo setCompleteOrb = stateType.GetMethod("SetCompleteOrb", [typeof(bool)])
            ?? throw new MissingMethodException(stateType.FullName, "SetCompleteOrb(bool)");
        MethodInfo payUpkeep = stateType.GetMethod("PayUpkeep", [typeof(int)])
            ?? throw new MissingMethodException(stateType.FullName, "PayUpkeep(int)");
        MethodInfo grantOrbStart = stateType.GetMethod("GrantOrbStart", Type.EmptyTypes)
            ?? throw new MissingMethodException(stateType.FullName, "GrantOrbStart()");
        if (setCompleteOrb.ReturnType != typeof(void)
            || !HasIntProperties(payUpkeep.ReturnType, "EnergyPaid", "ManaPaid", "HpLoss")
            || !HasIntProperties(grantOrbStart.ReturnType, "Energy", "Mana"))
            throw new PredictionUnsupportedException("Pinned ManaState turn-start method contract changed.");
    }

    private static bool HasIntProperties(Type type, params string[] names)
        => names.All(name => type.GetProperty(name)?.PropertyType == typeof(int));

    private static bool Rejects(Action action, Type? expectedType = null)
    {
        try
        {
            action();
            return false;
        }
        catch (Exception error) when (expectedType is null
            ? error is ArgumentException
            : expectedType.IsAssignableFrom(error.GetType()))
        {
            return true;
        }
    }

    private static void GuardPrefix() { }

    private sealed class PatchMetadataLease(System.Collections.IDictionary state, MethodInfo target,
        bool hadPrevious, object? previous) : IDisposable
    {
        public void Dispose()
        {
            if (hadPrevious) state[target] = previous;
            else state.Remove(target);
        }
    }
}
