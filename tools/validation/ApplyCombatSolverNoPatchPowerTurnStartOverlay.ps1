param(
    [Parameter(Mandatory = $true)]
    [string]$TargetRoot
)

$ErrorActionPreference = 'Stop'
$mirrorPath = Join-Path $TargetRoot 'src\Prediction\AdaptedPowerBeforeSideTurnStartMirrors.cs'
$hookPath = Join-Path $TargetRoot 'src\Engine\InCombat\Mirrors\Hooks\TurnStart\BeforeSideTurnStartMirrors.cs'
$onPlayPath = Join-Path $TargetRoot 'src\Prediction\AdaptedCardOnPlayMirrors.cs'

$mirror = @'
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.TurnStart;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CombatSolver;

/// <summary>Audited no-patch adapter for one concrete Mod power's BeforeSideTurnStart hook.</summary>
internal static class AdaptedPowerBeforeSideTurnStartMirrors
{
    private sealed record Registration(Type PowerType, MethodInfo Target, string Schema, string SourceIdentity,
        string Implementation, Action<AbstractModel, BeforeSideTurnStartMirrorContext> Handler);

    private static readonly object Gate = new();
    private static readonly Dictionary<Type, Registration> Registrations = [];
    private static bool _sealed;

    internal static void RegisterUnpatched(Type powerType, string schema, MethodInfo target,
        Assembly sourceAssembly, string modManifestJson,
        Action<AbstractModel, BeforeSideTurnStartMirrorContext> handler)
    {
        ArgumentNullException.ThrowIfNull(powerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(sourceAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(modManifestJson);
        ArgumentNullException.ThrowIfNull(handler);
        if (powerType.IsAbstract || !typeof(PowerModel).IsAssignableFrom(powerType)
            || powerType.Assembly != sourceAssembly || target != Resolve(powerType)
            || target.ReturnType != typeof(Task))
            throw new ArgumentException("The target must be the exact concrete Power type's BeforeSideTurnStart implementation.");

        string sourceIdentity = DescribeSource(powerType, sourceAssembly, modManifestJson);
        RejectPatched(target);
        lock (Gate)
        {
            if (_sealed) throw new InvalidOperationException("Power turn-start registration is closed after first root capture.");
            Registrations.Add(powerType, new(powerType, target, schema, sourceIdentity,
                MethodIdentity(handler.Method), handler));
        }
    }

    internal static bool Invoke(AbstractModel listener, BeforeSideTurnStartMirrorContext context)
    {
        if (!Registrations.TryGetValue(listener.GetType(), out Registration? registration)) return false;
        if (registration.Target != Resolve(listener.GetType()))
            throw new PredictionUnsupportedException($"BeforeSideTurnStart target changed for {listener.GetType().FullName}.");
        RejectPatched(registration.Target);
        registration.Handler(listener, context);
        return true;
    }

    internal static string CaptureLiveStamp()
    {
        lock (Gate)
        {
            _sealed = true;
            if (Registrations.Count == 0) return string.Empty;
            StringBuilder text = new();
            foreach (var pair in Registrations.OrderBy(pair => pair.Key.AssemblyQualifiedName, StringComparer.Ordinal))
            {
                Registration registration = pair.Value;
                RejectPatched(registration.Target);
                Field(text, pair.Key.AssemblyQualifiedName!);
                Field(text, MethodIdentity(registration.Target));
                Field(text, registration.Schema);
                Field(text, "unpatched/power-before-side-turn-start/v1/" + registration.SourceIdentity);
                Field(text, registration.Implementation);
            }
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
        }
    }

    private static MethodInfo? Resolve(Type type)
    {
        Type[] parameters = [typeof(PlayerChoiceContext), typeof(CombatSide), typeof(IReadOnlyList<Creature>), typeof(ICombatState)];
        for (Type? current = type; current != null && current != typeof(AbstractModel); current = current.BaseType)
        {
            MethodInfo? declared = current.GetMethod("BeforeSideTurnStart",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                binder: null, types: parameters, modifiers: null);
            if (declared != null) return declared;
        }
        return null;
    }

    private static void RejectPatched(MethodInfo target)
    {
        if (Harmony.GetPatchInfo(target) is { Owners.Count: > 0 })
            throw new PredictionUnsupportedException($"BeforeSideTurnStart for {target.DeclaringType?.FullName} has unreviewed Harmony patches.");
        Type? stateMachine = target.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
        if (stateMachine != null)
        {
            MethodInfo moveNext = AccessTools.Method(stateMachine, "MoveNext")
                ?? throw new PredictionUnsupportedException("A registered async turn-start hook has no MoveNext method.");
            if (Harmony.GetPatchInfo(moveNext) is { Owners.Count: > 0 })
                throw new PredictionUnsupportedException("A patched async turn-start state machine is unsupported.");
        }
    }

    private static string DescribeSource(Type powerType, Assembly sourceAssembly, string manifestJson)
    {
        using JsonDocument document = JsonDocument.Parse(manifestJson);
        JsonElement root = document.RootElement;
        string id = Required(root, "id");
        string name = Required(root, "name");
        string version = Required(root, "version");
        string assemblyName = sourceAssembly.GetName().Name
            ?? throw new ArgumentException("The Mod assembly has no simple name.");
        if (!StringComparer.Ordinal.Equals(id, assemblyName))
            throw new ArgumentException("The Mod manifest ID must match the source assembly name.", nameof(manifestJson));
        if (!root.TryGetProperty("affects_gameplay", out JsonElement gameplay) || gameplay.ValueKind != JsonValueKind.True)
            throw new ArgumentException("The hook adapter requires a gameplay Mod manifest.", nameof(manifestJson));
        StringBuilder identity = new();
        Field(identity, "mod-manifest/unpatched-power-before-side-turn-start/v1");
        Field(identity, id);
        Field(identity, name);
        Field(identity, version);
        Field(identity, sourceAssembly.FullName ?? assemblyName);
        Field(identity, sourceAssembly.ManifestModule.ModuleVersionId.ToString("N"));
        Field(identity, powerType.AssemblyQualifiedName ?? powerType.FullName!);
        Field(identity, MethodIdentity(Resolve(powerType)!));
        Field(identity, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifestJson))));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToString()))).ToLowerInvariant();
    }

    private static string Required(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out JsonElement value) || value.ValueKind != JsonValueKind.String
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
        string signature = method.GetParameters().Length == 0 ? "()"
            : "(" + string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.AssemblyQualifiedName)) + ")";
        return $"{moduleId}/{token}/{method.DeclaringType?.AssemblyQualifiedName}/{method.Name}{signature}";
    }

    private static void Field(StringBuilder text, string value)
        => text.Append(value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(':').Append(value);
}
'@
if (Test-Path -LiteralPath $mirrorPath) { throw 'The ignored CombatSolver copy already contains the turn-start power adapter; use a fresh local build cache.' }
[IO.File]::WriteAllText($mirrorPath, $mirror, [Text.UTF8Encoding]::new($false))

