using System.Text.RegularExpressions;

namespace SimulatorLab.Core.Mods;

public sealed record RegistrySnapshot(
    int PackageCount,
    int DefinitionCount,
    IReadOnlyList<string> PackageIds,
    IReadOnlyList<string> DefinitionKeys,
    string Digest);

/// <summary>
/// Transactional registry for standard model values. Conversion builds a
/// complete candidate first; this registry is only changed after every
/// blocking error has been cleared.
/// </summary>
public sealed class ModDefinitionRegistry
{
    private readonly Dictionary<string, ModPackage> packages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CharacterDefinition> characters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CardDefinition> cards = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RelicDefinition> relics = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PowerDefinition> powers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EnemyDefinition> enemies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EncounterDefinition> encounters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EventDefinition> events = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RewardDefinition> rewards = new(StringComparer.Ordinal);

    public int PackageCount => packages.Count;
    public int DefinitionCount => characters.Count + cards.Count + relics.Count + powers.Count +
        enemies.Count + encounters.Count + events.Count + rewards.Count;
    public int Count => PackageCount + DefinitionCount;

    public IReadOnlyList<string> PackageIds => packages.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();

    public RegistrySnapshot Snapshot()
    {
        var keys = characters.Keys
            .Concat(cards.Keys)
            .Concat(relics.Keys)
            .Concat(powers.Keys)
            .Concat(enemies.Keys)
            .Concat(encounters.Keys)
            .Concat(events.Keys)
            .Concat(rewards.Keys)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        return new(PackageCount, DefinitionCount, PackageIds, keys,
            AdapterDigest.Compute(new { PackageIds, DefinitionKeys = keys }));
    }

    internal bool TryCommit(ModDefinitionSet model, out AdapterErrorRecord? error)
    {
        error = null;
        if (packages.ContainsKey(model.Package.ModId))
        {
            error = AdapterErrorRecord.Create(AdapterErrorCode.DuplicateId, model.Package.ModId,
                model.Package.SourcePath, $"mod id '{model.Package.ModId}' is already registered");
            return false;
        }

        var keys = model.AllIds.Select(id => Key(model.Package.ModId, id)).ToArray();
        if (keys.Length != keys.Distinct(StringComparer.Ordinal).Count())
        {
            error = AdapterErrorRecord.Create(AdapterErrorCode.DuplicateId, model.Package.ModId,
                model.Package.SourcePath, "candidate contains duplicate definition ids");
            return false;
        }
        var existing = keys.FirstOrDefault(ContainsDefinitionKey);
        if (existing is not null)
        {
            error = AdapterErrorRecord.Create(AdapterErrorCode.DuplicateId, existing,
                model.Package.SourcePath, $"definition '{existing}' is already registered");
            return false;
        }

        packages.Add(model.Package.ModId, model.Package);
        Add(characters, model.Package.ModId, model.Characters);
        Add(cards, model.Package.ModId, model.Cards);
        Add(relics, model.Package.ModId, model.Relics);
        Add(powers, model.Package.ModId, model.Powers);
        Add(enemies, model.Package.ModId, model.Enemies);
        Add(encounters, model.Package.ModId, model.Encounters);
        Add(events, model.Package.ModId, model.Events);
        Add(rewards, model.Package.ModId, model.Rewards);
        return true;
    }

    private bool ContainsDefinitionKey(string key) =>
        characters.ContainsKey(key) || cards.ContainsKey(key) || relics.ContainsKey(key) || powers.ContainsKey(key) ||
        enemies.ContainsKey(key) || encounters.ContainsKey(key) || events.ContainsKey(key) || rewards.ContainsKey(key);

    private static string Key(string modId, string id) => $"{modId}/{id}";

    private static void Add<T>(IDictionary<string, T> destination, string modId, IEnumerable<T> values)
        where T : notnull
    {
        foreach (var value in values)
        {
            var id = value switch
            {
                CharacterDefinition x => x.Id,
                CardDefinition x => x.Id,
                RelicDefinition x => x.Id,
                PowerDefinition x => x.Id,
                EnemyDefinition x => x.Id,
                EncounterDefinition x => x.Id,
                EventDefinition x => x.Id,
                RewardDefinition x => x.Id,
                _ => throw new InvalidOperationException("unknown standard definition")
            };
            destination.Add(Key(modId, id), value);
        }
    }
}

