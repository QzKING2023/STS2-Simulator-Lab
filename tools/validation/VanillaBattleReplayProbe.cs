using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CombatSolver;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Simulation;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Platform.Steam;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using SimulatorLab.Core;

namespace OfflineSearchHarness;

/// <summary>Replays a solver-selected vanilla route through both the actual game core and its prediction branch.</summary>
internal static class VanillaBattleReplayProbe
{
    private sealed record StepEvidence(
        int Sequence,
        int Turn,
        string Action,
        string BeforeNativeRootDigest,
        string BeforeSimulatorRootDigest,
        string AfterNativeRootDigest,
        string AfterSimulatorRootDigest,
        string BeforeContinuationSha256,
        string SimulatorContinuationSha256,
        string NativeContinuationSha256,
        string SimulatorStateText,
        string NativeStateText,
        bool ExactMatch);

    private static Func<object?>? _captureTerminal;
    private static object? _terminalEvidence;
    private static Exception? _terminalCaptureError;
    private static int _suppressedReplayWrites;

    public static object Run(CombatState combat, MainLoopContext loop, ModRuntime.SearchOutcome outcome, string outputDirectory)
    {
        VanillaGameActionProbe.InstallGodotClockShim();
        AssertIsolatedRuntime();
        if (combat.CurrentSide != CombatSide.Player)
            throw new InvalidOperationException("Full battle replay requires a player-action root.");
        if (!outcome.Result.Snapshot.AllEnemiesDead || outcome.Result.Snapshot.BoundaryReason != SearchBoundaryReason.None)
            throw new InvalidOperationException("The selected solver result does not prove a terminal victory route.");

        Player player = LocalContext.GetMe(combat)
            ?? throw new InvalidOperationException("No local player in the generated combat.");
        CombatRootSnapshot rootSnapshot = CombatRootSnapshot.Capture(combat);
        CombatPredictionSimulator simulator = rootSnapshot.ForkSimulator();
        SimulatedCombatState shadow = (SimulatedCombatState)simulator.State.CombatState;
        VanillaCombatRootAdapter adapter = new(combat, "LAB_V0111_IRONCLAD_M1_01");
        CombatBeamSolver driver = new(
            rootSnapshot,
            SolverDisplayNames.Capture(combat),
            BattleDamageTracker.Observe(combat),
            SolverController.CaptureSearchPolicy(SolverSettings.Capture(), combat, false, null));
        MethodInfo advanceRound = typeof(CombatBeamSolver).GetMethod("AdvanceRound", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(CombatBeamSolver), "AdvanceRound");
        PrimeMemoryOnlyProfileIdentity();
        InstallTerminalCapture(combat, adapter);

