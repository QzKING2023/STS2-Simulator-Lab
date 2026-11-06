using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Runs;
using SimulatorLab.Core;
namespace OfflineSearchHarness;
internal static class NativeVisibleBattle
{
 internal static bool IsScenario(string s)=>s is "visible-battle" or "visible-battle-replay";
 internal static void Review(string directory){
  var rows=File.ReadLines(Path.Combine(directory,"trace.jsonl")).Select(l=>JsonDocument.Parse(l).RootElement.GetProperty("payload").Clone()).ToArray();
  JsonElement Read(string file)=>JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,file))).RootElement;
  var steps=Directory.GetFiles(directory,"visible-execution-*.json").Select(p=>JsonDocument.Parse(File.ReadAllText(p)).RootElement).OrderBy(x=>x.GetProperty("n").GetInt32()).ToArray();
  var finish=Read("visible-complete.json");
  if(steps.Length==0||steps.Length!=finish.GetProperty("steps").GetInt32()||Directory.GetFiles(directory,"solver-query-*.json").Length!=0||Directory.GetFiles(directory,"solver-online-step-*.json").Length!=0)throw new NativeDataException("Visible loop missing steps or used solver");
  for(int i=0;i<steps.Length;i++)Check(rows,steps[i],Read("visible-boundary-"+(i+1)+".json"),i+1);
  string outcome=finish.GetProperty("outcome").GetString()!;
  if(outcome is not ("BattleVictory" or "Death")||!finish.GetProperty("noSolverQueries").GetBoolean()||finish.GetProperty("wholeRun").GetBoolean()||finish.GetProperty("information").GetString()!="PlayerVisible"||finish.GetProperty("diagnosticInformation").GetString()!="Oracle")throw new NativeDataException("Visible completion scope");
  string kind=outcome=="BattleVictory"?"battle-victory-before-cleanup":"battle-loss-confirmed";
  if(rows.Count(r=>r.GetProperty("kind").GetString()==kind)!=1||finish.GetProperty("root").GetString()!=steps[^1].GetProperty("root").GetString())throw new NativeDataException("Visible native terminal absent");
 }
 internal static void Check(JsonElement[] rows,JsonElement step,JsonElement probe,int n){
  void R(bool b,string why){if(!b)throw new NativeDataException("Visible execution: "+why);}
  string S(JsonElement e,string k)=>e.GetProperty(k).GetString()!;
  bool Eq(JsonElement a,JsonElement b)=>NativeBoundaryContract.Equal(a,b);
  R(step.GetProperty("n").GetInt32()==n&&S(step,"information")=="PlayerVisible"&&S(step,"diagnosticInformation")=="Oracle","sequence/information scope");
  string root=S(step,"root");var dispatch=rows.Single(r=>S(r,"kind")=="dispatch"&&S(r,"root")==root);var confirm=rows.Single(r=>S(r,"kind")=="confirmed"&&S(r,"root")==root);
  R(Array.IndexOf(rows,dispatch)<Array.IndexOf(rows,confirm)&&S(step,"context")==S(dispatch,"context")&&S(step,"context")==S(confirm,"context"),"action context/order");
  var before=step.GetProperty("before");var after=step.GetProperty("after");
  R(Eq(before,dispatch.GetProperty("detail").GetProperty("state"))&&Eq(after,confirm.GetProperty("detail").GetProperty("state")),"full native before/after binding");
  var wire=S(step,"wire");var o=JsonSerializer.Deserialize<VisibleCombatObservation>(wire,VisibleCombatPolicy.Json)!;
  R(wire==VisibleCombatPolicy.Encode(VisibleCombatPolicy.Project(before,o.LegalActions))&&wire==S(probe,"wire")&&S(step,"token")==S(probe,"decision")&&S(step,"token")==VisibleCombatPolicy.Decide(wire),"whitelist and pure decision");
  R(probe.GetProperty("deployed").GetBoolean()&&probe.GetProperty("sourceUnchanged").GetBoolean()&&probe.GetProperty("zeroDispatch").GetBoolean()&&probe.GetProperty("unknownFieldsRejected").GetInt32()==5&&probe.GetProperty("tests").GetArrayLength()==5&&probe.GetProperty("tests").EnumerateArray().All(t=>t.GetProperty("passed").GetBoolean()),"projection controls and purity");
  var choice=o.LegalActions.Single(x=>x.Token==S(step,"token"));var actual=dispatch.GetProperty("detail").GetProperty("facts");
  R(S(actual,"kind")==choice.Kind&&Eq(step.GetProperty("decision"),actual.GetProperty("decision"))&&Eq(JsonSerializer.SerializeToElement(choice,Program.Json),step.GetProperty("selected")),"actual dispatch binding");
  if(choice.Kind=="PlayCard"){
   var card=before.GetProperty("combat").GetProperty("hand")[choice.HandSlot];var decision=step.GetProperty("decision");
   R(S(card,"id")==S(step,"card")&&S(card,"id")==S(decision,"card")&&S(card,"model")==S(decision,"model")&&S(card,"model")==o.Hand[choice.HandSlot].Model&&card.GetProperty("upgrade").GetInt32()==o.Hand[choice.HandSlot].Upgrade,"actual public card slot");
   if(choice.EnemySlot<0)R(step.GetProperty("target").ValueKind==JsonValueKind.Null&&decision.GetProperty("target").ValueKind==JsonValueKind.Null,"untargeted binding");
   else{var enemy=before.GetProperty("combat").GetProperty("enemies")[choice.EnemySlot];R(S(enemy,"id")==S(step,"target")&&S(enemy,"id")==S(decision,"target"),"actual public enemy slot");}
  }else R(step.GetProperty("decision").GetProperty("turn").GetInt32()==o.Turn,"actual turn");
 }

}
internal static partial class Program
{
 static void VisibleBattle()
 {
#if SOLVER_GATE
  // Diagnostics may see oracle state. The pure policy receives only the serialized
  // whitelist; neither a game reference nor a solver action enters its signature.
  var combat=CombatManager.Instance.DebugOnlyGetState()!;
  int wins=NativeHost.BattleVictories,losses=NativeLifecycle.Losses;
  for(int n=1;n<=64;n++){
   Ready();var before=State();int old=dispatched;
   var (wire,token)=VisiblePolicyProbe(combat,n,deployed:true);
   var observation=JsonSerializer.Deserialize<VisibleCombatObservation>(wire,VisibleCombatPolicy.Json)!;
   var selected=observation.LegalActions.Single(x=>x.Token==token);
   string? cardId=null,targetId=null;object decision;
   if(selected.Kind=="PlayCard"){
    var card=player!.PlayerCombatState!.Hand.Cards[selected.HandSlot];
    Creature? target=selected.EnemySlot<0?null:combat.Enemies[selected.EnemySlot];
    if(!card.CanPlayTargeting(target)||card.Id.ToString()!=observation.Hand[selected.HandSlot].Model||card.CurrentUpgradeLevel!=observation.Hand[selected.HandSlot].Upgrade)throw new NativeActionRejectedException("Visible slot no longer legal/current");
    cardId=Id(card);targetId=target==null?null:Id(target);
    decision=new{card=cardId,model=card.Id.ToString(),target=targetId};
    Action("PlayCard",decision,()=>{if(!card.TryManualPlay(target))throw new NativeActionRejectedException("Visible native play rejected");Await(RunManager.Instance.ActionExecutor.FinishedExecutingActions(),"visible native PlayCard");});
   }else{
    int turn=player!.PlayerCombatState!.TurnNumber;decision=new{turn};
    Action("EndTurn",decision,()=>{CombatManager.Instance.OnEndedTurnLocally();RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new EndPlayerTurnAction(player,turn));Await(RunManager.Instance.ActionExecutor.FinishedExecutingActions(),"visible native turn");});
   }
   if(dispatched!=old+1)throw new NativeDataException("Visible action dispatch count");
   Write("visible-execution-"+n+".json",new{schema="visible-execution/v1",n,information="PlayerVisible",diagnosticInformation="Oracle",scope="trusted common-subset policy, native Ironclad first battle only",wire,token,selected,decision,card=cardId,target=targetId,root=NativeEvidence.Root,context=NativeEvidence.Context,before,after=State(),confirmed,dispatched});
   string boundary=WaitBoundary();
   if(boundary=="Play")continue;
   if(boundary=="Death"){terminal=NativeRunTerminal.Death;if(NativeLifecycle.Losses!=losses+1)throw new NativeDataException("Visible death callback missing");}
   else if(boundary!="BattleVictory"||NativeHost.BattleVictories!=wins+1||player!.Creature.CurrentHp<=0)throw new NativeDataException("Visible terminal callback mismatch");
   NativeLifecycle.Closed=true;
   Write("visible-complete.json",new{schema="visible-completion/v1",steps=n,outcome=boundary,policy="first-legal-visible/v1",information="PlayerVisible",diagnosticInformation="Oracle",wholeRun=false,noSolverQueries=true,noHiddenPolicyInputs=true,root=NativeEvidence.Root,context=NativeEvidence.Context,state=State()});return;
  }
  throw new TimeoutException("Visible first-battle action budget 64");
#else
  throw new NotSupportedException("Registered diagnostic build required");
#endif
 }
}
