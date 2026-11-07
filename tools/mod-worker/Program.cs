using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SimulatorLab.Core.Mods;

const string FixedSnapshotLeaf = "workshop-snapshot-20261106-163352";
const string FixedSnapshotContentSha256 = "7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73";
const string WorkerSchema = "mod-worker-run/v1";

var options = CommandLine.Parse(args);
if (options.Help)
{
    Console.WriteLine("SimulatorLab.ModWorker --input <json> --output <fresh-directory>");
    return 0;
}

var started = DateTimeOffset.UtcNow;
var runId = Guid.NewGuid().ToString("N");
var output = options.Output is null ? null : Path.GetFullPath(options.Output);
var outputOwned = false;
var errors = new List<ModWorkerError>();
var logs = new List<string>();
string? rawInput = null;
ModWorkerInput input = new();
string? inputDigest = null;
ModCatalog? catalog = null;
DependencyResolution? resolution = null;
ModWorkerDiscoverySummary? discovery = null;
var blockedAccess = 0;
var blockedWrites = 0;
var isolation = new ModWorkerIsolationEvidence(
    WorkerProcess: true,
    CoreLoadedModAssembly: false,
    WorkshopEnumerationAllowed: false,
    FormalSaveAccessAllowed: false,
    FormalSettingsAccessAllowed: false,
    FormalGameDataAccessAllowed: false,
    BlockedAccessAttempts: 0,
    BlockedWriteAttempts: 0,
    ModRegistrationCount: 0,
    HarmonyPatchCount: 0,
    TemporaryFileCount: 0,
    AssemblyLoadContextUnloaded: false);

