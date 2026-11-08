using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SimulatorLab.Core.Mods;

/// <summary>
/// Stage 8 is deliberately a single Kaguya encounter. The native probe is the
/// only source of gameplay facts; this adapter consumes scalar evidence and
/// converts the observed enemy, encounter, reward, and ledger into the stable
/// standard model.
/// </summary>
public static class KaguyaStage8Constants
{
    public const string Schema = "kaguya-stage8/v1";
    public const string ProbeSchema = "kaguya-stage8-runtime-probe/v1";
    public const string TargetGameVersion = "0.111.0";
    public const string SnapshotId = "workshop-snapshot-20261106-163352";
    public const string SnapshotContentSha256 = "7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73";
    public const string WorkshopId = "3747548629";
    public const string ModId = "Kaguya";
    public const string ModVersion = "3.2.4";
    public const string BaseLibId = "BaseLib";
    public const string BaseLibVersion = "3.4.7";
    public const string EncounterType = "Kaguya.Encounters.MixedSoldiersEliteEncounter";
    public const string EnemyType = "Kaguya.Monsters.BattleSoldierA";
    public const string EventType = "Kaguya.Events.TaketoriMonogatariEvent";
}

public sealed record KaguyaStage8Action(string Kind, string? TargetId = null, int? ChoiceIndex = null);

public sealed record KaguyaStage8ActionScript(
    string Seed,
    IReadOnlyList<KaguyaStage8Action> Actions,
    string Digest);

public sealed record KaguyaEnemyObservation(
    string Id,
    string DisplayName,
    string TypeName,
    IReadOnlyList<string> IntentIds,
    IReadOnlyList<string> IntentKinds,
    bool IntendsToAttack,
    int InitialHp,
    int FinalHp,
    int InitialBlock,
    int FinalBlock,
    string EvidenceRef);

public sealed record KaguyaRewardObservation(
    string RewardId,
    string Kind,
    int? GoldAmount,
    string? CardId,
    string EvidenceRef);

public sealed record KaguyaEventObservation(
    string Id,
    string DisplayName,
    IReadOnlyList<string> OptionIds,
    string SelectedOptionId,
    string EvidenceRef);

public sealed record KaguyaRuntimeProbe(
    string Schema,
    bool NativeRuntimeAvailable,
    string? TargetGameVersion,
    string? SnapshotId,
    string? Seed,
    string? NativeBuildId,
    string? BuildContentKey,
    string? ModId,
    string? EncounterId,
    string? EncounterType,
    string? EnemyId,
    string? EnemyType,
    KaguyaEnemyObservation? Enemy,
    string? RewardId,
    KaguyaRewardObservation? Reward,
    int GoldBefore,
    int GoldAfter,
    IReadOnlyList<string> Events,
    JsonElement Evidence,
    string? PrimaryDigest,
    string? ReplayDigest,
    string? Terminal,
    string? FailureCode = null,
    string? FailureMessage = null,
    string? EventId = null,
    string? EventType = null,
    KaguyaEventObservation? Event = null);

public sealed record KaguyaStage8Request(
    string SnapshotDirectory,
    string SnapshotManifestPath,
    string Seed,
    IReadOnlyList<KaguyaStage8Action> Actions,
    string? WorkerRunManifestPath = null,
    string? NativeBuildManifestPath = null,
    string? ProbeDocumentPath = null,
    string? ReplayProbeDocumentPath = null);

public sealed record KaguyaCapabilityRecord(
    string Kind,
    string Id,
    string Status,
    string? StandardModelId,
    string EvidenceRef,
    string? Reason);

public sealed record KaguyaErrorDenominator(
    int Total,
    int Supported,
    int Unsupported,
    int Failed,
    int Observed,
    int ErrorCount);

public sealed record KaguyaStage8Report(
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
    string BaseLibVersion,
    string Seed,
    KaguyaStage8ActionScript ActionScript,
    IReadOnlyList<WatcherPhaseRecord> Phases,
    IReadOnlyList<KaguyaCapabilityRecord> Capabilities,
    KaguyaErrorDenominator ErrorDenominator,
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
        Schema, Status, StopCode, StopMessage, TargetGameVersion, SnapshotId,
        SnapshotContentSha256, WorkshopId, ModId, ModVersion, BaseLibVersion,
        Seed, ActionScript, Phases, Capabilities, ErrorDenominator,
        Errors = Errors.Select(x => x.StableSummary).ToArray(), PrimaryDigest,
        ReplayDigest, Deterministic, SnapshotWriteBlocked, FormalDataWriteBlocked
    });

    internal static string StableDigest<T>(T value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, ModWorkerJson.Options)))).ToLowerInvariant();
}

