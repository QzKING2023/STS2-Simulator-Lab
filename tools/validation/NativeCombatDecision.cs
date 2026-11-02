using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CombatSolver;
using CombatSolver.Engine.Common;
using MegaCrit.Sts2.Core.Combat;
using SimulatorLab.Core;

namespace OfflineSearchHarness;

/// <summary>A bounded, single-use query against an actual playable native boundary.</summary>
internal static class NativeCombatDecision
{
    public const int MaxDecisions = 48;
    public const int MaxTurns = 12;
    public const int ElapsedStopSeconds = 90;
    public static long BoundarySequence { get; private set; }
    public static void ObserveExecutedAction() => BoundarySequence++;

    internal sealed class Ticket(CombatState sourceCombat, string live, string continuation, JsonElement[] actions, object evidence)
    {
        private readonly long _sourceSequence = BoundarySequence;
        private bool _consumed;
        public object Evidence { get; } = evidence;
        public int CandidateCount => actions.Length;
        public JsonElement[] Consume(CombatState combat)
        {
            if (_consumed) throw new ValidationStopException(ValidationStop.InvalidAction,"ticket-consumed","Decision ticket has already been consumed.");
            if (!ReferenceEquals(sourceCombat, combat) || !CombatManager.Instance.IsInProgress)
                throw new ValidationStopException(ValidationStop.InvalidAction,"ticket-combat","Decision ticket belongs to another or terminated combat.");
            var current = CombatRootSnapshot.Capture(combat);
            if (_sourceSequence != BoundarySequence || current.LiveStamp.StateText != live || current.ContinuationStamp.StateText != continuation)
                throw new ValidationStopException(ValidationStop.InvalidAction,"ticket-stale","Decision ticket is stale; query the current boundary again.");
            _consumed = true;
            return [actions[0].Clone()];
        }
    }

    public static Ticket Query(CombatState combat, MainLoopContext loop, HarnessOptions options, string directory)
    {
        // These are the actual settings installed at bootstrap, not merely reporting labels.
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIMULATORLAB_STAGE_ONE_CASE")) && (options.SearchMode != "Evaluate" || options.Nodes != 1000 || options.Beam != 8
            || options.BudgetMilliseconds != 5000 || options.MaxDegreeOfParallelism != 1)
            )
            throw new PredictionUnsupportedException("Online fixture requires Evaluate / beam8 / nodes1000 / 5000ms / DoP1.");
        NativeModRunLoop.AssertDiscoveryStable();
        Directory.CreateDirectory(directory);
        var before = CombatRootSnapshot.Capture(combat);
        var outcome = ModRuntime.RunSearchDetailed(combat, options with { OutputDirectory = directory }, loop, null);
        var after = CombatRootSnapshot.Capture(combat);
        bool sourceMatches = before.LiveStamp.StateText == outcome.RootLiveStamp
            && before.ContinuationStamp.StateText == outcome.RootContinuationStamp;
        bool unchanged = before.LiveStamp.StateText == after.LiveStamp.StateText
            && before.ContinuationStamp.StateText == after.ContinuationStamp.StateText;
        NativeModRunLoop.AssertDiscoveryStable();
        if (!sourceMatches || !unchanged)
            throw new InvalidOperationException("Search query mutated the native source or returned a different source root.");
        var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        json.Converters.Add(new JsonStringEnumConverter());
        var actions = JsonSerializer.SerializeToElement(outcome.RouteActions, json).EnumerateArray().Select(e => e.Clone()).ToArray();
        var policy = JsonSerializer.SerializeToElement(outcome.Policy,json);
        int actualNodes = policy.GetProperty("profile").GetProperty("maxExpandedNodes").GetInt32();
        bool budgetHit = outcome.TimeBoundaryObserved || outcome.Result.BoundaryReason.ToString() is "NodeLimit" or "TimeLimit";
        var evidence = new {
            mode = "online-one-action", sourceSeed = NativeModRunLoop.SourceSeed,
            requested = new { beam = options.Beam, nodes = options.Nodes, budgetMilliseconds = options.BudgetMilliseconds, dop = options.MaxDegreeOfParallelism },
            actualPolicy = outcome.Policy, expandedNodes = outcome.Result.TotalExpandedNodes,
            outcome.WallSeconds, outcome.TimeBoundaryObserved, predictedBoundary = outcome.Result.BoundaryReason.ToString(),
            candidateActionCount = actions.Length, selectedAction = actions.Length>0?(JsonElement?)actions[0]:null, discardedTailActions = Math.Max(0,actions.Length - 1),
            budgetHit, budgetEvidence = new {actualNodes,requestNodes=options.Nodes, outcome.TimeBoundaryObserved, expanded=outcome.Result.TotalExpandedNodes},
            sourceLiveStamp = before.LiveStamp.StateText, sourceContinuation = before.ContinuationStamp.StateText,
            sourceLiveSha256 = Sha(before.LiveStamp.StateText), sourceContinuationSha256 = Sha(before.ContinuationStamp.StateText),
            returnedSourceMatches = sourceMatches, nativeSourceUnchangedByQuery = unchanged,
            semantics = "One native action only; the candidate tail is not executed. Query again after exact action verification. Finite-budget result, no global optimality claim."
        };
        File.WriteAllText(Path.Combine(directory, "decision-query.json"), JsonSerializer.Serialize(evidence, json));
        var stop = QueryDisposition.Stop(false,budgetHit,actions.Length);
        if (stop is not null)
            throw new ValidationStopException(stop.Value,
                budgetHit?"solver-budget":"solver-no-candidate",$"Bounded query stopped; candidates={actions.Length}.");
        return new Ticket(combat, before.LiveStamp.StateText, before.ContinuationStamp.StateText, actions, evidence);
    }

    private static string Sha(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
