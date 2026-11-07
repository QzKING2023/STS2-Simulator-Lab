using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SimulatorLab.Core.Mods;

/// <summary>
/// Constants and scalar contracts for the Watcher first-battle slice.  The
/// probe document is the only input from an isolated runtime.  It contains no
/// assembly, Godot, or Mod implementation values.
/// </summary>
public static class WatcherStage6Constants
{
    public const string Schema = "watcher-stage6/v1";
    public const string TargetGameVersion = "0.111.0";
    public const string SnapshotId = "workshop-snapshot-20261106-163352";
    public const string SnapshotContentSha256 = "7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73";
    public const string WorkshopId = "3747492505";
    public const string ModId = "Watcher";
    public const string ModVersion = "1.5.14";
    public const string BaseLibId = "BaseLib";
    public const string BaseLibVersion = "3.4.7";
}

public enum WatcherStage6Status
{
    Pass,
    Partial,
    Blocked
}

public enum WatcherPhaseStatus
{
    NotRun,
    Passed,
    Unsupported,
    RuntimeFailure
}

public sealed record WatcherAction(
    string Kind,
    string? CardId = null,
    string? TargetId = null,
    int? ChoiceIndex = null);

public sealed record WatcherActionScript(
    string Seed,
    IReadOnlyList<WatcherAction> Actions,
    string Digest);

public sealed record WatcherDefinitionObservation(
    string Kind,
    string Id,
    string DisplayName,
    bool Supported,
    string EvidenceRef,
    string? Reason = null,
    int? Cost = null,
    int? Duration = null,
    string? Description = null,
    IReadOnlyList<string>? StartingCardIds = null,
    IReadOnlyList<string>? StartingRelicIds = null,
    IReadOnlyList<string>? PowerIds = null,
    IReadOnlyList<string>? IntentIds = null,
    IReadOnlyList<string>? EnemyIds = null,
    IReadOnlyList<string>? OptionIds = null,
    IReadOnlyList<string>? ContentIds = null,
    IReadOnlyList<AdapterHookInput>? Hooks = null,
    IReadOnlyList<AdapterResourceInput>? ResourceResults = null);

public sealed record WatcherRuntimeProbeDocument(
    string Schema,
    bool NativeRuntimeAvailable,
    string? TargetGameVersion,
    string? SnapshotId,
    string? Seed,
    string? NativeBuildId,
    string? BuildContentKey,
    IReadOnlyList<WatcherDefinitionObservation> Identities,
    IReadOnlyList<string> Events,
    string? PrimaryDigest,
    string? ReplayDigest,
    string? Terminal,
    string? FailureCode,
    string? FailureMessage);

public sealed record WatcherStage6Request(
    string SnapshotDirectory,
    string SnapshotManifestPath,
    string Seed,
    IReadOnlyList<WatcherAction> Actions,
    string? WorkerRunManifestPath = null,
    string? NativeBuildManifestPath = null,
    string? ProbeDocumentPath = null,
    string? ReplayProbeDocumentPath = null);

public sealed record WatcherCapabilityRecord(
    string Kind,
    string Id,
    string Status,
    string? StandardModelId,
    string EvidenceRef,
    string? Reason);

public sealed record WatcherPhaseRecord(
    string Name,
    WatcherPhaseStatus Status,
    string EvidenceRef,
    string? Message = null);

public sealed record WatcherStage6Report(
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
    string Seed,
    WatcherActionScript ActionScript,
    IReadOnlyList<WatcherPhaseRecord> Phases,
    IReadOnlyList<WatcherCapabilityRecord> Capabilities,
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
        Schema,
        Status,
        StopCode,
        StopMessage,
        TargetGameVersion,
        SnapshotId,
        SnapshotContentSha256,
        WorkshopId,
        ModId,
        ModVersion,
        Seed,
        ActionScript,
        Phases,
        Capabilities,
        Errors = Errors.Select(x => x.StableSummary).ToArray(),
        PrimaryDigest,
        ReplayDigest,
        Deterministic,
        SnapshotWriteBlocked,
        FormalDataWriteBlocked
    });

    internal static string StableDigest<T>(T value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, ModWorkerJson.Options)))).ToLowerInvariant();
}

