using System.Text.Json;
namespace OfflineSearchHarness;
internal static class NativeSolverCoordinatorEvidence
{
 internal static void Completed(string directory,JsonElement[] rows,int actions){
  void Need(bool ok,string why){if(!ok)throw new NativeDataException("Coordinator completed battle prefix: "+why);}
  string K(JsonElement e)=>e.GetProperty("kind").GetString()!;
  JsonElement S(JsonElement e)=>e.GetProperty("detail").GetProperty("state");
  JsonElement F(JsonElement e)=>e.GetProperty("detail").GetProperty("facts");
  int P(JsonElement e)=>Array.IndexOf(rows,e);
  JsonElement Read(string name)=>JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,name))).RootElement;
  bool Same(JsonElement a,JsonElement b)=>NativeBoundaryContract.Equal(a,b);
  var cfg=rows.Single(e=>K(e)=="header").GetProperty("detail").GetProperty("coordinatorProfile");
  int prefix=cfg.GetProperty("PrefixCount").GetInt32();var sw=rows.Single(e=>K(e)=="solver-coordinator-switch");var owner=rows.Take(P(sw)).Last(e=>K(e)==(prefix==0?"new-run":"confirmed"));
  Need(!cfg.GetProperty("Probe").GetBoolean()&&owner.GetProperty("root").GetString()==(prefix==0?"boot":"a"+prefix)&&F(sw).GetProperty("prefix").GetInt32()==prefix&&Same(S(owner),S(sw))&&sw.GetProperty("root").GetString()==owner.GetProperty("root").GetString()&&sw.GetProperty("context").GetString()==owner.GetProperty("context").GetString(),"immutable declared switch");
  var qs=rows.Skip(P(sw)).Where(e=>K(e)=="solver-query-complete").ToArray();Need(qs.Length>0&&rows.Skip(P(sw)).Count(e=>K(e)=="dispatch")<=cfg.GetProperty("MaxActions").GetInt32(),"bounded nonempty actual online suffix");
  foreach(var qe in qs){
   int index=F(qe).GetProperty("query").GetInt32();var q=Read($"solver-query-{index}.json");var admission=Read($"solver-admission-{index}.json");
   NativeSolverCoordinator.BindSettings(q,cfg);NativeSolverCoordinator.Budget(q,cfg.TryGetProperty("SearchParallelism",out var declaredDop)?declaredDop.GetInt32():1,NativeSolverCoordinator.Limit(cfg));Need(NativeSolverCoordinator.Rejection(q,NativeSolverCoordinator.Guarded(cfg))==null&&admission.GetProperty("query").GetInt32()==index&&admission.GetProperty("policy").GetString()==NativeSolverCoordinator.Policy&&admission.GetProperty("decision").GetString()=="eligible-for-shadow"&&admission.GetProperty("reason").ValueKind==JsonValueKind.Null,"actual bounded query admission");
  }
  Need(Directory.GetFiles(directory,"solver-query-*.json").Length==actions&&Directory.GetFiles(directory,"solver-online-step-*.json").Length==actions,"all executed query/step files; no unadopted suffix");
  Need(Same(Read("final-state.json"),S(rows.Last(e=>K(e)=="confirmed"))),"immutable last confirmed actual next-act state");
 }
 internal static void Tail(string directory,JsonElement[] rows,JsonElement entry,int battles,int priorActions){
  void Need(bool ok,string why){if(!ok)throw new NativeDataException("Coordinator prefix: "+why);}
  string K(JsonElement e)=>e.GetProperty("kind").GetString()!;
  JsonElement F(JsonElement e)=>e.GetProperty("detail").GetProperty("facts");
  JsonElement S(JsonElement e)=>e.GetProperty("detail").GetProperty("state");
  int P(JsonElement e)=>Array.IndexOf(rows,e);
  JsonElement Read(string name)=>JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,name))).RootElement;
  bool Same(JsonElement a,JsonElement b)=>NativeBoundaryContract.Equal(a,b);
  var header=rows.Single(e=>K(e)=="header");var cfg=header.GetProperty("detail").GetProperty("coordinatorProfile");int prefix=cfg.GetProperty("PrefixCount").GetInt32();
  var sw=rows.Single(e=>K(e)=="solver-coordinator-switch");var switchOwner=rows.Take(P(sw)).Last(e=>K(e)==(prefix==0?"new-run":"confirmed"));
  Need(F(sw).GetProperty("prefix").GetInt32()==prefix&&switchOwner.GetProperty("root").GetString()==(prefix==0?"boot":"a"+prefix)&&Same(S(sw),S(switchOwner))&&sw.GetProperty("root").GetString()==switchOwner.GetProperty("root").GetString()&&sw.GetProperty("context").GetString()==switchOwner.GetProperty("context").GetString(),"exact immutable prefix switch");
  var tail=rows.Skip(P(entry)).ToArray();var steps=Directory.GetFiles(directory,"solver-online-step-*.json").Select(f=>JsonDocument.Parse(File.ReadAllText(f)).RootElement.Clone()).Where(q=>q.GetProperty("query").GetInt32()>battles*64).OrderBy(q=>q.GetProperty("query").GetInt32()).ToArray();
  Need(steps.Length<=cfg.GetProperty("MaxActions").GetInt32(),"tail work limit");
  if(steps.Length>0){
   var last=tail.Last(e=>K(e)=="confirmed");var owner=rows.Take(P(entry)).Last(e=>K(e)=="confirmed");
   var segment=new[]{header,owner}.Concat(rows.Skip(P(entry)+1).Take(P(last)-P(entry))).ToArray();
   NativeSolverBattleContract.ReviewBattle(directory,segment,battles*64,closed:false,partial:true);
  }
  var queries=tail.Where(e=>K(e)=="solver-query-complete").ToArray();
  Need(queries.Length==steps.Length||queries.Length==steps.Length+1,"complete executed or unadopted query set");
  foreach(var qe in queries){
   int index=F(qe).GetProperty("query").GetInt32();var q=Read($"solver-query-{index}.json");var owner=rows.Take(P(qe)).Last(e=>K(e)=="confirmed");
   Need(Same(q.GetProperty("nativeState"),S(qe))&&Same(S(qe),S(owner))&&qe.GetProperty("root").GetString()==owner.GetProperty("root").GetString()&&qe.GetProperty("context").GetString()==owner.GetProperty("context").GetString()&&q.GetProperty("sourceUnchanged").GetBoolean(),"query confirmed full state/owner/purity");
   var cap=tail.Single(e=>K(e)=="solver-root-pure"&&F(e).GetProperty("role").GetString()=="query"&&F(e).GetProperty("query").GetInt32()==index);
   string rootHash=NativeEvidence.Hash(q.GetProperty("root").GetString()!);
   Need(P(sw)<P(cap)&&P(cap)<P(qe)&&F(cap).GetProperty("continuation").GetString()==rootHash&&F(qe).GetProperty("root").GetString()==rootHash&&Same(S(cap),S(owner))&&cap.GetProperty("root").GetString()==owner.GetProperty("root").GetString()&&cap.GetProperty("context").GetString()==owner.GetProperty("context").GetString(),"query capture/root/order/owner");
   NativeSolverCoordinator.BindSettings(q,cfg);NativeSolverCoordinator.Budget(q,cfg.TryGetProperty("SearchParallelism",out var declaredDop)?declaredDop.GetInt32():1,NativeSolverCoordinator.Limit(cfg));string? rejection=NativeSolverCoordinator.Rejection(q,NativeSolverCoordinator.Guarded(cfg));var admission=Read($"solver-admission-{index}.json");
   Need(admission.GetProperty("query").GetInt32()==index&&admission.GetProperty("policy").GetString()==NativeSolverCoordinator.Policy&&admission.GetProperty("decision").GetString()==(rejection==null?"eligible-for-shadow":"rejected")&&admission.GetProperty("reason").GetString()==rejection,"actual admission decision");
  }
  var report=Read("result.json").GetProperty("result");string error=report.GetProperty("Error").GetString()!;
  bool probe=cfg.GetProperty("Probe").GetBoolean();
  if(probe)Need(steps.Length==0&&queries.Length==1&&error.Contains("Coordinator query probe completed without native dispatch"),"probe stop, zero new dispatch");
  else if(queries.Length==steps.Length)Need(steps.Length==cfg.GetProperty("MaxActions").GetInt32()&&error.Contains("Coordinator online tail action budget reached")&&report.GetProperty("Stop").GetString()=="BudgetExceeded","real action-budget stop");
  else{
   int index=F(queries.Last()).GetProperty("query").GetInt32();var q=Read($"solver-query-{index}.json");string? rejection=NativeSolverCoordinator.Rejection(q,NativeSolverCoordinator.Guarded(cfg));
   if(rejection!=null)Need(error.Contains("Coordinator candidate not adopted: "+rejection),"actual unadopted reason");
   else if(error.Contains("Unsupported solver execution field")){
    bool refused=false;try{NativeSolverActionContract.Fields(q.GetProperty("actions")[0],choices:true,potions:true);}catch(NotSupportedException ex){refused=ex.Message=="Unsupported solver execution field";}
    Need(refused&&!File.Exists(Path.Combine(directory,$"solver-online-step-{index}.json"))&&!File.Exists(Path.Combine(directory,$"solver-current-shadow-{index}.json"))&&!tail.Any(e=>K(e)=="solver-root-pure"&&F(e).GetProperty("role").GetString()=="shadow"&&F(e).GetProperty("query").GetInt32()==index),"actual execution protocol refused before shadow/native dispatch");
   }
   else{var sh=Read($"solver-current-shadow-{index}.json");bool lethal=sh.GetProperty("lethal").GetBoolean();if(File.Exists(Path.Combine(directory,$"solver-endturn-risk-{index}.json")))lethal|=Read($"solver-endturn-risk-{index}.json").GetProperty("risk").GetProperty("PlayerDead").GetBoolean();Need(lethal&&error.Contains("lethal protection"),"actual immediate lethal stop");}
  }
  var finalOwner=rows.Last(e=>K(e)=="confirmed");Need(Same(Read("final-state.json"),S(finalOwner)),"last confirmed final state");
  Need(Directory.GetFiles(directory,"solver-query-*.json").Length==priorActions+queries.Length&&Directory.GetFiles(directory,"solver-online-step-*.json").Length==priorActions+steps.Length,"all actual query/step files");
 }
}
