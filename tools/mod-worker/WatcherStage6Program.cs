using System.Text;
using System.Text.Json;
using SimulatorLab.Core.Mods;

if (args.Any(x => x is "--help" or "-h"))
{
    Console.WriteLine("WatcherStage6 --request <json> --output <fresh .build/runs directory> [--probe <json>] [--replay-probe <json>]");
    return 0;
}

var options = Parse(args);
var started = DateTimeOffset.UtcNow;
try
{
    EnsureFreshOutput(options.Output);
    var request = JsonSerializer.Deserialize<WatcherStage6Request>(
        File.ReadAllText(options.Request), ModWorkerJson.Options)
        ?? throw new InvalidDataException("stage 6 request is null");
    if (!string.IsNullOrWhiteSpace(options.Probe))
        request = request with { ProbeDocumentPath = options.Probe };
    if (!string.IsNullOrWhiteSpace(options.ReplayProbe))
        request = request with { ReplayProbeDocumentPath = options.ReplayProbe };
    EnsureReferencePaths(request, Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../")));

    var probe = WatcherStage6Adapter.ReadProbe(request.ProbeDocumentPath ?? "");
    var replay = WatcherStage6Adapter.ReadProbe(request.ReplayProbeDocumentPath ?? "");
    var evaluation = WatcherStage6Adapter.Evaluate(request, probe, replay);
    WriteOutputs(options.Output, options.Request, request, evaluation, started, null);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        status = evaluation.Report.Status.ToString(),
        stopCode = evaluation.Report.StopCode,
        determinismDigest = evaluation.Report.DeterminismDigest,
        inputDigest = evaluation.Report.InputDigest,
        output = options.Output
    }, ModWorkerJson.Options));
    return evaluation.Report.Status == WatcherStage6Status.Pass ? 0 :
        evaluation.Report.Errors.Any(x => x.Code == AdapterErrorCode.Unsupported) ? 2 : 1;
}
catch (Exception ex)
{
    var error = AdapterErrorRecord.Create(AdapterErrorCode.RuntimeFailure, WatcherStage6Constants.ModId,
        "<watcher-stage6>", ex.GetType().Name + ": " + ex.Message, "stage6/runner");
    WriteFailure(options.Output, options.Request, started, error);
    Console.Error.WriteLine(JsonSerializer.Serialize(new { status = "RuntimeFailure", error = error.StableSummary }, ModWorkerJson.Options));
    return 1;
}

static RunnerOptions Parse(string[] args)
{
    string? request = null, output = null, probe = null, replay = null;
    for (var i = 0; i < args.Length; i++)
    {
        var option = args[i];
        if (option is not ("--request" or "--output" or "--probe" or "--replay-probe"))
        {
            if (option.StartsWith("--", StringComparison.Ordinal))
                throw new InvalidDataException($"unknown option '{option}'");
            continue;
        }
        var value = Next(args, ref i);
        if (option == "--request") request = value;
        else if (option == "--output") output = value;
        else if (option == "--probe") probe = value;
        else replay = value;
    }
    if (string.IsNullOrWhiteSpace(request) || string.IsNullOrWhiteSpace(output))
        throw new InvalidDataException("--request and --output are required");
    return new(request!, output!, probe, replay);
}

static string Next(string[] args, ref int index) => index + 1 < args.Length
    ? args[++index]
    : throw new InvalidDataException($"missing value for '{args[index]}'");

