using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SimulatorLab.Core.Mods;

/// <summary>
/// Stage 9 keeps minion semantics in the simulator's vocabulary.  The native
/// probe only supplies scalar observations; MinionLib and Shadowverse types do
/// not cross this boundary.
/// </summary>
public static class MinionStage9Constants
{
    public const string Schema = "minion-stage9/v1";
    public const string ProbeSchema = "minion-stage9-runtime-probe/v1";
    public const string TargetGameVersion = "0.111.0";
    public const string SnapshotId = "workshop-snapshot-20261106-163352";
    public const string SnapshotContentSha256 = "7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73";
    public const string MinionWorkshopId = "3747595039";
    public const string MinionLibId = "MinionLib";
    public const string MinionLibVersion = "0.6.3";
    public const string RitsuWorkshopId = "3747602295";
    public const string RitsuLibId = "STS2-RitsuLib";
    public const string RitsuLibVersion = "0.6.10";
    public const string ShadowverseWorkshopId = "3747644378";
    public const string ShadowverseModId = "shadowversebydmod";
    public const string ShadowverseVersion = "1.6.0";
}

public sealed record MinionDefinition(
    string Id,
    string DisplayName,
    string OwnerId,
    int MaxHp,
    IReadOnlyList<HookCapability> Hooks,
    string EvidenceRef);

public sealed record MinionLifecycleObservation(
    string Id,
    string DisplayName,
    string OwnerId,
    string UniqueIdentity,
    int InitialHp,
    int FinalHp,
    bool Registered,
    bool Summoned,
    bool ComponentApplied,
    bool CombatEnded,
    bool CleanedUp,
    string EvidenceRef);

public sealed record MinionComponentObservation(
    string CardId,
    string ComponentId,
    string ResultKind,
    int Amount,
    string EvidenceRef);

public sealed record MinionStage9Action(string Kind, string? CardId = null, string? MinionId = null);

public sealed record MinionStage9ActionScript(
    string Seed,
    IReadOnlyList<MinionStage9Action> Actions,
    string Digest);

public sealed record MinionRuntimeProbe(
    string Schema,
    bool NativeRuntimeAvailable,
    string? TargetGameVersion,
    string? SnapshotId,
    string? Seed,
    string? NativeBuildId,
    string? BuildContentKey,
    bool MinionLibInitialized,
    bool ShadowverseAssemblyLoaded,
    bool ShadowverseContentRegistered,
    bool NativeGameplayObserved,
    IReadOnlyList<string> LoadOrder,
    IReadOnlyList<string> Events,
    MinionLifecycleObservation? Minion,
    MinionComponentObservation? Component,
    bool SnapshotWriteBlocked,
    bool FormalDataWriteBlocked,
    string? PrimaryDigest,
    string? ReplayDigest,
    string? Terminal,
    string? FailureCode = null,
    string? FailureMessage = null,
    MinionRealCombatObservation? RealCombat = null,
    IReadOnlyList<MinionCombatEvent>? RealCombatEvents = null);

/// <summary>
/// Scalar evidence for the stage 10 adapter boundary.  This is deliberately
/// independent of MinionLib, Shadowverse and Godot types; the native process
/// converts those objects before serializing this record.
/// </summary>
public sealed record MinionRealCombatObservation(
    string MinionType,
    string MinionModelId,
    string MinionCreatureId,
    int SummonedHp,
    int HpAfterDamage,
    int HpAfterDeath,
    bool AliveAfterDeath,
    bool RemovedFromCombat,
    bool RemovedFromPets,
    string ComponentCardType,
    string ComponentCardId,
    string ComponentId,
    int ComponentAmount,
    int TargetHpBeforeComponent,
    int TargetHpAfterComponent,
    bool ComponentApplied,
    bool CombatEnded,
    bool CleanupConfirmed,
    string EvidenceRef);

public sealed record MinionCombatEvent(
    string Kind,
    string Subject,
    string? ModelId,
    int? HpBefore,
    int? HpAfter,
    string EvidenceRef);

public sealed record MinionFixtureProbe(
    string Schema,
    string Seed,
    IReadOnlyList<string> Events,
    MinionLifecycleObservation Minion,
    MinionComponentObservation Component,
    string Digest);

