using System.Text.Json;
using SimulatorLab.Core;

if (args.Length == 2 && args[0] == "--validate-case")
{
    string json = File.ReadAllText(args[1]);
    var imported = CombatCaseImporter.Read(json);
    foreach (string error in imported.Errors) Console.Error.WriteLine($"ERROR {error}");
    if (imported.IsValid)
    {
        Console.WriteLine($"PASS {imported.CaseFile!.Schema} build={imported.CaseFile.Root.Header.GameVersion}/{imported.CaseFile.Root.Header.GameCommit} traceSteps={imported.CaseFile.Trace?.Steps.Count ?? 0}");
        return 0;
    }
    return 1;
}

if (args.Length == 2 && args[0] == "--validate-native-differential")
{
    string json = File.ReadAllText(args[1]);
    var imported = NativeGameDifferentialImporter.Read(json);
    foreach (string error in imported.Errors) Console.Error.WriteLine($"ERROR {error}");
    if (imported.IsValid)
    {
        var evidence = imported.Evidence!;
        Console.WriteLine($"PASS {evidence.Schema} build={evidence.GameVersion}/{evidence.GameCommit} action={evidence.Action.CardInstanceId} noDiff=True");
        return 0;
    }
    return 1;
}

int checks = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new InvalidOperationException($"FAIL {name}");
    checks++;
    Console.WriteLine($"PASS {name}");
}

bool RejectsRunAction(Action action)
{
    try { action(); return false; }
    catch (InvalidOperationException) { return true; }
}

CombatCaseHeaderV1 Header() => new()
{
    CharacterModelId = "IRONCLAD",
    EncounterModelId = "FUZZY_WURM_CRAWLER_WEAK",
};

CombatRootV1 Root() => new()
{
    Header = Header(),
    CombatId = "combat-001",
    Phase = CombatPhase.PlayerAction,
    TurnNumber = 1,
    CurrentActorId = "player-001",
    IsStableDecisionBoundary = true,
    Combatants =
    [
        new CombatantStateV1
        {
            CombatantId = "player-001", ModelId = "IRONCLAD", IsPlayer = true, CurrentHp = 80, MaxHp = 80,
            Resources = new CombatantResourcesV1 { Energy = 3, Stars = 0, Orbs = [], Counters = new(StringComparer.Ordinal) { ["burn"] = 0 } },
        },
        new CombatantStateV1
        {
            CombatantId = "monster-001", ModelId = "FUZZY_WURM", CurrentHp = 35, MaxHp = 35,
            IntentId = "BITE", IntentDamage = 6, IntentHitCount = 1,
        },
    ],
    Cards =
    [
        new CardInstanceStateV1 { InstanceId = "hand-strike", ModelId = "STRIKE_IRONCLAD", EnergyCost = 1, TargetType = "AnyEnemy", DynamicVars = new(StringComparer.Ordinal) { ["Damage"] = 6 } },
        new CardInstanceStateV1 { InstanceId = "draw-secret", ModelId = "DEFEND_IRONCLAD", EnergyCost = 1, TargetType = "Self", DynamicVars = new(StringComparer.Ordinal) { ["Block"] = 5 } },
        new CardInstanceStateV1 { InstanceId = "discard-visible", ModelId = "BASH", EnergyCost = 2, TargetType = "AnyEnemy", DynamicVars = new(StringComparer.Ordinal) { ["Damage"] = 8 } },
    ],
    Piles =
    [
        new CardPileStateV1 { Kind = PileKind.Draw, CardInstanceIds = ["draw-secret"] },
        new CardPileStateV1 { Kind = PileKind.Hand, CardInstanceIds = ["hand-strike"] },
        new CardPileStateV1 { Kind = PileKind.Discard, CardInstanceIds = ["discard-visible"] },
        new CardPileStateV1 { Kind = PileKind.Exhaust },
        new CardPileStateV1 { Kind = PileKind.Play },
    ],
    RandomStreams = Enum.GetValues<CombatRngStream>().Select(x => new RandomStreamCheckpointV1(x, "fixture-xoshiro256**", "0123456789abcdef", 0)).ToList(),
};

var root = Root();
Check("version pin", root.Header.GameVersion == "v0.111.0" && root.Header.GameCommit == "41cef1ea" && root.Header.MainAssemblyHash == 222455745);
Check("complete exact capture accepted", CombatCaptureValidator.ValidateExactReady(root).IsExactReady);

