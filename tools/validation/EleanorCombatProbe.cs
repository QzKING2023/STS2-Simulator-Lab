using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CombatSolver;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;
using CombatSolver.Engine.InCombat.Simulation;
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
using HarmonyLib;
using SimulatorLab.Core;

namespace OfflineSearchHarness;

/// <summary>Captures the local Eleanor runtime at stable boundaries and compares registered card effects and root guards.</summary>
internal static class EleanorCombatProbe
{
    private const string LatePatchOwner = "simulatorlab.eleanor.late-patch-guard-probe";
    private static readonly Harmony LatePatchProbe = new(LatePatchOwner);
    private sealed record FullBattleTerminal(CombatRootV1 Root, string StateText, RuntimeSnapshot Snapshot);
    private static Func<FullBattleTerminal>? _captureFullBattleTerminal;
    private static FullBattleTerminal? _fullBattleTerminal;
    private static Exception? _fullBattleTerminalError;
    private static int _suppressedFullBattleReplayWrites;
    private static int _suppressedFullBattleProgressSaves;
    private sealed record RuntimeSnapshot(
        int Mana,
        int Gauge,
        string Stance,
        bool CompleteOrb,
        int StartDrawBonus,
        int? PendingDiagnosticGauge,
        bool Listening,
        int ActiveCardTransactions,
        bool ReverseSyncing,
        string? ReverseIssue,
        SortedDictionary<string, int> OwnedPowerContributions,
        List<string> ActivePowerSources,
        List<string> AwaitingPowerRestore,
        SortedDictionary<string, string> PowerIdentities,
        int ActiveDualityReplays);

    private sealed record ManaBurstProbeResult(RuntimeSnapshot LiveAfter, object Evidence);

    public static object Run(CombatState combat, MainLoopContext loop, string outputDirectory)
    {
        VanillaGameActionProbe.InstallGodotClockShim();
        if (!TestMode.IsOn || SteamInitializer.Initialized)
            throw new InvalidOperationException("Eleanor probe requires isolated TestMode without Steam.");
        object saveStore = typeof(SaveManager).GetField("_saveStore", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(SaveManager.Instance)
            ?? throw new InvalidOperationException("Could not verify the active save backend.");
        if (saveStore.GetType().Name != "MemorySaveStore")
            throw new InvalidOperationException($"Eleanor probe refuses save backend {saveStore.GetType().FullName}.");

        Player player = LocalContext.GetMe(combat) ?? throw new InvalidOperationException("No local player in Eleanor combat.");
        if (player.Character.Id.Entry != "MY_MOD")
            throw new InvalidOperationException($"Expected MyMod character, found {player.Character.Id.Entry}.");
        PlayerCombatState playerState = player.PlayerCombatState ?? throw new InvalidOperationException("Player combat state is missing.");
        if (combat.CurrentSide != CombatSide.Player || playerState.Phase != PlayerTurnPhase.Play)
            throw new InvalidOperationException("Eleanor capture requires a stable player decision boundary.");

        Assembly mod = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name == "MyMod")
            ?? throw new InvalidOperationException("The pinned MyMod assembly was not loaded in this process.");
        Type runtimeType = mod.GetType("MyMod.MyModCode.Combat.ManaRuntime", throwOnError: true)!;
        object runtime = runtimeType.GetMethod("For", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [player]) ?? throw new InvalidOperationException("ManaRuntime.For returned null.");
        Type stateType = mod.GetType("MyMod.MyModCode.Combat.ManaState", throwOnError: true)!;
        FieldInfo cardsField = RequiredField(runtimeType, "_cards");
        FieldInfo reverseField = RequiredField(runtimeType, "_reverse");
        FieldInfo dualityPlayingField = mod.GetType("MyMod.MyModCode.Combat.DualityRuntime", true)!
            .GetField("Playing", BindingFlags.Static | BindingFlags.NonPublic)!;

        RuntimeSnapshot opening = CaptureRuntime(runtime, stateType, cardsField, reverseField, dualityPlayingField);
        bool completeOrbScenario = player.Relics.Any(relic => relic.GetType().Name == "CompleteCrystalOrb");
        bool manaToShieldScenario = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MANA_TO_SHIELD") == "1";
        bool fullBattleScenario = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_FULL_BATTLE") == "1";
        bool crossTurnScenario = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_CROSS_TURN") == "1";
        if (crossTurnScenario && (!completeOrbScenario || manaToShieldScenario || fullBattleScenario))
            throw new InvalidOperationException("Mentor cross-turn fixture requires CompleteCrystalOrb without the ManaToShield/full-battle modes.");
        string expectedOrbModel = completeOrbScenario ? "CompleteCrystalOrb" : "CrackedCrystalOrb";
        if (!opening.Listening)
            throw new InvalidOperationException($"{expectedOrbModel} did not start the ManaRuntime hooks for this combat.");
        if (opening.ActiveCardTransactions != 0 || opening.ReverseSyncing || opening.ActiveDualityReplays != 0)
            throw new InvalidOperationException("The live Eleanor runtime is not quiescent at the player decision boundary.");
        int expectedOpeningMana = completeOrbScenario ? 7 : 3;
        int expectedOpeningEnergy = completeOrbScenario ? 4 : 3;
        if (opening.Mana != expectedOpeningMana || opening.Gauge != 0 || opening.Stance != "Neutral"
            || opening.CompleteOrb != completeOrbScenario || playerState.Energy != expectedOpeningEnergy)
            throw new InvalidOperationException($"Unexpected {expectedOrbModel} opening state: mana={opening.Mana}, gauge={opening.Gauge}, stance={opening.Stance}, complete={opening.CompleteOrb}, energy={playerState.Energy}.");
        if (opening.OwnedPowerContributions.Count != 0 || opening.ActivePowerSources.Count != 0
            || opening.AwaitingPowerRestore.Count != 0 || opening.ReverseIssue != null
            || opening.PendingDiagnosticGauge is not null
            || player.Creature.Powers.Any(power => power.GetType().Name == "ManaToShieldPower" && power.Amount > 0))
            throw new PredictionUnsupportedException("This fixture cannot mirror active reverse-power ownership, restore, or diagnostic state.");

        EleanorCardMirrors.Register(mod, EleanorModBootstrap.ManifestJson, stateType, opening.Mana, opening.Gauge, opening.Stance,
            opening.CompleteOrb, opening.StartDrawBonus);
        RelicModel orbRelic = player.Relics.Single(relic => relic.GetType().Name is "CrackedCrystalOrb" or "CompleteCrystalOrb");

        CardModel canonical = ModelDb.All.OfType<CardModel>().FirstOrDefault(card => card.GetType().Name == "ManaGather")
            ?? throw new InvalidOperationException("Registered MyMod ManaGather model is missing from ModelDb.");
        CardModel nativeCard = playerState.Hand.Cards.FirstOrDefault(card => card.GetType().Name == "ManaGather")
            ?? combat.CreateCard(canonical, player);
        if (!playerState.Hand.Cards.Contains(nativeCard))
        {
            Task<CardPileAddResult> add = CardPileCmd.Add(nativeCard, PileType.Hand);
            loop.RunUntilCompleted(add, TimeSpan.FromSeconds(30), "Add isolated Eleanor ManaGather fixture");
            if (!add.GetAwaiter().GetResult().success)
                throw new InvalidOperationException("Game rejected the in-combat ManaGather fixture.");
        }
        CardModel? nativeManaBurst = null;
        if (completeOrbScenario && !manaToShieldScenario && !fullBattleScenario)
        {
            CardModel burstCanonical = ModelDb.All.OfType<CardModel>().Single(card => card.GetType().Name == "ManaBurst");
            nativeManaBurst = combat.CreateCard(burstCanonical, player);
            Task<CardPileAddResult> addBurst = CardPileCmd.Add(nativeManaBurst, PileType.Hand);
            loop.RunUntilCompleted(addBurst, TimeSpan.FromSeconds(30), "Add isolated Eleanor ManaBurst fixture");
            if (!addBurst.GetAwaiter().GetResult().success)
                throw new InvalidOperationException("Game rejected the in-combat ManaBurst fixture.");
        }
        CardModel[] crossTurnSetupStrikes = [];
        if (crossTurnScenario)
        {
            CardModel strikeCanonical = ModelDb.All.OfType<CardModel>().Single(card => card.GetType().Name == "ManaStrike");
            int strikesInHand = playerState.Hand.Cards.Count(card => card.GetType().Name == "ManaStrike");
            while (strikesInHand < 1)
            {
                CardModel extraStrike = combat.CreateCard(strikeCanonical, player);
                Task<CardPileAddResult> addStrike = CardPileCmd.Add(extraStrike, PileType.Hand);
                loop.RunUntilCompleted(addStrike, TimeSpan.FromSeconds(30), "Add isolated Eleanor first-turn ManaStrike fixture");
                if (!addStrike.GetAwaiter().GetResult().success)
                    throw new InvalidOperationException("Game rejected an in-combat first-turn ManaStrike fixture.");
                strikesInHand++;
            }
            for (int index = 0; index < 2; index++)
            {
                CardModel drawStrike = combat.CreateCard(strikeCanonical, player);
                Task<CardPileAddResult> addDrawStrike = CardPileCmd.Add(drawStrike, PileType.Draw);
                loop.RunUntilCompleted(addDrawStrike, TimeSpan.FromSeconds(30), "Add isolated Eleanor next-turn ManaStrike fixture");
                if (!addDrawStrike.GetAwaiter().GetResult().success)
                    throw new InvalidOperationException("Game rejected a next-turn ManaStrike draw-pile fixture.");
            }
            crossTurnSetupStrikes = playerState.Hand.Cards.Where(card => card.GetType().Name == "ManaStrike").Take(1).ToArray();
        }
        CardModel? nativeManaToShield = null;
        if (manaToShieldScenario)
        {
            if (!completeOrbScenario)
                throw new InvalidOperationException("ManaToShield fixture requires the complete-orb energy budget.");
            CardModel shieldCanonical = ModelDb.All.OfType<CardModel>().Single(card => card.GetType().Name == "ManaToShield");
            nativeManaToShield = combat.CreateCard(shieldCanonical, player);
            Task<CardPileAddResult> addShield = CardPileCmd.Add(nativeManaToShield, PileType.Hand);
            loop.RunUntilCompleted(addShield, TimeSpan.FromSeconds(30), "Add isolated Eleanor ManaToShield fixture");
            if (!addShield.GetAwaiter().GetResult().success)
                throw new InvalidOperationException("Game rejected the in-combat ManaToShield fixture.");
        }
        CardModel[] fullBattleStrikes = [];
        if (fullBattleScenario)
        {
            if (!completeOrbScenario || manaToShieldScenario)
                throw new InvalidOperationException("Full Eleanor battle fixture requires CompleteCrystalOrb without the power-install scenario.");
            CardModel strikeCanonical = ModelDb.All.OfType<CardModel>().Single(card => card.GetType().Name == "ManaStrike");
            int existingStrikes = playerState.Hand.Cards.Count(card => card.GetType().Name == "ManaStrike");
            if (existingStrikes == 0)
                throw new InvalidOperationException("Full Eleanor battle seed hand has no native ManaStrike card.");
            while (existingStrikes < 2)
            {
                CardModel extraStrike = combat.CreateCard(strikeCanonical, player);
                Task<CardPileAddResult> addStrike = CardPileCmd.Add(extraStrike, PileType.Hand);
                loop.RunUntilCompleted(addStrike, TimeSpan.FromSeconds(30), "Add isolated Eleanor full-battle ManaStrike fixture");
                if (!addStrike.GetAwaiter().GetResult().success)
                    throw new InvalidOperationException("Game rejected an in-combat ManaStrike battle fixture.");
                existingStrikes++;
            }
            CardModel[] firstTurnStrikes = playerState.Hand.Cards
                .Where(card => card.GetType().Name == "ManaStrike").Take(2).ToArray();
            if (firstTurnStrikes.Length != 2)
                throw new InvalidOperationException("Two first-turn ManaStrike cards could not be prepared.");
            List<CardModel> nextTurnStrikes = [];
            for (int index = 0; index < 2; index++)
            {
                CardModel drawStrike = combat.CreateCard(strikeCanonical, player);
                Task<CardPileAddResult> addDrawStrike = CardPileCmd.Add(drawStrike, PileType.Draw, CardPilePosition.Top);
                loop.RunUntilCompleted(addDrawStrike, TimeSpan.FromSeconds(30), "Add isolated next-turn ManaStrike fixture");
                if (!addDrawStrike.GetAwaiter().GetResult().success)
                    throw new InvalidOperationException("Game rejected an in-combat next-turn ManaStrike fixture.");
                nextTurnStrikes.Add(drawStrike);
            }
            fullBattleStrikes = firstTurnStrikes.Concat(nextTurnStrikes).ToArray();
        }
        if (!nativeCard.CanPlay())
            throw new InvalidOperationException("The Eleanor ManaGather fixture is not playable at the opening boundary.");

