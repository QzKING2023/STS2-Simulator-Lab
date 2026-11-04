using SimulatorLab.Core;
#if SOLVER_GATE
using CombatSolver;
using CombatSolver.Engine.Common;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
#endif
namespace OfflineSearchHarness;
internal static partial class Program {
#if !SOLVER_GATE
 static void SolverPowerDeathFixture()=>throw new NotSupportedException("Solver dependency required");
#else
 static void SolverPowerDeathFixture(){
  var combat=CombatManager.Instance.DebugOnlyGetState()!;var pcs=player!.PlayerCombatState!;var enemy=combat.Enemies.Single();CardModel first=null!,second=null!;
  Observe("fixture-setup-declared",new{name=scenario,changes="Native Strike card creation, FlutterPower5 on opening enemy, hand/power and HP APIs. Isolates amount-cache decrement and lethal cleanup; not natural encounter or strategy."});
  Action("FixtureSetup",new{name=scenario},()=>{
   foreach(var c in pcs.Hand.Cards.ToArray())Await(CardPileCmd.Add(c,PileType.Exhaust),"Power death fixture hand");
   foreach(var p in combat.Creatures.SelectMany(c=>c.Powers).ToArray())Await(PowerCmd.Remove(p),"Power death fixture powers");
   CardModel Add(){var c=combat.CreateCard(ModelDb.All.OfType<CardModel>().Single(x=>x.GetType().Name=="StrikeIronclad"),player!);Await(CardPileCmd.Add(c,PileType.Hand),"Power death original card");return c;}
   first=Add();second=Add();Await(PowerCmd.Apply<FlutterPower>(new ThrowingPlayerChoiceContext(),enemy,5,enemy,null),"Original amount power apply");
  });
  void Play(CardModel card,int query){
   var action=new PlanAction(PlanActionKind.PlayCard,pcs.TurnNumber,CardId:card.Id.Entry,CardStateKey:CardChoiceSupport.ChoiceCardKey(card),CardUpgradeLevel:card.CurrentUpgradeLevel,TargetIndex:0,TargetCombatId:enemy.CombatId);
   var ticket=new SolverTicket(action,State().GetRawText(),ContinuationStamp.CaptureLive(combat).StateText,combat,sequence);
   _=ExecuteSolverAction(ticket,combat,query,extended:true,allowTerminal:true);
  }
  var hp=enemy.CurrentHp;Play(first,1);
  if(enemy.CurrentHp!=hp-3||enemy.GetPower<FlutterPower>()?.Amount!=4)throw new NativeDataException("Independent nonlethal 50% damage and counter decrement contract");
  Action("FixtureSetup",new{name="amount-power-lethal"},()=>Await(CreatureCmd.SetCurrentHp(enemy,1),"Power death fixture HP1"));
  Play(second,2);
  if(SolverNativeTerminal?.Outcome!="Victory"||enemy.CombatState!=null||SolverNativeTerminal.Continuation.Contains("FLUTTER_POWER",StringComparison.Ordinal))throw new NativeDataException("Actual detached death power must not reappear in continuation");
  Write("power-death-report.json",new{scope="API fixture, not natural encounter; two native plays and exact diffs",nonlethalDamage=3,nonlethalAmount=4,detached=true,terminal=SolverNativeTerminal.Outcome,state=SolverNativeTerminal.State,continuation=SolverNativeTerminal.Continuation});
 }
#endif
}
