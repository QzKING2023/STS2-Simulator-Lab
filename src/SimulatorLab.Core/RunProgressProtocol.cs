namespace SimulatorLab.Core;

public enum RunProgressPhaseV1 { CombatEnded, Rewards, Route, NodeEntered }
public enum RunCombatOutcomeV1 { Victory, Defeat }
public enum RunRewardDispositionV1 { Pending, Selected, Skipped }
public enum RunProgressActionKindV1 { BeginRewards, SelectReward, SkipPendingRewards, ProceedToRoute, EnterNode }

public sealed record RunRewardOfferV1
{
    public string RewardId { get; init; } = "";
    public string Kind { get; init; } = "";
    public List<string> ChoiceIds { get; init; } = [];
    public RunRewardDispositionV1 Disposition { get; init; }
    public string? SelectedChoiceId { get; init; }
}

/// <summary>
/// A map node separates generated MapPointType from its resolved RoomType. In
/// particular, Unknown is not a room type and must carry a host-resolved result
/// before the node is entered.
/// </summary>
public sealed record RunRouteNodeV1
{
    public string NodeId { get; init; } = "";
    public int Column { get; init; }
    public int Row { get; init; }
    public string MapPointType { get; init; } = "";
    public string? ResolvedRoomType { get; init; }
    public List<string> ChildNodeIds { get; init; } = [];
}

public sealed record RunProgressActionV1
{
    public RunProgressActionKindV1 Kind { get; init; }
    public string? RewardId { get; init; }
    public string? ChoiceId { get; init; }
    public string? NodeId { get; init; }
}

/// <summary>
/// Minimal, versioned run-loop handoff after combat. This contract records
/// available rewards and legal route edges; it does not invent reward odds,
/// effects, map generation, or Unknown-room probabilities.
/// </summary>
public sealed record RunProgressStateV1
{
    public string Schema { get; init; } = "run-progress/v1";
    public string GameVersion { get; init; } = GameBuildPin.Version;
    public string GameCommit { get; init; } = GameBuildPin.Commit;
    public long MainAssemblyHash { get; init; } = GameBuildPin.MainAssemblyHash;
    public RunProgressPhaseV1 Phase { get; init; }
    public RunCombatOutcomeV1 Outcome { get; init; }
    public string CurrentNodeId { get; init; } = "";
    public List<RunRewardOfferV1> Rewards { get; init; } = [];
    public bool CanSkipPendingRewards { get; init; }
    public List<RunRouteNodeV1> RouteNodes { get; init; } = [];
    public List<string> LegalNextNodeIds { get; init; } = [];
    public string? EnteredNodeId { get; init; }

    public static RunProgressStateV1 AfterCombat(
        RunCombatOutcomeV1 outcome,
        string currentNodeId,
        IEnumerable<RunRewardOfferV1> rewards,
        bool canSkipPendingRewards,
        IEnumerable<RunRouteNodeV1> routeNodes)
        => new()
        {
            Phase = RunProgressPhaseV1.CombatEnded,
            Outcome = outcome,
            CurrentNodeId = currentNodeId,
            Rewards = rewards.Select(CopyReward).ToList(),
            CanSkipPendingRewards = canSkipPendingRewards,
            RouteNodes = routeNodes.Select(CopyNode).ToList(),
        };

    internal static RunRewardOfferV1 CopyReward(RunRewardOfferV1 reward)
        => reward with { ChoiceIds = reward.ChoiceIds.ToList() };

    internal static RunRouteNodeV1 CopyNode(RunRouteNodeV1 node)
        => node with { ChildNodeIds = node.ChildNodeIds.ToList() };
}

public static class RunProgressRulesV1
{
    public static RunProgressStateV1 Apply(RunProgressStateV1 state, RunProgressActionV1 action)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(action);
        ValidateIdentity(state);

