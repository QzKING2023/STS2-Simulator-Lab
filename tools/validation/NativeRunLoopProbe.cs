using System.Collections;
using System.Reflection;
using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Platform.Steam;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using SimulatorLab.Core;

namespace OfflineSearchHarness;

/// <summary>
/// Exercises the native v0.111.0 combat-reward and no-save map-entry APIs after
/// the M1 replay. It runs only in TestMode with Steam disabled and MemorySaveStore.
/// </summary>
internal static class NativeRunLoopProbe
{
    private sealed record RewardItem(string RewardId, string Kind, bool IsPopulated, bool WasSelected, string? SerializableKind);
    private sealed record RewardCapture(int SetId, bool CanSkip, List<RewardItem> Items, string? SelectedRewardId, bool SkippedPending);
    private static RewardCapture? _capture;
    private static int _offerCount;

    public static void Arm()
    {
        EnsureIsolated();
        _capture = null;
        _offerCount = 0;
        RewardsSet.testSelector = SelectAndSkipCombatRewards;
    }

    public static object Run(MainLoopContext loop, string outputDirectory)
    {
        EnsureIsolated();
        RunManager manager = RunManager.Instance;
        RunState runState = manager.State ?? throw new InvalidOperationException("No active isolated RunState after combat replay.");
        Player player = runState.Players.SingleOrDefault()
            ?? throw new InvalidOperationException("No player in the isolated run.");
        CombatRoom? combatRoom = runState.CurrentRoom as CombatRoom;
        if (combatRoom == null)
            throw new InvalidOperationException($"Victory reward probe expected a current CombatRoom, found {runState.CurrentRoom?.GetType().Name ?? "null"}.");
        if (!runState.VisitedMapCoords.Any())
            throw new InvalidOperationException("M3 run was not entered through a real generated map start node.");

        if (_capture == null)
        {
            // The normal combat-end path did not offer rewards in this headless
            // setup; invoke the game's standard room-end command once.
            Task offer = RewardsCmd.OfferForRoomEnd(player, combatRoom);
            loop.RunUntilCompleted(offer, TimeSpan.FromSeconds(30), "Offer native combat room-end rewards");
        }
        RewardCapture rewardCapture = _capture ?? throw new InvalidOperationException(
            "The native reward selector was not reached after both combat-end and explicit room-end offer.");
        if (_offerCount != 1)
            throw new InvalidOperationException($"Expected exactly one native combat reward set, observed {_offerCount}.");
        if (rewardCapture.Items.Count == 0)
            throw new InvalidOperationException("The native room-end reward set was empty; this fixture requires at least one offered reward.");
        if (!rewardCapture.SkippedPending)
            throw new InvalidOperationException("The native selector did not exercise the allowed skip-pending path.");

        Task openMap = manager.ProceedFromTerminalRewardsScreen();
        loop.RunUntilCompleted(openMap, TimeSpan.FromSeconds(30), "Proceed from native terminal reward screen");

        ActMap map = runState.Map ?? throw new InvalidOperationException("The active run has no generated ActMap.");
        MapPoint current = runState.CurrentMapPoint
            ?? throw new InvalidOperationException("The current visited coordinate did not resolve to a map point.");
        List<MapPoint> legalChildren = current.Children.OrderBy(point => point.coord.col).ThenBy(point => point.coord.row).ToList();
        if (legalChildren.Count == 0)
            throw new InvalidOperationException($"Current map point {NodeId(current)} has no child node for this minimum-cycle fixture.");
        MapPoint next = legalChildren.OrderBy(point => point.PointType == MapPointType.Monster ? 0 : 1)
            .ThenBy(point => point.coord.col).ThenBy(point => point.coord.row).First();

