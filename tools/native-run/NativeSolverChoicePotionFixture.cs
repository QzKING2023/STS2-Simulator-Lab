using System.Text.Json;
using SimulatorLab.Core;
#if SOLVER_GATE
using CombatSolver;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Runs;
#endif
namespace OfflineSearchHarness;
internal static partial class Program
{
#if !SOLVER_GATE
    static void SolverChoicePotionFixture()=>throw new NotSupportedException("Separate solver build required");
#else
    static object SolverBinding(CombatState combat,string continuation)=>new{
        continuation=Hash(continuation),state=Hash(JsonSerializer.Serialize(State())),cardIdentity=NativeSolverEnchantments.Protocol,
        hand=player!.PlayerCombatState!.Hand.Cards.Select(c=>new{id=Id(c),key=CardChoiceSupport.ChoiceCardKey(c),enchantment=NativeSolverEnchantments.Capture(c)}).ToArray(),
        creatures=combat.Creatures.Select(c=>new{id=Id(c),combatId=c.CombatId}).ToArray(),
        potions=player.Potions.Select(p=>new{id=Id(p),model=p.Id.ToString(),slot=player.GetPotionSlotIndex(p)}).ToArray()
    };
    static void SolverChoicePotionFixture()
    {
        var combat=CombatManager.Instance.DebugOnlyGetState()!;var pcs=player!.PlayerCombatState!;
        CardModel? grit=null;var potion=ModelDb.Potion<FirePotion>().ToMutable();
        var permanent=player.Deck.Cards.ToArray();string deck=JsonSerializer.Serialize(State().GetProperty("deck"));
        Observe("fixture-setup-declared",new{name=scenario,changes="native CardPileCmd.Add/Upgrade and potion procurement; fixed action protocol fixture, not solver-selected reward or natural deck"});
        Action("FixtureSetup",new{name=scenario},()=>{
            foreach(var c in pcs.Hand.Cards.ToArray())Await(CardPileCmd.Add(c,PileType.Exhaust),"Fixture original hand to Exhaust");
            CardModel Add(string type){var c=combat.CreateCard(ModelDb.All.OfType<CardModel>().Single(x=>x.GetType().Name==type),player);Await(CardPileCmd.Add(c,PileType.Hand),"Fixture actual generated card Add");return c;}
            grit=Add("TrueGrit");CardCmd.Upgrade(grit);_=Add("StrikeIronclad");_=Add("DefendIronclad");_=Add("DefendIronclad");
            Await(PotionCmd.TryToProcure(potion,player),"Fixture native FirePotion procurement");
        });
        var results=new List<object>();
        for(int number=1;number<=2;number++){
            var root=SolverCapture(combat,"fixture",number);var shadow=root.ForkSimulator();var parent=root.ForkSimulator();
            string Stamp(CombatPredictionSimulator sim)=>ContinuationStamp.CapturePredicted(player!,sim,pcs.TurnNumber,root.Forecast,root.StartTurnNumber).StateText;
            string parentBefore=Stamp(parent);var before=State();
            PlanAction action;
            if(number==1){
                var candidate=shadow.State.FindCard(grit!)!;var spec=CardChoiceSupport.GetSpec(shadow,candidate)??throw new NotSupportedException("No TrueGrit choice spec");
                var choice=CardChoiceSupport.BuildRequestedChoice(spec,["DEFEND_IRONCLAD"]);
                action=new(PlanActionKind.PlayCard,pcs.TurnNumber,CardId:grit!.Id.Entry,CardStateKey:CardChoiceSupport.ChoiceCardKey(grit),CardUpgradeLevel:grit.CurrentUpgradeLevel,Choice:choice);
                SolverChildControls(action,pcs.Hand.Cards.Where(c=>!ReferenceEquals(c,grit)).ToArray());
                var sim=(SimulatedCombatState)shadow.State.CombatState;sim.BeginActionChoices(action.GetActionChoicesInExecutionOrder());
                bool played=shadow.ManualPlay(candidate,null,out _);sim.EndActionChoices();
                if(!played||shadow.HasPendingChoice||sim.LastActionChoicesConsumed!=1)throw new NotSupportedException("TrueGrit planned choice unresolved");
            }else{
                action=new(PlanActionKind.UsePotion,pcs.TurnNumber,PotionSlot:player.GetPotionSlotIndex(potion),PotionId:potion.Id.Entry,TargetIndex:0,TargetCombatId:combat.Enemies[0].CombatId);
                var forkPotion=((SimulatedCombatState)shadow.State.CombatState).GetPotionAtSlot(player,action.PotionSlot)??throw new NativeActionRejectedException("Fork potion slot absent");
                if(forkPotion.Id.Entry!=action.PotionId)throw new NativeActionRejectedException("Fork potion slot model mismatch");
                var sim=(SimulatedCombatState)shadow.State.CombatState;int historyStart=shadow.History.Entries.Count;
                if(!PotionOnUseSupport.CanSearch(forkPotion))throw new NotSupportedException("Potion not in pinned solver capability table");
                sim.BeginActionChoices(action.GetActionChoicesInExecutionOrder());
                bool used=PotionExecutionSupport.Prepare(shadow,sim,forkPotion,action.PotionSlot,combat.Enemies[0])&&PotionExecutionSupport.Complete(shadow,sim,forkPotion,combat.Enemies[0],null,historyStart,new HashSet<uint>());
                sim.EndActionChoices();
                if(!used||shadow.HasPendingChoice)throw new NotSupportedException("FirePotion prediction unresolved");
            }
            NativeSolverActionContract.Fields(JsonSerializer.SerializeToElement(action,Json),choices:true,potions:true);
            string expected=Stamp(shadow);if(parentBefore!=Stamp(parent)||root.ContinuationStamp.StateText!=ContinuationStamp.CaptureLive(combat).StateText)throw new NativeDataException("Fixture fork/native contamination");
            var input=JsonSerializer.SerializeToElement(new{nativeState=before,root=root.ContinuationStamp.StateText,binding=SolverBinding(combat,root.ContinuationStamp.StateText)},Json);
            var ticket=new SolverTicket(action,before.GetRawText(),root.ContinuationStamp.StateText,combat,sequence);ticket.Consume(combat);
            object[] children=[];
            if(number==1){
                Action("PlayCard",new{card=Id(grit!),model=grit!.Id.ToString(),target=(string?)null},()=>{
                    using var choiceScope=new NativeSolverChoices(action);
                    if(!grit.TryManualPlay(null))throw new NativeActionRejectedException("Native TrueGrit play rejected");Await(RunManager.Instance.ActionExecutor.FinishedExecutingActions(),"Native solver-choice TrueGrit");choiceScope.AssertConsumed();children=choiceScope.Records.ToArray();
                });
            }else{
                Action("UsePotion",new{potion=Id(potion),slot=action.PotionSlot,target=Id(combat.Enemies[0])},()=>{
                    if(!ReferenceEquals(player.GetPotionAtSlotIndex(action.PotionSlot),potion)||!potion.IsValidTarget(combat.Enemies[0])||!potion.PassesCustomUsabilityCheck)throw new NativeActionRejectedException("Native potion action stale/illegal");
                    using var choiceScope=new NativeSolverChoices(action);potion.EnqueueManualUse(combat.Enemies[0]);Await(RunManager.Instance.ActionExecutor.FinishedExecutingActions(),"Native solver potion");choiceScope.AssertConsumed();
                });
            }
            string actual=ContinuationStamp.CaptureLive(combat).StateText;var after=State();
            Write("solver-fixture-step-"+number+".json",new{number,root=NativeEvidence.Root,context=NativeEvidence.Context,action,input,nativeAfter=after,expected,actual,exact=expected==actual,parentUnchanged=parentBefore==Stamp(parent),children});
            if(actual!=expected)throw new NativeDataException("Solver fixture differential mismatch "+number);
            Contract(deck==JsonSerializer.Serialize(after.GetProperty("deck"))&&player.Deck.Cards.SequenceEqual(permanent,ReferenceEqualityComparer.Instance),"solver fixture permanent deck content and references unchanged");
            if(number==1)Contract(after.GetProperty("block").GetInt32()-before.GetProperty("block").GetInt32()==9&&pcs.Energy==before.GetProperty("combat").GetProperty("energy").GetInt32()-1,"upgraded TrueGrit actual nine block / one energy");
            else Contract(before.GetProperty("combat").GetProperty("enemies")[0].GetProperty("hp").GetInt32()-combat.Enemies[0].CurrentHp==20&&player.GetPotionSlotIndex(potion)==-1,"native FirePotion twenty damage and actual slot removal");
            // Real search still runs after the fixed API action; its plan is not
            // substituted for the explicitly labelled fixture action above.
            _=SolverQuery(combat,number);results.Add(new{number,exact=true});
        }
        Write("solver-choice-potion-report.json",new{scope="fixed-native-API-fixture; not online solver choice selection",actions=results.Count,steps=results,information="Oracle",wholeBattle=false});
    }
    static void SolverChildControls(PlanAction action,CardModel[] options)
    {
        var choice=action.Choice!;var token=choice.Cards.Single();var rows=new List<object>();int before=dispatched;string state=State().GetRawText();
        void Reject(string name,Action test){bool rejected=false;try{test();}catch(Exception e)when(e is NativeActionRejectedException or NotSupportedException){rejected=true;}rows.Add(new{name,rejected});if(!rejected)throw new NativeDataException("Solver child negative accepted: "+name);}
        foreach(var bad in new[]{choice with{Cards=[token with{StateKey="stale"}]},choice with{Cards=[token with{SourceOccurrence=999}]},choice with{Cards=[token with{OptionOccurrence=999}]},choice with{Cards=[token with{CardId="BASH"}]},choice with{Cards=[token with{UpgradeLevel=99}]},choice with{Cards=[]},choice with{Cards=[token,token]},choice with{Effect=PlanChoiceEffect.Discard},choice with{SourcePile=PileType.Draw},choice with{Timing=PlanChoiceTiming.PlayerTurnStart},choice with{ContextId="stale"},choice with{SourceId="OTHER_CARD"}}){Reject("token-or-contract-"+rows.Count,()=>NativeSolverChoices.Resolve(bad,options,1,1,action.CardId));}
        using(var missing=new NativeSolverChoices(action with{Choice=null}))Reject("missing-answer",()=>NativeChoices.SolverProvider!("control",options,1,1,"FromHand",false));
        using(var extra=new NativeSolverChoices(action))Reject("extra-unconsumed-answer",extra.AssertConsumed);
        using(var duplicate=new NativeSolverChoices(action)){
            var answer=NativeChoices.SolverProvider!("control",options,1,1,"FromHand",false);if(answer.Single()!=1)throw new NativeDataException("Control expected actual non-first candidate");
            Reject("surplus-request",()=>NativeChoices.SolverProvider!("control2",options,1,1,"FromHand",false));duplicate.AssertConsumed();
        }
        if(before!=dispatched||state!=State().GetRawText())throw new NativeDataException("Solver child controls mutated source/dispatched");
        Write("solver-child-controls.json",new{scope="actual in-memory token/selector API controls; no native action dispatch",count=rows.Count,controls=rows,zeroAdditionalDispatch=true,sourceUnchanged=true});
    }
#endif
}
