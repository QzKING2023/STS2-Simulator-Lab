using System.Text.Json;
using SimulatorLab.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
#if SOLVER_GATE
using CombatSolver;
using CombatSolver.Engine.InCombat.Simulation;
#endif
namespace OfflineSearchHarness;
internal static class NativeSolverBattle
{
 internal static bool IsScenario(string s)=>s is "solver-battle-natural" or "solver-battle-death" or "solver-tainted-lifecycle" or "solver-self-cost" or "solver-power-death";
 internal static bool MultiScenario(string s)=>NativeSolverRun.IsScenario(s)||NativeSolverJourney.IsScenario(s);
 internal static bool Active=>IsScenario(NativeLifecycle.Scenario)||MultiScenario(NativeLifecycle.Scenario);
}
internal static partial class Program
{
#if SOLVER_GATE
 sealed record SolverTerminalObservation(string Outcome,int Query,string Root,string Context,JsonElement State,string Continuation);
 static SolverTerminalObservation? SolverNativeTerminal;
 static Func<string>? solverTerminalRead;
 static int solverTerminalQuery;
 static void ArmSolverTerminal(CombatState combat,int query){SolverNativeTerminal=null;solverTerminalQuery=query;solverTerminalRead=()=>ContinuationStamp.CaptureLive(combat).StateText;}
 internal static void CaptureSolverTerminal(string outcome)
 {
  if(!NativeSolverBattle.Active||solverTerminalRead==null)return;
  if(SolverNativeTerminal!=null)throw new NativeDataException("Duplicate native solver terminal callback");
  if(outcome=="Victory"?(player!.Creature.CurrentHp<=0||CombatManager.Instance.DebugOnlyGetState()!.Enemies.Any(e=>e.IsAlive&&e.IsPrimaryEnemy)):!player!.Creature.IsDead)throw new NativeDataException("Native solver terminal facts inconsistent");
  SolverNativeTerminal=new(outcome,solverTerminalQuery,NativeEvidence.Root,NativeEvidence.Context,State(),solverTerminalRead());
  Observe("solver-terminal-captured",SolverNativeTerminal);
 }
 static void CheckSolverTerminal(CombatTerminalStamp? expected,int query)
 {
  solverTerminalRead=null;
  if((expected==null)!=(SolverNativeTerminal==null)||expected!=null&&(SolverNativeTerminal!.Outcome!=expected.Value.Outcome.ToString()||SolverNativeTerminal.Query!=query||SolverNativeTerminal.Root!=NativeEvidence.Root||SolverNativeTerminal.Context!=NativeEvidence.Context))throw new NativeDataException("Predicted/native terminal outcome or ownership mismatch");
  if(expected!=null)Observe("solver-terminal-verified",new{query,outcome=expected.Value.Outcome.ToString(),turn=expected.Value.PlayerTurn,preCleanup=SolverNativeTerminal!.State,continuation=SolverNativeTerminal.Continuation});
 }
 static SolverTicket? previousBattleTicket;
 static int SolverWholeBattle(int queryOffset=0,bool close=true)
 {
  var combat=CombatManager.Instance.DebugOnlyGetState()!;
  if(scenario=="solver-battle-death"){
   Observe("fixture-setup-declared",new{name=scenario,changes="Actual native hand-to-Exhaust and HP1 API precondition; no forced decision or simulated outcome"});
   Action("FixtureSetup",new{name=scenario},()=>{
    foreach(var c in player!.PlayerCombatState!.Hand.Cards.ToArray())Await(CardPileCmd.Add(c,PileType.Exhaust),"Declared death fixture empty hand");
    Await(CreatureCmd.SetCurrentHp(player.Creature,1),"Declared death fixture native HP1");
   });
  }
  int victories=NativeHost.BattleVictories,losses=NativeLifecycle.Losses;int completed=0;
  int maxQueries=NativeFullRun.Active?NativeFullRun.Configuration!.Queries:64;
  var opening=State();Observe("solver-battle-opening",new{origin=provenance.ScenarioKind,information="Oracle",maxActions=maxQueries,maxQueries,budgetScope=NativeFullRun.Active?"existing-global-run-limits":"legacy-single-battle"});
  for(int localQuery=1;localQuery<=maxQueries;localQuery++){
   int query=NativeFullRun.Active?NativeFullRun.Queries+1:queryOffset+localQuery;
   Ready();var ticket=SolverQuery(combat,query);
   var crossBattle=ticket with{};
   var executed=ExecuteSolverAction(ticket,combat,query,extended:true,allowTerminal:true);completed++;
   if(executed.Terminal==null)continue;
   string boundary=WaitBoundary();
   if(executed.Terminal=="Victory"){
    if(boundary!="BattleVictory"||NativeHost.BattleVictories!=victories+1||NativeLifecycle.Losses!=losses||player!.Creature.CurrentHp<=0)throw new NativeDataException("Native solver Victory lifecycle incomplete");
   }else{
    if(boundary!="Death"||NativeLifecycle.Losses!=losses+1||NativeHost.BattleVictories!=victories||!player!.Creature.IsDead)throw new NativeDataException("Native solver Death lifecycle incomplete");
    terminal=NativeRunTerminal.Death;
   }
   if(CombatManager.Instance.IsInProgress)throw new NativeDataException("Native solver terminal still in progress");
   Observe("solver-battle-cleanup",new{outcome=executed.Terminal,query,historyEntries=CombatManager.Instance.History.Entries.Count(),battleVictories=NativeHost.BattleVictories-victories,losses=NativeLifecycle.Losses-losses,retailRunLossFinalization=false,afterCombatSimulated=false});
   previousBattleTicket=crossBattle;
   int before=dispatched;bool refused=false;
   if(close){
    NativeLifecycle.Closed=true;
    try{Action("EndTurn",new{turn=0},()=>throw new NativeDataException("Terminal control dispatched"));}catch(NativeActionRejectedException){refused=true;}
    if(!refused||before!=dispatched)throw new NativeDataException("Closed solver battle accepted action");
   }
   Observe("solver-battle-complete",new{nativeActionsExecuted=completed,queries=localQuery,outcome=executed.Terminal,wholeBattle=true,information="Oracle",origin=provenance.ScenarioKind,closedRejected=refused,zeroAdditionalDispatch=before==dispatched,opening,retailRunLossFinalization=false});return completed;
  }
  throw new TimeoutException("Whole solver battle reached declared action/query budget");
 }
#else
 internal static void CaptureSolverTerminal(string outcome){}
 static int SolverWholeBattle(int queryOffset=0,bool close=true)=>throw new NotSupportedException("Separate solver build required");
#endif
}
