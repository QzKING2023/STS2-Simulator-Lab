using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SimulatorLab.Core;
namespace OfflineSearchHarness;
internal static class NativeSolverRun
{
 internal static bool IsScenario(string s)=>s is "solver-run-skip" or "solver-run-take";
 internal static bool Active=>IsScenario(NativeLifecycle.Scenario);
 internal static bool Taking=>NativeLifecycle.Scenario=="solver-run-take";
 // A fixed reward policy over native legal candidates, never a synthetic award.
 internal static int Choose(NativeRewards.Option[] options,int preferred)
 {
  if(!Active||!options.Any(x=>x.Kind is "GoldReward" or "CardReward" or "SkipRewards"))return preferred;
  int gold=Array.FindIndex(options,x=>x.Kind=="GoldReward");if(gold>=0)return gold;
  int card=Array.FindIndex(options,x=>x.Kind=="CardReward");if(Taking&&card>=0)return card;
  int skip=Array.FindIndex(options,x=>x.Kind=="SkipRewards");return skip>=0?skip:throw new NotSupportedException("Required reward outside solver run policy");
 }
}
internal static partial class Program
{
 static void SolverTwoBattles()
 {
#if SOLVER_GATE
  Travel();Ready();var first=CombatManager.Instance.DebugOnlyGetState()!;
  Observe("solver-run-opening",new{origin="natural-run",information="Oracle",battleLimit=2,totalActionLimit=160,rewardPolicy=NativeSolverRun.Taking?"gold-take-first-card":"gold-skip-card"});
  int n=SolverWholeBattle(close:false);
  if(terminal==NativeRunTerminal.Death)throw new NotSupportedException("Solver first battle died; no reward/next-battle coverage");
  var cleaned=State();Observe("solver-run-reward-entry",new{battle=1});
  CAction("RewardOffer",new{source="native-room-end"},()=>Await(RewardsCmd.OfferForRoomEnd(player!,run!.CurrentRoom!),"Solver run native room-end rewards"));
  NativeRewards.AssertConsumed();
  Observe("solver-run-reward-complete",new{before=cleaned,acquired=NativeRewards.AcquiredCards.Select(NativeIdentity.Ref).ToArray()});
  Action("ProceedRewards",new{battle=1},()=>Await(RunManager.Instance.ProceedFromTerminalRewardsScreen(),"Solver run native proceed to map"));
  TravelC();Ready();var second=CombatManager.Instance.DebugOnlyGetState()!;
  if(ReferenceEquals(first,second))throw new NativeDataException("Next battle reused previous CombatState");
  if(NativeSolverRun.Taking)CardContinuityC();
  var stale=previousBattleTicket??throw new NativeDataException("Missing previous battle ticket");
  int before=dispatched;var stamp=State();bool rejected=false;
  try{stale.Consume(second);}catch(NativeActionRejectedException){rejected=true;}
  if(!rejected||stale.Used||before!=dispatched||!NativeDecisionTicket.SameActionJson(stamp.GetRawText(),State().GetRawText()))throw new NativeDataException("Previous battle ticket accepted or mutated next battle");
  Observe("solver-run-next-battle",new{previousBattleTicketRejected=rejected,unconsumed=!stale.Used,zeroAdditionalDispatch=before==dispatched,oldSequence=stale.Sequence,currentSequence=sequence,differentCombat=true});
  n+=SolverWholeBattle(64);
  if(dispatched>160)throw new TimeoutException("Solver run total action budget");
  Observe("n3c-contract",new{name=scenario,complete=true});NativeRewards.AssertConsumed();
  Observe("solver-run-complete",new{battles=2,solverActions=n,information="Oracle",origin="natural-run",rewardPolicy=NativeSolverRun.Taking?"gold-take-first-card":"gold-skip-card"});
#else
  throw new NotSupportedException("Separate solver build required");
#endif
 }
}
