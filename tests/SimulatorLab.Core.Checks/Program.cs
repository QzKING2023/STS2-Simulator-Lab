using System.Text.Json;
using SimulatorLab.Core;
using SimulatorLab.Core.Mods;

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

var deathResult = new ValidationOutcomeV1 { CaseId="death", ReasonCode="native-pending-loss", NativeOutcome=NativeOutcome.Death,
    SimulatorOutcome=NativeOutcome.Death, Comparison=ComparisonOutcome.Exact, Coverage=CoverageOutcome.Complete,
    StopReason=ValidationStop.Completed, VerificationPassed=true };
Check("matched actual death is valid independently of victory", deathResult.Validate().Count==0);
Check("predicted death is not actual death", (deathResult with {NativeOutcome=NativeOutcome.Unknown}).Validate().Count>0);
Check("mismatch cannot become a successful verification", (deathResult with {Comparison=ComparisonOutcome.Mismatch}).Validate().Count>0);
Check("exact prefix cannot complete unsupported coverage", (deathResult with {Coverage=CoverageOutcome.Unsupported}).Validate().Count>0);
Check("unsupported requires a coverage gap", (deathResult with {StopReason=ValidationStop.Unsupported}).Validate().Count>0);
foreach(var reason in Enum.GetValues<ValidationStop>())
{
    var value=new ValidationOutcomeV1 {CaseId="reason-contract",ReasonCode=reason.ToString(),StopReason=reason,
        Coverage=reason==ValidationStop.Unsupported?CoverageOutcome.Unsupported:CoverageOutcome.NotRun,
        Comparison=reason==ValidationStop.DifferentialMismatch?ComparisonOutcome.Mismatch:ComparisonOutcome.NotCompared};
    var roundtrip=JsonSerializer.Deserialize<ValidationOutcomeV1>(JsonSerializer.Serialize(value))!;
    Check($"outcome classification preserves {reason}",roundtrip.StopReason==reason);
}
Check("decision budget stops before another query",new DecisionBounds(1).Reached(1,1,0)=="decision-limit");
Check("turn budget stops after declared turn",new DecisionBounds(MaxTurns:1).Reached(0,2,0)=="turn-limit");
Check("elapsed boundary is inclusive",new DecisionBounds(ElapsedSeconds:1).Reached(0,1,1)=="elapsed-limit");
Check("elapsed boundary permits earlier query",new DecisionBounds(ElapsedSeconds:1).Reached(0,1,0.99)==null);
Check("independent shield base has five block without mana",EleanorV041Contracts.ManaShield(false,0)==(1,0,5));
Check("independent shield upgraded has thirteen block with mana",EleanorV041Contracts.ManaShield(true,2)==(1,1,13));
Check("independent contract rejects shared wrong observed block",EleanorV041Contracts.ManaShield(true,1).Block!=12);
Check("budget with no candidate retains budget reason",QueryDisposition.Stop(false,true,0)==ValidationStop.BudgetExceeded);
Check("empty result without budget is not unsupported",QueryDisposition.Stop(false,false,0)==ValidationStop.NoCandidate);
Check("known gap remains unsupported",QueryDisposition.Stop(true,false,0)==ValidationStop.Unsupported);
Check("candidate can proceed without a stop",QueryDisposition.Stop(false,false,1)==null);
foreach(var reason in Enum.GetValues<ValidationStop>().Where(r=>r!=ValidationStop.Completed))
    Check($"typed stop survives outer status mapping: {reason}",ValidationOutcomeV1.FromStatus("mapping",ValidationOutcomeV1.StatusForStop(reason)).StopReason==reason);
