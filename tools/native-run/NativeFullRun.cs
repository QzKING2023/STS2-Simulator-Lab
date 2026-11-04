using System.Diagnostics;
using System.Text.Json;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Runs;
using SimulatorLab.Core;
namespace OfflineSearchHarness;

// Internal run orchestration and admission limits. These do not replace native
// actions, search budgets, or the external process-tree/storage watchdog.
internal static class NativeFullRun
{
 internal sealed record Limits(string Schema,int Actions,int Queries,int WallSeconds,long TraceBytes);
 internal static Limits? Configuration=>NativeSolverCoordinator.Configuration?.Run;
 internal static bool Active=>Configuration!=null;
 static readonly Stopwatch Clock=Stopwatch.StartNew();
 internal static int Queries,ProbeQueries;
 internal static void Validate(Limits? c){
  if(c==null)return;
  int wall=c.Schema=="native-full-run/v1"?150:c.Schema=="native-full-run/v2"?450:0;
  if(wall==0||c.Actions is <128 or >2000||c.Queries is <64 or >1536||c.WallSeconds<30||c.WallSeconds>wall||c.TraceBytes is <1048576 or >536870912)throw new NativeActionRejectedException("Invalid full-run limits");
 }
 internal static void Check(int dispatched,bool query=false){
  if(!Active)return;CheckCounts(Configuration!,dispatched,Queries+ProbeQueries,Clock.Elapsed.TotalSeconds,query);
  if(query)Queries++;
 }
 internal static void AdmitProbe(int dispatched){CheckCounts(Configuration!,dispatched,Queries+ProbeQueries,Clock.Elapsed.TotalSeconds,true);ProbeQueries++;NativeEvidence.Emit("solver-probe-admitted",new{ordinal=ProbeQueries});}
 internal static void CheckCounts(Limits c,int actions,int queries,double seconds,bool query){
  if(actions>=c.Actions||seconds>=c.WallSeconds||query&&queries>=c.Queries)throw new TimeoutException("Full-run global admission budget reached");
 }
 internal static double RemainingActionSeconds=>Active?Math.Max(0.01,Math.Min(45,Configuration!.WallSeconds-Clock.Elapsed.TotalSeconds)):45;
 internal static object Snapshot()=>new{configuration=Configuration,queries=Queries,probeQueries=ProbeQueries,totalQueryAttempts=Queries+ProbeQueries,elapsedSeconds=Clock.Elapsed.TotalSeconds,scope="all root dispatches and all solver queries including frozen prefix/probes; cooperative admission, external watchdog separate"};
}

internal static partial class Program
{
 static int controlledAct=-1;
 static bool controlledBossComplete;
 static readonly List<object> controlledActs=[];
 static void FullActEnter(){
  if(!NativeFullRun.Active)return;
  int next=run!.CurrentActIndex;
  if(next!=controlledAct+1||controlledAct>=0&&!controlledBossComplete||next>=3||run.Acts.Count!=3)throw new NativeDataException("Full-run act order or original act count");
  controlledAct=next;controlledBossComplete=false;
  Observe("full-act-enter",new{act=next,map=RunManager.Instance.MapSelectionSynchronizer.MapGenerationCount});
  controlledActs.Add(new{stage="entered",act=next,confirmed,dispatched});
 }
 static void FullActBossComplete(CombatRoom room){
  if(!NativeFullRun.Active)return;
  if(controlledAct!=run!.CurrentActIndex||controlledBossComplete||!ReferenceEquals(room,run.CurrentRoom)||!ReferenceEquals(room,NativeHost.LastVictoriousRoom)||player!.Creature.CurrentHp<=0||room.Encounter.RoomType!=RoomType.Boss||!roomComplete)throw new NativeDataException("Full-run Boss completion requires original victory/rewards and completed room");
  controlledBossComplete=true;
  Observe("full-act-boss-complete",new{act=controlledAct,encounter=room.Encounter.Id.ToString(),room=NativeIdentity.Ref(room)});
  controlledActs.Add(new{stage="boss-rewards-complete",act=controlledAct,confirmed,dispatched});
 }
 static void FullRunFinalAct(){
  if(!NativeFullRun.Active||controlledAct!=2||run!.CurrentRoom is not MapRoom)throw new NativeDataException("Full-run final act requires actual second Boss transition");
  ThirdActFirstBattle();if(terminal==NativeRunTerminal.Death)return;
  ThirdActNodes();if(terminal==NativeRunTerminal.Death)return;
  if(!controlledBossComplete)throw new NativeDataException("Third Boss did not complete");
  ActualBossReady();
  CompleteArchitect(fullRun:true);
 }
 static void FullRunReport()=>Write("full-run-progress.json",new{schema="native-full-run-progress/v1",terminal=terminal.ToString(),confirmed,dispatched,lastConfirmed=last,controlledAct,controlledBossComplete,acts=controlledActs,budget=NativeFullRun.Snapshot(),finalState=State(),claim=terminal==NativeRunTerminal.RunVictory?"actual native completed run":"incomplete run; no full-victory claim"});
}