        var rootAdapter = new VanillaCombatRootAdapter(combat, "LAB_V0111_ELEANOR_M1_01");
        CombatRootSnapshot rootSnapshot = CombatRootSnapshot.Capture(combat);
        object rootRegistrationGuard = VerifyRegistrationChangeInvalidatesRoot(combat, rootSnapshot, mod);
        object rootPatchGuard = VerifyLatePatchInvalidatesRoot(combat);
        object turnStartRegistrationGuard = VerifyRelicTurnStartRegistrationInvalidatesRoot(combat, rootSnapshot);
        object turnStartPatchGuard = VerifyLateRelicTurnStartPatchRejected(combat, rootSnapshot);
        CombatPredictionSimulator simulator = rootSnapshot.ForkSimulator();
        SimulatedCombatState shadow = (SimulatedCombatState)simulator.State.CombatState;
        CombatRootV1 liveBeforeRoot = WithExtension(rootAdapter.CaptureLive(combat), opening);
        CombatRootV1 simulatedBeforeRoot = WithExtension(rootAdapter.CapturePredicted(
            combat, player, simulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber), opening);
        CaptureValidationV1 liveBeforeValidation = CombatCaptureValidator.ValidateExactReady(liveBeforeRoot);
        CaptureValidationV1 simulatedBeforeValidation = CombatCaptureValidator.ValidateExactReady(simulatedBeforeRoot);
        if (!liveBeforeValidation.IsExactReady || !simulatedBeforeValidation.IsExactReady)
            throw new InvalidOperationException("Eleanor root rejected: live=" + string.Join(";", liveBeforeValidation.Errors)
                + " predicted=" + string.Join(";", simulatedBeforeValidation.Errors));
        RequireEqual(StateDigest.Compute(liveBeforeRoot), StateDigest.Compute(simulatedBeforeRoot), "Eleanor opening root");
        bool extensionForkIsolation = VerifyDetachedFork(liveBeforeRoot);

        if (fullBattleScenario)
            return RunFullBattle(combat, loop, player, playerState, runtime, stateType, cardsField, reverseField,
                dualityPlayingField, rootAdapter, rootSnapshot, simulator, orbRelic, opening, fullBattleStrikes,
                liveBeforeRoot, rootPatchGuard, rootRegistrationGuard, turnStartPatchGuard,
                turnStartRegistrationGuard, extensionForkIsolation, outputDirectory);

        object? manaToShieldTransition = null;
        if (manaToShieldScenario)
        {
            if (nativeManaToShield == null || !nativeManaToShield.CanPlay())
                throw new InvalidOperationException("The native ManaToShield fixture is not playable at the opening boundary.");
            PredictedCard predictedShield = simulator.State.FindCard(nativeManaToShield)
                ?? throw new InvalidOperationException("Predicted MyMod state omitted the ManaToShield fixture.");
            SimulatedCombatState shieldShadow = (SimulatedCombatState)simulator.State.CombatState;
            string? shieldSimulationGap = null;
            bool shieldAccepted = false;
            try
            {
                if (!shieldShadow.CanPlayCard(simulator, predictedShield, out _, out _))
                    shieldSimulationGap = "CombatSolver does not consider ManaToShield playable on the complete-orb fixture.";
                else if (!simulator.ManualPlay(predictedShield, null, out _))
                    shieldSimulationGap = "CombatSolver rejected the MyMod ManaToShield action.";
                else
                {
                    shieldAccepted = true;
                    _ = simulator.CheckWinCondition(playerState.TurnNumber);
                }
            }
            catch (Exception error)
            {
                shieldSimulationGap = "CombatSolver ManaToShield mirror failed: " + Unwrap(error).GetType().Name + ": " + Unwrap(error).Message;
            }
            int shieldEnergyBefore = playerState.Energy;
            if (!nativeManaToShield.TryManualPlay(null))
                throw new InvalidOperationException("Native game refused the MyMod ManaToShield fixture.");
            loop.RunUntilCompleted(RunManager.Instance.ActionExecutor.FinishedExecutingActions(), TimeSpan.FromSeconds(30),
                "Lab native Eleanor ManaToShield action");
            RuntimeSnapshot shieldLiveAfter = CaptureRuntime(runtime, stateType, cardsField, reverseField, dualityPlayingField);
            EleanorManaSnapshot shieldPredictedState = shieldAccepted
                ? ReadPredictedState(simulator, orbRelic, opening)
                : new(opening.Mana, opening.Gauge, opening.Stance, opening.CompleteOrb, opening.StartDrawBonus);
            CombatRootV1 shieldLiveRoot = WithExtension(rootAdapter.CaptureLive(combat), shieldLiveAfter);
            CombatRootV1 shieldPredictedRoot = WithExtension(rootAdapter.CapturePredicted(
                combat, player, simulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber),
                SnapshotWithManaState(opening, shieldPredictedState));
            string shieldNativeContinuation = ContinuationStamp.CaptureLive(combat).StateText;
            string shieldPredictedContinuation = ContinuationStamp.CapturePredicted(
                player, simulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber).StateText;
            List<string> shieldDifferences = DescribeRootDifferences(shieldPredictedRoot, shieldLiveRoot);
            bool shieldContinuationMatches = StringComparer.Ordinal.Equals(shieldNativeContinuation, shieldPredictedContinuation);
            int liveShieldAmount = player.Creature.Powers.SingleOrDefault(power => power.GetType().Name == "ManaToShieldPower")?.Amount ?? 0;
            int simulatedShieldAmount = shieldShadow.EffectivePowers().SingleOrDefault(power =>
                power.GetType().Name == "ManaToShieldPower" && ReferenceEquals(power.Owner, player.Creature))?.Amount ?? 0;
            bool shieldExact = shieldAccepted && shieldDifferences.Count == 0 && shieldContinuationMatches
                && liveShieldAmount == 3 && simulatedShieldAmount == liveShieldAmount
                && shieldLiveAfter.ActiveCardTransactions == 0 && shieldLiveAfter.ActiveDualityReplays == 0;
            manaToShieldTransition = new
            {
                attempted = true,
                cardModelId = nativeManaToShield.Id.Entry,
                actualEnergyPaid = shieldEnergyBefore - playerState.Energy,
                liveAfter = shieldLiveAfter,
                livePowerAmount = liveShieldAmount,
                simulatedPowerAmount = simulatedShieldAmount,
                simulatedAccepted = shieldAccepted,
                simulationGap = shieldSimulationGap,
                rootDifferences = shieldDifferences,
                continuationMatches = shieldContinuationMatches,
                exactStateMatch = shieldExact,
            };
            if (!shieldExact)
                throw new InvalidOperationException("ManaToShield card native differential did not match its installed power.");
            opening = shieldLiveAfter;
            rootSnapshot = CombatRootSnapshot.Capture(combat);
            simulator = rootSnapshot.ForkSimulator();
        }

        PredictedCard predictedCard = simulator.State.FindCard(nativeCard)
            ?? throw new InvalidOperationException("Predicted MyMod state omitted the ManaGather fixture.");
        string? simulationGap = null;
        bool simulatedAccepted = false;
        int simulatedMana = opening.Mana;
        int simulatedGauge = opening.Gauge;
        try
        {
            if (!shadow.CanPlayCard(simulator, predictedCard, out _, out _))
                simulationGap = "CombatSolver does not consider the MyMod ManaGather playable from this root.";
            else if (!simulator.ManualPlay(predictedCard, null, out _))
                simulationGap = "CombatSolver rejected the MyMod ManaGather action.";
            else
            {
                simulatedAccepted = true;
                _ = simulator.CheckWinCondition(playerState.TurnNumber);
                EleanorManaSnapshot state = ReadPredictedState(simulator, orbRelic, opening);
                simulatedMana = state.Mana;
                simulatedGauge = state.Gauge;
            }
        }
        catch (Exception error)
        {
            simulationGap = "CombatSolver custom-card path failed: " + Unwrap(error).GetType().Name + ": " + Unwrap(error).Message;
        }

        int energyBefore = playerState.Energy;
        if (!nativeCard.TryManualPlay(null))
            throw new InvalidOperationException("Native game refused the MyMod ManaGather action.");
        loop.RunUntilCompleted(RunManager.Instance.ActionExecutor.FinishedExecutingActions(), TimeSpan.FromSeconds(30),
            "Lab native Eleanor ManaGather action");
        int energyPaid = energyBefore - playerState.Energy;
        RuntimeSnapshot liveAfter = CaptureRuntime(runtime, stateType, cardsField, reverseField, dualityPlayingField);
        CombatRootV1 liveAfterRoot = WithExtension(rootAdapter.CaptureLive(combat), liveAfter);
        CombatRootV1 predictedAfterRoot = WithExtension(rootAdapter.CapturePredicted(
            combat, player, simulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber),
            simulatedAccepted ? SnapshotWithManaState(opening, ReadPredictedState(simulator, orbRelic, opening)) : opening);

        string expectedManaAfterCard = simulatedAccepted ? simulatedMana.ToString() : "not-simulated";
        List<string> stateDifferences = DescribeRootDifferences(predictedAfterRoot, liveAfterRoot);
        string nativeContinuation = ContinuationStamp.CaptureLive(combat).StateText;
        string predictedContinuation = ContinuationStamp.CapturePredicted(
            player, simulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber).StateText;
        bool continuationMatches = StringComparer.Ordinal.Equals(nativeContinuation, predictedContinuation);
        bool exact = simulatedAccepted && stateDifferences.Count == 0 && continuationMatches
            && liveAfter.Mana == simulatedMana && liveAfter.Gauge == simulatedGauge
            && liveAfter.ActiveCardTransactions == 0 && liveAfter.ActiveDualityReplays == 0;

        object? crossTurnFirstTurnStrikes = null;
        RuntimeSnapshot liveBeforeStrike = liveAfter;
        object? manaBurstTransition = null;
        if (completeOrbScenario && !manaToShieldScenario)
        {
            if (!exact || nativeManaBurst == null)
                throw new InvalidOperationException("Complete-orb fixture needs an exact ManaGather baseline and native ManaBurst card.");
            ManaBurstProbeResult burst = RunManaBurstTransition(combat, loop, player, playerState, runtime,
                stateType, cardsField, reverseField, dualityPlayingField, rootAdapter, rootSnapshot, simulator,
                orbRelic, opening, nativeManaBurst, outputDirectory);
            liveBeforeStrike = burst.LiveAfter;
            manaBurstTransition = burst.Evidence;
        }

        if (crossTurnScenario)
        {
            if (!exact || liveBeforeStrike.Stance != "Mentor" || liveBeforeStrike.Gauge != 10)
                throw new InvalidOperationException("Cross-turn fixture requires the exact native/simulator Mentor boundary before its final first-turn attack.");
            CrossTurnSetupStrikeResult setup = RunCrossTurnSetupStrikes(combat, loop, player, playerState,
                runtime, stateType, cardsField, reverseField, dualityPlayingField, rootAdapter, rootSnapshot,
                simulator, orbRelic, opening, crossTurnSetupStrikes);
            liveBeforeStrike = setup.LiveAfter;
            crossTurnFirstTurnStrikes = setup.Evidence;
        }

        if (crossTurnScenario)
        {
            if (!exact || nativeManaBurst == null || liveBeforeStrike.Stance != "Mentor" || liveBeforeStrike.Gauge != 10)
                throw new InvalidOperationException("Mentor cross-turn fixture did not reach the native/simulator Mentor boundary.");
            return RunMentorCrossTurn(combat, loop, player, playerState, runtime, stateType, cardsField,
                reverseField, dualityPlayingField, rootAdapter, rootSnapshot, simulator, orbRelic, opening,
                liveBeforeStrike, crossTurnFirstTurnStrikes!, rootPatchGuard, rootRegistrationGuard,
                turnStartPatchGuard, turnStartRegistrationGuard, outputDirectory);
        }

