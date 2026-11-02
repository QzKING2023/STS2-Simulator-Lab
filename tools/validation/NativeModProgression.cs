using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CombatSolver;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;
using CombatSolver.Engine.InCombat.Simulation;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using SimulatorLab.Core;

namespace OfflineSearchHarness;

internal static partial class NativeModRunLoop
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, IncludeFields = true };
    private static readonly List<object> Lifecycle = [];
    private static object? RewardEvidence;
    private static object? BattleEvidence;
    private static string Phase = "not-started";
    private static object? NextBoundary;
    public static bool RecordFixtureEnabled => Enabled && Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_RECORD_FIXTURE")=="1";
    public static object? RecordingFixture { get; set; }
    private static object? RewardCardSupport;
    private static RunProgressStateV1? Protocol;

    public static object RunLoop(CombatState combat, MainLoopContext loop, string output, object route, HarnessOptions options)
    {
        var player = LocalContext.GetMe(combat)!;
        var run = (RunState)combat.RunState;
        string policy = Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_REWARD_POLICY") ?? "skip";
        object Report(string status, string? reason = null)
        {
            var report = new { schema = "eleanor-native-run-loop/v1", status, reason, phase = Phase, policy,
                modHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_DLL")!))),
                pool = PoolPreflight(), battle = BattleEvidence, rewards = RewardEvidence, lifecycle = Lifecycle,
                nextBoundary = NextBoundary, secondBattle=SecondBattle, protocol=Protocol, recordingFixture=RecordingFixture, rewardCardSupport=RewardCardSupport, semantics = "native reward execution + protocol legality + simulator combat handoffs; no independent reward predictor",
                offlineGuards=EleanorV040Probe.CaptureOfflineWriteEvidence(), persistentWritesAllowed = false };
            File.WriteAllText(Path.Combine(output,"native-run-loop.json"), JsonSerializer.Serialize(report,Json));
            return report;
        }
        try
        {
            Phase = "first-battle";
            Observe("first-player-boundary", player, run);
            string battleDir = Path.Combine(output,"battle"); Directory.CreateDirectory(battleDir);
            string planPath = Path.Combine(battleDir,"route.json");
            var routeJson = new JsonSerializerOptions(Json) { PropertyNamingPolicy=JsonNamingPolicy.CamelCase };
            routeJson.Converters.Add(new JsonStringEnumConverter());
            string? fixedPlan=Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_FIXED_PLAN");
            if(!string.IsNullOrEmpty(fixedPlan))
            {
                string expected=Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_FIXED_PLAN_SHA")??"";
                string actual=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixedPlan)));
                if(!StringComparer.OrdinalIgnoreCase.Equals(actual,expected))throw new InvalidOperationException("Fixed native plan hash mismatch.");
                File.Copy(fixedPlan,planPath,overwrite:false);
            }
            else File.WriteAllText(planPath, JsonSerializer.Serialize(route,routeJson));
            File.WriteAllText(Path.Combine(battleDir,"harness-result.json"),JsonSerializer.Serialize(new {
                eleanorMod = new { assemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_DLL")!))) },
                scenario = new { CharacterId = "MyMod", EncounterId = ((CombatRoom)run.CurrentRoom!).Encounter.Id.Entry, Seed = "LAB_V0111_ELEANOR_V040_FORM_01" }
            },Json));
            BattleEvidence = EleanorV040Probe.RunVictoryReplay(combat,loop,battleDir,planPath);
            string battleStatus = JsonSerializer.SerializeToElement(BattleEvidence,Json).GetProperty("status").GetString()!;
            if (battleStatus != "passed") return Report(battleStatus, "Native map battle replay did not complete exactly.");
            var room = (CombatRoom)run.CurrentRoom!;
            Phase = "post-combat-cleanup";
            Observe(Phase,player,run);
            VerifyReset(player);
            Phase = "native-rewards";
            // MegaInput's cctor initializes only unused presentation hotkeys. No alternatives/delegates are replaced.
            EnvironmentPatches.Patch(typeof(MegaInput).TypeInitializer!,prefix:new HarmonyMethod(typeof(NativeModRunLoop),nameof(SkipInputPresentationNames)));
            var oldSelector = RewardsSet.testSelector;
            int offerCount=0;
            var choice = new RewardSelector();
            var deckBefore=player.Deck.Cards.ToArray(); int goldBefore=player.Gold;
            List<RunRewardOfferV1> offers=[];
            List<string> selectedRewards=[];
            try
            {
                using var scope = CardSelectCmd.UseSelector(choice);
                RewardsSet.testSelector = async set =>
                {
                    if (!ReferenceEquals(set.Room,room) || ++offerCount!=1) throw new InvalidOperationException("Unexpected native reward offer.");
                    Observe("rewards-generated-before-choice",player,run);
                    object[] offered = set.Rewards.Select((reward,index)=>(object)new { index, kind=reward.GetType().Name,
                        populated=reward.IsPopulated, data=reward is CardReward cards ? (object)new { cards=cards.Cards.Select(c=>c.ToSerializable()).ToArray(), cards.CanSkip, cards.CanReroll } : reward.ToSerializable() }).ToArray();
                    offers=set.Rewards.Select((reward,index)=>new RunRewardOfferV1 { RewardId=$"reward:{set.Id}:{index}",Kind=reward.GetType().Name, ChoiceIds=reward is CardReward cards ? cards.Cards.Select(c=>c.Id.ToString()).ToList() : [] }).ToList();
                    var gold = set.Rewards.OfType<GoldReward>().Single();
                    await RunManager.Instance.RewardsSetSynchronizer.SelectLocalReward(gold);
                    if (policy=="take")
                        await RunManager.Instance.RewardsSetSynchronizer.SelectLocalReward(set.Rewards.OfType<CardReward>().Single());
                    else if (policy!="skip") throw new InvalidOperationException("Unknown reward policy.");
                    selectedRewards=set.Rewards.Select((r,i)=>(r,i)).Where(x=>x.r.SuccessfullySelected).Select(x=>offers[x.i].RewardId).ToList();
                    RunManager.Instance.RewardsSetSynchronizer.SkipLocalRewardsSet();
                    RewardEvidence = new { offerCount, offered, selectedCard=choice.Selected?.Id.ToString(),
                        offeredCards=choice.Options, goldBefore, goldAfter=player.Gold,
                        history=JsonSerializer.SerializeToElement(run.CurrentMapPointHistoryEntry,Json) };
                };
                loop.RunUntilCompleted(RewardsCmd.OfferForRoomEnd(player,room),TimeSpan.FromSeconds(30),"Native room-end reward offer");
            }
            finally { RewardsSet.testSelector=oldSelector; }
            if (offerCount!=1 || player.Gold<=goldBefore) throw new InvalidOperationException("Gold acquisition/offer count failed.");
            CardModel? acquired = player.Deck.Cards.Except(deckBefore).SingleOrDefault();
            if ((policy=="take" && (acquired==null || acquired.Id!=choice.Selected!.Id)) || (policy=="skip" && acquired!=null))
                throw new InvalidOperationException("Actual native card acquisition differs from policy.");
            if(acquired!=null) RewardCardSupport=new { card=acquired.Id.ToString(),ordinaryRegisteredOnPlay=CardOnPlayMirrors.CanMirror(acquired),
                adaptedOnPlay=AdaptedCardOnPlayMirrors.Select(acquired.GetType(),AccessTools.Method(acquired.GetType(),"OnPlay"),Harmony.GetPatchInfo(AccessTools.Method(acquired.GetType(),"OnPlay")))!=null,
                effectExecution=SecondBattleEnabled?"not-run at acquisition; see secondBattle trace":"not-run; first actionable state only", nativeDeckAddition=true };
            Observe("rewards-completed",player,run);
            Phase="map-travel";
            loop.RunUntilCompleted(RunManager.Instance.ProceedFromTerminalRewardsScreen(),TimeSpan.FromSeconds(15),"Proceed to native map");
            var current=run.CurrentMapPoint!;
            var next=current.Children.Where(p=>p.PointType==MegaCrit.Sts2.Core.Map.MapPointType.Monster).OrderBy(p=>p.coord.col).ThenBy(p=>p.coord.row).FirstOrDefault()
                ?? throw new PredictionUnsupportedException("No legal Monster child in this native map fixture.");
            string Id(MegaCrit.Sts2.Core.Map.MapPoint p)=>$"{p.coord.col},{p.coord.row}";
            var graph=new[]{current}.Concat(current.Children).Distinct().Select(p=>new RunRouteNodeV1 { NodeId=Id(p),Column=p.coord.col,Row=p.coord.row,MapPointType=p.PointType.ToString(),ChildNodeIds=p.Children.Select(Id).ToList() }).ToArray();
            Protocol=RunProgressStateV1.AfterCombat(RunCombatOutcomeV1.Victory,Id(current),offers,true,graph);
            Protocol=RunProgressRulesV1.Apply(Protocol,new(){Kind=RunProgressActionKindV1.BeginRewards});
            foreach(string id in selectedRewards) {
                var offer=offers.Single(o=>o.RewardId==id);
                Protocol=RunProgressRulesV1.Apply(Protocol,new(){Kind=RunProgressActionKindV1.SelectReward,RewardId=id,ChoiceId=offer.ChoiceIds.Count>0?choice.Selected!.Id.ToString():null});
            }
            Protocol=RunProgressRulesV1.Apply(Protocol,new(){Kind=RunProgressActionKindV1.SkipPendingRewards});
            Protocol=RunProgressRulesV1.Apply(Protocol,new(){Kind=RunProgressActionKindV1.ProceedToRoute});
            bool illegalRejected=false;
            try{_=RunProgressRulesV1.Apply(Protocol,new(){Kind=RunProgressActionKindV1.EnterNode,NodeId="illegal-node"});}catch(InvalidOperationException){illegalRejected=true;}
            if(!illegalRejected)throw new InvalidOperationException("Illegal route edge accepted.");
            int hpBeforeTravel=player.Creature.CurrentHp;int goldBeforeTravel=player.Gold;var permanentDeck=player.Deck.Cards.ToArray();var persistentRelics=player.Relics.ToArray();
            loop.RunUntilCompleted(RunManager.Instance.EnterMapCoord(next.coord),TimeSpan.FromSeconds(45),"Enter legal next native node");
            var nextCombat=OfflineCombat.WaitForPlayableCombat(loop);
            if (ReferenceEquals(nextCombat,combat)||run.ActFloor!=next.coord.row+1||nextCombat.RunState.CurrentMapPoint?.coord!=next.coord)
                throw new InvalidOperationException("Native next battle identity/coordinate mismatch.");
            if (acquired!=null && !player.PlayerCombatState!.AllCards.Any(c=>ReferenceEquals(c.DeckVersion,acquired)))
                throw new InvalidOperationException("Acquired reward was not cloned into the next battle.");
            Protocol=RunProgressRulesV1.Apply(Protocol,new(){Kind=RunProgressActionKindV1.EnterNode,NodeId=Id(next)});
            if(player.Creature.CurrentHp!=hpBeforeTravel||player.Gold!=goldBeforeTravel||!player.Deck.Cards.SequenceEqual(permanentDeck)||!player.Relics.SequenceEqual(persistentRelics))
                throw new InvalidOperationException("Unexpected persistent resource change during native next-node entry.");
            Phase="next-first-player-boundary";
            Observe(Phase,player,run);
            var state = JsonSerializer.SerializeToElement(ManaState(player),Json);
            if (state.GetProperty("Mana").GetInt32()!=3 || state.GetProperty("Echo").GetDecimal()!=0 || state.GetProperty("Stance").GetInt32()!=0 || player.Creature.Block!=0 || player.Creature.Powers.Count!=0)
                throw new InvalidOperationException("Next CrackedOrb combat failed to open with clean battle resources.");
            NextBoundary=EleanorV040Probe.CaptureNextNativeBoundary(nextCombat);
            if(SecondBattleEnabled)
            {
                Phase="second-battle";
                SecondBattle=RunSecondBattle(nextCombat,loop,output,acquired,options);
                var result=JsonSerializer.SerializeToElement(SecondBattle,Json);
                if(result.GetProperty("status").GetString()!="passed")return Report(result.GetProperty("status").GetString()!,result.GetProperty("reason").GetString());
                Phase="second-post-combat-cleanup";Observe(Phase,player,run);VerifyReset(player);
                if(player.Gold!=goldBeforeTravel||!player.Deck.Cards.SequenceEqual(permanentDeck)||!player.Relics.SequenceEqual(persistentRelics))
                    throw new InvalidOperationException("Persistent resources changed outside native combat effects.");
            }
            return Report("passed");
        }
        catch (PredictionUnsupportedException ex) { return Report("unsupported",ex.ToString()); }
        catch (Exception ex) { return Report("failed",ex.ToString()); }
    }

    private static object ManaState(Player player)
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="MyMod").GetType("MyMod.MyModCode.Combat.ManaRuntime",true)!;
        return type.GetProperty("State")!.GetValue(type.GetMethod("For")!.Invoke(null,[player]))!;
    }
    private static void VerifyReset(Player player)
    {
        var s=JsonSerializer.SerializeToElement(ManaState(player),Json);
        if (s.GetProperty("Mana").GetInt32()!=0 || s.GetProperty("Echo").GetDecimal()!=0 || s.GetProperty("Stance").GetInt32()!=0 || player.Creature.Block!=0 || player.Creature.Powers.Count!=0)
            throw new InvalidOperationException("Native post-Victory combat cleanup failed.");
    }
    private static void Observe(string phase, Player player, RunState run) => Lifecycle.Add(new { phase, hp=player.Creature.CurrentHp,
        maxHp=player.Creature.MaxHp,gold=player.Gold,deck=player.Deck.Cards.Select(c=>c.ToSerializable()).ToArray(),
        relics=player.Relics.Select(r=>r.ToSerializable()).ToArray(),mana=JsonSerializer.SerializeToElement(ManaState(player),Json),
        block=player.Creature.Block,powers=player.Creature.Powers.Select(p=>new { id=p.Id.ToString(),amount=p.Amount }).ToArray(),
        rng=run.Rng.ToSerializable(),playerRng=player.PlayerRng.ToSerializable(),odds=player.PlayerOdds.ToSerializable(),
        floor=run.ActFloor,node=run.CurrentMapPoint?.coord.ToString() });
    private static bool SkipInputPresentationNames() => false;
    private sealed class RewardSelector : ICardSelector
    {
        public CardModel? Selected; public object[] Options=[];
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options,int minSelect,int maxSelect) => throw new NotSupportedException("Unexpected non-reward decision.");
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,IReadOnlyList<CardRewardAlternative> alternatives)
        {
            if (options.Count==0) throw new InvalidOperationException("Empty native card offer.");
            Options=options.Select((option,index)=>(object)new {index,card=option.Card.ToSerializable()}).ToArray();
            Selected=options[0].Card;
            return new CardRewardSelection { card=Selected };
        }
    }
}