        List<MapPoint> localGraph = [current, .. legalChildren];
        List<RunRouteNodeV1> routeNodes = localGraph.Select(point => new RunRouteNodeV1
        {
            NodeId = NodeId(point),
            Column = point.coord.col,
            Row = point.coord.row,
            MapPointType = point.PointType.ToString(),
            // Unknown has no fixed room result until the game's own resolver runs.
            ResolvedRoomType = point.PointType == MapPointType.Unknown
                ? (point == current ? combatRoom.RoomType.ToString() : null)
                : ToRoomType(point.PointType),
            ChildNodeIds = point.Children.OrderBy(child => child.coord.col).ThenBy(child => child.coord.row).Select(NodeId).ToList(),
        }).ToList();
        List<RunRewardOfferV1> offeredRewards = rewardCapture.Items.Select(item => new RunRewardOfferV1
        {
            RewardId = item.RewardId,
            Kind = item.Kind,
        }).ToList();
        RunProgressStateV1 progress = RunProgressStateV1.AfterCombat(
            RunCombatOutcomeV1.Victory,
            NodeId(current),
            offeredRewards,
            rewardCapture.CanSkip,
            routeNodes);
        progress = RunProgressRulesV1.Apply(progress, new RunProgressActionV1 { Kind = RunProgressActionKindV1.BeginRewards });
        if (rewardCapture.SelectedRewardId != null)
            progress = RunProgressRulesV1.Apply(progress, new RunProgressActionV1
            {
                Kind = RunProgressActionKindV1.SelectReward,
                RewardId = rewardCapture.SelectedRewardId,
            });
        progress = RunProgressRulesV1.Apply(progress, new RunProgressActionV1 { Kind = RunProgressActionKindV1.SkipPendingRewards });
        progress = RunProgressRulesV1.Apply(progress, new RunProgressActionV1 { Kind = RunProgressActionKindV1.ProceedToRoute });
        if (!progress.LegalNextNodeIds.Contains(NodeId(next), StringComparer.Ordinal))
            throw new InvalidOperationException("The native map child is not legal under the run-progress contract.");

        int floorBefore = runState.ActFloor;
        if (!runState.AddVisitedMapCoord(next.coord))
            throw new InvalidOperationException($"Could not add legal child coordinate {NodeId(next)} to isolated visit history.");
        Task enterNode = manager.EnterMapPointInternal(next.coord.row + 1, next.PointType, null, saveGame: false);
        loop.RunUntilCompleted(enterNode, TimeSpan.FromSeconds(45), "Enter the next native map point without saving");
        Task nextRoomActions = manager.ActionExecutor.FinishedExecutingActions();
        loop.RunUntilCompleted(nextRoomActions, TimeSpan.FromSeconds(45), "Finish native next-room entry actions");

        RoomType actualRoomType = runState.CurrentRoom?.RoomType
            ?? throw new InvalidOperationException("Native map-point entry did not create a current room.");
        string nextId = NodeId(next);
        routeNodes = routeNodes.Select(node => node.NodeId == nextId
            ? node with { ResolvedRoomType = actualRoomType.ToString() }
            : node).ToList();
        progress = progress with { RouteNodes = routeNodes };
        progress = RunProgressRulesV1.Apply(progress, new RunProgressActionV1
        {
            Kind = RunProgressActionKindV1.EnterNode,
            NodeId = nextId,
        });
        if (runState.ActFloor != next.coord.row + 1 || progress.CurrentNodeId != nextId)
            throw new InvalidOperationException("Native room entry and run-progress coordinate did not match.");

