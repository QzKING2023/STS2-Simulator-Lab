using System.Text;
using System.Text.Json;
using SimulatorLab.Core.Mods;

if (args.Any(x => x is "--help" or "-h"))
{
    Console.WriteLine("MinionStage9 --request <json> --output <fresh .build/runs directory> --probe <json> --replay-probe <json>");
    return 0;
}

var options = Parse(args);
var started = DateTimeOffset.UtcNow;
try
{
    EnsureFreshOutput(options.Output);
    var request = JsonSerializer.Deserialize<MinionStage9Request>(
        File.ReadAllText(options.Request), ModWorkerJson.Options)
        ?? throw new InvalidDataException("stage 9 request is null");
    request = request with
    {
        ProbeDocumentPath = options.Probe ?? request.ProbeDocumentPath,
        ReplayProbeDocumentPath = options.ReplayProbe ?? request.ReplayProbeDocumentPath
    };
    EnsureReferencePaths(request);
    var probe = MinionStage9Adapter.ReadProbe(request.ProbeDocumentPath ?? "");
    var replay = MinionStage9Adapter.ReadProbe(request.ReplayProbeDocumentPath ?? "");
    var fixture = MinionStage9Fixture.Run(request.Seed);
    var fixtureReplay = MinionStage9Fixture.Run(request.Seed);
    var evaluation = MinionStage9Adapter.Evaluate(request, probe, replay, fixture, fixtureReplay);
    WriteOutputs(options.Output, options.Request, request, evaluation, started);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        status = evaluation.Report.Status.ToString(),
        stopCode = evaluation.Report.StopCode,
        determinismDigest = evaluation.Report.DeterminismDigest,
        inputDigest = evaluation.Report.InputDigest,
        fixtureDigest = evaluation.Report.FixtureDigest,
        output = options.Output
    }, ModWorkerJson.Options));
    return evaluation.Report.Status == WatcherStage6Status.Pass ? 0 :
        evaluation.Report.Errors.Any(x => x.Code == AdapterErrorCode.Unsupported) ? 2 : 1;
}
catch (Exception ex)
{
    var error = AdapterErrorRecord.Create(AdapterErrorCode.RuntimeFailure,
        MinionStage9Constants.ShadowverseModId, "<minion-stage9>",
        ex.GetType().Name + ": " + ex.Message, "stage9/runner");
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
        var value = i + 1 < args.Length ? args[++i] : throw new InvalidDataException($"missing value for '{option}'");
        if (option == "--request") request = value;
        else if (option == "--output") output = value;
        else if (option == "--probe") probe = value;
        else replay = value;
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

static void EnsureReferencePaths(MinionStage9Request request)
{
    var lab = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    var buildRoot = Path.Combine(lab, ".build") + Path.DirectorySeparatorChar;
    foreach (var path in new[] { request.WorkerRunManifestPath, request.NativeBuildManifestPath,
        request.ProbeDocumentPath, request.ReplayProbeDocumentPath }.Where(x => !string.IsNullOrWhiteSpace(x)))
    {
        var full = Path.GetFullPath(path!);
        if (!full.StartsWith(buildRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("runtime evidence references must be inside .build");
        if (full.Contains("\\steamapps\\workshop\\", StringComparison.OrdinalIgnoreCase) ||
            full.Contains("\\userdata\\", StringComparison.OrdinalIgnoreCase) ||
            full.Contains("\\saves\\", StringComparison.OrdinalIgnoreCase) ||
            full.Contains("\\settings\\", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("runtime evidence cannot refer to formal game data");
    }
}

static void WriteOutputs(string output, string requestPath, MinionStage9Request request,
    MinionStage9Evaluation evaluation, DateTimeOffset started)
{
    WriteJson(Path.Combine(output, "stage9-report.json"), new
    {
        evaluation.Report,
        determinismDigest = evaluation.Report.DeterminismDigest,
        conversion = evaluation.Conversion?.Report,
        minionModel = evaluation.MinionModel,
        fixture = evaluation.Fixture,
        catalog = new { evaluation.Catalog.SnapshotId, evaluation.Catalog.SnapshotContentSha256, evaluation.Catalog.TargetGameVersion },
        resolution = new { evaluation.Resolution.DependencyClosure, evaluation.Resolution.LoadOrder, evaluation.Resolution.SelectedVariants },
        startedUtc = started,
        finishedUtc = DateTimeOffset.UtcNow
    });
    WriteJson(Path.Combine(output, "minion-fixture.json"), evaluation.Fixture);
    WriteJson(Path.Combine(output, "input-summary.json"), new
    {
        requestPath = Path.GetFullPath(requestPath),
        request,
        inputDigest = evaluation.Report.InputDigest,
        snapshotWriteBlocked = evaluation.Report.SnapshotWriteBlocked,
        formalDataWriteBlocked = evaluation.Report.FormalDataWriteBlocked
    });
    var manifest = new
    {
        schema = "mod-stage9-run/v1",
        stage = 9,
        modId = MinionStage9Constants.ShadowverseModId,
        workshopId = MinionStage9Constants.ShadowverseWorkshopId,
        modVersion = MinionStage9Constants.ShadowverseVersion,
        targetGameVersion = MinionStage9Constants.TargetGameVersion,
        snapshotId = MinionStage9Constants.SnapshotId,
        dependencies = evaluation.Resolution.LoadOrder,
        selectedVariants = evaluation.Resolution.SelectedVariants,
        inputDigest = evaluation.Report.InputDigest,
        fixtureDigest = evaluation.Report.FixtureDigest,
        primaryDigest = evaluation.Report.PrimaryDigest,
        replayDigest = evaluation.Report.ReplayDigest,
        deterministic = evaluation.Report.Deterministic,
        errorDenominator = evaluation.Report.ErrorDenominator,
        identity = evaluation.Report.Capabilities,
        nativeBuildManifestPath = request.NativeBuildManifestPath,
        probeDocumentPath = request.ProbeDocumentPath,
        replayProbeDocumentPath = request.ReplayProbeDocumentPath,
        snapshotWriteBlocked = evaluation.Report.SnapshotWriteBlocked,
        formalDataWriteBlocked = evaluation.Report.FormalDataWriteBlocked
    };
    WriteJson(Path.Combine(output, "run-manifest.json"), manifest);
    File.WriteAllText(Path.Combine(output, "worker.log"),
        $"status={evaluation.Report.Status}\nstop={evaluation.Report.StopCode}\nmessage={evaluation.Report.StopMessage}\n",
        new UTF8Encoding(false));
}

static void WriteFailure(string output, string requestPath, DateTimeOffset started, AdapterErrorRecord error)
{
    try
    {
        if (!Directory.Exists(output)) Directory.CreateDirectory(output);
        WriteJson(Path.Combine(output, "stage9-report.json"), new
        {
            schema = MinionStage9Constants.Schema,
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
    catch { }
}

static void WriteJson(string path, object value) => File.WriteAllText(path,
    JsonSerializer.Serialize(value, ModWorkerJson.Options), new UTF8Encoding(false));

sealed record RunnerOptions(string Request, string Output, string? Probe, string? ReplayProbe);
