using System.Text.Json;
using SimulatorLab.Core;
#if SOLVER_GATE
using CombatSolver;
using CombatSolver.Engine.InCombat.Mirrors;
using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models.Powers;
#endif
namespace OfflineSearchHarness;
internal static partial class Program
{
#if SOLVER_GATE
 static bool hookBasicDone,hookMultipleDone;
 static readonly List<object> hookControls=[];
 // Branch-only API fixtures. Native HP/powers are never edited. Real turn and
 // terminal timing are independently checked on the following native path.
 static void SolverHookControls(CombatRootSnapshot root,CombatState native,int query)
 {
  bool multiple=native.Enemies.Count(e=>e.IsAlive&&e.IsPrimaryEnemy)>1;
  if(hookBasicDone&&(!multiple||hookMultipleDone))return;
  string live=State().GetRawText(),continuation=ContinuationStamp.CaptureLive(native).StateText;
  var parent=root.ForkSimulator();
  string Stamp(CombatPredictionSimulator s)=>ContinuationStamp.CapturePredicted(player!,s,((SimulatedCombatState)s.State.CombatState).GetPlayerTurnNumber(player!),root.Forecast,root.StartTurnNumber).StateText;
  string original=Stamp(parent);int dispatchCount=dispatched;
  void Case(string name,bool ending,bool closed,bool first,bool afterPlayer,bool killOne=false){
   var sim=root.ForkSimulator();var combat=(SimulatedCombatState)sim.State.CombatState;
   combat.SetAmount<PlatingPower>(player!.Creature,4);
   combat.SetAmount<DrawCardsNextTurnPower>(player.Creature,2);
   combat.SetAmount<HibernatePower>(player.Creature,3);
   if(ending||killOne){
    var enemies=sim.State.Enemies.Where(e=>sim.State.GetCreature(e).IsAlive&&e.IsPrimaryEnemy).ToArray();
    foreach(var enemy in killOne?enemies.Take(1):enemies)sim.State.GetCreature(enemy).CurrentHp=0;
   }
   if(sim.IsOverOrEnding!=ending)throw new NativeDataException("Hook fixture ending precondition: "+name);
   if(closed&&!sim.CheckWinCondition(Math.Max(1,combat.GetPlayerTurnNumber(player))))throw new NativeDataException("Hook fixture terminal precondition");
   string before=Stamp(sim);int history=sim.History.Entries.Count;
   bool complete=afterPlayer?HookMirrors.AfterPlayerTurnStart(sim,player,new TurnStartChoiceCursor(null)):
      combat.TriggerSideTurnStart(sim,CombatSide.Player,[player.Creature],decrementPlating:!first);
   string after=Stamp(sim);
   int plating=combat.GetAmount<PlatingPower>(player.Creature),draw=combat.GetAmount<DrawCardsNextTurnPower>(player.Creature),hibernate=combat.GetAmount<HibernatePower>(player.Creature);
   bool exactNoop=before==after&&history==sim.History.Entries.Count;
   bool passed=complete&&!sim.HasPendingChoice&&(ending?exactNoop:afterPlayer?hibernate==2:plating==(first?4:3)&&draw==0);
   hookControls.Add(new{name,query,origin="branch-api-fixture",nativeMutated=false,ending,closed,first,afterPlayer,killOne,complete,exactNoop,plating,draw,hibernate,historyBefore=history,historyAfter=sim.History.Entries.Count,before,after,passed});
   Write("solver-hook-controls.json",new{scope="branch-only fixtures plus source purity; not native battle outcomes",cases=hookControls,sourceUnchanged=live==State().GetRawText()&&continuation==ContinuationStamp.CaptureLive(native).StateText,parentUnchanged=original==Stamp(parent),zeroAdditionalDispatch=dispatchCount==dispatched});
   if(!passed)throw new NativeDataException("Regular Hook lifecycle control failed: "+name);
  }
  if(!hookBasicDone){
   Case("ongoing-first-side",false,false,true,false);
   Case("ongoing-later-side",false,false,false,false);
   Case("ending-side",true,false,false,false);
   Case("closed-side",true,true,false,false);
   Case("ongoing-after-player",false,false,false,true);
   Case("ending-after-player",true,false,false,true);
   hookBasicDone=true;
  }
  if(multiple&&!hookMultipleDone){
   Case("nonlast-death-side",false,false,false,false,killOne:true);
   Case("last-death-side",true,false,false,false);
   hookMultipleDone=true;
  }
  if(live!=State().GetRawText()||continuation!=ContinuationStamp.CaptureLive(native).StateText||original!=Stamp(parent)||dispatchCount!=dispatched)throw new NativeDataException("Hook lifecycle fixtures changed native/parent or dispatched");
 }
#endif
}
