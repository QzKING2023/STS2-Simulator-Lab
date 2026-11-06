using System.Diagnostics;
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
 static void SearchStudy(CombatRootSnapshot root,CombatState combat,SearchPolicySnapshot baseline,SolverDisplayNames names,BattleDamageSnapshot damage,int query)
 {
  if(NativeSolverCoordinator.Configuration?.SearchStudy!=true||query is not (1 or 6))return;
  using var measured=NativeTiming.Measure("search-study-wall");
  string before=State().GetRawText(),live=ContinuationStamp.CaptureLive(combat).StateText;
  var parent=root.ForkSimulator();
  string Parent()=>ContinuationStamp.CapturePredicted(player!,parent,((SimulatedCombatState)parent.State.CombatState).GetPlayerTurnNumber(player!),root.Forecast,root.StartTurnNumber).StateText;
  string parentBefore=Parent();var records=new List<object>();var clock=Stopwatch.StartNew();bool complete=false;
  try{
   VisiblePolicyProbe(combat,query);
   var low=SolverSettings.ResolvePerformanceValues(SolverSettings.ApplyPerformancePreset(SolverSettings.Current,SolverPerformancePreset.Low)).Profile;
   // Reverse second repetition to expose order/warm-up effects instead of hiding them.
   var cells=new[]{("LabBounded",1),("LabBounded",2),("UpstreamLow",1),("UpstreamLow",2)};
   for(int repeat=1;repeat<=2;repeat++)foreach(var (profileName,dop) in repeat==1?cells:cells.Reverse()){
    if(clock.Elapsed.TotalSeconds>=75)throw new TimeoutException("Per-root study wall admission budget");
    var profile=profileName=="LabBounded"?baseline.Profile:low with{StopPortfolioAtHpTarget=false};
    var policy=baseline with{Profile=profile,MaxDegreeOfParallelism=dop,FixedBudget=true,BudgetOverrideMilliseconds=profile.SoftTimeBudgetMilliseconds,PrimaryIncumbents=null};
    using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var process=Process.GetCurrentProcess();double cpu=process.TotalProcessorTime.TotalMilliseconds;long allocated=GC.GetTotalAllocatedBytes(false);int g0=GC.CollectionCount(0),g1=GC.CollectionCount(1),g2=GC.CollectionCount(2);
    var timer=Stopwatch.StartNew();SolverResult? result=null;string? error=null;
    try{var task=Task.Run(()=>CombatSearchCoordinator.Solve(root,names,damage,policy,cancel.Token,null));try{Await(task,"bounded same-root search study");}catch{cancel.Cancel();throw;}result=task.GetAwaiter().GetResult();}
    catch(OperationCanceledException ex){error=ex.GetType().Name;}
    bool pure=before==State().GetRawText()&&live==ContinuationStamp.CaptureLive(combat).StateText&&parentBefore==Parent();
    bool legal=false;
    if(result?.BestNode.Actions.Count>0){
     var a=result.BestNode.Actions[0];
     NativeSolverActionContract.Fields(JsonSerializer.SerializeToElement(a,Json),choices:true,potions:true,enchantments:true,phaseChoices:true);
     legal=a.Kind==PlanActionKind.EndTurn;
     if(a.Kind==PlanActionKind.PlayCard)legal=ResolveSolverCard(a,true,true).CanPlayTargeting(ResolveSolverTarget(a,combat));
     if(a.Kind==PlanActionKind.UsePotion){var p=player!.GetPotionAtSlotIndex(a.PotionSlot);legal=p!=null&&p.Id.Entry==a.PotionId&&p.PassesCustomUsabilityCheck;}
    }
    process.Refresh();
    records.Add(new{profileName,dop,repeat,requestedProfile=profile,cancellationSeconds=10,wallMilliseconds=timer.Elapsed.TotalMilliseconds,cpuMilliseconds=process.TotalProcessorTime.TotalMilliseconds-cpu,allocatedBytes=GC.GetTotalAllocatedBytes(false)-allocated,gen0=GC.CollectionCount(0)-g0,gen1=GC.CollectionCount(1)-g1,gen2=GC.CollectionCount(2)-g2,error,sourceUnchanged=pure,legalFirst=legal,result=result==null?null:new{boundary=result.BoundaryReason.ToString(),result.TotalExpandedNodes,result.ExpandedNodes,result.MaxParallelExpansionConcurrency,result.ParallelExpansionWorkItems,elapsedMilliseconds=result.TotalSearchElapsed.TotalMilliseconds,work=result.SearchWorkAttributions,actions=result.BestNode.Actions,actionHash=Hash(JsonSerializer.Serialize(result.BestNode.Actions,Json)),result.StartTurnNumber,result.DeathTurn,result.CombatEndedTurn,quality=new{result.Snapshot.PlayerHp,result.Snapshot.ProjectedPlayerHp,result.Snapshot.PlayerDead,result.Snapshot.AllEnemiesDead,result.Snapshot.HasRisk,unmirrored=result.UnmirroredDetails()},forecastExact=result.Forecast.IsExactForModeledDamage}});
    Write("search-study-"+query+".json",new{query,root=NativeEvidence.Root,context=NativeEvidence.Context,information="Oracle",continuation=live,nativeState=State(),records,complete=false,scope="same immutable root; no candidate deployment; 10s cooperative cancellation is separate from requested preset",commonPolicy="no HP early stop, no portfolio/novelty, Smart potions; existing scoring unchanged"});
    if(!pure)throw new NativeDataException("Search study mutated root/native/RNG");
   }
   complete=true;
  }finally{
   Write("search-study-"+query+".json",new{query,root=NativeEvidence.Root,context=NativeEvidence.Context,information="Oracle",continuation=live,nativeState=State(),records,complete,elapsedMilliseconds=clock.Elapsed.TotalMilliseconds,scope="same immutable root; predicted candidate quality only, no alternative native rollout; cancellation not pristine preset completion",commonPolicy="existing score, HP early stop/portfolio/novelty disabled consistently; declared preset budget shape"});
  }
 }
#endif
}
