using System.Reflection;
using System.Text.Json;
using SimulatorLab.Core;
#if SOLVER_GATE
using CombatSolver;
using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
#endif
namespace OfflineSearchHarness;
internal static partial class Program
{
#if SOLVER_GATE
 static bool branchControlsDone;
 static void SolverBranchControls(CombatRootSnapshot root,CombatState native,int query)
 {
  if(branchControlsDone||root.Forecast.ApproximationDetails.Count==0||NativeSolverBranchRuntime.Capture()==null)return;
  var owners=native.Enemies.Where(e=>e.Monster?.MoveStateMachine!=null).Select(e=>e.Monster!).ToArray();
  // Fixture availability must not reject an otherwise supported single-source
  // battle. Wait for a real root with the distinct-source negative precondition.
  var targets=owners.SelectMany(m=>m.MoveStateMachine!.States.Values.OfType<ConditionalBranchState>().Select(b=>(Monster:m,Branch:b)))
      .Where(t=>owners.Any(m=>m!=t.Monster&&!m.MoveStateMachine!.States.ContainsKey(t.Branch.Id))).ToArray();
  if(targets.Length==0)return;
  var resolve=typeof(BranchMonsterAi).GetMethod("ResolveConditional",BindingFlags.NonPublic|BindingFlags.Static)!;
  var parent=root.ForkSimulator();
  string Stamp(CombatPredictionSimulator s)=>ContinuationStamp.CapturePredicted(player!,s,((SimulatedCombatState)s.State.CombatState).GetPlayerTurnNumber(player!),root.Forecast,root.StartTurnNumber).StateText;
  string before=State().GetRawText(),live=ContinuationStamp.CaptureLive(native).StateText,parentBefore=Stamp(parent);int count=dispatched;
  var records=new List<object>();
  foreach(var target in targets){
   var source=BranchMonsterAi.Capture(target.Monster);var a=root.ForkSimulator();var b=root.ForkSimulator();
   var ac=(SimulatedCombatState)a.State.CombatState;var bc=(SimulatedCombatState)b.State.CombatState;
   string Call(BranchMonsterAiState state,CombatPredictionSimulator sim)=>(string)resolve.Invoke(null,[target.Branch,state,(SimulatedCombatState)sim.State.CombatState,sim])!;
   string expected=target.Branch.GetNextState(target.Monster.Creature,new Rng(a.Rng.MonsterAi.ToSerializable()));
   string aBefore=Stamp(a),first=Call(source,a);
   foreach(var power in bc.EffectivePowers().Where(p=>ReferenceEquals(p.Owner,target.Monster.Creature)).ToArray())bc.SetPowerAmount(power,0);
   string changed=Call(source,b),again=Call(source,a);
   // This synthetic source mismatch exercises the general fallback. It is not
   // an encounter or an action, and never mutates a native creature.
   var other=owners.First(m=>m!=target.Monster&&!m.MoveStateMachine!.States.ContainsKey(target.Branch.Id));
   int rejected=0;
   foreach(bool capturedRoot in new[]{true,false}){
    var selections=capturedRoot?new Dictionary<string,string>{{target.Branch.Id,expected}}:new Dictionary<string,string>();
    var bad=source with{Monster=other,Static=source.Static with{ConditionalSelections=selections}};
    try{_=Call(bad,a);}catch(TargetInvocationException ex)when(ex.InnerException is NotSupportedException e&&e.Message.StartsWith("Unverified conditional root fallback:",StringComparison.Ordinal)){rejected++;}
   }
   bool pass=first==expected&&again==first&&Stamp(a)==aBefore&&source.Machine.States.ContainsKey(changed)&&rejected==2;
   records.Add(new{monster=target.Monster.Id.ToString(),branch=target.Branch.Id,origin="branch-api-fixture",nativeCurrent=expected,modeledA=first,zeroPowersB=changed,modeledAgainA=again,changedWithBranchState=first!=changed,parentAUnchanged=Stamp(a)==aBefore,unknownFallbacksRejected=rejected,passed=pass});
   if(!pass){Write("solver-branch-controls.json",new{query,cases=records,passed=false});throw new NativeDataException("Upstream branch-runtime/fallback control failed");}
  }
  bool pure=before==State().GetRawText()&&live==ContinuationStamp.CaptureLive(native).StateText&&parentBefore==Stamp(parent)&&count==dispatched;
  Write("solver-branch-controls.json",new{query,root=NativeEvidence.Root,context=NativeEvidence.Context,cases=records,sourceUnchanged=pure,parentUnchanged=parentBefore==Stamp(parent),zeroAdditionalDispatch=count==dispatched,passed=pure,scope="native current condition plus A/B/A branch fixture and strict unknown fallback; not whole-future forecast certification"});
  if(!pure)throw new NativeDataException("Branch controls changed native/parent or dispatched");branchControlsDone=true;
 }
#endif
}
