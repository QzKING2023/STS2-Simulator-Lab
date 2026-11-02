using System.Reflection;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CombatSolver;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Simulation;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Platform.Steam;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;

namespace OfflineSearchHarness;

/// <summary>
/// Lab-owned differential probe. It uses the pinned game's CardModel.TryManualPlay
/// and CombatSolver's prediction branch from the same in-memory native game root.
/// It is compiled only into the ignored local harness copy and never into a game Mod.
/// </summary>
internal static class VanillaGameActionProbe
{
    private static readonly Stopwatch HarnessClock = Stopwatch.StartNew();

    public static object Run(CombatState combat, MainLoopContext loop, string outputDirectory)
    {
        InstallGodotClockShim();
        if (!TestMode.IsOn)
            throw new InvalidOperationException("Lab native probe requires the game's TestMode.");
        if (SteamInitializer.Initialized)
            throw new InvalidOperationException("Lab native probe refuses a Steam-initialized runtime.");

        bool myModLoaded = AppDomain.CurrentDomain.GetAssemblies()
            .Any(assembly => StringComparer.OrdinalIgnoreCase.Equals(assembly.GetName().Name, "MyMod"));
        if (myModLoaded)
            throw new InvalidOperationException("Vanilla baseline must not load MyMod.");

        object saveStore = typeof(SaveManager)
            .GetField("_saveStore", BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(SaveManager.Instance)
            ?? throw new InvalidOperationException("Could not verify the active SaveManager backend.");
        string saveStoreName = saveStore.GetType().FullName ?? saveStore.GetType().Name;
        if (saveStore.GetType().Name != "MemorySaveStore")
            throw new InvalidOperationException($"Refusing runtime save backend {saveStoreName}.");

        Player player = LocalContext.GetMe(combat)
            ?? throw new InvalidOperationException("No local player in the generated combat.");
        PlayerCombatState playerCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("No player combat state in the generated combat.");
        if (playerCombat.Phase != PlayerTurnPhase.Play || combat.CurrentSide != CombatSide.Player)
            throw new InvalidOperationException("Native probe root is not a stable player action phase.");

        CardModel nativeCard = playerCombat.Hand.Cards.FirstOrDefault(card =>
                card.Id.Entry == "STRIKE_IRONCLAD" && card.CanPlay())
            ?? throw new InvalidOperationException("The pinned vanilla opening hand has no playable Strike.");
        int handIndex = playerCombat.Hand.Cards.ToList().IndexOf(nativeCard);
        if (handIndex < 0)
            throw new InvalidOperationException("The selected native Strike is missing from the captured hand order.");
        Creature target = combat.Enemies.FirstOrDefault(enemy => enemy.IsAlive)
            ?? throw new InvalidOperationException("The native probe requires a living enemy.");

        CombatRootSnapshot root = CombatRootSnapshot.Capture(combat);
        CombatPredictionSimulator simulator = root.ForkSimulator();
        var liveBefore = ContinuationStamp.CaptureLive(combat);
        var simulatedBefore = ContinuationStamp.CapturePredicted(
            player, simulator, playerCombat.TurnNumber, root.Forecast, root.StartTurnNumber);
        bool beforeMatches = StringComparer.Ordinal.Equals(liveBefore.StateText, simulatedBefore.StateText);
        if (!beforeMatches)
            throw new InvalidOperationException("The simulator root differs from the native game root before action.");

        var rootAdapter = new VanillaCombatRootAdapter(combat, "LAB_V0111_IRONCLAD_M1_01");
        SimulatorLab.Core.CombatRootV1 nativeRoot = rootAdapter.CaptureLive(combat);
        SimulatorLab.Core.CombatRootV1 simulatorRoot = rootAdapter.CapturePredicted(
            combat, player, simulator, playerCombat.TurnNumber, root.Forecast, root.StartTurnNumber);
        var nativeRootValidation = SimulatorLab.Core.CombatCaptureValidator.ValidateExactReady(nativeRoot);
        var simulatorRootValidation = SimulatorLab.Core.CombatCaptureValidator.ValidateExactReady(simulatorRoot);
        if (!nativeRootValidation.IsExactReady || !simulatorRootValidation.IsExactReady)
            throw new InvalidOperationException("CombatRootV1 capture rejected an opening root: native="
                + string.Join(";", nativeRootValidation.Errors) + " simulator="
                + string.Join(";", simulatorRootValidation.Errors));
        string nativeRootDigest = SimulatorLab.Core.StateDigest.Compute(nativeRoot);
        string simulatorRootDigest = SimulatorLab.Core.StateDigest.Compute(simulatorRoot);
        if (!StringComparer.Ordinal.Equals(nativeRootDigest, simulatorRootDigest))
            throw new InvalidOperationException($"CombatRootV1 live/predicted roots differ: native={nativeRootDigest} simulator={simulatorRootDigest}.");

        PredictedCard simulatedCard = simulator.State.FindCard(nativeCard)
            ?? throw new InvalidOperationException("The native Strike is absent from the simulator root.");
        var simulatedCombat = (SimulatedCombatState)simulator.State.CombatState;
        if (!simulatedCombat.CanPlayCard(simulator, simulatedCard, out _, out _))
            throw new InvalidOperationException("The simulator rejected the native playable Strike.");
        bool simulatorAccepted = simulator.ManualPlay(simulatedCard, target, out _);
        if (!simulatorAccepted)
            throw new InvalidOperationException("The simulator could not finish the native Strike action.");

        var predictedAfter = ContinuationStamp.CapturePredicted(
            player, simulator, playerCombat.TurnNumber, root.Forecast, root.StartTurnNumber);
        SimulatorLab.Core.CombatRootV1 simulatorAfterRoot = rootAdapter.CapturePredicted(
            combat, player, simulator, playerCombat.TurnNumber, root.Forecast, root.StartTurnNumber);

        bool nativeAccepted = nativeCard.TryManualPlay(target);
        if (!nativeAccepted)
            throw new InvalidOperationException("The game's native CardModel.TryManualPlay rejected the Strike.");
        loop.RunUntilCompleted(
            RunManager.Instance.ActionExecutor.FinishedExecutingActions(),
            TimeSpan.FromSeconds(30),
            "Lab native Strike execution");

        var nativeAfter = ContinuationStamp.CaptureLive(combat);
        SimulatorLab.Core.CombatRootV1 nativeAfterRoot = rootAdapter.CaptureLive(combat);
        var nativeAfterValidation = SimulatorLab.Core.CombatCaptureValidator.ValidateExactReady(nativeAfterRoot);
        var simulatorAfterValidation = SimulatorLab.Core.CombatCaptureValidator.ValidateExactReady(simulatorAfterRoot);
        if (!nativeAfterValidation.IsExactReady || !simulatorAfterValidation.IsExactReady)
            throw new InvalidOperationException("CombatRootV1 capture rejected the post-Strike root: native="
                + string.Join(";", nativeAfterValidation.Errors) + " simulator="
                + string.Join(";", simulatorAfterValidation.Errors));
        string nativeAfterRootDigest = SimulatorLab.Core.StateDigest.Compute(nativeAfterRoot);
        string simulatorAfterRootDigest = SimulatorLab.Core.StateDigest.Compute(simulatorAfterRoot);
        if (!StringComparer.Ordinal.Equals(nativeAfterRootDigest, simulatorAfterRootDigest))
            throw new InvalidOperationException($"CombatRootV1 live/predicted post-Strike roots differ: native={nativeAfterRootDigest} simulator={simulatorAfterRootDigest}.");
        List<string> differences = predictedAfter.DescribeDifferences(nativeAfter).ToList();
        string targetCombatantId = target.CombatId.ToString()
            ?? throw new InvalidOperationException("The selected native target has no stable combatant ID.");
        var report = new SimulatorLab.Core.NativeGameActionDifferentialFileV1
        {
            CaptureMethod = "CombatSolver.ContinuationStamp.CaptureLive/CapturePredicted",
            ScenarioId = "LAB_V0111_IRONCLAD_M1_01",
            CharacterModelId = player.Character.Id.Entry,
            EncounterModelId = "FUZZY_WURM_CRAWLER_WEAK",
            TargetCombatantId = targetCombatantId,
            TestMode = TestMode.IsOn,
            SteamInitialized = SteamInitializer.Initialized,
            SaveStore = saveStoreName,
            MyModLoaded = myModLoaded,
            UsesPresentationClockShim = true,
            Action = new SimulatorLab.Core.CombatActionV1
            {
                Kind = SimulatorLab.Core.CombatActionKind.PlayCard,
                CardInstanceId = $"hand:{handIndex}:{nativeCard.Id.Entry}",
                CardHandIndex = handIndex,
                TargetCombatantId = targetCombatantId,
            },
            NativeRoot = SimulatorLab.Core.NativeGameDifferentialImporter.Stamp(liveBefore.StateText),
            SimulatorRoot = SimulatorLab.Core.NativeGameDifferentialImporter.Stamp(simulatedBefore.StateText),
            SimulatorAfter = SimulatorLab.Core.NativeGameDifferentialImporter.Stamp(predictedAfter.StateText),
            NativeAfter = SimulatorLab.Core.NativeGameDifferentialImporter.Stamp(nativeAfter.StateText),
            Differences = differences,
        };

        File.WriteAllText(
            Path.Combine(outputDirectory, "native-game-action-differential.json"),
            SimulatorLab.Core.NativeGameDifferentialImporter.Write(report));
        var rootTrace = new SimulatorLab.Core.CombatTraceV1
        {
            Header = nativeRoot.Header,
            RootStateDigest = nativeRootDigest,
            Steps =
            [
                new(nativeRootDigest, report.Action, nativeAfterRootDigest, []),
            ],
        };
        File.WriteAllText(
            Path.Combine(outputDirectory, "combat-root-single-action-trace.json"),
            System.Text.Json.JsonSerializer.Serialize(rootTrace,
                new System.Text.Json.JsonSerializerOptions(SimulatorLab.Core.ProtocolJson.Options) { WriteIndented = true }));
        File.WriteAllText(
            Path.Combine(outputDirectory, "combat-root-single-action-states.json"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                nativeRoot,
                simulatorRoot,
                nativeAfterRoot,
                simulatorAfterRoot,
                rootDigests = new { nativeRootDigest, simulatorRootDigest, nativeAfterRootDigest, simulatorAfterRootDigest },
            }, new System.Text.Json.JsonSerializerOptions(SimulatorLab.Core.ProtocolJson.Options) { WriteIndented = true }));
        var validated = SimulatorLab.Core.NativeGameDifferentialImporter.Read(
            File.ReadAllText(Path.Combine(outputDirectory, "native-game-action-differential.json")));
        if (!validated.IsValid)
        {
            throw new InvalidOperationException("Native differential file did not validate: " + string.Join("; ", validated.Errors));
        }
        if (!beforeMatches || !simulatorAccepted || !nativeAccepted || differences.Count != 0
            || nativeRootDigest != simulatorRootDigest || nativeAfterRootDigest != simulatorAfterRootDigest)
            throw new InvalidOperationException("Native Strike and simulator branch did not produce an exact one-action match.");
        return report;
    }

