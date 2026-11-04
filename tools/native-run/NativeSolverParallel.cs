#if SOLVER_GATE
using System.Text.Json;
using CombatSolver;
using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;
using SimulatorLab.Core;
namespace OfflineSearchHarness;
internal static partial class Program
{
 static bool parallelProbeDone;
 static void SolverSettingsAudit(){
  string before=JsonSerializer.Serialize(SolverSettings.Current,Json);
  var presets=new[]{SolverPerformancePreset.Low,SolverPerformancePreset.Medium,SolverPerformancePreset.High,SolverPerformancePreset.VeryHigh}.Select(p=>new{name=p.ToString(),profile=SolverSettings.ResolvePerformanceValues(SolverSettings.ApplyPerformancePreset(SolverSettings.Current,p)).Profile}).ToArray();
  Write("solver-settings-audit.json",new{source="pinned upstream SolverSettings.ApplyPerformancePreset/ResolvePerformanceValues",settingsIo=false,unchanged=before==JsonSerializer.Serialize(SolverSettings.Current,Json),logicalProcessors=Environment.ProcessorCount,presets,baseSettings=SolverSettings.Capture(),effectiveCoordinator=new{preset="LabBounded custom, not one of the four UI presets",beam=8,nodes=256,softMilliseconds=10000,dop=NativeSolverCoordinator.Configuration?.SearchParallelism??1,cancellationSeconds=15,returnedAggregateNodeLimit=NativeSolverCoordinator.ReturnLimit,executionPolicy=NativeSolverCoordinator.Configuration?.ExecutionPolicy??"Guarded",stopOnPredictedDeath=NativeSolverCoordinator.StopOnDeath}});
 }
 static void ParallelSettingsProbe(CombatRootSnapshot root,CombatState combat,SearchPolicySnapshot policy,SolverDisplayNames names,BattleDamageSnapshot damage){
  if(!CoordinatorTail||NativeSolverCoordinator.Configuration?.ParallelProbe!=true||parallelProbeDone)return;
  var records=new List<object>();string before=State().GetRawText(),live=ContinuationStamp.CaptureLive(combat).StateText;
  var parent=root.ForkSimulator();string Stamp(CombatPredictionSimulator sim)=>ContinuationStamp.CapturePredicted(player!,sim,((SimulatedCombatState)sim.State.CombatState).GetPlayerTurnNumber(player!),root.Forecast,root.StartTurnNumber).StateText;
  string parentBefore=Stamp(parent);bool completed=false;
  try{
   foreach(int dop in new[]{1,2}){
    NativeFullRun.AdmitProbe(dispatched);var chosen=policy with{MaxDegreeOfParallelism=dop};
    using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(15));var task=Task.Run(()=>CombatSearchCoordinator.Solve(root,names,damage,chosen,cancel.Token,null));
    try{Await(task,"same-root upstream parallel probe");}catch{cancel.Cancel();throw;}
    var result=task.GetAwaiter().GetResult();
    if(result.BoundaryReason is not (SearchBoundaryReason.None or SearchBoundaryReason.NodeLimit)||result.BestNode.Actions.Count==0||result.TotalExpandedNodes>4096||result.Snapshot.HasRisk||result.UnmirroredDetails().Any()||result.Forecast.HasUnsupportedIntent)throw new NotSupportedException("Parallel probe returned incomplete/unsupported result outside registered admission");
    var action=result.BestNode.Actions[0];NativeSolverActionContract.Fields(JsonSerializer.SerializeToElement(action,Json),choices:true,potions:true,enchantments:true,phaseChoices:true);
    bool legal=action.Kind==PlanActionKind.EndTurn;
    if(action.Kind==PlanActionKind.PlayCard){var card=ResolveSolverCard(action,true,true);legal=card.CanPlayTargeting(ResolveSolverTarget(action,combat));}
    if(action.Kind==PlanActionKind.UsePotion){var potion=player!.GetPotionAtSlotIndex(action.PotionSlot);legal=potion!=null&&potion.Id.Entry==action.PotionId&&potion.PassesCustomUsabilityCheck;}
    bool pure=before==State().GetRawText()&&live==ContinuationStamp.CaptureLive(combat).StateText&&parentBefore==Stamp(parent);
    records.Add(new{dop,chosen.Profile,boundary=result.BoundaryReason.ToString(),result.TotalExpandedNodes,result.ExpandedNodes,elapsedMilliseconds=result.TotalSearchElapsed.TotalMilliseconds,result.ParallelExpansionWaves,result.ParallelExpansionWorkItems,result.MaxParallelExpansionConcurrency,work=result.SearchWorkAttributions,forecastExact=result.Forecast.IsExactForModeledDamage,approximations=result.Forecast.ApproximationDetails,firstAction=action,legal,sourceUnchanged=pure});
    if(!legal||!pure)throw new NativeDataException("Parallel probe illegal candidate or changed parent/native/RNG");
   }
   completed=true;parallelProbeDone=true;
  }finally{Write("solver-parallel-probe.json",new{scope="same immutable root; comparison only; actual adopted action is independently shadow/native checked",information="Oracle",root=NativeEvidence.Root,context=NativeEvidence.Context,continuation=live,before=JsonDocument.Parse(before).RootElement.Clone(),after=State(),records,completed});}
 }
}
#endif