        object saveStore = typeof(SaveManager).GetField("_saveStore", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(SaveManager.Instance) ?? throw new InvalidOperationException("Could not verify active save backend.");
        if (saveStore.GetType().Name != "MemorySaveStore")
            throw new InvalidOperationException($"Run-loop probe refuses save backend {saveStore.GetType().FullName}.");

        var report = new
        {
            schema = "native-run-loop-differential/v1",
            gameVersion = GameBuildPin.Version,
            gameCommit = GameBuildPin.Commit,
            mainAssemblyHash = GameBuildPin.MainAssemblyHash,
            testMode = TestMode.IsOn,
            steamInitialized = SteamInitializer.Initialized,
            saveStore = saveStore.GetType().FullName,
            gameInstallWrites = false,
            entryPath = "EnterMapPointInternal(saveGame:false)",
            mapGenerator = map.GetType().FullName,
            startingNode = NodeId(current),
            combatOutcome = "Victory",
            rewardSetId = rewardCapture.SetId,
            rewardSetCanSkip = rewardCapture.CanSkip,
            offeredRewards = rewardCapture.Items,
            selectedRewardId = rewardCapture.SelectedRewardId,
            skippedPendingRewards = rewardCapture.SkippedPending,
            terminalRewardScreenProceeded = true,
            floorBeforeNextNode = floorBefore,
            nextNode = new
            {
                nodeId = nextId,
                next.coord.col,
                next.coord.row,
                mapPointType = next.PointType.ToString(),
                resolvedRoomType = actualRoomType.ToString(),
                legalFromNode = NodeId(current),
            },
            floorAfterNextNode = runState.ActFloor,
            currentRoomAfterNextNode = runState.CurrentRoom?.RoomType.ToString(),
            protocolPhase = progress.Phase.ToString(),
            protocolState = progress,
        };
        File.WriteAllText(Path.Combine(outputDirectory, "native-run-loop-differential.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true }));
        RewardsSet.testSelector = null;
        return report;
    }

    private static async Task SelectAndSkipCombatRewards(RewardsSet set)
    {
        if (set.Room is not CombatRoom) return;
        _offerCount++;
        if (_offerCount > 1) throw new InvalidOperationException("The native combat room offered a second reward set unexpectedly.");
        bool skipAllowed = !set.DisallowSkipping;
        List<RewardItem> items = set.Rewards.Select((reward, index) => new RewardItem(
                RewardId(set.Id, index),
                reward.GetType().Name,
                reward.IsPopulated,
                reward.SuccessfullySelected,
                SafeRewardType(reward))).ToList();
        // Card reward selection constructs CardRewardAlternative and touches
        // Godot input singletons in headless TestMode. This fixture deliberately
        // exercises the legal skip path and records native offers without
        // calling reward OnSelect handlers.
        if (skipAllowed) RunManager.Instance.RewardsSetSynchronizer.SkipLocalRewardsSet();

        _capture = new RewardCapture(set.Id, skipAllowed, items, SelectedRewardId: null, SkippedPending: skipAllowed);
    }

    private static void EnsureIsolated()
    {
        if (!TestMode.IsOn || SteamInitializer.Initialized)
            throw new InvalidOperationException("Native run-loop probe requires TestMode without Steam.");
        object saveStore = typeof(SaveManager).GetField("_saveStore", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(SaveManager.Instance) ?? throw new InvalidOperationException("Could not verify the active save backend.");
        if (saveStore.GetType().Name != "MemorySaveStore")
            throw new InvalidOperationException($"Native run-loop probe refuses save backend {saveStore.GetType().FullName}.");
    }

    private static string RewardId(int setId, int index) => $"reward-set:{setId}:{index}";
    private static string NodeId(MapPoint point) => $"{point.coord.col},{point.coord.row}";

    private static string? SafeRewardType(Reward reward)
    {
        try { return reward.ToSerializable().RewardType.ToString(); }
        catch { return null; }
    }

    private static string? ToRoomType(MapPointType pointType) => pointType switch
    {
        MapPointType.Monster => RoomType.Monster.ToString(),
        MapPointType.Elite => RoomType.Elite.ToString(),
        MapPointType.Boss => RoomType.Boss.ToString(),
        MapPointType.Treasure => RoomType.Treasure.ToString(),
        MapPointType.Shop => RoomType.Shop.ToString(),
        MapPointType.RestSite => RoomType.RestSite.ToString(),
        MapPointType.Ancient => RoomType.Event.ToString(),
        _ => null,
    };
}
