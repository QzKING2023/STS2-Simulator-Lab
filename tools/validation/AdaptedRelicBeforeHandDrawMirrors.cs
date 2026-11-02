using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Simulation;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CombatSolver;

/// <summary>Branch-local context for an audited Relic.BeforeHandDraw adapter.</summary>
internal sealed class RelicBeforeHandDrawMirrorContext(
    CombatPredictionSimulator simulator, Player player)
{
    public CombatPredictionSimulator Simulator { get; } = simulator;
    public Player Player { get; } = player;
}

/// <summary>
/// Exact-type, no-patch Relic.BeforeHandDraw and ModifyHandDraw adapters for isolated simulation branches.
/// Registered target, source, handlers, and observed Harmony state contribute to the root stamp.
/// </summary>
internal static class AdaptedRelicBeforeHandDrawMirrors
{
    private sealed record Registration(Type RelicType, string Schema,
        MethodInfo BeforeTarget, MethodInfo ModifyTarget, string SourceIdentity,
        string BeforeImplementation, string ModifyImplementation,
        Action<RelicModel, RelicBeforeHandDrawMirrorContext> BeforeHandler,
        Func<RelicModel, RelicBeforeHandDrawMirrorContext, decimal, decimal> ModifyHandler);

    private static readonly object Gate = new();
    private static readonly Dictionary<Type, Registration> Registrations = [];
    private static bool _sealed;

