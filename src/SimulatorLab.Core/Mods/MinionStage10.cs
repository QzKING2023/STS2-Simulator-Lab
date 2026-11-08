using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SimulatorLab.Core.Mods;

public static class MinionStage10Constants
{
    public const string Schema = "minion-stage10/v1";
    public const string ProbeSchema = "minion-stage10-runtime-probe/v1";
    public const string RealMinionType = "shadowversebydmod.Scripts.Monsters.KnightSoldierMinion";
    public const string RealComponentCardType = "shadowversebydmod.Scripts.Cards.CardCraft.Portalcraft.AncientArtifact";
    public const string RealComponentId = "shadowversebydmod.StrikeComponent";
}

public sealed record MinionStage10Request(
    string SnapshotDirectory,
    string SnapshotManifestPath,
    string Seed,
    string? WorkerRunManifestPath = null,
    string? NativeBuildManifestPath = null,
    string? ProbeDocumentPath = null,
    string? ReplayProbeDocumentPath = null,
    string? FailureProbeDocumentPath = null);

public sealed record MinionStage10Report(
    string Schema,
    WatcherStage6Status Status,
    string StopCode,
    string StopMessage,
    string TargetGameVersion,
    string SnapshotId,
    string SnapshotContentSha256,
    string MinionLibVersion,
    string RitsuLibVariant,
    string ShadowverseVersion,
    string Seed,
    IReadOnlyList<WatcherPhaseRecord> Phases,
    IReadOnlyList<MinionCapabilityRecord> Capabilities,
    MinionErrorDenominator ErrorDenominator,
    IReadOnlyList<AdapterErrorRecord> Errors,
    string InputDigest,
    string? PrimaryDigest,
    string? ReplayDigest,
    string? FailureDigest,
    bool Deterministic,
    bool FailureIsolated,
    bool SnapshotWriteBlocked,
    bool FormalDataWriteBlocked,
    string? WorkerRunManifestPath,
    string? NativeBuildManifestPath,
    string? ProbeDocumentPath,
    string? ReplayProbeDocumentPath,
    string? FailureProbeDocumentPath)
{
    public string DeterminismDigest => StableDigest(new
    {
        Schema, Status, StopCode, StopMessage, TargetGameVersion, SnapshotId,
        SnapshotContentSha256, MinionLibVersion, RitsuLibVariant, ShadowverseVersion,
        Seed, Phases, Capabilities, ErrorDenominator,
        Errors = Errors.Select(x => x.StableSummary).ToArray(),
        PrimaryDigest, ReplayDigest, FailureDigest, Deterministic, FailureIsolated,
        SnapshotWriteBlocked, FormalDataWriteBlocked
    });

    internal static string StableDigest<T>(T value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, ModWorkerJson.Options)))).ToLowerInvariant();
}

public sealed record MinionStage10Evaluation(
    MinionStage10Report Report,
    ModCatalog Catalog,
    DependencyResolution Resolution,
    MinionRuntimeProbe? Primary,
    MinionRuntimeProbe? Replay,
    MinionRuntimeProbe? Failure);

public static class MinionStage10Adapter
{
    private static readonly string[] RequiredEvents =
    [
        "minion-real-register", "minion-real-summon", "minion-component-real-result",
        "minion-real-damaged", "minion-real-death", "minion-real-removal",
        "minion-combat-end-cleanup"
    ];

    private static readonly (string Kind, string Id, string Evidence, string Reason)[] Unsupported =
    [
        ("UI", "shadowverse-ui", "stage10/capability-matrix", "visual UI and Godot scene semantics remain outside the scalar adapter"),
        ("Map", "shadowverse-map", "stage10/capability-matrix", "map and run routing are not part of this combat slice"),
        ("Save", "shadowverse-save", "stage10/capability-matrix", "formal saves and settings are isolated and unsupported"),
        ("ComplexComponent", "shadowverse-complex-components", "stage10/capability-matrix", "only the observed StrikeComponent card is mapped"),
        ("ReplayNetwork", "shadowverse-network", "stage10/capability-matrix", "network serialization and multiplayer semantics remain unsupported")
    ];

