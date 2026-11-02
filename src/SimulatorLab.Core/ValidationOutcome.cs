namespace SimulatorLab.Core;

public enum NativeOutcome { Unknown, Ongoing, Victory, Death }
public enum ComparisonOutcome { NotCompared, Exact, Mismatch }
public enum CoverageOutcome { NotRun, Complete, Unsupported }
public enum ValidationStop { Completed, BudgetExceeded, Unsupported, InvalidAction, EngineError, DifferentialMismatch, EnvironmentBlocked, Cancelled, Interrupted, NoCandidate }

/// <summary>Verification and policy outcome are deliberately independent.</summary>
public sealed record ValidationOutcomeV1
{
    public string Schema { get; init; } = "validation-outcome/v1";
    public string CaseId { get; init; } = "";
    public string ScenarioKind { get; init; } = "native-flow";
    public NativeOutcome NativeOutcome { get; init; }
    public NativeOutcome SimulatorOutcome { get; init; }
    public ComparisonOutcome Comparison { get; init; }
    public CoverageOutcome Coverage { get; init; }
    public ValidationStop StopReason { get; init; }
    public string ReasonCode { get; init; } = "";
    public string? Detail { get; init; }
    public bool VerificationPassed { get; init; }
    public int ConfirmedActions { get; init; }
    public string ExecutionState { get; init; } = "NotStarted";
    public List<string> Validate()
    {
        List<string> errors = [];
        if(Schema!="validation-outcome/v1")errors.Add("Unknown validation outcome schema.");
        if (string.IsNullOrWhiteSpace(CaseId) || string.IsNullOrWhiteSpace(ReasonCode)) errors.Add("Missing case/reason identity.");
        if (Comparison == ComparisonOutcome.Mismatch && VerificationPassed) errors.Add("A differential mismatch cannot verify successfully.");
        if (StopReason == ValidationStop.DifferentialMismatch && Comparison != ComparisonOutcome.Mismatch) errors.Add("Mismatch stop requires mismatch evidence.");
        if (StopReason == ValidationStop.Completed && (NativeOutcome is not (NativeOutcome.Victory or NativeOutcome.Death)
            || NativeOutcome != SimulatorOutcome || Comparison != ComparisonOutcome.Exact || Coverage != CoverageOutcome.Complete))
            errors.Add("Completed combat requires actual matching terminal evidence.");
        if (StopReason == ValidationStop.Unsupported && Coverage != CoverageOutcome.Unsupported) errors.Add("Unsupported stop requires a coverage gap.");
        if (ConfirmedActions < 0) errors.Add("Negative confirmed action count.");
        if (!Enum.IsDefined(NativeOutcome) || !Enum.IsDefined(SimulatorOutcome) || !Enum.IsDefined(Comparison)
            || !Enum.IsDefined(Coverage) || !Enum.IsDefined(StopReason)) errors.Add("Unknown result enum value.");
        return errors;
    }

    public static ValidationOutcomeV1 FromStatus(string caseId,string status,string? detail=null,int confirmed=0)
    {
        bool terminal=status is "passed" or "death";
        var native=status=="passed"?NativeOutcome.Victory:status=="death"?NativeOutcome.Death:NativeOutcome.Unknown;
        return new() {CaseId=caseId,ReasonCode=status,Detail=detail,ConfirmedActions=confirmed,
            NativeOutcome=native,SimulatorOutcome=native,
            Comparison=terminal||status=="boundary"?ComparisonOutcome.Exact:status=="differential-mismatch"?ComparisonOutcome.Mismatch:ComparisonOutcome.NotCompared,
            Coverage=status=="unsupported"?CoverageOutcome.Unsupported:terminal||status=="boundary"?CoverageOutcome.Complete:CoverageOutcome.NotRun,
            StopReason=status switch {"passed" or "death"=>ValidationStop.Completed,"unsupported"=>ValidationStop.Unsupported,
                "failed"=>ValidationStop.EngineError,"differential-mismatch"=>ValidationStop.DifferentialMismatch,"invalid-action"=>ValidationStop.InvalidAction,
                "environment-blocked"=>ValidationStop.EnvironmentBlocked,"cancelled"=>ValidationStop.Cancelled,"interrupted"=>ValidationStop.Interrupted,
                "budget-exceeded"=>ValidationStop.BudgetExceeded,"no-candidate" or "boundary"=>ValidationStop.NoCandidate,_=>ValidationStop.EngineError},
            VerificationPassed=terminal||status=="boundary",ExecutionState=terminal?"TerminalCaptured":"AtBoundary"};
    }

    public static string StatusForStop(ValidationStop reason) => reason switch {
        ValidationStop.BudgetExceeded=>"budget-exceeded",ValidationStop.Unsupported=>"unsupported",ValidationStop.InvalidAction=>"invalid-action",
        ValidationStop.DifferentialMismatch=>"differential-mismatch",ValidationStop.EnvironmentBlocked=>"environment-blocked",
        ValidationStop.Cancelled=>"cancelled",ValidationStop.Interrupted=>"interrupted",ValidationStop.NoCandidate=>"no-candidate",_=>"engine-error"};
}

public sealed class ValidationStopException(ValidationStop reason, string code, string message) : InvalidOperationException(message)
{
    public ValidationStop Reason { get; } = reason;
    public string Code { get; } = code;
}

public sealed record DecisionBounds(int MaxDecisions = 48, int MaxTurns = 12, double ElapsedSeconds = 90)
{
    public string? Reached(int decisions, int turn, double elapsed) => decisions >= MaxDecisions ? "decision-limit"
        : turn > MaxTurns ? "turn-limit" : elapsed >= ElapsedSeconds ? "elapsed-limit" : null;
}

public static class QueryDisposition
{
    public static ValidationStop? Stop(bool unsupported, bool budgetHit, int candidates) => unsupported ? ValidationStop.Unsupported
        : budgetHit ? ValidationStop.BudgetExceeded : candidates == 0 ? ValidationStop.NoCandidate : null;
}

/// <summary>Independently reviewed contract for the pinned v0.4.1 DLL, not derived from live DynamicVars.</summary>
public static class EleanorV041Contracts
{
    public const string AssemblySha256 = "949320F651298D34399335EB28176578D270139DFF2CCE60FB7FDF35E7DBAD42";
    public static (int EnergyPaid, int ManaPaid, int Block) ManaShield(bool upgraded, int mana) =>
        (1, mana >= 1 ? 1 : 0, 5 + (mana >= 1 ? upgraded ? 8 : 5 : 0));
}
