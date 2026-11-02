using System.Text.Json;

namespace SimulatorLab.Core;

/// <summary>ConfirmedActions counts the contiguous, accepted and exactly compared prefix in this report's scope.</summary>
public static class ReplayVerification
{
    public static ValidationOutcomeV1 Outcome(JsonElement replay) =>
        JsonSerializer.Deserialize<ValidationOutcomeV1>(replay.GetProperty("outcome").GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    public static bool Confirmed(JsonElement step) =>
        step.GetProperty("nativeAccepted").GetBoolean() && step.GetProperty("simulatedAccepted").GetBoolean()
        && step.GetProperty("exactStateMatch").GetBoolean() && step.GetProperty("parentBranchUnchanged").GetBoolean()
        && step.GetProperty("nativeContinuation").GetString() == step.GetProperty("simulatorContinuation").GetString();

    public static int Count(IEnumerable<JsonElement> reports)
    {
        int count = 0;
        foreach (var replay in reports)
            foreach (var step in replay.GetProperty("steps").EnumerateArray())
            {
                if (!Confirmed(step)) return count;
                count++;
            }
        return count;
    }

    public static void RequireBoundary(JsonElement replay, int expectedActions)
    {
        string? status = replay.GetProperty("status").GetString();
        var outcome = Outcome(replay);
        if (status != "boundary")
            throw new ValidationStopException(outcome.StopReason == ValidationStop.Completed ? ValidationStop.InvalidAction : outcome.StopReason,
                "inner-replay-" + status, replay.GetProperty("stopReason").GetString() ?? "Expected an ongoing exact replay boundary.");
        if (expectedActions <= 0 || replay.GetProperty("steps").GetArrayLength() != expectedActions
            || Count([replay]) != expectedActions || outcome.ConfirmedActions != expectedActions
            || outcome.NativeOutcome != NativeOutcome.Ongoing || outcome.SimulatorOutcome != NativeOutcome.Ongoing
            || outcome.Comparison != ComparisonOutcome.Exact || !outcome.VerificationPassed || outcome.Validate().Count != 0)
            throw new ValidationStopException(ValidationStop.DifferentialMismatch, "invalid-boundary-evidence",
                "Expected nonempty, accepted, exact, living native/simulator action evidence.");
    }

    public static ValidationOutcomeV1 Aggregate(string caseId, IReadOnlyList<JsonElement> reports, string status, string? reason)
    {
        var result = ValidationOutcomeV1.FromStatus(caseId, status, reason, Count(reports));
        if (reports.Count == 0) return result;
        var leaf = reports[^1];
        var observed = Outcome(leaf);
        if (leaf.GetProperty("status").GetString() == status)
            return observed with { CaseId = caseId, ConfirmedActions = Count(reports) };
        // Stops between actions retain separately measured outcomes of the last confirmed boundary.
        // Execution errors have an uncertain current state; their prefix remains in lastConfirmedBoundary.
        if (status is "budget-exceeded" or "no-candidate" or "unsupported" or "invalid-action")
            result = result with { NativeOutcome = observed.NativeOutcome, SimulatorOutcome = observed.SimulatorOutcome,
                Comparison = observed.Comparison, ExecutionState = "AtBoundary" };
        else if (status is "engine-error" or "failed")
            result = result with { ExecutionState = "OutcomeUncertain" };
        return result;
    }
}