        object? manaStrikeEvidence = null;
        CardModel? manaStrike = playerState.Hand.Cards.FirstOrDefault(card => card.GetType().Name == "ManaStrike");
        if (simulatedAccepted && manaStrike != null)
        {
            PredictedCard? predictedStrike = simulator.State.FindCard(manaStrike);
            Creature? target = combat.Enemies.FirstOrDefault(enemy => enemy.IsAlive);
            if (predictedStrike == null || target == null)
            {
                manaStrikeEvidence = new
                {
                    attempted = false,
                    gap = predictedStrike == null ? "CombatSolver lost the ManaStrike fixture after ManaGather." : "The ManaStrike follow-up had no living target or ManaState mirror."
                };
            }
            else
            {
                string strikeBeforeNative = ContinuationStamp.CaptureLive(combat).StateText;
                string strikeBeforePredicted = ContinuationStamp.CapturePredicted(
                    player, simulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber).StateText;
                RuntimeSnapshot strikeBeforeSnapshot = SnapshotWithManaState(opening,
                    ReadPredictedState(simulator, orbRelic, opening));
                CombatRootV1 strikeBeforeNativeRoot = WithExtension(rootAdapter.CaptureLive(combat), liveBeforeStrike);
                CombatRootV1 strikeBeforePredictedRoot = WithExtension(rootAdapter.CapturePredicted(
                    combat, player, simulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber),
                    strikeBeforeSnapshot);
                bool preActionRootMatches = StateDigest.Compute(strikeBeforeNativeRoot)
                    == StateDigest.Compute(strikeBeforePredictedRoot);
                bool preActionContinuationMatches = StringComparer.Ordinal.Equals(strikeBeforeNative, strikeBeforePredicted);
                int targetHpBefore = target.CurrentHp;

                CombatPredictionSimulator strikeSimulator = simulator.Fork();
                SimulatedCombatState strikeShadow = (SimulatedCombatState)strikeSimulator.State.CombatState;
                PredictedCard? branchStrike = strikeSimulator.State.FindCard(manaStrike);
                bool strikeAccepted = false;
                bool branchForkIsolation = false;
                string? strikeSimulationGap = null;
                try
                {
                    if (branchStrike == null)
                        strikeSimulationGap = "CombatSolver fork omitted the ManaStrike fixture.";
                    else if (!strikeShadow.CanPlayCard(strikeSimulator, branchStrike, out _, out _))
                        strikeSimulationGap = "CombatSolver does not consider the MyMod ManaStrike playable after ManaGather.";
                    else if (!strikeSimulator.ManualPlay(branchStrike, target, out _))
                        strikeSimulationGap = "CombatSolver rejected the MyMod ManaStrike action.";
                    else
                    {
                        strikeAccepted = true;
                        _ = strikeSimulator.CheckWinCondition(playerState.TurnNumber);
                        EleanorManaSnapshot branchState = ReadPredictedState(strikeSimulator, orbRelic, opening);
                        EleanorManaSnapshot parentState = ReadPredictedState(simulator, orbRelic, opening);
                        simulatedMana = branchState.Mana;
                        simulatedGauge = branchState.Gauge;
                        branchForkIsolation = parentState.Mana == liveBeforeStrike.Mana
                            && parentState.Gauge == liveBeforeStrike.Gauge
                            && (branchState.Mana != parentState.Mana || branchState.Gauge != parentState.Gauge);
                        if (!branchForkIsolation)
                            strikeSimulationGap = "Eleanor state did not deep-fork independently across sibling simulator branches.";
                    }
                }
                catch (Exception error)
                {
                    strikeSimulationGap = "CombatSolver custom-card path failed: " + Unwrap(error).GetType().Name + ": " + Unwrap(error).Message;
                }

                int strikeEnergyBefore = playerState.Energy;
                int nativeBlockBeforeStrike = player.Creature.Block;
                bool nativeStrikeAccepted = manaStrike.TryManualPlay(target);
                if (!nativeStrikeAccepted)
                    throw new InvalidOperationException("Native game refused the MyMod ManaStrike follow-up.");
                loop.RunUntilCompleted(RunManager.Instance.ActionExecutor.FinishedExecutingActions(), TimeSpan.FromSeconds(30),
                    "Lab native Eleanor ManaStrike action");
                RuntimeSnapshot strikeLiveAfter = CaptureRuntime(runtime, stateType, cardsField, reverseField, dualityPlayingField);
                CombatRootV1 strikeLiveRoot = WithExtension(rootAdapter.CaptureLive(combat), strikeLiveAfter);
                CombatRootV1 strikePredictedRoot = WithExtension(rootAdapter.CapturePredicted(
                    combat, player, strikeSimulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber),
                    strikeAccepted ? SnapshotWithManaState(opening, ReadPredictedState(strikeSimulator, orbRelic, opening)) : liveBeforeStrike);
                string strikeAfterNative = ContinuationStamp.CaptureLive(combat).StateText;
                string strikeAfterPredicted = ContinuationStamp.CapturePredicted(
                    player, strikeSimulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber).StateText;
                List<string> strikeDifferences = DescribeRootDifferences(strikePredictedRoot, strikeLiveRoot);
                bool strikeContinuationMatches = StringComparer.Ordinal.Equals(strikeAfterNative, strikeAfterPredicted);
                int nativeBlockAfterStrike = player.Creature.Block;
                int simulatedBlockAfterStrike = strikeSimulator.State.GetCreature(player.Creature).Block;
                int liveManaToShieldAmount = player.Creature.Powers.SingleOrDefault(power => power.GetType().Name == "ManaToShieldPower")?.Amount ?? 0;
                bool strikeExact = strikeAccepted && branchForkIsolation && strikeSimulationGap == null
                    && preActionRootMatches && preActionContinuationMatches
                    && strikeDifferences.Count == 0 && strikeContinuationMatches
                    && strikeLiveAfter.Mana == simulatedMana && strikeLiveAfter.Gauge == simulatedGauge
                    && strikeLiveAfter.ActiveCardTransactions == 0 && strikeLiveAfter.ActiveDualityReplays == 0;
                manaStrikeEvidence = new
                {
                    attempted = true,
                    cardModelId = manaStrike.Id.Entry,
                    targetCombatantId = target.CombatId.ToString(),
                    actualEnergyPaid = strikeEnergyBefore - playerState.Energy,
                    liveBefore = liveBeforeStrike,
                    liveAfter = strikeLiveAfter,
                    simulatedManaAfter = simulatedMana,
                    simulatedGaugeAfter = simulatedGauge,
                    simulatedAccepted = strikeAccepted,
                    simulationGap = strikeSimulationGap,
                    preActionRootMatches,
                    preActionContinuationMatches,
                    nativeBeforeSha256 = Sha(strikeBeforeNative),
                    predictedBeforeSha256 = Sha(strikeBeforePredicted),
                    nativeAfterSha256 = Sha(strikeAfterNative),
                    predictedAfterSha256 = Sha(strikeAfterPredicted),
                    rootDifferences = strikeDifferences,
                    continuationMatches = strikeContinuationMatches,
                    exactStateMatch = strikeExact,
                    sourceRuleOrder = new[] { "ManaRuntime.PayMana(1)", "DamageCmd.Attack(7).WithHitCount(2)", "ManaRuntime.FinishCard after both hits" },
                    liveObservedHitCount = strikeLiveAfter.Mana < liveBeforeStrike.Mana ? (targetHpBefore - target.CurrentHp) / Math.Max(1, ReadDynamicInt(manaStrike, "Damage")) : 0,
                    nativeBlockBeforeStrike,
                    nativeBlockAfterStrike,
                    simulatedBlockAfterStrike,
                    liveManaToShieldAmount,
                    nativeEnemyHpBefore = targetHpBefore,
                    nativeEnemyHpAfter = target.CurrentHp,
                    nativeEnemyHpDelta = targetHpBefore - target.CurrentHp,
                    nativeEnemyHp = target.CurrentHp,
                    branchForkIsolation,
                    gaps = strikeExact ? Array.Empty<string>() : new[]
                    {
                        strikeSimulationGap ?? "CombatSolver's generic custom-card path did not reproduce the two-hit ManaStrike effect and mana transaction.",
                        "This expected negative control covers only ManaStrike; it does not model stance upkeep, reverse Power ownership/removal, Duality replay, or an entire modded battle."
                    },
                };
            }
        }

