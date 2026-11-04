using System.Text.Json;
namespace OfflineSearchHarness;
internal static class NativeTerminalChoiceEvidence {
 static string T(JsonElement e,string k)=>e.GetProperty(k).GetString()!;
 static void R(bool b,string m)=>NativeBoundaryContract.Require(b,"Terminal implicit choice: "+m);
 internal static void Check(JsonElement[] rows,JsonElement step){
  var marked=step.GetProperty("children").EnumerateArray().Where(x=>x.TryGetProperty("suppressedAtTerminal",out var flag)&&flag.GetBoolean()).ToArray();if(marked.Length==0)return;
  string root=T(step,"root"),context=T(step,"context");var action=step.GetProperty("action");
  R(T(action,"Kind")=="PlayCard"&&marked.Length==1&&action.GetProperty("NestedChoices").ValueKind==JsonValueKind.Null&&step.GetProperty("exact").GetBoolean()&&step.GetProperty("parentUnchanged").GetBoolean()&&T(step,"expected")==T(step,"actual"),"actual complete action and exact terminal differential");
  var d=rows.Single(x=>T(x,"root")==root&&T(x,"kind")=="dispatch");var c=rows.Single(x=>T(x,"root")==root&&T(x,"kind")=="confirmed");NativeBoundaryContract.Snapshot(step.GetProperty("nativeBefore"),d.GetProperty("detail").GetProperty("state"),"Suppressed choice before");NativeBoundaryContract.Snapshot(step.GetProperty("nativeAfter"),c.GetProperty("detail").GetProperty("state"),"Suppressed choice after");
  foreach(var child in marked){string id=T(child,"request");var request=rows.Single(x=>T(x,"kind")=="choice-requested"&&T(x.GetProperty("detail"),"id")==id);var observed=rows.Single(x=>T(x,"kind")=="solver-terminal-choice"&&T(x.GetProperty("detail").GetProperty("facts"),"request")==id);var resolved=rows.Single(x=>T(x,"kind")=="choice-resolved"&&T(x.GetProperty("detail"),"id")==id);var returned=rows.Single(x=>T(x,"kind")=="choice-returned"&&T(x.GetProperty("detail"),"id")==id);
   var order=new[]{d,request,observed,resolved,returned,c};R(order.All(x=>T(x,"root")==root&&T(x,"context")==context)&&order.Zip(order.Skip(1)).All(p=>Array.IndexOf(rows,p.First)<Array.IndexOf(rows,p.Second)),"ordered actual implicit request and original return");
   var req=request.GetProperty("detail");var facts=observed.GetProperty("detail").GetProperty("facts");var state=observed.GetProperty("detail").GetProperty("state");
   R(T(child,"source")==T(action,"CardId")&&T(child,"leaf")=="FromCombatPile"&&T(req,"source")=="FromCombatPile"&&req.GetProperty("implicitChoice").GetBoolean()&&req.GetProperty("options").GetArrayLength()==0&&req.GetProperty("oracleOptions").GetArrayLength()==0&&req.GetProperty("min").GetInt32()==0&&facts.GetProperty("ending").GetBoolean()&&NativeBoundaryContract.Equal(facts.GetProperty("choice"),action.GetProperty("Choice"))&&NativeBoundaryContract.Equal(child.GetProperty("choice"),action.GetProperty("Choice")),"original ending gate and cancelled plan");
   R(new[]{child.GetProperty("indices"),child.GetProperty("selected"),child.GetProperty("candidates"),resolved.GetProperty("detail").GetProperty("selected"),returned.GetProperty("detail").GetProperty("objects")}.All(x=>x.GetArrayLength()==0),"no invented choice or moved instance");
   R(state.GetProperty("hp").GetInt32()>0&&state.GetProperty("combat").GetProperty("enemies").EnumerateArray().All(x=>!x.GetProperty("primary").GetBoolean()||!x.GetProperty("alive").GetBoolean()),"actual surviving native victory boundary");
   var victory=rows.Single(x=>T(x,"root")==root&&T(x,"kind")=="battle-victory-before-cleanup");R(T(victory,"context")==context&&Array.IndexOf(rows,victory)<Array.IndexOf(rows,c),"original victory callback required");
  }
 }
 internal static int Review(string directory,JsonElement[] rows){int count=0;foreach(string file in Directory.GetFiles(directory,"solver-online-step-*.json")){var step=JsonDocument.Parse(File.ReadAllText(file)).RootElement;if(step.GetProperty("children").EnumerateArray().Any(x=>x.TryGetProperty("suppressedAtTerminal",out _))){Check(rows,step);count++;}}return count;}
}
