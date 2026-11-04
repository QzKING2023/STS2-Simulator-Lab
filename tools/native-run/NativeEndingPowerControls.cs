using System.Text.Json;
using SimulatorLab.Core;
#if SOLVER_GATE
using CombatSolver;
using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models.Powers;
#endif
namespace OfflineSearchHarness;
internal static partial class Program
{
#if SOLVER_GATE
 static bool endingPowerBasic,endingPowerMultiple;
 static readonly List<object> endingPowerCases=[];
 static void EndingPowerControls(CombatRootSnapshot root,CombatState native,int query){
  var proof=JsonSerializer.SerializeToElement(NativeSolverBranchRuntime.Capture());
  if(proof.ValueKind!=JsonValueKind.Object||proof.GetProperty("derivative").GetString() is not ("ending-powers.2" or "possessed-root.1"))return;
  bool multiple=native.Enemies.Count(e=>e.IsAlive&&e.IsPrimaryEnemy)>1;
  if(endingPowerBasic&&(!multiple||endingPowerMultiple))return;
  string live=StateStamp(),nativeStamp=ContinuationStamp.CaptureLive(native).StateText;int count=dispatched;
  var parent=root.ForkSimulator();
  string Stamp(CombatPredictionSimulator sim)=>ContinuationStamp.CapturePredicted(player!,sim,((SimulatedCombatState)sim.State.CombatState).GetPlayerTurnNumber(player!),root.Forecast,root.StartTurnNumber).StateText;
  string parentBefore=Stamp(parent);
  void Case(string name,bool ending,bool closed,bool killOne=false){
   var sim=root.ForkSimulator();var state=(SimulatedCombatState)sim.State.CombatState;
   // Labelled branch-only preconditions; native resources/powers are untouched.
   state.SetAmount<StrengthPower>(player!.Creature,0);state.SetAmount<NoDrawPower>(player.Creature,0);
   if(ending||killOne){var enemies=sim.State.Enemies.Where(e=>sim.State.GetCreature(e).IsAlive&&e.IsPrimaryEnemy).ToArray();foreach(var enemy in killOne?enemies.Take(1):enemies)sim.State.GetCreature(enemy).CurrentHp=0;}
   if(sim.IsEnding!=ending)throw new NativeDataException("Ending power fixture precondition");
   if(closed)sim.CheckWinCondition(Math.Max(1,state.GetPlayerTurnNumber(player)));
   string before=Stamp(sim);int history=sim.History.Entries.Count;
   state.Apply<StrengthPower>(player.Creature,1,player.Creature);state.Apply<NoDrawPower>(player.Creature,1,player.Creature);
   bool valid=ending?before==Stamp(sim)&&history==sim.History.Entries.Count:state.GetAmount<StrengthPower>(player.Creature)==1&&state.GetAmount<NoDrawPower>(player.Creature)==1;
   endingPowerCases.Add(new{name,query,scope="branch-api-fixture",ending,closed,killOne,valid});
   if(!valid)throw new NativeDataException("Generic ending power guard control failed: "+name);
  }
  if(!endingPowerBasic){Case("alive-applies",false,false);Case("ending-skips",true,false);Case("terminal-skips",true,true);endingPowerBasic=true;}
  if(multiple&&!endingPowerMultiple){Case("one-dead-other-alive-applies",false,false,true);endingPowerMultiple=true;}
  bool pure=live==StateStamp()&&nativeStamp==ContinuationStamp.CaptureLive(native).StateText&&count==dispatched&&parentBefore==Stamp(parent);
  Write("ending-power-controls.json",new{scope="branch-only API controls; actual draw-kill native differential tested separately",cases=endingPowerCases,sourceUnchanged=pure,zeroAdditionalDispatch=count==dispatched});if(!pure)throw new NativeDataException("Ending power controls polluted parent/native");
 }
#endif
}
