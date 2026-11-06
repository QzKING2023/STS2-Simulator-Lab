using System.Text.Json;
using System.Text.Json.Serialization;
using SimulatorLab.Core;
namespace OfflineSearchHarness;

// A declared immutable replay prefix, followed by bounded generic online decisions.
// No encounter, card, or scoring special cases.
internal static class NativeSolverCoordinator
{
 internal const string Policy="coordinator-one-step/v1";
 internal static bool IsScenario(string s)=>NativePerformancePilot.IsScenario(s)||NativeDarv.IsScenario(s)||s is "solver-native-coordinator" or "solver-native-coordinator-replay";
 internal static bool Active=>IsScenario(NativeLifecycle.Scenario);
 internal static bool Switched;
 internal sealed record Plan(string Schema,int PrefixCount,int MaxActions,bool Probe,string InputSha,string ChoiceSha,string RewardSha,NativeFullRun.Limits? Run=null,int SearchParallelism=1,bool ParallelProbe=false,string ExecutionPolicy="Guarded",int ReturnedAggregateNodeLimit=4096,string Character="Ironclad",string Pilot="",bool SearchStudy=false,string EventChoiceProfile="default");
 internal static Plan? Configuration;
 internal static object? Profile=>Configuration;
 internal static bool StopOnDeath=>Configuration?.ExecutionPolicy!="RunToTerminal";
 internal static int ReturnLimit=>Configuration?.ReturnedAggregateNodeLimit??4096;
 internal static bool Guarded(JsonElement cfg)=>!cfg.TryGetProperty("ExecutionPolicy",out var p)||p.GetString()=="Guarded";
 internal static int Limit(JsonElement cfg)=>cfg.TryGetProperty("ReturnedAggregateNodeLimit",out var p)?p.GetInt32():4096;
 internal static void BindSettings(JsonElement query,JsonElement cfg){
  string policy=cfg.TryGetProperty("ExecutionPolicy",out var p)?p.GetString()!:"Guarded";
  int cap=Limit(cfg);
  if(policy is not ("Guarded" or "RunToTerminal")||cap is not (4096 or 16384))throw new NativeDataException("Unknown Coordinator execution policy/budget");
  if(query.TryGetProperty("executionPolicy",out var actual)){
   if(actual.GetString()!=policy||query.GetProperty("stopOnPredictedDeath").GetBoolean()!=Guarded(cfg))throw new NativeDataException("Coordinator declared execution policy mismatch");
  }else if(policy!="Guarded"||cap!=4096)throw new NativeDataException("Missing Coordinator execution policy evidence");
  if(query.GetProperty("budget").GetProperty("returnedAggregateNodeLimit").GetInt32()!=cap)throw new NativeDataException("Coordinator declared aggregate limit mismatch");
 }
 internal static void Configure(string path,string? replay,string scenario){
  if(!IsScenario(scenario))return;
  if(replay==null||path=="")throw new NativeActionRejectedException("Coordinator requires declared exact prefix input and plan");
  Configuration=JsonSerializer.Deserialize<Plan>(File.ReadAllText(path),new JsonSerializerOptions{UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow})??throw new NativeActionRejectedException("Missing Coordinator plan");
  var c=Configuration;NativePerformancePilot.Validate(c,scenario);NativeFullRun.Validate(c.Run);if(c.Run!=null&&!NativeDarv.IsActTwoProfile(scenario))throw new NativeActionRejectedException("Full run requires composed Darv profile");
  if(c.ExecutionPolicy is not ("Guarded" or "RunToTerminal")||c.ReturnedAggregateNodeLimit is not (4096 or 16384)||c.ExecutionPolicy=="RunToTerminal"&&(c.Run==null||c.Probe))throw new NativeActionRejectedException("Unsupported declared execution policy/budget");
  if(c.EventChoiceProfile is not ("default" or "amalgamator-strikes" or "field-resist"))throw new NativeActionRejectedException("Unsupported event choice profile");
  if(c.SearchParallelism is <1 or >2||c.SearchParallelism>Environment.ProcessorCount||c.Run==null&&(c.SearchParallelism!=1||c.ParallelProbe))throw new NativeActionRejectedException("Unsupported declared search parallelism/probe");string dir=Path.GetDirectoryName(replay)!;
  if(c.Schema!="solver-coordinator-prefix/v1"||c.PrefixCount<0||(c.MaxActions<1||c.MaxActions>(c.Run==null?128:2000))||scenario.EndsWith("-replay")&&c.Probe||NativeBranchSearch.Sha(replay)!=c.InputSha||NativeBranchSearch.Sha(Path.Combine(dir,"choices.json"))!=c.ChoiceSha||NativeBranchSearch.Sha(Path.Combine(dir,"reward-choices.json"))!=c.RewardSha)throw new NativeActionRejectedException("Coordinator prefix schema/budget/input binding");
 }
 internal static string? Rejection(JsonElement q,bool stopOnDeath=true){
  var c=q.GetProperty("candidate");
  if(c.GetProperty("policy").GetString()!=Policy)return "unknown-candidate-policy";
  if(q.GetProperty("boundary").GetString() is not ("None" or "NodeLimit"))return "unsupported-search-boundary";
  if(c.GetProperty("resultScope").GetString()!="SearchCompletion"||c.GetProperty("snapshotBoundary").GetString()!="None")return "unsupported-result-scope-or-snapshot";
  if(c.GetProperty("hasRisk").GetBoolean()||c.GetProperty("unmirrored").GetArrayLength()!=0||c.GetProperty("forecastUnsupported").GetBoolean())return "unmodeled-or-approximate";
  bool staticApprox=!c.GetProperty("forecastExact").GetBoolean()||c.GetProperty("approximations").GetArrayLength()!=0;
  if(staticApprox&&!NativeSolverBranchRuntime.AllowsConditionalPreview(q))return "unmodeled-or-approximate";
  var prediction=q.GetProperty("prediction");int turn=prediction.GetProperty("StartTurnNumber").GetInt32();
  if(turn!=q.GetProperty("nativeState").GetProperty("combat").GetProperty("turn").GetInt32())return "wrong-start-turn";
  var death=prediction.GetProperty("DeathTurn");bool dead=c.GetProperty("playerDead").GetBoolean();
  if(dead&&(death.ValueKind!=JsonValueKind.Number||death.GetInt32()<turn||prediction.GetProperty("outcome").GetString()!="Defeat"))return "unbound-predicted-death";
  if(!dead&&(death.ValueKind!=JsonValueKind.Null||prediction.GetProperty("outcome").GetString()=="Defeat"))return "contradictory-predicted-death";
  if(stopOnDeath&&dead&&death.GetInt32()==turn)return "predicted-current-turn-death";
  var actions=q.GetProperty("actions");
  if(actions.ValueKind!=JsonValueKind.Array||actions.GetArrayLength()==0)return "no-candidate";
  if(actions[0].GetProperty("Turn").GetInt32()!=turn)return "candidate-not-current-turn";
  return null;
 }
 internal static void Budget(JsonElement q,int expectedDop=1,int returnLimit=4096){
  void Need(bool ok,string why){if(!ok)throw new NativeDataException("Coordinator budget: "+why);}
  Need(q.GetProperty("engine").GetString()=="CombatSearchCoordinator"&&q.GetProperty("FixedBudget").GetBoolean(),"registered engine and fixed request");
  var p=q.GetProperty("Profile");var b=q.GetProperty("budget");long total=q.GetProperty("TotalExpandedNodes").GetInt64();
  Need(expectedDop is 1 or 2&&p.GetProperty("BeamWidth").GetInt32()==8&&p.GetProperty("MaxExpandedNodes").GetInt32()==256&&p.GetProperty("SoftTimeBudgetMilliseconds").GetInt32()==10000&&b.GetProperty("dop").GetInt32()==expectedDop&&b.GetProperty("cancelSeconds").GetInt32()==15&&q.GetProperty("BudgetOverrideMilliseconds").GetInt32()==10000,"effective upstream-derived profile and declared parallelism");
  Need(q.GetProperty("ExpandedNodes").GetInt64()>=0&&total>=q.GetProperty("ExpandedNodes").GetInt64(),"selected versus aggregate work");
  var work=q.GetProperty("work");long sum=0;
  foreach(var member in work.GetProperty("attributions").EnumerateArray()){long n=member.GetProperty("ExpandedNodes").GetInt64();Need(n>=0,"nonnegative member work");sum+=n;}
  Need(sum==total,"actual request work attribution sum");
  Need(returnLimit is 4096 or 16384&&b.GetProperty("returnedAggregateNodeLimit").GetInt32()==returnLimit,"return admission cap, not hard circuit breaker");
  if(total>returnLimit)throw new TimeoutException("Coordinator aggregate return admission exceeded");
 }
}
internal static partial class Program
{
 static void CoordinatorAtNewRun(){
  if(!NativeSolverCoordinator.Active||NativeSolverCoordinator.Configuration!.PrefixCount!=0)return;
  if(sequence!=0||replayIndex!=0)throw new NativeDataException("Zero prefix must start before any native root action");
  if(!scenario.EndsWith("-replay")){
   if(replay.Length!=0)throw new NativeActionRejectedException("Zero prefix live requires empty route");
   NativeChoices.AssertConsumed();NativeRewards.AssertConsumed();replayPath=null;NativeChoices.Replaying=false;
  }
  NativeSolverCoordinator.Switched=true;
  Observe("solver-coordinator-switch",new{prefix=0,mode=scenario.EndsWith("-replay")?"frozen-tail-replay":"zero-prefix/live",maxActions=NativeSolverCoordinator.Configuration.MaxActions});
 }
 static bool CoordinatorTail=>NativeSolverCoordinator.Active&&NativeSolverCoordinator.Switched;
 static void CoordinatorBeforeQuery(){
  if(!NativeSolverCoordinator.Active||NativeSolverCoordinator.Switched)return;
  var c=NativeSolverCoordinator.Configuration!;
  if(sequence<c.PrefixCount)return;
  if(sequence!=c.PrefixCount||replayIndex!=c.PrefixCount)throw new NativeDataException("Coordinator exact prefix boundary");
  if(!scenario.EndsWith("-replay")){
   NativeChoices.AssertConsumed();NativeRewards.AssertConsumed();replayPath=null;NativeChoices.Replaying=false;
  }
  NativeSolverCoordinator.Switched=true;
  Observe("solver-coordinator-switch",new{prefix=c.PrefixCount,mode=scenario.EndsWith("-replay")?"frozen-tail-replay":"exact-prefix/live-tail",maxActions=c.MaxActions});
 }
}