public sealed record WatcherStage6Evaluation(
    WatcherStage6Report Report,
    ConversionResult? Conversion,
    ModCatalog Catalog,
    DependencyResolution Resolution);

/// <summary>
/// Converts a scalar runtime probe into the existing standard model and keeps
/// every uncovered identity explicit.  Missing runtime evidence is a typed
/// Unsupported stop; it cannot become a passing empty model.
/// </summary>
public static class WatcherStage6Adapter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly string[] RequiredEvents =
    [
        "character-discovered", "character-selected", "initial-state",
        "battle-victory", "reward-boundary", "leave-combat", "cleanup", "replay"
    ];

    private static readonly HashSet<string> SupportedKinds = new(StringComparer.Ordinal)
    {
        "Character", "Card", "Relic", "Power", "Reward"
    };

    private static readonly HashSet<string> SupportedHookPoints = new(StringComparer.Ordinal)
    {
        "OnCombatStart", "OnCardPlayed", "OnTurnStart", "OnTurnEnd", "OnDeath", "OnCombatEnd",
        "OnRewardGenerated", "OnEventOptionSelected"
    };

    public static WatcherActionScript BuildActionScript(string seed, IReadOnlyList<WatcherAction> actions)
    {
        var normalizedSeed = seed?.Trim() ?? "";
        var normalized = (actions ?? [])
            .Select(x => new WatcherAction(x.Kind?.Trim() ?? "", x.CardId?.Trim(), x.TargetId?.Trim(), x.ChoiceIndex))
            .ToArray();
        var digest = WatcherStage6Report.StableDigest(new { Seed = normalizedSeed, Actions = normalized });
        return new(normalizedSeed, normalized, digest);
    }

    public static WatcherStage6Evaluation Evaluate(
        WatcherStage6Request request,
        WatcherRuntimeProbeDocument? probe = null,
        WatcherRuntimeProbeDocument? replayProbe = null)
    {
        var catalog = ModCatalogParser.Parse(request.SnapshotDirectory, request.SnapshotManifestPath,
            WatcherStage6Constants.TargetGameVersion);
        var resolution = DependencyResolver.Resolve(catalog, [WatcherStage6Constants.ModId]);
        var errors = new List<AdapterErrorRecord>();
        var phases = new List<WatcherPhaseRecord>();
        var capabilities = new List<WatcherCapabilityRecord>();
        var script = BuildActionScript(request.Seed, request.Actions);
        var inputDigest = WatcherStage6Report.StableDigest(new
        {
            request.SnapshotDirectory,
            request.SnapshotManifestPath,
            request.Seed,
            Actions = script.Actions,
            WorkerRunManifestPath = request.WorkerRunManifestPath,
            NativeBuildManifestPath = request.NativeBuildManifestPath,
            ProbeDocumentPath = request.ProbeDocumentPath,
            ReplayProbeDocumentPath = request.ReplayProbeDocumentPath
        });

        ValidateFixedInputs(request, catalog, resolution, errors);
        ValidateWorkerReference(request, errors);
        phases.Add(new("static-dependency-closure",
            errors.Count == 0 ? WatcherPhaseStatus.Passed : WatcherPhaseStatus.RuntimeFailure,
            "stage6/static-dependency-closure",
            errors.Count == 0 ? "Watcher -> BaseLib exact 0.111.0 closure" : "fixed input validation failed"));

        var conversion = default(ConversionResult);
        string? primaryDigest = probe?.PrimaryDigest;
        string? replayDigest = replayProbe?.ReplayDigest ?? probe?.ReplayDigest;
        var deterministic = false;

        if (errors.Count == 0 && probe is null)
        {
            AddUnsupported(errors, WatcherStage6Constants.ModId, "/runtime/native-build",
                "No approved 0.111.0 native build and isolated runtime probe were supplied; no Watcher battle was executed.",
                "stage6/native-runtime-required");
            AddUnrunPhases(phases, "No runtime probe was available");
        }
        else if (errors.Count == 0 && probe is not null)
        {
            if (string.IsNullOrWhiteSpace(request.NativeBuildManifestPath))
                AddUnsupported(errors, WatcherStage6Constants.ModId, "/runtime/build-manifest",
                    "A native build manifest bound to the exact game inputs is required before executing Watcher.",
                    "stage6/native-build-manifest-required");
            ValidateProbe(probe, request, errors);
            foreach (var identity in (probe.Identities ?? []).OrderBy(x => x.Kind, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal))
            {
                var kindSupported = SupportedKinds.Contains(identity.Kind);
                var semanticallySupported = identity.Supported && kindSupported;
                var unsupportedReason = identity.Reason ?? (kindSupported ? "runtime identity is unsupported" : $"definition kind '{identity.Kind}' is unsupported");
                var identityStatus = semanticallySupported ? "Mapped" : "Unsupported";
                capabilities.Add(new(identity.Kind, identity.Id, identityStatus,
                    semanticallySupported ? $"Watcher/{identity.Id}" : null,
                    identity.EvidenceRef,
                    semanticallySupported ? null : unsupportedReason));
                if (!semanticallySupported)
                    AddUnsupported(errors, identity.Id, $"/identities/{identity.Kind}/{identity.Id}",
                        unsupportedReason, identity.EvidenceRef);
                foreach (var hook in identity.Hooks ?? [])
                {
                    var hookSupported = !string.IsNullOrWhiteSpace(hook.Id) && SupportedHookPoints.Contains(hook.HookPoint ?? "");
                    capabilities.Add(new("Hook", hook.Id ?? "<missing>", hookSupported ? "Mapped" : "Unsupported",
                        hookSupported ? $"Watcher/{hook.Id}" : null, hook.EvidenceRef ?? identity.EvidenceRef,
                        hookSupported ? null : hook.Description ?? $"hook point '{hook.HookPoint}' is unsupported"));
                    if (!hookSupported)
                        AddUnsupported(errors, hook.Id ?? identity.Id, $"/identities/{identity.Kind}/{identity.Id}/hooks",
                            hook.Description ?? $"hook point '{hook.HookPoint}' is unsupported", hook.EvidenceRef ?? identity.EvidenceRef);
                }
            }

            if (errors.Count == 0)
            {
                var document = ToAdapterDocument(request, probe);
                conversion = ModAdapterConverter.Convert(document, new ModDefinitionRegistry());
                errors.AddRange(conversion.Errors);
                AddPhasesFromEvents(phases, probe.Events);
                deterministic = IsDeterministic(probe, replayProbe, request);
                if (!deterministic)
                    errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DeterminismMismatch, WatcherStage6Constants.ModId,
                        "/replay", "primary and new-process replay digests do not match", "stage6/replay"));
            }
            else
                AddUnrunPhases(phases, "runtime probe contains unsupported identities or failed validation");
        }

        var status = errors.Count == 0 && conversion?.IsSuccess == true && phases.All(x => x.Status == WatcherPhaseStatus.Passed)
            ? WatcherStage6Status.Pass
            : errors.Any(x => x.Code == AdapterErrorCode.Unsupported)
                ? WatcherStage6Status.Blocked
                : WatcherStage6Status.Partial;
        var stopCode = errors.FirstOrDefault()?.StableCode ?? "";
        var stopMessage = errors.FirstOrDefault()?.Message ?? "Watcher stage 6 completed";
        var report = new WatcherStage6Report(
            WatcherStage6Constants.Schema, status, stopCode, stopMessage,
            WatcherStage6Constants.TargetGameVersion, WatcherStage6Constants.SnapshotId,
            WatcherStage6Constants.SnapshotContentSha256, WatcherStage6Constants.WorkshopId,
            WatcherStage6Constants.ModId, WatcherStage6Constants.ModVersion, script.Seed, script,
            phases.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray(), capabilities,
            errors.OrderBy(x => x.Code).ThenBy(x => x.ObjectId, StringComparer.Ordinal).ThenBy(x => x.Path, StringComparer.Ordinal).ToArray(),
            inputDigest, primaryDigest, replayDigest, deterministic,
            SnapshotWriteBlocked(request), FormalDataWriteBlocked(request),
            request.WorkerRunManifestPath, request.NativeBuildManifestPath, request.ProbeDocumentPath);
        return new(report, conversion, catalog, resolution);
    }

    public static WatcherRuntimeProbeDocument? ReadProbe(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        return JsonSerializer.Deserialize<WatcherRuntimeProbeDocument>(File.ReadAllText(path), JsonOptions);
    }

    private static void ValidateFixedInputs(WatcherStage6Request request, ModCatalog catalog,
        DependencyResolution resolution, List<AdapterErrorRecord> errors)
    {
        if (!string.Equals(catalog.SnapshotId, WatcherStage6Constants.SnapshotId, StringComparison.Ordinal))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion, WatcherStage6Constants.ModId,
                "/snapshotDirectory", $"snapshot must be {WatcherStage6Constants.SnapshotId}"));
        if (!string.Equals(catalog.SnapshotContentSha256, WatcherStage6Constants.SnapshotContentSha256, StringComparison.OrdinalIgnoreCase))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, WatcherStage6Constants.ModId,
                "/snapshotContentSha256", "fixed snapshot content hash does not match"));
        if (!string.Equals(catalog.TargetGameVersion, WatcherStage6Constants.TargetGameVersion, StringComparison.Ordinal))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion, WatcherStage6Constants.ModId,
                "/targetGameVersion", "target game version must be exactly 0.111.0"));
        if (catalog.Errors.Count > 0)
            errors.AddRange(catalog.Errors.Select(x => AdapterErrorRecord.Create(MapError(x.Code), WatcherStage6Constants.ModId,
                x.Path, x.Message, "stage6/catalog")));
        var watcher = catalog.Manifests.FirstOrDefault(x => x.ModId == WatcherStage6Constants.ModId);
        var baseLib = catalog.Manifests.FirstOrDefault(x => x.ModId == WatcherStage6Constants.BaseLibId);
        if (watcher is null || baseLib is null || !resolution.IsSuccess || !resolution.DependencyClosure.Contains(WatcherStage6Constants.BaseLibId))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DependencyMissing, WatcherStage6Constants.ModId,
                "/dependencies", "Watcher exact BaseLib dependency closure is unavailable", "stage6/dependency-closure"));
        else
        {
            if (!string.Equals(watcher.Source.WorkshopId, WatcherStage6Constants.WorkshopId, StringComparison.Ordinal))
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, WatcherStage6Constants.ModId,
                    "/workshopId", "Watcher Workshop identity does not match fixed snapshot"));
            if (!string.Equals(watcher.Version, WatcherStage6Constants.ModVersion, StringComparison.Ordinal))
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, WatcherStage6Constants.ModId,
                    "/version", "Watcher version does not match fixed snapshot"));
            if (!string.Equals(baseLib.Version, WatcherStage6Constants.BaseLibVersion, StringComparison.Ordinal))
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion, WatcherStage6Constants.BaseLibId,
                    "/version", "BaseLib must be exactly 3.4.7"));
        }
        if (string.IsNullOrWhiteSpace(request.Seed))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, WatcherStage6Constants.ModId,
                "/seed", "fixed seed is required"));
        if (request.Actions is null || request.Actions.Count == 0)
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, WatcherStage6Constants.ModId,
                "/actions", "fixed action script is required"));
    }

    private static void ValidateProbe(WatcherRuntimeProbeDocument probe, WatcherStage6Request request,
        List<AdapterErrorRecord> errors)
    {
        if (!probe.NativeRuntimeAvailable)
            AddUnsupported(errors, WatcherStage6Constants.ModId, "/runtime", probe.FailureMessage ?? "native runtime is unavailable",
                probe.FailureCode ?? "stage6/native-runtime");
        if (!string.Equals(probe.Schema, "watcher-runtime-probe/v1", StringComparison.Ordinal))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.InvalidFormat, WatcherStage6Constants.ModId,
                "/probe/schema", "unexpected Watcher runtime probe schema"));
        if (!string.Equals(probe.TargetGameVersion, WatcherStage6Constants.TargetGameVersion, StringComparison.Ordinal))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.UnsupportedVersion, WatcherStage6Constants.ModId,
                "/probe/targetGameVersion", "runtime probe target must be exactly 0.111.0"));
        if (!string.Equals(probe.SnapshotId, WatcherStage6Constants.SnapshotId, StringComparison.Ordinal))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DataError, WatcherStage6Constants.ModId,
                "/probe/snapshotId", "runtime probe snapshot identity does not match"));
        if (!string.Equals(probe.Seed, request.Seed, StringComparison.Ordinal))
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.DeterminismMismatch, WatcherStage6Constants.ModId,
                "/probe/seed", "runtime probe seed does not match the fixed action script", "stage6/seed"));
        if (string.IsNullOrWhiteSpace(probe.NativeBuildId) || string.IsNullOrWhiteSpace(probe.BuildContentKey))
            AddUnsupported(errors, WatcherStage6Constants.ModId, "/probe/nativeBuild",
                "runtime probe is missing the exact native build identity", "stage6/native-build-identity");
        if (!string.Equals(probe.Terminal, "battle-complete", StringComparison.Ordinal))
            AddUnsupported(errors, WatcherStage6Constants.ModId, "/probe/terminal",
                probe.FailureMessage ?? "runtime probe did not complete the required battle boundary",
                probe.FailureCode ?? "stage6/battle-boundary");
        foreach (var identity in probe.Identities ?? [])
        {
            if (string.IsNullOrWhiteSpace(identity.Kind) || string.IsNullOrWhiteSpace(identity.Id) ||
                string.IsNullOrWhiteSpace(identity.DisplayName) || string.IsNullOrWhiteSpace(identity.EvidenceRef))
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingField, identity.Id ?? WatcherStage6Constants.ModId,
                    "/identities", "runtime identity requires kind, id, displayName and evidenceRef"));
        }
    }

    private static ModAdapterDocument ToAdapterDocument(WatcherStage6Request request, WatcherRuntimeProbeDocument probe)
    {
        var files = new[] { "Watcher.json", "Watcher.dll", "Watcher.pck", "BaseLib.json", "BaseLib.dll", "BaseLib.pck" }
            .Select(x => new AdapterFileInput(x, true)).ToArray();
        return new(
            WatcherStage6Constants.ModId, WatcherStage6Constants.ModId, WatcherStage6Constants.ModVersion,
            WatcherStage6Constants.TargetGameVersion, "3747492505/Watcher.json", files,
            [new AdapterDependencyInput(WatcherStage6Constants.BaseLibId, WatcherStage6Constants.BaseLibVersion)],
            [WatcherStage6Constants.BaseLibId],
            (probe.Identities ?? []).Where(x => x.Supported).Select(ToAdapterObject).ToArray(),
            [], 256);
    }

    private static AdapterObjectInput ToAdapterObject(WatcherDefinitionObservation x) => new(
        x.Kind, x.Id, x.DisplayName, x.Description ?? "", x.Cost, x.Duration,
        Sorted(x.StartingCardIds), Sorted(x.StartingRelicIds), Sorted(x.PowerIds), Sorted(x.IntentIds),
        Sorted(x.EnemyIds), Sorted(x.OptionIds), Sorted(x.ContentIds), x.Hooks ?? [], x.ResourceResults ?? [],
        IsGeneratedCard: string.Equals(x.Kind, "Card", StringComparison.Ordinal) && x.Cost == -1);

    private static IReadOnlyList<string> Sorted(IReadOnlyList<string>? values) =>
        (values ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    private static void AddPhasesFromEvents(List<WatcherPhaseRecord> phases, IReadOnlyList<string>? events)
    {
        var set = (events ?? []).ToHashSet(StringComparer.Ordinal);
        foreach (var name in RequiredEvents)
            phases.Add(new(name, set.Contains(name) ? WatcherPhaseStatus.Passed : WatcherPhaseStatus.RuntimeFailure,
                $"stage6/event/{name}", set.Contains(name) ? null : $"required event '{name}' is absent"));
    }

    private static void AddUnrunPhases(List<WatcherPhaseRecord> phases, string message)
    {
        foreach (var name in RequiredEvents)
            if (!phases.Any(x => x.Name == name)) phases.Add(new(name, WatcherPhaseStatus.Unsupported,
                $"stage6/event/{name}", message));
    }

    private static bool IsDeterministic(WatcherRuntimeProbeDocument primary, WatcherRuntimeProbeDocument? replay,
        WatcherStage6Request request)
    {
        if (replay is null || string.IsNullOrWhiteSpace(primary.PrimaryDigest) || string.IsNullOrWhiteSpace(replay.PrimaryDigest))
            return false;
        return string.Equals(primary.PrimaryDigest, replay.PrimaryDigest, StringComparison.Ordinal) &&
            string.Equals(primary.Seed, request.Seed, StringComparison.Ordinal) &&
            string.Equals(primary.SnapshotId, WatcherStage6Constants.SnapshotId, StringComparison.Ordinal);
    }

    private static void AddUnsupported(List<AdapterErrorRecord> errors, string objectId, string path,
        string message, string evidence) => errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.Unsupported,
        objectId, path, message, evidence));

    private static void ValidateWorkerReference(WatcherStage6Request request, List<AdapterErrorRecord> errors)
    {
        if (string.IsNullOrWhiteSpace(request.WorkerRunManifestPath))
        {
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingFile, WatcherStage6Constants.ModId,
                "/workerRunManifestPath", "stage 5 Watcher worker evidence is required", "stage6/worker-reference"));
            return;
        }
        if (!File.Exists(request.WorkerRunManifestPath))
        {
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.MissingFile, WatcherStage6Constants.ModId,
                request.WorkerRunManifestPath, "stage 5 worker run manifest is missing", "stage6/worker-reference"));
            return;
        }
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(request.WorkerRunManifestPath));
            var root = doc.RootElement;
            var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
            var modId = root.TryGetProperty("modId", out var m) ? m.GetString() : null;
            var target = root.TryGetProperty("targetGameVersion", out var t) ? t.GetString() : null;
            var snapshotId = root.TryGetProperty("snapshotId", out var si) ? si.GetString() : null;
            var snapshotHash = root.TryGetProperty("snapshotContentSha256", out var sh) ? sh.GetString() : null;
            if (!string.Equals(status, "succeeded", StringComparison.Ordinal) ||
                !string.Equals(modId, WatcherStage6Constants.ModId, StringComparison.Ordinal) ||
                !string.Equals(target, WatcherStage6Constants.TargetGameVersion, StringComparison.Ordinal) ||
                !string.Equals(snapshotId, WatcherStage6Constants.SnapshotId, StringComparison.Ordinal) ||
                !string.Equals(snapshotHash, WatcherStage6Constants.SnapshotContentSha256, StringComparison.OrdinalIgnoreCase))
                errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.RuntimeFailure, WatcherStage6Constants.ModId,
                    request.WorkerRunManifestPath, "stage 5 worker evidence is not a succeeded Watcher 0.111.0 run for the fixed snapshot", "stage6/worker-reference"));
        }
        catch (JsonException ex)
        {
            errors.Add(AdapterErrorRecord.Create(AdapterErrorCode.InvalidFormat, WatcherStage6Constants.ModId,
                request.WorkerRunManifestPath, ex.Message, "stage6/worker-reference"));
        }
    }

    private static AdapterErrorCode MapError(ModCatalogErrorCode code) => code switch
    {
        ModCatalogErrorCode.MissingFile => AdapterErrorCode.MissingFile,
        ModCatalogErrorCode.InvalidFormat => AdapterErrorCode.InvalidFormat,
        ModCatalogErrorCode.UnsupportedVersion => AdapterErrorCode.UnsupportedVersion,
        ModCatalogErrorCode.DependencyMissing => AdapterErrorCode.DependencyMissing,
        ModCatalogErrorCode.InvalidValue => AdapterErrorCode.InvalidValue,
        _ => AdapterErrorCode.RuntimeFailure
    };

    private static bool SnapshotWriteBlocked(WatcherStage6Request request) => true;

    private static bool FormalDataWriteBlocked(WatcherStage6Request request) => true;

}
