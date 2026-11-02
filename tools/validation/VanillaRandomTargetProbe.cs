using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CombatSolver;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Platform.Steam;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using SimulatorLab.Core;

namespace OfflineSearchHarness;

/// <summary>Exercises a vanilla random-target card through the pinned game's live and predicted action paths.</summary>
internal static class VanillaRandomTargetProbe
{
    public static object Run(CombatState combat, MainLoopContext loop, string outputDirectory)
    {
        VanillaGameActionProbe.InstallGodotClockShim();
        if (!TestMode.IsOn || SteamInitializer.Initialized)
            throw new InvalidOperationException("Random-target prelude requires isolated TestMode without Steam.");
        object saveStore = typeof(SaveManager).GetField("_saveStore", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(SaveManager.Instance)
            ?? throw new InvalidOperationException("Could not verify the SaveManager backend before random-target prelude.");
        if (!StringComparer.Ordinal.Equals(saveStore.GetType().Name, "MemorySaveStore"))
            throw new InvalidOperationException($"Random-target prelude refused {saveStore.GetType().FullName}.");

        Player player = LocalContext.GetMe(combat)
            ?? throw new InvalidOperationException("No local player for random-target prelude.");
        PlayerCombatState playerState = player.PlayerCombatState
            ?? throw new InvalidOperationException("No player combat state for random-target prelude.");
        if (combat.CurrentSide != CombatSide.Player || playerState.Phase != PlayerTurnPhase.Play)
            throw new InvalidOperationException("Random-target prelude requires a stable player decision boundary.");
        CardModel canonical = ModelDb.AllCards.FirstOrDefault(card => card.Id.Entry == "SWORD_BOOMERANG")
            ?? throw new InvalidOperationException("The v0.111.0 vanilla SWORD_BOOMERANG model is unavailable.");
        CardModel nativeCard = combat.CreateCard(canonical, player);
        Task<CardPileAddResult> addTask = CardPileCmd.Add(nativeCard, PileType.Hand);
        loop.RunUntilCompleted(addTask, TimeSpan.FromSeconds(30), "Add isolated vanilla random-target fixture card");
        if (!addTask.GetAwaiter().GetResult().success)
            throw new InvalidOperationException("The pinned game refused to add the SWORD_BOOMERANG fixture to hand.");

        CombatRootSnapshot snapshot = CombatRootSnapshot.Capture(combat);
        CombatPredictionSimulator simulator = snapshot.ForkSimulator();
        SimulatedCombatState shadow = (SimulatedCombatState)simulator.State.CombatState;
        VanillaCombatRootAdapter adapter = new(combat, "LAB_V0111_IRONCLAD_RANDOM_TARGET_01");
        CombatRootV1 nativeBeforeRoot = adapter.CaptureLive(combat);
        CombatRootV1 predictedBeforeRoot = adapter.CapturePredicted(
            combat, player, simulator, playerState.TurnNumber, snapshot.Forecast, snapshot.StartTurnNumber);
        RequireExactRoot(nativeBeforeRoot, "native random-target root");
        RequireExactRoot(predictedBeforeRoot, "predicted random-target root");
        string nativeBeforeDigest = StateDigest.Compute(nativeBeforeRoot);
        string predictedBeforeDigest = StateDigest.Compute(predictedBeforeRoot);
        RequireEqual(nativeBeforeDigest, predictedBeforeDigest, "random-target CombatRootV1 root");
        string nativeBeforeText = ContinuationStamp.CaptureLive(combat).StateText;
        var predictedBefore = ContinuationStamp.CapturePredicted(
            player, simulator, playerState.TurnNumber, snapshot.Forecast, snapshot.StartTurnNumber);
        RequireEqual(nativeBeforeText, predictedBefore.StateText, "random-target full continuation root");

        long targetsRngBefore = nativeBeforeRoot.RandomStreams.Single(stream => stream.Stream == CombatRngStream.Targets).DrawCount;
        Dictionary<string, int> enemyHpBefore = nativeBeforeRoot.Combatants
            .Where(combatant => !combatant.IsPlayer && combatant.IsAlive)
            .ToDictionary(combatant => combatant.CombatantId, combatant => combatant.CurrentHp, StringComparer.Ordinal);
        if (enemyHpBefore.Count < 2)
            throw new InvalidOperationException($"Random target selection requires at least two living enemies; captured {enemyHpBefore.Count}.");
        PredictedCard predictedCard = simulator.State.FindCard(nativeCard)
            ?? throw new InvalidOperationException("Predicted state omitted the SWORD_BOOMERANG fixture card.");
        if (!shadow.CanPlayCard(simulator, predictedCard, out _, out _))
            throw new InvalidOperationException("The simulator rejected a playable SWORD_BOOMERANG fixture.");
        if (!simulator.ManualPlay(predictedCard, null, out _))
            throw new InvalidOperationException("The simulator could not resolve the SWORD_BOOMERANG random-target action.");
        _ = simulator.CheckWinCondition(playerState.TurnNumber);
        var predictedAfter = ContinuationStamp.CapturePredicted(
            player, simulator, playerState.TurnNumber, snapshot.Forecast, snapshot.StartTurnNumber);
        CombatRootV1 predictedAfterRoot = adapter.CapturePredicted(
            combat, player, simulator, playerState.TurnNumber, snapshot.Forecast, snapshot.StartTurnNumber);

        if (!nativeCard.TryManualPlay(null))
            throw new InvalidOperationException("The pinned game rejected native SWORD_BOOMERANG.TryManualPlay(null).");
        loop.RunUntilCompleted(
            RunManager.Instance.ActionExecutor.FinishedExecutingActions(),
            TimeSpan.FromSeconds(30),
            "Lab native SWORD_BOOMERANG random-target action");
        var nativeAfter = ContinuationStamp.CaptureLive(combat);
        CombatRootV1 nativeAfterRoot = adapter.CaptureLive(combat);
        RequireExactRoot(nativeAfterRoot, "native random-target post-action root");
        RequireExactRoot(predictedAfterRoot, "predicted random-target post-action root");
        List<string> continuationDifferences = predictedAfter.DescribeDifferences(nativeAfter).ToList();
        string nativeAfterDigest = StateDigest.Compute(nativeAfterRoot);
        string predictedAfterDigest = StateDigest.Compute(predictedAfterRoot);
        if (continuationDifferences.Count != 0 || nativeAfterDigest != predictedAfterDigest)
            throw new InvalidOperationException("Random-target native/simulator results differ: root="
                + string.Join(",", RootDifferences(predictedAfterRoot, nativeAfterRoot)) + "; continuation="
                + string.Join(" | ", continuationDifferences.Take(12)) + ".");

        long targetsRngAfter = nativeAfterRoot.RandomStreams.Single(stream => stream.Stream == CombatRngStream.Targets).DrawCount;
        string targetsRngStateBefore = nativeBeforeRoot.RandomStreams.Single(stream => stream.Stream == CombatRngStream.Targets).StateHex;
        string targetsRngStateAfter = nativeAfterRoot.RandomStreams.Single(stream => stream.Stream == CombatRngStream.Targets).StateHex;
        if (targetsRngAfter <= targetsRngBefore)
            throw new InvalidOperationException("SWORD_BOOMERANG did not consume the captured Target RNG stream.");
        Dictionary<string, int> enemyHpAfter = nativeAfterRoot.Combatants
            .Where(combatant => !combatant.IsPlayer)
            .ToDictionary(combatant => combatant.CombatantId, combatant => combatant.CurrentHp, StringComparer.Ordinal);
        string[] nativeTargetsDamaged = enemyHpBefore.Keys
            .Where(id => enemyHpAfter.TryGetValue(id, out int hpAfter) && hpAfter < enemyHpBefore[id])
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        Dictionary<string, int> predictedHpAfter = predictedAfterRoot.Combatants
            .Where(combatant => !combatant.IsPlayer)
            .ToDictionary(combatant => combatant.CombatantId, combatant => combatant.CurrentHp, StringComparer.Ordinal);
        string[] predictedTargetsDamaged = enemyHpBefore.Keys
            .Where(id => predictedHpAfter.TryGetValue(id, out int hpAfter) && hpAfter < enemyHpBefore[id])
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (nativeTargetsDamaged.Length == 0 || !nativeTargetsDamaged.SequenceEqual(predictedTargetsDamaged, StringComparer.Ordinal))
            throw new InvalidOperationException("Random target action did not produce a matching selected-enemy HP delta.");

        var evidence = new
        {
            schema = "vanilla-random-target-differential/v1",
            gameVersion = GameBuildPin.Version,
            gameCommit = GameBuildPin.Commit,
            mainAssemblyHash = GameBuildPin.MainAssemblyHash,
            characterModelId = player.Character.Id.Entry,
            encounterModelId = combat.Encounter?.Id.Entry,
            cardModelId = nativeCard.Id.Entry,
            targetType = nativeCard.TargetType.ToString(),
            validEnemyTargets = enemyHpBefore.Count,
            enemyHpBefore,
            enemyHpAfter,
            nativeTargetsDamaged,
            predictedTargetsDamaged,
            testMode = TestMode.IsOn,
            steamInitialized = SteamInitializer.Initialized,
            saveStore = saveStore.GetType().FullName,
            myModLoaded = false,
            usesPresentationClockShim = true,
            nativeRootDigest = nativeBeforeDigest,
            simulatorRootDigest = predictedBeforeDigest,
            nativeAfterRootDigest = nativeAfterDigest,
            simulatorAfterRootDigest = predictedAfterDigest,
            nativeBeforeStateText = nativeBeforeText,
            simulatorBeforeStateText = predictedBefore.StateText,
            nativeAfterStateText = nativeAfter.StateText,
            simulatorAfterStateText = predictedAfter.StateText,
            targetRng = new { drawCountBefore = targetsRngBefore, drawCountAfter = targetsRngAfter,
                stateHexBefore = targetsRngStateBefore, stateHexAfter = targetsRngStateAfter },
            action = new CombatActionV1 { Kind = CombatActionKind.PlayCard,
                CardInstanceId = "fixture:SWORD_BOOMERANG", CardHandIndex = playerState.Hand.Cards.ToList().IndexOf(nativeCard) },
            exactStateMatch = true,
        };
        File.WriteAllText(Path.Combine(outputDirectory, "vanilla-random-target-differential.json"),
            JsonSerializer.Serialize(evidence, new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true }));
        return new { card = nativeCard.Id.Entry, targetRngDraws = targetsRngAfter - targetsRngBefore,
            exact = true, targets = combat.Enemies.Count(enemy => enemy.IsAlive) };
    }

