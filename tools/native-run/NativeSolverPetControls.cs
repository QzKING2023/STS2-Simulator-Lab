using System.Text.Json;
#if SOLVER_GATE
using CombatSolver;
using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;
#endif
namespace OfflineSearchHarness;
internal static partial class Program
{
#if SOLVER_GATE
 static bool petControlsDone;
 static void SolverPetControls(CombatRootSnapshot root,CombatState native,int query)
 {
  if(petControlsDone||!NativePerformancePilot.IsScenario(scenario))return;
  string liveBefore=ContinuationStamp.CaptureLive(native).StateText,before=State().GetRawText();int count=dispatched;
  var parent=root.ForkSimulator();var a=root.ForkSimulator();var b=root.ForkSimulator();
  string Stamp(CombatPredictionSimulator s)=>ContinuationStamp.CapturePredicted(player!,s,((SimulatedCombatState)s.State.CombatState).GetPlayerTurnNumber(player!),root.Forecast,root.StartTurnNumber).StateText;
  var pc=(SimulatedCombatState)parent.State.CombatState;var ac=(SimulatedCombatState)a.State.CombatState;var bc=(SimulatedCombatState)b.State.CombatState;
  var captured=player!.Osty;string parentBefore=Stamp(parent),bBefore=Stamp(b);
  if(!ReferenceEquals(pc.GetOsty(player),captured)||!ReferenceEquals(ac.GetOsty(player),captured)||!ReferenceEquals(bc.GetOsty(player),captured))throw new NativeDataException("Pet root presence/absence binding failed");
  // A/B/A uses upstream branch APIs only. This synthetic summon is never a native action.
  ac.SummonOsty(a,player,2);string aAfter=Stamp(a);
  bool firstIsolated=Stamp(parent)==parentBefore&&Stamp(b)==bBefore&&aAfter!=parentBefore;
  bc.SummonOsty(b,player,3);string bAfter=Stamp(b);
  bool aba=Stamp(a)==aAfter&&Stamp(parent)==parentBefore&&bAfter!=bBefore&&bAfter!=aAfter;
  bool generatedDistinct=captured!=null||(!ReferenceEquals(ac.GetOsty(player),bc.GetOsty(player))&&pc.GetOsty(player)==null);
  bool pure=liveBefore==ContinuationStamp.CaptureLive(native).StateText&&before==State().GetRawText()&&count==dispatched&&ReferenceEquals(player.Osty,captured);
  bool passed=firstIsolated&&aba&&generatedDistinct&&pure;
  Write("solver-pet-controls.json",new{query,origin="branch-api-fixture",information="Oracle",capturedPresent=captured!=null,firstIsolated,aba,generatedDistinct,sourceUnchanged=pure,zeroAdditionalDispatch=count==dispatched,parentBefore,aAfter,bAfter,parentAfter=Stamp(parent),passed,scope="captured pet presence/absence and upstream SummonOsty fork A/B/A; native cleanup checked separately by each online step"});
  if(!passed)throw new NativeDataException("Pet branch isolation control failed");petControlsDone=true;
 }
#endif
}
