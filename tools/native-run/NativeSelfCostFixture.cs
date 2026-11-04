using SimulatorLab.Core;
#if SOLVER_GATE
using CombatSolver;
using CombatSolver.Engine.Common;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
#endif
namespace OfflineSearchHarness;
internal static partial class Program {
#if !SOLVER_GATE
 static void SolverSelfCostFixture()=>throw new NotSupportedException("Solver dependency required");
#else
 static void SolverSelfCostFixture(){
  var combat=CombatManager.Instance.DebugOnlyGetState()!;var pcs=player!.PlayerCombatState!;var enemy=combat.Enemies.Single();
  CardModel first=null!,second=null!;
  Observe("fixture-setup-declared",new{name=scenario,changes="Original card creation, hand exhaust, power removal and HP APIs; nonlethal and lethal HP-cost order. API fixture, not a natural strategy."});
  Action("FixtureSetup",new{name=scenario},()=>{
   foreach(var c in pcs.Hand.Cards.ToArray())Await(CardPileCmd.Add(c,PileType.Exhaust),"Self-cost fixture hand");
   foreach(var p in combat.Creatures.SelectMany(c=>c.Powers).ToArray())Await(PowerCmd.Remove(p),"Self-cost fixture powers");
   CardModel Add(){var c=combat.CreateCard(ModelDb.All.OfType<CardModel>().Single(x=>x.GetType().Name=="Hemokinesis"),player!);Await(CardPileCmd.Add(c,PileType.Hand),"Self-cost original card");return c;}
   first=Add();second=Add();Await(CreatureCmd.SetCurrentHp(player.Creature,10),"Self-cost fixture HP10");
  });
  void Play(CardModel card,int query){
   var action=new PlanAction(PlanActionKind.PlayCard,pcs.TurnNumber,CardId:card.Id.Entry,CardStateKey:CardChoiceSupport.ChoiceCardKey(card),CardUpgradeLevel:card.CurrentUpgradeLevel,TargetIndex:0,TargetCombatId:enemy.CombatId);
   var ticket=new SolverTicket(action,State().GetRawText(),ContinuationStamp.CaptureLive(combat).StateText,combat,sequence);
   _=ExecuteSolverAction(ticket,combat,query,extended:true,allowTerminal:true);
  }
  var enemyBefore=enemy.CurrentHp;Play(first,1);
  if(player.Creature.CurrentHp!=8||enemy.CurrentHp!=enemyBefore-15||!enemy.IsAlive)throw new NativeDataException("Nonlethal cost2 then damage15 independent fixture contract");
  Action("FixtureSetup",new{name="lethal-self-cost"},()=>Await(CreatureCmd.SetCurrentHp(player.Creature,1),"Self-cost fixture HP1"));
  var enemyAtDeath=enemy.CurrentHp;Play(second,2);
  if(SolverNativeTerminal?.Outcome!="Defeat"||player.Creature.CurrentHp!=0||enemy.CurrentHp!=enemyAtDeath)throw new NativeDataException("Native lethal HP cost must precede and prevent attack");
  Write("self-cost-report.json",new{scope="API fixture; actual native two plays, not online strategy",nonlethalHp=8,nonlethalDamage=15,lethalAttackDamage=0,enemyBefore,enemyAtDeath,enemyAfter=enemy.CurrentHp,terminal=SolverNativeTerminal.Outcome,root=SolverNativeTerminal.Root,context=SolverNativeTerminal.Context,state=SolverNativeTerminal.State,continuation=SolverNativeTerminal.Continuation});
 }
#endif
}