        List<StepEvidence> steps = [];
        HashSet<uint> processedEnemyDeaths = [];
        int shufflesCrossed = 0;
        int sequence = 0;
        bool sawShuffle = false;
        bool sawEnemyAction = false;
        bool sawPower = false;
        bool sawRelic = false;
        SortedSet<string> powerIdsSeen = new(StringComparer.Ordinal);
        try
        {
            string openingStamp = ContinuationStamp.CaptureLive(combat).StateText;
            CombatRootV1 openingRoot = adapter.CaptureLive(combat);
            RequireExactRoot(openingRoot, "opening live root");
            if (!openingRoot.Relics.Any(relic => relic.RelicId == "BURNING_BLOOD"))
                throw new InvalidOperationException("The baseline root is missing the Ironclad's Burning Blood relic state.");
            sawRelic = true;

            foreach (PlanAction planned in outcome.Result.BestNode.Actions)
            {
                if (planned.Kind == PlanActionKind.PlayCard)
                {
                    PlayerCombatState playerState = player.PlayerCombatState
                        ?? throw new InvalidOperationException("Player combat state disappeared during replay.");
                    if (playerState.TurnNumber != planned.Turn || playerState.Phase != PlayerTurnPhase.Play)
                        throw new InvalidOperationException($"Route drift before action {planned}: live turn={playerState.TurnNumber} phase={playerState.Phase}.");

                    CombatRootV1 nativeBeforeRoot = adapter.CaptureLive(combat);
                    RequireExactRoot(nativeBeforeRoot, "native action root");
                    string nativeBeforeDigest = StateDigest.Compute(nativeBeforeRoot);
                    string nativeBeforeText = ContinuationStamp.CaptureLive(combat).StateText;
                    PredictedCard predictedCard = FindPredictedHandCard(player, simulator, planned);
                    CardModel nativeCard = FindNativeHandCard(playerState, planned);
                    Creature? target = FindTarget(combat, planned, nativeCard);

                    CombatRootV1 simulatorBeforeRoot = adapter.CapturePredicted(
                        combat, player, simulator, planned.Turn, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber);
                    RequireExactRoot(simulatorBeforeRoot, "simulator action root");
                    string simulatorBeforeDigest = StateDigest.Compute(simulatorBeforeRoot);
                    RequireEqual(nativeBeforeDigest, simulatorBeforeDigest, "CombatRootV1 before action");
                    var simulatorBefore = ContinuationStamp.CapturePredicted(
                        player, simulator, planned.Turn, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber);
                    RequireEqual(nativeBeforeText, simulatorBefore.StateText, "continuation before action");

                    if (!shadow.CanPlayCard(simulator, predictedCard, out _, out _))
                        throw new InvalidOperationException($"Simulator refused planned {planned.CardId} on turn {planned.Turn}.");
                    if (!simulator.ManualPlay(predictedCard, target, out _))
                        throw new InvalidOperationException($"Simulator failed planned {planned.CardId} on turn {planned.Turn}.");
                    _ = simulator.CheckWinCondition(planned.Turn);
                    var simulatorAfter = ContinuationStamp.CapturePredicted(
                        player, simulator, planned.Turn, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber);
                    CombatRootV1 simulatorAfterRoot = adapter.CapturePredicted(
                        combat, player, simulator, planned.Turn, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber);
                    RequireExactRoot(simulatorAfterRoot, "simulator post-action root");

                    if (!nativeCard.TryManualPlay(target))
                        throw new InvalidOperationException($"Pinned game CardModel.TryManualPlay refused {planned.CardId}.");
                    loop.RunUntilCompleted(
                        RunManager.Instance.ActionExecutor.FinishedExecutingActions(),
                        TimeSpan.FromSeconds(30),
                        $"Lab native {planned.CardId} on turn {planned.Turn}");

                    if (_terminalCaptureError != null)
                        throw new InvalidOperationException("Read-only terminal capture failed; the error was retained and rethrown after the game action.", _terminalCaptureError);
                    CombatRootV1 nativeAfterRoot;
                    string nativeAfterText;
                    if (_terminalEvidence is TerminalCapture terminal)
                    {
                        nativeAfterRoot = terminal.Root;
                        nativeAfterText = terminal.StateText;
                    }
                    else
                    {
                        nativeAfterRoot = adapter.CaptureLive(combat);
                        nativeAfterText = ContinuationStamp.CaptureLive(combat).StateText;
                    }
                    RequireExactRoot(nativeAfterRoot, "native post-action root");
                    string nativeAfterDigest = StateDigest.Compute(nativeAfterRoot);
                    string simulatorAfterDigest = StateDigest.Compute(simulatorAfterRoot);
                    List<string> differences = simulatorAfter.DescribeDifferences(new ContinuationStamp(nativeAfterText)).ToList();
                    if (!StringComparer.Ordinal.Equals(simulatorAfterDigest, nativeAfterDigest) || differences.Count != 0)
                    {
                        List<string> rootDifferences = DescribeRootDifferences(simulatorAfterRoot, nativeAfterRoot);
                        File.WriteAllText(Path.Combine(outputDirectory, "battle-replay-divergence.json"),
                            JsonSerializer.Serialize(new { action = planned, simulatorAfterRoot, nativeAfterRoot,
                                simulatorContinuation = simulatorAfter.StateText, nativeContinuation = nativeAfterText,
                                rootDifferences, continuationDifferences = differences },
                                new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true }));
                        throw new InvalidOperationException($"Native game and simulator diverged after {planned.CardId}: rootFields=[{string.Join(",", rootDifferences)}]; continuation={string.Join(" | ", differences.Take(12))}.");
                    }
                    if (planned.CardId == "BASH")
                    {
                        string[] actionPowerIds = nativeAfterRoot.Combatants.Where(unit => !unit.IsPlayer)
                            .SelectMany(unit => unit.Powers)
                            .Where(power => power.Amount > 0)
                            .Select(power => power.ModelId)
                            .ToArray();
                        sawPower |= actionPowerIds.Length > 0;
                        foreach (string powerId in actionPowerIds)
                            powerIdsSeen.Add(powerId);
                    }
                    steps.Add(new(sequence++, planned.Turn, planned.CardId,
                        nativeBeforeDigest, simulatorBeforeDigest, nativeAfterDigest, simulatorAfterDigest,
                        Sha256(nativeBeforeText), Sha256(simulatorAfter.StateText), Sha256(nativeAfterText),
                        simulatorAfter.StateText, nativeAfterText, true));
                }
                else if (planned.Kind == PlanActionKind.EndTurn)
                {
                    PlayerCombatState playerState = player.PlayerCombatState
                        ?? throw new InvalidOperationException("Player combat state disappeared before EndTurn.");
                    if (playerState.TurnNumber != planned.Turn || playerState.Phase != PlayerTurnPhase.Play)
                        throw new InvalidOperationException($"EndTurn route drift: expected turn {planned.Turn}, found {playerState.TurnNumber}/{playerState.Phase}.");

                    CombatRootV1 nativeBeforeRoot = adapter.CaptureLive(combat);
                    RequireExactRoot(nativeBeforeRoot, "native pre-EndTurn root");
                    string nativeBeforeDigest = StateDigest.Compute(nativeBeforeRoot);
                    string nativeBeforeText = ContinuationStamp.CaptureLive(combat).StateText;
                    CombatRootV1 simulatorBeforeRoot = adapter.CapturePredicted(
                        combat, player, simulator, planned.Turn, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber);
                    RequireExactRoot(simulatorBeforeRoot, "simulator pre-EndTurn root");
                    string simulatorBeforeDigest = StateDigest.Compute(simulatorBeforeRoot);
                    RequireEqual(nativeBeforeDigest, simulatorBeforeDigest, "CombatRootV1 before EndTurn");

                    object?[] arguments = [simulator, shadow, planned.Turn - rootSnapshot.StartTurnNumber,
                        processedEnemyDeaths, shufflesCrossed, null, null];
                    object? boundary = advanceRound.Invoke(driver, arguments);
                    if (!StringComparer.Ordinal.Equals(boundary?.ToString(), "None"))
                        throw new InvalidOperationException($"CombatSolver stopped while advancing turn {planned.Turn}: {boundary}.");
                    shufflesCrossed = arguments[4] is int nextShuffleCount ? nextShuffleCount : shufflesCrossed;
                    _ = shadow.ConsumePlayerTurnEndRequest();
                    if (!CombatBeamSolver.SettleReplayActionBoundary(simulator, shadow))
                        throw new InvalidOperationException($"CombatSolver reached a pending choice while settling turn {planned.Turn}.");
                    var simulatorAfter = ContinuationStamp.CapturePredicted(
                        player, simulator, planned.Turn + 1, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber);
                    CombatRootV1 simulatorAfterRoot = adapter.CapturePredicted(
                        combat, player, simulator, planned.Turn + 1, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber);
                    RequireExactRoot(simulatorAfterRoot, "simulator next-turn root");

                    CombatManager.Instance.OnEndedTurnLocally();
                    RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new EndPlayerTurnAction(player, planned.Turn));
                    loop.RunUntilCompleted(
                        RunManager.Instance.ActionExecutor.FinishedExecutingActions(),
                        TimeSpan.FromSeconds(30),
                        $"Lab native EndTurn {planned.Turn} with enemy turn and next draw");
                    if (_terminalCaptureError != null)
                        throw new InvalidOperationException("Read-only terminal capture failed during enemy action.", _terminalCaptureError);
                    if (player.PlayerCombatState is not { Phase: PlayerTurnPhase.Play } nextState
                        || nextState.TurnNumber != planned.Turn + 1)
                        throw new InvalidOperationException($"Native EndTurn did not reach turn {planned.Turn + 1} player decision phase.");

                    var nativeAfter = ContinuationStamp.CaptureLive(combat);
                    CombatRootV1 nativeAfterRoot = adapter.CaptureLive(combat);
                    RequireExactRoot(nativeAfterRoot, "native next-turn root");
                    string nativeAfterDigest = StateDigest.Compute(nativeAfterRoot);
                    string simulatorAfterDigest = StateDigest.Compute(simulatorAfterRoot);
                    List<string> differences = simulatorAfter.DescribeDifferences(nativeAfter).ToList();
                    if (!StringComparer.Ordinal.Equals(simulatorAfterDigest, nativeAfterDigest) || differences.Count != 0)
                        throw new InvalidOperationException($"Native game and simulator diverged across EndTurn {planned.Turn}: rootDiff={simulatorAfterDigest != nativeAfterDigest}; continuation={string.Join(" | ", differences.Take(12))}.");
                    long shuffleBefore = nativeBeforeRoot.RandomStreams.Single(stream => stream.Stream == CombatRngStream.Shuffle).DrawCount;
                    long shuffleAfter = nativeAfterRoot.RandomStreams.Single(stream => stream.Stream == CombatRngStream.Shuffle).DrawCount;
                    sawShuffle |= shuffleAfter > shuffleBefore;
                    sawEnemyAction |= nativeAfterRoot.Combatants.Single(unit => unit.IsPlayer).CurrentHp
                        < nativeBeforeRoot.Combatants.Single(unit => unit.IsPlayer).CurrentHp;
                    steps.Add(new(sequence++, planned.Turn, "EndTurn+enemy-turn+draw",
                        nativeBeforeDigest, simulatorBeforeDigest, nativeAfterDigest, simulatorAfterDigest,
                        Sha256(nativeBeforeText), Sha256(simulatorAfter.StateText), Sha256(nativeAfter.StateText),
                        simulatorAfter.StateText, nativeAfter.StateText, true));
                }
            }

