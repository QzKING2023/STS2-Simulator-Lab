using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CombatSolver;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.Damage;
using CombatSolver.Engine.InCombat.Simulation;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Platform.Steam;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using SimulatorLab.Core;

namespace OfflineSearchHarness;

/// <summary>Version-specific Lab adapter for the locked Eleanor v0.4.0 forms/echo binary.</summary>
internal static class EleanorV040Probe
{
    private const string PatchOwner = "simulatorlab.eleanor-v040.registration-probe";
    private static readonly Harmony PatchProbe = new(PatchOwner);
    private static readonly List<object> RegistrationEvidence = [];
    private static readonly List<object> AdapterInvocations = [];
    private static CombatPredictionSimulator? _lastTurnStartSimulator;
    private static V040Snapshot? _lastTurnStartSnapshot;
    private static V040PredictionState? _lastTurnStartState;
    private static V040PredictionState? _lastRecordingDamageState;
    private static int _lastTurnStartNumber;
    private static int _suppressedReplayWrites;
    private static int _suppressedProgressSaves;
    private static Func<VictoryTerminalCapture>? _captureVictoryTerminal;
    private static VictoryTerminalCapture? _victoryTerminalEvidence;
    private static Exception? _victoryTerminalCaptureError;
    private static int _suppressedVictoryHoverClears;
    private static Type? _stateType;
    private static string? _manifest;
    private static ManaStanceChoice _selectedForm;
    private static MethodInfo? _recordingHookTarget;
    private static MethodInfo? _temporaryRecordingTurnStartTarget;
    private static MainLoopContext? _loop;

    private enum ManaStanceChoice { Neutral, Mentor, Reverse }

    private sealed record V040Snapshot(int Mana, string Stance, bool CompleteOrb, int StartDrawBonus, decimal Echo);
    private sealed record PoolFirstDrawPreflight(string status, object poolQuery, object firstDraw, string caveat);
    private sealed record VictoryTerminalCapture(CombatRootV1 Root, string StateText, V040Snapshot Mana,
        decimal PlayerHp, object[] Enemies, bool VictoryConditionsSatisfied);

    public static void RegisterFromLoadedMod()
    {
        Assembly mod = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MyMod");
        _stateType = mod.GetType("MyMod.MyModCode.Combat.ManaState", true)!;
        _manifest = EleanorModBootstrap.ManifestJson;
        string requested = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_V040_FORM") ?? "";
        _selectedForm = Enum.TryParse(requested, ignoreCase: false, out ManaStanceChoice form) ? form
            : throw new InvalidOperationException("Set SIMULATORLAB_ELEANOR_V040_FORM to Mentor or Reverse before root capture.");
        if (_selectedForm == ManaStanceChoice.Neutral)
            throw new InvalidOperationException("A Neutral opening must branch to Mentor or Reverse.");
        Type cardType = mod.GetType("MyMod.MyModCode.Cards.GuidingPower", true)!;
        Type choiceBase = mod.GetType("MyMod.MyModCode.Cards.FormChoice", true)!;
        Type white = mod.GetType("MyMod.MyModCode.Cards.WhiteChoice", true)!;
        Type black = mod.GetType("MyMod.MyModCode.Cards.BlackChoice", true)!;
        RegisterOffensiveDefense(mod, _manifest);
        RegisterManaCardMirror(mod, "ManaStrike", "ManaGather", nameof(DispatchManaStrike));
        RegisterManaCardMirror(mod, "ManaGather", "ManaStrike", nameof(DispatchManaGather));
        RegisterManaCardMirror(mod, "ManaShield", "ManaStrike", nameof(DispatchManaShield));
        RegisterRecordingPowers(mod, _manifest);
        RegisterTemporaryRecordingTurnStart(mod, _manifest);
        RegisterCrystalOrbTurnStart(mod, _manifest);
        RegisterGuidingPower(mod, cardType, choiceBase, white, black, _manifest);
        VerifyLatePowerHookPatchInvalidatesRoot();
        VerifyLateTurnStartPatchInvalidatesRoot();
        _ = AdaptedCardOnPlayMirrors.CaptureLiveStamp();
    }

    public static object Run(CombatState combat, MainLoopContext loop, string outputDirectory)
    {
        _loop = loop;
        VanillaGameActionProbe.InstallGodotClockShim();
        if (!TestMode.IsOn || SteamInitializer.Initialized)
            throw new InvalidOperationException("Eleanor v0.4 probe requires isolated TestMode without Steam.");
        object saveStore = typeof(SaveManager).GetField("_saveStore", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(SaveManager.Instance) ?? throw new InvalidOperationException("Could not verify active save backend.");
        if (saveStore.GetType().Name != "MemorySaveStore")
            throw new InvalidOperationException($"Eleanor v0.4 refuses save backend {saveStore.GetType().FullName}.");
        InstallOfflineWriteGuards();

        Player player = LocalContext.GetMe(combat) ?? throw new InvalidOperationException("No local Eleanor player.");
        if (player.Character.Id.Entry != "MY_MOD") throw new InvalidOperationException("Expected MY_MOD character.");
        PlayerCombatState playerState = player.PlayerCombatState ?? throw new InvalidOperationException("PlayerCombatState missing.");
        if (combat.CurrentSide != CombatSide.Player || playerState.Phase != PlayerTurnPhase.Play)
            throw new InvalidOperationException("Capture requires a stable player decision boundary.");

        Assembly mod = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MyMod");
        Type stateType = mod.GetType("MyMod.MyModCode.Combat.ManaState", true)!;
        Type runtimeType = mod.GetType("MyMod.MyModCode.Combat.ManaRuntime", true)!;
        object runtime = runtimeType.GetMethod("For", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [player])!;
        _stateType = stateType;
        _manifest = EleanorModBootstrap.ManifestJson;

        Type guideType = mod.GetType("MyMod.MyModCode.Cards.GuidingPower", true)!;
        Type formBaseType = mod.GetType("MyMod.MyModCode.Cards.FormChoice", true)!;
        Type whiteChoiceType = mod.GetType("MyMod.MyModCode.Cards.WhiteChoice", true)!;
        Type blackChoiceType = mod.GetType("MyMod.MyModCode.Cards.BlackChoice", true)!;
        Type manaStanceType = mod.GetType("MyMod.MyModCode.Combat.ManaStance", true)!;
        Type relicType = mod.GetType("MyMod.MyModCode.Relics.CrackedCrystalOrb", true)!;
        RelicModel orb = player.Relics.Single(relic => relic.GetType() == relicType);
        PoolFirstDrawPreflight poolAndFirstDrawPreflight = VerifyStarterPoolAndFirstDraw(player, playerState, guideType, relicType, combat);
        File.WriteAllText(Path.Combine(outputDirectory, "eleanor-v041-pool-firstdraw-preflight.json"),
            JsonSerializer.Serialize(poolAndFirstDrawPreflight, new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true }));
        object liveState = runtimeType.GetProperty("State")?.GetValue(runtime)
            ?? throw new InvalidOperationException("ManaRuntime.State missing.");
        V040Snapshot opening = CaptureState(stateType, liveState);
        if (opening.Stance != "Neutral" || opening.Echo != 0m)
            throw new PredictionUnsupportedException($"Form-choice fixture requires neutral, no-Echo opening; got stance={opening.Stance}, Echo={opening.Echo}.");

        string requestedForm = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_V040_FORM") ?? "";
        _selectedForm = Enum.TryParse(requestedForm, ignoreCase: false, out ManaStanceChoice form) ? form
            : throw new InvalidOperationException("Set SIMULATORLAB_ELEANOR_V040_FORM to Mentor or Reverse.");
        if (_selectedForm == ManaStanceChoice.Neutral)
            throw new InvalidOperationException("A Neutral opening must branch to Mentor or Reverse.");

        var guide = playerState.Hand.Cards.FirstOrDefault(card => card.GetType() == guideType);
        if (guide == null)
        {
            CardModel canonical = ModelDb.All.OfType<CardModel>().Single(card => card.GetType() == guideType);
            guide = combat.CreateCard(canonical, player);
            Task<CardPileAddResult> add = CardPileCmd.Add(guide, PileType.Hand);
            loop.RunUntilCompleted(add, TimeSpan.FromSeconds(30), "Add native GuidingPower fixture");
            if (!add.GetAwaiter().GetResult().success) throw new InvalidOperationException("Game rejected GuidingPower fixture.");
        }
        if (!guide.CanPlay()) throw new PredictionUnsupportedException("GuidingPower is not playable at the opening boundary.");

        var adapter = new VanillaCombatRootAdapter(combat, "LAB_V0111_ELEANOR_V040_GUIDING_" + requestedForm.ToUpperInvariant());
        CombatRootSnapshot root = CombatRootSnapshot.Capture(combat);
        string nativeBeforeText = ContinuationStamp.CaptureLive(combat).StateText;
        CombatRootV1 nativeBefore = WithExtension(adapter.CaptureLive(combat), opening, requestedForm);
        CombatPredictionSimulator parent = root.ForkSimulator();
        V040PredictionState parentState = GetState(parent, orb, opening);
        V040Snapshot parentBefore = parentState.Capture();
        CombatRootV1 predictedBefore = WithExtension(adapter.CapturePredicted(combat, player, parent,
            playerState.TurnNumber, root.Forecast, root.StartTurnNumber), parentBefore, requestedForm);
        bool rootMatchesBefore = StateDigest.Compute(nativeBefore) == StateDigest.Compute(predictedBefore)
            && StringComparer.Ordinal.Equals(nativeBeforeText, ContinuationStamp.CapturePredicted(player, parent,
                playerState.TurnNumber, root.Forecast, root.StartTurnNumber).StateText);
        if (!rootMatchesBefore) throw new PredictionUnsupportedException("Native and simulator opening roots differ before GuidingPower.");

        CombatPredictionSimulator branch = parent.Fork();
        SimulatedCombatState branchCombat = (SimulatedCombatState)branch.State.CombatState;
        PredictedCard predictedGuide = branch.State.FindCard(guide)
            ?? throw new PredictionUnsupportedException("CombatSolver fork omitted GuidingPower.");
        if (!branchCombat.CanPlayCard(branch, predictedGuide, out _, out _))
            throw new PredictionUnsupportedException("CombatSolver does not consider GuidingPower playable.");
        if (!branch.ManualPlay(predictedGuide, null, out _))
            throw new PredictionUnsupportedException("CombatSolver rejected GuidingPower.");
        _ = branch.CheckWinCondition(playerState.TurnNumber);
        V040Snapshot predictedAfter = GetState(branch, GetBranchOrb(branch, player), opening).Capture();
        V040Snapshot parentAfterBranch = GetState(parent, orb, opening).Capture();
        bool parentUnchanged = parentBefore == parentAfterBranch;

        CardChoiceSelector selector = new(_selectedForm, formBaseType, formBaseType.GetProperty("Form")!);
        int energyBefore = playerState.Energy;
        bool nativeAccepted;
        object nativeAfter;
        CombatRootV1 nativeAfterRoot;
        string nativeAfterText;
        using (CardSelectCmd.UseSelector(selector))
        {
            nativeAccepted = guide.TryManualPlay(null);
            if (!nativeAccepted) throw new PredictionUnsupportedException("Native game rejected GuidingPower.");
            loop.RunUntilCompleted(RunManager.Instance.ActionExecutor.FinishedExecutingActions(), TimeSpan.FromSeconds(30),
                "Native GuidingPower choice and form switch");
            nativeAfter = CaptureState(stateType, runtimeType.GetProperty("State")!.GetValue(runtime)!);
            nativeAfterText = ContinuationStamp.CaptureLive(combat).StateText;
            nativeAfterRoot = WithExtension(adapter.CaptureLive(combat), (V040Snapshot)nativeAfter, requestedForm);
        }
        CombatRootV1 predictedAfterRoot = WithExtension(adapter.CapturePredicted(combat, player, branch,
            playerState.TurnNumber, root.Forecast, root.StartTurnNumber), predictedAfter, requestedForm);
        string predictedAfterText = ContinuationStamp.CapturePredicted(player, branch,
            playerState.TurnNumber, root.Forecast, root.StartTurnNumber).StateText;
        List<string> differences = DescribeRootDifferences(nativeAfterRoot, predictedAfterRoot);
        bool handReturn = playerState.Hand.Cards.Contains(guide);
        int actualEnergyPaid = energyBefore - playerState.Energy;
        string chosenNative = selector.Selected?.GetType().Name ?? "none";
        V040Snapshot nativeSnapshot = (V040Snapshot)nativeAfter;
        string[] expectedChoices = new[] { "NeutralChoice", "WhiteChoice", "BlackChoice" }
            .Where(name => name != (opening.Stance == "Neutral" ? "NeutralChoice" : ""))
            .OrderBy(name => name, StringComparer.Ordinal).ToArray();
        bool exact = nativeAccepted && selector.AvailableTypes.SequenceEqual(expectedChoices)
            && chosenNative == (_selectedForm == ManaStanceChoice.Mentor ? "WhiteChoice" : "BlackChoice")
            && nativeSnapshot.Stance == requestedForm && predictedAfter.Stance == requestedForm
            && nativeSnapshot.Mana == predictedAfter.Mana && nativeSnapshot.Echo == predictedAfter.Echo
            && actualEnergyPaid == 2 && handReturn && parentUnchanged && differences.Count == 0
            && StringComparer.Ordinal.Equals(nativeAfterText, predictedAfterText);