var confirmedStep=JsonSerializer.SerializeToElement(new {nativeAccepted=true,simulatedAccepted=true,exactStateMatch=true,parentBranchUnchanged=true,nativeContinuation="same",simulatorContinuation="same"});
JsonElement BoundaryFixture(string status="boundary",int count=1) => JsonSerializer.SerializeToElement(new {status,stopReason="controlled fixture",steps=Enumerable.Repeat(confirmedStep,count),
    outcome=new ValidationOutcomeV1 {CaseId="fixture",ReasonCode=status,NativeOutcome=status=="passed"?NativeOutcome.Victory:NativeOutcome.Ongoing,SimulatorOutcome=status=="passed"?NativeOutcome.Victory:NativeOutcome.Ongoing,
        Comparison=status=="differential-mismatch"?ComparisonOutcome.Mismatch:ComparisonOutcome.Exact,Coverage=status=="unsupported"?CoverageOutcome.Unsupported:CoverageOutcome.Complete,StopReason=ValidationOutcomeV1.FromStatus("fixture",status).StopReason,
        VerificationPassed=status is "boundary" or "passed",ConfirmedActions=count}});
ReplayVerification.RequireBoundary(BoundaryFixture(),1);
Check("real boundary gate accepts one confirmed step",true);
foreach(var bad in new[]{BoundaryFixture("boundary",0),BoundaryFixture("engine-error"),BoundaryFixture("unsupported"),BoundaryFixture("invalid-action"),BoundaryFixture("differential-mismatch")})
{
    bool rejected=false;try{ReplayVerification.RequireBoundary(bad,1);}catch(ValidationStopException ex){rejected=ex.Reason==(bad.GetProperty("status").GetString()=="boundary"?ValidationStop.DifferentialMismatch:ReplayVerification.Outcome(bad).StopReason);}
    Check("boundary gate rejects empty or failed inner replay: "+bad.GetProperty("status").GetString(),rejected);
}
var nine=Enumerable.Repeat(BoundaryFixture(),9).ToArray();
Check("aggregate keeps nine confirmed actions",ReplayVerification.Aggregate("second-battle",nine,"budget-exceeded","decision-limit").ConfirmedActions==9);
var interruptedPrefix=ReplayVerification.Aggregate("second-battle",nine.Take(2).ToArray(),"engine-error","injected query fault");
Check("query failure retains two confirmed actions and uncertain current states",interruptedPrefix.ConfirmedActions==2&&interruptedPrefix.NativeOutcome==NativeOutcome.Unknown&&interruptedPrefix.SimulatorOutcome==NativeOutcome.Unknown&&interruptedPrefix.ExecutionState=="OutcomeUncertain");
var lastVictory=BoundaryFixture("passed");
Check("last chunk does not replace cumulative action count",ReplayVerification.Aggregate("second-battle",nine.Take(8).Append(lastVictory).ToArray(),"passed",null).ConfirmedActions==9);
var nativeSlice=new NativeRunResultV1{Terminal=NativeRunTerminal.Ongoing,Stop=NativeRunStop.SliceBoundary,Dispatched=5,Confirmed=5,DataComplete=true,LastBoundary="next-combat"};
Check("native-only slice has no invented simulator outcome",nativeSlice.Validate().Count==0&&nativeSlice.ExitCode==0&&nativeSlice.Comparison==ComparisonOutcome.NotCompared);
Check("battle victory is not a complete run",(nativeSlice with{Stop=NativeRunStop.Completed}).ExitCode==1);
Check("empty slice cannot pass",(nativeSlice with{Dispatched=0,Confirmed=0}).ExitCode==1);
Check("native data gap cannot pass",(nativeSlice with{DataComplete=false}).ExitCode==1);
Check("native pending dispatch cannot pass",(nativeSlice with{Dispatched=6}).ExitCode==1);
Check("native mismatch cannot pass",(nativeSlice with{Comparison=ComparisonOutcome.Mismatch}).ExitCode==1);
Check("native death is valid without fake simulation",(nativeSlice with{Stop=NativeRunStop.Completed,Terminal=NativeRunTerminal.Death}).ExitCode==0);
Check("native budget is noncompletion",(nativeSlice with{Stop=NativeRunStop.BudgetExceeded}).ExitCode==3);
Check("native unsupported retains own exit",(nativeSlice with{Stop=NativeRunStop.Unsupported}).ExitCode==2);
Check("native unknown result fails closed",(nativeSlice with{Stop=(NativeRunStop)999}).ExitCode==1);
object source=new();
var nativeTicket=new NativeDecisionTicket(source,1,"state-a","PlayCard");
nativeTicket.Consume(source,1,"state-a","PlayCard");
Check("native ticket one-use",RejectsRunAction(()=>nativeTicket.Consume(source,1,"state-a","PlayCard")));
Check("native action equality ignores only JSON formatting",NativeDecisionTicket.SameActionJson("{\"kind\":\"EndTurn\"}","{ \"kind\": \"EndTurn\" }"));
Check("native action equality rejects additional fields",!NativeDecisionTicket.SameActionJson("{\"kind\":\"EndTurn\"}","{\"kind\":\"EndTurn\",\"NestedChoices\":[0]}"));
foreach(var invalid in new Action[]{()=>new NativeDecisionTicket(source,1,"state-a","PlayCard").Consume(new object(),1,"state-a","PlayCard"),()=>new NativeDecisionTicket(source,1,"state-a","PlayCard").Consume(source,2,"state-a","PlayCard"),()=>new NativeDecisionTicket(source,1,"state-a","PlayCard").Consume(source,1,"state-b","PlayCard"),()=>new NativeDecisionTicket(source,1,"state-a","PlayCard").Consume(source,1,"state-a","EndTurn")})
    Check("native ticket rejects source/sequence/state/action mismatch",RejectsRunAction(invalid));
