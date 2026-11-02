using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimulatorLab.Core;

public sealed record NativeEngineStateStampV1(string Sha256, string StateText);

/// <summary>
/// A narrow per-action differential produced by the Lab-owned probe. The native
/// fields come from v0.111.0 game objects; the engine stamp is the reviewed
/// CombatSolver continuation serialization used for a full state comparison.
/// This is not a complete CombatRootV1 capture or a retail UI trace.
/// </summary>
public sealed record NativeGameActionDifferentialFileV1
{
    public string Schema { get; init; } = "native-game-action-differential/v1";
    public string CaptureMethod { get; init; } = "";
    public string GameVersion { get; init; } = GameBuildPin.Version;
    public string GameCommit { get; init; } = GameBuildPin.Commit;
    public long MainAssemblyHash { get; init; } = GameBuildPin.MainAssemblyHash;
    public string ScenarioId { get; init; } = "";
    public string CharacterModelId { get; init; } = "";
    public string EncounterModelId { get; init; } = "";
    public string TargetCombatantId { get; init; } = "";
    public bool TestMode { get; init; }
    public bool SteamInitialized { get; init; }
    public string SaveStore { get; init; } = "";
    public bool MyModLoaded { get; init; }
    public bool UsesPresentationClockShim { get; init; }
    public CombatActionV1 Action { get; init; } = new();
    public NativeEngineStateStampV1 NativeRoot { get; init; } = new("", "");
    public NativeEngineStateStampV1 SimulatorRoot { get; init; } = new("", "");
    public NativeEngineStateStampV1 SimulatorAfter { get; init; } = new("", "");
    public NativeEngineStateStampV1 NativeAfter { get; init; } = new("", "");
    public List<string> Differences { get; init; } = [];
}

public sealed record NativeGameDifferentialImportResultV1(
    bool IsValid,
    NativeGameActionDifferentialFileV1? Evidence,
    List<string> Errors);

public static class NativeGameDifferentialImporter
{
    private static readonly JsonSerializerOptions StrictOptions = CreateStrictOptions();

    public static NativeGameDifferentialImportResultV1 Read(string json)
    {
        NativeGameActionDifferentialFileV1? evidence;
        try
        {
            evidence = JsonSerializer.Deserialize<NativeGameActionDifferentialFileV1>(json, StrictOptions);
        }
        catch (JsonException error)
        {
            return new(false, null, [$"Invalid native differential JSON: {error.Message}"]);
        }

        if (evidence is null)
            return new(false, null, ["Native differential document is null."]);

        List<string> errors = [];
        if (evidence.Schema != "native-game-action-differential/v1")
            errors.Add($"Unsupported native differential schema {evidence.Schema}.");
        if (evidence.GameVersion != GameBuildPin.Version || evidence.GameCommit != GameBuildPin.Commit
            || evidence.MainAssemblyHash != GameBuildPin.MainAssemblyHash)
            errors.Add("Native differential game build does not match the v0.111.0 pin.");
        if (string.IsNullOrWhiteSpace(evidence.CaptureMethod) || string.IsNullOrWhiteSpace(evidence.ScenarioId)
            || string.IsNullOrWhiteSpace(evidence.CharacterModelId) || string.IsNullOrWhiteSpace(evidence.EncounterModelId))
            errors.Add("Native differential capture or scenario metadata is incomplete.");
        if (!evidence.TestMode || evidence.SteamInitialized || evidence.MyModLoaded
            || !evidence.SaveStore.EndsWith("MemorySaveStore", StringComparison.Ordinal))
            errors.Add("Native differential isolation claims are missing or unsafe.");
        if (evidence.Action.Kind != CombatActionKind.PlayCard || string.IsNullOrWhiteSpace(evidence.Action.CardInstanceId)
            || evidence.Action.CardHandIndex is null or < 0 || string.IsNullOrWhiteSpace(evidence.TargetCombatantId)
            || evidence.Action.TargetCombatantId != evidence.TargetCombatantId)
            errors.Add("Native differential action is incomplete or unsupported.");

        ValidateStamp("nativeRoot", evidence.NativeRoot, errors);
        ValidateStamp("simulatorRoot", evidence.SimulatorRoot, errors);
        ValidateStamp("simulatorAfter", evidence.SimulatorAfter, errors);
        ValidateStamp("nativeAfter", evidence.NativeAfter, errors);
        if (!StringComparer.Ordinal.Equals(evidence.NativeRoot.Sha256, evidence.SimulatorRoot.Sha256))
            errors.Add("Simulator root does not match the native root.");
        if (!StringComparer.Ordinal.Equals(evidence.SimulatorAfter.Sha256, evidence.NativeAfter.Sha256)
            || !StringComparer.Ordinal.Equals(evidence.SimulatorAfter.StateText, evidence.NativeAfter.StateText)
            || evidence.Differences.Count != 0)
            errors.Add("Simulator and native game states differ after the action.");

        return new(errors.Count == 0, evidence, errors);
    }

    public static string Write(NativeGameActionDifferentialFileV1 evidence)
        => JsonSerializer.Serialize(evidence, new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true });

    public static NativeEngineStateStampV1 Stamp(string stateText)
        => new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stateText))).ToLowerInvariant(), stateText);

    private static void ValidateStamp(string name, NativeEngineStateStampV1 stamp, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(stamp.StateText) || string.IsNullOrWhiteSpace(stamp.Sha256))
        {
            errors.Add($"{name} state stamp is incomplete.");
            return;
        }
        string actual = Stamp(stamp.StateText).Sha256;
        if (!StringComparer.Ordinal.Equals(actual, stamp.Sha256))
            errors.Add($"{name} state text does not match its SHA-256.");
    }

    private static JsonSerializerOptions CreateStrictOptions()
        => new(ProtocolJson.Options) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
}