public sealed record KaguyaStage8Evaluation(
    KaguyaStage8Report Report,
    ConversionResult? Conversion,
    ModCatalog Catalog,
    DependencyResolution Resolution);

public static class KaguyaStage8Adapter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly string[] RequiredEvents =
    [
        "new-run", "kaguya-encounter-entered", "kaguya-enemy-intent",
        "kaguya-enemy-behavior", "battle-victory-cleanup", "reward-offer",
        "rewards-completed", "kaguya-resource-ledger", "kaguya-cleanup",
        "kaguya-teardown"
    ];

    private static readonly string[] RequiredEventSurfaceEvents =
    [
        "new-run", "kaguya-event-entered", "kaguya-event-option",
        "kaguya-event-result", "kaguya-resource-ledger", "kaguya-cleanup",
        "kaguya-teardown"
    ];

    public static KaguyaStage8ActionScript BuildActionScript(string seed, IReadOnlyList<KaguyaStage8Action> actions)
    {
        var normalizedSeed = seed?.Trim() ?? "";
        var normalized = (actions ?? []).Select(x => new KaguyaStage8Action(
            x.Kind?.Trim() ?? "", x.TargetId?.Trim(), x.ChoiceIndex)).ToArray();
        return new(normalizedSeed, normalized, KaguyaStage8Report.StableDigest(new { Seed = normalizedSeed, Actions = normalized }));
    }

    public static KaguyaStage8Evaluation Evaluate(
        KaguyaStage8Request request,
        KaguyaRuntimeProbe? probe = null,
        KaguyaRuntimeProbe? replayProbe = null)
    {
        var catalog = ModCatalogParser.Parse(request.SnapshotDirectory, request.SnapshotManifestPath, KaguyaStage8Constants.TargetGameVersion);
        var resolution = DependencyResolver.Resolve(catalog, [KaguyaStage8Constants.ModId]);
        var errors = new List<AdapterErrorRecord>();
        var phases = new List<WatcherPhaseRecord>();
        var capabilities = new List<KaguyaCapabilityRecord>();
        var script = BuildActionScript(request.Seed, request.Actions);
        var inputDigest = KaguyaStage8Report.StableDigest(new
        {
            request.SnapshotDirectory, request.SnapshotManifestPath, request.Seed,
            Actions = script.Actions, request.WorkerRunManifestPath,
            request.NativeBuildManifestPath, request.ProbeDocumentPath,
            request.ReplayProbeDocumentPath
        });

        ValidateFixedInputs(request, catalog, resolution, errors);
        ValidateWorkerReference(request, errors);
        phases.Add(new("static-dependency-closure",
            errors.Count == 0 ? WatcherPhaseStatus.Passed : WatcherPhaseStatus.RuntimeFailure,
            "stage8/static-dependency-closure",
            errors.Count == 0 ? "Kaguya -> exact BaseLib 3.4.7 closure" : "fixed input validation failed"));

        ConversionResult? conversion = null;
        var deterministic = false;
        string? primaryDigest = probe?.PrimaryDigest;
        string? replayDigest = replayProbe?.PrimaryDigest ?? probe?.ReplayDigest;

        if (errors.Count == 0 && probe is null)
        {
            AddUnsupported(errors, KaguyaStage8Constants.ModId, "/runtime",
                "No approved Kaguya 0.111.0 native runtime probe was supplied.", "stage8/native-runtime-required");
            AddUnrunPhases(phases, "No runtime probe was available");
        }
        else if (errors.Count == 0 && probe is not null)
        {
            ValidateProbe(probe, request, errors);
            if (errors.Count == 0)
            {
                var isEvent = probe.Event is not null;
                var enemy = probe.Enemy;
                var reward = probe.Reward!;
                var doc = isEvent
                    ? ToEventAdapterDocument(probe, probe.Event!, reward)
                    : ToAdapterDocument(probe, enemy!, reward);
                conversion = ModAdapterConverter.Convert(doc, new ModDefinitionRegistry());
                errors.AddRange(conversion.Errors);
                AddPhasesFromEvents(phases, probe.Events);
                deterministic = IsDeterministic(probe, replayProbe, request);
                if (!deterministic)
                {
                    errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DeterminismMismatch,
                        KaguyaStage8Constants.ModId, "/replay",
                        "primary and new-process replay digests do not match", "stage8/replay"));
                }

                if (isEvent)
                {
                    capabilities.Add(new("Event", probe.Event!.Id, "Mapped", probe.Event.Id,
                        probe.Event.EvidenceRef, null));
                }
                else
                {
                    capabilities.Add(new("Enemy", enemy!.Id, "Mapped", enemy.Id,
                        enemy.EvidenceRef, null));
                    capabilities.Add(new("Encounter", probe.EncounterId!, "Mapped", probe.EncounterId,
                        "native/kaguya/encounter", null));
                }
                capabilities.Add(new("Reward", reward.RewardId, "Mapped", reward.RewardId,
                    reward.EvidenceRef, null));
                capabilities.Add(new("UI", "kaguya-combat-ui", "Unsupported", null,
                    "stage8/native-run", "UI rendering and animation were outside the headless lifecycle slice"));
                capabilities.Add(new("Map", "kaguya-map-injection", "Unsupported", null,
                    "stage8/native-run", "The encounter was entered through the audited native debug room seam; map generation is not claimed"));
                capabilities.Add(new("Save", "kaguya-save-schema", "Unsupported", null,
                    "stage8/isolation", "formal save and load semantics were not exercised"));
            }
            else
            {
                AddUnrunPhases(phases, "runtime probe failed validation");
            }
        }

        var status = errors.Count == 0 && conversion?.IsSuccess == true &&
            phases.All(x => x.Status == WatcherPhaseStatus.Passed)
            ? WatcherStage6Status.Pass
            : errors.Any(x => x.Code == AdapterErrorCode.Unsupported)
                ? WatcherStage6Status.Blocked : WatcherStage6Status.Partial;
        var first = errors.OrderBy(x => x.Code)
            .ThenBy(x => x.ObjectId, StringComparer.Ordinal)
            .ThenBy(x => x.Path, StringComparer.Ordinal)
            .FirstOrDefault();
        var denominator = new KaguyaErrorDenominator(
            probe?.Event is not null ? 5 : 6,
            probe?.Event is not null ? 2 : 3,
            probe?.Event is not null ? 3 : 3,
            errors.Count, probe?.Event is not null || probe?.Enemy is not null ? 3 : 0, errors.Count);
        var report = new KaguyaStage8Report(
            KaguyaStage8Constants.Schema, status, first?.StableCode ?? "",
            first?.Message ?? "Kaguya stage 8 completed",
            KaguyaStage8Constants.TargetGameVersion, KaguyaStage8Constants.SnapshotId,
            KaguyaStage8Constants.SnapshotContentSha256, KaguyaStage8Constants.WorkshopId,
            KaguyaStage8Constants.ModId, KaguyaStage8Constants.ModVersion,
            KaguyaStage8Constants.BaseLibVersion, script.Seed,
            script, phases.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray(),
            capabilities.OrderBy(x => x.Kind, StringComparer.Ordinal)
                .ThenBy(x => x.Id, StringComparer.Ordinal).ToArray(), denominator,
            errors.OrderBy(x => x.Code).ThenBy(x => x.ObjectId, StringComparer.Ordinal)
                .ThenBy(x => x.Path, StringComparer.Ordinal).ToArray(), inputDigest,
            primaryDigest, replayDigest, deterministic, true, true,
            request.WorkerRunManifestPath, request.NativeBuildManifestPath,
            request.ProbeDocumentPath);
        return new(report, conversion, catalog, resolution);
    }

    public static KaguyaRuntimeProbe? ReadProbe(string path) =>
        string.IsNullOrWhiteSpace(path) || !File.Exists(path)
            ? null : JsonSerializer.Deserialize<KaguyaRuntimeProbe>(File.ReadAllText(path), JsonOptions);

    private static void ValidateFixedInputs(KaguyaStage8Request request, ModCatalog catalog,
        DependencyResolution resolution, List<AdapterErrorRecord> errors)
    {
        if (catalog.SnapshotId != KaguyaStage8Constants.SnapshotId)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion,
                KaguyaStage8Constants.ModId, "/snapshotDirectory",
                "snapshot must be the fixed 20261106-163352 snapshot"));
        if (!string.Equals(catalog.SnapshotContentSha256,
                KaguyaStage8Constants.SnapshotContentSha256, StringComparison.OrdinalIgnoreCase))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError,
                KaguyaStage8Constants.ModId, "/snapshotContentSha256",
                "fixed snapshot content hash does not match"));
        if (catalog.TargetGameVersion != KaguyaStage8Constants.TargetGameVersion)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion,
                KaguyaStage8Constants.ModId, "/targetGameVersion",
                "target game version must be exactly 0.111.0"));
        errors.AddRange(catalog.Errors.Select(x => AdapterErrorRecord.Create(
            MapError(x.Code), KaguyaStage8Constants.ModId, x.Path, x.Message,
            "stage8/catalog")));
        var kaguya = catalog.Manifests.FirstOrDefault(x => x.ModId == KaguyaStage8Constants.ModId);
        var baseLib = catalog.Manifests.FirstOrDefault(x => x.ModId == KaguyaStage8Constants.BaseLibId);
        if (kaguya is null || baseLib is null || !resolution.IsSuccess ||
            !resolution.DependencyClosure.Contains(KaguyaStage8Constants.BaseLibId))
        {
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DependencyMissing,
                KaguyaStage8Constants.ModId, "/dependencies",
                "Kaguya exact BaseLib dependency closure is unavailable",
                "stage8/dependency-closure"));
        }
        else
        {
            if (kaguya.Source.WorkshopId != KaguyaStage8Constants.WorkshopId)
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError,
                    KaguyaStage8Constants.ModId, "/workshopId",
                    "Kaguya Workshop identity does not match fixed snapshot"));
            if (kaguya.Version != KaguyaStage8Constants.ModVersion)
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError,
                    KaguyaStage8Constants.ModId, "/version",
                    "Kaguya version does not match fixed snapshot"));
            if (baseLib.Version != KaguyaStage8Constants.BaseLibVersion)
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion,
                    KaguyaStage8Constants.BaseLibId, "/version",
                    "BaseLib must be exactly 3.4.7"));
        }
        if (string.IsNullOrWhiteSpace(request.Seed))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField,
                KaguyaStage8Constants.ModId, "/seed", "fixed seed is required"));
        if (request.Actions is null || request.Actions.Count == 0)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField,
                KaguyaStage8Constants.ModId, "/actions", "fixed action script is required"));
    }

    private static void ValidateProbe(KaguyaRuntimeProbe probe, KaguyaStage8Request request,
        List<AdapterErrorRecord> errors)
    {
        if (!probe.NativeRuntimeAvailable)
            AddUnsupported(errors, KaguyaStage8Constants.ModId, "/runtime",
                probe.FailureMessage ?? "native runtime unavailable",
                probe.FailureCode ?? "stage8/native-runtime");
        if (probe.Schema != KaguyaStage8Constants.ProbeSchema)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.InvalidFormat,
                KaguyaStage8Constants.ModId, "/probe/schema",
                "unexpected Kaguya runtime probe schema"));
        if (probe.TargetGameVersion != KaguyaStage8Constants.TargetGameVersion)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion,
                KaguyaStage8Constants.ModId, "/probe/targetGameVersion",
                "runtime probe target must be exactly 0.111.0"));
        if (probe.SnapshotId != KaguyaStage8Constants.SnapshotId)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError,
                KaguyaStage8Constants.ModId, "/probe/snapshotId",
                "runtime probe snapshot identity does not match"));
        if (probe.Seed != request.Seed)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DeterminismMismatch,
                KaguyaStage8Constants.ModId, "/probe/seed",
                "runtime probe seed does not match fixed action script", "stage8/seed"));
        if (probe.ModId != KaguyaStage8Constants.ModId)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError,
                KaguyaStage8Constants.ModId, "/probe/modId",
                "runtime probe selected a different Mod"));
        if (string.IsNullOrWhiteSpace(probe.NativeBuildId) ||
            string.IsNullOrWhiteSpace(probe.BuildContentKey))
            AddUnsupported(errors, KaguyaStage8Constants.ModId, "/probe/nativeBuild",
                "runtime probe is missing exact native build identity",
                "stage8/native-build-identity");
        if (probe.Terminal != "battle-complete")
            AddUnsupported(errors, KaguyaStage8Constants.ModId, "/probe/terminal",
                probe.FailureMessage ?? "runtime probe did not complete the battle boundary",
                probe.FailureCode ?? "stage8/battle-boundary");
        if (probe.Event is not null)
        {
            if (string.IsNullOrWhiteSpace(probe.Event.Id) ||
                string.IsNullOrWhiteSpace(probe.Event.EvidenceRef) ||
                probe.Event.OptionIds.Count != 2 ||
                string.IsNullOrWhiteSpace(probe.Event.SelectedOptionId))
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField,
                    KaguyaStage8Constants.ModId, "/probe/event",
                    "native event probe must include identity, exactly two options, selection, and evidence"));
        }
        else if (probe.Enemy is null || string.IsNullOrWhiteSpace(probe.Enemy.Id) ||
                 string.IsNullOrWhiteSpace(probe.Enemy.EvidenceRef) ||
                 probe.Enemy.IntentIds.Count == 0)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField,
                KaguyaStage8Constants.ModId, "/probe/enemy",
                "native probe must include enemy identity, evidence, and at least one intent"));
        if (probe.Reward is null || string.IsNullOrWhiteSpace(probe.Reward.RewardId) ||
            string.IsNullOrWhiteSpace(probe.Reward.EvidenceRef))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField,
                KaguyaStage8Constants.ModId, "/probe/reward",
                "native probe must include reward identity and evidence"));
        var requiredEvents = probe.Event is not null ? RequiredEventSurfaceEvents : RequiredEvents;
        foreach (var required in requiredEvents.Where(x => !(probe.Events ?? []).Contains(x,
                     StringComparer.Ordinal)))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.Unsupported,
                KaguyaStage8Constants.ModId, "/probe/events", $"required lifecycle event '{required}' was not observed",
                "stage8/lifecycle"));
    }

    private static ModAdapterDocument ToAdapterDocument(KaguyaRuntimeProbe probe,
        KaguyaEnemyObservation enemy, KaguyaRewardObservation reward) => new(
        KaguyaStage8Constants.ModId, "超时空辉夜姬", KaguyaStage8Constants.ModVersion,
        KaguyaStage8Constants.TargetGameVersion,
        $"{KaguyaStage8Constants.SnapshotId}/{KaguyaStage8Constants.WorkshopId}/Kaguya.json",
        [new("Kaguya.json"), new("Kaguya.dll"), new("Kaguya.pck")],
        [new(KaguyaStage8Constants.BaseLibId, KaguyaStage8Constants.BaseLibVersion)],
        [KaguyaStage8Constants.BaseLibId],
        [
            new("Enemy", enemy.Id, enemy.DisplayName, "observed Kaguya Nay enemy",
                IntentIds: enemy.IntentIds,
                Hooks: [
                    new("enemy-intent", "combat-intent", "native intent snapshot", enemy.EvidenceRef),
                    new("enemy-behavior", "enemy-turn", "native enemy turn state delta", enemy.EvidenceRef),
                    new("enemy-teardown", "combat-end", "native cleanup boundary", "native/kaguya/teardown")
                ]),
            new("Encounter", probe.EncounterId, "Nay encounter", "observed Kaguya encounter",
                EnemyIds: [enemy.Id]),
            new("Reward", reward.RewardId, "Native encounter reward", "observed room-end reward",
                ContentIds: reward.CardId is null ? [] : [reward.CardId],
                ResourceResults: reward.GoldAmount.HasValue
                    ? [new("gold", reward.GoldAmount.Value, "native reward ledger")] : [])
        ],
        probe.GoldAfter > probe.GoldBefore
            ? [new("gold", probe.GoldAfter - probe.GoldBefore, "native encounter reward ledger")]
            : []);

    private static ModAdapterDocument ToEventAdapterDocument(KaguyaRuntimeProbe probe,
        KaguyaEventObservation ev, KaguyaRewardObservation reward) => new(
        KaguyaStage8Constants.ModId, "超时空辉夜姬", KaguyaStage8Constants.ModVersion,
        KaguyaStage8Constants.TargetGameVersion,
        $"{KaguyaStage8Constants.SnapshotId}/{KaguyaStage8Constants.WorkshopId}/Kaguya.json",
        [new("Kaguya.json"), new("Kaguya.dll"), new("Kaguya.pck")],
        [new(KaguyaStage8Constants.BaseLibId, KaguyaStage8Constants.BaseLibVersion)],
        [KaguyaStage8Constants.BaseLibId],
        [
            new("Event", ev.Id, ev.DisplayName, "observed Kaguya event",
                OptionIds: ev.OptionIds,
                Hooks: [
                    new("event-option", "OnEventOptionSelected", "native event option selection", ev.EvidenceRef),
                    new("event-result", "OnRewardGenerated", "native event result", ev.EvidenceRef)
                ]),
            new("Reward", reward.RewardId, "Native event result", "observed event resource result",
                ContentIds: reward.CardId is null ? [] : [reward.CardId],
                ResourceResults: reward.GoldAmount.HasValue
                    ? [new("gold", reward.GoldAmount.Value, "native event ledger")] : [])
        ],
        probe.GoldAfter > probe.GoldBefore
            ? [new("gold", probe.GoldAfter - probe.GoldBefore, "native event resource ledger")]
            : []);

    private static bool IsDeterministic(KaguyaRuntimeProbe probe,
        KaguyaRuntimeProbe? replay, KaguyaStage8Request request) =>
        replay is not null && probe.PrimaryDigest is not null &&
        replay.PrimaryDigest == probe.PrimaryDigest && replay.Seed == request.Seed &&
        replay.EncounterId == probe.EncounterId && replay.EnemyId == probe.EnemyId &&
        replay.RewardId == probe.RewardId && replay.GoldAfter == probe.GoldAfter;

    private static void AddPhasesFromEvents(List<WatcherPhaseRecord> phases,
        IReadOnlyList<string> events)
    {
        foreach (var (name, required) in new[]
        {
            ("lifecycle-start", "new-run"),
            ("encounter-execution", events.Contains("kaguya-event-result", StringComparer.Ordinal) ? "kaguya-event-result" : "kaguya-enemy-behavior"),
            ("reward-ledger", "kaguya-resource-ledger"),
            ("lifecycle-teardown", "kaguya-teardown")
        })
            phases.Add(new(name, events.Contains(required, StringComparer.Ordinal)
                ? WatcherPhaseStatus.Passed : WatcherPhaseStatus.RuntimeFailure,
                "stage8/" + name));
    }

    private static void AddUnrunPhases(List<WatcherPhaseRecord> phases, string message)
    {
        foreach (var name in new[] { "encounter-execution", "reward-ledger", "lifecycle-teardown" })
            phases.Add(new(name, WatcherPhaseStatus.NotRun, "stage8/" + name, message));
    }

    private static void ValidateWorkerReference(KaguyaStage8Request request,
        List<AdapterErrorRecord> errors)
    {
        var lab = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var buildRoot = Path.Combine(lab, ".build") + Path.DirectorySeparatorChar;
        foreach (var path in new[] { request.WorkerRunManifestPath,
                     request.NativeBuildManifestPath, request.ProbeDocumentPath,
                     request.ReplayProbeDocumentPath }.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var full = Path.GetFullPath(path!);
            if (!full.StartsWith(buildRoot, StringComparison.OrdinalIgnoreCase) ||
                full.Contains("\\steamapps\\workshop\\", StringComparison.OrdinalIgnoreCase) ||
                full.Contains("\\userdata\\", StringComparison.OrdinalIgnoreCase) ||
                full.Contains("\\saves\\", StringComparison.OrdinalIgnoreCase) ||
                full.Contains("\\settings\\", StringComparison.OrdinalIgnoreCase))
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError,
                    KaguyaStage8Constants.ModId, "/runtimeEvidence", "runtime evidence must stay inside .build and formal data is forbidden", "stage8/isolation"));
        }
    }

    private static void AddUnsupported(List<AdapterErrorRecord> errors, string objectId,
        string path, string message, string evidence) => errors.Add(
        AdapterErrorRecord.Create(AdapterErrorCode.Unsupported, objectId, path, message, evidence));

    private static AdapterErrorCode MapError(ModCatalogErrorCode code) => code switch
    {
        ModCatalogErrorCode.MissingFile => AdapterErrorCode.MissingFile,
        ModCatalogErrorCode.InvalidFormat => AdapterErrorCode.InvalidFormat,
        ModCatalogErrorCode.UnsupportedVersion => AdapterErrorCode.UnsupportedVersion,
        ModCatalogErrorCode.MissingField => AdapterErrorCode.MissingField,
        ModCatalogErrorCode.InvalidValue => AdapterErrorCode.InvalidValue,
        ModCatalogErrorCode.DuplicateId => AdapterErrorCode.DuplicateId,
        ModCatalogErrorCode.DependencyMissing => AdapterErrorCode.DependencyMissing,
        _ => AdapterErrorCode.DataError
    };
}
