param(
    [Parameter(Mandatory = $true)]
    [string]$TargetRoot
)

$ErrorActionPreference = 'Stop'
$mirrorPath = Join-Path $TargetRoot 'src\Prediction\AdaptedCardResultLocationMirrors.cs'
$callsitePath = Join-Path $TargetRoot 'src\Engine\InCombat\Mirrors\Cards\CardResultLocationMirrors.cs'
$onPlayPath = Join-Path $TargetRoot 'src\Prediction\AdaptedCardOnPlayMirrors.cs'
$simCreaturePath = Join-Path $TargetRoot 'src\Engine\InCombat\Simulation\SimCreatureState.cs'

if (-not (Test-Path -LiteralPath $mirrorPath)) {
    $mirror = @'
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CombatSolver.Engine.Common;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace CombatSolver;

/// <summary>Strict provenance gate for gameplay Mod result-location mirrors with no Harmony patch.</summary>
public static class AdaptedCardResultLocationMirrors
{
    private sealed record Registration(MethodInfo Target, string Schema, string SourceIdentity,
        string Implementation, Func<CardModel, CardLocation, CardLocation> Handler);

    private static readonly object Gate = new();
    private static readonly Dictionary<Type, Registration> Registrations = [];
    private static bool _sealed;

    public static void RegisterUnpatched<TCard>(string schema, MethodInfo target, Assembly sourceAssembly,
        string modManifestJson, Func<TCard, CardLocation, CardLocation> handler)
        where TCard : CardModel
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(sourceAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(modManifestJson);
        ArgumentNullException.ThrowIfNull(handler);
        if (typeof(TCard).IsAbstract || target != Resolve(typeof(TCard)) || target.DeclaringType != typeof(TCard))
            throw new ArgumentException("The target must be the exact card type's GetResultLocationForCardPlay implementation.");
        string sourceIdentity = DescribeSource<TCard>(sourceAssembly, modManifestJson);
        RejectPatched(target);
        lock (Gate)
        {
            if (_sealed) throw new InvalidOperationException("Result-location adaptation registration is closed after first root capture.");
            Registrations.Add(typeof(TCard), new(target, schema, sourceIdentity, MethodIdentity(handler.Method),
                (card, location) => handler((TCard)card, location)));
        }
    }

    internal static CardLocation ApplyRegistered(CardModel preview, CardLocation baseResult)
    {
        if (!Registrations.TryGetValue(preview.GetType(), out Registration? registration)) return baseResult;
        MethodInfo target = Resolve(preview.GetType())
            ?? throw new PredictionUnsupportedException($"Missing GetResultLocationForCardPlay for {preview.GetType().FullName}.");
        if (registration.Target != target)
            throw new PredictionUnsupportedException($"Result-location target changed for {preview.GetType().FullName}.");
        RejectPatched(target);
        return registration.Handler(preview, baseResult);
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
                Field(text, "unpatched/result-location/v1/" + registration.SourceIdentity);
                Field(text, registration.Implementation);
            }
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
        }
    }

    private static MethodInfo? Resolve(Type type)
        => AccessTools.Method(type, "GetResultLocationForCardPlay", Type.EmptyTypes);

    private static void RejectPatched(MethodInfo target)
    {
        if (Harmony.GetPatchInfo(target) is { Owners.Count: > 0 })
            throw new PredictionUnsupportedException($"GetResultLocationForCardPlay for {target.DeclaringType?.FullName} has unreviewed Harmony patches.");
    }

    private static string DescribeSource<TCard>(Assembly sourceAssembly, string manifestJson)
        where TCard : CardModel
    {
        if (typeof(TCard).Assembly != sourceAssembly)
            throw new ArgumentException("The registered card type must come from the declared Mod assembly.", nameof(sourceAssembly));
        using JsonDocument document = JsonDocument.Parse(manifestJson);
        JsonElement root = document.RootElement;
        string id = Required(root, "id");
        string name = Required(root, "name");
        string version = Required(root, "version");
        string assemblyName = sourceAssembly.GetName().Name
            ?? throw new ArgumentException("The source assembly has no simple name.", nameof(sourceAssembly));
        if (!string.Equals(id, assemblyName, StringComparison.Ordinal))
            throw new ArgumentException("The Mod manifest ID must match the source assembly name.", nameof(manifestJson));
        if (!root.TryGetProperty("affects_gameplay", out JsonElement gameplay) || gameplay.ValueKind != JsonValueKind.True)
            throw new ArgumentException("The result-location mirror requires a gameplay Mod manifest.", nameof(manifestJson));
        StringBuilder identity = new();
        Field(identity, "mod-manifest/unpatched-result-location/v1");
        Field(identity, id);
        Field(identity, name);
        Field(identity, version);
        Field(identity, sourceAssembly.FullName ?? assemblyName);
        Field(identity, sourceAssembly.ManifestModule.ModuleVersionId.ToString("N"));
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

$callsite = [IO.File]::ReadAllText($callsitePath, [Text.Encoding]::UTF8)
$oldCall = @'
        return Registry.Invoke(card.Preview, new()
        {
            Simulator = simulator,
            Card = card,
            BaseResult = result
        }, result).Value;
'@
$newCall = @'
        result = Registry.Invoke(card.Preview, new()
        {
            Simulator = simulator,
            Card = card,
            BaseResult = result
        }, result).Value;
        return AdaptedCardResultLocationMirrors.ApplyRegistered(card.Preview, result);
'@
if ($callsite.Contains($oldCall, [StringComparison]::Ordinal)) {
    $callsite = $callsite.Replace($oldCall, $newCall)
    [IO.File]::WriteAllText($callsitePath, $callsite, [Text.UTF8Encoding]::new($false))
} elseif (-not $callsite.Contains('AdaptedCardResultLocationMirrors.ApplyRegistered(card.Preview, result)', [StringComparison]::Ordinal)) {
    throw "CardResultLocationMirrors callsite did not match the pinned CombatSolver source: $callsitePath"
}

$onPlay = [IO.File]::ReadAllText($onPlayPath, [Text.Encoding]::UTF8)
$stampCall = 'Field(text, AdaptedCardResultLocationMirrors.CaptureLiveStamp());'
if (-not $onPlay.Contains($stampCall, [StringComparison]::Ordinal)) {
    $returnAnchor = '        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));'
    $first = $onPlay.IndexOf($returnAnchor, [StringComparison]::Ordinal)
    if ($first -lt 0 -or $onPlay.IndexOf($returnAnchor, $first + $returnAnchor.Length, [StringComparison]::Ordinal) -ge 0) {
        throw "CombatSolver root-stamp anchor was missing or ambiguous: $onPlayPath"
    }
    $onPlay = $onPlay.Insert($first, "        $stampCall`r`n")
    [IO.File]::WriteAllText($onPlayPath, $onPlay, [Text.UTF8Encoding]::new($false))
}

$simCreature = [IO.File]::ReadAllText($simCreaturePath, [Text.Encoding]::UTF8)
$loseBlock = @'
    public void LoseBlock(decimal amount)
    {
        if (amount < 0m)
        {
            throw new ArgumentException("amount must be nonnegative.", nameof(amount));
        }

        Block = Math.Max(Block - (int)Math.Min(amount, int.MaxValue), 0);
    }

'@
$healAnchor = @'
    public void Heal(decimal amount)
'@
if (-not $simCreature.Contains('public void LoseBlock(decimal amount)', [StringComparison]::Ordinal)) {
    $index = $simCreature.IndexOf($healAnchor, [StringComparison]::Ordinal)
    if ($index -lt 0 -or $simCreature.IndexOf($healAnchor, $index + $healAnchor.Length, [StringComparison]::Ordinal) -ge 0) {
        throw "CombatSolver SimCreatureState insertion anchor was missing or ambiguous: $simCreaturePath"
    }
    $simCreature = $simCreature.Insert($index, $loseBlock)
    [IO.File]::WriteAllText($simCreaturePath, $simCreature, [Text.UTF8Encoding]::new($false))
}