    public static void RegisterUnpatched(Type relicType, string schema,
        MethodInfo beforeTarget, MethodInfo modifyTarget, Assembly sourceAssembly,
        string modManifestJson,
        Action<RelicModel, RelicBeforeHandDrawMirrorContext> beforeHandler,
        Func<RelicModel, RelicBeforeHandDrawMirrorContext, decimal, decimal> modifyHandler)
    {
        ArgumentNullException.ThrowIfNull(relicType);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentNullException.ThrowIfNull(beforeTarget);
        ArgumentNullException.ThrowIfNull(modifyTarget);
        ArgumentNullException.ThrowIfNull(sourceAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(modManifestJson);
        ArgumentNullException.ThrowIfNull(beforeHandler);
        ArgumentNullException.ThrowIfNull(modifyHandler);
        if (!typeof(RelicModel).IsAssignableFrom(relicType) || relicType.IsAbstract)
            throw new ArgumentException("The adapter must target an exact concrete RelicModel type.", nameof(relicType));
        if (relicType.Assembly != sourceAssembly)
            throw new ArgumentException("The exact relic type must come from the declared Mod assembly.", nameof(sourceAssembly));
        if (beforeTarget != ResolveBeforeHandDraw(relicType) || beforeTarget.ReturnType != typeof(Task))
            throw new ArgumentException("The target must be the exact relic type's BeforeHandDraw implementation.", nameof(beforeTarget));
        if (modifyTarget != ResolveModifyHandDraw(relicType) || modifyTarget.ReturnType != typeof(decimal))
            throw new ArgumentException("The target must be the exact relic type's ModifyHandDraw implementation.", nameof(modifyTarget));

        string sourceIdentity = DescribeSource(relicType, sourceAssembly, modManifestJson);
        RejectPatchedStateMachine(beforeTarget);
        RejectPatchedStateMachine(modifyTarget);
        RejectPatches(beforeTarget);
        RejectPatches(modifyTarget);
        lock (Gate)
        {
            if (_sealed)
                throw new InvalidOperationException("Relic turn-start registration is closed after first capture.");
            Registrations.Add(relicType, new(relicType, schema, beforeTarget, modifyTarget, sourceIdentity,
                MethodIdentity(beforeHandler.Method), MethodIdentity(modifyHandler.Method), beforeHandler, modifyHandler));
        }
    }

    internal static void InvokeBeforeHandDraw(CombatPredictionSimulator simulator,
        Player player, RelicModel relic)
    {
        if (!TryGetCurrent(relic, out Registration? registration))
            return;
        if (ReferenceEquals(relic.Owner, player) && !relic.IsMelted)
            registration!.BeforeHandler(relic, new(simulator, player));
    }

    internal static bool TryModifyHandDraw(SimulatedCombatState combat,
        RelicModel relic, Player player, decimal count, out decimal modified)
    {
        modified = count;
        if (!TryGetCurrent(relic, out Registration? registration))
            return false;
        if (!ReferenceEquals(relic.Owner, player) || relic.IsMelted)
            return false;
        modified = registration!.ModifyHandler(relic, new(combat.PredictionBranchSimulator, player), count);
        return true;
    }

    internal static string? CaptureLiveStamp()
    {
        Registration[] registrations;
        lock (Gate)
        {
            Volatile.Write(ref _sealed, true);
            registrations = Registrations.Values.OrderBy(item => item.RelicType.AssemblyQualifiedName,
                StringComparer.Ordinal).ToArray();
        }
        if (registrations.Length == 0)
            return null;

        StringBuilder text = new();
        foreach (Registration registration in registrations)
        {
            ValidateNoPatches(registration);
            Field(text, registration.RelicType.AssemblyQualifiedName ?? registration.RelicType.FullName!);
            Field(text, MethodIdentity(registration.BeforeTarget));
            Field(text, MethodIdentity(registration.ModifyTarget));
            Field(text, registration.Schema);
            Field(text, registration.SourceIdentity);
            Field(text, registration.BeforeImplementation);
            Field(text, registration.ModifyImplementation);
        }

        foreach (MethodBase method in Harmony.GetAllPatchedMethods()
            .Where(IsTurnStartRelicHook)
            .OrderBy(MethodIdentity, StringComparer.Ordinal))
        {
            string signature = AdaptedCardOnPlayMirrors.DescribeActual(
                method, Harmony.GetPatchInfo(method), includeIndex: true);
            if (signature.Length == 0)
                continue;
            Field(text, MethodIdentity(method));
            Field(text, signature);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static bool TryGetCurrent(RelicModel relic, out Registration? registration)
    {
        lock (Gate)
        {
            if (!Registrations.TryGetValue(relic.GetType(), out registration))
                return false;
        }
        ValidateNoPatches(registration);
        return true;
    }

    private static void ValidateNoPatches(Registration registration)
    {
        if (registration.BeforeTarget != ResolveBeforeHandDraw(registration.RelicType)
            || registration.ModifyTarget != ResolveModifyHandDraw(registration.RelicType))
            throw new PredictionUnsupportedException("A registered Relic turn-start target no longer resolves to the same exact method.");
        RejectPatchedStateMachine(registration.BeforeTarget);
        RejectPatchedStateMachine(registration.ModifyTarget);
        if (Harmony.GetPatchInfo(registration.BeforeTarget) is { Owners.Count: > 0 }
            || Harmony.GetPatchInfo(registration.ModifyTarget) is { Owners.Count: > 0 })
            throw new PredictionUnsupportedException($"A registered Relic turn-start hook for {registration.RelicType.FullName} has a Harmony patch.");
    }

    private static MethodInfo ResolveBeforeHandDraw(Type relicType)
        => AccessTools.Method(relicType, "BeforeHandDraw",
            [typeof(Player), typeof(PlayerChoiceContext), typeof(ICombatState)])
            ?? throw new MissingMethodException(relicType.FullName, "BeforeHandDraw(Player, PlayerChoiceContext, ICombatState)");

    private static MethodInfo ResolveModifyHandDraw(Type relicType)
        => AccessTools.Method(relicType, "ModifyHandDraw", [typeof(Player), typeof(decimal)])
            ?? throw new MissingMethodException(relicType.FullName, "ModifyHandDraw(Player, decimal)");

    private static void RejectPatches(MethodInfo target)
    {
        if (Harmony.GetPatchInfo(target) is { Owners.Count: > 0 })
            throw new ArgumentException($"RegisterUnpatched requires an unpatched target: {target.DeclaringType?.FullName}.{target.Name}.");
    }

    private static void RejectPatchedStateMachine(MethodInfo target)
    {
        Type? stateMachine = target.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
        if (stateMachine is null)
            return;
        MethodInfo moveNext = AccessTools.Method(stateMachine, "MoveNext")
            ?? throw new PredictionUnsupportedException("A registered async Relic turn-start hook has no MoveNext method.");
        if (Harmony.GetPatchInfo(moveNext) is { Owners.Count: > 0 })
            throw new PredictionUnsupportedException("A patched async Relic turn-start state machine is unsupported.");
    }

    private static bool IsTurnStartRelicHook(MethodBase method)
        => method.DeclaringType is { } declaring
            && typeof(RelicModel).IsAssignableFrom(declaring)
            && method.Name is "BeforeHandDraw" or "ModifyHandDraw";

    private static string DescribeSource(Type relicType, Assembly sourceAssembly, string manifestJson)
    {
        using JsonDocument document = JsonDocument.Parse(manifestJson);
        JsonElement root = document.RootElement;
        string id = RequiredString(root, "id");
        string name = RequiredString(root, "name");
        string version = RequiredString(root, "version");
        string assemblyName = sourceAssembly.GetName().Name
            ?? throw new ArgumentException("The Mod assembly has no simple name.", nameof(sourceAssembly));
        if (!StringComparer.Ordinal.Equals(id, assemblyName))
            throw new ArgumentException("The Mod manifest ID must match the source assembly name.", nameof(manifestJson));
        if (!root.TryGetProperty("affects_gameplay", out JsonElement gameplay)
            || gameplay.ValueKind != JsonValueKind.True)
            throw new ArgumentException("The relic hook adapter requires a gameplay Mod manifest.", nameof(manifestJson));
        StringBuilder identity = new();
        Field(identity, "mod-manifest/relic-before-hand-draw/unpatched/v1");
        Field(identity, id);
        Field(identity, name);
        Field(identity, version);
        Field(identity, sourceAssembly.FullName ?? assemblyName);
        Field(identity, sourceAssembly.ManifestModule.ModuleVersionId.ToString("N"));
        Field(identity, relicType.AssemblyQualifiedName ?? relicType.FullName!);
        Field(identity, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifestJson))));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToString()))).ToLowerInvariant();
    }

    private static string RequiredString(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out JsonElement value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
            throw new ArgumentException($"The Mod manifest is missing a non-empty '{property}'.", "modManifestJson");
        return value.GetString()!;
    }

    private static string MethodIdentity(MethodBase method)
    {
        string moduleId;
        string token;
        try { moduleId = method.Module.ModuleVersionId.ToString("N"); }
        catch (InvalidOperationException) { moduleId = method.Module.Assembly.FullName ?? "dynamic-module"; }
        try { token = method.MetadataToken.ToString(System.Globalization.CultureInfo.InvariantCulture); }
        catch (InvalidOperationException) { token = "dynamic-method"; }
        string signature = "(" + string.Join(",", method.GetParameters()
            .Select(parameter => parameter.ParameterType.AssemblyQualifiedName)) + ")";
        return string.Join("|", moduleId, token, method.DeclaringType?.AssemblyQualifiedName ?? "",
            method.Name + signature, method.ToString() ?? method.Name);
    }

    private static void Field(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        target.Append(':').Append(value).Append(';');
    }
}
