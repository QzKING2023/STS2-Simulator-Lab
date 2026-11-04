using System.Text.Json;
using System.Text.Json.Nodes;
namespace OfflineSearchHarness;
// Full-run terminal contract; legacy finite Major/Tail validators remain scoped to their scenarios.
internal static class NativeFullRunTerminal
{
 internal static void Review(IReadOnlyList<JsonElement> rows,JsonElement report,JsonElement final){
  var result=report.GetProperty("result");
  if(result.GetProperty("Terminal").GetString()=="Death"){ReviewDeath(rows.ToArray(),report,final);return;}
  if(result.GetProperty("Terminal").GetString()!="RunVictory")return;
  void Need(bool v,string why)=>NativeBoundaryContract.Require(v,"Full run victory: "+why);
  string K(JsonElement e)=>e.GetProperty("kind").GetString()!;
  JsonElement S(JsonElement e)=>e.GetProperty("detail").GetProperty("state");
  var kinds=new[]{"run-win-start","run-end-start","run-end-return","run-win-completed","terminal-stamp","terminal-contract","tail-contract"};
  foreach(var k in kinds)Need(rows.Count(e=>K(e)==k)==1,"required unique "+k);
  var start=rows.Single(e=>K(e)=="run-win-start");string root=start.GetProperty("root").GetString()!,ctx=start.GetProperty("context").GetString()!;
  var selected=rows.Where(e=>e.GetProperty("root").GetString()==root&& (kinds.Contains(K(e))||K(e) is "dispatch" or "confirmed")).ToArray();
  Need(selected.Select(K).SequenceEqual(new[]{"dispatch","run-win-start","run-end-start","run-end-return","run-win-completed","confirmed","terminal-stamp","terminal-contract","tail-contract"}),"ordered actual final root lifecycle");
  Need(selected.All(e=>e.GetProperty("context").GetString()==ctx)&&rows.Where(e=>K(e)=="dispatch").Last().GetProperty("root").GetString()==root,"final dispatched action/context");
  var action=selected[0].GetProperty("detail").GetProperty("facts");var before=S(selected[0]);
  Need(action.GetProperty("kind").GetString()=="ArchitectOption"&&action.GetProperty("decision").GetProperty("model").GetString()=="EVENT.THE_ARCHITECT"&&before.GetProperty("hp").GetInt32()>0&&before.GetProperty("act").GetInt32()==2,"actual living Architect action");
  var option=before.GetProperty("rooms").GetProperty("eventOptions")[action.GetProperty("decision").GetProperty("index").GetInt32()];
  Need(option.GetProperty("TextKey").GetString()==action.GetProperty("decision").GetProperty("TextKey").GetString()&&!option.GetProperty("IsLocked").GetBoolean(),"actual native option");
  var n=JsonNode.Parse(before.GetRawText())!;
  for(int i=1;i<selected.Length;i++){
   var native=n["lifecycle"]!["native"]!;string kind=K(selected[i]);
   switch(kind){
    case "run-win-start":native["WinStarts"]=1;break;
    case "run-end-start":native["RunEndStarts"]=1;break;
    case "run-end-return":native["RunEndReturns"]=1;native["Won"]=true;break;
    case "run-win-completed":native["WinReturns"]=1;n["hp"]=0;break;
    case "confirmed":n["lifecycle"]!["eventModel"]!["IsFinished"]=true;n["rooms"]!["eventOptions"]=new JsonArray();break;
    case "terminal-contract":native["Closed"]=true;break;
   }
   NativeBoundaryContract.Snapshot(JsonSerializer.SerializeToElement(n),S(selected[i]),"Full run native terminal state chain "+kind);
  }
  var end=S(selected[^1]);NativeBoundaryContract.Snapshot(end,final,"Full run terminal final state");var life=end.GetProperty("lifecycle").GetProperty("native");
  Need(life.GetProperty("Losses").GetInt32()==0&&life.GetProperty("ActReturns").GetInt32()==3&&life.GetProperty("Won").GetBoolean()&&result.GetProperty("Stop").GetString()=="Completed"&&result.GetProperty("DataComplete").GetBoolean()&&result.GetProperty("Confirmed").GetInt32()==result.GetProperty("Dispatched").GetInt32()&&report.GetProperty("rawExit").GetInt32()==0,"actual completed run outcome");
  Need(selected[6].GetProperty("detail").GetProperty("facts").GetProperty("terminal").GetString()=="RunVictory","terminal stamp matches callback");
 }
 internal static void ReviewDeath(JsonElement[] rows,JsonElement report,JsonElement final){
  void Need(bool ok,string why)=>NativeBoundaryContract.Require(ok,"Full run death: "+why);
  string K(JsonElement e)=>e.GetProperty("kind").GetString()!;
  JsonElement S(JsonElement e)=>e.GetProperty("detail").GetProperty("state");
  JsonElement F(JsonElement e)=>e.GetProperty("detail").GetProperty("facts");
  var t=rows.Single(e=>K(e)=="solver-terminal-captured"&&F(e).GetProperty("Outcome").GetString()=="Defeat");
  string root=t.GetProperty("root").GetString()!,ctx=t.GetProperty("context").GetString()!;
  var dispatch=rows.Last(e=>K(e)=="dispatch");var confirmed=rows.Last(e=>K(e)=="confirmed");
  Need(dispatch.GetProperty("root").GetString()==root&&dispatch.GetProperty("context").GetString()==ctx&&Array.IndexOf(rows,dispatch)<Array.IndexOf(rows,t),"final actual lethal action");
  NativeSolverBattleContract.BindNativeTerminal(rows,t,confirmed);
  Need(F(t).GetProperty("Root").GetString()==root&&F(t).GetProperty("Context").GetString()==ctx&&S(t).GetProperty("hp").GetInt32()==0,"dead actual player and captured owner");
  NativeBoundaryContract.Snapshot(F(t).GetProperty("State"),S(t),"Full run death captured complete state");
  var v=rows.Single(e=>K(e)=="solver-terminal-verified"&&F(e).GetProperty("outcome").GetString()=="Defeat");
  Need(v.GetProperty("root").GetString()==root&&v.GetProperty("context").GetString()==ctx&&Array.IndexOf(rows,confirmed)<Array.IndexOf(rows,v)&&F(v).GetProperty("query").GetInt32()==F(t).GetProperty("Query").GetInt32()&&F(v).GetProperty("continuation").GetString()==F(t).GetProperty("Continuation").GetString(),"verified exact terminal ownership and sequence");
  NativeBoundaryContract.Snapshot(F(v).GetProperty("preCleanup"),S(t),"Full run death verified original callback");
  var life=final.GetProperty("lifecycle").GetProperty("native");var result=report.GetProperty("result");
  Need(final.GetProperty("hp").GetInt32()==0&&life.GetProperty("Losses").GetInt32()==1&&!life.GetProperty("Won").GetBoolean()&&result.GetProperty("Stop").GetString()=="Completed"&&result.GetProperty("DataComplete").GetBoolean()&&result.GetProperty("Confirmed").GetInt32()==result.GetProperty("Dispatched").GetInt32()&&report.GetProperty("rawExit").GetInt32()==0,"actual complete Death, not budget/protection/failure");
 }
}
