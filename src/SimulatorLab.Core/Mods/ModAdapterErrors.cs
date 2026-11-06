namespace SimulatorLab.Core.Mods;

/// <summary>
/// Error codes are part of the adapter contract. Keep names stable: reports and
/// fixture assertions use the names as machine-readable codes.
/// </summary>
public enum AdapterErrorCode
{
    MissingFile,
    InvalidFormat,
    UnsupportedVersion,
    MissingField,
    InvalidValue,
    DuplicateId,
    DependencyMissing,
    ConversionError,
    DataError,
    Unsupported,
    DeterminismMismatch,
    BudgetExceeded,
    RuntimeFailure
}

public sealed record AdapterErrorRecord(
    AdapterErrorCode Code,
    string ObjectId,
    string Path,
    bool Retryable,
    bool BlocksContinuation,
    string FixDirection,
    string Message,
    string? EvidenceRef = null)
{
    public string StableCode => AdapterErrorCatalog.StableCode(Code);
    public bool Blocking => BlocksContinuation;

    public string StableSummary => string.Join("|", StableCode, ObjectId, Path,
        Retryable ? "retryable" : "non-retryable",
        BlocksContinuation ? "blocking" : "non-blocking", FixDirection, Message,
        EvidenceRef ?? "");

    public static AdapterErrorRecord Create(
        AdapterErrorCode code,
        string? objectId,
        string? path,
        string message,
        string? evidenceRef = null,
        bool? retryable = null,
        bool? blocksContinuation = null,
        string? fixDirection = null) =>
        new(code, objectId ?? "<module>", path ?? "<input>",
            retryable ?? AdapterErrorCatalog.IsRetryable(code),
            blocksContinuation ?? AdapterErrorCatalog.BlocksContinuation(code),
            fixDirection ?? AdapterErrorCatalog.FixDirection(code),
            message, evidenceRef);
}

public static class AdapterErrorCatalog
{
    private sealed record Policy(string StableCode, bool Retryable, bool BlocksContinuation, string FixDirection);

    private static readonly IReadOnlyDictionary<AdapterErrorCode, Policy> Policies =
        new Dictionary<AdapterErrorCode, Policy>
        {
            [AdapterErrorCode.MissingFile] = new("MOD-001", true, true, "Restore the referenced file in the fixed snapshot and rerun conversion."),
            [AdapterErrorCode.InvalidFormat] = new("MOD-002", false, true, "Repair the adapter document or manifest format before conversion."),
            [AdapterErrorCode.UnsupportedVersion] = new("MOD-003", false, true, "Select an exact 0.111.0-compatible variant; never fall back silently."),
            [AdapterErrorCode.MissingField] = new("MOD-004", false, true, "Populate the required field at the reported path."),
            [AdapterErrorCode.InvalidValue] = new("MOD-005", false, true, "Replace the value with one satisfying the standard model contract."),
            [AdapterErrorCode.DuplicateId] = new("MOD-006", false, true, "Give each definition a unique stable ID."),
            [AdapterErrorCode.DependencyMissing] = new("MOD-007", true, true, "Add or resolve the declared dependency before conversion."),
            [AdapterErrorCode.ConversionError] = new("MOD-008", false, true, "Fix the source-to-model mapping or provide an explicit adapter."),
            [AdapterErrorCode.DataError] = new("MOD-009", false, true, "Correct the referenced data and rerun validation."),
            [AdapterErrorCode.Unsupported] = new("MOD-010", false, true, "Implement the required capability or keep it explicitly unsupported."),
            [AdapterErrorCode.DeterminismMismatch] = new("MOD-011", true, true, "Investigate unstable ordering or hidden state before accepting the result."),
            [AdapterErrorCode.BudgetExceeded] = new("MOD-012", true, true, "Increase the bounded fixture budget deliberately or reduce the input."),
            [AdapterErrorCode.RuntimeFailure] = new("MOD-013", true, true, "Capture the isolated runtime evidence and retry in a fresh worker."),
        };

    public static string StableCode(AdapterErrorCode code) => Policies[code].StableCode;
    public static bool IsRetryable(AdapterErrorCode code) => Policies[code].Retryable;
    public static bool BlocksContinuation(AdapterErrorCode code) => Policies[code].BlocksContinuation;
    public static string FixDirection(AdapterErrorCode code) => Policies[code].FixDirection;

    public static IReadOnlyList<AdapterErrorCode> AllCodes =>
        Enum.GetValues<AdapterErrorCode>().OrderBy(x => (int)x).ToArray();
}