static void EnsureFreshOutput(string output)
{
    var lab = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    var full = Path.GetFullPath(output);
    var runs = Path.Combine(lab, ".build", "runs") + Path.DirectorySeparatorChar;
    if (!full.StartsWith(runs, StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("output must be a new directory under .build/runs");
    if (Directory.Exists(full) || File.Exists(full))
        throw new InvalidDataException("output directory must be fresh");
    if (full.Contains("workshop-snapshot-", StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("output cannot be inside a fixed snapshot");
    Directory.CreateDirectory(full);
}

static void EnsureReferencePaths(WatcherStage6Request request, string lab)
{
    var buildRoot = Path.Combine(lab, ".build") + Path.DirectorySeparatorChar;
    foreach (var path in new[] { request.WorkerRunManifestPath, request.NativeBuildManifestPath,
        request.ProbeDocumentPath, request.ReplayProbeDocumentPath }.Where(x => !string.IsNullOrWhiteSpace(x)))
    {
        var full = Path.GetFullPath(path!);
        if (!full.StartsWith(buildRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("runtime evidence references must be inside .build");
        if (full.Contains("workshop-snapshot-", StringComparison.OrdinalIgnoreCase) ||
            full.Contains("\\steamapps\\workshop\\", StringComparison.OrdinalIgnoreCase) ||
            full.Contains("\\userdata\\", StringComparison.OrdinalIgnoreCase) ||
            full.Contains("\\saves\\", StringComparison.OrdinalIgnoreCase) ||
            full.Contains("\\settings\\", StringComparison.OrdinalIgnoreCase) ||
            full.Contains("\\config\\", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("runtime evidence cannot reference a fixed snapshot or formal game data");
    }
}

static void WriteOutputs(string output, string requestPath, WatcherStage6Request request,
    WatcherStage6Evaluation evaluation, DateTimeOffset started, string? failure)
{
    WriteJson(Path.Combine(output, "stage6-report.json"), new
    {
        evaluation.Report,
        determinismDigest = evaluation.Report.DeterminismDigest,
        conversion = evaluation.Conversion?.Report,
        catalog = new { evaluation.Catalog.SnapshotId, evaluation.Catalog.SnapshotContentSha256, evaluation.Catalog.TargetGameVersion },
        resolution = new { evaluation.Resolution.DependencyClosure, evaluation.Resolution.LoadOrder, evaluation.Resolution.SelectedVariants },
        startedUtc = started,
        finishedUtc = DateTimeOffset.UtcNow,
        failure
    });
    WriteJson(Path.Combine(output, "action-script.json"), evaluation.Report.ActionScript);
    WriteJson(Path.Combine(output, "input-summary.json"), new
    {
        requestPath = Path.GetFullPath(requestPath),
        request,
        inputDigest = evaluation.Report.InputDigest,
        snapshotWriteBlocked = evaluation.Report.SnapshotWriteBlocked,
        formalDataWriteBlocked = evaluation.Report.FormalDataWriteBlocked
    });
    if (!string.IsNullOrWhiteSpace(request.WorkerRunManifestPath))
        WriteJson(Path.Combine(output, "worker-run-reference.json"), new { path = request.WorkerRunManifestPath });
    if (!string.IsNullOrWhiteSpace(request.NativeBuildManifestPath))
        WriteJson(Path.Combine(output, "native-run-reference.json"), new { path = request.NativeBuildManifestPath });
    File.WriteAllText(Path.Combine(output, "worker.log"),
        $"status={evaluation.Report.Status}\nstop={evaluation.Report.StopCode}\nmessage={evaluation.Report.StopMessage}\n",
        new UTF8Encoding(false));
}

static void WriteFailure(string output, string requestPath, DateTimeOffset started, AdapterErrorRecord error)
{
    try
    {
        if (!Directory.Exists(output)) Directory.CreateDirectory(output);
        WriteJson(Path.Combine(output, "stage6-report.json"), new
        {
            schema = WatcherStage6Constants.Schema,
            status = "Blocked",
            stopCode = error.StableCode,
            stopMessage = error.Message,
            errors = new[] { error },
            requestPath = Path.GetFullPath(requestPath),
            startedUtc = started,
            finishedUtc = DateTimeOffset.UtcNow
        });
        File.WriteAllText(Path.Combine(output, "worker.log"), error.StableSummary, new UTF8Encoding(false));
    }
    catch
    {
        // Preserve the original process failure; do not mask it with output IO.
    }
}

static void WriteJson(string path, object value) => File.WriteAllText(path,
    JsonSerializer.Serialize(value, ModWorkerJson.Options), new UTF8Encoding(false));

sealed record RunnerOptions(string Request, string Output, string? Probe, string? ReplayProbe);
