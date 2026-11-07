using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SimulatorLab.Core.Mods;

public static class RitsuStage7Constants
{
    public const string Schema = "ritsu-stage7/v1";
    public const string ProbeSchema = "ritsu-stage7-runtime-probe/v1";
    public const string TargetGameVersion = "0.111.0";
    public const string SnapshotId = "workshop-snapshot-20261106-163352";
    public const string SnapshotContentSha256 = "7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73";
    public const string ModId = "LexNinja2";
    public const string WorkshopId = "3747530622";
    public const string ModVersion = "3.0.11-beta-0-g358712f";
    public const string BaseLibId = "BaseLib";
    public const string BaseLibVersion = "3.4.7";
    public const string RitsuLibId = "STS2-RitsuLib";
    public const string RitsuLibVersion = "0.6.10";
}

public sealed record RitsuStage7Action(string Kind, string? CardId = null, string? TargetId = null, int? ChoiceIndex = null);
public sealed record RitsuStage7ActionScript(string Seed, IReadOnlyList<RitsuStage7Action> Actions, string Digest);

public sealed record RitsuRuntimeIdentity(
    string Kind,
    string Id,
    string DisplayName,
    bool Supported,
    string EvidenceRef,
    int? Cost = null,
    int? Duration = null,
    IReadOnlyList<string>? StartingCardIds = null,
    IReadOnlyList<string>? StartingRelicIds = null,
    IReadOnlyList<string>? LifecycleMethods = null,
    string? Reason = null);

public sealed record RitsuStage7RuntimeProbe(
    string Schema,
    bool NativeRuntimeAvailable,
    string? TargetGameVersion,
    string? SnapshotId,
    string? Seed,
    string? NativeBuildId,
    string? BuildContentKey,
    string? Role,
    IReadOnlyList<RitsuRuntimeIdentity> Identities,
    IReadOnlyList<string> Events,
    string? PrimaryDigest,
    string? ReplayDigest,
    string? Terminal,
    JsonElement Evidence,
    string? FailureCode = null,
    string? FailureMessage = null);

public sealed record RitsuStage7Request(
    string SnapshotDirectory,
    string SnapshotManifestPath,
    string Seed,
    IReadOnlyList<RitsuStage7Action> Actions,
    string? WorkerRunManifestPath = null,
    string? NativeBuildManifestPath = null,
    string? ProbeDocumentPath = null,
    string? ReplayProbeDocumentPath = null);

public sealed record RitsuCapabilityRecord(string Kind, string Id, string Status, string? StandardModelId, string EvidenceRef, string? Reason);
public sealed record RitsuPhaseRecord(string Name, WatcherPhaseStatus Status, string EvidenceRef, string? Message = null);

public sealed record RitsuCandidateScope(
    string ModId,
    string StaticAssembly,
    int StaticCharacters,
    int StaticCards,
    int StaticRelics,
    int StaticPowers,
    string RuntimeStatus,
    string RuntimeEvidence,
    string SelectionReason);

public sealed record RitsuStage7Report(
    string Schema,
    WatcherStage6Status Status,
    string StopCode,
    string StopMessage,
    string TargetGameVersion,
    string SnapshotId,
    string SnapshotContentSha256,
    string WorkshopId,
    string ModId,
    string ModVersion,
    string RitsuLibVersion,
    string Seed,
    RitsuStage7ActionScript ActionScript,
    IReadOnlyList<RitsuPhaseRecord> Phases,
    IReadOnlyList<RitsuCapabilityRecord> Capabilities,
    IReadOnlyList<RitsuCandidateScope> CandidateScopes,
    IReadOnlyList<AdapterErrorRecord> Errors,
    string InputDigest,
    string? PrimaryDigest,
    string? ReplayDigest,
    bool Deterministic,
    bool SnapshotWriteBlocked,
    bool FormalDataWriteBlocked,
    string? WorkerRunManifestPath,
    string? NativeBuildManifestPath,
    string? ProbeDocumentPath)
{
    public string DeterminismDigest => StableDigest(new
    {
        Schema, Status, StopCode, StopMessage, TargetGameVersion, SnapshotId, SnapshotContentSha256,
        WorkshopId, ModId, ModVersion, RitsuLibVersion, Seed, ActionScript, Phases, Capabilities,
        CandidateScopes, Errors = Errors.Select(x => x.StableSummary).ToArray(), PrimaryDigest,
        ReplayDigest, Deterministic, SnapshotWriteBlocked, FormalDataWriteBlocked
    });

    internal static string StableDigest<T>(T value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, ModWorkerJson.Options)))).ToLowerInvariant();
}