try
{
    if (output is null) throw new WorkerFailure(ModWorkerErrorCode.InvalidFormat, "<command-line>", "--output is required");
    EnsureFreshOutput(output);
    Directory.CreateDirectory(output);
    outputOwned = true;
    logs.Add($"worker-start pid={Environment.ProcessId} runId={runId}");

    if (options.Input is null) throw new WorkerFailure(ModWorkerErrorCode.InvalidFormat, "<command-line>", "--input is required");
    rawInput = File.ReadAllText(options.Input);
    inputDigest = Digest(rawInput);
    input = JsonSerializer.Deserialize<ModWorkerInput>(rawInput, ModWorkerJson.Options)
        ?? throw new WorkerFailure(ModWorkerErrorCode.InvalidFormat, options.Input, "worker input is null");
    if (!string.IsNullOrWhiteSpace(options.Seed)) input = input with { Seed = options.Seed };
    if (!string.IsNullOrWhiteSpace(options.ReplayPath)) input = input with { ReplayPath = options.ReplayPath };
    if (!string.IsNullOrWhiteSpace(options.BuildManifestPath)) input = input with { BuildManifestPath = options.BuildManifestPath };
    logs.Add($"input digest={inputDigest}");

    var snapshot = RequireFixedSnapshot(input, errors);
    if (snapshot is not null)
    {
        catalog = ModCatalogParser.Parse(snapshot.Directory, snapshot.Manifest, ModCatalogConstants.TargetGameVersion);
        foreach (var error in catalog.Errors)
            errors.Add(new(ModWorkerErrorCode.InvalidFormat, error.Path, $"catalog:{error.Code}:{error.Message}"));

        if (!string.Equals(input.TargetGameVersion ?? ModCatalogConstants.TargetGameVersion,
                ModCatalogConstants.TargetGameVersion, StringComparison.Ordinal))
            errors.Add(new(ModWorkerErrorCode.UnsupportedVersion, input.TargetGameVersion ?? "", "target game version must be exactly 0.111.0"));

        if (string.IsNullOrWhiteSpace(input.ModId))
            errors.Add(new(ModWorkerErrorCode.DependencyMissing, "<modId>", "modId is required"));
        else if (catalog.Errors.Count == 0)
        {
            resolution = DependencyResolver.Resolve(catalog, [input.ModId]);
            foreach (var diagnostic in resolution.Diagnostics)
                errors.Add(new(MapDiagnostic(diagnostic.Code), diagnostic.ModId, diagnostic.Message));
            foreach (var cycle in resolution.Cycles)
                errors.Add(new(ModWorkerErrorCode.DependencyMissing, cycle[0], $"dependency cycle: {string.Join(" -> ", cycle)}"));
        }

        var approved = resolution is null || catalog is null
            ? new List<ApprovedFile>()
            : GetApprovedFiles(catalog, resolution, snapshot.Directory);
        ValidateRequestedPaths(input, approved, snapshot.Directory, errors);
        ValidateAccessAttempts(input, approved, snapshot.Directory, errors, ref blockedAccess);
        ValidateWriteAttempts(input, errors, ref blockedWrites);
        var replaySha = ReadReplayEvidence(input.ReplayPath, output, snapshot.Directory, errors);
        var build = ReadBuildEvidence(input.BuildManifestPath, output, snapshot.Directory, errors);

        if (errors.Count == 0)
        {
            discovery = Discover(input.ModId!, resolution!, approved, errors, ref isolation);
            if (discovery is not null)
                logs.Add($"discovery digest={discovery.Digest} files={discovery.Files.Count} assemblies={discovery.Assemblies.Count}");
        }

        isolation = isolation with
        {
            BlockedAccessAttempts = blockedAccess,
            BlockedWriteAttempts = blockedWrites,
            AssemblyLoadContextUnloaded = discovery is null || isolation.AssemblyLoadContextUnloaded
        };
        WriteOutputs(output, runId, started, input, catalog, discovery, isolation, errors, inputDigest!, replaySha, build, logs);
        var exit = errors.Count == 0 ? 0 : (errors.Any(e => e.Code is ModWorkerErrorCode.AccessBlocked or ModWorkerErrorCode.WriteBlocked) ? 2 : 1);
        return exit;
    }

    ValidateAccessAttempts(input, [], input.SnapshotDirectory ?? "", errors, ref blockedAccess);
    ValidateWriteAttempts(input, errors, ref blockedWrites);
    isolation = isolation with { BlockedAccessAttempts = blockedAccess, BlockedWriteAttempts = blockedWrites };
    WriteOutputs(output, runId, started, input, catalog, discovery, isolation, errors, inputDigest ?? Digest(rawInput ?? ""), null, null, logs);
    return 1;
}
catch (WorkerFailure ex)
{
    errors.Add(new(ex.Code, ex.Path, ex.Message));
    if (output is not null && outputOwned)
    {
        try
        {
            Directory.CreateDirectory(output);
            WriteOutputs(output, runId, started, input, catalog, discovery, isolation with
            {
                BlockedAccessAttempts = blockedAccess,
                BlockedWriteAttempts = blockedWrites
            }, errors, inputDigest ?? Digest(rawInput ?? ""), null, null, logs);
        }
        catch (Exception writeFailure) { Console.Error.WriteLine($"worker-output-failure:{writeFailure.Message}"); }
    }
    Console.Error.WriteLine(ex.Message);
    return 1;
}
catch (Exception ex)
{
    errors.Add(new(ModWorkerErrorCode.RuntimeFailure, "<worker>", ex.GetType().Name + ": " + ex.Message));
    if (output is not null && outputOwned)
    {
        try
        {
            Directory.CreateDirectory(output);
            WriteOutputs(output, runId, started, input, catalog, discovery, isolation with
            {
                BlockedAccessAttempts = blockedAccess,
                BlockedWriteAttempts = blockedWrites
            }, errors, inputDigest ?? Digest(rawInput ?? ""), null, null, logs);
        }
        catch (Exception writeFailure) { Console.Error.WriteLine($"worker-output-failure:{writeFailure.Message}"); }
    }
    Console.Error.WriteLine($"worker-runtime-failure:{ex.GetType().Name}:{ex.Message}");
    return 1;
}

static void EnsureFreshOutput(string output)
{
    if (Directory.Exists(output) || File.Exists(output))
        throw new WorkerFailure(ModWorkerErrorCode.RuntimeFailure, output, "output directory must be new for every worker process");
    var normalized = output.Replace('\\', '/');
    if (normalized.Contains("/workshop-snapshot-", StringComparison.OrdinalIgnoreCase) ||
        normalized.Contains("/steamapps/workshop/", StringComparison.OrdinalIgnoreCase))
        throw new WorkerFailure(ModWorkerErrorCode.PathEscape, output, "worker output cannot be inside a snapshot or Workshop directory");
}

