using System.Text.Json;
namespace OfflineSearchHarness;
// Opt-in run-to-terminal policy certifies deployed steps, not future forecast truth.
internal static class NativeTerminalPolicyEvidence {
 internal static void Review(string directory,JsonElement[] rows,JsonElement cfg){
  if(NativeSolverCoordinator.Guarded(cfg))return;
  void Need(bool ok,string why)=>NativeBoundaryContract.Require(ok,"Terminal policy: "+why);
  string K(JsonElement e)=>e.GetProperty("kind").GetString()!;
  JsonElement S(JsonElement e)=>e.GetProperty("detail").GetProperty("state");
  foreach(var path in Directory.GetFiles(directory,"solver-online-step-*.json")){
   var s=JsonDocument.Parse(File.ReadAllText(path)).RootElement;int idx=s.GetProperty("query").GetInt32();
   var q=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,$"solver-query-{idx}.json"))).RootElement;
   var a=s.GetProperty("action");string root=s.GetProperty("root").GetString()!;
   var d=rows.Single(e=>K(e)=="dispatch"&&e.GetProperty("root").GetString()==root);
   var c=rows.Single(e=>K(e)=="confirmed"&&e.GetProperty("root").GetString()==root);
   NativeSolverCoordinator.BindSettings(q,cfg);
   Need(NativeSolverCoordinator.Rejection(q,false)==null&&q.GetProperty("sourceUnchanged").GetBoolean(),"eligible actual search candidate without source mutation");
   Need(NativeBoundaryContract.Equal(a,q.GetProperty("actions")[0])&&s.GetProperty("context").GetString()==d.GetProperty("context").GetString()&&Array.IndexOf(rows,d)<Array.IndexOf(rows,c),"unmodified selected action/owner/order");
   NativeSolverActionContract.Bind(a,q,d.GetProperty("detail").GetProperty("facts").GetProperty("decision"),choices:true,potions:true);
   NativeSolverActionContract.BindReport(s,d.GetProperty("detail").GetProperty("facts").GetProperty("decision"));
   NativeBoundaryContract.Snapshot(s.GetProperty("nativeBefore"),S(d),"Terminal policy actual dispatch state");
   NativeBoundaryContract.Snapshot(s.GetProperty("nativeAfter"),S(c),"Terminal policy actual confirmed state");
   foreach(var flag in new[]{"exact","parentUnchanged","consumedRejected","staleRejected","unconsumedAtRejection","zeroAdditionalDispatch"})Need(s.GetProperty(flag).GetBoolean(),"step "+flag);
   Need(s.GetProperty("expected").GetString()==s.GetProperty("actual").GetString(),"complete continuation/RNG differential");
  }
 }
}
