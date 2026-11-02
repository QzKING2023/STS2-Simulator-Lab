param(
    [Parameter(Mandatory = $true)]
    [string]$TargetRoot
)

$ErrorActionPreference = 'Stop'
$mirrorPath = Join-Path $TargetRoot 'src\Prediction\AdaptedPowerAfterDamageReceivedMirrors.cs'
$hookPath = Join-Path $TargetRoot 'src\Engine\InCombat\Mirrors\Hooks\Damage\AfterDamageReceivedMirrors.cs'
$onPlayPath = Join-Path $TargetRoot 'src\Prediction\AdaptedCardOnPlayMirrors.cs'

if (-not (Test-Path -LiteralPath $mirrorPath)) {
    $mirror = @'
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.Damage;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace CombatSolver;

/// <summary>Exact, no-patch third-party Power.AfterDamageReceived adapter with root-stamped provenance.</summary>
internal static class AdaptedPowerAfterDamageReceivedMirrors
{
    private sealed record Registration(MethodInfo Target, string Schema, string SourceIdentity,
        string Implementation, Action<AbstractModel, AfterDamageReceivedMirrorContext> Handler);

    private static readonly object Gate = new();
    private static readonly Dictionary<Type, Registration> Registrations = [];
    private static bool _sealed;

    internal static void RegisterUnpatched(Type powerType, string schema, MethodInfo target,
        Assembly sourceAssembly, string modManifestJson,
        Action<AbstractModel, AfterDamageReceivedMirrorContext> handler)
    {
        ArgumentNullException.ThrowIfNull(powerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(sourceAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(modManifestJson);
        ArgumentNullException.ThrowIfNull(handler);
        if (powerType.IsAbstract || !typeof(PowerModel).IsAssignableFrom(powerType)
            || target != Resolve(powerType) || target.ReturnType != typeof(Task))
            throw new ArgumentException("The target must be the exact Power type's AfterDamageReceived implementation.");
        string sourceIdentity = DescribeSource(powerType, sourceAssembly, modManifestJson);
        RejectPatched(target);
        lock (Gate)
        {
            if (_sealed) throw new InvalidOperationException("Power hook registration is closed after first root capture.");
            Registrations.Add(powerType, new(target, schema, sourceIdentity, MethodIdentity(handler.Method), handler));
        }
    }

    internal static bool Invoke(AbstractModel listener, AfterDamageReceivedMirrorContext context)
    {
        if (!Registrations.TryGetValue(listener.GetType(), out Registration? registration)) return false;
        if (registration.Target != Resolve(listener.GetType()))
            throw new PredictionUnsupportedException($"AfterDamageReceived target changed for {listener.GetType().FullName}.");
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
                Field(text, "unpatched/power-hook/v1/" + registration.SourceIdentity);
                Field(text, registration.Implementation);
            }
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
        }
    }

    private static MethodInfo? Resolve(Type type)
    {
        Type[] parameters =
        [
            typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext),
            typeof(MegaCrit.Sts2.Core.Entities.Creatures.Creature),
            typeof(MegaCrit.Sts2.Core.Entities.Creatures.DamageResult),
            typeof(MegaCrit.Sts2.Core.ValueProps.ValueProp),
            typeof(MegaCrit.Sts2.Core.Entities.Creatures.Creature),
            typeof(MegaCrit.Sts2.Core.Models.CardModel)
        ];
        for (Type? current = type; current != null && current != typeof(AbstractModel); current = current.BaseType)
        {
            MethodInfo? declared = current.GetMethod("AfterDamageReceived",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                binder: null, types: parameters, modifiers: null);
            if (declared != null) return declared;
        }
        return null;
    }

    private static void RejectPatched(MethodInfo target)
    {
        if (Harmony.GetPatchInfo(target) is { Owners.Count: > 0 })
            throw new PredictionUnsupportedException($"AfterDamageReceived for {target.DeclaringType?.FullName} has unreviewed Harmony patches.");
    }

    private static string DescribeSource(Type powerType, Assembly sourceAssembly, string manifestJson)
    {
        if (powerType.Assembly != sourceAssembly || powerType.GetMethod("AfterDamageReceived",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.DeclaringType?.Assembly != sourceAssembly)
            throw new ArgumentException("The power and exact hook implementation must come from the declared Mod assembly.", nameof(sourceAssembly));
        using JsonDocument document = JsonDocument.Parse(manifestJson);
        JsonElement root = document.RootElement;
        string id = Required(root, "id");
        string name = Required(root, "name");
        string version = Required(root, "version");
        string assemblyName = sourceAssembly.GetName().Name
            ?? throw new ArgumentException("The Mod assembly has no simple name.", nameof(sourceAssembly));
        if (!StringComparer.Ordinal.Equals(id, assemblyName))
            throw new ArgumentException("The Mod manifest ID must match the source assembly name.", nameof(manifestJson));
        if (!root.TryGetProperty("affects_gameplay", out JsonElement gameplay) || gameplay.ValueKind != JsonValueKind.True)
            throw new ArgumentException("The hook adapter requires a gameplay Mod manifest.", nameof(manifestJson));
        StringBuilder identity = new();
        Field(identity, "mod-manifest/unpatched-power-hook/v1");
        Field(identity, id);
        Field(identity, name);
        Field(identity, version);
        Field(identity, sourceAssembly.FullName ?? assemblyName);
        Field(identity, sourceAssembly.ManifestModule.ModuleVersionId.ToString("N"));
        Field(identity, powerType.AssemblyQualifiedName ?? powerType.FullName!);
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
        => $"{method.Module.ModuleVersionId:N}/{method.MetadataToken}/{method.DeclaringType?.AssemblyQualifiedName}";

    private static void Field(StringBuilder text, string value)
        => text.Append(value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(':').Append(value);
}
'@
    [IO.File]::WriteAllText($mirrorPath, $mirror, [Text.UTF8Encoding]::new($false))
}

$hook = [IO.File]::ReadAllText($hookPath, [Text.Encoding]::UTF8)
$invoke = 'if (AdaptedPowerAfterDamageReceivedMirrors.Invoke(listener, context)) return;'
if (-not $hook.Contains($invoke, [StringComparison]::Ordinal)) {
    $anchor = @'
    public static void Invoke(AbstractModel listener, AfterDamageReceivedMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }
'@
    $replacement = @'
    public static void Invoke(AbstractModel listener, AfterDamageReceivedMirrorContext context)
    {
        if (AdaptedPowerAfterDamageReceivedMirrors.Invoke(listener, context)) return;
        Registry.Invoke(listener, context);
    }
'@
    if (-not $hook.Contains($anchor, [StringComparison]::Ordinal)) {
        throw "AfterDamageReceived mirror insertion anchor was missing: $hookPath"
    }
    $hook = $hook.Replace($anchor, $replacement)
    [IO.File]::WriteAllText($hookPath, $hook, [Text.UTF8Encoding]::new($false))
}

$onPlay = [IO.File]::ReadAllText($onPlayPath, [Text.Encoding]::UTF8)
$stamp = 'Field(text, AdaptedPowerAfterDamageReceivedMirrors.CaptureLiveStamp());'
if (-not $onPlay.Contains($stamp, [StringComparison]::Ordinal)) {
    $anchor = '        Field(text, AdaptedCardResultLocationMirrors.CaptureLiveStamp());'
    if (-not $onPlay.Contains($anchor, [StringComparison]::Ordinal)) {
        throw "Root-stamp insertion anchor for Power hooks was missing: $onPlayPath"
    }
    $onPlay = $onPlay.Replace($anchor, $anchor + "`r`n        $stamp")
    [IO.File]::WriteAllText($onPlayPath, $onPlay, [Text.UTF8Encoding]::new($false))
}