public sealed record MinionStage9Request(
    string SnapshotDirectory,
    string SnapshotManifestPath,
    string Seed,
    IReadOnlyList<MinionStage9Action> Actions,
    string? WorkerRunManifestPath = null,
    string? NativeBuildManifestPath = null,
    string? ProbeDocumentPath = null,
    string? ReplayProbeDocumentPath = null);

public sealed record MinionCapabilityRecord(
    string Kind,
    string Id,
    string Status,
    string? StandardModelId,
    string EvidenceRef,
    string? Reason);

public sealed record MinionErrorDenominator(
    int Total,
    int Supported,
    int Unsupported,
    int Failed,
    int Observed,
    int ErrorCount);

public sealed record MinionStage9Report(
    string Schema,
    WatcherStage6Status Status,
    string StopCode,
    string StopMessage,
    string TargetGameVersion,
    string SnapshotId,
    string SnapshotContentSha256,
    string MinionLibVersion,
    string RitsuLibVersion,
    string ShadowverseVersion,
    string Seed,
    MinionStage9ActionScript ActionScript,
    IReadOnlyList<WatcherPhaseRecord> Phases,
    IReadOnlyList<MinionCapabilityRecord> Capabilities,
    MinionErrorDenominator ErrorDenominator,
    IReadOnlyList<AdapterErrorRecord> Errors,
    string InputDigest,
    string FixtureDigest,
    string? PrimaryDigest,
    string? ReplayDigest,
    bool Deterministic,
    bool SnapshotWriteBlocked,
    bool FormalDataWriteBlocked,
    string? WorkerRunManifestPath,
    string? NativeBuildManifestPath,
    string? ProbeDocumentPath,
    string? ReplayProbeDocumentPath)
{
    public string DeterminismDigest => StableDigest(new
    {
        Schema, Status, StopCode, StopMessage, TargetGameVersion, SnapshotId,
        SnapshotContentSha256, MinionLibVersion, RitsuLibVersion, ShadowverseVersion,
        Seed, ActionScript, Phases, Capabilities,
        Errors = Errors.Select(x => x.StableSummary).ToArray(),
        ErrorDenominator, FixtureDigest, PrimaryDigest, ReplayDigest,
        Deterministic, SnapshotWriteBlocked, FormalDataWriteBlocked
    });

    internal static string StableDigest<T>(T value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, ModWorkerJson.Options)))).ToLowerInvariant();
}

public sealed record MinionStage9Evaluation(
    MinionStage9Report Report,
    ConversionResult? Conversion,
    MinionDefinition? MinionModel,
    MinionFixtureProbe Fixture,
    ModCatalog Catalog,
    DependencyResolution Resolution);

public static class MinionStage9Fixture
{
    public const string Schema = "minion-fixture/v1";

    public static MinionFixtureProbe Run(string seed)
    {
        var normalizedSeed = seed?.Trim() ?? "";
        const string id = "MINION.SHADOWVERSE_FIXTURE_SCOUT";
        const string unique = "MINION.SHADOWVERSE_FIXTURE_SCOUT#1";
        var events = new[] { "register", "summon", "component-applied", "combat-end", "cleanup" };
        var minion = new MinionLifecycleObservation(
            id, "Fixture Scout", "CHARACTER.SHADOWVERSE_FIXTURE_OWNER", unique,
            12, 8, true, true, true, true, true, "fixture/minion-lifecycle");
        var component = new MinionComponentObservation(
            "CARD.SHADOWVERSE_FIXTURE_COMPONENT", "COMPONENT.SHADOWVERSE_FIXTURE_SCOUT",
            "damage", 4, "fixture/component-card");
        var digest = StableDigest(new { Schema, normalizedSeed, events, minion, component });
        return new(Schema, normalizedSeed, events, minion, component, digest);
    }

    private static string StableDigest<T>(T value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, ModWorkerJson.Options)))).ToLowerInvariant();
}