    public static MinionStage10Evaluation Evaluate(
        MinionStage10Request request,
        MinionRuntimeProbe? primary,
        MinionRuntimeProbe? replay,
        MinionRuntimeProbe? failure)
    {
        var catalog = ModCatalogParser.Parse(request.SnapshotDirectory, request.SnapshotManifestPath,
            MinionStage9Constants.TargetGameVersion);
        var resolution = DependencyResolver.Resolve(catalog, [MinionStage9Constants.ShadowverseModId]);
        var errors = new List<AdapterErrorRecord>();
        var phases = new List<WatcherPhaseRecord>();
        var capabilities = new List<MinionCapabilityRecord>();
        ValidateFixedInputs(request, catalog, resolution, errors);
        phases.Add(new("static-dependency-closure",
            errors.Count == 0 ? WatcherPhaseStatus.Passed : WatcherPhaseStatus.RuntimeFailure,
            "stage10/static-dependency-closure",
            errors.Count == 0 ? "Shadowverse -> exact RitsuLib compat/0.111.0 + MinionLib closure" : "fixed input validation failed"));

        var primaryOk = ValidateCombatProbe(primary, "primary", errors, phases, capabilities);
        var replayOk = ValidateCombatProbe(replay, "replay", errors, phases, capabilities);
        var deterministic = primaryOk && replayOk && primary!.PrimaryDigest is not null &&
            primary.PrimaryDigest == replay!.PrimaryDigest;
        if (!deterministic)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DeterminismMismatch,
                MinionStage9Constants.ShadowverseModId, "/replay/primaryDigest",
                "primary and replay real-combat digests do not match", "stage10/replay"));
        var failureIsolated = failure is not null && !failure.NativeRuntimeAvailable &&
            failure.Terminal == "runtime-failed" && !string.IsNullOrWhiteSpace(failure.FailureMessage) &&
            failure.SnapshotWriteBlocked && failure.FormalDataWriteBlocked;
        if (!failureIsolated)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.RuntimeFailure,
                MinionStage9Constants.ShadowverseModId, "/failure-isolation",
                "failure scenario did not produce an isolated runtime-failed probe", "stage10/failure-isolation"));
        phases.Add(new("failure-isolation", failureIsolated ? WatcherPhaseStatus.Passed : WatcherPhaseStatus.RuntimeFailure,
            "stage10/failure-isolation", failureIsolated ? null : "failure probe is missing or contaminated"));
        foreach (var item in Unsupported)
            capabilities.Add(new(item.Kind, item.Id, "Unsupported", null, item.Evidence, item.Reason));

        var inputDigest = MinionStage10Report.StableDigest(new
        {
            request.SnapshotDirectory, request.SnapshotManifestPath, request.Seed,
            request.WorkerRunManifestPath, request.NativeBuildManifestPath,
            request.ProbeDocumentPath, request.ReplayProbeDocumentPath,
            request.FailureProbeDocumentPath
        });
        var status = errors.Count == 0 && phases.All(x => x.Status == WatcherPhaseStatus.Passed)
            ? WatcherStage6Status.Pass
            : errors.Any(x => x.Code == AdapterErrorCode.Unsupported)
                ? WatcherStage6Status.Blocked : WatcherStage6Status.Partial;
        var first = errors.OrderBy(x => x.Code).ThenBy(x => x.Path, StringComparer.Ordinal).FirstOrDefault();
        var report = new MinionStage10Report(
            MinionStage10Constants.Schema, status, first?.StableCode ?? "",
            first?.Message ?? "Minion stage 10 real combat completed",
            MinionStage9Constants.TargetGameVersion, MinionStage9Constants.SnapshotId,
            MinionStage9Constants.SnapshotContentSha256, MinionStage9Constants.MinionLibVersion,
            "compat/0.111.0", MinionStage9Constants.ShadowverseVersion, request.Seed,
            phases.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray(),
            capabilities.OrderBy(x => x.Kind, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray(),
            new(capabilities.Count, capabilities.Count(x => x.Status == "Mapped"),
                capabilities.Count(x => x.Status == "Unsupported"), errors.Count,
                primary?.RealCombat is not null ? 1 : 0, errors.Count),
            errors.OrderBy(x => x.Code).ThenBy(x => x.Path, StringComparer.Ordinal).ToArray(),
            inputDigest, primary?.PrimaryDigest, replay?.PrimaryDigest, failure?.PrimaryDigest,
            deterministic, failureIsolated,
            primary?.SnapshotWriteBlocked ?? true, primary?.FormalDataWriteBlocked ?? true,
            request.WorkerRunManifestPath, request.NativeBuildManifestPath,
            request.ProbeDocumentPath, request.ReplayProbeDocumentPath,
            request.FailureProbeDocumentPath);
        return new(report, catalog, resolution, primary, replay, failure);
    }

    private static bool ValidateCombatProbe(
        MinionRuntimeProbe? probe, string label, List<AdapterErrorRecord> errors,
        List<WatcherPhaseRecord> phases, List<MinionCapabilityRecord> capabilities)
    {
        if (probe is null)
        {
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.RuntimeFailure,
                MinionStage9Constants.ShadowverseModId, $"/probe/{label}", "real combat probe is missing", $"stage10/{label}"));
            foreach (var name in RequiredEvents)
                phases.Add(new(name, WatcherPhaseStatus.NotRun, $"stage10/{label}/{name}", "probe is missing"));
            return false;
        }
        var ok = probe.Schema == MinionStage10Constants.ProbeSchema && probe.NativeRuntimeAvailable &&
            probe.NativeGameplayObserved && probe.RealCombat is not null &&
            probe.MinionLibInitialized && probe.ShadowverseContentRegistered &&
            probe.SnapshotWriteBlocked && probe.FormalDataWriteBlocked;
        if (!ok)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.RuntimeFailure,
                MinionStage9Constants.ShadowverseModId, $"/probe/{label}",
                "real combat probe did not close the native runtime boundary", $"stage10/{label}"));
        var events = probe.Events.ToHashSet(StringComparer.Ordinal);
        foreach (var name in RequiredEvents)
        {
            var found = events.Contains(name);
            phases.Add(new(name, found ? WatcherPhaseStatus.Passed : WatcherPhaseStatus.RuntimeFailure,
                $"stage10/{label}/{name}", found ? null : $"required event '{name}' is absent"));
            if (!found)
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.RuntimeFailure,
                    MinionStage9Constants.ShadowverseModId, $"/probe/{label}/events/{name}",
                    $"required event '{name}' is absent", $"stage10/{label}/{name}"));
        }
        var combat = probe.RealCombat;
        if (combat is not null)
        {
            if (combat.MinionType != MinionStage10Constants.RealMinionType || !combat.ComponentApplied ||
                combat.ComponentCardType != MinionStage10Constants.RealComponentCardType ||
                combat.ComponentId != MinionStage10Constants.RealComponentId ||
                combat.ComponentAmount <= 0 || combat.TargetHpAfterComponent >= combat.TargetHpBeforeComponent ||
                combat.HpAfterDamage >= combat.SummonedHp || combat.AliveAfterDeath ||
                !combat.RemovedFromCombat || !combat.CombatEnded || !combat.CleanupConfirmed)
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError,
                    MinionStage9Constants.ShadowverseModId, $"/probe/{label}/realCombat",
                    "real Minion combat scalar contract is incomplete", $"stage10/{label}/real-combat"));
            capabilities.Add(new("Minion", combat.MinionModelId, "Mapped", combat.MinionModelId,
                $"stage10/{label}/minion-real-summon", null));
            capabilities.Add(new("ComponentCard", combat.ComponentCardId, "Mapped", combat.ComponentCardId,
                $"stage10/{label}/component-real-result", null));
            capabilities.Add(new("MinionLifecycle", combat.MinionCreatureId, "Mapped", combat.MinionModelId,
                $"stage10/{label}/minion-real-removal", null));
            capabilities.Add(new("CombatCleanup", label, combat.CleanupConfirmed ? "Mapped" : "Unsupported",
                combat.CleanupConfirmed ? "combat-end" : null, $"stage10/{label}/combat-end-cleanup",
                combat.CleanupConfirmed ? null : "combat cleanup was not observed"));
        }
        return ok;
    }

    private static void ValidateFixedInputs(MinionStage10Request request, ModCatalog catalog,
        DependencyResolution resolution, List<AdapterErrorRecord> errors)
    {
        if (catalog.SnapshotId != MinionStage9Constants.SnapshotId)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion, MinionStage9Constants.ShadowverseModId,
                "/snapshotDirectory", "snapshot identity drifted", "stage10/catalog"));
        if (!string.Equals(catalog.SnapshotContentSha256, MinionStage9Constants.SnapshotContentSha256, StringComparison.OrdinalIgnoreCase))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, MinionStage9Constants.ShadowverseModId,
                "/snapshotContentSha256", "fixed snapshot content hash does not match", "stage10/catalog"));
        if (catalog.TargetGameVersion != MinionStage9Constants.TargetGameVersion)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion, MinionStage9Constants.ShadowverseModId,
                "/targetGameVersion", "target game version must be exactly 0.111.0", "stage10/catalog"));
        errors.AddRange(catalog.Errors.Select(x => AdapterErrorRecord.Create(MapError(x.Code),
            MinionStage9Constants.ShadowverseModId, x.Path, x.Message, "stage10/catalog")));
        var shadowverse = catalog.Manifests.FirstOrDefault(x => x.ModId == MinionStage9Constants.ShadowverseModId);
        var minion = catalog.Manifests.FirstOrDefault(x => x.ModId == MinionStage9Constants.MinionLibId);
        var ritsu = catalog.Manifests.FirstOrDefault(x => x.ModId == MinionStage9Constants.RitsuLibId);
        if (shadowverse is null || minion is null || ritsu is null || !resolution.IsSuccess ||
            !resolution.DependencyClosure.Contains(MinionStage9Constants.MinionLibId) ||
            !resolution.DependencyClosure.Contains(MinionStage9Constants.RitsuLibId) ||
            !resolution.SelectedVariants.Any(x => x.ModId == MinionStage9Constants.RitsuLibId && x.RelativeDirectory == "compat/0.111.0"))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DependencyMissing, MinionStage9Constants.ShadowverseModId,
                "/dependencies", "Shadowverse exact compat/0.111.0 dependency closure is unavailable", "stage10/dependency-closure"));
        else
        {
            if (shadowverse.Source.WorkshopId != MinionStage9Constants.ShadowverseWorkshopId || shadowverse.Version != MinionStage9Constants.ShadowverseVersion)
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, MinionStage9Constants.ShadowverseModId, "/identity", "Shadowverse identity does not match fixed snapshot", "stage10/catalog"));
            if (minion.Source.WorkshopId != MinionStage9Constants.MinionWorkshopId || minion.Version != MinionStage9Constants.MinionLibVersion)
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, MinionStage9Constants.MinionLibId, "/identity", "MinionLib identity does not match fixed snapshot", "stage10/catalog"));
            if (ritsu.Source.WorkshopId != MinionStage9Constants.RitsuWorkshopId || ritsu.Version != MinionStage9Constants.RitsuLibVersion)
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, MinionStage9Constants.RitsuLibId, "/identity", "RitsuLib identity does not match fixed snapshot", "stage10/catalog"));
            if (minion.MinimumGameVersion is { } minGame && CompareVersions(minGame, MinionStage9Constants.TargetGameVersion) > 0)
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion, MinionStage9Constants.MinionLibId, "/min_game_version", "MinionLib minimum game version exceeds target", "stage10/catalog"));
        }
        if (string.IsNullOrWhiteSpace(request.Seed))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, MinionStage9Constants.ShadowverseModId,
                "/seed", "fixed seed is required", "stage10/request"));
    }

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

    public static MinionRuntimeProbe? ReadProbe(string path) =>
        string.IsNullOrWhiteSpace(path) || !File.Exists(path)
            ? null : JsonSerializer.Deserialize<MinionRuntimeProbe>(File.ReadAllText(path), ModWorkerJson.Options);
}