    private static void RequireExactRoot(CombatRootV1 root, string label)
    {
        CaptureValidationV1 result = CombatCaptureValidator.ValidateExactReady(root);
        if (!result.IsExactReady)
            throw new InvalidOperationException($"{label} is incomplete: " + string.Join("; ", result.Errors));
    }

    private static void RequireEqual(string expected, string actual, string label)
    {
        if (!StringComparer.Ordinal.Equals(expected, actual))
            throw new InvalidOperationException($"{label} mismatch: expected={Sha(expected)} actual={Sha(actual)}.");
    }

    private static List<string> RootDifferences(CombatRootV1 expected, CombatRootV1 actual)
    {
        JsonElement left = JsonSerializer.SerializeToElement(expected, ProtocolJson.Options);
        JsonElement right = JsonSerializer.SerializeToElement(actual, ProtocolJson.Options);
        Dictionary<string, JsonElement> a = left.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
        Dictionary<string, JsonElement> b = right.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
        return a.Keys.Union(b.Keys, StringComparer.Ordinal).Where(key =>
                !a.TryGetValue(key, out JsonElement x) || !b.TryGetValue(key, out JsonElement y)
                || StateDigest.Compute(x) != StateDigest.Compute(y))
            .OrderBy(key => key, StringComparer.Ordinal).ToList();
    }

    private static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