$hook = [IO.File]::ReadAllText($hookPath, [Text.Encoding]::UTF8)
$anchor = @'
    internal static void Invoke(AbstractModel listener, BeforeSideTurnStartMirrorContext context)
    {
        Seal();
'@
$replacement = @'
    internal static void Invoke(AbstractModel listener, BeforeSideTurnStartMirrorContext context)
    {
        Seal();
        if (AdaptedPowerBeforeSideTurnStartMirrors.Invoke(listener, context)) return;
'@
if (-not $hook.Contains($replacement, [StringComparison]::Ordinal)) {
    if (-not $hook.Contains($anchor, [StringComparison]::Ordinal)) { throw 'BeforeSideTurnStart hook insertion anchor missing.' }
    $hook = $hook.Replace($anchor, $replacement)
    [IO.File]::WriteAllText($hookPath, $hook, [Text.UTF8Encoding]::new($false))
}

$onPlay = [IO.File]::ReadAllText($onPlayPath, [Text.Encoding]::UTF8)
$stamp = 'Field(text, AdaptedPowerBeforeSideTurnStartMirrors.CaptureLiveStamp());'
if (-not $onPlay.Contains($stamp, [StringComparison]::Ordinal)) {
    $anchor = '        Field(text, AdaptedPowerAfterDamageReceivedMirrors.CaptureLiveStamp());'
    if (-not $onPlay.Contains($anchor, [StringComparison]::Ordinal)) { throw 'Power hook root-stamp anchor missing.' }
    $onPlay = $onPlay.Replace($anchor, $anchor + "`r`n        $stamp")
    [IO.File]::WriteAllText($onPlayPath, $onPlay, [Text.UTF8Encoding]::new($false))
}