var snapshotRoot = Path.GetFullPath("local-mods/workshop-snapshot-20261106-163352");
var snapshotManifest = snapshotRoot + ".manifest.json";
var modCatalogA = ModCatalogParser.Parse(snapshotRoot, snapshotManifest);
var modCatalogB = ModCatalogParser.Parse(snapshotRoot, snapshotManifest);
Check("mod catalog discovers 21 sources and 24 manifests", modCatalogA.Sources.Count == 21 && modCatalogA.Manifests.Count == 24 && modCatalogA.Errors.Count == 0);
Check("mod catalog fixes target game version", modCatalogA.TargetGameVersion == "0.111.0");
Check("mod catalog recognizes BaseLib RitsuLib MinionLib Watcher", new[] { "BaseLib", "STS2-RitsuLib", "MinionLib", "Watcher" }.All(id => modCatalogA.Manifests.Any(m => m.ModId == id)));
Check("mod catalog selects all RitsuLib compatibility directories", modCatalogA.Manifests.Single(m => m.ModId == "STS2-RitsuLib").CompatibilityVariants.Select(v => v.TargetGameVersion).SequenceEqual(new[] { "0.107.1", "0.109.0", "0.110.0", "0.111.0" }));
Check("mod catalog recognizes YukiMakoto exact target variant", modCatalogA.Manifests.Single(m => m.ModId == "YukiMakoto").CompatibilityVariants.Any(v => v.TargetGameVersion == "0.111.0"));
Check("mod catalog repeat parse is deterministic", modCatalogA.ModelSummary == modCatalogB.ModelSummary && modCatalogA.InputHash == modCatalogB.InputHash && modCatalogA.Errors.Select(e => e.ToString()).SequenceEqual(modCatalogB.Errors.Select(e => e.ToString())));
var missingCatalog = ModCatalogParser.Parse(Path.Combine(".build", "missing-mod-snapshot"), Path.Combine(".build", "missing-mod-snapshot.manifest.json"));
Check("mod catalog reports missing files with stable code", missingCatalog.Errors.All(e => e.Code == ModCatalogErrorCode.MissingFile));
Console.WriteLine($"PASS all {checks} contract checks");
return 0;