public static class MinionStage9Adapter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly string[] RequiredFixtureEvents =
        ["register", "summon", "component-applied", "combat-end", "cleanup"];

    public static MinionStage9ActionScript BuildActionScript(string seed, IReadOnlyList<MinionStage9Action> actions)
    {
        var normalizedSeed = seed?.Trim() ?? "";
        var normalized = (actions ?? []).Select(x => new MinionStage9Action(
            x.Kind?.Trim() ?? "", x.CardId?.Trim(), x.MinionId?.Trim())).ToArray();
        return new(normalizedSeed, normalized,
            MinionStage9Report.StableDigest(new { Seed = normalizedSeed, Actions = normalized }));
    }

    public static MinionStage9Evaluation Evaluate(
        MinionStage9Request request,
        MinionRuntimeProbe? probe = null,
        MinionRuntimeProbe? replayProbe = null,
        MinionFixtureProbe? fixture = null,
        MinionFixtureProbe? replayFixture = null)
    {
        var catalog = ModCatalogParser.Parse(request.SnapshotDirectory, request.SnapshotManifestPath,
            MinionStage9Constants.TargetGameVersion);
        var resolution = DependencyResolver.Resolve(catalog, [MinionStage9Constants.ShadowverseModId]);
        var errors = new List<AdapterErrorRecord>();
        var phases = new List<WatcherPhaseRecord>();
        var capabilities = new List<MinionCapabilityRecord>();
        var script = BuildActionScript(request.Seed, request.Actions);
        var inputDigest = MinionStage9Report.StableDigest(new
        {
            request.SnapshotDirectory, request.SnapshotManifestPath, request.Seed,
            Actions = script.Actions, request.WorkerRunManifestPath,
            request.NativeBuildManifestPath, request.ProbeDocumentPath,
            request.ReplayProbeDocumentPath
        });
        var fixtureResult = fixture ?? MinionStage9Fixture.Run(request.Seed);
        var fixtureReplayResult = replayFixture ?? MinionStage9Fixture.Run(request.Seed);

        ValidateFixedInputs(request, catalog, resolution, errors);
        ValidateWorkerReference(request, errors);
        phases.Add(new("static-dependency-closure",
            errors.Count == 0 ? WatcherPhaseStatus.Passed : WatcherPhaseStatus.RuntimeFailure,
            "stage9/static-dependency-closure",
            errors.Count == 0 ? "Shadowverse -> exact RitsuLib 0.111.0 + MinionLib closure" : "fixed input validation failed"));

        var deterministicFixture = fixtureResult.Digest == fixtureReplayResult.Digest &&
            fixtureResult.Seed == request.Seed;
        if (!deterministicFixture)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DeterminismMismatch,
                MinionStage9Constants.MinionLibId, "/fixture/replay",
                "Minion fixture replay digest does not match", "stage9/fixture-replay"));
        ValidateFixture(fixtureResult, request, errors);
        capabilities.Add(new("Minion", fixtureResult.Minion.Id, "Mapped",
            fixtureResult.Minion.Id, fixtureResult.Minion.EvidenceRef, null));
        capabilities.Add(new("ComponentCard", fixtureResult.Component.CardId, "Mapped",
            fixtureResult.Component.CardId, fixtureResult.Component.EvidenceRef, null));
        capabilities.Add(new("MinionLifecycle", fixtureResult.Minion.UniqueIdentity,
            fixtureResult.Minion.CleanedUp ? "Mapped" : "Unsupported",
            fixtureResult.Minion.CleanedUp ? fixtureResult.Minion.Id : null,
            fixtureResult.Minion.EvidenceRef,
            fixtureResult.Minion.CleanedUp ? null : "combat cleanup was not observed"));

        string? primaryDigest = probe?.PrimaryDigest;
        string? replayDigest = replayProbe?.PrimaryDigest ?? probe?.ReplayDigest;
        var nativeReady = false;
        ConversionResult? conversion = null;
        MinionDefinition? minionModel = null;
        if (errors.Count == 0 && probe is null)
        {
            AddUnsupported(errors, MinionStage9Constants.ShadowverseModId, "/runtime",
                "No isolated MinionLib/Shadowverse runtime probe was supplied.", "stage9/native-runtime-required");
            AddUnrunPhases(phases, "No runtime probe was available");
        }
        else if (errors.Count == 0 && probe is not null)
        {
            ValidateProbe(probe, request, errors);
            if (errors.Count == 0)
            {
                nativeReady = true;
                AddPhasesFromEvents(phases, probe.Events);
                var replayStable = IsDeterministic(probe, replayProbe, request);
                if (!replayStable)
                    errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DeterminismMismatch,
                        MinionStage9Constants.ShadowverseModId, "/replay",
                        "native primary and new-process replay digests do not match", "stage9/replay"));

                var observed = probe.Minion ?? fixtureResult.Minion;
                minionModel = new(observed.Id, observed.DisplayName, observed.OwnerId,
                    observed.InitialHp, [new("OnSummon", "OnSummon", true,
                        "native Minion registration and summon lifecycle", observed.EvidenceRef),
                        new("OnCombatEnd", "OnCombatEnd", true,
                            "native combat-end cleanup boundary", observed.EvidenceRef)], observed.EvidenceRef);
                var component = probe.Component ?? fixtureResult.Component;
                var document = ToAdapterDocument(probe, observed, component);
                conversion = ModAdapterConverter.Convert(document, new ModDefinitionRegistry());
                errors.AddRange(conversion.Errors);
                capabilities.Add(new("ShadowverseRegistration",
                    MinionStage9Constants.ShadowverseModId,
                    probe.ShadowverseContentRegistered ? "Mapped" : "Unsupported",
                    probe.ShadowverseContentRegistered ? MinionStage9Constants.ShadowverseModId : null,
                    "stage9/native-loader",
                    probe.ShadowverseContentRegistered ? null : "Shadowverse content registration was not observed"));
                if (!probe.ShadowverseContentRegistered)
                    AddUnsupported(errors, MinionStage9Constants.ShadowverseModId, "/runtime/content-registration",
                        "Shadowverse content registration was not observed in the isolated process.", "stage9/native-loader");
                if (!probe.NativeGameplayObserved)
                    capabilities.Add(new("Gameplay", "shadowverse-minion-combat", "Unsupported", null,
                        "stage9/native-loader", "a real Shadowverse minion combat was outside the bounded slice"));
            }
            else
                AddUnrunPhases(phases, "runtime probe failed validation");
        }

        var status = errors.Count == 0 && nativeReady && conversion?.IsSuccess == true &&
            phases.All(x => x.Status == WatcherPhaseStatus.Passed)
            ? WatcherStage6Status.Pass
            : errors.Any(x => x.Code == AdapterErrorCode.Unsupported)
                ? WatcherStage6Status.Blocked : WatcherStage6Status.Partial;
        var first = errors.OrderBy(x => x.Code).ThenBy(x => x.ObjectId, StringComparer.Ordinal)
            .ThenBy(x => x.Path, StringComparer.Ordinal).FirstOrDefault();
        var report = new MinionStage9Report(
            MinionStage9Constants.Schema, status, first?.StableCode ?? "",
            first?.Message ?? "Minion stage 9 completed",
            MinionStage9Constants.TargetGameVersion, MinionStage9Constants.SnapshotId,
            MinionStage9Constants.SnapshotContentSha256, MinionStage9Constants.MinionLibVersion,
            MinionStage9Constants.RitsuLibVersion, MinionStage9Constants.ShadowverseVersion,
            script.Seed, script, phases.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray(),
            capabilities.OrderBy(x => x.Kind, StringComparer.Ordinal)
                .ThenBy(x => x.Id, StringComparer.Ordinal).ToArray(),
            new(RequiredFixtureEvents.Length + 4,
                fixtureResult.Minion.CleanedUp && fixtureResult.Component.Amount > 0 ? 5 : 0,
                errors.Count(x => x.Code == AdapterErrorCode.Unsupported), errors.Count,
                probe?.Minion is not null ? 1 : 0, errors.Count),
            errors.OrderBy(x => x.Code).ThenBy(x => x.ObjectId, StringComparer.Ordinal)
                .ThenBy(x => x.Path, StringComparer.Ordinal).ToArray(), inputDigest,
            fixtureResult.Digest, primaryDigest, replayDigest,
            deterministicFixture && probe is not null && IsDeterministic(probe, replayProbe, request),
            probe?.SnapshotWriteBlocked ?? true, probe?.FormalDataWriteBlocked ?? true,
            request.WorkerRunManifestPath, request.NativeBuildManifestPath,
            request.ProbeDocumentPath, request.ReplayProbeDocumentPath);
        return new(report, conversion, minionModel, fixtureResult, catalog, resolution);
    }

    public static MinionRuntimeProbe? ReadProbe(string path) =>
        string.IsNullOrWhiteSpace(path) || !File.Exists(path)
            ? null : JsonSerializer.Deserialize<MinionRuntimeProbe>(File.ReadAllText(path), JsonOptions);

    private static void ValidateFixedInputs(MinionStage9Request request, ModCatalog catalog,
        DependencyResolution resolution, List<AdapterErrorRecord> errors)
    {
        if (catalog.SnapshotId != MinionStage9Constants.SnapshotId)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion,
                MinionStage9Constants.ShadowverseModId, "/snapshotDirectory", "snapshot identity drifted"));
        if (!string.Equals(catalog.SnapshotContentSha256, MinionStage9Constants.SnapshotContentSha256,
                StringComparison.OrdinalIgnoreCase))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError,
                MinionStage9Constants.ShadowverseModId, "/snapshotContentSha256", "fixed snapshot content hash does not match"));
        if (catalog.TargetGameVersion != MinionStage9Constants.TargetGameVersion)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion,
                MinionStage9Constants.ShadowverseModId, "/targetGameVersion", "target game version must be exactly 0.111.0"));
        errors.AddRange(catalog.Errors.Select(x => AdapterErrorRecord.Create(MapError(x.Code),
            MinionStage9Constants.ShadowverseModId, x.Path, x.Message, "stage9/catalog")));
        var shadowverse = catalog.Manifests.FirstOrDefault(x => x.ModId == MinionStage9Constants.ShadowverseModId);
        var minion = catalog.Manifests.FirstOrDefault(x => x.ModId == MinionStage9Constants.MinionLibId);
        var ritsu = catalog.Manifests.FirstOrDefault(x => x.ModId == MinionStage9Constants.RitsuLibId);
        if (shadowverse is null || minion is null || ritsu is null || !resolution.IsSuccess ||
            !resolution.DependencyClosure.Contains(MinionStage9Constants.MinionLibId) ||
            !resolution.DependencyClosure.Contains(MinionStage9Constants.RitsuLibId))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DependencyMissing,
                MinionStage9Constants.ShadowverseModId, "/dependencies",
                "Shadowverse exact RitsuLib + MinionLib dependency closure is unavailable", "stage9/dependency-closure"));
        else
        {
            if (shadowverse.Source.WorkshopId != MinionStage9Constants.ShadowverseWorkshopId || shadowverse.Version != MinionStage9Constants.ShadowverseVersion)
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, MinionStage9Constants.ShadowverseModId, "/identity", "Shadowverse identity does not match fixed snapshot"));
            if (minion.Source.WorkshopId != MinionStage9Constants.MinionWorkshopId || minion.Version != MinionStage9Constants.MinionLibVersion)
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, MinionStage9Constants.MinionLibId, "/identity", "MinionLib identity does not match fixed snapshot"));
            if (ritsu.Source.WorkshopId != MinionStage9Constants.RitsuWorkshopId || ritsu.Version != MinionStage9Constants.RitsuLibVersion)
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, MinionStage9Constants.RitsuLibId, "/identity", "RitsuLib identity does not match fixed snapshot"));
            if (!resolution.SelectedVariants.Any(x => x.ModId == MinionStage9Constants.RitsuLibId && x.RelativeDirectory == "compat/0.111.0"))
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion, MinionStage9Constants.RitsuLibId, "/compatibility", "RitsuLib must select compat/0.111.0 exactly"));
            if (minion.MinimumGameVersion is { } minGame && CompareVersions(minGame, MinionStage9Constants.TargetGameVersion) > 0)
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion, MinionStage9Constants.MinionLibId, "/min_game_version", "MinionLib minimum game version exceeds target"));
        }
        if (string.IsNullOrWhiteSpace(request.Seed))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, MinionStage9Constants.ShadowverseModId, "/seed", "fixed seed is required"));
        if (request.Actions is null || request.Actions.Count == 0)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, MinionStage9Constants.ShadowverseModId, "/actions", "fixed action script is required"));
    }

    private static void ValidateWorkerReference(MinionStage9Request request, List<AdapterErrorRecord> errors)
    {
        if (string.IsNullOrWhiteSpace(request.WorkerRunManifestPath) || !File.Exists(request.WorkerRunManifestPath))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingFile, MinionStage9Constants.ShadowverseModId,
                "/workerRunManifestPath", "stage 9 worker evidence is required", "stage9/worker-reference"));
    }

    private static void ValidateProbe(MinionRuntimeProbe probe, MinionStage9Request request, List<AdapterErrorRecord> errors)
    {
        if (!probe.NativeRuntimeAvailable)
            AddUnsupported(errors, MinionStage9Constants.ShadowverseModId, "/runtime", probe.FailureMessage ?? "native runtime unavailable", probe.FailureCode ?? "stage9/native-runtime");
        if (probe.Schema != MinionStage9Constants.ProbeSchema)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.InvalidFormat, MinionStage9Constants.ShadowverseModId, "/probe/schema", "unexpected Minion runtime probe schema"));
        if (probe.TargetGameVersion != MinionStage9Constants.TargetGameVersion)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion, MinionStage9Constants.ShadowverseModId, "/probe/targetGameVersion", "runtime probe target must be exactly 0.111.0"));
        if (probe.SnapshotId != MinionStage9Constants.SnapshotId)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, MinionStage9Constants.ShadowverseModId, "/probe/snapshotId", "runtime probe snapshot identity does not match"));
        if (probe.Seed != request.Seed)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DeterminismMismatch, MinionStage9Constants.ShadowverseModId, "/probe/seed", "runtime probe seed does not match fixed action script", "stage9/seed"));
        if (string.IsNullOrWhiteSpace(probe.NativeBuildId) || string.IsNullOrWhiteSpace(probe.BuildContentKey))
            AddUnsupported(errors, MinionStage9Constants.ShadowverseModId, "/probe/nativeBuild", "runtime probe is missing exact native build identity", "stage9/native-build-identity");
        if (probe.Terminal is not ("battle-complete" or "lifecycle-complete"))
            AddUnsupported(errors, MinionStage9Constants.ShadowverseModId, "/probe/terminal", probe.FailureMessage ?? "runtime probe did not complete its lifecycle boundary", probe.FailureCode ?? "stage9/boundary");
        if (!probe.MinionLibInitialized || !probe.ShadowverseAssemblyLoaded)
            AddUnsupported(errors, MinionStage9Constants.ShadowverseModId, "/probe/loader", "MinionLib and Shadowverse assemblies were not both loaded", "stage9/native-loader");
        if (probe.Minion is null || probe.Component is null)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, MinionStage9Constants.ShadowverseModId, "/probe/observations", "minion and component observations are required"));
        if (!probe.SnapshotWriteBlocked || !probe.FormalDataWriteBlocked)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, MinionStage9Constants.ShadowverseModId, "/probe/isolation", "native probe did not prove write isolation", "stage9/isolation"));
    }

    private static void ValidateFixture(MinionFixtureProbe fixture, MinionStage9Request request, List<AdapterErrorRecord> errors)
    {
        if (fixture.Schema != MinionStage9Fixture.Schema || fixture.Seed != request.Seed)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.InvalidFormat, MinionStage9Constants.MinionLibId, "/fixture", "fixture schema or seed does not match"));
        foreach (var required in RequiredFixtureEvents)
            if (!fixture.Events.Contains(required, StringComparer.Ordinal))
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.Unsupported, MinionStage9Constants.MinionLibId, "/fixture/events", $"fixture event '{required}' is missing", "stage9/fixture"));
        if (!fixture.Minion.Registered || !fixture.Minion.Summoned || !fixture.Minion.ComponentApplied || !fixture.Minion.CombatEnded || !fixture.Minion.CleanedUp)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.Unsupported, fixture.Minion.Id, "/fixture/lifecycle", "fixture did not complete registration, summon, combat-end, and cleanup", fixture.Minion.EvidenceRef));
        if (string.IsNullOrWhiteSpace(fixture.Minion.UniqueIdentity) || fixture.Component.Amount <= 0)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.InvalidValue, MinionStage9Constants.MinionLibId, "/fixture/identity", "fixture identity or component result is invalid", "stage9/fixture"));
    }

    private static ModAdapterDocument ToAdapterDocument(MinionRuntimeProbe probe, MinionLifecycleObservation minion, MinionComponentObservation component) =>
        new(MinionStage9Constants.ShadowverseModId, "Shadowverse Beyond Mod", MinionStage9Constants.ShadowverseVersion,
            MinionStage9Constants.TargetGameVersion, "stage9/native/shadowversebydmod",
            [new("shadowversebydmod.dll"), new("shadowversebydmod.pck")],
            [new(MinionStage9Constants.RitsuLibId, "0.6.2"), new(MinionStage9Constants.MinionLibId, "0.6.2")],
            probe.LoadOrder,
            [new("Enemy", minion.Id, minion.DisplayName, "scalar minion observation", Hooks:
                [new("OnCombatStart", "OnCombatStart", "minion summon boundary", minion.EvidenceRef), new("OnCombatEnd", "OnCombatEnd", "minion cleanup", minion.EvidenceRef)]),
             new("Card", component.CardId, component.CardId, "scalar component card observation", Cost: 1,
                 ResourceResults: [new(component.ResultKind, component.Amount, component.EvidenceRef)])],
            [], 8);

    private static void AddPhasesFromEvents(List<WatcherPhaseRecord> phases, IReadOnlyList<string>? events)
    {
        foreach (var name in new[] { "native-loader", "minion-register", "minion-summon", "component-result", "combat-end", "cleanup" })
        {
            var found = events?.Contains(name, StringComparer.Ordinal) == true;
            phases.Add(new(name, found ? WatcherPhaseStatus.Passed : WatcherPhaseStatus.Unsupported,
                $"stage9/native/{name}", found ? null : "native event was not observed"));
        }
    }

    private static void AddUnrunPhases(List<WatcherPhaseRecord> phases, string message)
    {
        foreach (var name in new[] { "native-loader", "minion-register", "minion-summon", "component-result", "combat-end", "cleanup" })
            if (!phases.Any(x => x.Name == name)) phases.Add(new(name, WatcherPhaseStatus.Unsupported, $"stage9/event/{name}", message));
    }

    private static bool IsDeterministic(MinionRuntimeProbe primary, MinionRuntimeProbe? replay, MinionStage9Request request) =>
        replay is not null && !string.IsNullOrWhiteSpace(primary.PrimaryDigest) &&
        primary.PrimaryDigest == replay.PrimaryDigest && primary.Seed == request.Seed &&
        primary.SnapshotId == MinionStage9Constants.SnapshotId;

    private static void AddUnsupported(List<AdapterErrorRecord> errors, string id, string path, string message, string evidence) =>
        errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.Unsupported, id, path, message, evidence));

    private static AdapterErrorCode MapError(ModCatalogErrorCode code) => code switch
    {
        ModCatalogErrorCode.MissingFile => AdapterErrorCode.MissingFile,
        ModCatalogErrorCode.InvalidFormat => AdapterErrorCode.InvalidFormat,
        ModCatalogErrorCode.UnsupportedVersion => AdapterErrorCode.UnsupportedVersion,
        ModCatalogErrorCode.MissingField => AdapterErrorCode.MissingField,
        ModCatalogErrorCode.InvalidValue => AdapterErrorCode.InvalidValue,
        ModCatalogErrorCode.DuplicateId => AdapterErrorCode.DuplicateId,
        ModCatalogErrorCode.DependencyMissing => AdapterErrorCode.DependencyMissing,
        ModCatalogErrorCode.PathEscape => AdapterErrorCode.DataError,
        _ => AdapterErrorCode.RuntimeFailure
    };

    private static int CompareVersions(string left, string right)
    {
        static int[] Parts(string value) => value.TrimStart('v', 'V').Split('-', 2)[0].Split('.').Select(int.Parse).ToArray();
        var a = Parts(left); var b = Parts(right);
        for (var i = 0; i < 3; i++) { var c = a[i].CompareTo(b[i]); if (c != 0) return c; }
        return 0;
    }
}