    internal static void InstallGodotClockShim()
    {
        Type timeType = typeof(Godot.Time);
        ConstructorInfo typeInitializer = timeType.TypeInitializer
            ?? throw new InvalidOperationException("Godot.Time has no type initializer to guard in the headless harness.");
        GameBootstrap.Harmony.Patch(typeInitializer,
            prefix: new HarmonyMethod(typeof(VanillaGameActionProbe), nameof(SkipGodotTimeTypeInitializer)));
        GameBootstrap.Harmony.Patch(AccessTools.Method(timeType, nameof(Godot.Time.GetTicksMsec)),
            prefix: new HarmonyMethod(typeof(VanillaGameActionProbe), nameof(GetTicksMsecPrefix)));
        GameBootstrap.Harmony.Patch(AccessTools.Method(timeType, nameof(Godot.Time.GetTicksUsec)),
            prefix: new HarmonyMethod(typeof(VanillaGameActionProbe), nameof(GetTicksUsecPrefix)));
        GameBootstrap.Harmony.Patch(AccessTools.Method(timeType, nameof(Godot.Time.GetUnixTimeFromSystem)),
            prefix: new HarmonyMethod(typeof(VanillaGameActionProbe), nameof(GetUnixTimePrefix)));
    }

    private static bool SkipGodotTimeTypeInitializer() => false;

    private static bool GetTicksMsecPrefix(ref ulong __result)
    {
        __result = (ulong)HarnessClock.ElapsedMilliseconds;
        return false;
    }

    private static bool GetTicksUsecPrefix(ref ulong __result)
    {
        __result = (ulong)(HarnessClock.ElapsedTicks * 1_000_000L / Stopwatch.Frequency);
        return false;
    }

    private static bool GetUnixTimePrefix(ref double __result)
    {
        __result = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
        return false;
    }

}
