using System.Text;
using System.Text.Json;
using SimulatorLab.Core.Mods;

if (args.Any(x => x is "--help" or "-h"))
{
    Console.WriteLine("RitsuStage7 --request <json> --output <fresh .build/runs directory> --probe <json> --replay-probe <json>");
    return 0;
}

var options = Parse(args);
var started = DateTimeOffset.UtcNow;
try
{
    EnsureFreshOutput(options.Output);
    var request = JsonSerializer.Deserialize<RitsuStage7Request>(File.ReadAllText(options.Request), ModWorkerJson.Options)
        ?? throw new InvalidDataException("stage 7 request is null");
    request = request with
    {
        ProbeDocumentPath = options.Probe ?? request.ProbeDocumentPath,
        ReplayProbeDocumentPath = options.ReplayProbe ?? request.ReplayProbeDocumentPath
    };
    EnsureReferencePaths(request);
    var probe = RitsuStage7Adapter.ReadProbe(request.ProbeDocumentPath ?? "");
    var replay = RitsuStage7Adapter.ReadProbe(request.ReplayProbeDocumentPath ?? "");
    var evaluation = RitsuStage7Adapter.Evaluate(request, probe, replay);
    WriteOutputs(options.Output, options.Request, request, evaluation, started);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        status = evaluation.Report.Status.ToString(), stopCode = evaluation.Report.StopCode,
        determinismDigest = evaluation.Report.DeterminismDigest, inputDigest = evaluation.Report.InputDigest,
        output = options.Output
    }, ModWorkerJson.Options));
    return evaluation.Report.Status == WatcherStage6Status.Pass ? 0 : evaluation.Report.Errors.Any(x => x.Code == AdapterErrorCode.Unsupported) ? 2 : 1;
}
catch (Exception ex)
{
    var error = AdapterErrorRecord.Create(AdapterErrorCode.RuntimeFailure, RitsuStage7Constants.ModId, "<ritsu-stage7>", ex.GetType().Name + ": " + ex.Message, "stage7/runner");
    WriteFailure(options.Output, options.Request, started, error);
    Console.Error.WriteLine(JsonSerializer.Serialize(new { status = "RuntimeFailure", error = error.StableSummary }, ModWorkerJson.Options));
    return 1;
}

static RunnerOptions Parse(string[] args)
{
    string? request = null, output = null, probe = null, replay = null;
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] is not ("--request" or "--output" or "--probe" or "--replay-probe"))
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal)) throw new InvalidDataException($"unknown option '{args[i]}'");
            continue;
        }
        var value = i + 1 < args.Length ? args[++i] : throw new InvalidDataException($"missing value for '{args[i]}'");
        if (args[i - 1] == "--request") request = value;
        else if (args[i - 1] == "--output") output = value;
        else if (args[i - 1] == "--probe") probe = value;
        else replay = value;
    }
    if (string.IsNullOrWhiteSpace(request) || string.IsNullOrWhiteSpace(output)) throw new InvalidDataException("--request and --output are required");
    return new(request!, output!, probe, replay);
}

static void EnsureFreshOutput(string output)
{
    var lab = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    var full = Path.GetFullPath(output);
    var runs = Path.Combine(lab, ".build", "runs") + Path.DirectorySeparatorChar;
    if (!full.StartsWith(runs, StringComparison.OrdinalIgnoreCase) || full.Contains("workshop-snapshot-", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("output must be a fresh directory under .build/runs");
    if (Directory.Exists(full) || File.Exists(full)) throw new InvalidDataException("output directory must be fresh");
    Directory.CreateDirectory(full);
}

static void EnsureReferencePaths(RitsuStage7Request request)
{
    var lab = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    var buildRoot = Path.Combine(lab, ".build") + Path.DirectorySeparatorChar;
    foreach (var path in new[] { request.WorkerRunManifestPath, request.NativeBuildManifestPath, request.ProbeDocumentPath, request.ReplayProbeDocumentPath }.Where(x => !string.IsNullOrWhiteSpace(x)))
    {
        var full = Path.GetFullPath(path!);
        if (!full.StartsWith(buildRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("runtime evidence references must be inside .build");
        if (full.Contains("\\steamapps\\workshop\\", StringComparison.OrdinalIgnoreCase) || full.Contains("\\userdata\\", StringComparison.OrdinalIgnoreCase) || full.Contains("\\saves\\", StringComparison.OrdinalIgnoreCase) || full.Contains("\\settings\\", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("runtime evidence cannot reference formal game data");
    }
}

static void WriteOutputs(string output, string requestPath, RitsuStage7Request request, RitsuStage7Evaluation evaluation, DateTimeOffset started)
{
    WriteJson(Path.Combine(output, "stage7-report.json"), new { evaluation.Report, determinismDigest = evaluation.Report.DeterminismDigest, conversion = evaluation.Conversion?.Report, catalog = new { evaluation.Catalog.SnapshotId, evaluation.Catalog.SnapshotContentSha256, evaluation.Catalog.TargetGameVersion }, resolution = new { evaluation.Resolution.DependencyClosure, evaluation.Resolution.LoadOrder, evaluation.Resolution.SelectedVariants }, startedUtc = started, finishedUtc = DateTimeOffset.UtcNow });
    WriteJson(Path.Combine(output, "input-summary.json"), new { requestPath = Path.GetFullPath(requestPath), request, inputDigest = evaluation.Report.InputDigest, snapshotWriteBlocked = evaluation.Report.SnapshotWriteBlocked, formalDataWriteBlocked = evaluation.Report.FormalDataWriteBlocked });
    File.WriteAllText(Path.Combine(output, "worker.log"), $"status={evaluation.Report.Status}\nstop={evaluation.Report.StopCode}\nmessage={evaluation.Report.StopMessage}\n", new UTF8Encoding(false));
}

static void WriteFailure(string output, string requestPath, DateTimeOffset started, AdapterErrorRecord error)
{
    try
    {
        if (!Directory.Exists(output)) Directory.CreateDirectory(output);
        WriteJson(Path.Combine(output, "stage7-report.json"), new { schema = RitsuStage7Constants.Schema, status = "Blocked", stopCode = error.StableCode, stopMessage = error.Message, errors = new[] { error }, requestPath = Path.GetFullPath(requestPath), startedUtc = started, finishedUtc = DateTimeOffset.UtcNow });
        File.WriteAllText(Path.Combine(output, "worker.log"), error.StableSummary, new UTF8Encoding(false));
    }
    catch { }
}

static void WriteJson(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, ModWorkerJson.Options), new UTF8Encoding(false));
sealed record RunnerOptions(string Request, string Output, string? Probe, string? ReplayProbe);