var fork = root.Fork();
fork.Piles.Single(x => x.Kind == PileKind.Hand).CardInstanceIds.Add("fork-only");
fork.Cards.Single(x => x.InstanceId == "hand-strike").DynamicVars["Damage"] = 99;
fork.Combatants.Single(x => x.IsPlayer).Resources!.Counters["burn"] = 4;
Check("fork pile isolation", root.Piles.Single(x => x.Kind == PileKind.Hand).CardInstanceIds.SequenceEqual(["hand-strike"]));
Check("fork dynamic-var isolation", root.Cards.Single(x => x.InstanceId == "hand-strike").DynamicVars["Damage"] == 6);
Check("fork nested custom-state isolation", root.Combatants.Single(x => x.IsPlayer).Resources!.Counters["burn"] == 0);
var snapshot = CombatSnapshots.Read(root);
snapshot.Piles.Single(x => x.Kind == PileKind.Hand).CardInstanceIds.Clear();
Check("read snapshot is detached", root.Piles.Single(x => x.Kind == PileKind.Hand).CardInstanceIds.SequenceEqual(["hand-strike"]));

var observation = CombatObservationProjector.Project(root);
var observedJson = JsonSerializer.Serialize(observation, ProtocolJson.Options);
Check("player view hides draw identity", !observedJson.Contains("draw-secret", StringComparison.Ordinal));
Check("player view hides RNG state", !observedJson.Contains("fixture-xoshiro", StringComparison.Ordinal) && !observedJson.Contains("0123456789abcdef", StringComparison.Ordinal));
Check("player view keeps draw count", observation.DrawPileCount == 1);

var jsonA = JsonDocument.Parse("{\"a\":1,\"nested\":{\"x\":2,\"y\":3}}").RootElement.Clone();
var jsonB = JsonDocument.Parse("{\"nested\":{\"y\":3,\"x\":2},\"a\":1}").RootElement.Clone();
var payloadA = root with { Extensions = [new ModelExtensionStateV1 { ModId = "fixture", TypeId = "state", SchemaVersion = 1, Payload = jsonA, HasForkAdapter = true }] };
var payloadB = root with { Extensions = [new ModelExtensionStateV1 { ModId = "fixture", TypeId = "state", SchemaVersion = 1, Payload = jsonB, HasForkAdapter = true }] };
Check("canonical digest ignores JSON object key order", StateDigest.Compute(payloadA) == StateDigest.Compute(payloadB));
var reorderedPile = root.Fork();
reorderedPile.Piles.Single(x => x.Kind == PileKind.Draw).CardInstanceIds.Add("another-draw-card");
Check("digest includes ordered combat state", StateDigest.Compute(root) != StateDigest.Compute(reorderedPile));

var missingStream = root with { RandomStreams = root.RandomStreams.Where(x => x.Stream != CombatRngStream.Niche).ToList() };
Check("missing RNG stream rejects exact replay", !CombatCaptureValidator.ValidateExactReady(missingStream).IsExactReady);
var unknownEffect = root with { Gaps = [new PredictionGapV1 { EffectId = "MOD_EFFECT_1", SourceModelId = "mod-card", Reason = "no adapter", Impact = GapImpact.CombatOutcome }] };
Check("unmodeled gameplay gap rejects exact replay", !CombatCaptureValidator.ValidateExactReady(unknownEffect).IsExactReady);

var digest = StateDigest.Compute(root);
var trace = new CombatTraceV1
{
    Header = root.Header with { Mods = root.Header.Mods.ToList() },
    RootStateDigest = digest,
    Steps =
    [
        new CombatTraceStepV1(digest, new CombatActionV1 { Kind = CombatActionKind.PlayCard, CardInstanceId = "hand-strike", TargetCombatantId = "monster-001" }, new string('a', 64), []),
    ],
};
Check("consistent action trace validates", CombatTraceValidator.Validate(root, trace).Count == 0);
var brokenTrace = trace with { Steps = [trace.Steps[0] with { BeforeStateDigest = new string('b', 64) }] };
Check("trace discontinuity is reported", CombatTraceValidator.Validate(root, brokenTrace).Any(x => x.Contains("does not continue", StringComparison.Ordinal)));

var hostCall = HostProbeActionBridge.TryCreateManualPlayCall(root,
    new CombatActionV1 { Kind = CombatActionKind.PlayCard, CardInstanceId = "hand-strike", TargetCombatantId = "monster-001" },
    "live-token-001");
