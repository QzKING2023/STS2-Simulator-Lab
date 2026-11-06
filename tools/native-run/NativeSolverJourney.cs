using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using SimulatorLab.Core;
namespace OfflineSearchHarness;

// Existing native first-act nodes/rewards; replace only the battle policy.
internal static class NativeSolverJourney
{
 internal static bool IsScenario(string s)=>!NativePerformancePilot.IsScenario(s)&&(NativeSolverCoordinator.IsScenario(s)||s is "solver-native-boss" or "solver-native-budget" or "solver-native-shop");
 internal static bool ShopScenario(string s)=>s=="solver-native-shop"||(!NativePerformancePilot.IsScenario(s)&&NativeSolverCoordinator.IsScenario(s));
 internal static bool CandidateScenario(string s)=>NativePerformancePilot.IsScenario(s)||s=="solver-native-budget"||ShopScenario(s);
 internal static bool Candidates=>CandidateScenario(NativeLifecycle.Scenario);
 internal static bool Active=>IsScenario(NativeLifecycle.Scenario);
}
internal static partial class Program
{
 static int journeyBattles;
 static readonly List<object> journeyResults=[];
 static void SolverJourneyBattle()
 {
  int battle=++journeyBattles,offset=NativeFullRun.Active?NativeFullRun.Queries:(battle-1)*64;
  var combat=CombatManager.Instance.DebugOnlyGetState()!;
  bool priorRejected=false;
  if(battle>1){
#if SOLVER_GATE
   var stale=previousBattleTicket??throw new NativeDataException("Previous battle ticket missing");
   int count=dispatched;var before=State();
   try{stale.Consume(combat);}catch(NativeActionRejectedException){priorRejected=true;}
   if(!priorRejected||stale.Used||count!=dispatched||!NativeDecisionTicket.SameActionJson(before.GetRawText(),State().GetRawText()))throw new NativeDataException("Journey stale ticket crossed battle or mutated source");
#else
   throw new NotSupportedException("Separate solver build required");
#endif
  }
  Observe("solver-journey-battle-entry",new{battle,offset,priorRejected,information="Oracle",policy="pinned-beam-first-action/v1"});
  try{
   int actions=SolverWholeBattle(offset,close:false);
   Observe("solver-journey-battle-return",new{battle,offset,actions,terminal=terminal.ToString()});
   journeyResults.Add(new{battle,offset,actions,status="completed",terminal=terminal.ToString()});
  }catch(Exception ex){journeyResults.Add(new{battle,offset,status="failed",error=ex.GetType().Name,message=ex.Message,confirmed,dispatched});throw;}
  finally{Write("solver-journey-progress.json",new{scope=NativeFullRun.Active?"normal-start multi-act run; see actual terminal and global progress":"natural first act Boss/rewards and next-act first decision; not full run",information="Oracle",battles=journeyResults,confirmed,dispatched});}
 }
}
