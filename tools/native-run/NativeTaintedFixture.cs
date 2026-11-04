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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
#endif
namespace OfflineSearchHarness;
internal static partial class Program {
#if !SOLVER_GATE
 static void SolverTaintedFixture()=>throw new NotSupportedException("Solver dependency required");
#else
 static void SolverTaintedFixture(){
  var combat=CombatManager.Instance.DebugOnlyGetState()!;var pcs=player!.PlayerCombatState!;var enemy=combat.Enemies.Single();
  CardModel first=null!,second=null!,skill=null!;
  Observe("fixture-setup-declared",new{name=scenario,changes="Original native card creation/hand exhaust, PowerCmd.Apply/Remove, VitalSpark.BeforeCombatStart and HP APIs; tests attached removal versus lethal detachment. Fixed API fixture, not natural strategy."});
  void Afflict(){Await(PowerCmd.Apply<VitalSparkPower>(new ThrowingPlayerChoiceContext(),enemy,1,enemy,null),"Fixture actual VitalSpark apply");Await(enemy.GetPower<VitalSparkPower>()!.BeforeCombatStart(),"Fixture original power entry hook");}
  Action("FixtureSetup",new{name=scenario},()=>{
   foreach(var c in pcs.Hand.Cards.ToArray())Await(CardPileCmd.Add(c,PileType.Exhaust),"Fixture original hand");
   foreach(var p in combat.Creatures.SelectMany(c=>c.Powers).ToArray())Await(PowerCmd.Remove(p),"Fixture original power removal");
   CardModel Add(string type){var c=combat.CreateCard(ModelDb.All.OfType<CardModel>().Single(x=>x.GetType().Name==type),player);Await(CardPileCmd.Add(c,PileType.Hand),"Fixture original card add");return c;}
   first=Add("StrikeIronclad");second=Add("StrikeIronclad");skill=Add("DefendIronclad");Afflict();
  });
  if(skill.Affliction is not Tainted{Amount:1})throw new NativeDataException("Original fixture did not afflict skill once");
  void Play(CardModel card,int query){
   var action=new PlanAction(PlanActionKind.PlayCard,pcs.TurnNumber,CardId:card.Id.Entry,CardStateKey:CardChoiceSupport.ChoiceCardKey(card),CardUpgradeLevel:card.CurrentUpgradeLevel,TargetIndex:0,TargetCombatId:enemy.CombatId);
   var ticket=new SolverTicket(action,State().GetRawText(),ContinuationStamp.CaptureLive(combat).StateText,combat,sequence);
   _=ExecuteSolverAction(ticket,combat,query,extended:true,allowTerminal:true);
  }
  Play(first,1);if(!enemy.IsAlive||skill.Affliction is not Tainted{Amount:1})throw new NativeDataException("Nonlethal Tainted preservation contract");
  var root=SolverCapture(combat,"removal-fixture",2);var shadow=root.ForkSimulator();var parent=root.ForkSimulator();
  string Stamp(CombatPredictionSimulator x)=>ContinuationStamp.CapturePredicted(player!,x,pcs.TurnNumber,root.Forecast,root.StartTurnNumber).StateText;
  string prior=Stamp(parent);var sim=(SimulatedCombatState)shadow.State.CombatState;sim.SetAmount<VitalSparkPower>(enemy,0);sim.NormalizePowerCardState(shadow);
  string expected=Stamp(shadow);var before=State();
  Action("FixtureSetup",new{name="attached-power-removal"},()=>Await(PowerCmd.Remove(enemy.GetPower<VitalSparkPower>()!),"Original attached power removal"));
  string actual=ContinuationStamp.CaptureLive(combat).StateText;bool exact=actual==expected,isolated=prior==Stamp(parent);
  Write("tainted-removal.json",new{scope="actual native PowerCmd.Remove vs existing branch power API; not a plan action",root=NativeEvidence.Root,context=NativeEvidence.Context,before,after=State(),expected,actual,exact,parentUnchanged=isolated,ownerAttached=enemy.CombatState!=null,skillAffliction=skill.Affliction?.Id.ToString()});
  if(!exact||!isolated||enemy.CombatState==null||skill.Affliction!=null)throw new NativeDataException("Attached removal differential or contract failed");
  Action("FixtureSetup",new{name="lethal-power-owner"},()=>{Afflict();Await(CreatureCmd.SetCurrentHp(enemy,1),"Fixture original lethal boundary input");});
  Play(second,3);
  if(SolverNativeTerminal?.Outcome!="Victory"||!SolverNativeTerminal.Continuation.Contains("TAINTED:1",StringComparison.Ordinal)||enemy.CombatState!=null)throw new NativeDataException("Detached original death must retain pre-cleanup Tainted");
  Write("tainted-lifecycle-report.json",new{scope="API fixture; actual original commands, not natural win",nonlethal=true,attachedRemoval=true,detachedDeath=true,terminal=SolverNativeTerminal.Outcome,root=SolverNativeTerminal.Root,context=SolverNativeTerminal.Context,state=SolverNativeTerminal.State,continuation=SolverNativeTerminal.Continuation});
 }
#endif
}