        return action.Kind switch
        {
            RunProgressActionKindV1.BeginRewards => BeginRewards(state),
            RunProgressActionKindV1.SelectReward => SelectReward(state, action),
            RunProgressActionKindV1.SkipPendingRewards => SkipPendingRewards(state),
            RunProgressActionKindV1.ProceedToRoute => ProceedToRoute(state),
            RunProgressActionKindV1.EnterNode => EnterNode(state, action),
            _ => throw new InvalidOperationException($"Unsupported run action {action.Kind}."),
        };
    }

    private static RunProgressStateV1 BeginRewards(RunProgressStateV1 state)
    {
        RequirePhase(state, RunProgressPhaseV1.CombatEnded, "Rewards can only follow a completed combat.");
        if (state.Outcome != RunCombatOutcomeV1.Victory)
            throw new InvalidOperationException("A defeated run cannot open a victory reward set.");
        return state with { Phase = RunProgressPhaseV1.Rewards };
    }

    private static RunProgressStateV1 SelectReward(RunProgressStateV1 state, RunProgressActionV1 action)
    {
        RequirePhase(state, RunProgressPhaseV1.Rewards, "Rewards can only be selected while the reward set is open.");
        if (string.IsNullOrWhiteSpace(action.RewardId))
            throw new InvalidOperationException("Selecting a reward requires a reward ID.");
        int index = state.Rewards.FindIndex(reward => reward.RewardId == action.RewardId);
        if (index < 0) throw new InvalidOperationException($"Reward {action.RewardId} is not in this reward set.");
        RunRewardOfferV1 reward = state.Rewards[index];
        if (reward.Disposition != RunRewardDispositionV1.Pending)
            throw new InvalidOperationException($"Reward {action.RewardId} is already resolved.");
        if (reward.ChoiceIds.Count == 0 && action.ChoiceId != null)
            throw new InvalidOperationException($"Reward {action.RewardId} does not have a selectable alternative.");
        if (reward.ChoiceIds.Count > 0 && (action.ChoiceId == null || !reward.ChoiceIds.Contains(action.ChoiceId, StringComparer.Ordinal)))
            throw new InvalidOperationException($"Choice {action.ChoiceId ?? "<missing>"} is not offered by reward {action.RewardId}.");

        List<RunRewardOfferV1> rewards = state.Rewards.Select(RunProgressStateV1.CopyReward).ToList();
        rewards[index] = reward with { Disposition = RunRewardDispositionV1.Selected, SelectedChoiceId = action.ChoiceId };
        return state with { Rewards = rewards };
    }

    private static RunProgressStateV1 SkipPendingRewards(RunProgressStateV1 state)
    {
        RequirePhase(state, RunProgressPhaseV1.Rewards, "Rewards can only be skipped while the reward set is open.");
        if (!state.CanSkipPendingRewards)
            throw new InvalidOperationException("The host reward set does not allow skipping pending rewards.");
        return state with
        {
            Rewards = state.Rewards.Select(reward => reward.Disposition == RunRewardDispositionV1.Pending
                ? reward with { Disposition = RunRewardDispositionV1.Skipped }
                : RunProgressStateV1.CopyReward(reward)).ToList(),
        };
    }

    private static RunProgressStateV1 ProceedToRoute(RunProgressStateV1 state)
    {
        RequirePhase(state, RunProgressPhaseV1.Rewards, "The map route opens after the reward set is resolved.");
        if (state.Rewards.Any(reward => reward.Disposition == RunRewardDispositionV1.Pending))
            throw new InvalidOperationException("Pending rewards must be selected or skipped before proceeding.");
        RunRouteNodeV1 current = state.RouteNodes.SingleOrDefault(node => node.NodeId == state.CurrentNodeId)
            ?? throw new InvalidOperationException($"Current node {state.CurrentNodeId} is missing from the route graph.");
        if (current.ChildNodeIds.Count == 0)
            throw new InvalidOperationException("This milestone requires a reachable next node; terminal act transitions are not modeled.");
        return state with { Phase = RunProgressPhaseV1.Route, LegalNextNodeIds = current.ChildNodeIds.ToList() };
    }

    private static RunProgressStateV1 EnterNode(RunProgressStateV1 state, RunProgressActionV1 action)
    {
        RequirePhase(state, RunProgressPhaseV1.Route, "A map node can only be entered after proceeding to the route.");
        if (string.IsNullOrWhiteSpace(action.NodeId) || !state.LegalNextNodeIds.Contains(action.NodeId, StringComparer.Ordinal))
            throw new InvalidOperationException($"Node {action.NodeId ?? "<missing>"} is not a legal child of {state.CurrentNodeId}.");
        RunRouteNodeV1 node = state.RouteNodes.SingleOrDefault(candidate => candidate.NodeId == action.NodeId)
            ?? throw new InvalidOperationException($"Legal node {action.NodeId} has no route snapshot.");
        if (node.MapPointType == "Unknown" && string.IsNullOrWhiteSpace(node.ResolvedRoomType))
            throw new InvalidOperationException("An Unknown map point must be resolved by the game before route entry.");
        return state with
        {
            Phase = RunProgressPhaseV1.NodeEntered,
            CurrentNodeId = node.NodeId,
            EnteredNodeId = node.NodeId,
            LegalNextNodeIds = [],
        };
    }

    private static void ValidateIdentity(RunProgressStateV1 state)
    {
        if (state.Schema != "run-progress/v1") throw new InvalidOperationException($"Unsupported run-progress schema {state.Schema}.");
        if (state.GameVersion != GameBuildPin.Version || state.GameCommit != GameBuildPin.Commit || state.MainAssemblyHash != GameBuildPin.MainAssemblyHash)
            throw new InvalidOperationException("Run-progress game build does not match the pinned v0.111.0 build.");
        if (string.IsNullOrWhiteSpace(state.CurrentNodeId)) throw new InvalidOperationException("Current map node is missing.");
        if (state.Rewards.Select(reward => reward.RewardId).Any(string.IsNullOrWhiteSpace)
            || state.Rewards.Select(reward => reward.RewardId).Distinct(StringComparer.Ordinal).Count() != state.Rewards.Count)
            throw new InvalidOperationException("Reward IDs are missing or duplicated.");
        if (state.Rewards.Any(reward => string.IsNullOrWhiteSpace(reward.Kind)
            || reward.ChoiceIds.Any(string.IsNullOrWhiteSpace)
            || reward.ChoiceIds.Distinct(StringComparer.Ordinal).Count() != reward.ChoiceIds.Count))
            throw new InvalidOperationException("Reward kind or selectable choice IDs are invalid.");
        if (state.RouteNodes.Select(node => node.NodeId).Any(string.IsNullOrWhiteSpace)
            || state.RouteNodes.Select(node => node.NodeId).Distinct(StringComparer.Ordinal).Count() != state.RouteNodes.Count)
            throw new InvalidOperationException("Route node IDs are missing or duplicated.");
        if (state.RouteNodes.Any(node => node.MapPointType.Length == 0 || node.ChildNodeIds.Distinct(StringComparer.Ordinal).Count() != node.ChildNodeIds.Count))
            throw new InvalidOperationException("Route node point types or child IDs are invalid.");
        if (state.LegalNextNodeIds.Distinct(StringComparer.Ordinal).Count() != state.LegalNextNodeIds.Count)
            throw new InvalidOperationException("Legal next-node IDs are duplicated.");
    }

    private static void RequirePhase(RunProgressStateV1 state, RunProgressPhaseV1 expected, string message)
    {
        if (state.Phase != expected) throw new InvalidOperationException(message);
    }
}
