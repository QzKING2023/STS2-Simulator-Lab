using System.Text.Json;
using SimulatorLab.Core;
namespace OfflineSearchHarness;

// Narrow transport capability. Enemy effects/search remain upstream/native;
// unknown phase effects are refused rather than answered by the baseline policy.
internal static class NativeSolverPhaseProtocol
{
 internal const string Schema="endturn-native-choice/v2";
 internal static object? Capture(){
#if SOLVER_GATE
  return new{schema=Schema,solver=NativeSolverBranchRuntime.Capture(),resolver="NativeChoiceSession.ResolvePlannedCards",source="KnowledgeDemonChoiceSupport.SourceId; original StratagemPower.AfterShuffle",scope="EndTurn EnemyTurn curse or PlayerTurnStart Draw/MoveToHand under observed original shuffle hook; other phase effects unsupported"};
#else
  return null;
#endif
 }
 internal static bool Enabled(JsonElement q)=>q.TryGetProperty("phaseChoices",out var proof)&&proof.ValueKind==JsonValueKind.Object&&NativeBoundaryContract.Equal(proof,JsonSerializer.SerializeToElement(Capture(),Program.Json));
 internal static void Validate(JsonElement action){
  var choices=action.GetProperty("TurnStartChoices");
  if(action.GetProperty("Kind").GetString()!="EndTurn"||choices.ValueKind!=JsonValueKind.Array||choices.GetArrayLength() is <1 or >16)throw new NotSupportedException("Unsupported phase action/choice count");
  var sources=new HashSet<string>();
  foreach(var choice in choices.EnumerateArray()){
   if(choice.GetProperty("Timing").GetString()=="PlayerTurnStart"){
    if(choice.GetProperty("SourceId").GetString()!="STRATAGEM_POWER"||choice.GetProperty("Effect").GetString()!="MoveToHand"||choice.GetProperty("SourcePile").GetString()!="Draw"||choice.GetProperty("ContextId").GetString()!="")throw new NotSupportedException("Unsupported player phase provenance");
    foreach(var cardToken in choice.GetProperty("Cards").EnumerateArray())if(string.IsNullOrEmpty(cardToken.GetProperty("StateKey").GetString())||cardToken.GetProperty("SourceOccurrence").GetInt32()<0||cardToken.GetProperty("OptionOccurrence").GetInt32()<0)throw new NativeActionRejectedException("Missing player phase identity");
    continue;
   }
   if(choice.GetProperty("Effect").GetString()!="ApplyKnowledgeCurse"||choice.GetProperty("Timing").GetString()!="EnemyTurn"||choice.GetProperty("SourcePile").GetString()!="None"||choice.GetProperty("ContextId").GetString()!=""||string.IsNullOrEmpty(choice.GetProperty("SourceId").GetString())||!sources.Add(choice.GetProperty("SourceId").GetString()!)||choice.GetProperty("Cards").GetArrayLength()!=1)throw new NotSupportedException("Unsupported/duplicate phase provenance");
   var token=choice.GetProperty("Cards")[0];
   if(string.IsNullOrEmpty(token.GetProperty("CardId").GetString())||token.GetProperty("UpgradeLevel").GetInt32()!=0||token.GetProperty("StateKey").GetString()!=""||token.GetProperty("SourceOccurrence").GetInt32()!=0||token.GetProperty("OptionOccurrence").GetInt32()<0)throw new NativeActionRejectedException("Invalid enemy choice token");
  }
 }
 internal static void ReviewChildren(JsonElement[] rows,JsonElement step,JsonElement dispatch,JsonElement confirmed,JsonElement query){
  void Need(bool ok,string why)=>NativeBoundaryContract.Require(ok,"Enemy phase evidence: "+why);
  var action=step.GetProperty("action");Validate(action);Need(Enabled(query),"exact pinned capability");
  string root=dispatch.GetProperty("root").GetString()!,context=dispatch.GetProperty("context").GetString()!;
  Need(NativeBoundaryContract.Equal(action,query.GetProperty("actions")[0])&&step.GetProperty("root").GetString()==root&&step.GetProperty("context").GetString()==context,"actual selected action and root");
  Need(NativeBoundaryContract.Equal(step.GetProperty("nativeBefore"),dispatch.GetProperty("detail").GetProperty("state"))&&NativeBoundaryContract.Equal(step.GetProperty("nativeAfter"),confirmed.GetProperty("detail").GetProperty("state"))&&step.GetProperty("expected").GetString()==step.GetProperty("actual").GetString()&&step.GetProperty("exact").GetBoolean(),"actual complete states and continuation/RNG differential");
  NativeSolverActionContract.Bind(action,query,dispatch.GetProperty("detail").GetProperty("facts").GetProperty("decision"),choices:true,potions:true);
  var plans=action.GetProperty("TurnStartChoices").EnumerateArray().ToArray();var children=step.GetProperty("children").EnumerateArray().ToArray();
  var requested=rows.Where(p=>p.GetProperty("root").GetString()==root&&p.GetProperty("kind").GetString()=="choice-requested").ToArray();Need(plans.Length==children.Length&&requested.Length==plans.Length,"complete exact phase choice set");
  int prior=Array.IndexOf(rows,dispatch);
  var identities=rows.Where(p=>p.GetProperty("kind").GetString()=="identity").ToDictionary(p=>p.GetProperty("detail").GetProperty("id").GetString()!,p=>p.GetProperty("detail"));
  for(int i=0;i<plans.Length;i++){
   var plan=plans[i];var child=children[i];var origin=child.GetProperty("phase");
   if(plan.GetProperty("Timing").GetString()=="PlayerTurnStart"){prior=NativePlayerPhaseEvidence.Child(rows,plan,child,dispatch,confirmed,prior);continue;}
   Need(NativeBoundaryContract.Equal(plan,child.GetProperty("choice"))&&child.GetProperty("root").GetString()==root&&child.GetProperty("context").GetString()==context&&child.GetProperty("leaf").GetString()=="FromChooseACardScreen","current phase plan/owner/transport");
   string requestId=child.GetProperty("request").GetString()!;
   var observed=rows.Single(p=>p.GetProperty("kind").GetString()=="solver-phase-choice"&&p.GetProperty("detail").GetProperty("facts").GetProperty("request").GetString()==requestId);
   Need(observed.GetProperty("root").GetString()==root&&observed.GetProperty("context").GetString()==context&&NativeBoundaryContract.Equal(observed.GetProperty("detail").GetProperty("facts").GetProperty("phase"),origin)&&NativeBoundaryContract.Equal(observed.GetProperty("detail").GetProperty("facts").GetProperty("choice"),plan),"canonical current phase observation and complete metadata binding");
   var middle=observed.GetProperty("detail").GetProperty("state");NativeTraceContract.CheckState(middle);string source=origin.GetProperty("source").GetString()!;
   var enemy=middle.GetProperty("combat").GetProperty("enemies").EnumerateArray().Single(e=>e.GetProperty("id").GetString()==source);
   var binding=query.GetProperty("binding").GetProperty("creatures").EnumerateArray().Single(e=>e.GetProperty("id").GetString()==source);
   string actual=origin.GetProperty("model").GetString()!.Split('.')[1]+":"+origin.GetProperty("combatId").GetUInt32()+":"+origin.GetProperty("counter").GetInt32();
   Need(origin.GetProperty("counter").GetInt32()>=0&&origin.GetProperty("performing").GetBoolean()&&origin.GetProperty("side").GetString()=="Enemy"&&middle.GetProperty("combat").GetProperty("side").GetString()=="Enemy"&&origin.GetProperty("model").GetString()==enemy.GetProperty("model").GetString()&&origin.GetProperty("combatId").GetUInt32()==binding.GetProperty("combatId").GetUInt32()&&actual==origin.GetProperty("sourceId").GetString()&&actual==plan.GetProperty("SourceId").GetString(),"actual performing source/model/combat identity/counter");
   var before=step.GetProperty("nativeBefore");Need(middle.GetProperty("act").GetInt32()==before.GetProperty("act").GetInt32()&&middle.GetProperty("node").GetString()==before.GetProperty("node").GetString(),"same battle position");
   var req=requested[i];string id=req.GetProperty("detail").GetProperty("id").GetString()!;
   Need(id==child.GetProperty("request").GetString()&&req.GetProperty("context").GetString()==context&&req.GetProperty("detail").GetProperty("source").GetString()=="FromChooseACardScreen","actual native request identity");
   var resolved=rows.Single(p=>p.GetProperty("kind").GetString()=="choice-resolved"&&p.GetProperty("detail").GetProperty("id").GetString()==id);
   var returned=rows.Single(p=>p.GetProperty("kind").GetString()=="choice-returned"&&p.GetProperty("detail").GetProperty("id").GetString()==id);
   Need(new[]{resolved,returned}.All(e=>e.GetProperty("root").GetString()==root&&e.GetProperty("context").GetString()==context),"actual response/return owner");
   int ri=Array.IndexOf(rows,req),si=Array.IndexOf(rows,resolved),ti=Array.IndexOf(rows,returned);Need(prior<ri&&ri<Array.IndexOf(rows,observed)&&Array.IndexOf(rows,observed)<si&&si<ti&&ti<Array.IndexOf(rows,confirmed),"complete ordered phase request/resolve/return");prior=ti;
   var candidates=child.GetProperty("candidates").EnumerateArray().ToArray();var options=req.GetProperty("detail").GetProperty("options");var oracle=req.GetProperty("detail").GetProperty("oracleOptions");Need(candidates.Length==options.GetArrayLength()&&candidates.Length==oracle.GetArrayLength(),"complete actual candidate set");
   for(int n=0;n<candidates.Length;n++){string candidate=candidates[n].GetProperty("id").GetString()!;Need(oracle[n].GetProperty("representative").GetProperty("$ref").GetString()==candidate&&identities[candidate].GetProperty("model").GetString()==candidates[n].GetProperty("model").GetString()&&options[n].GetProperty("model").GetString()==candidates[n].GetProperty("model").GetString()&&options[n].GetProperty("upgrade").GetInt32()==candidates[n].GetProperty("upgrade").GetInt32(),"candidate actual registered instance/model/upgrade");}
   int selected=child.GetProperty("indices")[0].GetInt32();Need(selected>=0&&selected<candidates.Length&&child.GetProperty("indices").GetArrayLength()==1&&child.GetProperty("selected").GetArrayLength()==1,"one actual selection");var token=plan.GetProperty("Cards")[0];var chosen=candidates[selected];string selectedId=chosen.GetProperty("id").GetString()!;
   Need(chosen.GetProperty("model").GetString()=="CARD."+token.GetProperty("CardId").GetString()&&chosen.GetProperty("upgrade").GetInt32()==token.GetProperty("UpgradeLevel").GetInt32()&&candidates.Take(selected).Count(e=>e.GetProperty("model").GetString()==chosen.GetProperty("model").GetString()&&e.GetProperty("upgrade").GetInt32()==chosen.GetProperty("upgrade").GetInt32())==token.GetProperty("OptionOccurrence").GetInt32()&&child.GetProperty("selected")[0].GetString()==selectedId&&resolved.GetProperty("detail").GetProperty("selected")[0].GetString()=="o"+selected&&returned.GetProperty("detail").GetProperty("objects")[0].GetProperty("$ref").GetString()==selectedId,"upstream token occurrence and actual selected/returned instance");
  }
 }
 internal static int Review(string directory){
  var rows=File.ReadLines(Path.Combine(directory,"trace.jsonl")).Select(l=>JsonDocument.Parse(l).RootElement.GetProperty("payload").Clone()).ToArray();int count=0;
  foreach(string path in Directory.GetFiles(directory,"solver-online-step-*.json")){
   var step=JsonDocument.Parse(File.ReadAllText(path)).RootElement;var action=step.GetProperty("action");
   if(action.GetProperty("TurnStartChoices").ValueKind!=JsonValueKind.Array||action.GetProperty("TurnStartChoices").GetArrayLength()==0)continue;
   string root=step.GetProperty("root").GetString()!;var query=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"solver-query-"+step.GetProperty("query").GetInt32()+".json"))).RootElement;
   var dispatch=rows.Single(p=>p.GetProperty("root").GetString()==root&&p.GetProperty("kind").GetString()=="dispatch");var confirmed=rows.Single(p=>p.GetProperty("root").GetString()==root&&p.GetProperty("kind").GetString()=="confirmed");
   ReviewChildren(rows,step,dispatch,confirmed,query);count++;
  }
  return count;
 }
}
