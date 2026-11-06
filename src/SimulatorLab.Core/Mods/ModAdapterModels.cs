using System.Collections.ObjectModel;

namespace SimulatorLab.Core.Mods;

/// <summary>
/// Stable, serializable values that cross from the adapter boundary into the
/// simulator.  No Godot, reflection, or Mod assembly types are represented here.
/// </summary>
public static class ModAdapterConstants
{
    public const string Schema = "mod-adapter/v1";
    public const string TargetGameVersion = ModCatalogConstants.TargetGameVersion;
    public const string AdapterVersion = "mod-adapter/v1";
}

public sealed record ModPackage(
    string ModId,
    string DisplayName,
    string Version,
    string TargetGameVersion,
    IReadOnlyList<ModDependency> Dependencies,
    string SourcePath)
{
    public static ModPackage FromManifest(ModManifest manifest, string targetGameVersion = ModAdapterConstants.TargetGameVersion) =>
        new(manifest.ModId, manifest.DisplayName, manifest.Version, targetGameVersion,
            manifest.Dependencies.OrderBy(x => x.ModId, StringComparer.Ordinal).ToArray(),
            manifest.ManifestRelativePath);
}

public sealed record ResourceResult(
    string ResourceId,
    int Amount,
    string Reason);

public sealed record HookCapability(
    string Id,
    string HookPoint,
    bool Supported,
    string Description,
    string? EvidenceRef = null);

public sealed record CharacterDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<string> StartingCardIds,
    IReadOnlyList<string> StartingRelicIds,
    IReadOnlyList<string> PowerIds);

public sealed record CardDefinition(
    string Id,
    string DisplayName,
    int Cost,
    string Description,
    IReadOnlyList<ResourceResult> ResourceResults,
    IReadOnlyList<HookCapability> Hooks);

public sealed record RelicDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<HookCapability> Hooks,
    IReadOnlyList<ResourceResult> ResourceResults);

public sealed record PowerDefinition(
    string Id,
    string DisplayName,
    int? Duration,
    IReadOnlyList<HookCapability> Hooks,
    IReadOnlyList<ResourceResult> ResourceResults);

public sealed record EnemyDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<string> IntentIds,
    IReadOnlyList<HookCapability> Hooks);

public sealed record EncounterDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<string> EnemyIds);

public sealed record EventDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<string> OptionIds,
    IReadOnlyList<HookCapability> Hooks);

public sealed record RewardDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<string> ContentIds,
    IReadOnlyList<ResourceResult> ResourceResults);

public sealed record ModDefinitionSet(
    ModPackage Package,
    IReadOnlyList<CharacterDefinition> Characters,
    IReadOnlyList<CardDefinition> Cards,
    IReadOnlyList<RelicDefinition> Relics,
    IReadOnlyList<PowerDefinition> Powers,
    IReadOnlyList<EnemyDefinition> Enemies,
    IReadOnlyList<EncounterDefinition> Encounters,
    IReadOnlyList<EventDefinition> Events,
    IReadOnlyList<RewardDefinition> Rewards,
    IReadOnlyList<ResourceResult> ResourceResults,
    IReadOnlyList<HookCapability> HookCapabilities)
{
    public static ModDefinitionSet Empty(ModPackage package) => new(
        package, [], [], [], [], [], [], [], [], [], []);

    public int DefinitionCount => Characters.Count + Cards.Count + Relics.Count + Powers.Count +
        Enemies.Count + Encounters.Count + Events.Count + Rewards.Count;

    public IReadOnlyList<string> AllIds => new ReadOnlyCollection<string>(
        Characters.Select(x => x.Id)
            .Concat(Cards.Select(x => x.Id))
            .Concat(Relics.Select(x => x.Id))
            .Concat(Powers.Select(x => x.Id))
            .Concat(Enemies.Select(x => x.Id))
            .Concat(Encounters.Select(x => x.Id))
            .Concat(Events.Select(x => x.Id))
            .Concat(Rewards.Select(x => x.Id))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray());
}

public sealed record ConversionCounts(
    int Characters,
    int Cards,
    int Relics,
    int Powers,
    int Enemies,
    int Encounters,
    int Events,
    int Rewards,
    int ResourceResults,
    int Hooks)
{
    public static ConversionCounts From(ModDefinitionSet model) => new(
        model.Characters.Count, model.Cards.Count, model.Relics.Count, model.Powers.Count,
        model.Enemies.Count, model.Encounters.Count, model.Events.Count, model.Rewards.Count,
        model.ResourceResults.Count, model.HookCapabilities.Count);
}

public sealed record CompatibilityReport(
    string Schema,
    string AdapterVersion,
    string TargetGameVersion,
    string ModId,
    string SourcePath,
    IReadOnlyList<string> DiscoveredCapabilities,
    IReadOnlyList<string> SupportedCapabilities,
    IReadOnlyList<string> UnsupportedCapabilities,
    ConversionCounts ConversionCounts,
    IReadOnlyList<AdapterErrorRecord> Errors,
    string DeterminismDigest,
    bool RegistryCommitted)
{
    public bool IsSuccess => Errors.Count == 0 && RegistryCommitted;

    public string Summary => AdapterDigest.Compute(new
    {
        Schema,
        AdapterVersion,
        TargetGameVersion,
        ModId,
        SourcePath,
        DiscoveredCapabilities,
        SupportedCapabilities,
        UnsupportedCapabilities,
        ConversionCounts,
        Errors = Errors.Select(x => x.StableSummary),
        DeterminismDigest,
        RegistryCommitted
    });
}

public sealed record ConversionResult(
    ModDefinitionSet? Model,
    CompatibilityReport Report,
    IReadOnlyList<AdapterErrorRecord> Errors)
{
    public bool IsSuccess => Model is not null && Report.IsSuccess;
    public string Summary => Report.Summary;
}

internal static class AdapterDigest
{
    public static string Compute<T>(T value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(value))))
            .ToLowerInvariant();
}