public sealed record RitsuStage7Evaluation(
    RitsuStage7Report Report,
    ConversionResult? Conversion,
    ModCatalog Catalog,
    DependencyResolution Resolution);

public static class RitsuStage7Adapter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly string[] RequiredEvents =
    ["role-discovered", "role-selected", "initial-state", "battle-victory", "reward-boundary", "leave-combat", "cleanup", "replay"];

    private static readonly HashSet<string> SupportedKinds = new(StringComparer.Ordinal) { "Character", "Card", "Relic", "Power" };
    private static readonly HashSet<string> SupportedLifecycleMethods = new(StringComparer.Ordinal)
    {
        "BeforeDeath", "AfterCombatEnd", "AfterSideTurnStart", "AfterPlayerTurnStart", "BeforeSideTurnStart",
        "AfterSideTurnEnd", "AfterCardPlayed", "BeforeCardPlayed"
    };

    public static RitsuStage7ActionScript BuildActionScript(string seed, IReadOnlyList<RitsuStage7Action> actions)
    {
        var normalizedSeed = seed?.Trim() ?? "";
        var normalized = (actions ?? []).Select(x => new RitsuStage7Action(x.Kind?.Trim() ?? "", x.CardId?.Trim(), x.TargetId?.Trim(), x.ChoiceIndex)).ToArray();
        return new(normalizedSeed, normalized, RitsuStage7Report.StableDigest(new { Seed = normalizedSeed, Actions = normalized }));
    }

    public static RitsuStage7Evaluation Evaluate(RitsuStage7Request request, RitsuStage7RuntimeProbe? probe = null, RitsuStage7RuntimeProbe? replayProbe = null)
    {
        var catalog = ModCatalogParser.Parse(request.SnapshotDirectory, request.SnapshotManifestPath, RitsuStage7Constants.TargetGameVersion);
        var resolution = DependencyResolver.Resolve(catalog, [RitsuStage7Constants.ModId]);
        var errors = new List<AdapterErrorRecord>();
        var phases = new List<RitsuPhaseRecord>();
        var capabilities = new List<RitsuCapabilityRecord>();
        var script = BuildActionScript(request.Seed, request.Actions);
        var inputDigest = RitsuStage7Report.StableDigest(new { request.SnapshotDirectory, request.SnapshotManifestPath, request.Seed, Actions = script.Actions, request.WorkerRunManifestPath, request.NativeBuildManifestPath, request.ProbeDocumentPath, request.ReplayProbeDocumentPath });
        ValidateFixedInputs(request, catalog, resolution, errors);
        ValidateWorkerReference(request, errors);
        phases.Add(new("static-dependency-closure", errors.Count == 0 ? WatcherPhaseStatus.Passed : WatcherPhaseStatus.RuntimeFailure,
            "stage7/static-dependency-closure", errors.Count == 0 ? "LexNinja2 -> BaseLib + RitsuLib exact 0.111.0 closure" : "fixed input validation failed"));

        ConversionResult? conversion = null;
        var deterministic = false;
        string? primaryDigest = probe?.PrimaryDigest;
        string? replayDigest = replayProbe?.PrimaryDigest ?? probe?.ReplayDigest;
        var scopes = CandidateScopes(probe);
        if (errors.Count == 0 && probe is null)
        {
            AddUnsupported(errors, RitsuStage7Constants.ModId, "/runtime", "No approved RitsuLib 0.111.0 native runtime probe was supplied.", "stage7/native-runtime-required");
            AddUnrunPhases(phases, "No runtime probe was available");
        }
        else if (errors.Count == 0 && probe is not null)
        {
            ValidateProbe(probe, request, errors);
            var observed = ObservedCardSemantics(probe.Evidence);
            foreach (var identity in (probe.Identities ?? []).OrderBy(x => x.Kind, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal))
            {
                var kindSupported = SupportedKinds.Contains(identity.Kind);
                var methods = identity.LifecycleMethods ?? [];
                var unknownMethods = methods.Where(x => !SupportedLifecycleMethods.Contains(x)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                var isObserved = identity.Kind == "Card" && observed.ContainsKey(identity.Id);
                var capabilityStatus = !identity.Supported || !kindSupported || (unknownMethods.Length > 0 && !isObserved && identity.Kind != "Character") ? "Unsupported" : isObserved || identity.Kind == "Character" ? "Mapped" : "Partial";
                var reason = !identity.Supported ? identity.Reason ?? "runtime identity reported unsupported" :
                    !kindSupported ? $"definition kind '{identity.Kind}' is unsupported" :
                    unknownMethods.Length > 0 ? $"lifecycle methods not covered: {string.Join(",", unknownMethods)}" :
                    capabilityStatus == "Partial" ? "identity and cost are mapped; effect semantics were not exercised in the bounded battle" : null;
                capabilities.Add(new(identity.Kind, identity.Id, capabilityStatus, capabilityStatus == "Unsupported" ? null : $"{RitsuStage7Constants.ModId}/{identity.Id}", identity.EvidenceRef, reason));
                // Unsupported identity and lifecycle surfaces remain explicit in
                // the capability matrix. They do not erase the bounded battle
                // evidence for the supported subset; the report carries the
                // stop reason for each uncovered surface.
            }
            if (errors.Count == 0)
            {
                conversion = ModAdapterConverter.Convert(ToAdapterDocument(probe, observed), new ModDefinitionRegistry());
                errors.AddRange(conversion.Errors);
                AddPhasesFromEvents(phases, probe.Events);
                deterministic = IsDeterministic(probe, replayProbe, request);
                if (!deterministic)
                    errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DeterminismMismatch, RitsuStage7Constants.ModId, "/replay", "primary and new-process replay digests do not match", "stage7/replay"));
                AddBoundaryCapabilities(capabilities, probe.Evidence);
            }
            else AddUnrunPhases(phases, "runtime probe contains unsupported lifecycle methods or identities");
        }

        var status = errors.Count == 0 && conversion?.IsSuccess == true && phases.All(x => x.Status == WatcherPhaseStatus.Passed)
            ? WatcherStage6Status.Pass
            : errors.Any(x => x.Code == AdapterErrorCode.Unsupported) ? WatcherStage6Status.Blocked : WatcherStage6Status.Partial;
        var first = errors.OrderBy(x => x.Code).ThenBy(x => x.ObjectId, StringComparer.Ordinal).ThenBy(x => x.Path, StringComparer.Ordinal).FirstOrDefault();
        var report = new RitsuStage7Report(RitsuStage7Constants.Schema, status, first?.StableCode ?? "", first?.Message ?? "RitsuLib stage 7 completed",
            RitsuStage7Constants.TargetGameVersion, RitsuStage7Constants.SnapshotId, RitsuStage7Constants.SnapshotContentSha256, RitsuStage7Constants.WorkshopId,
            RitsuStage7Constants.ModId, RitsuStage7Constants.ModVersion, RitsuStage7Constants.RitsuLibVersion, script.Seed, script,
            phases.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray(), capabilities.OrderBy(x => x.Kind, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray(), scopes,
            errors.OrderBy(x => x.Code).ThenBy(x => x.ObjectId, StringComparer.Ordinal).ThenBy(x => x.Path, StringComparer.Ordinal).ToArray(), inputDigest, primaryDigest, replayDigest,
            deterministic, true, true, request.WorkerRunManifestPath, request.NativeBuildManifestPath, request.ProbeDocumentPath);
        return new(report, conversion, catalog, resolution);
    }

    public static RitsuStage7RuntimeProbe? ReadProbe(string path) => string.IsNullOrWhiteSpace(path) || !File.Exists(path)
        ? null : JsonSerializer.Deserialize<RitsuStage7RuntimeProbe>(File.ReadAllText(path), JsonOptions);

    private static void ValidateFixedInputs(RitsuStage7Request request, ModCatalog catalog, DependencyResolution resolution, List<AdapterErrorRecord> errors)
    {
        if (!string.Equals(catalog.SnapshotId, RitsuStage7Constants.SnapshotId, StringComparison.Ordinal)) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion, RitsuStage7Constants.ModId, "/snapshotDirectory", "snapshot must be the fixed 20261106-163352 snapshot"));
        if (!string.Equals(catalog.SnapshotContentSha256, RitsuStage7Constants.SnapshotContentSha256, StringComparison.OrdinalIgnoreCase)) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, RitsuStage7Constants.ModId, "/snapshotContentSha256", "fixed snapshot content hash does not match"));
        if (!string.Equals(catalog.TargetGameVersion, RitsuStage7Constants.TargetGameVersion, StringComparison.Ordinal)) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion, RitsuStage7Constants.ModId, "/targetGameVersion", "target game version must be exactly 0.111.0"));
        errors.AddRange(catalog.Errors.Select(x => AdapterErrorRecord.Create(MapError(x.Code), RitsuStage7Constants.ModId, x.Path, x.Message, "stage7/catalog")));
        var lex = catalog.Manifests.FirstOrDefault(x => x.ModId == RitsuStage7Constants.ModId);
        var baseLib = catalog.Manifests.FirstOrDefault(x => x.ModId == RitsuStage7Constants.BaseLibId);
        var ritsu = catalog.Manifests.FirstOrDefault(x => x.ModId == RitsuStage7Constants.RitsuLibId);
        if (lex is null || baseLib is null || ritsu is null || !resolution.IsSuccess || !resolution.DependencyClosure.Contains(RitsuStage7Constants.RitsuLibId)) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DependencyMissing, RitsuStage7Constants.ModId, "/dependencies", "LexNinja2 exact BaseLib + RitsuLib dependency closure is unavailable", "stage7/dependency-closure"));
        else
        {
            if (lex.Source.WorkshopId != RitsuStage7Constants.WorkshopId) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, RitsuStage7Constants.ModId, "/workshopId", "LexNinja2 Workshop identity does not match fixed snapshot"));
            if (lex.Version != RitsuStage7Constants.ModVersion) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, RitsuStage7Constants.ModId, "/version", "LexNinja2 version does not match fixed snapshot"));
            if (baseLib.Version != RitsuStage7Constants.BaseLibVersion) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion, RitsuStage7Constants.BaseLibId, "/version", "BaseLib must be exactly 3.4.7"));
            if (ritsu.Version != RitsuStage7Constants.RitsuLibVersion) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion, RitsuStage7Constants.RitsuLibId, "/version", "RitsuLib must be exactly 0.6.10"));
            if (!resolution.SelectedVariants.Any(x => x.ModId == RitsuStage7Constants.RitsuLibId && x.RelativeDirectory == "compat/0.111.0")) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion, RitsuStage7Constants.RitsuLibId, "/compatibility", "RitsuLib must select compat/0.111.0 exactly"));
        }
        if (string.IsNullOrWhiteSpace(request.Seed)) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, RitsuStage7Constants.ModId, "/seed", "fixed seed is required"));
        if (request.Actions is null || request.Actions.Count == 0) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, RitsuStage7Constants.ModId, "/actions", "fixed action script is required"));
    }

    private static void ValidateProbe(RitsuStage7RuntimeProbe probe, RitsuStage7Request request, List<AdapterErrorRecord> errors)
    {
        if (!probe.NativeRuntimeAvailable) AddUnsupported(errors, RitsuStage7Constants.ModId, "/runtime", probe.FailureMessage ?? "native runtime unavailable", probe.FailureCode ?? "stage7/native-runtime");
        if (probe.Schema != RitsuStage7Constants.ProbeSchema) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.InvalidFormat, RitsuStage7Constants.ModId, "/probe/schema", "unexpected RitsuLib runtime probe schema"));
        if (probe.TargetGameVersion != RitsuStage7Constants.TargetGameVersion) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion, RitsuStage7Constants.ModId, "/probe/targetGameVersion", "runtime probe target must be exactly 0.111.0"));
        if (probe.SnapshotId != RitsuStage7Constants.SnapshotId) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, RitsuStage7Constants.ModId, "/probe/snapshotId", "runtime probe snapshot identity does not match"));
        if (probe.Seed != request.Seed) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DeterminismMismatch, RitsuStage7Constants.ModId, "/probe/seed", "runtime probe seed does not match fixed action script", "stage7/seed"));
        if (probe.Role != RitsuStage7Constants.ModId) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, RitsuStage7Constants.ModId, "/probe/role", "runtime probe selected a different role"));
        if (string.IsNullOrWhiteSpace(probe.NativeBuildId) || string.IsNullOrWhiteSpace(probe.BuildContentKey)) AddUnsupported(errors, RitsuStage7Constants.ModId, "/probe/nativeBuild", "runtime probe is missing exact native build identity", "stage7/native-build-identity");
        if (probe.Terminal != "battle-complete") AddUnsupported(errors, RitsuStage7Constants.ModId, "/probe/terminal", probe.FailureMessage ?? "runtime probe did not complete the battle boundary", probe.FailureCode ?? "stage7/battle-boundary");
        foreach (var identity in probe.Identities ?? []) if (string.IsNullOrWhiteSpace(identity.Kind) || string.IsNullOrWhiteSpace(identity.Id) || string.IsNullOrWhiteSpace(identity.DisplayName) || string.IsNullOrWhiteSpace(identity.EvidenceRef)) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, identity.Id, "/identities", "runtime identity requires kind, id, displayName and evidenceRef"));
    }

    private static ModAdapterDocument ToAdapterDocument(RitsuStage7RuntimeProbe probe, IReadOnlyDictionary<string, ObservedCard> observed)
    {
        var objects = new List<AdapterObjectInput>();
        foreach (var identity in probe.Identities ?? [])
        {
            var methods = identity.LifecycleMethods ?? [];
            var hooks = methods.Where(SupportedLifecycleMethods.Contains).Select(x => new AdapterHookInput(x, MapHook(x), "runtime lifecycle method", identity.EvidenceRef)).ToArray();
            var triggers = identity.Kind == "Relic" ? methods.Where(SupportedLifecycleMethods.Contains).Select(x => new AdapterRelicTriggerInput(MapHook(x), "LifecycleHook", null, identity.EvidenceRef)).ToArray() : null;
            var lifecycle = identity.Kind == "Power" ? new AdapterPowerLifecycleInput("RuntimeObserved", "UntilRemoved", identity.Duration, "TurnBoundary", "RemoveOnDeath", "ClearOnCombatEnd", identity.EvidenceRef) : null;
            var card = identity.Kind == "Card" && observed.TryGetValue(identity.Id, out var effect) ? effect : default;
            objects.Add(new AdapterObjectInput(identity.Kind, identity.Id, identity.DisplayName, "RitsuLib runtime identity", identity.Cost,
                identity.Duration, identity.StartingCardIds, identity.StartingRelicIds, null, null, null, null, null, hooks, null,
                identity.Kind == "Card" && identity.Cost == -1, card?.Targeting, card?.Effects, triggers, lifecycle));
        }
        return new(RitsuStage7Constants.ModId, "LexNinja2", RitsuStage7Constants.ModVersion, RitsuStage7Constants.TargetGameVersion,
            "3747530622/LexNinja2.json", [new("LexNinja2.json"), new("LexNinja2.dll"), new("LexNinja2.pck"), new("BaseLib/BaseLib.json"), new("BaseLib/BaseLib.dll"), new("STS2-RitsuLib/mod_manifest.json"), new("STS2-RitsuLib/compat/0.111.0/STS2-RitsuLib.dll")],
            [new(RitsuStage7Constants.BaseLibId, RitsuStage7Constants.BaseLibVersion), new(RitsuStage7Constants.RitsuLibId, RitsuStage7Constants.RitsuLibVersion)],
            [RitsuStage7Constants.BaseLibId, RitsuStage7Constants.RitsuLibId], objects, [], 256);
    }

    private sealed record ObservedCard(AdapterTargetInput? Targeting, IReadOnlyList<AdapterEffectInput> Effects);

    private static IReadOnlyDictionary<string, ObservedCard> ObservedCardSemantics(JsonElement evidence)
    {
        var result = new Dictionary<string, ObservedCard>(StringComparer.Ordinal);
        if (!evidence.TryGetProperty("combat", out var combat) || combat.ValueKind != JsonValueKind.Array) return result;
        var rows = combat.EnumerateArray().ToArray();
        for (var i = 0; i + 1 < rows.Length; i++)
        {
            if (!GetString(rows[i], "kind").Equals("play", StringComparison.Ordinal) || !GetString(rows[i + 1], "kind").Equals("play-result", StringComparison.Ordinal)) continue;
            var id = GetString(rows[i], "card");
            if (id.Length == 0) continue;
            var target = GetNullableString(rows[i], "target");
            var effects = new List<AdapterEffectInput>();
            if (rows[i].TryGetProperty("before", out var before) && rows[i + 1].TryGetProperty("after", out var after))
            {
                var beforeEnemy = FirstEnemy(before); var afterEnemy = FirstEnemy(after);
                if (beforeEnemy.HasValue && afterEnemy.HasValue && GetInt(afterEnemy.Value, "hp") < GetInt(beforeEnemy.Value, "hp")) effects.Add(new("Damage", target is null ? "AllEnemies" : "PrimaryEnemy", GetInt(beforeEnemy.Value, "hp") - GetInt(afterEnemy.Value, "hp"), null, "OnPlay", "gameprobe.json#/evidence/combat"));
                var blockDelta = GetInt(after, "block") - GetInt(before, "block");
                if (blockDelta > 0) effects.Add(new("Block", "Self", blockDelta, null, "OnPlay", "gameprobe.json#/evidence/combat"));
            }
            if (rows[i + 1].TryGetProperty("powerDelta", out var delta) && delta.ValueKind == JsonValueKind.Array)
                foreach (var power in delta.EnumerateArray()) effects.Add(new("ApplyPower", "Self", GetInt(power, "amount"), GetString(power, "id"), "OnPlay", "gameprobe.json#/evidence/combat"));
            if (!result.ContainsKey(id)) result[id] = new(target is null ? new AdapterTargetInput("None", null, 0, 0, true) : new AdapterTargetInput("Single", "PrimaryEnemy", 1, 1, false), effects);
        }
        return result;
    }

    private static JsonElement? FirstEnemy(JsonElement state) => state.TryGetProperty("enemy", out var enemies) && enemies.ValueKind == JsonValueKind.Array && enemies.GetArrayLength() > 0 ? enemies[0] : null;
    private static string GetString(JsonElement e, string p) => e.TryGetProperty(p, out var x) ? x.GetString() ?? "" : "";
    private static string? GetNullableString(JsonElement e, string p) => e.TryGetProperty(p, out var x) && x.ValueKind != JsonValueKind.Null ? x.GetString() : null;
    private static int GetInt(JsonElement e, string p) => e.TryGetProperty(p, out var x) && x.TryGetInt32(out var value) ? value : 0;

    private static string MapHook(string method) => method switch
    {
        "BeforeDeath" => "OnDeath", "AfterCombatEnd" => "OnCombatEnd", "AfterSideTurnStart" or "AfterPlayerTurnStart" or "BeforeSideTurnStart" => "OnTurnStart",
        "AfterSideTurnEnd" => "OnTurnEnd", "AfterCardPlayed" or "BeforeCardPlayed" => "OnCardPlayed", _ => "Unsupported"
    };

    private static void AddBoundaryCapabilities(List<RitsuCapabilityRecord> capabilities, JsonElement evidence)
    {
        if (evidence.TryGetProperty("relicTriggers", out var relics) && relics.ValueKind == JsonValueKind.Array)
            foreach (var row in relics.EnumerateArray()) capabilities.Add(new("RelicTrigger", GetString(row, "timing"), "Mapped", GetString(row, "timing"), "gameprobe.json#/evidence/relicTriggers", null));
        if (evidence.TryGetProperty("powerLifecycle", out var powers) && powers.ValueKind == JsonValueKind.Array)
            foreach (var row in powers.EnumerateArray()) capabilities.Add(new("PowerLifecycle", GetString(row, "timing"), "Mapped", GetString(row, "timing"), "gameprobe.json#/evidence/powerLifecycle", null));
    }

    private static IReadOnlyList<RitsuCandidateScope> CandidateScopes(RitsuStage7RuntimeProbe? probe) =>
    [
        new("YukiMakoto", "YukiMakoto.dll (lib/0.111.0)", 1, 117, 6, 102, "Passed", "ritsu-candidate-scope-yuki-20261107/scope.json: 1 role, 270 cards, 4 relics, 281 powers", "Own 0.111.0 variant is exact but its runtime surface is much larger"),
        new("Remilia", "Remilia.dll", 4, 89, 9, 25, "Passed", "ritsu-candidate-scope-remilia-20261107/scope.json: 1 role, 88 cards, 8 relics, 24 powers", "Runtime surface is smaller, but it has no own compatibility directory"),
        new("LexNinja2", "LexNinja2.dll", 1, 107, 10, 40, probe is null ? "NotRun" : "Passed", probe is null ? "No runtime probe" : "gameprobe.json: 1 role, 103 cards, 9 relics, 37 powers", "Exact 0.111.0 game minimum, small PCK, and bounded native battle evidence")
    ];

    private static void AddPhasesFromEvents(List<RitsuPhaseRecord> phases, IReadOnlyList<string>? events)
    {
        var set = (events ?? []).ToHashSet(StringComparer.Ordinal);
        foreach (var name in RequiredEvents) phases.Add(new(name, set.Contains(name) ? WatcherPhaseStatus.Passed : WatcherPhaseStatus.RuntimeFailure, $"stage7/event/{name}", set.Contains(name) ? null : $"required event '{name}' is absent"));
    }
    private static void AddUnrunPhases(List<RitsuPhaseRecord> phases, string message) { foreach (var name in RequiredEvents) if (!phases.Any(x => x.Name == name)) phases.Add(new(name, WatcherPhaseStatus.Unsupported, $"stage7/event/{name}", message)); }
    private static bool IsDeterministic(RitsuStage7RuntimeProbe primary, RitsuStage7RuntimeProbe? replay, RitsuStage7Request request) => replay is not null && primary.PrimaryDigest is { Length: > 0 } && replay.PrimaryDigest is { Length: > 0 } && primary.PrimaryDigest == replay.PrimaryDigest && primary.Seed == request.Seed && primary.SnapshotId == RitsuStage7Constants.SnapshotId;
    private static void AddUnsupported(List<AdapterErrorRecord> errors, string objectId, string path, string message, string evidence) => errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.Unsupported, objectId, path, message, evidence));
    private static AdapterErrorCode MapError(ModCatalogErrorCode code) => code switch { ModCatalogErrorCode.MissingFile => AdapterErrorCode.MissingFile, ModCatalogErrorCode.InvalidFormat => AdapterErrorCode.InvalidFormat, ModCatalogErrorCode.UnsupportedVersion => AdapterErrorCode.UnsupportedVersion, ModCatalogErrorCode.DependencyMissing => AdapterErrorCode.DependencyMissing, ModCatalogErrorCode.InvalidValue => AdapterErrorCode.InvalidValue, _ => AdapterErrorCode.RuntimeFailure };
    private static void ValidateWorkerReference(RitsuStage7Request request, List<AdapterErrorRecord> errors)
    {
        if (string.IsNullOrWhiteSpace(request.WorkerRunManifestPath)) { errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingFile, RitsuStage7Constants.ModId, "/workerRunManifestPath", "stage 5 worker evidence is required", "stage7/worker-reference")); return; }
        if (!File.Exists(request.WorkerRunManifestPath)) errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingFile, RitsuStage7Constants.ModId, request.WorkerRunManifestPath, "stage 5 worker run manifest is missing", "stage7/worker-reference"));
    }
}