static SnapshotPaths? RequireFixedSnapshot(ModWorkerInput input, List<ModWorkerError> errors)
{
    if (string.IsNullOrWhiteSpace(input.SnapshotDirectory))
    {
        errors.Add(new(ModWorkerErrorCode.MissingFile, "<snapshotDirectory>", "snapshotDirectory is required"));
        return null;
    }
    var directory = Path.GetFullPath(input.SnapshotDirectory);
    if (!string.Equals(Path.GetFileName(directory), FixedSnapshotLeaf, StringComparison.Ordinal))
    {
        errors.Add(new(ModWorkerErrorCode.PathEscape, input.SnapshotDirectory, $"only fixed snapshot {FixedSnapshotLeaf} is allowed"));
        return null;
    }
    var expectedManifest = directory + ".manifest.json";
    var suppliedManifest = string.IsNullOrWhiteSpace(input.SnapshotManifestPath)
        ? expectedManifest
        : Path.GetFullPath(input.SnapshotManifestPath);
    if (!string.Equals(suppliedManifest, expectedManifest, StringComparison.OrdinalIgnoreCase))
        errors.Add(new(ModWorkerErrorCode.PathEscape, suppliedManifest, "snapshot manifest must be the fixed snapshot sibling manifest"));
    if (!File.Exists(suppliedManifest))
        errors.Add(new(ModWorkerErrorCode.MissingFile, suppliedManifest, "snapshot manifest is missing"));
    if (!Directory.Exists(directory)) errors.Add(new(ModWorkerErrorCode.MissingFile, directory, "fixed snapshot directory is missing"));
    if (!File.Exists(expectedManifest)) errors.Add(new(ModWorkerErrorCode.MissingFile, expectedManifest, "fixed snapshot manifest is missing"));
    if (errors.Count > 0) return null;
    try
    {
        using var document = JsonDocument.Parse(File.ReadAllText(expectedManifest));
        var root = document.RootElement;
        var snapshotId = root.TryGetProperty("SnapshotDirectory", out var id) ? id.GetString() : null;
        var contentHash = root.TryGetProperty("SnapshotContentSha256", out var hash) ? hash.GetString() : null;
        if (!string.Equals(snapshotId, FixedSnapshotLeaf, StringComparison.Ordinal) ||
            !string.Equals(contentHash, FixedSnapshotContentSha256, StringComparison.OrdinalIgnoreCase))
            errors.Add(new(ModWorkerErrorCode.RuntimeFailure, expectedManifest, "fixed snapshot manifest identity or content hash does not match the approved snapshot"));
    }
    catch (JsonException ex)
    {
        errors.Add(new(ModWorkerErrorCode.InvalidFormat, expectedManifest, $"fixed snapshot manifest: {ex.Message}"));
    }
    if (errors.Count > 0) return null;
    return new SnapshotPaths(directory, expectedManifest);
}

static List<ApprovedFile> GetApprovedFiles(ModCatalog catalog, DependencyResolution resolution, string snapshotDirectory)
{
    var result = new List<ApprovedFile>();
    foreach (var modId in resolution.LoadOrder)
    {
        var manifest = catalog.Manifests.FirstOrDefault(x => x.ModId == modId);
        if (manifest is null) continue;
        var variant = resolution.SelectedVariants.FirstOrDefault(x => x.ModId == modId);
        var files = variant?.Files ?? manifest.PackageFiles;
        foreach (var file in files.OrderBy(x => x.RelativePath, StringComparer.Ordinal))
        {
            var kind = file.RelativePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? "dll" :
                file.RelativePath.EndsWith(".pck", StringComparison.OrdinalIgnoreCase) ? "pck" : "manifest";
            result.Add(new(modId, manifest.Source.SnapshotRelativeRoot, file, kind) { SnapshotDirectory = snapshotDirectory });
        }
    }
    return result.OrderBy(x => x.ModId, StringComparer.Ordinal).ThenBy(x => x.File.RelativePath, StringComparer.Ordinal).ToList();
}