        var report = new
        {
            schema = "eleanor-runtime-differential/v1",
            gameVersion = GameBuildPin.Version,
            gameCommit = GameBuildPin.Commit,
            mainAssemblyHash = GameBuildPin.MainAssemblyHash,
            modId = "MyMod",
            modVersion = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_VERSION"),
            manifestSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(EleanorModBootstrap.ManifestJson))).ToLowerInvariant(),
            sourceAssemblyFullName = mod.FullName,
            sourceAssemblyMvid = mod.ManifestModule.ModuleVersionId,
            modAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_DLL")!))).ToLowerInvariant(),
            characterModelId = player.Character.Id.Entry,
            encounterModelId = combat.Encounter?.Id.Entry,
            cardModelId = nativeCard.Id.Entry,
            cardTypeName = nativeCard.GetType().FullName,
            testMode = TestMode.IsOn,
            steamInitialized = SteamInitializer.Initialized,
            saveStore = saveStore.GetType().FullName,
            gameInstallWrites = false,
            registrationGuards = EleanorCardMirrors.GuardEvidence,
            rootPatchGuard,
            rootRegistrationGuard,
            opening = opening,
            liveAfter = liveAfter,
            completeOrbScenario,
            manaToShieldScenario,
            manaToShieldTransition,
            manaBurstTransition,
            simulatedManaAfter = expectedManaAfterCard,
            simulatedGaugeAfter = simulatedGauge,
            actualEnergyPaid = energyPaid,
            extensionForkIsolation,
            simulatedAccepted,
            simulationGap,
            nativeRootDigestBefore = StateDigest.Compute(liveBeforeRoot),
            predictedRootDigestBefore = StateDigest.Compute(simulatedBeforeRoot),
            nativeRootDigestAfter = StateDigest.Compute(liveAfterRoot),
            predictedRootDigestAfter = StateDigest.Compute(predictedAfterRoot),
            rootDifferences = stateDifferences,
            nativeContinuationSha256 = Sha(nativeContinuation),
            predictedContinuationSha256 = Sha(predictedContinuation),
            continuationMatches,
            exactStateMatch = exact,
            manaStrikeFollowUp = manaStrikeEvidence,
            gaps = exact ? Array.Empty<string>() : new[]
            {
                simulationGap ?? "The predicted combat state or mod extension differs from the native game action.",
                "This fixture covers only ManaGather; it does not model later stance upkeep, reverse Power ownership, removal, or Duality replay."
            },
        };
        File.WriteAllText(Path.Combine(outputDirectory, "eleanor-runtime-differential.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true }));
        return report;
    }

    private sealed record CrossTurnSetupStrikeResult(RuntimeSnapshot LiveAfter, object Evidence);

    private static CrossTurnSetupStrikeResult RunCrossTurnSetupStrikes(CombatState combat, MainLoopContext loop,
        Player player, PlayerCombatState playerState, object runtime, Type stateType, FieldInfo cardsField,
        FieldInfo reverseField, FieldInfo dualityPlayingField, VanillaCombatRootAdapter rootAdapter,
        CombatRootSnapshot rootSnapshot, CombatPredictionSimulator simulator, RelicModel orbRelic,
        RuntimeSnapshot opening, IReadOnlyList<CardModel> nativeStrikes)
    {
        if (nativeStrikes.Count != 1)
            throw new InvalidOperationException($"The fixed Mentor route can spend the remaining one energy on one first-turn ManaStrike, got {nativeStrikes.Count}.");
        List<object> steps = [];
        foreach (CardModel nativeCard in nativeStrikes)
        {
            Creature target = combat.Enemies.FirstOrDefault(enemy => enemy.IsAlive)
                ?? throw new InvalidOperationException("Cross-turn setup lost its living enemy target.");
            PredictedCard predictedCard = simulator.State.FindCard(nativeCard)
                ?? throw new InvalidOperationException("Cross-turn setup ManaStrike is missing from the simulator branch.");
            CombatRootV1 nativeBeforeRoot = WithExtension(rootAdapter.CaptureLive(combat),
                CaptureRuntime(runtime, stateType, cardsField, reverseField, dualityPlayingField));
            string nativeBeforeText = ContinuationStamp.CaptureLive(combat).StateText;
            EleanorManaSnapshot predictedBeforeState = ReadPredictedState(simulator, orbRelic, opening);
            CombatRootV1 predictedBeforeRoot = WithExtension(rootAdapter.CapturePredicted(
                combat, player, simulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber),
                SnapshotWithManaState(opening, predictedBeforeState));
            string predictedBeforeText = ContinuationStamp.CapturePredicted(
                player, simulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber).StateText;
            bool preActionMatches = StateDigest.Compute(nativeBeforeRoot) == StateDigest.Compute(predictedBeforeRoot)
                && StringComparer.Ordinal.Equals(nativeBeforeText, predictedBeforeText);
            if (!preActionMatches)
                throw new InvalidOperationException("Cross-turn setup ManaStrike did not start from an exact native/simulator boundary.");

            SimulatedCombatState shadow = (SimulatedCombatState)simulator.State.CombatState;
            if (!shadow.CanPlayCard(simulator, predictedCard, out _, out _)
                || !simulator.ManualPlay(predictedCard, target, out _))
                throw new PredictionUnsupportedException("The branch rejected a fixed cross-turn setup ManaStrike.");
            _ = simulator.CheckWinCondition(playerState.TurnNumber);
            int energyBefore = playerState.Energy;
            int enemyHpBefore = target.CurrentHp;
            if (!nativeCard.TryManualPlay(target))
                throw new InvalidOperationException("Native game refused a fixed cross-turn setup ManaStrike.");
            loop.RunUntilCompleted(RunManager.Instance.ActionExecutor.FinishedExecutingActions(), TimeSpan.FromSeconds(30),
                "Lab native Eleanor cross-turn setup ManaStrike");

            RuntimeSnapshot nativeAfter = CaptureRuntime(runtime, stateType, cardsField, reverseField, dualityPlayingField);
            EleanorManaSnapshot predictedAfterState = ReadPredictedState(simulator, orbRelic, opening);
            CombatRootV1 nativeAfterRoot = WithExtension(rootAdapter.CaptureLive(combat), nativeAfter);
            CombatRootV1 predictedAfterRoot = WithExtension(rootAdapter.CapturePredicted(
                combat, player, simulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber),
                SnapshotWithManaState(opening, predictedAfterState));
            string nativeAfterText = ContinuationStamp.CaptureLive(combat).StateText;
            string predictedAfterText = ContinuationStamp.CapturePredicted(
                player, simulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber).StateText;
            List<string> differences = DescribeRootDifferences(predictedAfterRoot, nativeAfterRoot);
            bool continuationMatches = StringComparer.Ordinal.Equals(nativeAfterText, predictedAfterText);
            bool exact = differences.Count == 0 && continuationMatches
                && predictedAfterState == new EleanorManaSnapshot(nativeAfter.Mana, nativeAfter.Gauge,
                    nativeAfter.Stance, nativeAfter.CompleteOrb, nativeAfter.StartDrawBonus);
            if (!exact)
                throw new PredictionUnsupportedException("A first-turn cross-turn setup ManaStrike diverged from the native game.");
            steps.Add(new
            {
                cardModelId = nativeCard.Id.Entry,
                energyPaid = energyBefore - playerState.Energy,
                nativeMana = nativeAfter.Mana,
                simulatedMana = predictedAfterState.Mana,
                nativeGauge = nativeAfter.Gauge,
                simulatedGauge = predictedAfterState.Gauge,
                nativeEnemyHpBefore = enemyHpBefore,
                nativeEnemyHpAfter = target.CurrentHp,
                exactStateMatch = exact,
                continuationMatches,
            });
        }

        RuntimeSnapshot liveAfter = CaptureRuntime(runtime, stateType, cardsField, reverseField, dualityPlayingField);
        return new(liveAfter, new
        {
            schema = "eleanor-cross-turn-first-turn-setup/v1",
            actions = steps,
            exactEveryAction = steps.Count == 1,
        });
    }

    private static ManaBurstProbeResult RunManaBurstTransition(CombatState combat, MainLoopContext loop,
        Player player, PlayerCombatState playerState, object runtime, Type stateType, FieldInfo cardsField,
        FieldInfo reverseField, FieldInfo dualityPlayingField, VanillaCombatRootAdapter rootAdapter,
        CombatRootSnapshot rootSnapshot, CombatPredictionSimulator simulator, RelicModel orbRelic,
        RuntimeSnapshot opening, CardModel nativeManaBurst, string outputDirectory)
    {
        PredictedCard predictedCard = simulator.State.FindCard(nativeManaBurst)
            ?? throw new InvalidOperationException("Predicted MyMod state omitted the ManaBurst fixture.");
        SimulatedCombatState shadow = (SimulatedCombatState)simulator.State.CombatState;
        RuntimeSnapshot beforeLive = CaptureRuntime(runtime, stateType, cardsField, reverseField, dualityPlayingField);
        EleanorManaSnapshot beforeState = ReadPredictedState(simulator, orbRelic, opening);
        string beforeNativeContinuation = ContinuationStamp.CaptureLive(combat).StateText;
        string beforePredictedContinuation = ContinuationStamp.CapturePredicted(
            player, simulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber).StateText;
        CombatRootV1 beforeNativeRoot = WithExtension(rootAdapter.CaptureLive(combat), beforeLive);
        CombatRootV1 beforePredictedRoot = WithExtension(rootAdapter.CapturePredicted(
            combat, player, simulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber),
            SnapshotWithManaState(opening, beforeState));
        bool preActionRootMatches = StateDigest.Compute(beforeNativeRoot) == StateDigest.Compute(beforePredictedRoot);
        bool preActionContinuationMatches = StringComparer.Ordinal.Equals(beforeNativeContinuation, beforePredictedContinuation);

        string? simulationGap = null;
        bool simulatedAccepted = false;
        try
        {
            if (!shadow.CanPlayCard(simulator, predictedCard, out _, out _))
                simulationGap = "CombatSolver does not consider ManaBurst playable after ManaGather on the complete-orb fixture.";
            else if (!simulator.ManualPlay(predictedCard, null, out _))
                simulationGap = "CombatSolver rejected the MyMod ManaBurst action.";
            else
            {
                simulatedAccepted = true;
                _ = simulator.CheckWinCondition(playerState.TurnNumber);
            }
        }
        catch (Exception error)
        {
            simulationGap = "CombatSolver ManaBurst mirror failed: " + Unwrap(error).GetType().Name + ": " + Unwrap(error).Message;
        }

        int energyBefore = playerState.Energy;
        bool nativeAccepted = nativeManaBurst.TryManualPlay(null);
        if (!nativeAccepted)
            throw new InvalidOperationException("Native game refused the MyMod ManaBurst complete-orb follow-up.");
        loop.RunUntilCompleted(RunManager.Instance.ActionExecutor.FinishedExecutingActions(), TimeSpan.FromSeconds(30),
            "Lab native Eleanor ManaBurst complete-orb action");
        RuntimeSnapshot liveAfter = CaptureRuntime(runtime, stateType, cardsField, reverseField, dualityPlayingField);
        EleanorManaSnapshot simulatedAfter = simulatedAccepted
            ? ReadPredictedState(simulator, orbRelic, opening)
            : beforeState;
        CombatRootV1 liveAfterRoot = WithExtension(rootAdapter.CaptureLive(combat), liveAfter);
        CombatRootV1 predictedAfterRoot = WithExtension(rootAdapter.CapturePredicted(
            combat, player, simulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber),
            SnapshotWithManaState(opening, simulatedAfter));
        string nativeContinuation = ContinuationStamp.CaptureLive(combat).StateText;
        string predictedContinuation = ContinuationStamp.CapturePredicted(
            player, simulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber).StateText;
        List<string> differences = DescribeRootDifferences(predictedAfterRoot, liveAfterRoot);
        bool continuationMatches = StringComparer.Ordinal.Equals(nativeContinuation, predictedContinuation);
        int energyPaid = energyBefore - playerState.Energy;
        bool exact = simulatedAccepted && preActionRootMatches && preActionContinuationMatches
            && differences.Count == 0 && continuationMatches && energyPaid == 2
            && liveAfter.Mana == simulatedAfter.Mana && liveAfter.Gauge == simulatedAfter.Gauge
            && liveAfter.Stance == "Mentor" && liveAfter.Gauge == 10 && liveAfter.CompleteOrb
            && liveAfter.ActiveCardTransactions == 0 && liveAfter.ActiveDualityReplays == 0
            && playerState.ExhaustPile.Cards.Contains(nativeManaBurst);
        object evidence = new
        {
            attempted = true,
            cardModelId = nativeManaBurst.Id.Entry,
            sourceRuleOrder = new[] { "EnergySpent is recorded at BeginCard", "ManaRuntime.GainMana(7)", "FinishCard settles gauge from energy plus gained mana" },
            opening = beforeLive,
            liveAfter,
            simulatedAfter,
            actualEnergyPaid = energyPaid,
            nativeExhausted = playerState.ExhaustPile.Cards.Contains(nativeManaBurst),
            simulatedAccepted,
            simulationGap,
            preActionRootMatches,
            preActionContinuationMatches,
            rootDifferences = differences,
            nativeContinuationSha256 = Sha(nativeContinuation),
            predictedContinuationSha256 = Sha(predictedContinuation),
            continuationMatches,
            exactStateMatch = exact,
            stanceEntered = liveAfter.Stance == "Mentor" && liveAfter.Gauge == 10,
            gaps = exact ? Array.Empty<string>() : new[] { simulationGap ?? "Native and predicted ManaBurst state diverged." },
        };
        File.WriteAllText(Path.Combine(outputDirectory, "eleanor-mana-burst-differential.json"),
            JsonSerializer.Serialize(evidence, new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true }));
        if (!exact)
            throw new InvalidOperationException("ManaBurst complete-orb native differential did not match; see eleanor-mana-burst-differential.json.");
        return new(liveAfter, evidence);
    }

    private static object RunMentorCrossTurn(CombatState combat, MainLoopContext loop, Player player,
        PlayerCombatState playerState, object runtime, Type stateType, FieldInfo cardsField,
        FieldInfo reverseField, FieldInfo dualityPlayingField, VanillaCombatRootAdapter rootAdapter,
        CombatRootSnapshot rootSnapshot, CombatPredictionSimulator simulator, RelicModel orbRelic,
        RuntimeSnapshot opening, RuntimeSnapshot nativeMentorBoundary, object firstTurnSetupEvidence,
        object rootPatchGuard,
        object rootRegistrationGuard, object turnStartPatchGuard, object turnStartRegistrationGuard,
        string outputDirectory)
    {
        int turn = playerState.TurnNumber;
        int nativeEnergyBefore = playerState.Energy;
        int beforeHandDrawCallsBefore = EleanorTurnStartMirrors.BeforeHandDrawCalls;
        int modifyHandDrawCallsBefore = EleanorTurnStartMirrors.ModifyHandDrawCalls;
        string parentBeforeText = ContinuationStamp.CapturePredicted(
            player, simulator, turn, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber).StateText;
        EleanorManaSnapshot parentBeforeState = ReadPredictedState(simulator, orbRelic, opening);
        CombatPredictionSimulator turnSimulator = simulator.Fork();
        SimulatedCombatState shadow = (SimulatedCombatState)turnSimulator.State.CombatState;
        EleanorManaSnapshot beforeState = ReadPredictedState(turnSimulator, orbRelic, opening);
        string nativeBeforeText = ContinuationStamp.CaptureLive(combat).StateText;
        string predictedBeforeText = ContinuationStamp.CapturePredicted(
            player, turnSimulator, turn, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber).StateText;
        CombatRootV1 nativeBeforeRoot = WithExtension(rootAdapter.CaptureLive(combat), nativeMentorBoundary);
        CombatRootV1 predictedBeforeRoot = WithExtension(rootAdapter.CapturePredicted(
            combat, player, turnSimulator, turn, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber),
            SnapshotWithManaState(opening, beforeState));
        bool preTurnRootMatches = StateDigest.Compute(nativeBeforeRoot) == StateDigest.Compute(predictedBeforeRoot);
        bool preTurnContinuationMatches = StringComparer.Ordinal.Equals(nativeBeforeText, predictedBeforeText);
        if (!preTurnRootMatches || !preTurnContinuationMatches)
            throw new InvalidOperationException("Mentor cross-turn route did not begin from an exact native/simulator root.");

        var driver = new CombatBeamSolver(
            rootSnapshot,
            SolverDisplayNames.Capture(combat),
            BattleDamageTracker.Observe(combat),
            SolverController.CaptureSearchPolicy(SolverSettings.Capture(), combat, false, null));
        MethodInfo advanceRound = typeof(CombatBeamSolver).GetMethod(
            "AdvanceRound", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(CombatBeamSolver), "AdvanceRound");
        bool simulatorReachedNextTurn = false;
        string? simulatorTransitionGap = null;
        try
        {
            object?[] arguments = [turnSimulator, shadow, turn - rootSnapshot.StartTurnNumber,
                new HashSet<uint>(), 0, null, null];
            object? boundary = advanceRound.Invoke(driver, arguments);
            if (!StringComparer.Ordinal.Equals(boundary?.ToString(), "None"))
                simulatorTransitionGap = "CombatSolver stopped at the cross-turn boundary: " + (boundary?.ToString() ?? "null");
            else
            {
                _ = shadow.ConsumePlayerTurnEndRequest();
                if (!CombatBeamSolver.SettleReplayActionBoundary(turnSimulator, shadow))
                    simulatorTransitionGap = "CombatSolver reached a pending choice while settling the simulated turn end.";
                else
                    simulatorReachedNextTurn = true;
            }
        }
        catch (Exception error)
        {
            simulatorTransitionGap = "CombatSolver cross-turn execution raised "
                + Unwrap(error).GetType().Name + ": " + Unwrap(error).Message;
        }

        int enemyHpBefore = combat.Enemies.Where(enemy => enemy.IsAlive).Sum(enemy => enemy.CurrentHp);
        CombatManager.Instance.OnEndedTurnLocally();
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new EndPlayerTurnAction(player, turn));
        loop.RunUntilCompleted(RunManager.Instance.ActionExecutor.FinishedExecutingActions(),
            TimeSpan.FromSeconds(30), "Lab native Eleanor Mentor upkeep and next player turn");
        if (player.PlayerCombatState is not { Phase: PlayerTurnPhase.Play } nextState || nextState.TurnNumber != turn + 1)
            throw new InvalidOperationException($"Native Eleanor EndTurn did not reach player decision phase {turn + 1}.");

        RuntimeSnapshot nativeAfter = CaptureRuntime(runtime, stateType, cardsField, reverseField, dualityPlayingField);
        EleanorManaSnapshot predictedAfterState = ReadPredictedState(turnSimulator, orbRelic, opening);
        CombatRootV1 nativeAfterRoot = WithExtension(rootAdapter.CaptureLive(combat), nativeAfter);
        CombatRootV1 predictedAfterRoot = WithExtension(rootAdapter.CapturePredicted(
            combat, player, turnSimulator, turn + 1, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber),
            SnapshotWithManaState(opening, predictedAfterState));
        string nativeAfterText = ContinuationStamp.CaptureLive(combat).StateText;
        string predictedAfterText = ContinuationStamp.CapturePredicted(
            player, turnSimulator, turn + 1, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber).StateText;
        List<string> rootDifferences = DescribeRootDifferences(predictedAfterRoot, nativeAfterRoot);
        List<string> continuationDifferences = new ContinuationStamp(predictedAfterText).DescribeDifferences(
            new ContinuationStamp(nativeAfterText)).ToList();
        bool continuationMatches = StringComparer.Ordinal.Equals(nativeAfterText, predictedAfterText);
        bool exact = simulatorReachedNextTurn && rootDifferences.Count == 0 && continuationMatches
            && predictedAfterState == new EleanorManaSnapshot(nativeAfter.Mana, nativeAfter.Gauge,
                nativeAfter.Stance, nativeAfter.CompleteOrb, nativeAfter.StartDrawBonus);
        string parentAfterText = ContinuationStamp.CapturePredicted(
            player, simulator, turn, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber).StateText;
        EleanorManaSnapshot parentAfterState = ReadPredictedState(simulator, orbRelic, opening);
        bool parentBranchStateUnchanged = StringComparer.Ordinal.Equals(parentBeforeText, parentAfterText)
            && parentBeforeState == parentAfterState;
        exact &= parentBranchStateUnchanged;
        int enemyHpAfter = combat.Enemies.Where(enemy => enemy.IsAlive).Sum(enemy => enemy.CurrentHp);
        bool victory = combat.Enemies.All(enemy => !enemy.IsAlive);
        object evidence = new
        {
            schema = "eleanor-mentor-cross-turn-differential/v1",
            status = exact ? "passed" : "unsupported",
            unsupportedReason = exact ? null : simulatorTransitionGap
                ?? "The branch-local CompleteCrystalOrb Mentor turn-start state, draw, root, or continuation did not match the native turn.",
            gameVersion = GameBuildPin.Version,
            gameCommit = GameBuildPin.Commit,
            modVersion = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_VERSION"),
            testMode = TestMode.IsOn,
            steamInitialized = SteamInitializer.Initialized,
            saveStore = SaveManager.Instance.GetType().GetField("_saveStore", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(SaveManager.Instance)?.GetType().FullName,
            persistentSaveWrites = false,
            progressSaveCallsSuppressed = 0,
            replayWritesSuppressed = 0,
            rootPatchGuard,
            rootRegistrationGuard,
            turnStartPatchGuard,
            turnStartRegistrationGuard,
            preTurnRootMatches,
            preTurnContinuationMatches,
            firstTurnSetup = firstTurnSetupEvidence,
            simulatorReachedNextTurn,
            simulatorTransitionGap,
            turnStartCallbackDiagnostics = new
            {
                beforeHandDrawCallsBefore,
                beforeHandDrawCallsAfter = EleanorTurnStartMirrors.BeforeHandDrawCalls,
                modifyHandDrawCallsBefore,
                modifyHandDrawCallsAfter = EleanorTurnStartMirrors.ModifyHandDrawCalls,
                lastBeforeHandDrawEnergy = EleanorTurnStartMirrors.LastBeforeHandDrawEnergy,
                lastBeforeHandDrawRelic = EleanorTurnStartMirrors.LastBeforeHandDrawRelic,
                lastBeforeHandDrawDetail = EleanorTurnStartMirrors.LastBeforeHandDrawDetail,
                simulatedRelics = shadow.RelicsOf(player).Select(relic => relic.GetType().FullName).ToArray(),
            },
            branchForkIsolation = parentBranchStateUnchanged,
            parentBranchStateUnchanged,
            parentTurnAfterBranch = turn,
            nativeTransitionReachedNextTurn = true,
            nativeBefore = new { turn, energy = nativeEnergyBefore, nativeMentorBoundary.Mana,
                nativeMentorBoundary.Gauge, nativeMentorBoundary.Stance, nativeMentorBoundary.StartDrawBonus },
            nativeAfter = new { turn = nextState.TurnNumber, energy = nextState.Energy, nativeAfter.Mana,
                nativeAfter.Gauge, nativeAfter.Stance, nativeAfter.StartDrawBonus,
                handCount = nextState.Hand.Cards.Count, drawPileCount = nextState.DrawPile.Cards.Count },
            simulatedAfter = new { turn = turn + 1,
                energy = turnSimulator.State.GetPlayerCombatState(player).Energy,
                predictedAfterState.Mana, predictedAfterState.Gauge, predictedAfterState.Stance,
                predictedAfterState.StartDrawBonus,
                handCount = turnSimulator.State.GetPlayerCombatState(player).Hand.Cards.Count,
                drawPileCount = turnSimulator.State.GetPlayerCombatState(player).DrawPile.Cards.Count },
            nativeEnemyHpBefore = enemyHpBefore,
            nativeEnemyHpAfter = enemyHpAfter,
            nativeVictory = victory,
            simulatedVictory = turnSimulator.TerminalStamp?.Outcome.ToString(),
            nativeAfterRootDigest = StateDigest.Compute(nativeAfterRoot),
            predictedAfterRootDigest = StateDigest.Compute(predictedAfterRoot),
            rootDifferences,
            nativeContinuationSha256 = Sha(nativeAfterText),
            predictedContinuationSha256 = Sha(predictedAfterText),
            continuationMatches,
            continuationDifferences = continuationDifferences.Take(24).ToArray(),
            supportedTurnStartScope = new[]
            {
                "CompleteCrystalOrb + Mentor upkeep on an isolated fork",
                "Mentor orb income and turn-start draw bonus from the actual ManaState",
            },
            missingAdapters = new[]
            {
                "ReversePowerLedger and native Strength/Dexterity removal/restoration events have no forked simulator state adapter.",
                "Reverse stance upkeep success, failure, and HP loss remain unsupported; this route never forced Reverse through a debug command."
            },
            reverseUpkeepStatus = "unsupported",
            reversePowerOwnershipRemovalRestoreStatus = "unsupported",
        };
        File.WriteAllText(Path.Combine(outputDirectory, "eleanor-mentor-cross-turn-differential.json"),
            JsonSerializer.Serialize(evidence, new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true }));
        return new
        {
            schema = "eleanor-cross-turn-probe/v1",
            crossTurn = evidence,
            registrationGuards = EleanorCardMirrors.GuardEvidence,
        };
    }

    private static object VerifyLatePatchInvalidatesRoot(CombatState combat)
    {
        Type cardType = AppDomain.CurrentDomain.GetAssemblies().Single(assembly => assembly.GetName().Name == "MyMod")
            .GetType("MyMod.MyModCode.Cards.ManaGather", throwOnError: true)!;
        MethodInfo target = AccessTools.Method(cardType, "OnPlay",
                [typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext), typeof(CardPlay)])
            ?? throw new MissingMethodException(cardType.FullName, "OnPlay");
        string before = ContinuationStamp.CaptureLive(combat).StateText;
        string? rejectedType = null;
        string? rejectedMessage = null;
        LatePatchProbe.Patch(target, prefix: new HarmonyMethod(
            typeof(EleanorCombatProbe).GetMethod(nameof(LatePatchPrefix), BindingFlags.Static | BindingFlags.NonPublic)!));
        string patched;
        try
        {
            patched = ContinuationStamp.CaptureLive(combat).StateText;
            try
            {
                _ = CombatRootSnapshot.Capture(combat);
            }
            catch (Exception error) when (error.GetType().Name is "PredictionUnsupportedException" or "IncompatibleGameplayModException")
            {
                rejectedType = error.GetType().Name;
                rejectedMessage = error.Message;
            }
        }
        finally
        {
            LatePatchProbe.UnpatchAll(LatePatchOwner);
        }
        string restored = ContinuationStamp.CaptureLive(combat).StateText;
        bool stampChanged = !StringComparer.Ordinal.Equals(before, patched);
        bool stampRestored = StringComparer.Ordinal.Equals(before, restored);
        bool rootRejected = rejectedType != null;
        if (!stampChanged || !stampRestored || !rootRejected)
            throw new InvalidOperationException($"Late Harmony patch guard failed: changed={stampChanged}, restored={stampRestored}, rejected={rootRejected}.");
        return new
        {
            scenario = "registered ManaGather receives a temporary post-registration Harmony prefix",
            patchAlwaysRemoved = true,
            stampChanged,
            stampRestored,
            rootCaptureRejected = rootRejected,
            rejectionType = rejectedType,
            rejectionMessage = rejectedMessage,
            beforeSha256 = Sha(before),
            patchedSha256 = Sha(patched),
            restoredSha256 = Sha(restored),
        };
    }

    private static object VerifyRelicTurnStartRegistrationInvalidatesRoot(
        CombatState combat, CombatRootSnapshot rootSnapshot)
    {
        Type registryType = typeof(CombatRootSnapshot).Assembly.GetType(
            "CombatSolver.AdaptedRelicBeforeHandDrawMirrors", throwOnError: true)!;
        FieldInfo registrationsField = registryType.GetField("Registrations", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(registryType.FullName, "Registrations");
        IDictionary registrations = (IDictionary)(registrationsField.GetValue(null)
            ?? throw new InvalidOperationException("Relic turn-start registration table is missing."));
        Type relicType = registrations.Keys.Cast<Type>().Single(type => type.Name == "CompleteCrystalOrb");
        object originalRegistration = registrations[relicType]
            ?? throw new InvalidOperationException("CompleteCrystalOrb has no registered turn-start descriptor.");
        Type registrationType = originalRegistration.GetType();
        BindingFlags members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Read(string name) => registrationType.GetProperty(name, members)?.GetValue(originalRegistration)
            ?? throw new MissingMemberException(registrationType.FullName, name);
        ConstructorInfo constructor = registrationType.GetConstructors(members)
            .Single(candidate => candidate.GetParameters().Length == 9);

        string capturedStamp = rootSnapshot.ContinuationStamp.StateText;
        string beforeStamp = ContinuationStamp.CaptureLive(combat).StateText;
        bool rootMatchesBefore = StringComparer.Ordinal.Equals(capturedStamp, beforeStamp);
        bool lateRegistrationRejected = EleanorTurnStartMirrors.RejectLateRegistration();
        string afterRejectedStamp = ContinuationStamp.CaptureLive(combat).StateText;
        bool rejectedRegistrationStampStable = StringComparer.Ordinal.Equals(beforeStamp, afterRejectedStamp);

        string schema = (string)Read("Schema");
        object?[] values =
        [
            Read("RelicType"), schema + "/lab-negative-mutation", Read("BeforeTarget"), Read("ModifyTarget"),
            Read("SourceIdentity"), Read("BeforeImplementation"), Read("ModifyImplementation"),
            Read("BeforeHandler"), Read("ModifyHandler"),
        ];
        string? mutatedStamp = null;
        string? restoredStamp = null;
        bool capturedRootStaleAfterRegistryMutation = false;
        bool stampRestored = false;
        try
        {
            registrations[relicType] = constructor.Invoke(values);
            mutatedStamp = ContinuationStamp.CaptureLive(combat).StateText;
            capturedRootStaleAfterRegistryMutation = !StringComparer.Ordinal.Equals(capturedStamp, mutatedStamp);
        }
        finally
        {
            registrations[relicType] = originalRegistration;
            restoredStamp = ContinuationStamp.CaptureLive(combat).StateText;
            stampRestored = StringComparer.Ordinal.Equals(beforeStamp, restoredStamp);
        }

        bool registryRestored = ReferenceEquals(registrations[relicType], originalRegistration);
        if (!rootMatchesBefore || !lateRegistrationRejected || !rejectedRegistrationStampStable
            || !capturedRootStaleAfterRegistryMutation || !stampRestored || !registryRestored)
            throw new InvalidOperationException("Relic turn-start registry stability guard failed: "
                + $"rootMatches={rootMatchesBefore}, lateRegistrationRejected={lateRegistrationRejected}, "
                + $"rejectedStampStable={rejectedRegistrationStampStable}, "
                + $"registryMutationInvalidatedCapturedRoot={capturedRootStaleAfterRegistryMutation}, "
                + $"restored={stampRestored}.");

        return new
        {
            scenario = "late CompleteCrystalOrb adapter registration rejection plus isolated descriptor mutation",
            lateRegistrationRejected,
            rejectedRegistrationStampStable,
            capturedRootStaleAfterRegistryMutation,
            stampRestored,
            registryRestored,
            capturedRootSha256 = Sha(capturedStamp),
            mutatedSha256 = Sha(mutatedStamp!),
            restoredSha256 = Sha(restoredStamp!),
        };
    }

    private static object VerifyLateRelicTurnStartPatchRejected(CombatState combat,
        CombatRootSnapshot rootSnapshot)
    {
        string beforeStamp = ContinuationStamp.CaptureLive(combat).StateText;
        string? rejectionType = null;
        string? rejectionMessage = null;
        bool syntheticPatchMetadataInjected = false;
        using (EleanorTurnStartMirrors.InjectHarmonyPatchMetadata(
            EleanorTurnStartMirrors.RegisteredModifyTarget, "simulatorlab.eleanor.turn-start-late-patch-guard"))
        {
            syntheticPatchMetadataInjected = true;
            try
            {
                _ = CombatRootSnapshot.Capture(combat);
            }
            catch (Exception error) when (error.GetType().Name is "PredictionUnsupportedException" or "IncompatibleGameplayModException")
            {
                rejectionType = error.GetType().Name;
                rejectionMessage = error.Message;
            }
        }
        string restoredStamp = ContinuationStamp.CaptureLive(combat).StateText;
        bool rootCaptureRejected = rejectionType is not null;
        bool patchMetadataRestored = StringComparer.Ordinal.Equals(beforeStamp, restoredStamp);
        bool capturedRootMatchesAfterRestore = StringComparer.Ordinal.Equals(
            rootSnapshot.ContinuationStamp.StateText, restoredStamp);
        if (!syntheticPatchMetadataInjected || !rootCaptureRejected || !patchMetadataRestored || !capturedRootMatchesAfterRestore)
            throw new InvalidOperationException("Late CompleteCrystalOrb hook patch guard failed: "
                + $"metadataInjected={syntheticPatchMetadataInjected}, rejected={rootCaptureRejected}, "
                + $"metadataRestored={patchMetadataRestored}, rootRestored={capturedRootMatchesAfterRestore}.");
        return new
        {
            scenario = "isolated Harmony PatchInfo metadata injection on registered CompleteCrystalOrb.ModifyHandDraw",
            patchMetadataControl = "synthetic metadata only; Harmony refused runtime weaving on these RelicModel methods",
            syntheticPatchMetadataInjected,
            patchMetadataRestored,
            rootCaptureRejected,
            rejectionType,
            rejectionMessage,
            capturedRootMatchesAfterRestore,
            beforeSha256 = Sha(beforeStamp),
            restoredSha256 = Sha(restoredStamp),
        };
    }

    private static object VerifyRegistrationChangeInvalidatesRoot(
        CombatState combat, CombatRootSnapshot rootSnapshot, Assembly mod)
    {
        Type registryType = typeof(CombatRootSnapshot).Assembly.GetType(
            "CombatSolver.AdaptedCardOnPlayMirrors", throwOnError: true)!;
        FieldInfo registrationsField = registryType.GetField("Registrations", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(registryType.FullName, "Registrations");
        IDictionary registrations = (IDictionary)(registrationsField.GetValue(null)
            ?? throw new InvalidOperationException("No-patch registration table is missing."));
        Type cardType = registrations.Keys.Cast<Type>()
            .OrderBy(type => type.FullName, StringComparer.Ordinal).First();
        object originalRegistration = registrations[cardType]
            ?? throw new InvalidOperationException("A registered no-patch entry has no descriptor.");
        Type registrationType = originalRegistration.GetType();
        BindingFlags properties = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Read(string name) => registrationType.GetProperty(name, properties)?.GetValue(originalRegistration)
            ?? throw new MissingMemberException(registrationType.FullName, name);

        MethodInfo target = (MethodInfo)Read("Target");
        string schema = (string)Read("Schema");
        string signature = (string)Read("Signature");
        string implementation = (string)Read("Implementation");
        string sourceIdentity = (string)Read("SourceIdentity");
        bool requiresPatchComposition = (bool)Read("RequiresPatchComposition");
        object mirror = Read("Mirror");
        ConstructorInfo constructor = registrationType.GetConstructors(properties)
            .Single(candidate => candidate.GetParameters().Length == 7);

        string rootStamp = rootSnapshot.ContinuationStamp.StateText;
        string beforeStamp = ContinuationStamp.CaptureLive(combat).StateText;
        bool matchesCapturedRootBefore = StringComparer.Ordinal.Equals(rootStamp, beforeStamp);
        MethodInfo register = registryType.GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(method => method.Name == "RegisterUnpatched" && method.IsGenericMethodDefinition)
            .MakeGenericMethod(cardType);
        Type handlerType = typeof(Action<,>).MakeGenericType(cardType, typeof(CardOnPlayMirrorContext));
        MethodInfo noOpMethod = typeof(EleanorCombatProbe).GetMethod(
            nameof(NoOpRegistrationHandler), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(cardType);
        Delegate handler = noOpMethod.CreateDelegate(handlerType);
        bool supportedRegistrationRejected = false;
        try
        {
            register.Invoke(null, ["late-registration-guard/v1", target, mod,
                EleanorModBootstrap.ManifestJson, handler]);
        }
        catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException inner
            && inner.Message.Contains("registration is closed after first capture", StringComparison.Ordinal))
        {
            supportedRegistrationRejected = true;
        }
        string afterRejectedRegistration = ContinuationStamp.CaptureLive(combat).StateText;
        bool stampUnchangedAfterRejectedRegistration = StringComparer.Ordinal.Equals(beforeStamp, afterRejectedRegistration);

        string? mutatedStamp = null;
        string? restoredStamp = null;
        bool registrationMutationMadeCapturedRootStale = false;
        bool stampRestored = false;
        try
        {
            object alteredRegistration = constructor.Invoke(new object?[]
            {
                target, schema, signature + "/lab-negative-mutation", implementation,
                sourceIdentity, requiresPatchComposition, mirror });
            registrations[cardType] = alteredRegistration;
            mutatedStamp = ContinuationStamp.CaptureLive(combat).StateText;
            registrationMutationMadeCapturedRootStale = !StringComparer.Ordinal.Equals(rootStamp, mutatedStamp);
        }
        finally
        {
            registrations[cardType] = originalRegistration;
            restoredStamp = ContinuationStamp.CaptureLive(combat).StateText;
            stampRestored = StringComparer.Ordinal.Equals(beforeStamp, restoredStamp);
        }

        bool registryRestored = ReferenceEquals(registrations[cardType], originalRegistration);
        if (!matchesCapturedRootBefore || !supportedRegistrationRejected || !stampUnchangedAfterRejectedRegistration
            || !registrationMutationMadeCapturedRootStale || !stampRestored || !registryRestored)
            throw new InvalidOperationException(
                $"Registration/root stability guard failed: initialRoot={matchesCapturedRootBefore}, " +
                $"lateRegistrationRejected={supportedRegistrationRejected}, " +
                $"rejectedStampStable={stampUnchangedAfterRejectedRegistration}, " +
                $"mutationInvalidatedRoot={registrationMutationMadeCapturedRootStale}, restored={stampRestored}.");

        return new
        {
            scenario = "late registration rejection plus isolated registration-signature fault injection",
            supportedRegistrationRejected,
            stampUnchangedAfterRejectedRegistration,
            registrationMutationMadeCapturedRootStale,
            stampRestored,
            rootBeforeSha256 = Sha(rootStamp),
            mutatedSha256 = Sha(mutatedStamp!),
            restoredSha256 = Sha(restoredStamp!),
            registryRestored,
        };
    }

    private static void NoOpRegistrationHandler<TCard>(TCard card, CardOnPlayMirrorContext context)
        where TCard : CardModel { }

    private static void LatePatchPrefix() { }

    private sealed record FullBattleStep(int Sequence, string CardModelId, int Turn,
        string NativeBeforeDigest, string PredictedBeforeDigest, string NativeAfterDigest,
        string PredictedAfterDigest, string NativeAfterContinuationSha256,
        string PredictedAfterContinuationSha256, int EnemyHpBefore, int EnemyHpAfter, bool ExactMatch);

    private static object RunFullBattle(CombatState combat, MainLoopContext loop, Player player,
        PlayerCombatState playerState, object runtime, Type stateType, FieldInfo cardsField,
        FieldInfo reverseField, FieldInfo dualityPlayingField, VanillaCombatRootAdapter rootAdapter,
        CombatRootSnapshot rootSnapshot, CombatPredictionSimulator simulator, RelicModel orbRelic,
        RuntimeSnapshot opening, CardModel[] nativeStrikes, CombatRootV1 openingRoot,
        object rootPatchGuard, object rootRegistrationGuard, object turnStartPatchGuard,
        object turnStartRegistrationGuard, bool extensionForkIsolation, string outputDirectory)
    {
        if (nativeStrikes.Length != 4 || playerState.Energy != 4 || opening.Mana != 7 || !opening.CompleteOrb)
            throw new InvalidOperationException($"Full battle root mismatch: strikes={nativeStrikes.Length}, energy={playerState.Energy}, mana={opening.Mana}, complete={opening.CompleteOrb}.");
        string parentBeforeText = ContinuationStamp.CapturePredicted(
            player, simulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber).StateText;
        EleanorManaSnapshot parentBeforeState = ReadPredictedState(simulator, orbRelic, opening);
        CombatPredictionSimulator battleSimulator = simulator.Fork();
        SimulatedCombatState shadow = (SimulatedCombatState)battleSimulator.State.CombatState;
        List<FullBattleStep> steps = [];
        object? turnBoundaryEvidence = null;
        InstallFullBattleTerminalCapture(combat, rootAdapter, runtime, stateType, cardsField, reverseField, dualityPlayingField);
        try
        {
            Creature target = combat.Enemies.Single(enemy => enemy.IsAlive);
            for (int index = 0; index < nativeStrikes.Length; index++)
            {
                if (index == 2)
                {
                    int endingTurn = playerState.TurnNumber;
                    string nativeBoundaryBefore = ContinuationStamp.CaptureLive(combat).StateText;
                    string predictedBoundaryBefore = ContinuationStamp.CapturePredicted(
                        player, battleSimulator, endingTurn, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber).StateText;
                    if (!StringComparer.Ordinal.Equals(nativeBoundaryBefore, predictedBoundaryBefore))
                        throw new InvalidOperationException("Two-turn Eleanor fixture reached a divergent boundary before ending turn one.");

                    var turnDriver = new CombatBeamSolver(rootSnapshot,
                        SolverDisplayNames.Capture(combat), BattleDamageTracker.Observe(combat),
                        SolverController.CaptureSearchPolicy(SolverSettings.Capture(), combat, false, null));
                    MethodInfo advanceRound = typeof(CombatBeamSolver).GetMethod(
                        "AdvanceRound", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?? throw new MissingMethodException(nameof(CombatBeamSolver), "AdvanceRound");
                    object?[] advanceArguments = [battleSimulator, shadow,
                        endingTurn - rootSnapshot.StartTurnNumber, new HashSet<uint>(), 0, null, null];
                    object? boundary = advanceRound.Invoke(turnDriver, advanceArguments);
                    if (!StringComparer.Ordinal.Equals(boundary?.ToString(), "None"))
                        throw new PredictionUnsupportedException("CombatSolver stopped before the fixed next-turn boundary: "
                            + (boundary?.ToString() ?? "null"));
                    _ = shadow.ConsumePlayerTurnEndRequest();
                    if (!CombatBeamSolver.SettleReplayActionBoundary(battleSimulator, shadow))
                        throw new PredictionUnsupportedException("CombatSolver left a pending choice at the fixed next-turn boundary.");

                    CombatManager.Instance.OnEndedTurnLocally();
                    RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new EndPlayerTurnAction(player, endingTurn));
                    loop.RunUntilCompleted(RunManager.Instance.ActionExecutor.FinishedExecutingActions(),
                        TimeSpan.FromSeconds(30), "Lab Eleanor two-turn Victory turn boundary");
                    if (player.PlayerCombatState is not { Phase: PlayerTurnPhase.Play } nextState
                        || nextState.TurnNumber != endingTurn + 1)
                        throw new InvalidOperationException("Native Eleanor battle did not reach its second player turn.");

                    string nativeBoundaryAfter = ContinuationStamp.CaptureLive(combat).StateText;
                    string predictedBoundaryAfter = ContinuationStamp.CapturePredicted(
                        player, battleSimulator, nextState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber).StateText;
                    RuntimeSnapshot nativeTurnState = CaptureRuntime(runtime, stateType, cardsField, reverseField, dualityPlayingField);
                    EleanorManaSnapshot predictedTurnState = ReadPredictedState(battleSimulator, orbRelic, opening);
                    CombatRootV1 nativeTurnRoot = WithExtension(rootAdapter.CaptureLive(combat), nativeTurnState);
                    CombatRootV1 predictedTurnRoot = WithExtension(rootAdapter.CapturePredicted(
                        combat, player, battleSimulator, nextState.TurnNumber,
                        rootSnapshot.Forecast, rootSnapshot.StartTurnNumber),
                        SnapshotWithManaState(opening, predictedTurnState));
                    List<string> boundaryDifferences = DescribeRootDifferences(predictedTurnRoot, nativeTurnRoot);
                    bool boundaryExact = boundaryDifferences.Count == 0
                        && StringComparer.Ordinal.Equals(nativeBoundaryAfter, predictedBoundaryAfter)
                        && predictedTurnState == new EleanorManaSnapshot(nativeTurnState.Mana, nativeTurnState.Gauge,
                            nativeTurnState.Stance, nativeTurnState.CompleteOrb, nativeTurnState.StartDrawBonus);
                    bool secondTurnCardsPresent = nativeStrikes.Skip(2).All(card => nextState.Hand.Cards.Contains(card))
                        && nativeStrikes.Skip(2).All(card => battleSimulator.State.FindCard(card) is not null);
                    turnBoundaryEvidence = new
                    {
                        fromTurn = endingTurn,
                        toTurn = nextState.TurnNumber,
                        nativeEnergy = nextState.Energy,
                        simulatorEnergy = battleSimulator.State.GetPlayerCombatState(player).Energy,
                        nativeMana = nativeTurnState.Mana,
                        simulatedMana = predictedTurnState.Mana,
                        nativeDrawBonus = nativeTurnState.StartDrawBonus,
                        simulatedDrawBonus = predictedTurnState.StartDrawBonus,
                        nativeHandCount = nextState.Hand.Cards.Count,
                        simulatedHandCount = battleSimulator.State.GetPlayerCombatState(player).Hand.Cards.Count,
                        secondTurnCardsPresent,
                        boundaryDifferences,
                        continuationMatches = StringComparer.Ordinal.Equals(nativeBoundaryAfter, predictedBoundaryAfter),
                        exactStateMatch = boundaryExact,
                    };
                    if (!boundaryExact || !secondTurnCardsPresent)
                        throw new PredictionUnsupportedException("Native and simulator did not match at the two-turn Victory boundary or the fixed draw cards were absent.");
                }
                CardModel nativeCard = nativeStrikes[index];
                if (index >= 2 && !playerState.Hand.Cards.Contains(nativeCard))
                    throw new PredictionUnsupportedException($"Fixed next-turn ManaStrike #{index - 1} was not in the native hand after the real draw.");
                PredictedCard predictedCard = battleSimulator.State.FindCard(nativeCard)
                    ?? throw new InvalidOperationException($"Predicted state omitted full-battle ManaStrike #{index + 1}.");
                string nativeBeforeText = ContinuationStamp.CaptureLive(combat).StateText;
                string predictedBeforeText = ContinuationStamp.CapturePredicted(
                    player, battleSimulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber).StateText;
                CombatRootV1 nativeBeforeRoot = WithExtension(rootAdapter.CaptureLive(combat),
                    CaptureRuntime(runtime, stateType, cardsField, reverseField, dualityPlayingField));
                CombatRootV1 predictedBeforeRoot = WithExtension(rootAdapter.CapturePredicted(
                    combat, player, battleSimulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber),
                    SnapshotWithManaState(opening, ReadPredictedState(battleSimulator, orbRelic, opening)));
                RequireExactFullBattleRoot(nativeBeforeRoot, $"native before ManaStrike #{index + 1}");
                RequireExactFullBattleRoot(predictedBeforeRoot, $"predicted before ManaStrike #{index + 1}");
                string nativeBeforeDigest = StateDigest.Compute(nativeBeforeRoot);
                string predictedBeforeDigest = StateDigest.Compute(predictedBeforeRoot);
                RequireEqual(nativeBeforeDigest, predictedBeforeDigest, $"Eleanor full-battle pre-action root #{index + 1}");
                RequireEqual(nativeBeforeText, predictedBeforeText, $"Eleanor full-battle pre-action continuation #{index + 1}");
                if (!shadow.CanPlayCard(battleSimulator, predictedCard, out _, out _))
                    throw new InvalidOperationException($"Simulator refused full-battle ManaStrike #{index + 1}.");
                if (!battleSimulator.ManualPlay(predictedCard, target, out _))
                    throw new InvalidOperationException($"Simulator rejected full-battle ManaStrike #{index + 1}.");
                battleSimulator.CheckWinCondition(playerState.TurnNumber);
                string predictedAfterText = ContinuationStamp.CapturePredicted(
                    player, battleSimulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber).StateText;
                bool predictedVictory = battleSimulator.TerminalStamp?.Outcome == CombatTerminalOutcome.Victory;
                CombatRootV1 predictedAfterRoot = WithExtension(rootAdapter.CapturePredicted(
                    combat, player, battleSimulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber,
                    terminalVictory: predictedVictory),
                    SnapshotWithManaState(opening, ReadPredictedState(battleSimulator, orbRelic, opening)));
                int enemyHpBefore = target.CurrentHp;
                if (!nativeCard.TryManualPlay(target))
                    throw new InvalidOperationException($"Native game refused full-battle ManaStrike #{index + 1}.");
                loop.RunUntilCompleted(RunManager.Instance.ActionExecutor.FinishedExecutingActions(), TimeSpan.FromSeconds(30),
                    $"Lab Eleanor full-battle ManaStrike #{index + 1}");
                if (_fullBattleTerminalError != null)
                    throw new InvalidOperationException("Eleanor full-battle terminal capture failed.", _fullBattleTerminalError);
                RuntimeSnapshot liveAfter = _fullBattleTerminal?.Snapshot
                    ?? CaptureRuntime(runtime, stateType, cardsField, reverseField, dualityPlayingField);
                CombatRootV1 nativeAfterRoot = _fullBattleTerminal?.Root
                    ?? WithExtension(rootAdapter.CaptureLive(combat), liveAfter);
                string nativeAfterText = _fullBattleTerminal?.StateText
                    ?? ContinuationStamp.CaptureLive(combat).StateText;
                RequireExactFullBattleRoot(nativeAfterRoot, $"native after ManaStrike #{index + 1}");
                string nativeAfterDigest = StateDigest.Compute(nativeAfterRoot);
                string predictedAfterDigest = StateDigest.Compute(predictedAfterRoot);
                bool exact = StringComparer.Ordinal.Equals(nativeAfterDigest, predictedAfterDigest)
                    && StringComparer.Ordinal.Equals(nativeAfterText, predictedAfterText)
                    && DescribeRootDifferences(predictedAfterRoot, nativeAfterRoot).Count == 0;
                steps.Add(new(index + 1, nativeCard.Id.Entry, playerState.TurnNumber,
                    nativeBeforeDigest, predictedBeforeDigest, nativeAfterDigest, predictedAfterDigest,
                    Sha(nativeAfterText), Sha(predictedAfterText), enemyHpBefore, target.IsAlive ? target.CurrentHp : 0, exact));
                if (!exact)
                {
                    string divergence = Path.Combine(outputDirectory, "eleanor-full-battle-divergence.json");
                    File.WriteAllText(divergence, JsonSerializer.Serialize(new
                    {
                        sequence = index + 1,
                        nativeAfterRoot,
                        predictedAfterRoot,
                        nativeAfterText,
                        predictedAfterText,
                        rootDifferences = DescribeRootDifferences(predictedAfterRoot, nativeAfterRoot),
                    }, new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true }));
                    throw new InvalidOperationException($"Eleanor native/simulator full battle diverged after ManaStrike #{index + 1}; see {divergence}.");
                }
                if (_fullBattleTerminal != null && !predictedVictory)
                    throw new InvalidOperationException("Native battle reached victory before the simulator did.");
                if (predictedVictory && _fullBattleTerminal == null)
                    throw new InvalidOperationException("Simulator reached victory without native CombatManager victory teardown.");
            }
            if (_fullBattleTerminal == null || battleSimulator.TerminalStamp?.Outcome != CombatTerminalOutcome.Victory)
                throw new InvalidOperationException($"Full battle did not converge to Victory: native={_fullBattleTerminal != null}, simulator={battleSimulator.TerminalStamp?.Outcome}.");
            if (steps.Count != 4 || steps.Any(step => !step.ExactMatch))
                throw new InvalidOperationException("Full Eleanor battle did not retain exact equality at every card boundary.");
            if (turnBoundaryEvidence is null)
                throw new InvalidOperationException("Full Eleanor battle did not cross the required player-turn boundary.");
            string parentAfterText = ContinuationStamp.CapturePredicted(
                player, simulator, rootSnapshot.StartTurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber).StateText;
            EleanorManaSnapshot parentAfterState = ReadPredictedState(simulator, orbRelic, opening);
            bool parentBranchStateUnchanged = StringComparer.Ordinal.Equals(parentBeforeText, parentAfterText)
                && parentBeforeState == parentAfterState;
            if (!parentBranchStateUnchanged)
                throw new InvalidOperationException("Multi-turn Victory branch mutated its parent simulator state.");
            FullBattleTerminal terminal = _fullBattleTerminal;
            string terminalContinuation = ContinuationStamp.CapturePredicted(
                player, battleSimulator, playerState.TurnNumber, rootSnapshot.Forecast, rootSnapshot.StartTurnNumber).StateText;
            bool terminalContinuationMatches = StringComparer.Ordinal.Equals(terminal.StateText, terminalContinuation);
            bool terminalRootMatches = StateDigest.Compute(terminal.Root) == StateDigest.Compute(
                WithExtension(rootAdapter.CapturePredicted(combat, player, battleSimulator, playerState.TurnNumber,
                    rootSnapshot.Forecast, rootSnapshot.StartTurnNumber, terminalVictory: true),
                    SnapshotWithManaState(opening, ReadPredictedState(battleSimulator, orbRelic, opening))));
            if (!terminalContinuationMatches || !terminalRootMatches)
                throw new InvalidOperationException("Terminal Eleanor battle root or continuation differs after victory.");
            object report = new
            {
                schema = "eleanor-full-battle-differential/v1",
                gameVersion = GameBuildPin.Version,
                gameCommit = GameBuildPin.Commit,
                mainAssemblyHash = GameBuildPin.MainAssemblyHash,
                modVersion = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_VERSION"),
                scenarioId = rootAdapter.ScenarioId,
                seed = "LAB_V0111_ELEANOR_M1_01",
                encounterModelId = combat.Encounter?.Id.Entry,
                testMode = TestMode.IsOn,
                steamInitialized = SteamInitializer.Initialized,
                saveStore = "OfflineSearchHarness.MemorySaveStore",
                persistentSaveWrites = false,
                replayWritesSuppressed = _suppressedFullBattleReplayWrites,
                progressSaveCallsSuppressed = _suppressedFullBattleProgressSaves,
                openingRootDigest = StateDigest.Compute(openingRoot),
                opening = opening,
                route = steps.Select(step => step.CardModelId).ToArray(),
                turnCount = steps.Select(step => step.Turn).Distinct().Count(),
                turnBoundary = turnBoundaryEvidence,
                steps,
                terminal = new
                {
                    nativeVictory = _fullBattleTerminal != null,
                    simulatorVictory = battleSimulator.TerminalStamp?.Outcome.ToString(),
                    nativeRootDigest = StateDigest.Compute(terminal.Root),
                    nativeContinuationSha256 = Sha(terminal.StateText),
                    simulatorContinuationSha256 = Sha(terminalContinuation),
                    terminalContinuationMatches,
                    terminalRootMatches,
                    finalMana = terminal.Snapshot.Mana,
                    finalGauge = terminal.Snapshot.Gauge,
                    finalStance = terminal.Snapshot.Stance,
                },
                rootPatchGuard,
                rootRegistrationGuard,
                turnStartPatchGuard,
                turnStartRegistrationGuard,
                registrationGuards = EleanorCardMirrors.GuardEvidence,
                extensionForkIsolation,
                parentBranchStateUnchanged,
                exactEveryStep = steps.All(step => step.ExactMatch),
                gameInstallWrites = false,
            };
            string outputPath = Path.Combine(outputDirectory, "eleanor-full-battle-differential.json");
            File.WriteAllText(outputPath, JsonSerializer.Serialize(report,
                new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true }));
            return report;
        }
        finally
        {
            _captureFullBattleTerminal = null;
            _fullBattleTerminal = null;
            _fullBattleTerminalError = null;
            _suppressedFullBattleReplayWrites = 0;
            _suppressedFullBattleProgressSaves = 0;
        }
    }

    private static void InstallFullBattleTerminalCapture(CombatState combat, VanillaCombatRootAdapter adapter,
        object runtime, Type stateType, FieldInfo cardsField, FieldInfo reverseField, FieldInfo dualityPlayingField)
    {
        MethodInfo[] candidates = typeof(CombatManager).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(method => method.Name == "EndCombatInternal")
            .Where(method => method.GetParameters() is { Length: 1 } parameters
                && parameters[0].ParameterType.Name == "CombatTurnState")
            .ToArray();
        if (candidates.Length != 1)
            throw new MissingMethodException($"Expected one v0.111.0 EndCombatInternal(CombatTurnState), found {candidates.Length}.");
        _captureFullBattleTerminal = () =>
        {
            RuntimeSnapshot snapshot = CaptureRuntime(runtime, stateType, cardsField, reverseField, dualityPlayingField);
            return new(WithExtension(adapter.CaptureLive(combat, terminalVictory: true), snapshot),
                ContinuationStamp.CaptureLive(combat).StateText, snapshot);
        };
        _fullBattleTerminal = null;
        _fullBattleTerminalError = null;
        MethodInfo writeReplay = AccessTools.Method(typeof(RunManager), nameof(RunManager.WriteReplay), [typeof(bool)])
            ?? throw new MissingMethodException(nameof(RunManager), nameof(RunManager.WriteReplay));
        Type progressSaveManager = typeof(SaveManager).Assembly.GetType(
            "MegaCrit.Sts2.Core.Saves.Managers.ProgressSaveManager", throwOnError: true)!;
        MethodInfo saveProgress = progressSaveManager.GetMethod("SaveProgress", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, types: Type.EmptyTypes, modifiers: null)
            ?? throw new MissingMethodException(progressSaveManager.FullName, "SaveProgress()");
        if (saveProgress.ReturnType != typeof(void))
            throw new InvalidOperationException($"Pinned SaveProgress changed return type to {saveProgress.ReturnType.FullName}; refusing unsafe save interception.");
        GameBootstrap.Harmony.Patch(writeReplay, prefix: new HarmonyMethod(
            typeof(EleanorCombatProbe).GetMethod(nameof(SuppressFullBattleReplayWrite), BindingFlags.Static | BindingFlags.NonPublic)!));
        GameBootstrap.Harmony.Patch(saveProgress, prefix: new HarmonyMethod(
            typeof(EleanorCombatProbe).GetMethod(nameof(SuppressFullBattleProgressSave), BindingFlags.Static | BindingFlags.NonPublic)!));
        GameBootstrap.Harmony.Patch(candidates[0], prefix: new HarmonyMethod(
            typeof(EleanorCombatProbe).GetMethod(nameof(CaptureFullBattleTerminalPrefix), BindingFlags.Static | BindingFlags.NonPublic)!));
        MethodInfo hoverClear = AccessTools.Method(typeof(NHoverTipSet), nameof(NHoverTipSet.Clear))
            ?? throw new MissingMethodException(nameof(NHoverTipSet), nameof(NHoverTipSet.Clear));
        GameBootstrap.Harmony.Patch(hoverClear, prefix: new HarmonyMethod(
            typeof(EleanorCombatProbe).GetMethod(nameof(SuppressFullBattleHoverClear), BindingFlags.Static | BindingFlags.NonPublic)!));
    }

    private static void CaptureFullBattleTerminalPrefix()
    {
        if (_captureFullBattleTerminal == null || _fullBattleTerminal != null || _fullBattleTerminalError != null) return;
        try { _fullBattleTerminal = _captureFullBattleTerminal(); }
        catch (Exception error) { _fullBattleTerminalError = error; }
    }

    private static bool SuppressFullBattleReplayWrite()
    {
        _suppressedFullBattleReplayWrites++;
        return false;
    }

    private static bool SuppressFullBattleProgressSave()
    {
        _suppressedFullBattleProgressSaves++;
        return false;
    }

    private static bool SuppressFullBattleHoverClear() => false;

    private static void RequireExactFullBattleRoot(CombatRootV1 root, string label)
    {
        CaptureValidationV1 validation = CombatCaptureValidator.ValidateExactReady(root);
        if (!validation.IsExactReady)
            throw new InvalidOperationException($"{label} failed exact-ready validation: {string.Join(";", validation.Errors)}");
    }

    private static RuntimeSnapshot CaptureRuntime(object runtime, Type stateType, FieldInfo cardsField,
        FieldInfo reverseField, FieldInfo dualityPlayingField)
    {
        object state = runtime.GetType().GetProperty("State")!.GetValue(runtime)!;
        object reverse = reverseField.GetValue(runtime)!;
        Player player = (Player)RequiredField(runtime.GetType(), "_player").GetValue(runtime)!;
        var activeCards = (ICollection)cardsField.GetValue(runtime)!;
        var activeReplays = (IEnumerable)dualityPlayingField.GetValue(null)!;
        object? pendingDiagnostic = runtime.GetType().GetField("_diagnosticGauge", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(runtime);
        Dictionary<string, int> owned = ReadDictionary(RequiredField(reverse.GetType(), "_owned").GetValue(reverse)!)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        List<string> active = ReadSet(RequiredField(reverse.GetType(), "_active").GetValue(reverse)!);
        List<string> cleared = ReadSet(RequiredField(reverse.GetType(), "_cleared").GetValue(reverse)!);
        SortedDictionary<string, string> identities = new(StringComparer.Ordinal);
        foreach ((string key, object value) in ReadObjectDictionary(RequiredField(reverse.GetType(), "_identities").GetValue(reverse)!))
        {
            int? amount = value.GetType().GetProperty("Amount")?.GetValue(value) is int current ? current : null;
            bool attached = player.Creature.Powers.Any(power => ReferenceEquals(power, value));
            identities[key] = $"{value.GetType().FullName};amount={amount?.ToString() ?? "?"};attached={attached}";
        }
        return new(
            ReadIntProperty(state, "Mana"),
            ReadIntProperty(state, "Gauge"),
            stateType.GetProperty("Stance")!.GetValue(state)!.ToString()!,
            (bool)stateType.GetProperty("CompleteOrb")!.GetValue(state)!,
            ReadIntProperty(state, "StartDrawBonus"),
            pendingDiagnostic is null ? null : Convert.ToInt32(pendingDiagnostic),
            ReadBoolField(runtime, "_listening"),
            activeCards.Count,
            ReadBoolField(reverse, "_syncing"),
            reverse.GetType().GetProperty("Issue")!.GetValue(reverse)?.ToString(),
            new SortedDictionary<string, int>(owned, StringComparer.Ordinal),
            active,
            cleared,
            identities,
            activeReplays.Cast<object>().Count());
    }

    private static CombatRootV1 WithExtension(CombatRootV1 root, RuntimeSnapshot snapshot)
    {
        bool forkable = snapshot.ActiveCardTransactions == 0 && !snapshot.ReverseSyncing
            && snapshot.ActiveDualityReplays == 0 && snapshot.PendingDiagnosticGauge is null
            && snapshot.PowerIdentities.Values.All(value => value.Contains("attached=True", StringComparison.Ordinal));
        ModelExtensionStateV1 extension = new()
        {
            ModId = "MyMod",
            TypeId = "Eleanor.ManaRuntime",
            SchemaVersion = 1,
            Payload = JsonSerializer.SerializeToElement(snapshot, ProtocolJson.Options),
            HasForkAdapter = forkable,
        };
        if (!forkable)
            throw new InvalidOperationException("Eleanor runtime cannot be safely forked: active transaction/identity/diagnostic state is unresolved.");
        return root with
        {
            Header = root.Header with { Mods = [new ModVersionV1("MyMod", Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_VERSION") ?? "unknown")] },
            Extensions = [extension],
        };
    }

    private static bool VerifyDetachedFork(CombatRootV1 root)
    {
        string parentDigest = StateDigest.Compute(root);
        CombatRootV1 child = root.Fork();
        if (StateDigest.Compute(child) != parentDigest) return false;
        ModelExtensionStateV1 extension = child.Extensions.Single();
        RuntimeSnapshot state = JsonSerializer.Deserialize<RuntimeSnapshot>(extension.Payload.GetRawText(), ProtocolJson.Options)
            ?? throw new InvalidOperationException("Could not deserialize Eleanor fork payload.");
        CombatRootV1 changedChild = child with
        {
            Extensions = [extension with { Payload = JsonSerializer.SerializeToElement(state with { Mana = state.Mana + 1 }, ProtocolJson.Options) }],
        };
        return StateDigest.Compute(root) == parentDigest && StateDigest.Compute(changedChild) != parentDigest;
    }

    private static EleanorManaSnapshot ReadPredictedState(CombatPredictionSimulator simulator, RelicModel orbRelic,
        RuntimeSnapshot fallback)
        => EleanorCardMirrors.TryRead(simulator, orbRelic, out EleanorManaSnapshot state)
            ? state
            : new(fallback.Mana, fallback.Gauge, fallback.Stance, fallback.CompleteOrb, fallback.StartDrawBonus);

    private static RuntimeSnapshot SnapshotWithManaState(RuntimeSnapshot snapshot, EleanorManaSnapshot state)
        => snapshot with { Mana = state.Mana, Gauge = state.Gauge, Stance = state.Stance,
            CompleteOrb = state.CompleteOrb, StartDrawBonus = state.StartDrawBonus };

    private static object CloneManaState(Type stateType, RuntimeSnapshot snapshot)
    {
        object clone = Activator.CreateInstance(stateType)!;
        SetProperty(clone, "Mana", snapshot.Mana);
        SetProperty(clone, "Gauge", snapshot.Gauge);
        SetProperty(clone, "Stance", Enum.Parse(stateType.GetProperty("Stance")!.PropertyType, snapshot.Stance));
        SetProperty(clone, "CompleteOrb", snapshot.CompleteOrb);
        SetProperty(clone, "StartDrawBonus", snapshot.StartDrawBonus);
        return clone;
    }

    private static int ReadDynamicInt(CardModel card, string name)
        => Convert.ToInt32(card.DynamicVars[name].GetType().GetProperty("IntValue")!.GetValue(card.DynamicVars[name]));

    private static bool ReadBoolField(object owner, string name)
        => (bool)(RequiredField(owner.GetType(), name).GetValue(owner) ?? false);

    private static int ReadIntProperty(object owner, string name)
        => Convert.ToInt32(owner.GetType().GetProperty(name)!.GetValue(owner));

    private static FieldInfo RequiredField(Type type, string name)
        => type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new MissingFieldException(type.FullName, name);

    private static void SetProperty(object target, string name, object? value)
    {
        PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(target.GetType().FullName, name);
        (property.GetSetMethod(true) ?? throw new MissingMethodException(target.GetType().FullName, "set_" + name)).Invoke(target, [value]);
    }

    private static Dictionary<string, int> ReadDictionary(object source)
        => ReadObjectDictionary(source).ToDictionary(item => item.Key, item => Convert.ToInt32(item.Value), StringComparer.Ordinal);

    private static List<string> ReadSet(object source)
        => ((IEnumerable)source).Cast<object>().Select(value => value.ToString() ?? "").OrderBy(value => value, StringComparer.Ordinal).ToList();

    private static List<(string Key, object Value)> ReadObjectDictionary(object source)
    {
        List<(string, object)> values = [];
        foreach (object item in (IEnumerable)source)
        {
            Type itemType = item.GetType();
            object key = itemType.GetProperty("Key")!.GetValue(item)!;
            object value = itemType.GetProperty("Value")!.GetValue(item)!;
            string label = key.ToString() ?? "";
            if (key.GetType().IsValueType && key.GetType().FullName?.StartsWith("System.ValueTuple", StringComparison.Ordinal) == true)
            {
                object? first = key.GetType().GetField("Item1")?.GetValue(key);
                object? second = key.GetType().GetField("Item2")?.GetValue(key);
                label = $"{first}/{second}";
            }
            values.Add((label, value));
        }
        return values;
    }

    private static List<string> DescribeRootDifferences(CombatRootV1 expected, CombatRootV1 actual)
    {
        JsonElement left = JsonSerializer.SerializeToElement(expected, ProtocolJson.Options);
        JsonElement right = JsonSerializer.SerializeToElement(actual, ProtocolJson.Options);
        Dictionary<string, JsonElement> a = left.EnumerateObject().ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        Dictionary<string, JsonElement> b = right.EnumerateObject().ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        return a.Keys.Union(b.Keys, StringComparer.Ordinal).Where(key => !a.TryGetValue(key, out JsonElement x)
                || !b.TryGetValue(key, out JsonElement y) || StateDigest.Compute(x) != StateDigest.Compute(y))
            .OrderBy(key => key, StringComparer.Ordinal).ToList();
    }

    private static void RequireEqual(string expected, string actual, string label)
    {
        if (!StringComparer.Ordinal.Equals(expected, actual))
            throw new InvalidOperationException($"{label} mismatch: expected={Sha(expected)} actual={Sha(actual)}.");
    }

    private static string Sha(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static Exception Unwrap(Exception error)
    {
        while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
        return error;
    }
}
