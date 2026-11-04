using System.Text.Json;
namespace OfflineSearchHarness;
internal static class NativeGeneratedChoiceEvidence
{
 static string S(JsonElement e,string k)=>e.GetProperty(k).GetString()!;
 internal static void Check(JsonElement[] rows,JsonElement step,JsonElement query){
  void R(bool b,string why)=>NativeBoundaryContract.Require(b,"Generated choice evidence: "+why);
  string root=S(step,"root"),context=S(step,"context");var a=step.GetProperty("action");var plan=a.GetProperty("Choice");bool generated=S(plan,"Effect")=="GenerateToHand";string leaf=generated?"FromChooseACardScreen":S(plan,"SourcePile")=="Hand"?"FromHand":"FromCombatPile";
  R(S(a,"Kind")=="UsePotion"&&(generated?S(plan,"SourcePile")=="None":(S(plan,"Effect"),S(plan,"SourcePile")) is ("MoveToHand","Draw") or ("MoveToHandFreeThisTurn","Discard") or ("Exhaust","Hand") or ("DiscardAndDraw","Hand") or ("SetFreeThisCombat","Hand"))&&S(plan,"Timing")=="Action"&&S(plan,"SourceId")==S(a,"PotionId")&&S(plan,"ContextId")=="","primary potion capability");
  var d=rows.Single(x=>S(x,"kind")=="dispatch"&&S(x,"root")==root);var c=rows.Single(x=>S(x,"kind")=="confirmed"&&S(x,"root")==root);
  NativeBoundaryContract.Snapshot(step.GetProperty("nativeBefore"),d.GetProperty("detail").GetProperty("state"),"Generated before");NativeBoundaryContract.Snapshot(step.GetProperty("nativeAfter"),c.GetProperty("detail").GetProperty("state"),"Generated after");
  R(S(step,"expected")==S(step,"actual")&&step.GetProperty("exact").GetBoolean()&&step.GetProperty("parentUnchanged").GetBoolean(),"full continuation/RNG and parent");
  NativeSolverActionContract.Bind(a,query,d.GetProperty("detail").GetProperty("facts").GetProperty("decision"),true,true);
  var children=step.GetProperty("children");R(children.GetArrayLength()==1,"one required primary choice");var child=children[0];string request=S(child,"request");
  var req=rows.Single(x=>S(x,"kind")=="choice-requested"&&S(x,"root")==root);var resolved=rows.Single(x=>S(x,"kind")=="choice-resolved"&&S(x.GetProperty("detail"),"id")==request);var returned=rows.Single(x=>S(x,"kind")=="choice-returned"&&S(x.GetProperty("detail"),"id")==request);
  R(new[]{d,c,req,resolved,returned}.All(x=>S(x,"root")==root&&S(x,"context")==context)&&Array.IndexOf(rows,d)<Array.IndexOf(rows,req)&&Array.IndexOf(rows,req)<Array.IndexOf(rows,resolved)&&Array.IndexOf(rows,resolved)<Array.IndexOf(rows,returned)&&Array.IndexOf(rows,returned)<Array.IndexOf(rows,c),"owned complete ordered request/return");
  R(S(req.GetProperty("detail"),"id")==request&&S(req.GetProperty("detail"),"source")==leaf&&S(child,"leaf")==leaf&&S(child,"source")==S(a,"PotionId")&&S(child,"root")==root&&S(child,"context")==context&&NativeBoundaryContract.Equal(child.GetProperty("choice"),plan),"actual plan/transport");
  var candidates=child.GetProperty("candidates").EnumerateArray().ToArray();var oracle=req.GetProperty("detail").GetProperty("oracleOptions");var visible=req.GetProperty("detail").GetProperty("options");
  var identities=rows.Where(x=>S(x,"kind")=="identity").ToDictionary(x=>S(x.GetProperty("detail"),"id"),x=>x.GetProperty("detail"));
  R(candidates.Length==oracle.GetArrayLength()&&candidates.Length==visible.GetArrayLength(),"full native candidate set");
  for(int i=0;i<candidates.Length;i++){var v=candidates[i];R(S(oracle[i].GetProperty("representative"),"$ref")==S(v,"id")&&S(identities[S(v,"id")],"model")==S(v,"model")&&S(visible[i],"model")==S(v,"model")&&visible[i].GetProperty("upgrade").GetInt32()==v.GetProperty("upgrade").GetInt32(),"native generated instance/model/upgrade");}
  var tokens=plan.GetProperty("Cards");int count=tokens.GetArrayLength();R(count>=req.GetProperty("detail").GetProperty("min").GetInt32()&&count<=req.GetProperty("detail").GetProperty("max").GetInt32()&&child.GetProperty("indices").GetArrayLength()==count&&child.GetProperty("selected").GetArrayLength()==count&&resolved.GetProperty("detail").GetProperty("selected").GetArrayLength()==count&&returned.GetProperty("detail").GetProperty("objects").GetArrayLength()==count,"exact selected cardinality");
  var sourcePile=generated?candidates:step.GetProperty("nativeBefore").GetProperty("combat").GetProperty(S(plan,"SourcePile").ToLowerInvariant()).EnumerateArray().ToArray();
  if(!generated)foreach(var v in candidates){var actual=sourcePile.Single(x=>S(x,"id")==S(v,"id"));R(S(actual,"model")==S(v,"model")&&actual.GetProperty("upgrade").GetInt32()==v.GetProperty("upgrade").GetInt32(),"actual source pile instance/model/upgrade");}
  R(child.GetProperty("indices").EnumerateArray().Select(x=>x.GetInt32()).Distinct().Count()==count,"no duplicated instance");
  for(int k=0;k<count;k++){var t=tokens[k];int n=child.GetProperty("indices")[k].GetInt32();R(n>=0&&n<candidates.Length,"actual candidate index");var v=candidates[n];
   int occurrence=candidates.Take(n).Count(x=>S(x,"model")==S(v,"model")&&x.GetProperty("upgrade").GetInt32()==v.GetProperty("upgrade").GetInt32());
   R(S(v,"model")=="CARD."+S(t,"CardId")&&v.GetProperty("upgrade").GetInt32()==t.GetProperty("UpgradeLevel").GetInt32()&&S(v,"key")==S(t,"StateKey")&&sourcePile.TakeWhile(x=>S(x,"id")!=S(v,"id")).Count(x=>S(x,"model")==S(v,"model")&&x.GetProperty("upgrade").GetInt32()==v.GetProperty("upgrade").GetInt32())==t.GetProperty("SourceOccurrence").GetInt32()&&occurrence==t.GetProperty("OptionOccurrence").GetInt32()&&child.GetProperty("selected")[k].GetString()==S(v,"id")&&resolved.GetProperty("detail").GetProperty("selected")[k].GetString()=="o"+n&&S(returned.GetProperty("detail").GetProperty("objects")[k],"$ref")==S(v,"id"),"exact upstream token and actual native result");
  }
 }
 internal static void Review(string directory,JsonElement[] rows){
  foreach(string f in Directory.GetFiles(directory,"solver-online-step-*.json")){
   var step=JsonDocument.Parse(File.ReadAllText(f)).RootElement;var a=step.GetProperty("action");
   if(S(a,"Kind")!="UsePotion"||a.GetProperty("Choice").ValueKind==JsonValueKind.Null)continue;
   Check(rows,step,JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"solver-query-"+step.GetProperty("query").GetInt32()+".json"))).RootElement);
  }
 }
}