            if (_terminalCaptureError != null)
                throw new InvalidOperationException("Read-only terminal capture failed; the error was retained and rethrown.", _terminalCaptureError);
            if (_terminalEvidence is not TerminalCapture finalTerminal)
                throw new InvalidOperationException("The native game did not enter its combat victory/teardown path.");
            if (simulator.TerminalStamp?.Outcome != CombatTerminalOutcome.Victory)
                throw new InvalidOperationException($"Simulator did not finish with Victory: {simulator.TerminalStamp?.Outcome}.");
            if (!sawShuffle || !sawEnemyAction || !sawPower || !sawRelic)
                throw new InvalidOperationException($"Coverage gate missing: shuffle={sawShuffle} enemyAction={sawEnemyAction} strength/power={sawPower} relic={sawRelic}.");

            var terminalRoot = finalTerminal.Root;
            var terminalText = finalTerminal.StateText;
            var predictedTerminal = ContinuationStamp.CapturePredicted(
                player, simulator, player.PlayerCombatState?.TurnNumber ?? outcome.Result.Snapshot.Turn,
                rootSnapshot.Forecast, rootSnapshot.StartTurnNumber);
            List<string> terminalDifferences = predictedTerminal.DescribeDifferences(new ContinuationStamp(terminalText)).ToList();
            if (terminalDifferences.Count != 0)
                throw new InvalidOperationException("Victory state differs before native combat teardown: " + string.Join(" | ", terminalDifferences.Take(16)));
            RequireExactRoot(terminalRoot, "native victory boundary");

