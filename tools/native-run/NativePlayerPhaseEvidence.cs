using System.Text.Json;
namespace OfflineSearchHarness;
internal static class NativePlayerPhaseEvidence {
 static string S(JsonElement e,string k)=>e.GetProperty(k).GetString()!;
 internal static int Child(JsonElement[] rows,JsonElement plan,JsonElement child,JsonElement dispatch,JsonElement confirmed,int prior){
  void R(bool b,string why)=>NativeBoundaryContract.Require(b,"Player phase evidence: "+why);
  string root=S(dispatch,"root"),context=S(dispatch,"context"),id=S(child,"request");
  var request=rows.Single(x=>S(x,"kind")=="choice-requested"&&S(x.GetProperty("detail"),"id")==id);
  var resolved=rows.Single(x=>S(x,"kind")=="choice-resolved"&&S(x.GetProperty("detail"),"id")==id);
  var returned=rows.Single(x=>S(x,"kind")=="choice-returned"&&S(x.GetProperty("detail"),"id")==id);
  var observed=rows.Single(x=>S(x,"kind")=="solver-phase-choice"&&S(x.GetProperty("detail").GetProperty("facts"),"request")==id);
  var origin=child.GetProperty("phase");
  var order=new[]{request,observed,resolved,returned};R(order.All(x=>S(x,"root")==root&&S(x,"context")==context)&&prior<Array.IndexOf(rows,request)&&Array.IndexOf(rows,returned)<Array.IndexOf(rows,confirmed)&&order.Zip(order.Skip(1)).All(x=>Array.IndexOf(rows,x.First)<Array.IndexOf(rows,x.Second)),"actual request/return ordered owner");
  R(S(child,"root")==root&&S(child,"context")==context&&S(child,"leaf")=="FromCombatPile"&&S(request.GetProperty("detail"),"source")=="FromCombatPile"&&NativeBoundaryContract.Equal(child.GetProperty("choice"),plan)&&NativeBoundaryContract.Equal(observed.GetProperty("detail").GetProperty("facts").GetProperty("phase"),origin),"plan and source metadata binding");
  var middle=observed.GetProperty("detail").GetProperty("state");var power=middle.GetProperty("powers").EnumerateArray().Single(x=>S(x,"id")==S(origin,"source"));
  R(S(origin,"hook")=="AfterShuffle"&&S(origin,"model")=="POWER.STRATAGEM_POWER"&&S(power,"model")==S(origin,"model")&&S(origin,"sourceId")==S(plan,"SourceId")&&origin.GetProperty("amount").GetInt32()==power.GetProperty("amount").GetInt32()&&S(middle.GetProperty("combat"),"side")=="Player","actual player power/hook/amount");
  R(origin.GetProperty("stackVerified").GetBoolean()&&origin.GetProperty("methodToken").GetInt32()>0,"actual original async hook stack observed");
  var candidates=child.GetProperty("candidates").EnumerateArray().ToArray();var draw=middle.GetProperty("combat").GetProperty("draw").EnumerateArray().ToArray();var req=request.GetProperty("detail");
  R(candidates.Length==draw.Length&&req.GetProperty("options").GetArrayLength()==draw.Length,"complete original draw candidates");
  for(int i=0;i<candidates.Length;i++){var actual=draw.Single(x=>S(x,"id")==S(candidates[i],"id"));R(S(actual,"model")==S(candidates[i],"model")&&actual.GetProperty("upgrade").GetInt32()==candidates[i].GetProperty("upgrade").GetInt32()&&S(req.GetProperty("oracleOptions")[i].GetProperty("representative"),"$ref")==S(actual,"id"),"actual native sorted candidates and source draw instances");}
  var tokens=plan.GetProperty("Cards");int n=tokens.GetArrayLength();R(n>=req.GetProperty("min").GetInt32()&&n<=req.GetProperty("max").GetInt32()&&new[]{child.GetProperty("indices"),child.GetProperty("selected"),resolved.GetProperty("detail").GetProperty("selected"),returned.GetProperty("detail").GetProperty("objects")}.All(x=>x.GetArrayLength()==n),"actual complete selection count");
  var used=new HashSet<int>();for(int i=0;i<n;i++){var t=tokens[i];int index=child.GetProperty("indices")[i].GetInt32();R(index>=0&&index<candidates.Length&&used.Add(index),"unique legal index");var c=candidates[index];int occurrence=candidates.Take(index).Count(x=>S(x,"key")==S(c,"key"));R(S(c,"key")==S(t,"StateKey")&&S(c,"model")=="CARD."+S(t,"CardId")&&c.GetProperty("upgrade").GetInt32()==t.GetProperty("UpgradeLevel").GetInt32()&&occurrence==t.GetProperty("SourceOccurrence").GetInt32()&&occurrence==t.GetProperty("OptionOccurrence").GetInt32()&&child.GetProperty("selected")[i].GetString()==S(c,"id")&&resolved.GetProperty("detail").GetProperty("selected")[i].GetString()=="o"+index&&S(returned.GetProperty("detail").GetProperty("objects")[i],"$ref")==S(c,"id"),"full token and real selected instance");}
  return Array.IndexOf(rows,returned);
 }
}
