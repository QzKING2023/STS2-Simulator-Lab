using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SimulatorLab.Core;

namespace OfflineSearchHarness;
internal static class NativeRewardProbe
{
    internal static readonly HashSet<string> Fixtures=["reward-gold","reward-zero","reward-empty","reward-skip","reward-required","reward-card","reward-card-skip","reward-consecutive","reward-nested","reward-capacity","reward-relic","reward-fire","reward-block","reward-treasure","reward-treasure-skip","reward-repeat-treasure"];
    internal static readonly HashSet<string> Faults=["reward-missing","reward-stale","reward-cross-set","reward-invalid","reward-extra","reward-duplicate","reward-task-fault","reward-potion-stale","reward-potion-wrong-target","reward-potion-dead-target","reward-potion-timing","reward-potion-effect-fault","reward-required-skip"];
    internal static bool IsScenario(string s)=>NativeSolverRun.IsScenario(s)||NativeRewardClosure.IsScenario(s)||s=="rewards-natural"||Fixtures.Contains(s)||Faults.Contains(s);
    internal static bool Active=>IsScenario(NativeLifecycle.Scenario)||NativeEventFlow.Unified||NativeMajor.Active||NativeEventFixtures.Active;
    internal static string Scenario=>NativeLifecycle.Scenario;
}
internal static partial class Program
{
    static void CAction(string kind,object decision,Action execute)
    {
        NativeRewards.Operation? root=null;
        Action(kind,decision,()=>{root=NativeRewards.Begin(kind);execute();NativeRewards.End(root,new{completed=true});});
        NativeRewards.Boundary(root!);
    }
    static void TravelC()
    {
        if(!MegaCrit.Sts2.Core.Hooks.Hook.ShouldProceedToNextMapPoint(run!))throw new NativeActionRejectedException("Native travel guard refused");
        var current=run!.CurrentMapPoint!;var children=current.Children.OrderBy(x=>x.coord.col).ThenBy(x=>x.coord.row).ToArray();
        if(children.Length==0)throw new NotSupportedException("4C natural route has no child");
        var options=children.Select(x=>(object)new{from=current.coord.ToString(),to=x.coord.ToString()}).ToArray();var next=children[PolicyIndex("NativeTravel",options)];
        Observe("map-candidates",options);
        CAction("NativeTravel",new{from=current.coord.ToString(),to=next.coord.ToString()},()=>{Await(RunManager.Instance.EnterMapCoord(next.coord),"Native map travel");if(run.CurrentRoom is CombatRoom)Ready();else throw new NotSupportedException("4C natural route reached non-combat; node package deferred");});
    }
    static void CardContinuityC()
    {
        var pcs=player!.PlayerCombatState??throw new NativeDataException("No next combat card state");
        var copies=pcs.Hand.Cards.Concat(pcs.DrawPile.Cards).Concat(pcs.DiscardPile.Cards).Concat(pcs.ExhaustPile.Cards).ToArray();
        if(NativeRewards.AcquiredCards.Any(p=>!player.Deck.Cards.Contains(p)||!copies.Any(c=>ReferenceEquals(c.DeckVersion,p))))throw new NativeDataException("Native reward card lost permanent/combat linkage");
        Observe("n3c-card-continuity",new{permanent=NativeRewards.AcquiredCards.Select(NativeIdentity.Ref).ToArray()});
    }
    static void OfferC(List<Reward> rewards,bool required=false,bool empty=false)
    {
        CAction("RewardOffer",new{source="declared-native-custom",required,empty},()=>{
            var set=new RewardsSet(player!).WithCustomRewards(rewards);
            if(required)set.WithSkippingDisallowed();if(empty)set.EmptyForRoom(run!.CurrentRoom!);
            Await(set.Offer(),"Native custom reward set");
        });
    }
    static void UsePotionC(PotionModel potion,Creature? target)
    {
        int slot=player!.GetPotionSlotIndex(potion);string stamp=StateStamp();
        bool inCombat=CombatManager.Instance.IsInProgress;
        if(inCombat&&target!=null&&!CombatManager.Instance.DebugOnlyGetState()!.Creatures.Contains(target))throw new NativeActionRejectedException("Potion target outside current combat");
        if(slot<0||!ReferenceEquals(player.GetPotionAtSlotIndex(slot),potion)||potion.IsQueued||!player.CanUseOrRemovePotions||!potion.PassesCustomUsabilityCheck||potion.Usage is not (PotionUsage.CombatOnly or PotionUsage.AnyTime)||potion.Usage==PotionUsage.CombatOnly&&!inCombat||inCombat&&player.PlayerCombatState?.Phase!=PlayerTurnPhase.Play||!potion.IsValidTarget(target))throw new NativeActionRejectedException("Illegal/stale potion/target/timing");
        new NativeDecisionTicket(potion,sequence,stamp,"UsePotion").Consume(potion,sequence,StateStamp(),"UsePotion");
        CAction("UsePotion",new{potion=NativeIdentity.Ref(potion),slot,target=NativeIdentity.Ref(target)},()=>{
            var op=NativeRewards.Begin("potion-use",potion);
            potion.EnqueueManualUse(target);Await(RunManager.Instance.ActionExecutor.FinishedExecutingActions(),"Native potion use action");
            if(player.Potions.Contains(potion)||!potion.HasBeenRemovedFromState)throw new NativeDataException("Native potion use did not consume specified instance");
            if(scenario=="reward-potion-effect-fault")throw new InvalidOperationException("labelled fault after actual native potion consumption/effect");
            NativeRewards.End(op,new{consumed=NativeIdentity.Ref(potion),target=NativeIdentity.Ref(target)});
        });
    }
    static void RewardsProbe()
    {
        var previous=RewardsSet.testSelector;RewardsSet.testSelector=NativeRewards.Collect;
        try{
            if(NativeRewardClosure.Active){ClosureProbe();}
            else if(scenario=="rewards-natural"){
                TravelC();Battle();if(terminal==NativeRunTerminal.Death)return;
                CAction("RewardOffer",new{source="native-room-end"},()=>Await(RewardsCmd.OfferForRoomEnd(player!,run!.CurrentRoom!),"Native room-end collection"));
                Await(RunManager.Instance.ProceedFromTerminalRewardsScreen(),"Native proceed from rewards");
                var gained=player!.Potions.ToArray();TravelC();CardContinuityC();
                var choices=(from p in gained from t in new Creature?[]{null}.Concat(CombatManager.Instance.DebugOnlyGetState()!.Creatures.Cast<Creature?>()) where p.IsValidTarget(t)&&p.PassesCustomUsabilityCheck&&(p.Usage is PotionUsage.CombatOnly or PotionUsage.AnyTime) select(p,t)).ToArray();
                if(choices.Length==0)throw new NotSupportedException("No naturally acquired usable potion at next battle");UsePotionC(choices[0].p,choices[0].t);
            }else{
                Observe("fixture-setup-declared",new{name=scenario,fixtureKind="native-reward-API",changes="Explicit custom rewards / native debug room / native potion procurement only; natural run is separate"});
                int goldBefore=player!.Gold;int deckBefore=player.Deck.Cards.Count;
                if(NativeRepeatTreasure.Active){RepeatTreasure();}
                else if(scenario is "reward-treasure" or "reward-treasure-skip"){
                    CAction("RewardSetup",new{room="Treasure"},()=>Await(RunManager.Instance.EnterRoomDebug(RoomType.Treasure,showTransition:false),"Declared treasure fixture"));TreasureC(scenario.EndsWith("skip"));
                }else if(scenario is "reward-fire" or "reward-block" or "reward-potion-stale" or "reward-potion-wrong-target" or "reward-potion-dead-target" or "reward-potion-timing" or "reward-potion-effect-fault"){
                    PotionModel p=scenario=="reward-block"?ModelDb.Potion<BlockPotion>().ToMutable():ModelDb.Potion<FirePotion>().ToMutable();
                    CAction("RewardSetup",new{potion=p.Id.ToString()},()=>Await(PotionCmd.TryToProcure(p,player),"Declared native potion setup"));
                    if(scenario!="reward-potion-timing")TravelC();
                    var target=scenario=="reward-block"||scenario=="reward-potion-wrong-target"?player.Creature:CombatManager.Instance.DebugOnlyGetState()?.Enemies.First();
                    if(scenario=="reward-potion-stale")CAction("RewardSetup",new{replace="native discard then procure; old object retained only as negative control"},()=>{Await(PotionCmd.Discard(p),"Native discard old potion");Await(PotionCmd.TryToProcure(ModelDb.Potion<BlockPotion>().ToMutable(),player),"Native replacement potion");});
                    if(scenario=="reward-potion-dead-target")CAction("RewardSetup",new{target=NativeIdentity.Ref(target),fixture="actual native kill for dead-target rejection"},()=>Await(CreatureCmd.Kill(target!),"Declared dead target setup"));
                    UsePotionC(p,target);
                }else{
                    if(scenario=="reward-capacity")CAction("RewardSetup",new{fill="native slots"},()=>{while(player.HasOpenPotionSlots)Await(PotionCmd.TryToProcure(ModelDb.Potion<BlockPotion>().ToMutable(),player),"Native capacity setup");});
                    if(NativeRewardProbe.Faults.Contains(scenario)&&scenario!="reward-task-fault")NativeRewards.Fault=scenario[7..];
                    if(scenario is "reward-skip" or "reward-required-skip"&&!NativeChoices.Replaying)NativeRewards.Planned.Enqueue(1);
                    if(scenario=="reward-card"&&!NativeChoices.Replaying){NativeRewards.Planned.Enqueue(0);NativeRewards.Planned.Enqueue(1);}
                    if(scenario=="reward-card-skip"&&!NativeChoices.Replaying){NativeRewards.Planned.Enqueue(0);NativeRewards.Planned.Enqueue(2);}
                    List<Reward> rewards=scenario switch{
                        "reward-empty"=>[],"reward-zero"=>[new GoldReward(0,player)],
                        "reward-card" or "reward-card-skip"=>[new CardReward([run!.CreateCard<StrikeIronclad>(player),run.CreateCard<DefendIronclad>(player)],CardCreationSource.Encounter,player,CardCreationOptions.ForRoom(player,RoomType.Monster))],
                        "reward-capacity"=>[new PotionReward(ModelDb.Potion<FirePotion>().ToMutable(),player)],
                        "reward-relic"=>[new RelicReward(ModelDb.Relic<Vajra>().ToMutable(),player)],
                        "reward-nested"=>[new RelicReward(ModelDb.Relic<Cauldron>().ToMutable(),player)],
                        _=>[new GoldReward(7,player)]};
                    OfferC(rewards,scenario is "reward-required" or "reward-required-skip",scenario=="reward-empty");
                    if(scenario=="reward-consecutive")OfferC([new GoldReward(11,player)]);
                    if(scenario=="reward-relic")TravelC();
                    if(scenario=="reward-card"){TravelC();CardContinuityC();}
                }
                Observe("n3c-assertion",new{name=scenario,goldBefore,deckBefore});
            }
            NativeRewards.AssertConsumed();Observe("n3c-contract",new{name=scenario,complete=true});
        }finally{RewardsSet.testSelector=previous;Write("reward-choices.json",NativeRewards.Transcript);}
    }
}
