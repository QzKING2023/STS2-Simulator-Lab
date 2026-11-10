using System.Text.Json.Nodes;
namespace SimulatorLab.Core;

// Native execution and optional prediction have separate completion contracts.
public enum NativeRunTerminal { Unknown, Ongoing, RunVictory, Death }
public enum NativeRunStop
{
    SliceBoundary,
    Completed,
    Unsupported,
    BudgetExceeded,
    InvalidAction,
    EngineError,
    RuntimeFailure,
    EnvironmentBlocked,
    StrategyStopped,
    ProcessCrashed,
    Interrupted,
    DataError,
}
public sealed record NativeRunResultV1
{
    public string Schema { get; init; } = "native-run-result/v1";
    public NativeRunTerminal Terminal { get; init; } = NativeRunTerminal.Unknown;
    public NativeRunStop Stop { get; init; }
    public ComparisonOutcome Comparison { get; init; } = ComparisonOutcome.NotCompared;
    public int Dispatched { get; init; }
    public int Confirmed { get; init; }
    public bool DataComplete { get; init; }
    public string LastBoundary { get; init; } = "";
    public string? Error { get; init; }
    public List<string> Validate()
    {
        List<string> errors=[];
        if(Schema!="native-run-result/v1" || !Enum.IsDefined(Terminal) || !Enum.IsDefined(Stop) || !Enum.IsDefined(Comparison)) errors.Add("Unknown schema/value");
        if(Confirmed<0 || Dispatched<Confirmed) errors.Add("Invalid action counters");
        if(Terminal is NativeRunTerminal.RunVictory or NativeRunTerminal.Death && Stop!=NativeRunStop.Completed) errors.Add("Native terminal requires Completed stop");
        if(Stop==NativeRunStop.Completed && Terminal is not (NativeRunTerminal.RunVictory or NativeRunTerminal.Death)) errors.Add("Actual run terminal required");
        if(Stop==NativeRunStop.SliceBoundary && (Terminal!=NativeRunTerminal.Ongoing || Confirmed==0)) errors.Add("Nonempty ongoing slice required");
        if(Stop is NativeRunStop.Completed or NativeRunStop.SliceBoundary && (!DataComplete || string.IsNullOrEmpty(LastBoundary) || Comparison==ComparisonOutcome.Mismatch || Error!=null)) errors.Add("Incomplete or contradictory evidence");
        if(Stop is NativeRunStop.Completed or NativeRunStop.SliceBoundary && (Confirmed==0 || Confirmed!=Dispatched)) errors.Add("Unconfirmed action at successful boundary");
        return errors;
    }
    public int ExitCode => Validate().Count>0 ? 1 : Stop switch
    {
        NativeRunStop.Completed or NativeRunStop.SliceBoundary => 0,
        NativeRunStop.Unsupported => 2,
        NativeRunStop.BudgetExceeded => 3,
        NativeRunStop.StrategyStopped => 5,
        NativeRunStop.ProcessCrashed => 6,
        _ => 1,
    };
}

/// <summary>One-use authority for a native action; no simulator capture dependency.</summary>
public sealed class NativeDecisionTicket(object source, long sequence, string boundary, string action)
{
    public static bool SameActionJson(string left,string right)=>JsonNode.DeepEquals(JsonNode.Parse(left),JsonNode.Parse(right));
    private bool consumed;
    public void Consume(object current, long currentSequence, string currentBoundary, string requestedAction)
    {
        if(consumed || !ReferenceEquals(source,current) || sequence!=currentSequence || boundary!=currentBoundary || action!=requestedAction)
            throw new NativeActionRejectedException("Invalid/stale/consumed native decision ticket");
        consumed=true;
    }
}
public sealed class NativeActionRejectedException(string message) : InvalidOperationException(message);
public sealed class NativeRuntimeFailureException(string message) : InvalidOperationException(message);
public sealed class NativeEnvironmentBlockedException(string message) : InvalidOperationException(message);
public sealed class NativeStrategyStoppedException(string message) : InvalidOperationException(message);