        object keywords = guide.GetType().GetProperty("CanonicalKeywords")!.GetValue(guide)!;
        string[] keywordNames = ((IEnumerable)keywords).Cast<object>().Select(item => item.ToString() ?? "").ToArray();
        object resultLocation = guide.GetType().GetMethod("GetResultLocationForCardPlay", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(guide, null)!;
        string resultPile = resultLocation.GetType().GetField("pileType")!.GetValue(resultLocation)!.ToString()!;
        var report = new
        {
            schema = "eleanor-v040-guiding-form-differential/v1",
            status = exact ? "passed" : "failed",
            gameVersion = GameBuildPin.Version,
            gameCommit = GameBuildPin.Commit,
            modVersion = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_VERSION"),
            modAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_DLL")!))).ToLowerInvariant(),
            modManifestSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_manifest))).ToLowerInvariant(),
            testMode = TestMode.IsOn,
            steamInitialized = SteamInitializer.Initialized,
            saveStore = saveStore.GetType().Name,
            gameInstallWrites = false,
            selectedForm = requestedForm,
            opening = opening,
            nativeAfter = nativeSnapshot,
            simulatorAfter = predictedAfter,
            actualEnergyPaid,
            resultPile,
            keywordNames,
            chosenNativeChoice = chosenNative,
            offeredNativeChoices = selector.AvailableTypes,
            nativeHandReturn = handReturn,
            parentBranchUnchanged = parentUnchanged,
            openingRootsMatch = rootMatchesBefore,
            postActionRootDifferences = differences,
            nativeContinuationSha256 = Sha(nativeAfterText),
            simulatorContinuationSha256 = Sha(predictedAfterText),
            continuationMatches = StringComparer.Ordinal.Equals(nativeAfterText, predictedAfterText),
            registrationGuards = RegistrationEvidence,
            exactStateMatch = exact,
        };
        File.WriteAllText(Path.Combine(outputDirectory, "eleanor-v040-guiding-form-differential.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true }));
        if (!exact) throw new PredictionUnsupportedException("GuidingPower native/simulator comparison diverged; inspect its v0.4 differential report.");
        object offensiveDefense = RunOffensiveDefense(combat, loop, player, playerState, orb, adapter, outputDirectory);
        object recordEcho = RunRecordEchoFixture(combat, loop, player, playerState, orb, adapter, outputDirectory);
        V040Snapshot postScheduledBoundary = CaptureState(_stateType!, GetLiveManaState(player));
        object reverseUpkeep = _selectedForm != ManaStanceChoice.Reverse
            ? new { status = "not-applicable" }
            : postScheduledBoundary.Mana >= 2
                ? RunReverseUpkeepFixture(combat, loop, player, playerState, orb, adapter, outputDirectory)
                : new
                {
                    status = "not-run",
                    reason = "The real scheduled turn-boundary fixture already spent Reverse upkeep, leaving fewer than two Mana for the separate success-plus-failure boundary fixture.",
                    manaAfterScheduledBoundary = postScheduledBoundary.Mana,
                    equivalentPinnedDirectBoundaryEvidence = ".build/runs/eleanor-v041-20261101-150722-360/reverse/eleanor-v040-reverse-upkeep-boundary.json",
                };
        var offlineWriteGuards = new
        {
            status = "passed",
            saveStore = saveStore.GetType().Name,
            replayWriteTarget = "RunManager.WriteReplay(Boolean)",
            progressSaveTarget = "ProgressSaveManager.SaveProgress()",
            interceptedReplayWrites = _suppressedReplayWrites,
            interceptedProgressSaves = _suppressedProgressSaves,
            persistentWritesAllowed = false,
        };
        File.WriteAllText(Path.Combine(outputDirectory, "eleanor-v041-offline-write-guards.json"),
            JsonSerializer.Serialize(offlineWriteGuards, new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true }));
        string overallStatus = poolAndFirstDrawPreflight.status == "passed" ? "passed" : "unsupported";
        return new { status = overallStatus, exactStateMatch = true, offlineWriteGuards, poolAndFirstDrawPreflight, guidingPower = report, offensiveDefense, recordEcho, reverseUpkeep };
    }

    public static object RunVictoryReplay(CombatState combat, MainLoopContext loop, string outputDirectory, string planPath, bool allowBoundary = false)
    {
        _loop = loop;
        VanillaGameActionProbe.InstallGodotClockShim();
        if (!TestMode.IsOn || SteamInitializer.Initialized)
            throw new InvalidOperationException("Eleanor victory replay requires isolated TestMode without Steam.");
        object saveStore = typeof(SaveManager).GetField("_saveStore", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(SaveManager.Instance) ?? throw new InvalidOperationException("Could not verify active save backend.");
        if (saveStore.GetType().Name != "MemorySaveStore")
            throw new InvalidOperationException($"Victory replay refuses save backend {saveStore.GetType().FullName}.");
        InstallOfflineWriteGuards();
        int replayCallsBeforeBattle = _suppressedReplayWrites;
        int progressCallsBeforeBattle = _suppressedProgressSaves;

        string planFullPath = Path.GetFullPath(planPath);
        string planSha256 = File.Exists(planFullPath)
            ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(planFullPath))).ToLowerInvariant() : "";
        string currentModSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(
            Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_DLL")!))).ToLowerInvariant();
        int planActionCount = 0;
        string planResultPath = Path.Combine(Path.GetDirectoryName(planFullPath)!, "harness-result.json");
        if (!File.Exists(planFullPath) || !File.Exists(planResultPath))
            return WritePreflightUnsupported("The pinned route plan or its source harness-result.json is missing.");
        using (JsonDocument planSource = JsonDocument.Parse(File.ReadAllText(planResultPath)))
        {
            JsonElement sourceRoot = planSource.RootElement;
            string sourceAssemblySha = sourceRoot.GetProperty("eleanorMod").GetProperty("assemblySha256").GetString() ?? "";
            string sourceCharacter = sourceRoot.GetProperty("scenario").GetProperty("CharacterId").GetString() ?? "";
            string sourceEncounter = sourceRoot.GetProperty("scenario").GetProperty("EncounterId").GetString() ?? "";
            string sourceSeed = sourceRoot.GetProperty("scenario").GetProperty("Seed").GetString() ?? "";
            string expectedEncounter = NativeModRunLoop.Enabled ? ((CombatRoom)combat.RunState.CurrentRoom!).Encounter.Id.Entry : "FUZZY_WURM_CRAWLER_WEAK";
            string expectedSeed = "LAB_V0111_ELEANOR_V040_FORM_01";
            if (!StringComparer.OrdinalIgnoreCase.Equals(sourceAssemblySha, currentModSha256)
                || sourceCharacter != "MyMod" || sourceEncounter != expectedEncounter
                || sourceSeed != expectedSeed)
                return WritePreflightUnsupported("The existing route plan is not tied to this locked v0.4.1 assembly and exact combat seed.");
        }

        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(planFullPath));
        if (plan.RootElement.ValueKind != JsonValueKind.Array)
            return WritePreflightUnsupported("The fixed search plan is not a JSON action array.");
        JsonElement[] actions = plan.RootElement.EnumerateArray().ToArray();
        planActionCount = actions.Length;
        if (actions.Length == 0) return WritePreflightUnsupported("The fixed search plan contains no actions.");

        Player player = LocalContext.GetMe(combat) ?? throw new InvalidOperationException("No local Eleanor player.");
        if (player.Character.Id.Entry != "MY_MOD") throw new InvalidOperationException("Expected MY_MOD character.");
        PlayerCombatState playerState = player.PlayerCombatState ?? throw new InvalidOperationException("PlayerCombatState missing.");
        if (combat.CurrentSide != CombatSide.Player || playerState.Phase != PlayerTurnPhase.Play)
            throw new PredictionUnsupportedException("The fixed route replay did not begin at a player Play boundary.");
        Assembly mod = AppDomain.CurrentDomain.GetAssemblies().Single(assembly => assembly.GetName().Name == "MyMod");
        _stateType = mod.GetType("MyMod.MyModCode.Combat.ManaState", true)!;
        _manifest = EleanorModBootstrap.ManifestJson;
        string requestedForm = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_V040_FORM") ?? "";
        if (!Enum.TryParse(requestedForm, ignoreCase: false, out _selectedForm) || _selectedForm == ManaStanceChoice.Neutral)
            return WritePreflightUnsupported("Set the fixed route form to Mentor or Reverse before replay.");
        object liveState = GetLiveManaState(player);
        V040Snapshot opening = CaptureState(_stateType, liveState);
        if (!allowBoundary && (opening.Stance != "Neutral" || (opening.Echo != 0m && !NativeModRunLoop.RecordFixtureEnabled)))
            return WritePreflightUnsupported($"Native route opening is not an allowed Neutral fixture state: {opening}.");

        Type guideType = mod.GetType("MyMod.MyModCode.Cards.GuidingPower", true)!;
        Type formChoiceType = mod.GetType("MyMod.MyModCode.Cards.FormChoice", true)!;
        RelicModel nativeOrb = player.Relics.Single(relic => relic.GetType().Name is "CrackedCrystalOrb" or "CompleteCrystalOrb");
        var adapter = new VanillaCombatRootAdapter(combat, "LAB_V0111_ELEANOR_V041_VICTORY_REPLAY_" + requestedForm.ToUpperInvariant());
        InstallVictoryHeadlessTerminalCapture(combat, player, adapter, requestedForm);
        CombatRootSnapshot root = CombatRootSnapshot.Capture(combat);
        string nativeOpeningText = ContinuationStamp.CaptureLive(combat).StateText;
        CombatRootV1 nativeOpeningRoot = WithExtension(adapter.CaptureLive(combat), opening, requestedForm);
        CombatPredictionSimulator parent = root.ForkSimulator();
        RelicModel parentOrb = GetBranchOrb(parent, player);
        V040PredictionState parentState = GetState(parent, parentOrb, opening);
        V040Snapshot parentOpening = parentState.Capture();
        CombatPredictionSimulator branch = parent.Fork();
        RelicModel branchOrb = GetBranchOrb(branch, player);
        _ = GetState(branch, branchOrb, opening);
        CombatRootV1 simulatorOpeningRoot = WithExtension(adapter.CapturePredicted(combat, player, parent,
            playerState.TurnNumber, root.Forecast, root.StartTurnNumber), parentOpening, requestedForm);
        string simulatorOpeningText = ContinuationStamp.CapturePredicted(player, parent,
            playerState.TurnNumber, root.Forecast, root.StartTurnNumber).StateText;
        bool openingExact = DescribeRootDifferences(nativeOpeningRoot, simulatorOpeningRoot).Count == 0
            && StringComparer.Ordinal.Equals(nativeOpeningText, simulatorOpeningText)
            && CombatCaptureValidator.ValidateExactReady(nativeOpeningRoot).IsExactReady
            && CombatCaptureValidator.ValidateExactReady(simulatorOpeningRoot).IsExactReady;
        var stepReports = new List<object>();
        string? stopReason = null;
        int stopActionIndex = -1;
        string status = openingExact ? "running" : "failed";
        bool nativeVictory = false;
        bool simulatorVictory = false;
        bool nativeDeath = false;
        bool simulatorDeath = false;
        string currentNativeText = nativeOpeningText;
        string currentSimulatorText = simulatorOpeningText;
        V040Snapshot currentNativeState = opening;
        V040Snapshot currentSimulatorState = parentOpening;
        var driver = new CombatBeamSolver(root, SolverDisplayNames.Capture(combat), BattleDamageTracker.Observe(combat),
            SolverController.CaptureSearchPolicy(SolverSettings.Capture(), combat, false, null));
        MethodInfo advanceRound = typeof(CombatBeamSolver).GetMethod("AdvanceRound", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(CombatBeamSolver), "AdvanceRound");
        var shuffledCards = new HashSet<uint>();
        int shuffleCount = 0;
        Creature[] trackedEnemies = combat.Enemies.ToArray();
        int nativeCombatWonEvents = 0;
        bool nativeCombatWonForExpectedRoom = false;
        CombatRoom expectedRoom = combat.RunState.CurrentRoom as CombatRoom
            ?? throw new PredictionUnsupportedException("Native fixed-route replay has no active CombatRoom.");
        void ObserveCombatWon(CombatRoom room)
        {
            nativeCombatWonEvents++;
            nativeCombatWonForExpectedRoom = ReferenceEquals(room, expectedRoom);
        }
        CombatManager.Instance.CombatWon += ObserveCombatWon;

        try
        {
        for (int index = 0; index < actions.Length && status == "running"; index++)
        {
            if (NativeModRunLoop.Enabled) NativeModRunLoop.AssertDiscoveryStable();
            stopActionIndex = index;
            JsonElement action = actions[index];
            string kind = action.GetProperty("kind").GetString() ?? "";
            int actionTurn = action.GetProperty("turn").GetInt32();
            string cardId = action.GetProperty("cardId").GetString() ?? "";
            int targetIndex = action.GetProperty("targetIndex").GetInt32();
            int cardOccurrence = action.GetProperty("cardOccurrence").GetInt32();
            int liveTurn = playerState.TurnNumber;
            if (actionTurn != liveTurn)
            {
                status = "unsupported";
                stopReason = $"Fixed plan turn {actionTurn} does not match actual turn {liveTurn} at action {index + 1}.";
                break;
            }

            V040Snapshot beforeNative = CaptureState(_stateType, GetLiveManaState(player));
            RelicModel beforeBranchOrb = GetBranchOrb(branch, player);
            V040Snapshot beforeSimulator = GetState(branch, beforeBranchOrb, opening).Capture();
            CombatRootV1 beforeNativeRoot = WithExtension(adapter.CaptureLive(combat), beforeNative, requestedForm);
            CombatRootV1 beforeSimulatorRoot = WithExtension(adapter.CapturePredicted(combat, player, branch,
                liveTurn, root.Forecast, root.StartTurnNumber), beforeSimulator, requestedForm);
            string beforeNativeText = ContinuationStamp.CaptureLive(combat).StateText;
            string beforeSimulatorText = ContinuationStamp.CapturePredicted(player, branch, liveTurn,
                root.Forecast, root.StartTurnNumber).StateText;
            List<string> beforeDifferences = DescribeRootDifferences(beforeNativeRoot, beforeSimulatorRoot);
            if (beforeDifferences.Count != 0 || !StringComparer.Ordinal.Equals(beforeNativeText, beforeSimulatorText)
                || !CombatCaptureValidator.ValidateExactReady(beforeNativeRoot).IsExactReady
                || !CombatCaptureValidator.ValidateExactReady(beforeSimulatorRoot).IsExactReady)
            {
                status = "failed";
                stopReason = $"Native/simulator roots diverged before planned action {index + 1} ({kind}/{cardId}).";
                break;
            }

            bool simulatedAccepted = false;
            bool nativeAccepted = false;
            decimal targetHpBefore = 0m;
            string targetName = "";
            string executionStage = "resolve-action";
            CombatRootV1? predictedTerminalRoot = null;
            string? predictedTerminalText = null;
            try
            {
                if (kind == "PlayCard")
                {
                    executionStage = "resolve-native-card-instance";
                    CardModel[] nativeMatches = playerState.Hand.Cards
                        .Where(card => card.Id.Entry == cardId).ToArray();
                    if (cardOccurrence < 0 || cardOccurrence >= nativeMatches.Length)
                        throw new PredictionUnsupportedException($"Planned card {cardId} occurrence {cardOccurrence} is not in the native hand.");
                    CardModel nativeCard = nativeMatches[cardOccurrence];
                    NativeModRunLoop.VerifySecondRewardCard(nativeCard);
                    PredictedCard predictedCard = branch.State.FindCard(nativeCard)
                        ?? throw new PredictionUnsupportedException($"Simulator branch cannot resolve the planned {cardId} card instance.");
                    Creature? target = null;
                    if (targetIndex >= 0)
                    {
                        Creature[] livingEnemies = combat.Enemies.Where(enemy => enemy.IsAlive)
                            .Select(enemy => (Creature)enemy).ToArray();
                        if (targetIndex >= livingEnemies.Length)
                            throw new PredictionUnsupportedException($"Planned target index {targetIndex} is not a living enemy.");
                        target = livingEnemies[targetIndex];
                        targetName = combat.Enemies.First(enemy => ReferenceEquals(enemy, target)).Monster?.Id.Entry
                            ?? target.GetType().Name;
                        targetHpBefore = target.CurrentHp;
                    }
                    SimulatedCombatState simulatedCombat = branch.State.CombatState as SimulatedCombatState
                        ?? throw new PredictionUnsupportedException("Route replay branch has no SimulatedCombatState.");
                    if (!simulatedCombat.CanPlayCard(branch, predictedCard, out _, out _))
                        throw new PredictionUnsupportedException($"Simulator rejects planned {cardId} as unplayable at turn {liveTurn}.");
                    executionStage = "simulator-manual-play";
                    if (!branch.ManualPlay(predictedCard, target, out _))
                        throw new PredictionUnsupportedException($"Simulator rejects planned {cardId} at turn {liveTurn}.");
                    simulatedAccepted = true;
                    executionStage = "simulator-win-check";
                    _ = branch.CheckWinCondition(liveTurn);
                    if (branch.TerminalStamp?.Outcome == CombatTerminalOutcome.Victory)
                    {
                        V040Snapshot predictedMana = GetState(branch, GetBranchOrb(branch, player), opening).Capture();
                        predictedTerminalRoot = WithExtension(adapter.CapturePredicted(combat, player, branch,
                            liveTurn, root.Forecast, root.StartTurnNumber, terminalVictory: true), predictedMana, requestedForm);
                        predictedTerminalText = ContinuationStamp.CapturePredicted(player, branch, liveTurn,
                            root.Forecast, root.StartTurnNumber).StateText;
                    }

                    executionStage = "native-manual-play";
                    if (nativeCard.GetType() == guideType)
                    {
                        var selector = new CardChoiceSelector(_selectedForm, formChoiceType,
                            formChoiceType.GetProperty("Form") ?? throw new MissingMemberException(formChoiceType.FullName, "Form"));
                        using (CardSelectCmd.UseSelector(selector)) nativeAccepted = nativeCard.TryManualPlay(target);
                    }
                    else nativeAccepted = nativeCard.TryManualPlay(target);
                    if (!nativeAccepted) throw new PredictionUnsupportedException($"Native game rejected planned {cardId} at turn {liveTurn}.");
                    executionStage = "native-action-drain";
                    loop.RunUntilCompleted(RunManager.Instance.ActionExecutor.FinishedExecutingActions(), TimeSpan.FromSeconds(45),
                        $"Native fixed-plan {cardId} action {index + 1}");
                    if (_victoryTerminalCaptureError is not null)
                        throw new InvalidOperationException("Read-only native Victory capture failed before game teardown.", _victoryTerminalCaptureError);
                }
                else if (kind == "EndTurn")
                {
                    executionStage = "simulator-end-turn";
                    SimulatedCombatState shadow = branch.State.CombatState as SimulatedCombatState
                        ?? throw new PredictionUnsupportedException("Route replay branch has no simulated state at EndTurn.");
                    _lastTurnStartSimulator = null;
                    _lastTurnStartSnapshot = null;
                    _lastTurnStartState = null;
                    _lastTurnStartNumber = 0;
                    object?[] args = [branch, shadow, liveTurn - root.StartTurnNumber, shuffledCards, shuffleCount, null, null];
                    object? boundary = advanceRound.Invoke(driver, args);
                    if (!StringComparer.Ordinal.Equals(boundary?.ToString(), "None"))
                        throw new PredictionUnsupportedException($"Simulator stopped at the planned turn boundary: {boundary}.");
                    branch = _lastTurnStartSimulator
                        ?? throw new PredictionUnsupportedException("Simulator did not produce the next player-turn branch.");
                    if (_lastTurnStartNumber != liveTurn + 1 || _lastTurnStartSnapshot is null)
                        throw new PredictionUnsupportedException($"Simulator turn-start callback reached turn {_lastTurnStartNumber}, expected {liveTurn + 1}.");
                    SimulatedCombatState nextShadow = branch.State.CombatState as SimulatedCombatState
                        ?? throw new PredictionUnsupportedException("Next-turn branch omitted SimulatedCombatState.");
                    _ = nextShadow.ConsumePlayerTurnEndRequest();
                    if (!CombatBeamSolver.SettleReplayActionBoundary(branch, nextShadow))
                        throw new PredictionUnsupportedException("Simulator left an unresolved choice at EndTurn.");
                    shuffleCount = args[4] is int nextShuffleCount ? nextShuffleCount : shuffleCount;
                    executionStage = "native-end-turn-enqueue";
                    CombatManager.Instance.OnEndedTurnLocally();
                    RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new EndPlayerTurnAction(player, liveTurn));
                    executionStage = "native-enemy-phase-drain";
                    loop.RunUntilCompleted(RunManager.Instance.ActionExecutor.FinishedExecutingActions(), TimeSpan.FromSeconds(45),
                        $"Native fixed-plan EndTurn {liveTurn} including enemy phase");
                    if (player.PlayerCombatState is not { Phase: PlayerTurnPhase.Play } nextState || nextState.TurnNumber != liveTurn + 1)
                        throw new PredictionUnsupportedException("Native EndPlayerTurn did not reach the next player Play boundary.");
                    simulatedAccepted = true;
                    nativeAccepted = true;
                }
                else throw new PredictionUnsupportedException($"Unsupported fixed-plan action kind '{kind}'.");
            }
            catch (PredictionUnsupportedException ex)
            {
                status = "unsupported";
                stopReason = ex.Message;
                break;
            }
            catch (Exception ex)
            {
                status = "failed";
                string liveAfterFailure;
                try
                {
                    liveAfterFailure = $"playerHp={player.Creature.CurrentHp}; enemies=[{string.Join(",", combat.Enemies.Select(enemy => $"{enemy.Monster?.Id.Entry ?? enemy.GetType().Name}:{enemy.CurrentHp}:{enemy.IsAlive}"))}]";
                }
                catch (Exception stateError) { liveAfterFailure = $"liveStateRead={stateError.GetType().Name}:{stateError.Message}"; }
                stopReason = $"Action {index + 1} failed during {executionStage}: {ex}\nstateAfterFailure={liveAfterFailure}";
                break;
            }

            if (_victoryTerminalCaptureError is not null)
                throw new InvalidOperationException("Read-only native Victory capture failed before game teardown.", _victoryTerminalCaptureError);
            VictoryTerminalCapture? terminalEvidence = _victoryTerminalEvidence;
            int afterTurn = player.PlayerCombatState?.TurnNumber ?? liveTurn;
            V040Snapshot afterNative = terminalEvidence?.Mana ?? CaptureState(_stateType, GetLiveManaState(player));
            V040Snapshot afterSimulator = kind == "EndTurn" && _lastTurnStartSnapshot is not null
                ? _lastTurnStartSnapshot
                : GetState(branch, GetBranchOrb(branch, player), opening).Capture();
            nativeVictory = terminalEvidence is { VictoryConditionsSatisfied: true }
                && nativeCombatWonEvents == 1 && nativeCombatWonForExpectedRoom && !CombatManager.Instance.IsInProgress;
            simulatorVictory = branch.TerminalStamp?.Outcome == CombatTerminalOutcome.Victory;
            nativeDeath = (terminalEvidence?.PlayerHp ?? player.Creature.CurrentHp) <= 0;
            simulatorDeath = !branch.State.GetCreature(player.Creature).IsAlive;
            CombatRootV1 afterNativeRoot = terminalEvidence?.Root
                ?? WithExtension(adapter.CaptureLive(combat, terminalVictory: nativeVictory), afterNative, requestedForm);
            CombatRootV1 afterSimulatorRoot = predictedTerminalRoot
                ?? WithExtension(adapter.CapturePredicted(combat, player, branch,
                    afterTurn, root.Forecast, root.StartTurnNumber, terminalVictory: simulatorVictory), afterSimulator, requestedForm);
            string afterNativeText = terminalEvidence?.StateText ?? ContinuationStamp.CaptureLive(combat).StateText;
            string afterSimulatorText = predictedTerminalText ?? ContinuationStamp.CapturePredicted(player, branch, afterTurn,
                root.Forecast, root.StartTurnNumber).StateText;
            List<string> differences = DescribeRootDifferences(afterNativeRoot, afterSimulatorRoot);
            CaptureValidationV1 nativeRootValidation = CombatCaptureValidator.ValidateExactReady(afterNativeRoot);
            CaptureValidationV1 simulatorRootValidation = CombatCaptureValidator.ValidateExactReady(afterSimulatorRoot);
            CombatRootV1 parentAfterRoot = WithExtension(adapter.CapturePredicted(combat, player, parent,
                root.StartTurnNumber, root.Forecast, root.StartTurnNumber), GetState(parent, parentOrb, opening).Capture(), requestedForm);
            string parentAfterText = ContinuationStamp.CapturePredicted(player, parent,
                root.StartTurnNumber, root.Forecast, root.StartTurnNumber).StateText;
            List<string> parentDifferences = DescribeRootDifferences(simulatorOpeningRoot, parentAfterRoot);
            bool parentUnchanged = parentOpening == GetState(parent, parentOrb, opening).Capture()
                && parentDifferences.Count == 0 && StringComparer.Ordinal.Equals(simulatorOpeningText, parentAfterText);
            bool exact = simulatedAccepted && nativeAccepted && differences.Count == 0
                && StringComparer.Ordinal.Equals(afterNativeText, afterSimulatorText) && parentUnchanged
                && nativeRootValidation.IsExactReady && simulatorRootValidation.IsExactReady;
            stepReports.Add(new
            {
                index = index + 1, kind, turn = liveTurn, afterTurn, cardId,
                targetIndex, target = targetName, targetHpBefore,
                simulatedAccepted, nativeAccepted,
                nativeMana = afterNative, simulatorMana = afterSimulator,
                nativePlayerHp = terminalEvidence?.PlayerHp ?? player.Creature.CurrentHp,
                simulatorPlayerHp = branch.State.GetCreature(player.Creature).CurrentHp,
                nativeEnemies = terminalEvidence?.Enemies ?? combat.Enemies.Select(enemy => (object)new
                    { id = enemy.Monster?.Id.Entry ?? enemy.GetType().Name, hp = enemy.CurrentHp, alive = enemy.IsAlive }).ToArray(),
                simulatorEnemyHp = combat.Enemies.Select(enemy => branch.State.GetCreature(enemy).CurrentHp).ToArray(),
                trackedEnemyHp = trackedEnemies.Select(enemy => new
                {
                    id = enemy.Monster?.Id.Entry ?? enemy.GetType().Name,
                    native = enemy.CurrentHp, simulator = branch.State.GetCreature(enemy).CurrentHp,
                }).ToArray(),
                beforeNativeRoot, beforeSimulatorRoot, nativeRoot = afterNativeRoot, simulatorRoot = afterSimulatorRoot,
                beforeNativeContinuation = beforeNativeText, beforeSimulatorContinuation = beforeSimulatorText,
                nativeContinuation = afterNativeText, simulatorContinuation = afterSimulatorText,
                postActionRootDifferences = differences,
                nativeRootValidation, simulatorRootValidation,
                parentRoot = parentAfterRoot, parentContinuation = parentAfterText,
                parentRootDifferences = parentDifferences,
                nativeContinuationSha256 = Sha(afterNativeText),
                simulatorContinuationSha256 = Sha(afterSimulatorText),
                continuationMatches = StringComparer.Ordinal.Equals(afterNativeText, afterSimulatorText),
                parentBranchUnchanged = parentUnchanged,
                exactStateMatch = exact,
            });
            currentNativeState = afterNative;
            currentSimulatorState = afterSimulator;
            currentNativeText = afterNativeText;
            currentSimulatorText = afterSimulatorText;
            if (!exact)
            {
                status = "failed";
                stopReason = $"Native/simulator state diverged after planned action {index + 1} ({kind}/{cardId}).";
            }
            else if (nativeVictory || simulatorVictory)
            {
                bool safetyBoundaryCovered = _suppressedProgressSaves - progressCallsBeforeBattle == 1 && _suppressedReplayWrites - replayCallsBeforeBattle == 1
                    && _suppressedVictoryHoverClears == 1;
                status = nativeVictory && simulatorVictory && safetyBoundaryCovered ? "passed" : "failed";
                stopReason = nativeVictory != simulatorVictory ? "Only one of native/simulator reached Victory."
                    : safetyBoundaryCovered ? null : "The fixed battle did not exercise each verified save/replay/UI guard exactly once.";
            }
            else if (nativeDeath || simulatorDeath)
            {
                status = nativeDeath && simulatorDeath ? "death" : "failed";
                stopReason = nativeDeath == simulatorDeath ? "Native and simulator both reached Death." : "Only one of native/simulator reached Death.";
            }
            WriteResult(status, stopActionIndex >= 0 ? stopActionIndex + 1 : null, stopReason);
        }

        if (status == "running")
        {
            status = allowBoundary ? "boundary" : "unsupported";
            stopReason = allowBoundary ? "Explicit partial action prefix ended at an exact live boundary; not a terminal pass." : "The fixed search route ended before a native Victory or Death.";
            stopActionIndex = actions.Length;
        }
        return WriteResult(status, stopActionIndex >= 0 ? stopActionIndex + 1 : null, stopReason);
        }
        finally
        {
            CombatManager.Instance.CombatWon -= ObserveCombatWon;
            _captureVictoryTerminal = null;
        }

        object WriteResult(string resultStatus, int? stopAt, string? reason)
        {
            var report = new
            {
                schema = "eleanor-v041-fixed-route-victory-replay/v2",
                status = resultStatus,
                gameVersion = GameBuildPin.Version,
                gameCommit = GameBuildPin.Commit,
                modVersion = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_VERSION"),
                modAssemblySha256 = currentModSha256,
                selectedForm = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_V040_FORM"),
                scenario = new { character = "MyMod", encounter = expectedRoom.Encounter.Id.Entry, seed = "LAB_V0111_ELEANOR_V040_FORM_01" },
                planPath = planFullPath,
                planSha256,
                planActionCount,
                testMode = TestMode.IsOn,
                steamInitialized = SteamInitializer.Initialized,
                saveStore = saveStore.GetType().Name,
                replayAndSaveGuardsInstalled = true,
                interceptedReplayWrites = _suppressedReplayWrites,
                interceptedProgressSaves = _suppressedProgressSaves,
                persistentWritesAllowed = false,
                headlessUiCleanup = new { target = "NHoverTipSet.Clear()", interceptedCalls = _suppressedVictoryHoverClears,
                    behavior = "skip presentation-only hover-tip removal; native combat actions and EndCombatInternal execute" },
                nativeCombatWonEvents,
                nativeCombatWonForExpectedRoom,
                nativeCombatStillInProgress = CombatManager.Instance.IsInProgress,
                nativeVictoryConditionsSatisfied = _victoryTerminalEvidence?.VictoryConditionsSatisfied,
                terminalCaptureBoundary = "read-only prefix before native EndCombatInternal teardown; CombatWon and action drain must complete",
                registrationEvidence = RegistrationEvidence,
                openingExact,
                nativeOpeningRoot, simulatorOpeningRoot, nativeOpeningContinuation = nativeOpeningText,
                simulatorOpeningContinuation = simulatorOpeningText,
                opening = opening,
                nativeFinal = currentNativeState,
                simulatorFinal = currentSimulatorState,
                nativeVictory,
                simulatorVictory,
                nativeDeath,
                simulatorDeath,
                terminalContinuationMatches = StringComparer.Ordinal.Equals(currentNativeText, currentSimulatorText),
                stopAction = stopAt,
                stopReason = reason,
                steps = stepReports,
            };
            File.WriteAllText(Path.Combine(outputDirectory, "eleanor-v041-fixed-route-victory-replay.json"),
                JsonSerializer.Serialize(report, new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true }));
            return report;
        }

        object WritePreflightUnsupported(string reason)
        {
            var report = new
            {
                schema = "eleanor-v041-fixed-route-victory-replay/v1",
                status = "unsupported",
                gameVersion = GameBuildPin.Version,
                gameCommit = GameBuildPin.Commit,
                modVersion = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_VERSION"),
                modAssemblySha256 = currentModSha256,
                planPath = planFullPath,
                planSha256,
                planActionCount,
                saveStore = saveStore.GetType().Name,
                replayAndSaveGuardsInstalled = true,
                persistentWritesAllowed = false,
                stopReason = reason,
            };
            File.WriteAllText(Path.Combine(outputDirectory, "eleanor-v041-fixed-route-victory-replay.json"),
                JsonSerializer.Serialize(report, new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true }));
            return report;
        }
    }

    public static object AddRecordingResetFixture(CombatState combat, MainLoopContext loop)
    {
        Player player=LocalContext.GetMe(combat)!;
        Assembly mod=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="MyMod");
        foreach(var (name,amount) in new[]{("RecordingPower",1m),("TemporaryRecordingPower",2m)})
            loop.RunUntilCompleted(InvokeNativePowerApply(mod.GetType("MyMod.MyModCode.Powers."+name,true)!,player.Creature,amount),TimeSpan.FromSeconds(15),"Native Recording lifecycle fixture");
        V040Snapshot before=CaptureState(_stateType!,GetLiveManaState(player));
        loop.RunUntilCompleted(InvokeNativeBlockGain(player.Creature,1m),TimeSpan.FromSeconds(15),"Native fixture block");
        Task damage=InvokeNativeDamage(player.Creature,1m,(ValueProp)4);
        loop.RunUntilCompleted(damage,TimeSpan.FromSeconds(15),"Native fixture blocked incoming damage");
        V040Snapshot after=CaptureState(_stateType!,GetLiveManaState(player));
        if(after.Echo<=before.Echo) throw new InvalidOperationException("Real Recording fixture did not gain Echo.");
        var results=((IEnumerable)damage.GetType().GetProperty("Result")!.GetValue(damage)!).Cast<object>().Select(result=>new {blocked=ReadDamageDecimal(result,"BlockedDamage"),unblocked=ReadDamageDecimal(result,"UnblockedDamage")}).ToArray();
        return new { kind="real power API fixture; no B cards",before,after,damageResults=results };
    }

    public static object CaptureNextNativeBoundary(CombatState combat)
    {
        Player player = LocalContext.GetMe(combat)!;
        V040Snapshot opening = CaptureState(_stateType!, GetLiveManaState(player));
        var adapter = new VanillaCombatRootAdapter(combat, "ELEANOR_NATIVE_NEXT_COMBAT");
        CombatRootSnapshot captured = CombatRootSnapshot.Capture(combat);
        var parent = captured.ForkSimulator();
        _ = GetState(parent, GetBranchOrb(parent, player), opening);
        var child = parent.Fork();
        var childState = GetState(child, GetBranchOrb(child, player), opening).Capture();
        int turn = player.PlayerCombatState!.TurnNumber;
        var actual = WithExtension(adapter.CaptureLive(combat), opening, "Reverse");
        var predicted = WithExtension(adapter.CapturePredicted(combat, player, child, turn, captured.Forecast, captured.StartTurnNumber), childState, "Reverse");
        string actualText = ContinuationStamp.CaptureLive(combat).StateText;
        string predictedText = ContinuationStamp.CapturePredicted(player, child, turn, captured.Forecast,captured.StartTurnNumber).StateText;
        string parentText = ContinuationStamp.CapturePredicted(player,parent,turn,captured.Forecast,captured.StartTurnNumber).StateText;
        var shadow = (SimulatedCombatState)child.State.CombatState!;
        bool playable = player.PlayerCombatState.Hand.Cards.Any(card => child.State.FindCard(card) is { } c && shadow.CanPlayCard(child,c,out _,out _));
        if (DescribeRootDifferences(actual,predicted).Count!=0 || actualText!=predictedText || actualText!=parentText || !playable
            || !CombatCaptureValidator.ValidateExactReady(actual).IsExactReady || !CombatCaptureValidator.ValidateExactReady(predicted).IsExactReady)
            throw new PredictionUnsupportedException("Next native first-player root is not an exact-ready actionable simulator boundary.");
        return new { status="passed", turn, playable, actual, predicted, actualContinuation=actualText,predictedContinuation=predictedText,parentContinuation=parentText };
    }

    public static object CaptureOfflineWriteEvidence()
    {
        object store=typeof(SaveManager).GetField("_saveStore",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(SaveManager.Instance)!;
        if(store.GetType().Name!="MemorySaveStore"||!TestMode.IsOn||SteamInitializer.Initialized)throw new InvalidOperationException("Unsafe offline save host.");
        return new {saveStore=store.GetType().Name,testMode=TestMode.IsOn,steamInitialized=SteamInitializer.Initialized,
            interceptedReplayWrites=_suppressedReplayWrites,interceptedProgressSaves=_suppressedProgressSaves,persistentWritesAllowed=false};
    }

    private static void InstallOfflineWriteGuards()
    {
        MethodInfo writeReplay = AccessTools.Method(typeof(RunManager), nameof(RunManager.WriteReplay), [typeof(bool)])
            ?? throw new MissingMethodException(nameof(RunManager), nameof(RunManager.WriteReplay));
        Type progressSaveManager = typeof(SaveManager).Assembly.GetType(
            "MegaCrit.Sts2.Core.Saves.Managers.ProgressSaveManager", throwOnError: true)!;
        MethodInfo saveProgress = progressSaveManager.GetMethod("SaveProgress", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, types: Type.EmptyTypes, modifiers: null)
            ?? throw new MissingMethodException(progressSaveManager.FullName, "SaveProgress()");
        if (writeReplay.ReturnType != typeof(void) || saveProgress.ReturnType != typeof(void))
            throw new InvalidOperationException("Pinned replay/progress write signatures changed; refusing to run without offline guards.");
        if (Harmony.GetPatchInfo(writeReplay)?.Prefixes.Any(p => p.owner == PatchProbe.Id && p.PatchMethod.Name == nameof(SuppressOfflineReplayWrite)) != true)
        PatchProbe.Patch(writeReplay, prefix: new HarmonyMethod(typeof(EleanorV040Probe)
            .GetMethod(nameof(SuppressOfflineReplayWrite), BindingFlags.Static | BindingFlags.NonPublic)!));
        if (Harmony.GetPatchInfo(saveProgress)?.Prefixes.Any(p => p.owner == PatchProbe.Id && p.PatchMethod.Name == nameof(SuppressOfflineProgressSave)) != true)
        PatchProbe.Patch(saveProgress, prefix: new HarmonyMethod(typeof(EleanorV040Probe)
            .GetMethod(nameof(SuppressOfflineProgressSave), BindingFlags.Static | BindingFlags.NonPublic)!));
        var replayPrefixes = Harmony.GetPatchInfo(writeReplay)?.Prefixes;
        var progressPrefixes = Harmony.GetPatchInfo(saveProgress)?.Prefixes;
        if (replayPrefixes is null || progressPrefixes is null
            || !replayPrefixes.Any(patch => patch.owner == PatchProbe.Id && patch.PatchMethod?.Name == nameof(SuppressOfflineReplayWrite))
            || !progressPrefixes.Any(patch => patch.owner == PatchProbe.Id && patch.PatchMethod?.Name == nameof(SuppressOfflineProgressSave)))
            throw new InvalidOperationException("Could not verify the offline replay/progress write suppression prefixes.");
    }

    private static bool SuppressOfflineReplayWrite()
    {
        _suppressedReplayWrites++;
        return false;
    }

    private static bool SuppressOfflineProgressSave()
    {
        _suppressedProgressSaves++;
        return false;
    }

    private static void InstallVictoryHeadlessTerminalCapture(CombatState combat, Player player,
        VanillaCombatRootAdapter adapter, string selectedForm)
    {
        MethodInfo[] candidates = typeof(CombatManager).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(method => method.Name == "EndCombatInternal")
            .Where(method => method.GetParameters() is { Length: 1 } parameters
                && parameters[0].ParameterType.Name == "CombatTurnState")
            .ToArray();
        if (candidates.Length != 1)
            throw new MissingMethodException($"Expected one v0.111.0 EndCombatInternal(CombatTurnState), found {candidates.Length}.");

        Creature[] initialEnemies = combat.Enemies.ToArray();
        _captureVictoryTerminal = () =>
        {
            bool victoryConditions = initialEnemies.Length > 0 && initialEnemies.All(enemy => !enemy.IsAlive)
                && !combat.Enemies.Any(enemy => enemy.IsAlive && enemy.IsPrimaryEnemy) && player.Creature.CurrentHp > 0;
            if (!victoryConditions)
                throw new PredictionUnsupportedException("Native EndCombatInternal did not satisfy this fixed battle's living-player/dead-enemy Victory conditions.");
            V040Snapshot mana = CaptureState(_stateType!, GetLiveManaState(player));
            CombatRootV1 root = WithExtension(adapter.CaptureLive(combat, terminalVictory: true), mana, selectedForm);
            string stateText = ContinuationStamp.CaptureLive(combat).StateText;
            object[] enemies = combat.Enemies.Select(enemy => (object)new
            {
                id = enemy.Monster?.Id.Entry ?? enemy.GetType().Name,
                hp = enemy.CurrentHp,
                alive = enemy.IsAlive,
            }).ToArray();
            return new VictoryTerminalCapture(root, stateText, mana, player.Creature.CurrentHp, enemies, victoryConditions);
        };
        _victoryTerminalEvidence = null;
        _victoryTerminalCaptureError = null;
        _suppressedVictoryHoverClears = 0;
        if (Harmony.GetPatchInfo(candidates[0])?.Prefixes.Any(p=>p.owner==GameBootstrap.Harmony.Id && p.PatchMethod.Name==nameof(CaptureVictoryTerminalPrefix))!=true)
        GameBootstrap.Harmony.Patch(candidates[0], prefix: new HarmonyMethod(
            typeof(EleanorV040Probe).GetMethod(nameof(CaptureVictoryTerminalPrefix), BindingFlags.Static | BindingFlags.NonPublic)!));
        MethodInfo hoverClear = AccessTools.Method(typeof(NHoverTipSet), nameof(NHoverTipSet.Clear), Type.EmptyTypes)
            ?? throw new MissingMethodException(nameof(NHoverTipSet), nameof(NHoverTipSet.Clear));
        if (!hoverClear.IsStatic || hoverClear.ReturnType != typeof(void))
            throw new InvalidOperationException("Pinned hover-tip presentation cleanup signature changed.");
        if (Harmony.GetPatchInfo(hoverClear)?.Prefixes.Any(p=>p.owner==GameBootstrap.Harmony.Id && p.PatchMethod.Name==nameof(SkipHeadlessVictoryHoverClear))!=true)
        GameBootstrap.Harmony.Patch(hoverClear, prefix: new HarmonyMethod(
            typeof(EleanorV040Probe).GetMethod(nameof(SkipHeadlessVictoryHoverClear), BindingFlags.Static | BindingFlags.NonPublic)!));
    }

    private static void CaptureVictoryTerminalPrefix()
    {
        if (_captureVictoryTerminal is null || _victoryTerminalEvidence is not null || _victoryTerminalCaptureError is not null)
            return;
        try { _victoryTerminalEvidence = _captureVictoryTerminal(); }
        catch (Exception error) { _victoryTerminalCaptureError = error; }
    }

    private static bool SkipHeadlessVictoryHoverClear()
    {
        _suppressedVictoryHoverClears++;
        return false;
    }

    private static PoolFirstDrawPreflight VerifyStarterPoolAndFirstDraw(Player player, PlayerCombatState playerState,
        Type guideType, Type starterRelicType, CombatState combat)
    {
        CardModel guide = ModelDb.All.OfType<CardModel>().Single(card => card.GetType() == guideType);
        PropertyInfo poolProperty = typeof(CardModel).GetProperty("Pool",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new PredictionUnsupportedException("Pinned CardModel.Pool property is unavailable for the starter-pool preflight.");
        object? pool = null;
        Exception? poolException = null;
        try { pool = poolProperty.GetValue(guide); }
        catch (Exception ex) { poolException = ex; }
        Exception? poolError = poolException is TargetInvocationException { InnerException: not null } invocation
            ? invocation.InnerException : poolException;
        string poolId = pool == null ? "" : pool.GetType().GetProperty("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(pool)?.ToString() ?? pool.GetType().Name;
        bool correctStarterPool = pool != null && (pool.GetType().Name.Contains("MyModCardPool", StringComparison.Ordinal)
            || poolId.Contains("MY_MOD", StringComparison.OrdinalIgnoreCase)
            || poolId.Contains("MYMOD", StringComparison.OrdinalIgnoreCase));
        bool guideInFirstHand = playerState.TurnNumber == 1 && playerState.Hand.Cards.Any(card => card.GetType() == guideType);
        bool relicVisibleAtFirstDraw = player.Relics.Any(relic => relic.GetType() == starterRelicType);
        bool playerDecisionReached = combat.CurrentSide == CombatSide.Player && playerState.Phase == PlayerTurnPhase.Play;
        string[] knownPoolTypes = ModelDb.AllCardPools.Select(candidate => candidate.GetType().FullName ?? candidate.GetType().Name).ToArray();
        bool firstDrawPassed = guideInFirstHand && relicVisibleAtFirstDraw && playerDecisionReached;
        string poolStatus = correctStarterPool ? "passed" : "failed";
        string firstDrawStatus = firstDrawPassed ? "passed" : "failed";
        string combinedStatus = correctStarterPool && firstDrawPassed ? "passed" : "unsupported";
        return new PoolFirstDrawPreflight(
            combinedStatus,
            new
            {
                status = poolStatus,
                query = "ModelDb canonical GuidingPower.CardModel.Pool",
                querySucceeded = pool != null,
                poolType = pool?.GetType().FullName,
                poolId = poolId.Length == 0 ? null : poolId,
                errorType = poolError?.GetType().FullName,
                error = poolError?.Message,
                visibleCardPoolTypes = knownPoolTypes,
            },
            new
            {
                status = firstDrawStatus,
                firstPlayerTurn = playerState.TurnNumber,
                phase = playerState.Phase.ToString(),
                handCount = playerState.Hand.Cards.Count,
                openingHand = playerState.Hand.Cards.Select(card => card.Id.Entry).ToArray(),
                openingDrawPileCount = playerState.DrawPile.Cards.Count,
                guideInNativeFirstHand = guideInFirstHand,
                starterRelicVisible = relicVisibleAtFirstDraw,
                firstDecisionReached = playerDecisionReached,
            },
            "The harness bypasses retail ModManager loading; pinned BaseLib.Main.Initialize could not run because its Godot native StringName initialization crashes in this non-Godot process. A failed Pool lookup is therefore reported as an integration prerequisite gap, not retail acceptance evidence.");
    }

    private static int CountNativePower(Player player, string typeName)
        => player.Creature.Powers.OfType<PowerModel>().Where(power => power.GetType().Name == typeName).Sum(power => power.Amount);

    private static int CountSimulatedPower(CombatPredictionSimulator simulator, string typeName, Creature owner)
        => (simulator.State.CombatState as SimulatedCombatState
            ?? throw new PredictionUnsupportedException("Power count requires a branch-local simulated combat state."))
            .EffectivePowers().OfType<PowerModel>()
            .Where(power => power.GetType().Name == typeName && power.Owner == owner).Sum(power => power.Amount);

    private static void RegisterGuidingPower(Assembly mod, Type cardType, Type wrongTargetType,
        Type whiteChoiceType, Type blackChoiceType, string manifest)
    {
        MethodInfo target = AccessTools.Method(cardType, "OnPlay",
            [typeof(PlayerChoiceContext), typeof(CardPlay)]) ?? throw new MissingMethodException(cardType.FullName, "OnPlay");
        Type contextType = typeof(CardOnPlayMirrorContext);
        Type handlerType = typeof(Action<,>).MakeGenericType(cardType, contextType);
        MethodInfo handlerMethod = typeof(EleanorV040Probe).GetMethod(nameof(DispatchGuidingPower), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(cardType);
        Delegate handler = handlerMethod.CreateDelegate(handlerType);
        MethodInfo register = typeof(AdaptedCardOnPlayMirrors).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "RegisterUnpatched" && method.IsGenericMethodDefinition).MakeGenericMethod(cardType);
        MethodInfo wrongTarget = AccessTools.Method(wrongTargetType, "OnPlay", [typeof(PlayerChoiceContext), typeof(CardPlay)])!;
        string wrongManifest = "{\"id\":\"not-the-loaded-assembly\",\"name\":\"guard\",\"version\":\"0\",\"affects_gameplay\":true}";
        bool wrongTargetRejected = Rejects(register, ["eleanor-v040/wrong-target", wrongTarget, mod, manifest, handler]);
        bool wrongAssemblyRejected = Rejects(register, ["eleanor-v040/wrong-assembly", target, typeof(EleanorV040Probe).Assembly, manifest, handler]);
        bool wrongManifestRejected = Rejects(register, ["eleanor-v040/wrong-manifest", target, mod, wrongManifest, handler]);
        PatchProbe.Patch(target, prefix: new HarmonyMethod(typeof(EleanorV040Probe).GetMethod(nameof(GuardPrefix), BindingFlags.Static | BindingFlags.NonPublic)!));
        bool existingPatchRejected;
        try { existingPatchRejected = Rejects(register, ["eleanor-v040/existing-patch", target, mod, manifest, handler]); }
        finally { PatchProbe.UnpatchAll(PatchOwner); }
        if (!(wrongTargetRejected && wrongAssemblyRejected && wrongManifestRejected && existingPatchRejected))
            throw new InvalidOperationException("GuidingPower strict mirror registration negative tests failed.");
        RegistrationEvidence.Add(new { cardType = cardType.FullName, wrongTargetRejected, wrongAssemblyRejected, wrongManifestRejected, existingPatchRejected });
        InvokeRegister(register, ["eleanor-v040/guiding-power", target, mod, manifest, handler]);

        MethodInfo resultTarget = AccessTools.Method(cardType, "GetResultLocationForCardPlay", Type.EmptyTypes)
            ?? throw new MissingMethodException(cardType.FullName, "GetResultLocationForCardPlay");
        Type resultHandlerType = typeof(Func<,,>).MakeGenericType(cardType, typeof(CardLocation), typeof(CardLocation));
        MethodInfo resultHandlerMethod = typeof(EleanorV040Probe).GetMethod(nameof(ReturnHand), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(cardType);
        Delegate resultHandler = resultHandlerMethod.CreateDelegate(resultHandlerType);
        MethodInfo resultRegister = typeof(AdaptedCardResultLocationMirrors).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "RegisterUnpatched" && method.IsGenericMethodDefinition).MakeGenericMethod(cardType);
        MethodInfo baseResultTarget = AccessTools.Method(typeof(CardModel), "GetResultLocationForCardPlay", Type.EmptyTypes)!;
        bool wrongResultTargetRejected = Rejects(resultRegister,
            ["eleanor-v040/result-wrong-target", baseResultTarget, mod, manifest, resultHandler]);
        bool wrongResultAssemblyRejected = Rejects(resultRegister,
            ["eleanor-v040/result-wrong-assembly", resultTarget, typeof(EleanorV040Probe).Assembly, manifest, resultHandler]);
        bool wrongResultManifestRejected = Rejects(resultRegister,
            ["eleanor-v040/result-wrong-manifest", resultTarget, mod, wrongManifest, resultHandler]);
        PatchProbe.Patch(resultTarget, prefix: new HarmonyMethod(typeof(EleanorV040Probe).GetMethod(nameof(GuardPrefix), BindingFlags.Static | BindingFlags.NonPublic)!));
        bool existingResultPatchRejected;
        try
        {
            existingResultPatchRejected = Rejects(resultRegister,
                ["eleanor-v040/result-existing-patch", resultTarget, mod, manifest, resultHandler]);
        }
        finally { PatchProbe.UnpatchAll(PatchOwner); }
        if (!(wrongResultTargetRejected && wrongResultAssemblyRejected && wrongResultManifestRejected && existingResultPatchRejected))
            throw new InvalidOperationException("GuidingPower result-location registration negative tests failed.");
        InvokeRegister(resultRegister, ["eleanor-v040/guiding-power-result-location", resultTarget, mod, manifest, resultHandler]);

        PatchProbe.Patch(resultTarget, prefix: new HarmonyMethod(typeof(EleanorV040Probe).GetMethod(nameof(GuardPrefix), BindingFlags.Static | BindingFlags.NonPublic)!));
        bool laterResultPatchInvalidatesRoot;
        try
        {
            try { _ = AdaptedCardOnPlayMirrors.CaptureLiveStamp(); laterResultPatchInvalidatesRoot = false; }
            catch (PredictionUnsupportedException) { laterResultPatchInvalidatesRoot = true; }
        }
        finally { PatchProbe.UnpatchAll(PatchOwner); }
        if (!laterResultPatchInvalidatesRoot)
            throw new InvalidOperationException("A post-registration result-location Harmony patch did not invalidate the root stamp.");
        RegistrationEvidence.Add(new
        {
            cardType = cardType.FullName,
            wrongResultTargetRejected,
            wrongResultAssemblyRejected,
            wrongResultManifestRejected,
            existingResultPatchRejected,
            laterResultPatchInvalidatesRoot,
        });
    }

    private static void RegisterOffensiveDefense(Assembly mod, string manifest)
    {
        Type cardType = mod.GetType("MyMod.MyModCode.Cards.OffensiveDefense", true)!;
        MethodInfo target = AccessTools.Method(cardType, "OnPlay", [typeof(PlayerChoiceContext), typeof(CardPlay)])
            ?? throw new MissingMethodException(cardType.FullName, "OnPlay");
        Type contextType = typeof(CardOnPlayMirrorContext);
        Type handlerType = typeof(Action<,>).MakeGenericType(cardType, contextType);
        MethodInfo handlerMethod = typeof(EleanorV040Probe).GetMethod(nameof(DispatchOffensiveDefense), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(cardType);
        Delegate handler = handlerMethod.CreateDelegate(handlerType);
        MethodInfo register = typeof(AdaptedCardOnPlayMirrors).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "RegisterUnpatched" && method.IsGenericMethodDefinition).MakeGenericMethod(cardType);
        InvokeRegister(register, ["eleanor-v040/offensive-defense", target, mod, manifest, handler]);
    }

    private static void RegisterManaCardMirror(Assembly mod, string cardName, string otherCardName, string handlerName)
    {
        Type cardType = mod.GetType($"MyMod.MyModCode.Cards.{cardName}", true)!;
        Type otherCardType = mod.GetType($"MyMod.MyModCode.Cards.{otherCardName}", true)!;
        Type[] signature = [typeof(PlayerChoiceContext), typeof(CardPlay)];
        MethodInfo target = AccessTools.Method(cardType, "OnPlay", signature)
            ?? throw new MissingMethodException(cardType.FullName, "OnPlay(PlayerChoiceContext, CardPlay)");
        MethodInfo wrongTarget = AccessTools.Method(otherCardType, "OnPlay", signature)
            ?? throw new MissingMethodException(otherCardType.FullName, "OnPlay(PlayerChoiceContext, CardPlay)");
        Type handlerType = typeof(Action<,>).MakeGenericType(cardType, typeof(CardOnPlayMirrorContext));
        MethodInfo handlerMethod = typeof(EleanorV040Probe).GetMethod(handlerName, BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(cardType);
        Delegate handler = handlerMethod.CreateDelegate(handlerType);
        MethodInfo register = typeof(AdaptedCardOnPlayMirrors).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "RegisterUnpatched" && method.IsGenericMethodDefinition).MakeGenericMethod(cardType);
        string wrongManifest = "{\"id\":\"not-the-loaded-assembly\",\"name\":\"guard\",\"version\":\"0\",\"affects_gameplay\":true}";
        bool wrongTargetRejected = Rejects(register, [$"eleanor-v040/{cardName}/wrong-target", wrongTarget, mod, _manifest, handler]);
        bool wrongAssemblyRejected = Rejects(register, [$"eleanor-v040/{cardName}/wrong-assembly", target, typeof(EleanorV040Probe).Assembly, _manifest, handler]);
        bool wrongManifestRejected = Rejects(register, [$"eleanor-v040/{cardName}/wrong-manifest", target, mod, wrongManifest, handler]);
        PatchProbe.Patch(target, prefix: new HarmonyMethod(typeof(EleanorV040Probe)
            .GetMethod(nameof(GuardPrefix), BindingFlags.Static | BindingFlags.NonPublic)!));
        bool existingPatchRejected;
        try { existingPatchRejected = Rejects(register, [$"eleanor-v040/{cardName}/existing-patch", target, mod, _manifest, handler]); }
        finally { PatchProbe.UnpatchAll(PatchOwner); }
        if (!(wrongTargetRejected && wrongAssemblyRejected && wrongManifestRejected && existingPatchRejected))
            throw new InvalidOperationException($"{cardName} strict mirror registration negative tests failed.");
        RegistrationEvidence.Add(new { cardType = cardType.FullName, target = target.ToString(),
            wrongTargetRejected, wrongAssemblyRejected, wrongManifestRejected, existingPatchRejected });
        InvokeRegister(register, [$"eleanor-v040/{cardName.ToLowerInvariant()}", target, mod, _manifest, handler]);
    }

    private static void RegisterRecordingPowers(Assembly mod, string manifest)
    {
        Type recordingType = mod.GetType("MyMod.MyModCode.Powers.RecordingPower", true)!;
        Type temporaryType = mod.GetType("MyMod.MyModCode.Powers.TemporaryRecordingPower", true)!;
        Type[] hookParameters = [typeof(PlayerChoiceContext), typeof(Creature), typeof(DamageResult),
            typeof(ValueProp), typeof(Creature), typeof(CardModel)];
        MethodInfo target = ResolveDeclaredImplementation(recordingType, "AfterDamageReceived", hookParameters)
            ?? throw new MissingMethodException(recordingType.FullName, "AfterDamageReceived");
        _recordingHookTarget = target;
        Action<AbstractModel, AfterDamageReceivedMirrorContext> handler = DispatchRecordingPower;
        MethodInfo register = typeof(AdaptedPowerAfterDamageReceivedMirrors).GetMethod("RegisterUnpatched",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(AdaptedPowerAfterDamageReceivedMirrors).FullName, "RegisterUnpatched");
        MethodInfo wrongTarget = AccessTools.Method(typeof(CardModel), "GetResultLocationForCardPlay", Type.EmptyTypes)!;
        string wrongManifest = "{\"id\":\"not-the-loaded-assembly\",\"name\":\"guard\",\"version\":\"0\",\"affects_gameplay\":true}";
        bool wrongTargetRejected = Rejects(register, [recordingType, "eleanor-v040/echo/wrong-target", wrongTarget, mod, manifest, handler]);
        bool wrongAssemblyRejected = Rejects(register, [recordingType, "eleanor-v040/echo/wrong-assembly", target,
            typeof(EleanorV040Probe).Assembly, manifest, handler]);
        bool wrongManifestRejected = Rejects(register, [recordingType, "eleanor-v040/echo/wrong-manifest", target, mod, wrongManifest, handler]);
        PatchProbe.Patch(target, prefix: new HarmonyMethod(typeof(EleanorV040Probe).GetMethod(nameof(GuardPrefix), BindingFlags.Static | BindingFlags.NonPublic)!));
        bool existingPatchRejected;
        try
        {
            existingPatchRejected = Rejects(register, [recordingType, "eleanor-v040/echo/existing-patch", target, mod, manifest, handler]);
        }
        finally { PatchProbe.UnpatchAll(PatchOwner); }
        if (!(wrongTargetRejected && wrongAssemblyRejected && wrongManifestRejected && existingPatchRejected))
            throw new InvalidOperationException("RecordingPower strict hook registration negative tests failed.");
        InvokeRegister(register, [recordingType, "eleanor-v040/recording-power/unpatched/v1", target, mod, manifest, handler]);
        InvokeRegister(register, [temporaryType, "eleanor-v040/temporary-recording-power/unpatched/v1", target, mod, manifest, handler]);
        RegistrationEvidence.Add(new { powerTypes = new[] { recordingType.FullName, temporaryType.FullName },
            hookTarget = target.ToString(), wrongTargetRejected, wrongAssemblyRejected, wrongManifestRejected, existingPatchRejected });
    }

    private static void RegisterTemporaryRecordingTurnStart(Assembly mod, string manifest)
    {
        Type powerType = mod.GetType("MyMod.MyModCode.Powers.TemporaryRecordingPower", true)!;
        Type[] parameters = [typeof(PlayerChoiceContext), typeof(CombatSide), typeof(IReadOnlyList<Creature>), typeof(ICombatState)];
        MethodInfo target = ResolveDeclaredImplementation(powerType, "BeforeSideTurnStart", parameters)
            ?? throw new MissingMethodException(powerType.FullName, "BeforeSideTurnStart");
        _temporaryRecordingTurnStartTarget = target;
        Assembly solverAssembly = typeof(AdaptedCardOnPlayMirrors).Assembly;
        Type contextType = solverAssembly.GetType("CombatSolver.Engine.InCombat.Mirrors.Hooks.TurnStart.BeforeSideTurnStartMirrorContext", true)!;
        Type delegateType = typeof(Action<,>).MakeGenericType(typeof(AbstractModel), contextType);
        Delegate handler = CreateAdapterDelegate(delegateType, nameof(DispatchTemporaryRecordingBeforeSideTurnStart));
        Type adapterType = solverAssembly.GetType("CombatSolver.AdaptedPowerBeforeSideTurnStartMirrors", true)!;
        MethodInfo register = adapterType.GetMethod("RegisterUnpatched", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(adapterType.FullName, "RegisterUnpatched");
        MethodInfo wrongTarget = AccessTools.Method(typeof(CardModel), "GetResultLocationForCardPlay", Type.EmptyTypes)!;
        string wrongManifest = "{\"id\":\"not-the-loaded-assembly\",\"name\":\"guard\",\"version\":\"0\",\"affects_gameplay\":true}";
        bool wrongTargetRejected = Rejects(register, [powerType, "eleanor-v041/temporary-recording/wrong-target", wrongTarget, mod, manifest, handler]);
        bool wrongAssemblyRejected = Rejects(register, [powerType, "eleanor-v041/temporary-recording/wrong-assembly", target,
            typeof(EleanorV040Probe).Assembly, manifest, handler]);
        bool wrongManifestRejected = Rejects(register, [powerType, "eleanor-v041/temporary-recording/wrong-manifest", target, mod, wrongManifest, handler]);
        bool existingPatchRejected;
        using (HarmonyPatchMetadataProbe.Inject(target, "simulatorlab.eleanor-v041.temporary-recording-metadata-guard"))
            existingPatchRejected = Rejects(register, [powerType, "eleanor-v041/temporary-recording/existing-patch", target, mod, manifest, handler]);
        if (!(wrongTargetRejected && wrongAssemblyRejected && wrongManifestRejected && existingPatchRejected))
            throw new InvalidOperationException("TemporaryRecordingPower strict turn-start registration negative tests failed.");
        InvokeRegister(register, [powerType, "eleanor-v041/temporary-recording/side-turn-start/unpatched/v1", target, mod, manifest, handler]);
        RegistrationEvidence.Add(new { powerType = powerType.FullName, hookTarget = target.ToString(),
            wrongTargetRejected, wrongAssemblyRejected, wrongManifestRejected, existingPatchMetadataRejected = existingPatchRejected,
            patchControl = "synthetic Harmony metadata only; not an IL weave" });
    }

    private static void RegisterCrystalOrbTurnStart(Assembly mod, string manifest)
    {
        Type relicType = mod.GetType("MyMod.MyModCode.Relics.CrackedCrystalOrb", true)!;
        MethodInfo beforeTarget = AccessTools.Method(relicType, "BeforeHandDraw",
            [typeof(Player), typeof(PlayerChoiceContext), typeof(ICombatState)])
            ?? throw new MissingMethodException(relicType.FullName, "BeforeHandDraw");
        MethodInfo modifyTarget = AccessTools.Method(relicType, "ModifyHandDraw", [typeof(Player), typeof(decimal)])
            ?? throw new MissingMethodException(relicType.FullName, "ModifyHandDraw");
        Assembly solverAssembly = typeof(AdaptedCardOnPlayMirrors).Assembly;
        Type contextType = solverAssembly.GetType("CombatSolver.RelicBeforeHandDrawMirrorContext", true)!;
        Type beforeDelegateType = typeof(Action<,>).MakeGenericType(typeof(RelicModel), contextType);
        Type modifyDelegateType = typeof(Func<,,,>).MakeGenericType(typeof(RelicModel), contextType, typeof(decimal), typeof(decimal));
        Delegate beforeHandler = CreateAdapterDelegate(beforeDelegateType, nameof(DispatchCrackedOrbBeforeHandDraw));
        Delegate modifyHandler = CreateAdapterDelegate(modifyDelegateType, nameof(DispatchCrackedOrbModifyHandDraw));
        Type adapterType = solverAssembly.GetType("CombatSolver.AdaptedRelicBeforeHandDrawMirrors", true)!;
        MethodInfo register = adapterType.GetMethod("RegisterUnpatched", BindingFlags.Static | BindingFlags.Public)
            ?? throw new MissingMethodException(adapterType.FullName, "RegisterUnpatched");
        MethodInfo wrongTarget = AccessTools.Method(typeof(CardModel), "GetResultLocationForCardPlay", Type.EmptyTypes)!;
        string wrongManifest = "{\"id\":\"not-the-loaded-assembly\",\"name\":\"guard\",\"version\":\"0\",\"affects_gameplay\":true}";
        bool wrongTargetRejected = Rejects(register, [relicType, "eleanor-v041/orb/wrong-target", wrongTarget, modifyTarget, mod, manifest, beforeHandler, modifyHandler]);
        bool wrongAssemblyRejected = Rejects(register, [relicType, "eleanor-v041/orb/wrong-assembly", beforeTarget, modifyTarget,
            typeof(EleanorV040Probe).Assembly, manifest, beforeHandler, modifyHandler]);
        bool wrongManifestRejected = Rejects(register, [relicType, "eleanor-v041/orb/wrong-manifest", beforeTarget, modifyTarget, mod,
            wrongManifest, beforeHandler, modifyHandler]);
        bool existingPatchRejected;
        using (HarmonyPatchMetadataProbe.Inject(beforeTarget, "simulatorlab.eleanor-v041.orb-metadata-guard"))
            existingPatchRejected = Rejects(register, [relicType, "eleanor-v041/orb/existing-patch", beforeTarget, modifyTarget, mod, manifest, beforeHandler, modifyHandler]);
        if (!(wrongTargetRejected && wrongAssemblyRejected && wrongManifestRejected && existingPatchRejected))
            throw new InvalidOperationException("CrackedCrystalOrb strict turn-start registration negative tests failed.");
        InvokeRegister(register, [relicType, "eleanor-v041/cracked-orb/before-hand-draw/unpatched/v1",
            beforeTarget, modifyTarget, mod, manifest, beforeHandler, modifyHandler]);
        RegistrationEvidence.Add(new { relicType = relicType.FullName,
            beforeTarget = beforeTarget.ToString(), modifyTarget = modifyTarget.ToString(),
            wrongTargetRejected, wrongAssemblyRejected, wrongManifestRejected, existingPatchMetadataRejected = existingPatchRejected,
            patchControl = "synthetic Harmony metadata only; not an IL weave" });
    }

    private static Delegate CreateAdapterDelegate(Type delegateType, string handlerName)
    {
        MethodInfo handler = typeof(EleanorV040Probe).GetMethod(handlerName,
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(EleanorV040Probe).FullName, handlerName);
        // Reference-type variance binds the actual static handler. Expression.Compile creates
        // process-specific dynamic module identities and made otherwise exact repeat stamps differ.
        Delegate result = handler.CreateDelegate(delegateType);
        if (result.Method != handler || result.Target != null)
            throw new InvalidOperationException("Adapter delegate must bind the exact static implementation.");
        return result;
    }

    private static void VerifyLateTurnStartPatchInvalidatesRoot()
    {
        MethodInfo target = _temporaryRecordingTurnStartTarget
            ?? throw new InvalidOperationException("TemporaryRecordingPower turn-start hook was not registered.");
        bool latePatchInvalidatesRoot;
        using (HarmonyPatchMetadataProbe.Inject(target, "simulatorlab.eleanor-v041.temporary-recording-late-metadata-guard"))
        {
            try { _ = AdaptedCardOnPlayMirrors.CaptureLiveStamp(); latePatchInvalidatesRoot = false; }
            catch (PredictionUnsupportedException) { latePatchInvalidatesRoot = true; }
        }
        if (!latePatchInvalidatesRoot)
            throw new InvalidOperationException("A post-registration TemporaryRecordingPower patch did not invalidate the root stamp.");
        _ = AdaptedCardOnPlayMirrors.CaptureLiveStamp();
        RegistrationEvidence.Add(new { powerHook = target.ToString(), laterPatchMetadataInvalidatesRoot = latePatchInvalidatesRoot,
            patchControl = "synthetic Harmony metadata only; not an IL weave" });
    }

    private static void VerifyLatePowerHookPatchInvalidatesRoot()
    {
        MethodInfo target = _recordingHookTarget
            ?? throw new InvalidOperationException("RecordingPower hook was not registered.");
        PatchProbe.Patch(target, prefix: new HarmonyMethod(typeof(EleanorV040Probe).GetMethod(nameof(GuardPrefix), BindingFlags.Static | BindingFlags.NonPublic)!));
        bool laterPatchInvalidatesRoot;
        try
        {
            try { _ = AdaptedCardOnPlayMirrors.CaptureLiveStamp(); laterPatchInvalidatesRoot = false; }
            catch (PredictionUnsupportedException) { laterPatchInvalidatesRoot = true; }
        }
        finally { PatchProbe.UnpatchAll(PatchOwner); }
        if (!laterPatchInvalidatesRoot)
            throw new InvalidOperationException("A post-registration Power hook patch did not invalidate the root stamp.");
        _ = AdaptedCardOnPlayMirrors.CaptureLiveStamp();
        RegistrationEvidence.Add(new { powerHook = target.ToString(), laterPatchInvalidatesRoot });
    }

    private static bool Rejects(MethodInfo register, object?[] args)
    {
        try { InvokeRegister(register, args); return false; }
        catch (ArgumentException) { return true; }
        catch (PredictionUnsupportedException) { return true; }
    }

    private static MethodInfo? ResolveDeclaredImplementation(Type type, string name, Type[] parameters)
    {
        for (Type? current = type; current != null && current != typeof(AbstractModel); current = current.BaseType)
        {
            MethodInfo? method = current.GetMethod(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                binder: null, types: parameters, modifiers: null);
            if (method != null) return method;
        }
        return null;
    }

    private static void InvokeRegister(MethodInfo register, object?[] args)
    {
        try { register.Invoke(null, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }

    private static void GuardPrefix() { }

    private static void DispatchGuidingPower<TCard>(TCard card, CardOnPlayMirrorContext context) where TCard : CardModel
    {
        RelicModel orb = GetBranchOrb(context.Simulator, context.PreviewCard.Owner);
        V040PredictionState state = GetState(context, orb);
        int energy = context.CardPlay.PlayIndex == 0 ? context.CardPlay.Resources.EnergySpent : 0;
        object transaction = state.BeginCard(energy);
        state.SwitchStance(_selectedForm.ToString());
        if (!state.SyncReverse(context.Simulator, context.PreviewCard.Owner.Creature,
                context.PreviewCard.Owner.Relics.Any(relic => relic.GetType().Name == "CompleteCrystalOrb"),
                turnStartRestore: false))
            throw new PredictionUnsupportedException("GuidingPower branch ReversePowerLedger sync was unresolved.");
        state.FinishCard(transaction);
    }

    private static void DispatchOffensiveDefense<TCard>(TCard card, CardOnPlayMirrorContext context) where TCard : CardModel
    {
        RelicModel orb = GetBranchOrb(context.Simulator, context.PreviewCard.Owner);
        V040PredictionState state = GetState(context, orb);
        int energy = context.CardPlay.PlayIndex == 0 ? context.CardPlay.Resources.EnergySpent : 0;
        object transaction = state.BeginCard(energy);
        Creature owner = context.PreviewCard.Owner.Creature;
        SimulatedCombatState combat = context.Simulator.State.CombatState as SimulatedCombatState
            ?? throw new PredictionUnsupportedException("OffensiveDefense requires the branch-local simulated combat state.");
        int manaCost = Convert.ToInt32(card.GetType().GetProperty("ManaCost")!.GetValue(card));
        bool manaPaid = state.TryPayMana(manaCost);
        if (manaPaid)
        {
            int shieldPerMana = combat.EffectivePowers().OfType<PowerModel>()
                .Where(power => power.GetType().Name == "ManaToShieldPower" && power.Owner == owner)
                .Sum(power => power.Amount);
            if (shieldPerMana > 0)
                context.Simulator.GainBlock(owner, (decimal)shieldPerMana * manaCost,
                    (ValueProp)4, context.Card, context.CardPlay);
        }

        int conversion = card.DynamicVars["Conversion"].IntValue;
        SimCreatureState ownerState = context.State.GetCreature(owner);
        bool converted = ownerState.Block >= conversion;
        if (converted) ownerState.LoseBlock(conversion);

        decimal damagePerHit = card.DynamicVars.Damage.BaseValue + (converted ? card.DynamicVars["Conversion"].BaseValue : 0m);
        var attack = DamageCmd.Attack(damagePerHit)
            .WithHitCount(card.DynamicVars["Hits"].IntValue)
            .FromCard(context.PreviewCard, context.CardPlay)
            .Targeting(context.Target);
        attack.Simulate(context.Simulator);
        if (context.Simulator.HasPendingChoice)
            throw new PredictionUnsupportedException("OffensiveDefense attack created an unsupported pending choice.");
        decimal unblocked = attack.Results.SelectMany(results => results)
            .Sum(result => Math.Max(0m, result.UnblockedDamage));
        if (manaPaid)
            context.Simulator.GainBlock(owner, unblocked, (ValueProp)4, context.Card, context.CardPlay);
        state.FinishCard(transaction);
    }

    private static void DispatchManaShield<TCard>(TCard card, CardOnPlayMirrorContext context) where TCard : CardModel
    {
        Player player=context.PreviewCard.Owner;
        var state=GetState(context,GetBranchOrb(context.Simulator,player));
        object transaction=state.BeginCard(context.CardPlay.PlayIndex==0 ? context.CardPlay.Resources.EnergySpent : 0);
        int cost=Convert.ToInt32(card.GetType().GetProperty("ManaCost")!.GetValue(card));
        bool paid=state.TryPayMana(cost);
        var shadow=context.Simulator.State.CombatState as SimulatedCombatState
            ?? throw new PredictionUnsupportedException("ManaShield requires branch-local combat.");
        if(paid)
        {
            int amount=shadow.EffectivePowers().OfType<PowerModel>().Where(p=>p.GetType().Name=="ManaToShieldPower" && p.Owner==player.Creature).Sum(p=>p.Amount);
            if(amount>0)context.Simulator.GainBlock(player.Creature,(decimal)amount*cost,(ValueProp)4,context.Card,context.CardPlay);
        }
        context.Simulator.GainBlock(player.Creature,card.DynamicVars.Block.BaseValue+(paid?card.DynamicVars["ExtraBlock"].BaseValue:0m),(ValueProp)8,context.Card,context.CardPlay);
        state.FinishCard(transaction);
    }

    private static void DispatchManaGather<TCard>(TCard card, CardOnPlayMirrorContext context) where TCard : CardModel
    {
        Player player = context.PreviewCard.Owner;
        RelicModel orb = GetBranchOrb(context.Simulator, player);
        V040PredictionState state = GetState(context, orb);
        int energy = context.CardPlay.PlayIndex == 0 ? context.CardPlay.Resources.EnergySpent : 0;
        object transaction = state.BeginCard(energy);
        decimal block = card.DynamicVars.Block.BaseValue;
        context.Simulator.GainBlock(player.Creature, block, (ValueProp)8, context.Card, context.CardPlay);
        state.GainMana(card.DynamicVars["Mana"].IntValue);
        state.FinishCard(transaction);
    }

    private static void DispatchManaStrike<TCard>(TCard card, CardOnPlayMirrorContext context) where TCard : CardModel
    {
        Player player = context.PreviewCard.Owner;
        Creature target = context.Target ?? throw new PredictionUnsupportedException("ManaStrike route action has no target.");
        RelicModel orb = GetBranchOrb(context.Simulator, player);
        V040PredictionState state = GetState(context, orb);
        int energy = context.CardPlay.PlayIndex == 0 ? context.CardPlay.Resources.EnergySpent : 0;
        object transaction = state.BeginCard(energy);
        bool manaPaid = state.TryPayMana(1);
        var attack = DamageCmd.Attack(card.DynamicVars.Damage.BaseValue)
            .WithHitCount(manaPaid ? 2 : 1)
            .FromCard(context.PreviewCard, context.CardPlay)
            .Targeting(target);
        attack.Simulate(context.Simulator);
        if (context.Simulator.HasPendingChoice)
            throw new PredictionUnsupportedException("ManaStrike attack created an unsupported pending choice.");
        state.FinishCard(transaction);
    }

    private static void DispatchRecordingPower(AbstractModel listener, AfterDamageReceivedMirrorContext context)
    {
        if (listener is not PowerModel power || context.Target != power.Owner || power.Owner.Player is not { } player
            || context.Result.BlockedDamage <= 0)
            return;
        RelicModel orb = GetBranchOrb(context.Simulator, player);
        V040PredictionState state = GetState(context, orb);
        V040Snapshot before = state.Capture();
        state.RecordBlockedDamage(power.Amount, Convert.ToInt32(context.Result.BlockedDamage));
        V040Snapshot after = state.Capture();
        _lastRecordingDamageState = state;
        AdapterInvocations.Add(new { adapter = "RecordingPower.AfterDamageReceived", power = power.GetType().Name,
            amount = power.Amount, blocked = context.Result.BlockedDamage, before, after });
    }

    private static void DispatchTemporaryRecordingBeforeSideTurnStart(AbstractModel listener, object context)
    {
        if (listener is not PowerModel power)
            throw new PredictionUnsupportedException("TemporaryRecordingPower turn-start adapter received a non-Power listener.");
        object? side = context.GetType().GetProperty("Side")?.GetValue(context);
        object? participants = context.GetType().GetProperty("Participants")?.GetValue(context);
        object? simulatorValue = context.GetType().BaseType?.GetProperty("Simulator")?.GetValue(context)
            ?? context.GetType().GetProperty("Simulator")?.GetValue(context);
        if (side?.ToString() != "Player" || participants is not IEnumerable participatingCreatures
            || !participatingCreatures.Cast<object>().Any(creature => ReferenceEquals(creature, power.Owner)))
            return;
        CombatPredictionSimulator simulator = simulatorValue as CombatPredictionSimulator
            ?? throw new PredictionUnsupportedException("Turn-start mirror omitted its branch simulator.");
        SimulatedCombatState combat = simulator.State.CombatState as SimulatedCombatState
            ?? throw new PredictionUnsupportedException("TemporaryRecording expiry requires the branch-local simulated combat state.");
        int before = combat.EffectivePowers().OfType<PowerModel>()
            .Where(candidate => candidate.GetType() == power.GetType() && candidate.Owner == power.Owner)
            .Sum(candidate => candidate.Amount);
        combat.SetPowerAmount(power, 0);
        int after = combat.EffectivePowers().OfType<PowerModel>()
            .Where(candidate => candidate.GetType() == power.GetType() && candidate.Owner == power.Owner)
            .Sum(candidate => candidate.Amount);
        AdapterInvocations.Add(new { adapter = "TemporaryRecordingPower.BeforeSideTurnStart", side = side?.ToString(), before, after });
    }

    private static void DispatchCrackedOrbBeforeHandDraw(RelicModel relic, object context)
    {
        if (relic.GetType().FullName != "MyMod.MyModCode.Relics.CrackedCrystalOrb")
            throw new PredictionUnsupportedException("Turn-start adapter received a non-CrackedCrystalOrb relic.");
        CombatPredictionSimulator simulator = GetContextValue<CombatPredictionSimulator>(context, "Simulator");
        Player player = GetContextValue<Player>(context, "Player");
        V040PredictionState state = GetState(simulator, relic,
            CaptureState(_stateType!, GetLiveManaState(player)));
        V040Snapshot before = state.Capture();
        PropertyInfo completeProperty = relic.GetType().GetProperty("Complete", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(relic.GetType().FullName, "Complete");
        bool complete = Convert.ToBoolean(completeProperty.GetValue(relic));
        ApplyBranchTurnStart(simulator, player, relic, state, complete);
        _lastTurnStartSimulator = simulator;
        _lastTurnStartSnapshot = state.Capture();
        _lastTurnStartState = state;
        _lastTurnStartNumber = ((SimulatedCombatState)simulator.State.CombatState).GetPlayerTurnNumber(player);
        AdapterInvocations.Add(new { adapter = "CrackedCrystalOrb.BeforeHandDraw", before, after = state.Capture() });
    }

    private static decimal DispatchCrackedOrbModifyHandDraw(RelicModel relic, object context, decimal count)
    {
        if (relic.GetType().FullName != "MyMod.MyModCode.Relics.CrackedCrystalOrb")
            throw new PredictionUnsupportedException("Hand-draw adapter received a non-CrackedCrystalOrb relic.");
        CombatPredictionSimulator simulator = GetContextValue<CombatPredictionSimulator>(context, "Simulator");
        Player player = GetContextValue<Player>(context, "Player");
        V040PredictionState state = GetState(simulator, relic,
            CaptureState(_stateType!, GetLiveManaState(player)));
        V040Snapshot snapshot = state.Capture();
        decimal result = count + snapshot.StartDrawBonus;
        AdapterInvocations.Add(new { adapter = "CrackedCrystalOrb.ModifyHandDraw", count, startDrawBonus = snapshot.StartDrawBonus, result });
        return result;
    }

    private static T GetContextValue<T>(object context, string property)
    {
        object? value = context.GetType().GetProperty(property)?.GetValue(context)
            ?? context.GetType().BaseType?.GetProperty(property)?.GetValue(context);
        return value is T result ? result
            : throw new PredictionUnsupportedException($"The {property} context value is missing or changed type.");
    }

    private static void ApplyBranchTurnStart(CombatPredictionSimulator simulator, Player player,
        RelicModel relic, V040PredictionState state, bool complete)
    {
        SimPlayerCombatState playerState = simulator.State.GetPlayerCombatState(player);
        state.SetCompleteOrb(complete);
        object upkeep = state.PayUpkeep(playerState.Energy);
        int energyPaid = ReadUpkeepInt(upkeep, "EnergyPaid");
        int manaPaid = ReadUpkeepInt(upkeep, "ManaPaid");
        int hpLoss = ReadUpkeepInt(upkeep, "HpLoss");
        if (energyPaid > 0) simulator.LoseEnergy(player, energyPaid);
        if (manaPaid > 0)
        {
            int shieldPerMana = ((SimulatedCombatState)simulator.State.CombatState).EffectivePowers()
                .OfType<PowerModel>().Where(power => power.GetType().Name == "ManaToShieldPower" && power.Owner == player.Creature)
                .Sum(power => power.Amount);
            if (shieldPerMana > 0)
                simulator.GainBlock(player.Creature, (decimal)shieldPerMana * manaPaid, (ValueProp)4, null, null);
        }
        if (!state.SyncReverse(simulator, player.Creature, complete, turnStartRestore: false))
            throw new PredictionUnsupportedException("CrystalOrb pre-draw ReversePowerLedger sync was unresolved.");
        if (hpLoss > 0)
        {
            _ = simulator.Damage(player.Creature, hpLoss, (ValueProp)6, player.Creature);
            if (!simulator.State.GetCreature(player.Creature).IsAlive) return;
        }
        object orbStart = state.GrantOrbStart();
        int orbEnergy = ReadUpkeepInt(orbStart, "Energy");
        if (orbEnergy > 0) simulator.GainEnergy(player, orbEnergy);
        if (!state.SyncReverse(simulator, player.Creature, complete, turnStartRestore: true))
            throw new PredictionUnsupportedException("CrystalOrb turn-start ReversePowerLedger restore was unresolved.");
    }

    private static object RunOffensiveDefense(CombatState combat, MainLoopContext loop, Player player,
        PlayerCombatState playerState, RelicModel orb, VanillaCombatRootAdapter adapter, string outputDirectory)
    {
        Assembly mod = AppDomain.CurrentDomain.GetAssemblies().Single(assembly => assembly.GetName().Name == "MyMod");
        Type cardType = mod.GetType("MyMod.MyModCode.Cards.OffensiveDefense", true)!;
        Type manaShieldType = mod.GetType("MyMod.MyModCode.Powers.ManaToShieldPower", true)!;
        CardModel canonical = ModelDb.All.OfType<CardModel>().Single(card => card.GetType() == cardType);
        CardModel card = combat.CreateCard(canonical, player);
        Task<CardPileAddResult> add = CardPileCmd.Add(card, PileType.Hand);
        loop.RunUntilCompleted(add, TimeSpan.FromSeconds(30), "Add native OffensiveDefense fixture");
        if (!add.GetAwaiter().GetResult().success) throw new InvalidOperationException("Game rejected the OffensiveDefense fixture.");

        Task blockTask = InvokeNativeBlockGain(player.Creature, 5m);
        loop.RunUntilCompleted(blockTask, TimeSpan.FromSeconds(30), "Seed OffensiveDefense conversion threshold");
        Task applyTask = InvokeNativePowerApply(manaShieldType, player.Creature, 1m);
        loop.RunUntilCompleted(applyTask, TimeSpan.FromSeconds(30), "Apply native ManaToShield fixture power");

        Creature target = combat.Enemies.Single(enemy => enemy.IsAlive);
        V040Snapshot opening = CaptureState(_stateType!, GetLiveManaState(player));
        CombatRootSnapshot root = CombatRootSnapshot.Capture(combat);
        string nativeBeforeText = ContinuationStamp.CaptureLive(combat).StateText;
        CombatRootV1 nativeBefore = WithExtension(adapter.CaptureLive(combat), opening, _selectedForm.ToString());
        CombatPredictionSimulator parent = root.ForkSimulator();
        V040PredictionState parentState = GetState(parent, orb, opening);
        V040Snapshot parentBefore = parentState.Capture();
        CombatRootV1 predictedBefore = WithExtension(adapter.CapturePredicted(combat, player, parent,
            playerState.TurnNumber, root.Forecast, root.StartTurnNumber), parentBefore, _selectedForm.ToString());
        string predictedBeforeText = ContinuationStamp.CapturePredicted(player, parent,
            playerState.TurnNumber, root.Forecast, root.StartTurnNumber).StateText;
        bool rootMatchesBefore = StateDigest.Compute(nativeBefore) == StateDigest.Compute(predictedBefore)
            && StringComparer.Ordinal.Equals(nativeBeforeText, predictedBeforeText);
        if (!rootMatchesBefore)
            throw new PredictionUnsupportedException("OffensiveDefense native/simulator opening roots differ after real block and ManaToShield setup.");

        CombatPredictionSimulator branch = parent.Fork();
        PredictedCard predictedCard = branch.State.FindCard(card)
            ?? throw new PredictionUnsupportedException("CombatSolver fork omitted OffensiveDefense.");
        Creature? predictedTarget = combat.Enemies.Single(enemy => enemy == target);
        if (!((SimulatedCombatState)branch.State.CombatState).CanPlayCard(branch, predictedCard, out _, out _))
            throw new PredictionUnsupportedException("CombatSolver does not consider OffensiveDefense playable.");
        if (!branch.ManualPlay(predictedCard, predictedTarget, out _))
            throw new PredictionUnsupportedException("CombatSolver rejected OffensiveDefense.");
        _ = branch.CheckWinCondition(playerState.TurnNumber);
        V040Snapshot simulatedAfter = GetState(branch, GetBranchOrb(branch, player), opening).Capture();
        bool parentUnchanged = parentBefore == GetState(parent, orb, opening).Capture();

        int energyBefore = playerState.Energy;
        int manaBefore = opening.Mana;
        int blockBefore = player.Creature.Block;
        int targetHpBefore = target.CurrentHp;
        if (!card.TryManualPlay(target))
            throw new PredictionUnsupportedException("Native game rejected OffensiveDefense.");
        loop.RunUntilCompleted(RunManager.Instance.ActionExecutor.FinishedExecutingActions(), TimeSpan.FromSeconds(30),
            "Native OffensiveDefense mana payment, conversion, attack, and block gain");
        V040Snapshot nativeAfter = CaptureState(_stateType!, GetLiveManaState(player));
        CombatRootV1 nativeAfterRoot = WithExtension(adapter.CaptureLive(combat), nativeAfter, _selectedForm.ToString());
        CombatRootV1 predictedAfterRoot = WithExtension(adapter.CapturePredicted(combat, player, branch,
            playerState.TurnNumber, root.Forecast, root.StartTurnNumber), simulatedAfter, _selectedForm.ToString());
        string nativeAfterText = ContinuationStamp.CaptureLive(combat).StateText;
        string predictedAfterText = ContinuationStamp.CapturePredicted(player, branch,
            playerState.TurnNumber, root.Forecast, root.StartTurnNumber).StateText;
        List<string> differences = DescribeRootDifferences(nativeAfterRoot, predictedAfterRoot);
        int actualEnergyPaid = energyBefore - playerState.Energy;
        int enemyDamage = targetHpBefore - target.CurrentHp;
        int nativePowerAmount = player.Creature.Powers.OfType<PowerModel>()
            .Where(power => power.GetType() == manaShieldType).Sum(power => power.Amount);
        bool exact = nativeAfter == simulatedAfter && actualEnergyPaid == 1 && nativePowerAmount == 1
            && differences.Count == 0 && parentUnchanged
            && StringComparer.Ordinal.Equals(nativeAfterText, predictedAfterText);
        var report = new
        {
            schema = "eleanor-v040-offensive-defense-differential/v1",
            status = exact ? "passed" : "failed",
            gameVersion = GameBuildPin.Version,
            gameCommit = GameBuildPin.Commit,
            modVersion = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_VERSION"),
            selectedForm = _selectedForm.ToString(),
            opening = new { opening, playerEnergy = energyBefore, playerBlock = blockBefore, manaShieldPower = nativePowerAmount,
                enemyHp = targetHpBefore },
            nativeAfter,
            simulatorAfter = simulatedAfter,
            actualEnergyPaid,
            playerBlockAfter = player.Creature.Block,
            enemyHpAfter = target.CurrentHp,
            enemyDamage,
            conversionThreshold = 6,
            manaPaymentFirst = true,
            manaToShieldBlockBeforeConversion = 1,
            exactOverkillCounterTest = false,
            parentBranchUnchanged = parentUnchanged,
            openingRootsMatch = rootMatchesBefore,
            postActionRootDifferences = differences,
            nativeContinuationSha256 = Sha(nativeAfterText),
            simulatorContinuationSha256 = Sha(predictedAfterText),
            continuationMatches = StringComparer.Ordinal.Equals(nativeAfterText, predictedAfterText),
            exactStateMatch = exact,
        };
        File.WriteAllText(Path.Combine(outputDirectory, "eleanor-v040-offensive-defense-differential.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true }));
        if (!exact)
            throw new PredictionUnsupportedException("OffensiveDefense native/simulator comparison diverged; inspect its v0.4 differential report.");
        return report;
    }

    private static object RunRecordEchoFixture(CombatState combat, MainLoopContext loop, Player player,
        PlayerCombatState playerState, RelicModel orb, VanillaCombatRootAdapter adapter, string outputDirectory)
    {
        Assembly mod = AppDomain.CurrentDomain.GetAssemblies().Single(assembly => assembly.GetName().Name == "MyMod");
        Type recordingType = mod.GetType("MyMod.MyModCode.Powers.RecordingPower", true)!;
        Type temporaryType = mod.GetType("MyMod.MyModCode.Powers.TemporaryRecordingPower", true)!;
        awaitNativePower(InvokeNativePowerApply(recordingType, player.Creature, 1m), "Add permanent Recording fixture power");
        awaitNativePower(InvokeNativePowerApply(temporaryType, player.Creature, 2m), "Add temporary Recording fixture power");
        V040Snapshot opening = CaptureState(_stateType!, GetLiveManaState(player));
        CombatRootSnapshot root = CombatRootSnapshot.Capture(combat);
        string nativeBeforeText = ContinuationStamp.CaptureLive(combat).StateText;
        CombatRootV1 nativeBefore = WithExtension(adapter.CaptureLive(combat), opening, _selectedForm.ToString());
        CombatPredictionSimulator parent = root.ForkSimulator();
        V040PredictionState parentState = GetState(parent, orb, opening);
        V040Snapshot parentBefore = parentState.Capture();
        CombatRootV1 predictedBefore = WithExtension(adapter.CapturePredicted(combat, player, parent,
            playerState.TurnNumber, root.Forecast, root.StartTurnNumber), parentBefore, _selectedForm.ToString());
        string predictedBeforeText = ContinuationStamp.CapturePredicted(player, parent,
            playerState.TurnNumber, root.Forecast, root.StartTurnNumber).StateText;
        bool rootMatchesBefore = StateDigest.Compute(nativeBefore) == StateDigest.Compute(predictedBefore)
            && StringComparer.Ordinal.Equals(nativeBeforeText, predictedBeforeText);
        if (!rootMatchesBefore)
            throw new PredictionUnsupportedException("Record/Echo fixture native/simulator opening roots differ.");

        CombatPredictionSimulator branch = parent.Fork();
        IReadOnlyList<DamageResult> simulatedResults = branch.Damage(player.Creature, 18m, (ValueProp)4, null);
        if (simulatedResults.Count != 1)
            throw new PredictionUnsupportedException($"Record/Echo fixture produced {simulatedResults.Count} simulated DamageResults; expected one.");
        V040Snapshot simulatedAfter = GetState(branch, GetBranchOrb(branch, player), opening).Capture();
        bool parentUnchanged = parentBefore == GetState(parent, orb, opening).Capture();

        Task nativeDamageTask = InvokeNativeDamage(player.Creature, 18m, (ValueProp)4);
        loop.RunUntilCompleted(nativeDamageTask, TimeSpan.FromSeconds(30), "Native RecordingPower blocked-damage API fixture");
        object nativeDamageValue = nativeDamageTask.GetType().GetProperty("Result")?.GetValue(nativeDamageTask)
            ?? throw new InvalidOperationException("Native CreatureCmd.Damage did not produce its result list.");
        object[] nativeDamageResults = ((IEnumerable)nativeDamageValue).Cast<object>().ToArray();
        if (nativeDamageResults.Length != 1)
            throw new PredictionUnsupportedException($"Record/Echo fixture produced {nativeDamageResults.Length} native DamageResults; expected one.");
        object nativeDamageResult = nativeDamageResults[0];
        decimal actualBlocked = ReadDamageDecimal(nativeDamageResult, "BlockedDamage");
        decimal actualUnblocked = ReadDamageDecimal(nativeDamageResult, "UnblockedDamage");
        V040Snapshot nativeAfter = CaptureState(_stateType!, GetLiveManaState(player));
        CombatRootV1 nativeAfterRoot = WithExtension(adapter.CaptureLive(combat), nativeAfter, _selectedForm.ToString());
        CombatRootV1 predictedAfterRoot = WithExtension(adapter.CapturePredicted(combat, player, branch,
            playerState.TurnNumber, root.Forecast, root.StartTurnNumber), simulatedAfter, _selectedForm.ToString());
        string nativeAfterText = ContinuationStamp.CaptureLive(combat).StateText;
        string predictedAfterText = ContinuationStamp.CapturePredicted(player, branch,
            playerState.TurnNumber, root.Forecast, root.StartTurnNumber).StateText;
        List<string> differences = DescribeRootDifferences(nativeAfterRoot, predictedAfterRoot);
        decimal simulatedBlocked = simulatedResults[0].BlockedDamage;
        decimal simulatedUnblocked = simulatedResults[0].UnblockedDamage;
        bool exact = nativeAfter == simulatedAfter && actualBlocked == simulatedBlocked && actualUnblocked == simulatedUnblocked
            && nativeAfter.Echo - opening.Echo == 3m * actualBlocked
            && differences.Count == 0 && parentUnchanged
            && StringComparer.Ordinal.Equals(nativeAfterText, predictedAfterText);
        var report = new
        {
            schema = "eleanor-v040-record-echo-api-differential/v1",
            status = exact ? "passed" : "failed",
            fixtureKind = "real RecordingPower/TemporaryRecordingPower hooks; no formal B card classes or card effects",
            gameVersion = GameBuildPin.Version,
            gameCommit = GameBuildPin.Commit,
            modVersion = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_VERSION"),
            selectedForm = _selectedForm.ToString(),
            opening,
            incomingDamage = 18m,
            recordingLayers = new { permanent = 1, temporary = 2, total = 3 },
            nativeResult = new { blocked = actualBlocked, unblocked = actualUnblocked },
            simulatorResult = new { blocked = simulatedBlocked, unblocked = simulatedUnblocked },
            nativeEchoAfter = nativeAfter.Echo,
            simulatorEchoAfter = simulatedAfter.Echo,
            expectedEchoDelta = 3m * actualBlocked,
            temporaryRecordingPowerPresentBeforePlayerTurnStart = true,
            temporaryExpirationBoundaryTest = "not-run",
            parentBranchUnchanged = parentUnchanged,
            openingRootsMatch = rootMatchesBefore,
            postActionRootDifferences = differences,
            nativeContinuationSha256 = Sha(nativeAfterText),
            simulatorContinuationSha256 = Sha(predictedAfterText),
            continuationMatches = StringComparer.Ordinal.Equals(nativeAfterText, predictedAfterText),
            exactStateMatch = exact,
        };
        object turnBoundary = RunRecordEchoTurnBoundary(combat, loop, player, playerState, orb, adapter, outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "eleanor-v040-record-echo-api-differential.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true }));
        if (!exact)
            throw new PredictionUnsupportedException("Record/Echo native/simulator API fixture diverged; inspect its v0.4 differential report.");
        return new { apiFixture = report, turnBoundary };

        void awaitNativePower(Task task, string label)
            => loop.RunUntilCompleted(task, TimeSpan.FromSeconds(30), label);
    }

    private static object RunRecordEchoTurnBoundary(CombatState combat, MainLoopContext loop, Player player,
        PlayerCombatState playerState, RelicModel orb, VanillaCombatRootAdapter adapter, string outputDirectory)
    {
        V040Snapshot opening = CaptureState(_stateType!, GetLiveManaState(player));
        int turnBefore = playerState.TurnNumber;
        int hpBefore = player.Creature.CurrentHp;
        int blockBefore = player.Creature.Block;
        string enemyIntentBefore = combat.Enemies.FirstOrDefault(enemy => enemy.IsAlive)?.Monster?.NextMove?.Id ?? "none";
        int nativeTemporaryBefore = CountNativePower(player, "TemporaryRecordingPower");
        int nativePermanentBefore = CountNativePower(player, "RecordingPower");
        if (nativeTemporaryBefore != 2 || nativePermanentBefore != 1)
            throw new PredictionUnsupportedException($"Turn-boundary fixture expected temporary/permanent Recording stacks 2/1; got {nativeTemporaryBefore}/{nativePermanentBefore}.");

        CombatRootSnapshot root = CombatRootSnapshot.Capture(combat);
        string nativeBeforeText = ContinuationStamp.CaptureLive(combat).StateText;
        CombatPredictionSimulator parent = root.ForkSimulator();
        V040PredictionState parentState = GetState(parent, orb, opening);
        V040Snapshot parentBefore = parentState.Capture();
        CombatPredictionSimulator branch = parent.Fork();
        _ = GetState(branch, orb, opening);
        SimulatedCombatState shadow = (SimulatedCombatState)branch.State.CombatState;
        CombatRootV1 nativeBeforeRoot = WithExtension(adapter.CaptureLive(combat), opening, _selectedForm.ToString());
        CombatRootV1 predictedBeforeRoot = WithExtension(adapter.CapturePredicted(combat, player, parent,
            turnBefore, root.Forecast, root.StartTurnNumber), parentBefore, _selectedForm.ToString());
        string predictedBeforeText = ContinuationStamp.CapturePredicted(player, parent, turnBefore,
            root.Forecast, root.StartTurnNumber).StateText;
        bool openingExact = DescribeRootDifferences(nativeBeforeRoot, predictedBeforeRoot).Count == 0
            && StringComparer.Ordinal.Equals(nativeBeforeText, predictedBeforeText);
        if (!openingExact)
            throw new PredictionUnsupportedException("Native/simulator roots differed before scheduled Recording expiry and enemy turn.");

        var driver = new CombatBeamSolver(root, SolverDisplayNames.Capture(combat), BattleDamageTracker.Observe(combat),
            SolverController.CaptureSearchPolicy(SolverSettings.Capture(), combat, false, null));
        MethodInfo advanceRound = typeof(CombatBeamSolver).GetMethod("AdvanceRound", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(CombatBeamSolver), "AdvanceRound");
        _lastTurnStartSimulator = null;
        _lastTurnStartSnapshot = null;
        _lastTurnStartState = null;
        _lastRecordingDamageState = null;
        _lastTurnStartNumber = 0;
        object?[] advanceArguments = [branch, shadow, turnBefore - root.StartTurnNumber, new HashSet<uint>(), 0, null, null];
        object? boundary = advanceRound.Invoke(driver, advanceArguments);
        if (!StringComparer.Ordinal.Equals(boundary?.ToString(), "None"))
            throw new PredictionUnsupportedException("CombatSolver stopped before the real enemy/next-player boundary: " + boundary);
        CombatPredictionSimulator scheduledBranch = _lastTurnStartSimulator
            ?? throw new PredictionUnsupportedException("CombatSolver did not invoke the registered CrackedCrystalOrb turn-start mirror on its scheduled continuation.");
        if (_lastTurnStartNumber != turnBefore + 1 || _lastTurnStartSnapshot is null)
            throw new PredictionUnsupportedException($"CrackedCrystalOrb turn-start mirror ran at turn {_lastTurnStartNumber}; expected {turnBefore + 1}.");
        SimulatedCombatState scheduledShadow = scheduledBranch.State.CombatState as SimulatedCombatState
            ?? throw new PredictionUnsupportedException("CrackedCrystalOrb turn-start mirror did not retain a simulated combat branch.");
        _ = scheduledShadow.ConsumePlayerTurnEndRequest();
        if (!CombatBeamSolver.SettleReplayActionBoundary(scheduledBranch, scheduledShadow))
            throw new PredictionUnsupportedException("CombatSolver left a pending choice at the scheduled turn boundary.");
        int shuffleCount = advanceArguments[4] is int nextShuffleCount ? nextShuffleCount : 0;
        string simulatorAfterText = ContinuationStamp.CapturePredicted(player, scheduledBranch, turnBefore + 1,
            root.Forecast, root.StartTurnNumber).StateText;

        CombatManager.Instance.OnEndedTurnLocally();
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new EndPlayerTurnAction(player, turnBefore));
        loop.RunUntilCompleted(RunManager.Instance.ActionExecutor.FinishedExecutingActions(), TimeSpan.FromSeconds(45),
            "v0.4.1 native EndTurn with enemy phase, temporary Recording expiry and next hand draw");
        if (player.PlayerCombatState is not { Phase: PlayerTurnPhase.Play } nextState || nextState.TurnNumber != turnBefore + 1)
            throw new PredictionUnsupportedException("Native EndPlayerTurn did not reach the next player decision after the enemy phase.");

        V040Snapshot nativeAfter = CaptureState(_stateType!, GetLiveManaState(player));
        V040PredictionState branchStateAfter = _lastTurnStartState
            ?? throw new PredictionUnsupportedException("CrackedCrystalOrb turn-start adapter did not retain its fork-local custom state.");
        V040Snapshot simulatedAfter = _lastTurnStartSnapshot
            ?? throw new PredictionUnsupportedException("CrackedCrystalOrb turn-start adapter did not capture its updated custom state.");
        CombatRootV1 nativeAfterRoot = WithExtension(adapter.CaptureLive(combat), nativeAfter, _selectedForm.ToString());
        CombatRootV1 simulatedAfterRoot = WithExtension(adapter.CapturePredicted(combat, player, scheduledBranch,
            nextState.TurnNumber, root.Forecast, root.StartTurnNumber), simulatedAfter, _selectedForm.ToString());
        string nativeAfterText = ContinuationStamp.CaptureLive(combat).StateText;
        string predictedAfterText = ContinuationStamp.CapturePredicted(player, scheduledBranch, nextState.TurnNumber,
            root.Forecast, root.StartTurnNumber).StateText;
        List<string> boundaryDifferences = DescribeRootDifferences(nativeAfterRoot, simulatedAfterRoot);
        int nativeTemporaryAfter = CountNativePower(player, "TemporaryRecordingPower");
        int nativePermanentAfter = CountNativePower(player, "RecordingPower");
        int simTemporaryAfter = CountSimulatedPower(scheduledBranch, "TemporaryRecordingPower", player.Creature);
        int simPermanentAfter = CountSimulatedPower(scheduledBranch, "RecordingPower", player.Creature);
        bool temporaryExpired = nativeTemporaryAfter == 0 && simTemporaryAfter == 0;
        bool permanentStayed = nativePermanentAfter == 1 && simPermanentAfter == 1;
        bool echoBoundaryMatches = nativeAfter.Echo == simulatedAfter.Echo;
        bool branchParentUnchanged = parentBefore == parentState.Capture();
        bool boundaryContinuationMatches = StringComparer.Ordinal.Equals(nativeAfterText, predictedAfterText);
        bool boundaryExact = nativeAfter == simulatedAfter && boundaryDifferences.Count == 0
            && boundaryContinuationMatches
            && temporaryExpired && permanentStayed && echoBoundaryMatches && branchParentUnchanged;
        var boundaryReport = new
        {
            schema = "eleanor-v041-recording-two-turn-scheduled-boundary/v1",
            status = boundaryExact ? "passed" : "failed",
            gameVersion = GameBuildPin.Version,
            gameCommit = GameBuildPin.Commit,
            modVersion = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_VERSION"),
            modAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_DLL")!))).ToLowerInvariant(),
            fixture = "real native EndPlayerTurnAction, enemy phase and next hand draw; simulator CombatBeamSolver.AdvanceRound",
            fromTurn = turnBefore,
            toTurn = nextState.TurnNumber,
            enemyPhaseCompleted = true,
            enemyIntentBefore,
            enemyIntentAfter = combat.Enemies.FirstOrDefault(enemy => enemy.IsAlive)?.Monster?.NextMove?.Id ?? "none",
            playerHpBefore = hpBefore,
            playerHpAfterEnemyPhase = player.Creature.CurrentHp,
            playerBlockBefore = blockBefore,
            playerBlockAfterEnemyPhase = player.Creature.Block,
            reverseStartTurn = new { opening.Mana, after = nativeAfter.Mana, opening.Stance, afterStance = nativeAfter.Stance },
            temporaryRecording = new { before = nativeTemporaryBefore, nativeAfter = nativeTemporaryAfter, simulatedAfter = simTemporaryAfter, expired = temporaryExpired },
            permanentRecording = new { before = nativePermanentBefore, nativeAfter = nativePermanentAfter, simulatedAfter = simPermanentAfter, stayed = permanentStayed },
            echo = new { before = opening.Echo, afterTurnStart = nativeAfter.Echo, simulatedAfterTurnStart = simulatedAfter.Echo,
                enemyPhaseDelta = nativeAfter.Echo - opening.Echo, matchesAcrossBoundary = echoBoundaryMatches },
            openingRootsMatch = openingExact,
            turnBoundaryRootDifferences = boundaryDifferences,
            nativeAfter,
            simulatedAfter,
            boundaryContinuationMatches,
            nativeContinuationSha256 = Sha(nativeAfterText),
            simulatedContinuationSha256 = Sha(predictedAfterText),
            adapterInvocations = AdapterInvocations.TakeLast(8).ToArray(),
            parentBranchUnchanged = branchParentUnchanged,
            exactStateMatch = boundaryExact,
        };
        File.WriteAllText(Path.Combine(outputDirectory, "eleanor-v041-recording-two-turn-scheduled-boundary.json"),
            JsonSerializer.Serialize(boundaryReport, new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true }));
        if (!boundaryExact)
            throw new PredictionUnsupportedException("Native and simulator diverged across the scheduled temporary Recording expiry/enemy-turn boundary.");

        int blockToFive = Math.Max(0, 5 - player.Creature.Block);
        if (blockToFive > 0)
        {
            loop.RunUntilCompleted(InvokeNativeBlockGain(player.Creature, blockToFive), TimeSpan.FromSeconds(30),
                "Seed post-expiry native Block counter");
            scheduledBranch.GainBlock(player.Creature, blockToFive, (ValueProp)4, null, null);
        }
        CombatRootV1 nativeDamageRoot = WithExtension(adapter.CaptureLive(combat), nativeAfter, _selectedForm.ToString());
        CombatRootV1 simulatedDamageRoot = WithExtension(adapter.CapturePredicted(combat, player, scheduledBranch,
            nextState.TurnNumber, root.Forecast, root.StartTurnNumber), simulatedAfter, _selectedForm.ToString());
        bool damageOpeningExact = DescribeRootDifferences(nativeDamageRoot, simulatedDamageRoot).Count == 0;
        if (!damageOpeningExact)
            throw new PredictionUnsupportedException("Native/simulator roots differed before the post-expiry Recording damage check.");

        IReadOnlyList<DamageResult> simulatedDamage = scheduledBranch.Damage(player.Creature, 8m, (ValueProp)4, null);
        Task nativeDamageTask = InvokeNativeDamage(player.Creature, 8m, (ValueProp)4);
        loop.RunUntilCompleted(nativeDamageTask, TimeSpan.FromSeconds(30), "Native post-expiry permanent Recording damage check");
        object resultValue = nativeDamageTask.GetType().GetProperty("Result")?.GetValue(nativeDamageTask)
            ?? throw new PredictionUnsupportedException("Native post-expiry damage result was not returned.");
        DamageResult nativeDamage = ((IEnumerable)resultValue).Cast<object>().Select(result => result)
            .Select(result => (DamageResult)result).Single();
        if (simulatedDamage.Count != 1)
            throw new PredictionUnsupportedException("Simulator post-expiry damage did not produce exactly one result.");
        V040Snapshot nativeEnd = CaptureState(_stateType!, GetLiveManaState(player));
        V040Snapshot simulatedEnd = (_lastRecordingDamageState ?? branchStateAfter).Capture();
        CombatRootV1 nativeEndRoot = WithExtension(adapter.CaptureLive(combat), nativeEnd, _selectedForm.ToString());
        CombatRootV1 simulatedEndRoot = WithExtension(adapter.CapturePredicted(combat, player, scheduledBranch,
            nextState.TurnNumber, root.Forecast, root.StartTurnNumber), simulatedEnd, _selectedForm.ToString());
        string nativeEndText = ContinuationStamp.CaptureLive(combat).StateText;
        string simulatedEndText = ContinuationStamp.CapturePredicted(player, scheduledBranch, nextState.TurnNumber,
            root.Forecast, root.StartTurnNumber).StateText;
        List<string> finalDifferences = DescribeRootDifferences(nativeEndRoot, simulatedEndRoot);
        decimal nativeBlocked = nativeDamage.BlockedDamage;
        decimal nativeUnblocked = nativeDamage.UnblockedDamage;
        decimal simulatedBlocked = simulatedDamage[0].BlockedDamage;
        decimal simulatedUnblocked = simulatedDamage[0].UnblockedDamage;
        bool postExpiryExact = nativeEnd == simulatedEnd && nativeBlocked == simulatedBlocked
            && nativeUnblocked == simulatedUnblocked && nativeEnd.Echo - nativeAfter.Echo == nativeBlocked
            && simulatedEnd.Echo - simulatedAfter.Echo == simulatedBlocked
            && finalDifferences.Count == 0 && StringComparer.Ordinal.Equals(nativeEndText, simulatedEndText)
            && CountNativePower(player, "TemporaryRecordingPower") == 0
            && CountNativePower(player, "RecordingPower") == 1
            && CountSimulatedPower(scheduledBranch, "TemporaryRecordingPower", player.Creature) == 0
            && CountSimulatedPower(scheduledBranch, "RecordingPower", player.Creature) == 1
            && parentBefore == parentState.Capture();
        var report = new
        {
            schema = "eleanor-v041-recording-two-turn-scheduled-boundary/v1",
            status = boundaryExact && postExpiryExact ? "passed" : "failed",
            gameVersion = GameBuildPin.Version,
            gameCommit = GameBuildPin.Commit,
            modVersion = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_VERSION"),
            modAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_DLL")!))).ToLowerInvariant(),
            fixture = "real native EndPlayerTurnAction, enemy phase and next hand draw; simulator CombatBeamSolver.AdvanceRound",
            fromTurn = turnBefore,
            toTurn = nextState.TurnNumber,
            enemyPhaseCompleted = true,
            enemyIntentBefore,
            enemyIntentAfter = combat.Enemies.FirstOrDefault(enemy => enemy.IsAlive)?.Monster?.NextMove?.Id ?? "none",
            playerHpBefore = hpBefore,
            playerHpAfterEnemyPhase = player.Creature.CurrentHp,
            playerBlockBefore = blockBefore,
            playerBlockAfterEnemyPhase = player.Creature.Block,
            shuffleCrossings = shuffleCount,
            reverseStartTurn = new { opening.Mana, after = nativeAfter.Mana, opening.Stance, afterStance = nativeAfter.Stance },
            temporaryRecording = new { before = nativeTemporaryBefore, nativeAfter = nativeTemporaryAfter, simulatedAfter = simTemporaryAfter, expired = temporaryExpired },
            permanentRecording = new { before = nativePermanentBefore, nativeAfter = nativePermanentAfter, simulatedAfter = simPermanentAfter, stayed = permanentStayed },
            echo = new { before = opening.Echo, afterTurnStart = nativeAfter.Echo, simulatedAfterTurnStart = simulatedAfter.Echo,
                enemyPhaseDelta = nativeAfter.Echo - opening.Echo, afterDamage = nativeEnd.Echo, simulatedAfterDamage = simulatedEnd.Echo,
                matchesAcrossTurnStart = echoBoundaryMatches },
            postExpiryIncoming = new { amount = 8m, nativeBlocked, nativeUnblocked, simulatedBlocked, simulatedUnblocked,
                expectedEchoDelta = nativeBlocked, actualEchoDelta = nativeEnd.Echo - nativeAfter.Echo },
            openingRootsMatch = openingExact,
            turnBoundaryRootDifferences = boundaryDifferences,
            boundaryContinuationMatches,
            damageOpeningRootMatches = damageOpeningExact,
            postExpiryRootDifferences = finalDifferences,
            postExpiryContinuationMatches = StringComparer.Ordinal.Equals(nativeEndText, simulatedEndText),
            parentBranchUnchanged = parentBefore == parentState.Capture(),
            exactStateMatch = boundaryExact && postExpiryExact,
        };
        File.WriteAllText(Path.Combine(outputDirectory, "eleanor-v041-recording-two-turn-scheduled-boundary.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true }));
        if (!postExpiryExact)
            throw new PredictionUnsupportedException("Native/simulator post-expiry permanent Recording check diverged.");
        return report;
    }

    private static object RunReverseUpkeepFixture(CombatState combat, MainLoopContext loop, Player player,
        PlayerCombatState playerState, RelicModel orb, VanillaCombatRootAdapter adapter, string outputDirectory)
    {
        if (orb.GetType().Name != "CrackedCrystalOrb")
            throw new PredictionUnsupportedException("This Reverse upkeep fixture is pinned to the incomplete CrackedCrystalOrb boundary.");

        Assembly mod = _stateType!.Assembly;
        Type runtimeType = mod.GetType("MyMod.MyModCode.Combat.ManaRuntime", true)!;
        object runtime = runtimeType.GetMethod("For", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [player])!;
        object liveMana = runtimeType.GetProperty("State")!.GetValue(runtime)!;
        object liveLedger = runtimeType.GetField("_reverse", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(runtime)
            ?? throw new PredictionUnsupportedException("ManaRuntime reverse ledger is unavailable.");
        if (CaptureState(_stateType, liveMana).Stance != "Reverse")
            throw new PredictionUnsupportedException("Reverse upkeep fixture requires the real form-choice branch to be Reverse.");

        // Success boundary: execute the exact native StartTurn implementation and independently
        // apply ManaState.PayUpkeep plus the audited reverse ledger to a fork.
        V040Snapshot successOpening = CaptureState(_stateType, liveMana);
        CombatRootSnapshot successRoot = CombatRootSnapshot.Capture(combat);
        string successBeforeText = ContinuationStamp.CaptureLive(combat).StateText;
        CombatRootV1 successBeforeNative = WithExtension(adapter.CaptureLive(combat), successOpening, "Reverse");
        CombatPredictionSimulator successParent = successRoot.ForkSimulator();
        V040PredictionState successParentState = GetState(successParent, orb, successOpening);
        V040Snapshot successParentBefore = successParentState.Capture();
        CombatRootV1 successBeforeSim = WithExtension(adapter.CapturePredicted(combat, player, successParent,
            playerState.TurnNumber, successRoot.Forecast, successRoot.StartTurnNumber), successParentBefore, "Reverse");
        string successBeforeSimText = ContinuationStamp.CapturePredicted(player, successParent,
            playerState.TurnNumber, successRoot.Forecast, successRoot.StartTurnNumber).StateText;
        bool successOpeningMatches = StateDigest.Compute(successBeforeNative) == StateDigest.Compute(successBeforeSim)
            && StringComparer.Ordinal.Equals(successBeforeText, successBeforeSimText);
        if (!successOpeningMatches)
            throw new PredictionUnsupportedException("Reverse upkeep success fixture native/simulator opening roots differ.");

        CombatPredictionSimulator successBranch = successParent.Fork();
        V040PredictionState successState = GetState(successBranch, orb, successOpening);
        object successLedger = CreateSeededReverseLedger(mod, liveLedger, player.Creature, successOpening.CompleteOrb, loop);
        object successUpkeep = successState.PayUpkeep(playerState.Energy);
        ApplyUpkeepToBranch(successBranch, successState, player, player.Creature, successLedger,
            successUpkeep, successOpening.CompleteOrb, loop, turnStartRestore: true);
        object successNativeTask = InvokeStartTurn(runtime, new ThrowingPlayerChoiceContext(), complete: false);
        loop.RunUntilCompleted((Task)successNativeTask, TimeSpan.FromSeconds(30), "Native Reverse successful upkeep boundary");
        V040Snapshot successNativeAfter = CaptureState(_stateType, liveMana);
        V040Snapshot successSimAfter = successState.Capture();
        CombatRootV1 successNativeAfterRoot = WithExtension(adapter.CaptureLive(combat), successNativeAfter, "Reverse");
        CombatRootV1 successSimAfterRoot = WithExtension(adapter.CapturePredicted(combat, player, successBranch,
            playerState.TurnNumber, successRoot.Forecast, successRoot.StartTurnNumber), successSimAfter, "Reverse");
        string successNativeAfterText = ContinuationStamp.CaptureLive(combat).StateText;
        string successSimAfterText = ContinuationStamp.CapturePredicted(player, successBranch,
            playerState.TurnNumber, successRoot.Forecast, successRoot.StartTurnNumber).StateText;
        int successNativeHp = player.Creature.CurrentHp;
        int successNativeBlock = player.Creature.Block;
        int successNativeStrength = GetLivePowerAmount<StrengthPower>(player.Creature);
        int successNativeDexterity = GetLivePowerAmount<DexterityPower>(player.Creature);
        List<string> successDifferences = DescribeRootDifferences(successNativeAfterRoot, successSimAfterRoot);
        bool successParentUnchanged = successParentBefore == GetState(successParent, orb, successOpening).Capture();
        bool successExact = successNativeAfter == successSimAfter && successDifferences.Count == 0
            && successParentUnchanged && StringComparer.Ordinal.Equals(successNativeAfterText, successSimAfterText)
            && ReadUpkeepBoolean(successUpkeep, "Success")
            && successOpening.Mana - successNativeAfter.Mana == ReadUpkeepInt(successUpkeep, "ManaPaid");

        // The next failure input is reached through the pinned ManaState.TryPayMana API on both
        // live and branch-local copies, then checked as an exact new root before invoking StartTurn.
        object successLiveMana = runtimeType.GetProperty("State")!.GetValue(runtime)!;
        int remainingMana = ReadInt(successLiveMana, "Mana");
        MethodInfo tryPayMana = _stateType.GetMethod("TryPayMana")!;
        bool nativeDrained = (bool)tryPayMana.Invoke(successLiveMana, [remainingMana])!;
        bool simDrained = successState.TryPayMana(remainingMana);
        if (!nativeDrained || !simDrained || remainingMana <= 0)
            throw new PredictionUnsupportedException("Could not prepare the failed-upkeep boundary through the actual ManaState API.");

        V040Snapshot failureOpening = CaptureState(_stateType, successLiveMana);
        CombatRootSnapshot failureRoot = CombatRootSnapshot.Capture(combat);
        string failureBeforeText = ContinuationStamp.CaptureLive(combat).StateText;
        CombatRootV1 failureBeforeNative = WithExtension(adapter.CaptureLive(combat), failureOpening, "Reverse");
        CombatPredictionSimulator failureParent = failureRoot.ForkSimulator();
        V040PredictionState failureParentState = GetState(failureParent, orb, failureOpening);
        V040Snapshot failureParentBefore = failureParentState.Capture();
        CombatRootV1 failureBeforeSim = WithExtension(adapter.CapturePredicted(combat, player, failureParent,
            playerState.TurnNumber, failureRoot.Forecast, failureRoot.StartTurnNumber), failureParentBefore, "Reverse");
        string failureBeforeSimText = ContinuationStamp.CapturePredicted(player, failureParent,
            playerState.TurnNumber, failureRoot.Forecast, failureRoot.StartTurnNumber).StateText;
        bool failureOpeningMatches = StateDigest.Compute(failureBeforeNative) == StateDigest.Compute(failureBeforeSim)
            && StringComparer.Ordinal.Equals(failureBeforeText, failureBeforeSimText);
        if (!failureOpeningMatches)
            throw new PredictionUnsupportedException("Reverse upkeep failure fixture native/simulator opening roots differ.");

        CombatPredictionSimulator failureBranch = failureParent.Fork();
        V040PredictionState failureState = GetState(failureBranch, orb, failureOpening);
        object failureLedger = CreateSeededReverseLedger(mod, liveLedger, player.Creature, failureOpening.CompleteOrb, loop);
        object failureUpkeep = failureState.PayUpkeep(playerState.Energy);
        ApplyUpkeepToBranch(failureBranch, failureState, player, player.Creature, failureLedger,
            failureUpkeep, failureOpening.CompleteOrb, loop, turnStartRestore: true);
        int failureHpBefore = player.Creature.CurrentHp;
        int failureStrengthBefore = GetLivePowerAmount<StrengthPower>(player.Creature);
        int failureDexterityBefore = GetLivePowerAmount<DexterityPower>(player.Creature);
        object failureNativeTask = InvokeStartTurn(runtime, new ThrowingPlayerChoiceContext(), complete: false);
        loop.RunUntilCompleted((Task)failureNativeTask, TimeSpan.FromSeconds(30), "Native Reverse failed upkeep, stat withdrawal and HP penalty");

        V040Snapshot failureNativeAfter = CaptureState(_stateType, successLiveMana);
        V040Snapshot failureSimAfter = failureState.Capture();
        CombatRootV1 failureNativeAfterRoot = WithExtension(adapter.CaptureLive(combat), failureNativeAfter, "Neutral");
        CombatRootV1 failureSimAfterRoot = WithExtension(adapter.CapturePredicted(combat, player, failureBranch,
            playerState.TurnNumber, failureRoot.Forecast, failureRoot.StartTurnNumber), failureSimAfter, "Neutral");
        string failureNativeAfterText = ContinuationStamp.CaptureLive(combat).StateText;
        string failureSimAfterText = ContinuationStamp.CapturePredicted(player, failureBranch,
            playerState.TurnNumber, failureRoot.Forecast, failureRoot.StartTurnNumber).StateText;
        List<string> failureDifferences = DescribeRootDifferences(failureNativeAfterRoot, failureSimAfterRoot);
        bool failureParentUnchanged = failureParentBefore == GetState(failureParent, orb, failureOpening).Capture();
        int actualFailureHpLoss = failureHpBefore - player.Creature.CurrentHp;
        int actualStrengthRemoved = failureStrengthBefore - GetLivePowerAmount<StrengthPower>(player.Creature);
        int actualDexterityRemoved = failureDexterityBefore - GetLivePowerAmount<DexterityPower>(player.Creature);
        int requestedFailureHpLoss = ReadUpkeepInt(failureUpkeep, "HpLoss");
        bool failureExact = failureNativeAfter == failureSimAfter && failureDifferences.Count == 0
            && failureParentUnchanged && StringComparer.Ordinal.Equals(failureNativeAfterText, failureSimAfterText)
            && !ReadUpkeepBoolean(failureUpkeep, "Success") && requestedFailureHpLoss > 0
            && actualFailureHpLoss == requestedFailureHpLoss
            && failureStrengthBefore > 0 && failureDexterityBefore > 0
            && GetLivePowerAmount<StrengthPower>(player.Creature) == 0
            && GetLivePowerAmount<DexterityPower>(player.Creature) == 0;

        var report = new
        {
            schema = "eleanor-v040-reverse-upkeep-boundary/v1",
            status = successExact && failureExact ? "passed" : "failed",
            gameVersion = GameBuildPin.Version,
            gameCommit = GameBuildPin.Commit,
            modVersion = Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_VERSION"),
            modAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_DLL")!))).ToLowerInvariant(),
            fixture = "native ManaRuntime.StartTurn called directly at the player boundary; no enemy turn, scheduler transition or hand draw",
            success = new
            {
                opening = successOpening,
                nativeAfter = successNativeAfter,
                simulatorAfter = successSimAfter,
                nativeHp = successNativeHp,
                nativeBlock = successNativeBlock,
                nativeStrength = successNativeStrength,
                nativeDexterity = successNativeDexterity,
                openingRootsMatch = successOpeningMatches,
                postActionRootDifferences = successDifferences,
                parentBranchUnchanged = successParentUnchanged,
                continuationMatches = StringComparer.Ordinal.Equals(successNativeAfterText, successSimAfterText),
                exactStateMatch = successExact,
            },
            failure = new
            {
                opening = failureOpening,
                nativeAfter = failureNativeAfter,
                simulatorAfter = failureSimAfter,
                nativeHpBefore = failureHpBefore,
                nativeHp = player.Creature.CurrentHp,
                nativeHpLoss = actualFailureHpLoss,
                requestedHpLoss = requestedFailureHpLoss,
                nativeBlock = player.Creature.Block,
                nativeStrengthBefore = failureStrengthBefore,
                nativeStrength = GetLivePowerAmount<StrengthPower>(player.Creature),
                nativeStrengthRemoved = actualStrengthRemoved,
                nativeDexterityBefore = failureDexterityBefore,
                nativeDexterity = GetLivePowerAmount<DexterityPower>(player.Creature),
                nativeDexterityRemoved = actualDexterityRemoved,
                openingRootsMatch = failureOpeningMatches,
                postActionRootDifferences = failureDifferences,
                parentBranchUnchanged = failureParentUnchanged,
                continuationMatches = StringComparer.Ordinal.Equals(failureNativeAfterText, failureSimAfterText),
                exactStateMatch = failureExact,
                manaWasDrainedViaNativeStateApi = nativeDrained,
            },
            completeOrbUpkeep = "not-run",
            fullTurnSchedulerAndEnemyTransition = "not-run",
            temporaryRecordingPowerExpiration = "not-run",
        };
        File.WriteAllText(Path.Combine(outputDirectory, "eleanor-v040-reverse-upkeep-boundary.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true }));
        if (!successExact || !failureExact)
            throw new PredictionUnsupportedException("Reverse upkeep native/simulator boundary comparison diverged; inspect its v0.4 differential report.");
        return report;
    }

    private static void ApplyUpkeepToBranch(CombatPredictionSimulator simulator, V040PredictionState manaState,
        Player player, Creature owner, object reverseLedger, object upkeep, bool complete, MainLoopContext loop,
        bool turnStartRestore)
    {
        int energyPaid = ReadUpkeepInt(upkeep, "EnergyPaid");
        int manaPaid = ReadUpkeepInt(upkeep, "ManaPaid");
        int hpLoss = ReadUpkeepInt(upkeep, "HpLoss");
        if (energyPaid > 0) simulator.LoseEnergy(player, energyPaid);
        if (manaPaid > 0)
        {
            int blockPerMana = ((SimulatedCombatState)simulator.State.CombatState).EffectivePowers()
                .OfType<PowerModel>().Where(power => power.GetType().Name == "ManaToShieldPower" && power.Owner == owner)
                .Sum(power => power.Amount);
            if (blockPerMana > 0)
                simulator.GainBlock(owner, (decimal)blockPerMana * manaPaid, (ValueProp)4, null, null);
        }

        bool reverse = manaState.Capture().Stance == "Reverse";
        object task = InvokeReverseLedgerSync(reverseLedger, reverse, complete,
            CreateSimulatedReversePowerPort(simulator, owner), turnStartRestore);
        loop.RunUntilCompleted((Task)task, TimeSpan.FromSeconds(30), "Branch ReversePowerLedger.Sync");
        if (!(bool)task.GetType().GetProperty("Result")!.GetValue(task)!)
            throw new PredictionUnsupportedException("The actual ReversePowerLedger marked the branch projection unresolved.");
        if (hpLoss > 0)
        {
            IReadOnlyList<DamageResult> results = simulator.Damage(owner, hpLoss, (ValueProp)6, null);
            if (results.Count == 0) throw new PredictionUnsupportedException("Reverse upkeep HP penalty produced no simulated DamageResult.");
        }
    }

    private static object CreateSeededReverseLedger(Assembly mod, object liveLedger, Creature owner,
        bool complete, MainLoopContext loop)
    {
        if (complete)
            throw new PredictionUnsupportedException("Complete Crystal Orb Reverse upkeep requires a separate boundary fixture.");
        string? issue = liveLedger.GetType().GetProperty("Issue")?.GetValue(liveLedger) as string;
        if (!string.IsNullOrEmpty(issue))
            throw new PredictionUnsupportedException($"Native ReversePowerLedger is unresolved: {issue}");
        Type sourceType = mod.GetType("MyMod.MyModCode.Combat.ReverseSource", true)!;
        Type statType = mod.GetType("MyMod.MyModCode.Combat.ReverseStat", true)!;
        MethodInfo owned = liveLedger.GetType().GetMethod("Owned")!;
        int strength = Convert.ToInt32(owned.Invoke(liveLedger, [Enum.Parse(sourceType, "Base"), Enum.Parse(statType, "Strength")]));
        int dexterity = Convert.ToInt32(owned.Invoke(liveLedger, [Enum.Parse(sourceType, "Base"), Enum.Parse(statType, "Dexterity")]));
        if (strength <= 0 || dexterity <= 0 || GetLivePowerAmount<StrengthPower>(owner) < strength
            || GetLivePowerAmount<DexterityPower>(owner) < dexterity)
            throw new PredictionUnsupportedException("Live Reverse-owned Strength/Dexterity values do not match the expected ledger boundary.");

        Type ledgerType = mod.GetType("MyMod.MyModCode.Combat.ReversePowerLedger", true)!;
        object ledger = Activator.CreateInstance(ledgerType)!;
        var primeProxy = CreateReversePowerPort(mod, (statName, delta) =>
        {
            int actual = statName == "Strength" ? strength : statName == "Dexterity" ? dexterity
                : throw new PredictionUnsupportedException($"Unknown ReverseStat {statName}.");
            if (delta != actual)
                throw new PredictionUnsupportedException($"ReversePowerLedger requested seed delta {delta} for {statName}; live owned amount is {actual}.");
            PowerModel identity = statName == "Strength"
                ? owner.GetPower<StrengthPower>() ?? throw new PredictionUnsupportedException("Native Strength power identity is missing.")
                : owner.GetPower<DexterityPower>() ?? throw new PredictionUnsupportedException("Native Dexterity power identity is missing.");
            return CreateCompletedPowerDelta(mod, actual, identity);
        });
        object seedTask = InvokeReverseLedgerSync(ledger, reverse: true, complete: false, primeProxy, turnStartRestore: false);
        loop.RunUntilCompleted((Task)seedTask, TimeSpan.FromSeconds(30), "Seed branch ReversePowerLedger from live ownership");
        if (!(bool)seedTask.GetType().GetProperty("Result")!.GetValue(seedTask)!)
            throw new PredictionUnsupportedException("Could not seed a fork-local ReversePowerLedger from native ownership.");
        return ledger;
    }

    private static object CreateSimulatedReversePowerPort(CombatPredictionSimulator simulator, Creature owner)
    {
        Assembly mod = _stateType!.Assembly;
        return CreateReversePowerPort(mod, (statName, delta) =>
        {
            Type powerType = statName switch
            {
                "Strength" => typeof(StrengthPower),
                "Dexterity" => typeof(DexterityPower),
                _ => throw new PredictionUnsupportedException($"Unknown ReverseStat {statName}."),
            };
            SimulatedCombatState sim = simulator.State.CombatState as SimulatedCombatState
                ?? throw new PredictionUnsupportedException("Reverse upkeep requires branch-local SimulatedCombatState.");
            int before = sim.EffectivePowers().OfType<PowerModel>()
                .Where(power => power.GetType() == powerType && power.Owner == owner).Sum(power => power.Amount);
            sim.ApplyPower(powerType, owner, delta, owner);
            int after = sim.EffectivePowers().OfType<PowerModel>()
                .Where(power => power.GetType() == powerType && power.Owner == owner).Sum(power => power.Amount);
            PowerModel? identity = sim.EffectivePowers().OfType<PowerModel>()
                .FirstOrDefault(power => power.GetType() == powerType && power.Owner == owner && power.Amount != 0);
            return CreateCompletedPowerDelta(mod, after - before, identity);
        });
    }

    private static object CreateReversePowerPort(Assembly mod, Func<string, int, object> change)
    {
        Type interfaceType = mod.GetType("MyMod.MyModCode.Combat.IReversePowerPort", true)!;
        MethodInfo create = typeof(DispatchProxy).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Create" && method.IsGenericMethodDefinition
                && method.GetGenericArguments().Length == 2 && method.GetParameters().Length == 0)
            .MakeGenericMethod(interfaceType, typeof(ReversePowerPortProxy));
        var proxy = (ReversePowerPortProxy)create.Invoke(null, null)!;
        proxy.Configure(change);
        return proxy;
    }

    private static object CreateCompletedPowerDelta(Assembly mod, int actual, object? identity)
    {
        Type deltaType = mod.GetType("MyMod.MyModCode.Combat.PowerDelta", true)!;
        object delta = Activator.CreateInstance(deltaType, [actual, true, null, identity])!;
        MethodInfo fromResult = typeof(Task).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "FromResult" && method.IsGenericMethodDefinition)
            .MakeGenericMethod(deltaType);
        return fromResult.Invoke(null, [delta])!;
    }

    private static object InvokeReverseLedgerSync(object ledger, bool reverse, bool complete,
        object port, bool turnStartRestore)
        => ledger.GetType().GetMethod("Sync")!.Invoke(ledger, [reverse, complete, port, turnStartRestore])
            ?? throw new InvalidOperationException("ReversePowerLedger.Sync returned null.");

    private static object InvokeStartTurn(object runtime, PlayerChoiceContext context, bool complete)
        => runtime.GetType().GetMethod("StartTurn", BindingFlags.Public | BindingFlags.Instance,
            binder: null, types: [typeof(PlayerChoiceContext), typeof(bool)], modifiers: null)?.Invoke(runtime, [context, complete])
            ?? throw new MissingMethodException(runtime.GetType().FullName, "StartTurn(PlayerChoiceContext, Boolean)");

    private static bool ReadUpkeepBoolean(object upkeep, string property)
        => Convert.ToBoolean(upkeep.GetType().GetProperty(property)!.GetValue(upkeep));

    private static int ReadUpkeepInt(object upkeep, string property)
        => Convert.ToInt32(upkeep.GetType().GetProperty(property)!.GetValue(upkeep));

    private static int GetLivePowerAmount<TPower>(Creature owner) where TPower : PowerModel
        => owner.Powers.OfType<TPower>().Sum(power => power.Amount);

    private static object GetLiveManaState(Player player)
    {
        Type runtimeType = _stateType!.Assembly.GetType("MyMod.MyModCode.Combat.ManaRuntime", true)!;
        object runtime = runtimeType.GetMethod("For", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [player])!;
        return runtimeType.GetProperty("State")!.GetValue(runtime)!;
    }

    private static V040PredictionState GetState(AfterDamageReceivedMirrorContext context, RelicModel orb)
        => context.StateStore.Get<RelicModel, V040PredictionState>(orb,
            _ => CreatePredictionState(orb.Owner, CaptureState(_stateType!, GetLiveManaState(orb.Owner))));

    private static Task InvokeNativeBlockGain(Creature creature, decimal amount)
    {
        MethodInfo method = typeof(CreatureCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(candidate => candidate.Name == "GainBlock" && candidate.GetParameters().Select(p => p.ParameterType)
                .SequenceEqual([typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardPlay), typeof(bool)]));
        return (Task)(method.Invoke(null, [creature, amount, (ValueProp)4, null, false])
            ?? throw new InvalidOperationException("CreatureCmd.GainBlock returned null."));
    }

    private static Task InvokeNativePowerApply(Type powerType, Creature creature, decimal amount)
    {
        MethodInfo method = typeof(PowerCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(candidate => candidate.Name == "Apply" && candidate.IsGenericMethodDefinition
                && candidate.GetParameters().Select(p => p.ParameterType).SequenceEqual(
                    [typeof(PlayerChoiceContext), typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool)]))
            .MakeGenericMethod(powerType);
        return (Task)(method.Invoke(null, [new ThrowingPlayerChoiceContext(), creature, amount, creature, null, false])
            ?? throw new InvalidOperationException("PowerCmd.Apply returned null."));
    }

    private static Task InvokeNativeDamage(Creature target, decimal amount, ValueProp props)
    {
        MethodInfo method = typeof(CreatureCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(candidate => candidate.Name == "Damage" && candidate.GetParameters().Select(p => p.ParameterType)
                .SequenceEqual([typeof(PlayerChoiceContext), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardModel), typeof(CardPlay)]));
        return (Task)(method.Invoke(null, [new ThrowingPlayerChoiceContext(), target, amount, props, null, null])
            ?? throw new InvalidOperationException("CreatureCmd.Damage returned null."));
    }

    private static CardLocation ReturnHand<TCard>(TCard card, CardLocation result) where TCard : CardModel
    {
        if (result.pileType == PileType.Discard)
            result.pileType = PileType.Hand;
        return result;
    }

    private static V040PredictionState GetState(CardOnPlayMirrorContext context, RelicModel orb)
        => context.Simulator.StateStore.Get<RelicModel, V040PredictionState>(orb,
            _ =>
            {
                Type runtimeType = _stateType!.Assembly.GetType("MyMod.MyModCode.Combat.ManaRuntime", true)!;
                object runtime = runtimeType.GetMethod("For", BindingFlags.Public | BindingFlags.Static)!
                    .Invoke(null, [context.PreviewCard.Owner])!;
                object liveState = runtimeType.GetProperty("State")!.GetValue(runtime)!;
                return CreatePredictionState(context.PreviewCard.Owner, CaptureState(_stateType, liveState));
            });

    private static V040PredictionState GetState(CombatPredictionSimulator simulator, RelicModel orb, V040Snapshot? opening)
        => simulator.StateStore.Get<RelicModel, V040PredictionState>(orb,
            _ => CreatePredictionState(orb.Owner, opening ?? CaptureState(_stateType!, GetLiveManaState(orb.Owner))));

    private static RelicModel GetBranchOrb(CombatPredictionSimulator simulator, Player player)
    {
        SimulatedCombatState combat = simulator.State.CombatState as SimulatedCombatState
            ?? throw new PredictionUnsupportedException("The v0.4 branch has no simulated combat state.");
        MethodInfo relicsOf = AccessTools.Method(typeof(SimulatedCombatState), "RelicsOf", [typeof(Player)])
            ?? throw new MissingMethodException(typeof(SimulatedCombatState).FullName, "RelicsOf(Player)");
        IEnumerable<RelicModel> relics = (IEnumerable<RelicModel>)(relicsOf.Invoke(combat, [player])
            ?? throw new PredictionUnsupportedException("SimulatedCombatState.RelicsOf returned null."));
        return relics.Single(relic => relic.GetType().Name is "CrackedCrystalOrb" or "CompleteCrystalOrb");
    }

    private static V040PredictionState CreatePredictionState(Player player, V040Snapshot opening)
        => new(_stateType!, opening, CloneReverseLedger(GetLiveReverseLedger(player)));

    private static object GetLiveReverseLedger(Player player)
    {
        Type runtimeType = _stateType!.Assembly.GetType("MyMod.MyModCode.Combat.ManaRuntime", true)!;
        object runtime = runtimeType.GetMethod("For", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [player])!;
        return runtimeType.GetField("_reverse", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(runtime)
            ?? throw new PredictionUnsupportedException("ManaRuntime reverse ledger is unavailable for branch initialization.");
    }

    private static object CloneReverseLedger(object source)
    {
        if (source.GetType().GetProperty("Issue")?.GetValue(source) is string issue && !string.IsNullOrWhiteSpace(issue))
            throw new PredictionUnsupportedException("Cannot fork an unresolved ReversePowerLedger: " + issue);
        object clone = Activator.CreateInstance(source.GetType())
            ?? throw new PredictionUnsupportedException("Could not create a fork-local ReversePowerLedger.");
        foreach (FieldInfo field in source.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            object? sourceValue = field.GetValue(source);
            object? cloneValue = field.GetValue(clone);
            if (sourceValue is IDictionary sourceDictionary && cloneValue is IDictionary cloneDictionary)
            {
                cloneDictionary.Clear();
                if (field.Name != "_identities")
                    foreach (DictionaryEntry entry in sourceDictionary) cloneDictionary.Add(entry.Key, entry.Value);
                continue;
            }
            if (sourceValue is IEnumerable sourceItems && cloneValue != null
                && cloneValue.GetType().GetMethod("Add") is MethodInfo add)
            {
                cloneValue.GetType().GetMethod("Clear")?.Invoke(cloneValue, null);
                if (field.Name != "_identities")
                    foreach (object? item in sourceItems) add.Invoke(cloneValue, [item]);
                continue;
            }
            if (field.Name.Contains("Issue", StringComparison.Ordinal)) field.SetValue(clone, sourceValue);
            if (field.Name.Contains("syncing", StringComparison.OrdinalIgnoreCase) && sourceValue is true)
                throw new PredictionUnsupportedException("Cannot fork a ReversePowerLedger during an active sync.");
        }
        return clone;
    }

    private static V040Snapshot CaptureState(Type stateType, object state)
        => new(ReadInt(state, "Mana"), stateType.GetProperty("Stance")!.GetValue(state)!.ToString()!,
            (bool)stateType.GetProperty("CompleteOrb")!.GetValue(state)!, ReadInt(state, "StartDrawBonus"),
            Convert.ToDecimal(stateType.GetProperty("Echo")!.GetValue(state)));

    private static int ReadInt(object state, string property)
        => Convert.ToInt32(state.GetType().GetProperty(property)!.GetValue(state));

    private static decimal ReadDamageDecimal(object damageResult, string member)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object? value = damageResult.GetType().GetProperty(member, flags)?.GetValue(damageResult)
            ?? damageResult.GetType().GetField(member, flags)?.GetValue(damageResult);
        return value == null
            ? throw new MissingMemberException(damageResult.GetType().FullName, member)
            : Convert.ToDecimal(value);
    }

    private static CombatRootV1 WithExtension(CombatRootV1 root, V040Snapshot snapshot, string selectedForm)
    {
        ModelExtensionStateV1 extension = new()
        {
            ModId = "MyMod", TypeId = "Eleanor.ManaRuntime.v040", SchemaVersion = 1,
            Payload = JsonSerializer.SerializeToElement(new { state = snapshot, selectedForm }, ProtocolJson.Options),
            HasForkAdapter = true,
        };
        return root with
        {
            Header = root.Header with { Mods = [new ModVersionV1("MyMod", Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_VERSION") ?? "unknown")] },
            Extensions = [extension],
        };
    }

    private static List<string> DescribeRootDifferences(CombatRootV1 expected, CombatRootV1 actual)
    {
        JsonElement left = JsonSerializer.SerializeToElement(expected, ProtocolJson.Options);
        JsonElement right = JsonSerializer.SerializeToElement(actual, ProtocolJson.Options);
        var a = left.EnumerateObject().ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        var b = right.EnumerateObject().ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        return a.Keys.Union(b.Keys, StringComparer.Ordinal).Where(key => !a.TryGetValue(key, out JsonElement x)
                || !b.TryGetValue(key, out JsonElement y) || StateDigest.Compute(x) != StateDigest.Compute(y))
            .OrderBy(key => key, StringComparer.Ordinal).ToList();
    }

    private static string Sha(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class CardChoiceSelector(ManaStanceChoice desired, Type formChoiceType, PropertyInfo formProperty) : ICardSelector
    {
        public string[] AvailableTypes { get; private set; } = [];
        public CardModel? Selected { get; private set; }

        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            CardModel[] cards = options.ToArray();
            AvailableTypes = cards.Select(card => card.GetType().Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            Selected = cards.SingleOrDefault(card => card.GetType().Name == (desired == ManaStanceChoice.Mentor ? "WhiteChoice" : "BlackChoice"));
            if (Selected == null) throw new InvalidOperationException("Native GuidingPower did not offer the requested choice.");
            object selectedForm = formProperty.GetValue(Selected)!;
            if (selectedForm.ToString() != desired.ToString() || !formChoiceType.IsInstanceOfType(Selected))
                throw new InvalidOperationException("Native choice card form identity did not match its exact type.");
            return Task.FromResult<IEnumerable<CardModel>>([Selected]);
        }

        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,
            IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new NotSupportedException("Reward selection is outside this combat fixture.");
    }

    private sealed class V040PredictionState : IPredictionStateForkable, IPredictionForkBoundary
    {
        private readonly Type _type;
        private object _state;
        private object _reverseLedger;

        public V040PredictionState(Type type, V040Snapshot snapshot, object reverseLedger)
        {
            _type = type;
            _state = Create(type, snapshot);
            _reverseLedger = reverseLedger;
        }

        private V040PredictionState(Type type, object state, object reverseLedger)
        { _type = type; _state = state; _reverseLedger = reverseLedger; }
        public object Raw => _state;
        public object Fork(PredictionForkContext context)
            => new V040PredictionState(_type, Clone(_type, Capture()), CloneReverseLedger(_reverseLedger));
        public void AssertForkable() { }
        public object BeginCard(int paid) => _type.GetMethod("BeginCard")!.Invoke(_state, [paid])!;
        public bool TryPayMana(int amount) => (bool)_type.GetMethod("TryPayMana")!.Invoke(_state, [amount])!;
        public int GainMana(int amount) => (int)_type.GetMethod("GainMana")!.Invoke(_state, [amount])!;
        public object PayUpkeep(int availableEnergy) => _type.GetMethod("PayUpkeep")!.Invoke(_state, [availableEnergy])!;
        public object GrantOrbStart() => _type.GetMethod("GrantOrbStart")!.Invoke(_state, null)!;
        public void SetCompleteOrb(bool complete) => _type.GetMethod("SetCompleteOrb")!.Invoke(_state, [complete]);
        public void FinishCard(object tx) => _type.GetMethod("FinishCard")!.Invoke(_state, [tx]);
        public bool SyncReverse(CombatPredictionSimulator simulator, Creature owner, bool complete, bool turnStartRestore)
        {
            object task = InvokeReverseLedgerSync(_reverseLedger, Capture().Stance == "Reverse", complete,
                CreateSimulatedReversePowerPort(simulator, owner), turnStartRestore);
            if (task is not Task<bool> completed || !completed.IsCompleted)
                throw new PredictionUnsupportedException("Branch ReversePowerLedger projection did not complete synchronously; refusing to block the search worker on the Godot loop.");
            return completed.GetAwaiter().GetResult();
        }
        public void RecordBlockedDamage(int layers, int actualBlocked)
        {
            if (layers <= 0 || actualBlocked <= 0) return;
            _type.GetMethod("GainEcho")!.Invoke(_state, [(decimal)layers * actualBlocked]);
        }
        public void SwitchStance(string stance)
            => _type.GetMethod("SwitchStance")!.Invoke(_state, [Enum.Parse(_type.GetProperty("Stance")!.PropertyType, stance)]);
        public V040Snapshot Capture() => CaptureState(_type, _state);

        private static object Create(Type type, V040Snapshot snapshot)
        {
            object state = Activator.CreateInstance(type)!;
            Set(state, "Mana", snapshot.Mana);
            Set(state, "Stance", Enum.Parse(type.GetProperty("Stance")!.PropertyType, snapshot.Stance));
            Set(state, "CompleteOrb", snapshot.CompleteOrb);
            Set(state, "StartDrawBonus", snapshot.StartDrawBonus);
            Set(state, "Echo", snapshot.Echo);
            return state;
        }

        private static object Clone(Type type, V040Snapshot snapshot) => Create(type, snapshot);
        private static void Set(object target, string propertyName, object? value)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new MissingMemberException(target.GetType().FullName, propertyName);
            (property.GetSetMethod(true) ?? throw new MissingMethodException(target.GetType().FullName, "set_" + propertyName)).Invoke(target, [value]);
        }
    }
}

public class ReversePowerPortProxy : DispatchProxy
{
    private Func<string, int, object>? _change;
    public void Configure(Func<string, int, object> change) => _change = change;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name != "Change" || args is not { Length: 2 })
            throw new NotSupportedException($"Unsupported IReversePowerPort member {targetMethod?.Name}.");
        string stat = args[0]?.ToString() ?? throw new InvalidOperationException("ReverseStat is null.");
        int delta = Convert.ToInt32(args[1]);
        return (_change ?? throw new InvalidOperationException("Reverse power port is not configured."))(stat, delta);
    }
}