Check("HostProbe bridge maps card and target to captured order", hostCall.IsSupported && hostCall.Call is { HandIndex: 0, EnemyIndex: 0, ExpectedHostToken: "live-token-001" });
var staleOrUnsupported = HostProbeActionBridge.TryCreateManualPlayCall(root,
    new CombatActionV1 { Kind = CombatActionKind.PlayCard, CardInstanceId = "draw-secret" },
    "live-token-001");
Check("HostProbe bridge rejects a card outside the hand", !staleOrUnsupported.IsSupported && staleOrUnsupported.Call is null);
var unknownTargetRoot = root with
{
    Cards = root.Cards.Select(x => x.InstanceId == "hand-strike" ? x with { TargetType = "RandomEnemy" } : x).ToList(),
};
var unknownTarget = HostProbeActionBridge.TryCreateManualPlayCall(unknownTargetRoot,
    new CombatActionV1 { Kind = CombatActionKind.PlayCard, CardInstanceId = "hand-strike", TargetCombatantId = "monster-001" },
    "live-token-001");
Check("HostProbe bridge refuses unknown targeting semantics", !unknownTarget.IsSupported && unknownTarget.Call is null);

var sameObservation = CombatObservationComparer.Compare(observation, observation with { Hand = observation.Hand.ToList() });
Check("observation replay comparison accepts identical visible state", sameObservation.IsMatch && sameObservation.MismatchedFields.Count == 0);
var changedObservation = CombatObservationComparer.Compare(observation, observation with { DrawPileCount = observation.DrawPileCount + 1 });
Check("observation replay comparison reports field-level drift", !changedObservation.IsMatch && changedObservation.MismatchedFields.SequenceEqual(["drawPileCount"]));

var caseFile = new CombatCaseFileV1 { Root = root, Trace = trace };
var caseJson = CombatCaseImporter.Write(caseFile);
var importedCase = CombatCaseImporter.Read(caseJson);
Check("combat case round-trips strict root and trace schema", importedCase.IsValid && importedCase.CaseFile?.Trace?.Steps.Count == 1);
var unknownFieldCase = caseJson.Replace("\"combatId\":\"combat-001\"", "\"combatId\":\"combat-001\",\"unexpectedField\":true", StringComparison.Ordinal);
Check("combat case importer rejects unknown fields", !CombatCaseImporter.Read(unknownFieldCase).IsValid);
var wrongCaseVersion = caseJson.Replace("combat-case-file/v1", "combat-case-file/v9", StringComparison.Ordinal);
Check("combat case importer rejects unknown schema versions", !CombatCaseImporter.Read(wrongCaseVersion).IsValid);

var rootStamp = NativeGameDifferentialImporter.Stamp("native root fixture");
var afterStamp = NativeGameDifferentialImporter.Stamp("after strike fixture");
var nativeFixture = new NativeGameActionDifferentialFileV1
{
    CaptureMethod = "fixture-only",
    ScenarioId = "fixture/ironclad-strike",
    CharacterModelId = "IRONCLAD",
    EncounterModelId = "FUZZY_WURM_CRAWLER_WEAK",
    TargetCombatantId = "enemy-001",
    TestMode = true,
    SteamInitialized = false,
    SaveStore = "Fixture.MemorySaveStore",
    MyModLoaded = false,
    UsesPresentationClockShim = true,
    Action = new CombatActionV1
    {
        Kind = CombatActionKind.PlayCard,
        CardInstanceId = "hand:2:STRIKE_IRONCLAD",
        CardHandIndex = 2,
        TargetCombatantId = "enemy-001",
    },
    NativeRoot = rootStamp,
    SimulatorRoot = rootStamp,
    SimulatorAfter = afterStamp,
    NativeAfter = afterStamp,
};
var nativeFixtureJson = NativeGameDifferentialImporter.Write(nativeFixture);
var importedNativeFixture = NativeGameDifferentialImporter.Read(nativeFixtureJson);
Check("native differential schema round-trips full state stamps", importedNativeFixture.IsValid);
var divergentNativeFixture = nativeFixture with { NativeAfter = NativeGameDifferentialImporter.Stamp("native differs") };
Check("native differential importer rejects post-action state drift", !NativeGameDifferentialImporter.Read(NativeGameDifferentialImporter.Write(divergentNativeFixture)).IsValid);
var invalidNativeHandIndex = nativeFixture with { Action = nativeFixture.Action with { CardHandIndex = -1 } };
Check("native differential importer rejects an invalid hand index", !NativeGameDifferentialImporter.Read(NativeGameDifferentialImporter.Write(invalidNativeHandIndex)).IsValid);
var unknownNativeField = nativeFixtureJson.Replace("\"targetCombatantId\": \"enemy-001\"", "\"targetCombatantId\": \"enemy-001\", \"unexpectedField\": true", StringComparison.Ordinal);
Check("native differential importer rejects unknown fields", !NativeGameDifferentialImporter.Read(unknownNativeField).IsValid);

