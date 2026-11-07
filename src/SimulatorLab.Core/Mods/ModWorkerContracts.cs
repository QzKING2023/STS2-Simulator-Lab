using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimulatorLab.Core.Mods;

/// <summary>
/// JSON-only protocol records shared by the isolated Mod worker and callers.
/// These records deliberately contain paths and scalar evidence only; they do
/// not expose an assembly, Godot object, or Mod implementation type to the
/// simulator core.
/// </summary>
public sealed record ModWorkerInput(
    string? SnapshotDirectory = null,
    string? SnapshotManifestPath = null,
    string? ModId = null,
    string? TargetGameVersion = null,
    string? Seed = null,
    string? BuildManifestPath = null,
    string? ReplayPath = null,
    IReadOnlyList<string>? RequestedPaths = null,
    IReadOnlyList<ModWorkerAccessAttempt>? AccessAttempts = null,
    IReadOnlyList<ModWorkerWriteAttempt>? WriteAttempts = null,
    string? RequestedDllPath = null,
    string? RequestedPckPath = null);

public sealed record ModWorkerAccessAttempt(string? Operation = null, string? Path = null);
public sealed record ModWorkerWriteAttempt(string? Operation = null, string? Path = null);

public enum ModWorkerErrorCode
{
    MissingFile,
    InvalidFormat,
    UnsupportedVersion,
    DependencyMissing,
    PathEscape,
    AccessBlocked,
    WriteBlocked,
    RuntimeFailure
}

public sealed record ModWorkerError(
    ModWorkerErrorCode Code,
    string Path,
    string Message,
    bool BlocksContinuation = true)
{
    public string StableCode => Code switch
    {
        ModWorkerErrorCode.MissingFile => "MOD-W001",
        ModWorkerErrorCode.InvalidFormat => "MOD-W002",
        ModWorkerErrorCode.UnsupportedVersion => "MOD-W003",
        ModWorkerErrorCode.DependencyMissing => "MOD-W004",
        ModWorkerErrorCode.PathEscape => "MOD-W005",
        ModWorkerErrorCode.AccessBlocked => "MOD-W006",
        ModWorkerErrorCode.WriteBlocked => "MOD-W007",
        ModWorkerErrorCode.RuntimeFailure => "MOD-W008",
        _ => "MOD-W999"
    };

    public string StableSummary => string.Join('|', StableCode, Path, BlocksContinuation ? "blocking" : "non-blocking", Message);
}

public sealed record ModWorkerFileEvidence(
    string ModId,
    string RelativePath,
    string Kind,
    long Length,
    string Sha256);

public sealed record ModWorkerAssemblyEvidence(
    string ModId,
    string RelativePath,
    string AssemblyName,
    string? AssemblyVersion,
    string ModuleVersionId);

public sealed record ModWorkerDiscoverySummary(
    string Schema,
    string TargetGameVersion,
    string ModId,
    IReadOnlyList<string> DependencyClosure,
    IReadOnlyList<ModWorkerFileEvidence> Files,
    IReadOnlyList<ModWorkerAssemblyEvidence> Assemblies,
    IReadOnlyList<string> PckFiles,
    IReadOnlyList<string> Errors,
    string Digest);

public sealed record ModWorkerIsolationEvidence(
    bool WorkerProcess,
    bool CoreLoadedModAssembly,
    bool WorkshopEnumerationAllowed,
    bool FormalSaveAccessAllowed,
    bool FormalSettingsAccessAllowed,
    bool FormalGameDataAccessAllowed,
    int BlockedAccessAttempts,
    int BlockedWriteAttempts,
    int ModRegistrationCount,
    int HarmonyPatchCount,
    int TemporaryFileCount,
    bool AssemblyLoadContextUnloaded);

public sealed record ModWorkerRunManifest(
    string Schema,
    string RunId,
    int ProcessId,
    string StartedUtc,
    string FinishedUtc,
    string Status,
    int ExitCode,
    string TargetGameVersion,
    string? Seed,
    string SnapshotId,
    string SnapshotContentSha256,
    string ModId,
    string? BuildId,
    string? BuildContentKey,
    string? ReplayPath,
    string? ReplaySha256,
    string InputDigest,
    ModWorkerDiscoverySummary? Discovery,
    ModWorkerIsolationEvidence Isolation,
    IReadOnlyList<ModWorkerError> Errors,
    IReadOnlyList<string> Outputs);

public static class ModWorkerJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    static ModWorkerJson()
    {
        Options.Converters.Add(new JsonStringEnumConverter());
    }
}