            var trace = new
            {
                schema = "vanilla-battle-replay/v1",
                gameVersion = SimulatorLab.Core.GameBuildPin.Version,
                gameCommit = SimulatorLab.Core.GameBuildPin.Commit,
                mainAssemblyHash = SimulatorLab.Core.GameBuildPin.MainAssemblyHash,
                characterModelId = player.Character.Id.Entry,
                encounterModelId = combat.Encounter?.Id.Entry,
                scenarioId = "LAB_V0111_IRONCLAD_M1_01",
                testMode = TestMode.IsOn,
                steamInitialized = SteamInitializer.Initialized,
                saveStore = VerifyMemorySaveStore(),
                myModLoaded = false,
                usesPresentationClockShim = true,
                headlessUiCleanupSuppressed = true,
                replayFileWritesSuppressed = _suppressedReplayWrites,
                root = openingRoot,
                rootDigest = StateDigest.Compute(openingRoot),
                steps,
                terminal = new
                {
                    outcome = simulator.TerminalStamp!.Value.Outcome.ToString(),
                    nativeTerminalRoot = terminalRoot,
                    nativeTerminalDigest = StateDigest.Compute(terminalRoot),
                    simulatorTerminalState = predictedTerminal.StateText,
                    nativeTerminalState = terminalText,
                    terminalSha256 = new { simulator = Sha256(predictedTerminal.StateText), native = Sha256(terminalText) },
                    victoryTeardownObserved = true,
                },
                coverage = new
                {
                    cardActions = steps.Count(step => step.Action != "EndTurn+enemy-turn+draw"),
                    endedTurns = steps.Count(step => step.Action == "EndTurn+enemy-turn+draw"),
                    shuffle = sawShuffle,
                    enemyAction = sawEnemyAction,
                    powers = sawPower,
                    powerModelIdsAfterBash = powerIdsSeen.ToArray(),
                    relicState = sawRelic,
                    randomStreams = terminalRoot.RandomStreams.Select(stream => stream.Stream.ToString()).OrderBy(x => x).ToArray(),
                },
            };
            string output = Path.Combine(outputDirectory, "vanilla-battle-replay.json");
            File.WriteAllText(output, JsonSerializer.Serialize(trace,
                new JsonSerializerOptions(SimulatorLab.Core.ProtocolJson.Options) { WriteIndented = true }));
            var json = JsonDocument.Parse(File.ReadAllText(output));
            if (json.RootElement.GetProperty("steps").EnumerateArray().Count() != steps.Count
                || json.RootElement.GetProperty("terminal").GetProperty("terminalSha256").GetProperty("simulator")
                    .GetString() != json.RootElement.GetProperty("terminal").GetProperty("terminalSha256").GetProperty("native").GetString())
                throw new InvalidOperationException("Full battle replay evidence failed its post-write integrity check.");
            return new { output, stepCount = steps.Count, turn = player.PlayerCombatState?.TurnNumber, victory = true, shuffle = sawShuffle, enemyAction = sawEnemyAction, power = sawPower, relic = sawRelic };
        }
        finally
        {
            _captureTerminal = null;
            _terminalEvidence = null;
            _terminalCaptureError = null;
            _suppressedReplayWrites = 0;
        }
    }

    private static PredictedCard FindPredictedHandCard(Player player, CombatPredictionSimulator simulator, PlanAction action)
        => simulator.State.GetPlayerCombatState(player).Hand.Cards
            .Where(card => StringComparer.Ordinal.Equals(card.Preview.Id.Entry, action.CardId))
            .Skip(action.CardOccurrence)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Predicted hand lacks {action.CardId} occurrence {action.CardOccurrence}.");

    private static CardModel FindNativeHandCard(PlayerCombatState playerState, PlanAction action)
        => playerState.Hand.Cards
            .Where(card => StringComparer.Ordinal.Equals(card.Id.Entry, action.CardId))
            .Skip(action.CardOccurrence)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Native hand lacks {action.CardId} occurrence {action.CardOccurrence}.");

    private static Creature? FindTarget(CombatState combat, PlanAction action, CardModel card)
    {
        if (card.TargetType.ToString().Contains("Random", StringComparison.OrdinalIgnoreCase))
            return null;
        return combat.Enemies.FirstOrDefault(enemy => enemy.CombatId == action.TargetCombatId)
            ?? throw new InvalidOperationException($"Planned target {action.TargetCombatId} is not a live enemy for {card.Id.Entry} ({card.TargetType}).");
    }

    private static void RequireExactRoot(CombatRootV1 root, string label)
    {
        SimulatorLab.Core.CaptureValidationV1 validation = SimulatorLab.Core.CombatCaptureValidator.ValidateExactReady(root);
        if (!validation.IsExactReady)
            throw new InvalidOperationException($"{label} failed CombatRootV1 exact-ready validation: {string.Join("; ", validation.Errors)}");
    }

    private static void RequireEqual(string expected, string actual, string label)
    {
        if (!StringComparer.Ordinal.Equals(expected, actual))
            throw new InvalidOperationException($"{label} differs: expectedSha={Sha256(expected)} actualSha={Sha256(actual)}.");
    }

    private static List<string> DescribeRootDifferences(CombatRootV1 expected, CombatRootV1 actual)
    {
        JsonElement left = JsonSerializer.SerializeToElement(expected, ProtocolJson.Options);
        JsonElement right = JsonSerializer.SerializeToElement(actual, ProtocolJson.Options);
        Dictionary<string, JsonElement> leftProperties = left.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
        Dictionary<string, JsonElement> rightProperties = right.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
        return leftProperties.Keys.Union(rightProperties.Keys, StringComparer.Ordinal)
            .Where(key => !leftProperties.TryGetValue(key, out JsonElement leftValue)
                || !rightProperties.TryGetValue(key, out JsonElement rightValue)
                || !StringComparer.Ordinal.Equals(StateDigest.Compute(leftValue), StateDigest.Compute(rightValue)))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
    }

    private static string Sha256(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static void AssertIsolatedRuntime()
    {
        if (!TestMode.IsOn || SteamInitializer.Initialized)
            throw new InvalidOperationException("Battle replay requires TestMode and an uninitialized Steam runtime.");
        bool myMod = AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
            StringComparer.OrdinalIgnoreCase.Equals(assembly.GetName().Name, "MyMod"));
        if (myMod)
            throw new InvalidOperationException("Vanilla baseline refuses to load MyMod.");
        _ = VerifyMemorySaveStore();
    }

    private static string VerifyMemorySaveStore()
    {
        object saveStore = typeof(SaveManager)
            .GetField("_saveStore", BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(SaveManager.Instance)
            ?? throw new InvalidOperationException("Could not verify the active SaveManager backend.");
        string name = saveStore.GetType().FullName ?? saveStore.GetType().Name;
        if (!StringComparer.Ordinal.Equals(saveStore.GetType().Name, "MemorySaveStore"))
            throw new InvalidOperationException($"Refusing runtime save backend {name}.");
        return name;
    }

    private static void PrimeMemoryOnlyProfileIdentity()
    {
        SaveManager manager = SaveManager.Instance;
        if (manager.IsProfileInitialized)
            throw new InvalidOperationException("Offline harness unexpectedly initialized a persistent profile before the battle probe.");
        FieldInfo profileField = typeof(SaveManager).GetField("_currentProfileId", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(SaveManager), "_currentProfileId");
        profileField.SetValue(manager, 0);
        if (!manager.IsProfileInitialized || manager.CurrentProfileId != 0)
            throw new InvalidOperationException("Could not set the deterministic in-memory profile identity.");

        MethodInfo writeReplay = AccessTools.Method(typeof(RunManager), nameof(RunManager.WriteReplay), [typeof(bool)])
            ?? throw new MissingMethodException(nameof(RunManager), nameof(RunManager.WriteReplay));
        GameBootstrap.Harmony.Patch(writeReplay, prefix: new HarmonyMethod(typeof(VanillaBattleReplayProbe), nameof(SuppressReplayFileWritePrefix)));
    }

    private static bool SuppressReplayFileWritePrefix()
    {
        _suppressedReplayWrites++;
        return false;
    }

    private static void InstallTerminalCapture(CombatState combat, VanillaCombatRootAdapter adapter)
    {
        MethodInfo[] candidates = typeof(CombatManager).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(method => StringComparer.Ordinal.Equals(method.Name, "EndCombatInternal"))
            .Where(method => method.GetParameters() is { Length: 1 } parameters
                && StringComparer.Ordinal.Equals(parameters[0].ParameterType.Name, "CombatTurnState"))
            .ToArray();
        if (candidates.Length != 1)
            throw new MissingMethodException($"Expected one private CombatManager.EndCombatInternal(CombatTurnState) method; found {candidates.Length}; signatures={string.Join(" | ", typeof(CombatManager).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Where(method => method.Name == "EndCombatInternal").Select(method => method.ToString()))}.");
        _captureTerminal = () =>
        {
            SimulatorLab.Core.CombatRootV1 root = adapter.CaptureLive(combat, terminalVictory: true);
            string stateText = ContinuationStamp.CaptureLive(combat).StateText;
            return new TerminalCapture(root, stateText);
        };
        _terminalEvidence = null;
        _terminalCaptureError = null;
        GameBootstrap.Harmony.Patch(candidates[0], prefix: new HarmonyMethod(typeof(VanillaBattleReplayProbe), nameof(TerminalPrefix)));
        MethodInfo hoverClear = AccessTools.Method(typeof(NHoverTipSet), nameof(NHoverTipSet.Clear))
            ?? throw new MissingMethodException(nameof(NHoverTipSet), nameof(NHoverTipSet.Clear));
        GameBootstrap.Harmony.Patch(hoverClear, prefix: new HarmonyMethod(typeof(VanillaBattleReplayProbe), nameof(SkipHeadlessHoverTipClearPrefix)));
    }

    private sealed record TerminalCapture(CombatRootV1 Root, string StateText);

    private static void TerminalPrefix()
    {
        if (_captureTerminal == null || _terminalEvidence != null || _terminalCaptureError != null)
            return;
        try { _terminalEvidence = _captureTerminal(); }
        catch (Exception error) { _terminalCaptureError = error; }
    }

    private static bool SkipHeadlessHoverTipClearPrefix() => false;
}
