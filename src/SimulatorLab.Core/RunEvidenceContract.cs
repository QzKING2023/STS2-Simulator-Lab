using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SimulatorLab.Core;

public enum RunEvidenceKindV1 { Primary, Repeat, Replay, Failure }

/// <summary>
/// The immutable identity of a native run. Values are intentionally explicit;
/// a missing snapshot, dependency, collector, or adapter identity is not a
/// reproducible input and therefore cannot pass manifest validation.
/// </summary>
public sealed record CanonicalRunInputV1
{
    public string Schema { get; init; } = "canonical-run-input/v1";
    public string TargetGameVersion { get; init; } = "0.111.0";
    public string GameCommit { get; init; } = GameBuildPin.Commit;
    public string SnapshotId { get; init; } = "";
    public string SnapshotContentSha256 { get; init; } = "";
    public string SnapshotManifestSha256 { get; init; } = "";
    public string ModId { get; init; } = "vanilla";
    public string ModVersion { get; init; } = "0.111.0";
    public IReadOnlyList<string> DependencyClosure { get; init; } = [];
    public string Seed { get; init; } = "";
    public string WorkerIdentity { get; init; } = "";
    public string Configuration { get; init; } = "";
    public string Adapter { get; init; } = "native-run/v1";
    public string Collector { get; init; } = "";

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Schema != "canonical-run-input/v1") errors.Add("Unsupported canonical input schema.");
        if (TargetGameVersion != "0.111.0" || GameCommit != GameBuildPin.Commit)
            errors.Add("Canonical input is not bound to the pinned 0.111.0 game.");
        if (string.IsNullOrWhiteSpace(SnapshotId)) errors.Add("Snapshot identity is missing.");
        if (!IsSha256(SnapshotContentSha256)) errors.Add("Snapshot content digest is missing or malformed.");
        if (!IsSha256(SnapshotManifestSha256)) errors.Add("Snapshot manifest digest is missing or malformed.");
        if (string.IsNullOrWhiteSpace(ModId) || string.IsNullOrWhiteSpace(ModVersion)) errors.Add("Mod identity is missing.");
        if (string.IsNullOrWhiteSpace(Seed)) errors.Add("Run seed is missing.");
        if (string.IsNullOrWhiteSpace(WorkerIdentity) || string.IsNullOrWhiteSpace(Adapter) || string.IsNullOrWhiteSpace(Collector))
            errors.Add("Worker, adapter, and collector identities are required.");
        if (DependencyClosure.Any(string.IsNullOrWhiteSpace) || DependencyClosure.Distinct(StringComparer.Ordinal).Count() != DependencyClosure.Count)
            errors.Add("Dependency closure contains missing or duplicate identities.");
        return errors;
    }

    internal static bool IsSha256(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
}

public sealed record RunManifestV1
{
    public string Schema { get; init; } = "native-run-manifest/v1";
    public string RunId { get; init; } = "";
    public RunEvidenceKindV1 EvidenceKind { get; init; }
    public DateTimeOffset StartedUtc { get; init; }
    public DateTimeOffset FinishedUtc { get; init; }
    public int ProcessId { get; init; }
    public CanonicalRunInputV1 CanonicalInput { get; init; } = new();
    public NativeRunTerminal Terminal { get; init; }
    public NativeRunStop Stop { get; init; }
    public string? ErrorCode { get; init; }
    public string? Error { get; init; }
    public string LastBoundary { get; init; } = "";
    public IReadOnlyList<string> LifecycleBoundaries { get; init; } = [];
    public string? StateDigest { get; init; }
    public string? RngDigest { get; init; }
    public string? TraceDigest { get; init; }
    public string? LifecycleDigest { get; init; }
    public string? RouteDigest { get; init; }
    public string? ChoiceDigest { get; init; }
    public bool CompleteStateEvidence { get; init; }
    public bool CompleteRngEvidence { get; init; }
    public bool CompleteTraceEvidence { get; init; }

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Schema != "native-run-manifest/v1") errors.Add("Unsupported run manifest schema.");
        if (string.IsNullOrWhiteSpace(RunId) || ProcessId <= 0) errors.Add("Run identity is missing.");
        if (FinishedUtc < StartedUtc) errors.Add("Run timestamps are reversed.");
        errors.AddRange(CanonicalInput.Validate());
        if (Stop == NativeRunStop.Completed && Terminal is not (NativeRunTerminal.RunVictory or NativeRunTerminal.Death))
            errors.Add("Completed manifest requires actual RunVictory or Death.");
        if (Terminal is NativeRunTerminal.RunVictory or NativeRunTerminal.Death && Stop != NativeRunStop.Completed)
            errors.Add("A native terminal cannot be paired with a non-completed stop.");
        if (Stop is not (NativeRunStop.Completed or NativeRunStop.SliceBoundary) && string.IsNullOrWhiteSpace(Error))
            errors.Add("Non-terminal stop must retain its original reason.");
        if (Stop is NativeRunStop.Completed or NativeRunStop.SliceBoundary &&
            (!CompleteStateEvidence || !CompleteRngEvidence || !CompleteTraceEvidence))
            errors.Add("Successful boundary is missing complete state/RNG/trace evidence.");
        return errors;
    }
}

public sealed record RunComparisonV1
{
    public string Schema { get; init; } = "native-run-comparison/v1";
    public bool CanonicalInputMatch { get; init; }
    public bool TerminalMatch { get; init; }
    public bool BoundaryMatch { get; init; }
    public bool StateMatch { get; init; }
    public bool RngMatch { get; init; }
    public bool TraceMatch { get; init; }
    public bool Exact => CanonicalInputMatch && TerminalMatch && BoundaryMatch && StateMatch && RngMatch && TraceMatch;
    public IReadOnlyList<string> Differences { get; init; } = [];

    public static RunComparisonV1 Compare(RunManifestV1 left, RunManifestV1 right)
    {
        var differences = new List<string>();
        bool input = StateDigest.Compute(left.CanonicalInput) == StateDigest.Compute(right.CanonicalInput);
        bool terminal = left.Terminal == right.Terminal && left.Stop == right.Stop;
        bool boundary = string.Equals(left.LastBoundary, right.LastBoundary, StringComparison.Ordinal) &&
                        left.LifecycleBoundaries.SequenceEqual(right.LifecycleBoundaries, StringComparer.Ordinal);
        bool state = string.Equals(left.StateDigest, right.StateDigest, StringComparison.Ordinal);
        bool rng = string.Equals(left.RngDigest, right.RngDigest, StringComparison.Ordinal);
        bool trace = string.Equals(left.TraceDigest, right.TraceDigest, StringComparison.Ordinal);
        if (!input) differences.Add("canonical-input");
        if (!terminal) differences.Add("terminal");
        if (!boundary) differences.Add("lifecycle-boundary");
        if (!state) differences.Add("state");
        if (!rng) differences.Add("rng");
        if (!trace) differences.Add("trace");
        return new() { CanonicalInputMatch = input, TerminalMatch = terminal, BoundaryMatch = boundary,
            StateMatch = state, RngMatch = rng, TraceMatch = trace, Differences = differences };
    }
}
