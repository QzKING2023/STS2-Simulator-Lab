using System.Text;
using System.Text.Json;
using SimulatorLab.Core.Mods;

if (args.Any(x => x is "--help" or "-h"))
{
    Console.WriteLine("KaguyaStage8 --request <json> --output <fresh .build/runs directory> --probe <json> --replay-probe <json>");
    return 0;
}

var options = Parse(args);
var started = DateTimeOffset.UtcNow;
try
{
    EnsureFreshOutput(options.Output);
    var request = JsonSerializer.Deserialize<KaguyaStage8Request>(
        File.ReadAllText(options.Request), ModWorkerJson.Options)
        ?? throw new InvalidDataException("stage 8 request is null");
    request = request with
    {
        ProbeDocumentPath = options.Probe ?? request.ProbeDocumentPath,
        ReplayProbeDocumentPath = options.ReplayProbe ?? request.ReplayProbeDocumentPath
    };
    EnsureReferencePaths(request);
    var probe = KaguyaStage8Adapter.ReadProbe(request.ProbeDocumentPath ?? "");
    var replay = KaguyaStage8Adapter.ReadProbe(request.ReplayProbeDocumentPath ?? "");
    var evaluation = KaguyaStage8Adapter.Evaluate(request, probe, replay);
    WriteOutputs(options.Output, options.Request, request, evaluation, started);
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
    var error = AdapterErrorRecord.Create(AdapterErrorCode.RuntimeFailure,
        KaguyaStage8Constants.ModId, "<kaguya-stage8>",
        ex.GetType().Name + ": " + ex.Message, "stage8/runner");
    WriteFailure(options.Output, options.Request, started, error);
    Console.Error.WriteLine(JsonSerializer.Serialize(new
    {
        status = "RuntimeFailure", error = error.StableSummary
    }, ModWorkerJson.Options));
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
        if (++i >= args.Length) throw new InvalidDataException($"missing value for '{option}'");
        if (option == "--request") request = args[i];
        else if (option == "--output") output = args[i];
        else if (option == "--probe") probe = args[i];
        else replay = args[i];
    }
    if (string.IsNullOrWhiteSpace(request) || string.IsNullOrWhiteSpace(output))
        throw new InvalidDataException("--request and --output are required");
    return new(request!, output!, probe, replay);
}

static void EnsureFreshOutput(string output)
{
    var lab = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    var full = Path.GetFullPath(output);
    var runs = Path.Combine(lab, ".build", "runs") + Path.DirectorySeparatorChar;
    if (!full.StartsWith(runs, StringComparison.OrdinalIgnoreCase) ||
        full.Contains("workshop-snapshot-", StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("output must be a fresh directory under .build/runs");
    if (Directory.Exists(full) || File.Exists(full))
        throw new InvalidDataException("output directory must be fresh");
    Directory.CreateDirectory(full);
}

static void EnsureReferencePaths(KaguyaStage8Request request)
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
            throw new InvalidDataException("runtime evidence cannot reference formal game data or live Workshop");
    }
}

static void WriteOutputs(string output, string requestPath, KaguyaStage8Request request,
    KaguyaStage8Evaluation evaluation, DateTimeOffset started)
{
    var report = evaluation.Report;
    WriteJson(Path.Combine(output, "stage8-report.json"), new
    {
        report,
        determinismDigest = report.DeterminismDigest,
        conversion = evaluation.Conversion?.Report,
        catalog = new { evaluation.Catalog.SnapshotId, evaluation.Catalog.SnapshotContentSha256, evaluation.Catalog.TargetGameVersion },
        resolution = new { evaluation.Resolution.DependencyClosure, evaluation.Resolution.LoadOrder, evaluation.Resolution.SelectedVariants },
        startedUtc = started,
        finishedUtc = DateTimeOffset.UtcNow
    });
    WriteJson(Path.Combine(output, "input-summary.json"), new
    {
        requestPath = Path.GetFullPath(requestPath), request,
        inputDigest = report.InputDigest,
        snapshotWriteBlocked = report.SnapshotWriteBlocked,
        formalDataWriteBlocked = report.FormalDataWriteBlocked
    });
    var manifest = new
    {
        schema = "kaguya-stage8-run-manifest/v1",
        status = report.Status.ToString(),
        exitCode = report.Status == WatcherStage6Status.Pass ? 0 : report.Errors.Any(x => x.Code == AdapterErrorCode.Unsupported) ? 2 : 1,
        targetGameVersion = report.TargetGameVersion,
        snapshotId = report.SnapshotId,
        snapshotContentSha256 = report.SnapshotContentSha256,
        workshopId = report.WorkshopId,
        modId = report.ModId,
        modVersion = report.ModVersion,
        dependencies = evaluation.Resolution.DependencyClosure,
        loadOrder = evaluation.Resolution.LoadOrder,
        selectedVariants = evaluation.Resolution.SelectedVariants,
        inputDigest = report.InputDigest,
        primaryDigest = report.PrimaryDigest,
        replayDigest = report.ReplayDigest,
        deterministic = report.Deterministic,
        identity = report.Capabilities.Where(x => x.Kind is "Enemy" or "Encounter" or "Event" or "Reward")
            .Select(x => new { x.Kind, x.Id, x.Status, x.EvidenceRef }).ToArray(),
        errorDenominator = report.ErrorDenominator,
        errors = report.Errors,
        reportPath = Path.Combine(output, "stage8-report.json"),
        startedUtc = started,
        finishedUtc = DateTimeOffset.UtcNow
    };
    WriteJson(Path.Combine(output, "run-manifest.json"), manifest);
    File.WriteAllText(Path.Combine(output, "worker.log"),
        $"status={report.Status}\nstop={report.StopCode}\nmessage={report.StopMessage}\n",
        new UTF8Encoding(false));
}

static void WriteFailure(string output, string requestPath, DateTimeOffset started,
    AdapterErrorRecord error)
{
    try
    {
        if (!Directory.Exists(output)) Directory.CreateDirectory(output);
        var report = new
        {
            schema = KaguyaStage8Constants.Schema,
            status = "Blocked",
            stopCode = error.StableCode,
            stopMessage = error.Message,
            errors = new[] { error },
            requestPath = Path.GetFullPath(requestPath),
            startedUtc = started,
            finishedUtc = DateTimeOffset.UtcNow
        };
        WriteJson(Path.Combine(output, "stage8-report.json"), report);
        WriteJson(Path.Combine(output, "run-manifest.json"), new
        {
            schema = "kaguya-stage8-run-manifest/v1",
            status = "Blocked", exitCode = 1, modId = KaguyaStage8Constants.ModId,
            errorDenominator = new KaguyaErrorDenominator(0, 0, 0, 1, 0, 1), errors = new[] { error },
            reportPath = Path.Combine(output, "stage8-report.json"), startedUtc = started,
            finishedUtc = DateTimeOffset.UtcNow
        });
        File.WriteAllText(Path.Combine(output, "worker.log"), error.StableSummary,
            new UTF8Encoding(false));
    }
    catch { }
}

static void WriteJson(string path, object value) => File.WriteAllText(path,
    JsonSerializer.Serialize(value, ModWorkerJson.Options), new UTF8Encoding(false));

sealed record RunnerOptions(string Request, string Output, string? Probe, string? ReplayProbe);