public static class ModAdapterConverter
{
    private static readonly Regex VersionPattern = new("^\\d+\\.\\d+\\.\\d+(?:[-+][0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> SupportedKinds = new(StringComparer.Ordinal)
    {
        "Character", "Card", "Relic", "Power", "Enemy", "Encounter", "Event", "Reward"
    };
    private static readonly HashSet<string> SupportedHooks = new(StringComparer.Ordinal)
    {
        "OnCombatStart", "OnCardPlayed", "OnTurnStart", "OnTurnEnd", "OnDeath", "OnCombatEnd",
        "OnRewardGenerated", "OnEventOptionSelected"
    };

    public static ConversionResult Convert(ModAdapterDocument document, ModDefinitionRegistry? registry = null)
    {
        registry ??= new ModDefinitionRegistry();
        var errors = new List<AdapterErrorRecord>();
        var discovered = new List<string>();
        var supported = new List<string>();
        var unsupported = new List<string>();
        var characters = new List<CharacterDefinition>();
        var cards = new List<CardDefinition>();
        var relics = new List<RelicDefinition>();
        var powers = new List<PowerDefinition>();
        var enemies = new List<EnemyDefinition>();
        var encounters = new List<EncounterDefinition>();
        var events = new List<EventDefinition>();
        var rewards = new List<RewardDefinition>();
        var resources = new List<ResourceResult>();
        var hooks = new List<HookCapability>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        ValidateDocument(document, errors);
        ValidateDependencies(document, errors);
        var objectInputs = document.Objects ?? [];
        var maxObjects = document.MaxObjects ?? 256;
        if (maxObjects < 0)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.InvalidValue, document.ModId, "/maxObjects", "maxObjects cannot be negative"));
        else if (objectInputs.Count > maxObjects)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.BudgetExceeded, document.ModId, "/objects",
                $"object count {objectInputs.Count} exceeds bounded budget {maxObjects}"));

        foreach (var input in objectInputs)
        {
            if (input is null)
            {
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, document.ModId, "/objects[]", "object is null"));
                continue;
            }

            var kind = input.Kind?.Trim() ?? "";
            var id = input.Id?.Trim() ?? "";
            var name = input.Name?.Trim() ?? "";
            if (kind.Length == 0)
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, idOrModule(document, id), "/objects/kind", "object kind is required"));
            if (id.Length == 0)
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, idOrModule(document, id), "/objects/id", "object id is required"));
            if (name.Length == 0)
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, idOrModule(document, id), $"/objects/{id}/name", "object name is required"));
            if (id.Length > 0 && !seenIds.Add(id))
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DuplicateId, id, $"/objects/{id}/id", $"duplicate definition id '{id}'"));
            if (kind.Length > 0 && !SupportedKinds.Contains(kind))
            {
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.Unsupported, idOrModule(document, id), $"/objects/{id}/kind", $"definition kind '{kind}' is unsupported"));
                continue;
            }

            var objectErrorsBefore = errors.Count;
            ValidateObjectValues(input, kind, id, errors);
            var objectHooks = ConvertHooks(input, id, hooks, discovered, supported, unsupported, errors);
            var objectResources = ConvertResources(input.ResourceResults, id, resources, errors);
            if (errors.Count != objectErrorsBefore && errors.Skip(objectErrorsBefore).Any(x => x.BlocksContinuation))
                continue;

            try
            {
                switch (kind)
                {
                    case "Character": characters.Add(new(id, name, Sorted(input.StartingCardIds), Sorted(input.StartingRelicIds), Sorted(input.PowerIds))); break;
                    case "Card": cards.Add(new(id, name, input.Cost!.Value, input.Description ?? "", objectResources, objectHooks, input.IsGeneratedCard)); break;
                    case "Relic": relics.Add(new(id, name, objectHooks, objectResources)); break;
                    case "Power": powers.Add(new(id, name, input.Duration, objectHooks, objectResources)); break;
                    case "Enemy": enemies.Add(new(id, name, Sorted(input.IntentIds), objectHooks)); break;
                    case "Encounter": encounters.Add(new(id, name, Sorted(input.EnemyIds))); break;
                    case "Event": events.Add(new(id, name, Sorted(input.OptionIds), objectHooks)); break;
                    case "Reward": rewards.Add(new(id, name, Sorted(input.ContentIds), objectResources)); break;
                }
            }
            catch (Exception ex)
            {
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.ConversionError, id, $"/objects/{id}", ex.Message));
            }
        }

        _ = ConvertResources(document.ResourceResults, document.ModId ?? "<module>", resources, errors);
        var model = new ModDefinitionSet(
            new ModPackage(document.ModId?.Trim() ?? "", document.DisplayName?.Trim() ?? "", document.Version?.Trim() ?? "",
                document.TargetGameVersion?.Trim() ?? "", (document.Dependencies ?? []).Where(x => x?.ModId is not null)
                    .Select(x => new ModDependency(x!.ModId!.Trim(), x.MinimumVersion?.Trim(), x.MinimumVersion?.Trim())).OrderBy(x => x.ModId, StringComparer.Ordinal).ToArray(),
                document.SourcePath?.Trim() ?? ""),
            characters.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(), cards.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(),
            relics.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(), powers.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(),
            enemies.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(), encounters.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(),
            events.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(), rewards.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(),
            resources.OrderBy(x => x.ResourceId, StringComparer.Ordinal).ThenBy(x => x.Amount).ThenBy(x => x.Reason, StringComparer.Ordinal).ToArray(),
            hooks.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray());

        ValidateReferences(model, errors);
        errors = errors.OrderBy(x => x.Code).ThenBy(x => x.ObjectId, StringComparer.Ordinal).ThenBy(x => x.Path, StringComparer.Ordinal).ThenBy(x => x.Message, StringComparer.Ordinal).ToList();
        var canCommit = model.Package.ModId.Length > 0 && errors.Count == 0;
        var committed = false;
        AdapterErrorRecord? registryError = null;
        if (canCommit)
            committed = registry.TryCommit(model, out registryError);
        if (canCommit && !committed)
            errors.Add(registryError ?? AdapterErrorRecord.Create(AdapterErrorCode.DuplicateId, model.Package.ModId, model.Package.SourcePath, "registry rejected candidate"));
        errors = errors.OrderBy(x => x.Code).ThenBy(x => x.ObjectId, StringComparer.Ordinal).ThenBy(x => x.Path, StringComparer.Ordinal).ThenBy(x => x.Message, StringComparer.Ordinal).ToList();
        var outputModel = errors.Count == 0 && committed ? model : null;
        var digest = AdapterDigest.Compute(outputModel is null ? new { Input = document, Errors = errors.Select(x => x.StableSummary) } : CanonicalModel(outputModel));
        var report = new CompatibilityReport(ModAdapterConstants.Schema, ModAdapterConstants.AdapterVersion,
            document.TargetGameVersion?.Trim() ?? "", document.ModId?.Trim() ?? "<module>", document.SourcePath?.Trim() ?? "<input>",
            discovered.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            supported.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            unsupported.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            outputModel is null ? ConversionCounts.From(ModDefinitionSet.Empty(model.Package)) : ConversionCounts.From(outputModel),
            errors, digest, committed);
        return new(outputModel, report, errors);
    }

    public static ConversionResult Convert(AdapterParseResult parsed, ModDefinitionRegistry? registry = null)
    {
        if (parsed.Document is not null && parsed.Errors.Count == 0)
            return Convert(parsed.Document, registry);
        var errors = parsed.Errors.OrderBy(x => x.Code).ThenBy(x => x.Path, StringComparer.Ordinal).ToArray();
        var report = new CompatibilityReport(ModAdapterConstants.Schema, ModAdapterConstants.AdapterVersion,
            ModAdapterConstants.TargetGameVersion, "<parse>", "<input>", [], [], [], new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0), errors,
            AdapterDigest.Compute(errors.Select(x => x.StableSummary)), false);
        return new(null, report, errors);
    }

    public static ConversionResult ConvertJson(string json, string path = "<fixture>", ModDefinitionRegistry? registry = null) =>
        Convert(ModAdapterDocumentParser.Parse(json, path), registry);

    private static void ValidateDocument(ModAdapterDocument document, List<AdapterErrorRecord> errors)
    {
        if (string.IsNullOrWhiteSpace(document.ModId)) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, null, "/modId", "modId is required"));
        if (string.IsNullOrWhiteSpace(document.DisplayName)) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, document.ModId, "/displayName", "displayName is required"));
        if (string.IsNullOrWhiteSpace(document.Version)) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, document.ModId, "/version", "version is required"));
        else if (!VersionPattern.IsMatch(document.Version.Trim().TrimStart('v', 'V'))) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.InvalidValue, document.ModId, "/version", "version is invalid"));
        if (string.IsNullOrWhiteSpace(document.TargetGameVersion)) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, document.ModId, "/targetGameVersion", "targetGameVersion is required"));
        else if (!string.Equals(document.TargetGameVersion.Trim(), ModAdapterConstants.TargetGameVersion, StringComparison.Ordinal)) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion, document.ModId, "/targetGameVersion", $"target must be exactly {ModAdapterConstants.TargetGameVersion}"));
        if (string.IsNullOrWhiteSpace(document.SourcePath)) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, document.ModId, "/sourcePath", "sourcePath is required"));
        if (document.Files is null || document.Files.Count == 0) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingFile, document.ModId, "/files", "at least one adapter input file is required"));
        else foreach (var file in document.Files)
        {
            if (file is null || string.IsNullOrWhiteSpace(file.Path) || !file.Present)
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingFile, document.ModId, $"/files/{file?.Path ?? "<null>"}", "referenced adapter file is missing"));
        }
    }

    private static void ValidateDependencies(ModAdapterDocument document, List<AdapterErrorRecord> errors)
    {
        var resolved = (document.ResolvedDependencyIds ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal);
        foreach (var dependency in document.Dependencies ?? [])
        {
            if (dependency is null || string.IsNullOrWhiteSpace(dependency.ModId))
            {
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, document.ModId, "/dependencies/id", "dependency id is required"));
                continue;
            }
            if (!resolved.Contains(dependency.ModId.Trim()))
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DependencyMissing, document.ModId, $"/dependencies/{dependency.ModId}", $"dependency '{dependency.ModId}' is not resolved"));
        }
    }

    private static void ValidateObjectValues(AdapterObjectInput input, string kind, string id, List<AdapterErrorRecord> errors)
    {
        // The game uses canonical cost -1 for generated/token cards that are
        // not ordinary hand-play cards (for example Watcher's Wish tokens).
        // Only an adapter that explicitly proves that semantic may preserve
        // the sentinel; ordinary inputs still fail closed at -1 and below.
        if (kind == "Card" && (!input.Cost.HasValue || input.Cost.Value < 0 && !(input.Cost.Value == -1 && input.IsGeneratedCard))) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.InvalidValue, id, $"/objects/{id}/cost", "card cost must be zero or an explicitly identified generated-card sentinel -1"));
        if (kind == "Power" && input.Duration is < 0) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.InvalidValue, id, $"/objects/{id}/duration", "power duration cannot be negative"));
    }

    private static IReadOnlyList<HookCapability> ConvertHooks(AdapterObjectInput input, string id, List<HookCapability> all, List<string> discovered, List<string> supported, List<string> unsupported, List<AdapterErrorRecord> errors)
    {
        var result = new List<HookCapability>();
        foreach (var hook in input.Hooks ?? [])
        {
            if (hook is null || string.IsNullOrWhiteSpace(hook.Id) || string.IsNullOrWhiteSpace(hook.HookPoint))
            {
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, id, $"/objects/{id}/hooks", "hook id and hook point are required"));
                continue;
            }
            var hookId = hook.Id.Trim(); var hookPoint = hook.HookPoint.Trim(); var ok = SupportedHooks.Contains(hookPoint);
            var capability = new HookCapability(hookId, hookPoint, ok, hook.Description?.Trim() ?? "", hook.EvidenceRef);
            result.Add(capability); all.Add(capability); discovered.Add(hookId);
            if (ok) supported.Add(hookId); else { unsupported.Add(hookId); errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.Unsupported, id, $"/objects/{id}/hooks/{hookId}", $"hook point '{hookPoint}' is unsupported", hook.EvidenceRef)); }
        }
        return result;
    }

    private static IReadOnlyList<ResourceResult> ConvertResources(IReadOnlyList<AdapterResourceInput>? inputs, string id, List<ResourceResult> all, List<AdapterErrorRecord> errors)
    {
        var result = new List<ResourceResult>();
        foreach (var resource in inputs ?? [])
        {
            if (resource is null || string.IsNullOrWhiteSpace(resource.ResourceId) || !resource.Amount.HasValue || string.IsNullOrWhiteSpace(resource.Reason))
            {
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, id, $"/objects/{id}/resourceResults", "resourceId, amount and reason are required"));
                continue;
            }
            var value = new ResourceResult(resource.ResourceId.Trim(), resource.Amount.Value, resource.Reason.Trim());
            result.Add(value); all.Add(value);
        }
        return result;
    }

    private static void ValidateReferences(ModDefinitionSet model, List<AdapterErrorRecord> errors)
    {
        var ids = model.AllIds.ToHashSet(StringComparer.Ordinal);
        foreach (var c in model.Characters)
            foreach (var id in c.StartingCardIds.Concat(c.StartingRelicIds).Concat(c.PowerIds).Where(x => !ids.Contains(x)))
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, c.Id, $"/objects/{c.Id}", $"character reference '{id}' is missing"));
        foreach (var e in model.Encounters)
            foreach (var id in e.EnemyIds.Where(x => !ids.Contains(x))) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, e.Id, $"/objects/{e.Id}", $"encounter enemy reference '{id}' is missing"));
        foreach (var e in model.Events)
            foreach (var id in e.OptionIds.Where(x => string.IsNullOrWhiteSpace(x))) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, e.Id, $"/objects/{e.Id}", "event option id is empty"));
    }

    private static string idOrModule(ModAdapterDocument document, string id) => id.Length > 0 ? id : document.ModId ?? "<module>";
    private static IReadOnlyList<string> Sorted(IReadOnlyList<string>? values) => (values ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    private static object CanonicalModel(ModDefinitionSet model) => new
    {
        Package = model.Package,
        Characters = model.Characters,
        Cards = model.Cards,
        Relics = model.Relics,
        Powers = model.Powers,
        Enemies = model.Enemies,
        Encounters = model.Encounters,
        Events = model.Events,
        Rewards = model.Rewards,
        Resources = model.ResourceResults,
        Hooks = model.HookCapabilities
    };
}