static void ValidateRequestedPaths(ModWorkerInput input, IReadOnlyList<ApprovedFile> approved, string snapshotDirectory, List<ModWorkerError> errors)
{
    var requested = (input.RequestedPaths ?? []).Concat([input.RequestedDllPath, input.RequestedPckPath])
        .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToArray();
    var approvedByPath = approved.ToDictionary(x => FullPath(snapshotDirectory, x), StringComparer.OrdinalIgnoreCase);
    foreach (var path in requested)
    {
        var candidate = Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(snapshotDirectory, path));
        if (!approvedByPath.TryGetValue(candidate, out var entry))
            errors.Add(new(ModWorkerErrorCode.PathEscape, path, "requested path is not in the approved manifest/DLL/PCK set"));
        else if (string.Equals(path, input.RequestedDllPath, StringComparison.Ordinal) && entry.Kind != "dll")
            errors.Add(new(ModWorkerErrorCode.InvalidFormat, path, "requested DLL path does not identify an approved DLL"));
        else if (string.Equals(path, input.RequestedPckPath, StringComparison.Ordinal) && entry.Kind != "pck")
            errors.Add(new(ModWorkerErrorCode.InvalidFormat, path, "requested PCK path does not identify an approved PCK"));
    }
}

static void ValidateAccessAttempts(ModWorkerInput input, IReadOnlyList<ApprovedFile> approved, string snapshotDirectory, List<ModWorkerError> errors, ref int blocked)
{
    var approvedPaths = approved.Select(x => FullPath(snapshotDirectory, x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var attempt in input.AccessAttempts ?? [])
    {
        blocked++;
        var path = attempt.Path?.Trim() ?? "<empty>";
        var normalized = path.Replace('\\', '/');
        var reason = IsProtectedPath(normalized) ? "formal Workshop/save/settings/game-data access is blocked" :
            (Path.IsPathRooted(path) && approvedPaths.Contains(Path.GetFullPath(path)) ? "approved path access must use the static discovery path" : "worker exposes no arbitrary filesystem view");
        errors.Add(new(ModWorkerErrorCode.AccessBlocked, path, $"{attempt.Operation ?? "read"}: {reason}"));
    }
}

static void ValidateWriteAttempts(ModWorkerInput input, List<ModWorkerError> errors, ref int blocked)
{
    foreach (var attempt in input.WriteAttempts ?? [])
    {
        blocked++;
        errors.Add(new(ModWorkerErrorCode.WriteBlocked, attempt.Path ?? "<empty>", $"{attempt.Operation ?? "write"}: Mod writes are isolated and never reported as successful"));
    }
}

static string? ReadReplayEvidence(string? replayPath, string output, string snapshotDirectory, List<ModWorkerError> errors)
{
    if (string.IsNullOrWhiteSpace(replayPath)) return null;
    var full = Path.GetFullPath(replayPath);
    if (IsProtectedPath(full) || full.StartsWith(Path.GetFullPath(snapshotDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
    {
        errors.Add(new(ModWorkerErrorCode.PathEscape, replayPath, "replay input cannot be a Workshop snapshot or formal game data path"));
        return null;
    }
    if (!File.Exists(full)) { errors.Add(new(ModWorkerErrorCode.MissingFile, replayPath, "replay input is missing")); return null; }
    return Sha256(full);
}

static BuildEvidence ReadBuildEvidence(string? path, string output, string snapshotDirectory, List<ModWorkerError> errors)
{
    if (string.IsNullOrWhiteSpace(path)) return new(null, null);
    var full = Path.GetFullPath(path);
    if (IsProtectedPath(full) || full.StartsWith(Path.GetFullPath(snapshotDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
    {
        errors.Add(new(ModWorkerErrorCode.PathEscape, path, "build manifest cannot come from Workshop or formal game data"));
        return new(null, null);
    }
    if (!File.Exists(full)) { errors.Add(new(ModWorkerErrorCode.MissingFile, path, "build manifest is missing")); return new(null, null); }
    try
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(full));
        var root = doc.RootElement;
        var buildId = root.TryGetProperty("buildId", out var b) ? b.GetString() : null;
        var manifest = root.TryGetProperty("manifest", out var m) ? m : root;
        var key = manifest.TryGetProperty("contentKey", out var k) ? k.GetString() : null;
        return new(buildId, key);
    }
    catch (JsonException ex) { errors.Add(new(ModWorkerErrorCode.InvalidFormat, path, $"build manifest: {ex.Message}")); return new(null, null); }
}

static ModWorkerDiscoverySummary? Discover(string modId, DependencyResolution resolution, IReadOnlyList<ApprovedFile> approved, List<ModWorkerError> errors, ref ModWorkerIsolationEvidence isolation)
{
    var assemblies = new List<ModWorkerAssemblyEvidence>();
    var files = new List<ModWorkerFileEvidence>();
    var pcks = new List<string>();
    var dlls = approved.Where(x => x.Kind == "dll").OrderBy(x => x.ModId, StringComparer.Ordinal).ThenBy(x => x.File.RelativePath, StringComparer.Ordinal).ToList();
    var loadPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    try
    {
        foreach (var entry in dlls)
        {
            var path = FullPathFromApproved(entry);
            ValidateFile(entry, path, errors);
            var name = AssemblyName.GetAssemblyName(path);
            if (!string.IsNullOrWhiteSpace(name.Name)) loadPaths[name.Name] = path;
        }
        if (errors.Count > 0) return null;
        var alc = new ApprovedLoadContext(loadPaths);
        try
        {
            foreach (var entry in dlls)
            {
                var path = FullPathFromApproved(entry);
                var name = AssemblyName.GetAssemblyName(path);
                var assembly = alc.LoadFromAssemblyPath(path);
                assemblies.Add(new(entry.ModId, entry.File.RelativePath, name.Name ?? "", name.Version?.ToString(), assembly.ManifestModule.ModuleVersionId.ToString("D")));
            }
            foreach (var entry in approved.Where(x => x.Kind is "dll" or "pck" or "manifest"))
            {
                var path = FullPathFromApproved(entry);
                ValidateFile(entry, path, errors);
                if (errors.Count > 0) return null;
                var kind = entry.Kind;
                if (kind == "pck") pcks.Add(entry.SnapshotRelativeRoot + "/" + entry.File.RelativePath);
                files.Add(new(entry.ModId, entry.File.RelativePath, kind, entry.File.Length, entry.File.Sha256));
            }
        }
        finally
        {
            alc.Unload();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            isolation = isolation with { AssemblyLoadContextUnloaded = true };
        }
    }
    catch (FileNotFoundException ex) { errors.Add(new(ModWorkerErrorCode.MissingFile, ex.FileName ?? "<dll>", ex.Message)); return null; }
    catch (BadImageFormatException ex) { errors.Add(new(ModWorkerErrorCode.InvalidFormat, ex.FileName ?? "<dll>", ex.Message)); return null; }
    catch (FileLoadException ex) { errors.Add(new(ModWorkerErrorCode.RuntimeFailure, ex.FileName ?? "<dll>", ex.Message)); return null; }
    catch (Exception ex) { errors.Add(new(ModWorkerErrorCode.RuntimeFailure, "<dll>", ex.GetType().Name + ": " + ex.Message)); return null; }

    var digestInput = new
    {
        schema = "mod-worker-discovery/v1",
        targetGameVersion = ModCatalogConstants.TargetGameVersion,
        modId,
        dependencyClosure = resolution.DependencyClosure.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
        files = files.OrderBy(x => x.ModId, StringComparer.Ordinal).ThenBy(x => x.RelativePath, StringComparer.Ordinal),
        assemblies = assemblies.OrderBy(x => x.ModId, StringComparer.Ordinal).ThenBy(x => x.RelativePath, StringComparer.Ordinal),
        pcks = pcks.OrderBy(x => x, StringComparer.Ordinal),
        errors = errors.Select(x => x.StableSummary).OrderBy(x => x, StringComparer.Ordinal)
    };
    var digest = Digest(JsonSerializer.Serialize(digestInput, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    return new("mod-worker-discovery/v1", ModCatalogConstants.TargetGameVersion, modId,
        resolution.DependencyClosure.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
        files.OrderBy(x => x.ModId, StringComparer.Ordinal).ThenBy(x => x.RelativePath, StringComparer.Ordinal).ToArray(),
        assemblies.OrderBy(x => x.ModId, StringComparer.Ordinal).ThenBy(x => x.RelativePath, StringComparer.Ordinal).ToArray(),
        pcks.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
        errors.Select(x => x.StableSummary).OrderBy(x => x, StringComparer.Ordinal).ToArray(), digest);
}

static void ValidateFile(ApprovedFile entry, string path, List<ModWorkerError> errors)
{
    if (!File.Exists(path)) { errors.Add(new(ModWorkerErrorCode.MissingFile, entry.File.RelativePath, "approved snapshot file is missing")); return; }
    var info = new FileInfo(path);
    if (info.Length != entry.File.Length) { errors.Add(new(ModWorkerErrorCode.RuntimeFailure, entry.File.RelativePath, $"approved file length mismatch expected={entry.File.Length} actual={info.Length}")); return; }
    var hash = Sha256(path);
    if (!string.Equals(hash, entry.File.Sha256, StringComparison.OrdinalIgnoreCase)) errors.Add(new(ModWorkerErrorCode.RuntimeFailure, entry.File.RelativePath, "approved file SHA-256 mismatch"));
}

static void WriteOutputs(string output, string runId, DateTimeOffset started, ModWorkerInput input, ModCatalog? catalog, ModWorkerDiscoverySummary? discovery, ModWorkerIsolationEvidence isolation, IReadOnlyList<ModWorkerError> errors, string inputDigest, string? replaySha, BuildEvidence? build, List<string> logs)
{
    var finished = DateTimeOffset.UtcNow;
    var status = errors.Count == 0 ? "succeeded" : errors.Any(e => e.Code is ModWorkerErrorCode.AccessBlocked or ModWorkerErrorCode.WriteBlocked) ? "blocked" : "failed";
    var exit = status == "succeeded" ? 0 : status == "blocked" ? 2 : 1;
    var includeBuildManifest = !string.IsNullOrWhiteSpace(input.BuildManifestPath) && File.Exists(input.BuildManifestPath);
    var outputs = !includeBuildManifest
        ? new[] { "run-manifest.json", "worker.log", "input-summary.json" }
        : new[] { "build-manifest.json", "run-manifest.json", "worker.log", "input-summary.json" };
    var manifest = new ModWorkerRunManifest(WorkerSchema, runId, Environment.ProcessId, started.ToString("O"), finished.ToString("O"), status, exit,
        input.TargetGameVersion ?? ModCatalogConstants.TargetGameVersion, input.Seed,
        catalog?.SnapshotId ?? Path.GetFileName(input.SnapshotDirectory ?? ""), catalog?.SnapshotContentSha256 ?? "", input.ModId ?? "<missing>",
        build?.BuildId, build?.ContentKey, input.ReplayPath, replaySha, inputDigest, discovery,
        isolation with { ModRegistrationCount = 0, HarmonyPatchCount = 0, TemporaryFileCount = 0 },
        errors.OrderBy(e => e.Code).ThenBy(e => e.Path, StringComparer.Ordinal).ThenBy(e => e.Message, StringComparer.Ordinal).ToArray(),
        outputs);
    if (!string.IsNullOrWhiteSpace(input.BuildManifestPath) && File.Exists(input.BuildManifestPath))
        File.Copy(input.BuildManifestPath, Path.Combine(output, "build-manifest.json"), overwrite: false);
    WriteJson(Path.Combine(output, "run-manifest.json"), manifest);
    var inputSummary = new
    {
        schema = "mod-worker-input/v1",
        inputDigest,
        snapshotId = catalog?.SnapshotId,
        snapshotContentSha256 = catalog?.SnapshotContentSha256,
        modId = input.ModId,
        targetGameVersion = input.TargetGameVersion ?? ModCatalogConstants.TargetGameVersion,
        seed = input.Seed,
        requestedPathCount = (input.RequestedPaths ?? []).Count + (input.RequestedDllPath is null ? 0 : 1) + (input.RequestedPckPath is null ? 0 : 1),
        accessAttemptCount = (input.AccessAttempts ?? []).Count,
        writeAttemptCount = (input.WriteAttempts ?? []).Count,
        buildId = build?.BuildId,
        buildContentKey = build?.ContentKey,
        replaySha256 = replaySha
    };
    WriteJson(Path.Combine(output, "input-summary.json"), inputSummary);
    logs.Add($"worker-end status={status} exit={exit} blockedAccess={isolation.BlockedAccessAttempts} blockedWrites={isolation.BlockedWriteAttempts}");
    File.WriteAllLines(Path.Combine(output, "worker.log"), logs, new UTF8Encoding(false));
}

static string FullPath(string snapshotDirectory, ApprovedFile entry) => Path.GetFullPath(Path.Combine(snapshotDirectory, entry.SnapshotRelativeRoot, entry.File.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
static string FullPathFromApproved(ApprovedFile entry) => Path.GetFullPath(Path.Combine(entry.SnapshotDirectory, entry.SnapshotRelativeRoot, entry.File.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
static bool IsProtectedPath(string path)
{
    var p = path.Replace('\\', '/');
    return p.Contains("/steamapps/workshop/", StringComparison.OrdinalIgnoreCase) ||
        p.Contains("/steamapps/common/", StringComparison.OrdinalIgnoreCase) ||
        p.Contains("/userdata/", StringComparison.OrdinalIgnoreCase) ||
        p.Contains("/saves/", StringComparison.OrdinalIgnoreCase) ||
        p.Contains("/save/", StringComparison.OrdinalIgnoreCase) ||
        p.Contains("/settings/", StringComparison.OrdinalIgnoreCase) ||
        p.Contains("/config/", StringComparison.OrdinalIgnoreCase) ||
        p.Contains("/appdata/", StringComparison.OrdinalIgnoreCase) ||
        p.Contains("/localappdata/", StringComparison.OrdinalIgnoreCase) ||
        p.Contains("/savedata/", StringComparison.OrdinalIgnoreCase) ||
        p.Contains("/gamedata/", StringComparison.OrdinalIgnoreCase) ||
        p.Contains("/game-data/", StringComparison.OrdinalIgnoreCase);
}
static ModWorkerErrorCode MapDiagnostic(ModCatalogErrorCode code) => code switch
{
    ModCatalogErrorCode.MissingFile => ModWorkerErrorCode.MissingFile,
    ModCatalogErrorCode.UnsupportedVersion => ModWorkerErrorCode.UnsupportedVersion,
    ModCatalogErrorCode.DependencyMissing => ModWorkerErrorCode.DependencyMissing,
    ModCatalogErrorCode.PathEscape => ModWorkerErrorCode.PathEscape,
    ModCatalogErrorCode.InvalidFormat => ModWorkerErrorCode.InvalidFormat,
    _ => ModWorkerErrorCode.RuntimeFailure
};
static void WriteJson(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, ModWorkerJson.Options), new UTF8Encoding(false));

sealed record SnapshotPaths(string Directory, string Manifest);
sealed record ApprovedFile(string ModId, string SnapshotRelativeRoot, ModFileSummary File, string Kind)
{
    public string SnapshotDirectory { get; init; } = "";
}
sealed record BuildEvidence(string? BuildId, string? ContentKey);
sealed class WorkerFailure : Exception
{
    public ModWorkerErrorCode Code { get; }
    public string Path { get; }
    public WorkerFailure(ModWorkerErrorCode code, string path, string message) : base(message)
    {
        Code = code;
        Path = path;
    }
}

sealed class ApprovedLoadContext(IReadOnlyDictionary<string, string> paths) : AssemblyLoadContext("simulatorlab-mod-worker", isCollectible: true)
{
    protected override Assembly? Load(AssemblyName assemblyName) =>
        assemblyName.Name is not null && paths.TryGetValue(assemblyName.Name, out var path) ? LoadFromAssemblyPath(path) : null;
}

static class CommandLine
{
    public static Options Parse(string[] args)
    {
        string? input = null, output = null, seed = null, replay = null, build = null;
        var help = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help": case "-h": help = true; break;
                case "--input": input = Next(args, ref i); break;
                case "--output": output = Next(args, ref i); break;
                case "--seed": seed = Next(args, ref i); break;
                case "--replay": replay = Next(args, ref i); break;
                case "--build-manifest": build = Next(args, ref i); break;
                default: throw new WorkerFailure(ModWorkerErrorCode.InvalidFormat, "<command-line>", $"unknown argument '{args[i]}'");
            }
        }
        return new(input, output, seed, replay, build, help);
    }
    private static string Next(string[] args, ref int index) => index + 1 < args.Length ? args[++index] : throw new WorkerFailure(ModWorkerErrorCode.InvalidFormat, "<command-line>", $"missing value for '{args[index]}'");
}
sealed record Options(string? Input, string? Output, string? Seed, string? ReplayPath, string? BuildManifestPath, bool Help);