var progressRoot = RunProgressStateV1.AfterCombat(
    RunCombatOutcomeV1.Victory,
    "0,0",
    [
        new RunRewardOfferV1 { RewardId = "gold", Kind = "Gold" },
        new RunRewardOfferV1 { RewardId = "card", Kind = "Card", ChoiceIds = ["STRIKE", "DEFEND"] },
    ],
    canSkipPendingRewards: true,
    [
        new RunRouteNodeV1 { NodeId = "0,0", Column = 0, Row = 0, MapPointType = "Monster", ChildNodeIds = ["0,1", "1,1"] },
        new RunRouteNodeV1 { NodeId = "0,1", Column = 0, Row = 1, MapPointType = "Unknown", ResolvedRoomType = "Event" },
        new RunRouteNodeV1 { NodeId = "1,1", Column = 1, Row = 1, MapPointType = "Monster", ResolvedRoomType = "Monster" },
    ]);
Check("defeat cannot enter victory rewards", RejectsRunAction(() => RunProgressRulesV1.Apply(
    progressRoot with { Outcome = RunCombatOutcomeV1.Defeat }, new RunProgressActionV1 { Kind = RunProgressActionKindV1.BeginRewards })));
var progress = RunProgressRulesV1.Apply(progressRoot, new RunProgressActionV1 { Kind = RunProgressActionKindV1.BeginRewards });
Check("victory opens the reward selection phase", progress.Phase == RunProgressPhaseV1.Rewards);
Check("pending rewards block route transition", RejectsRunAction(() => RunProgressRulesV1.Apply(progress,
    new RunProgressActionV1 { Kind = RunProgressActionKindV1.ProceedToRoute })));
Check("card reward requires an offered choice", RejectsRunAction(() => RunProgressRulesV1.Apply(progress,
    new RunProgressActionV1 { Kind = RunProgressActionKindV1.SelectReward, RewardId = "card", ChoiceId = "BASH" })));
progress = RunProgressRulesV1.Apply(progress, new RunProgressActionV1 { Kind = RunProgressActionKindV1.SelectReward, RewardId = "gold" });
progress = RunProgressRulesV1.Apply(progress, new RunProgressActionV1 { Kind = RunProgressActionKindV1.SkipPendingRewards });
Check("reward set records selected and skipped rewards", progress.Rewards[0].Disposition == RunRewardDispositionV1.Selected
    && progress.Rewards[1].Disposition == RunRewardDispositionV1.Skipped);
progress = RunProgressRulesV1.Apply(progress, new RunProgressActionV1 { Kind = RunProgressActionKindV1.ProceedToRoute });
Check("route exposes only current node children", progress.Phase == RunProgressPhaseV1.Route
    && progress.LegalNextNodeIds.SequenceEqual(["0,1", "1,1"]));
Check("route rejects a non-child node", RejectsRunAction(() => RunProgressRulesV1.Apply(progress,
    new RunProgressActionV1 { Kind = RunProgressActionKindV1.EnterNode, NodeId = "9,9" })));
var unresolvedUnknown = progress with
{
    RouteNodes = progress.RouteNodes.Select(node => node.NodeId == "0,1" ? node with { ResolvedRoomType = null } : node).ToList(),
};
Check("unknown map point needs a game-resolved room type", RejectsRunAction(() => RunProgressRulesV1.Apply(unresolvedUnknown,
    new RunProgressActionV1 { Kind = RunProgressActionKindV1.EnterNode, NodeId = "0,1" })));
progress = RunProgressRulesV1.Apply(progress, new RunProgressActionV1 { Kind = RunProgressActionKindV1.EnterNode, NodeId = "0,1" });
Check("legal child entry completes minimum run loop", progress.Phase == RunProgressPhaseV1.NodeEntered
    && progress.CurrentNodeId == "0,1" && progress.EnteredNodeId == "0,1");
Check("reward choice lists remain detached across transitions", progressRoot.Rewards[1].ChoiceIds.SequenceEqual(["STRIKE", "DEFEND"]));

Console.WriteLine($"PASS all {checks} contract checks");
return 0;
