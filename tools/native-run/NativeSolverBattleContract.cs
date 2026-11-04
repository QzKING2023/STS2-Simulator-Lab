using System.Text.Json;
using System.Text.Json.Nodes;
using SimulatorLab.Core;
namespace OfflineSearchHarness;
internal static class NativeSolverBattleContract
{
 static void Need(bool ok,string why){if(!ok)throw new NativeDataException("Whole solver battle evidence: "+why);}
 static bool Same(JsonElement a,JsonElement b)=>NativeDecisionTicket.SameActionJson(a.GetRawText(),b.GetRawText());
 internal static void BindNativeTerminal(JsonElement[] rows,JsonElement terminal,JsonElement confirmed)
 {
  string Kind(JsonElement r)=>r.GetProperty("kind").GetString()!;
  string Text(JsonElement r,string k)=>r.GetProperty(k).GetString()!;
  int Pos(JsonElement r)=>Array.IndexOf(rows,r);
  string outcome=terminal.GetProperty("detail").GetProperty("facts").GetProperty("Outcome").GetString()!;
  var callback=rows.Single(r=>Kind(r)==(outcome=="Victory"?"battle-victory-before-cleanup":"battle-loss-start"));
  void Owner(JsonElement r)=>Need(Text(r,"root")==Text(terminal,"root")&&Text(r,"context")==Text(terminal,"context"),"native terminal callback action/context");
  Owner(callback);Owner(confirmed);
  Need(Pos(terminal)<Pos(callback)&&Pos(callback)<Pos(confirmed),"native terminal callback order");
  Need(Same(callback.GetProperty("detail").GetProperty("state"),terminal.GetProperty("detail").GetProperty("state")),"native terminal callback immutable full state");
  if(outcome=="Defeat"){
   var loss=rows.Single(r=>Kind(r)=="battle-loss-confirmed");Owner(loss);
   Need(Pos(callback)<Pos(loss)&&Pos(loss)<Pos(confirmed),"native loss completion order");
  }
 }
 internal static void Review(string directory)
 {
  var rows=File.ReadLines(Path.Combine(directory,"trace.jsonl")).Select(x=>JsonDocument.Parse(x).RootElement.GetProperty("payload").Clone()).ToArray();
  ReviewBattle(directory,rows);
 }
 internal static void ReviewBattle(string directory,JsonElement[] rows,int queryOffset=0,bool closed=true,bool partial=false)
 {
  JsonElement Read(string n)=>JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,n))).RootElement;
  string K(JsonElement r)=>r.GetProperty("kind").GetString()!;JsonElement F(JsonElement r)=>r.GetProperty("detail").GetProperty("facts");JsonElement S(JsonElement r)=>r.GetProperty("detail").GetProperty("state");
  string T(JsonElement e,string p)=>e.GetProperty(p).GetString()!;int Pos(JsonElement e)=>Array.IndexOf(rows,e);
  var header=rows.Single(r=>K(r)=="header").GetProperty("detail");string scenario=T(header,"scenario");
  Need((NativeSolverBattle.IsScenario(scenario)||NativeSolverBattle.MultiScenario(scenario))&&T(header,"information")=="Oracle","registered Oracle whole-battle scenario");
  var profile=Read("solver-profile.json");Need(T(profile,"information")=="Oracle"&&T(profile,"solverVersion")=="0.50.1"&&!profile.GetProperty("nativeOnly").GetBoolean()&&!profile.GetProperty("entryInitializeCalled").GetBoolean()&&!profile.GetProperty("settingsLoadSaveCalled").GetBoolean()&&!profile.GetProperty("extraSearchPatches").GetBoolean(),"isolated pinned profile");
  Need(rows.Count(r=>K(r)=="fixture-setup-declared")== (scenario=="solver-battle-death"?1:0),"accurate fixture origin");
  var opening=rows.Single(r=>K(r)=="solver-battle-opening");var end=partial?default:rows.Single(r=>K(r)=="solver-battle-complete");var ef=partial?default:F(end);
  var ds=rows.Where(r=>K(r)=="dispatch"&&T(F(r),"kind") is "PlayCard" or "UsePotion" or "EndTurn").ToArray();
  var qs=rows.Where(r=>K(r)=="solver-query-complete").ToArray();var caps=rows.Where(r=>K(r)=="solver-root-pure").ToArray();
  Need(ds.Length is >0 and <=64&&qs.Length==ds.Length&&caps.Length==ds.Length*2,"complete bounded action/query/capture set");
  if(!NativeSolverBattle.MultiScenario(scenario))Need(Directory.GetFiles(directory,"solver-online-step-*.json").Length==ds.Length&&Directory.GetFiles(directory,"solver-query-*.json").Length==qs.Length,"no missing or surplus query/step files");
  var terminal=partial?default:rows.Single(r=>K(r)=="solver-terminal-captured");var tf=partial?default:F(terminal);var verified=partial?default:rows.Single(r=>K(r)=="solver-terminal-verified");var cleanup=partial?default:rows.Single(r=>K(r)=="solver-battle-cleanup");string outcome=partial?"partial":T(tf,"Outcome");
  Need(partial||outcome is "Victory" or "Defeat","known actual terminal");
  int last=Pos(opening);
  void Owned(JsonElement e,JsonElement o){Need(T(e,"root")==T(o,"root")&&T(e,"context")==T(o,"context")&&Same(S(e),S(o)),"immutable full boundary state and owner");}
  Owned(opening,rows.Take(Pos(opening)).Last(r=>K(r)=="confirmed"));
  Need(F(opening).GetProperty("maxActions").GetInt32()==64&&F(opening).GetProperty("maxQueries").GetInt32()==64&&T(F(opening),"information")=="Oracle"&&T(F(opening),"origin")== (scenario=="solver-battle-death"?"api-fixture":"natural-run"),"declared opening scope and limits");
  for(int i=0;i<qs.Length;i++){
   int idx=queryOffset+i+1;
   var d=ds[i];var c=rows.Single(r=>K(r)=="confirmed"&&T(r,"root")==T(d,"root"));var q=Read($"solver-query-{idx}.json");var step=Read($"solver-online-step-{idx}.json");var a=step.GetProperty("action");var cap=caps[i*2];var shadow=caps[i*2+1];var owner=rows.Take(Pos(cap)).Last(r=>K(r)=="confirmed");string hash=NativeEvidence.Hash(T(q,"root"));
   Need(last<Pos(cap)&&Pos(cap)<Pos(qs[i])&&Pos(qs[i])<Pos(shadow)&&Pos(shadow)<Pos(d)&&Pos(d)<Pos(c),"query/shadow/dispatch/confirmation order");last=Pos(c);
   foreach(var pair in new[]{(cap,"query"),(shadow,"shadow")}){Need(T(F(pair.Item1),"role")==pair.Item2&&F(pair.Item1).GetProperty("query").GetInt32()==idx&&T(F(pair.Item1),"information")=="Oracle"&&T(F(pair.Item1),"continuation")==hash,"capture role/continuation");Owned(pair.Item1,owner);}
    bool bounded=NativeSolverJourney.CandidateScenario(scenario);
    if(bounded){
     Need(T(profile,"candidatePolicy")== (NativeSolverCoordinator.IsScenario(scenario)?"frozen-beam-prefix/coordinator-one-step/v1":NativeSolverCandidateContract.Policy),"explicit candidate profile");
      if(T(q.GetProperty("candidate"),"policy")==NativeSolverCoordinator.Policy){var cfg=header.GetProperty("coordinatorProfile");NativeSolverCoordinator.BindSettings(q,cfg);bool guarded=NativeSolverCoordinator.Guarded(cfg);NativeSolverCoordinator.Budget(q,cfg.TryGetProperty("SearchParallelism",out var dp)?dp.GetInt32():1,NativeSolverCoordinator.Limit(cfg));Need(NativeSolverCoordinator.Rejection(q,guarded)==null,"eligible Coordinator query");var sh=Read($"solver-current-shadow-{idx}.json");Need(sh.GetProperty("query").GetInt32()==idx&&(!guarded||!sh.GetProperty("lethal").GetBoolean()&&T(sh,"outcome")!="Defeat")&&T(sh,"root")==T(qs[i],"root")&&T(sh,"context")==T(qs[i],"context")&&sh.GetProperty("parentUnchanged").GetBoolean()&&Same(sh.GetProperty("action"),a)&&Same(sh.GetProperty("nativeBefore"),step.GetProperty("nativeBefore"))&&T(sh,"continuation")==T(step,"expected"),"current shadow policy/full boundary");if(T(a,"Kind")=="EndTurn"){var risk=Read($"solver-endturn-risk-{idx}.json");Need(risk.GetProperty("sourceUnchanged").GetBoolean()&&(!guarded||!risk.GetProperty("risk").GetProperty("PlayerDead").GetBoolean())&&Same(risk.GetProperty("nativeState"),step.GetProperty("nativeBefore")),"upstream live EndTurn safety");}}else NativeSolverCandidateContract.Review(q,profile);
     Need(T(step,"searchBoundary")==T(q,"boundary")&&step.GetProperty("incompleteSearch").GetBoolean()==(T(q,"boundary")!="None"),"actual search boundary retained at deployment");
     var gaps=step.GetProperty("shadowPredictionGaps");var shadowRisk=Read($"solver-shadow-{idx}.json");
     Need(gaps.ValueKind==JsonValueKind.Array&&gaps.EnumerateArray().All(g=>g.GetProperty("Compensated").GetBoolean())&&shadowRisk.GetProperty("query").GetInt32()==idx&&!shadowRisk.GetProperty("uncompensated").GetBoolean()&&Same(gaps,shadowRisk.GetProperty("gaps")),"upstream shadow compensation and exact occurrence record");
    }
    Owned(qs[i],owner);Need(q.GetProperty("query").GetInt32()==idx&&F(qs[i]).GetProperty("query").GetInt32()==idx&&T(F(qs[i]),"root")==hash&&T(F(qs[i]),"boundary")==T(q,"boundary")&&(bounded||T(q,"boundary")=="None")&&q.GetProperty("sourceUnchanged").GetBoolean()&&Same(q.GetProperty("nativeState"),S(qs[i])),"actual query root/state");
   Need(step.GetProperty("query").GetInt32()==idx&&T(step,"root")==T(d,"root")&&T(step,"context")==T(d,"context")&&Same(a,q.GetProperty("actions")[0])&&T(a,"Kind")==T(F(d),"kind"),"unmodified first action/owner");
   NativeSolverActionContract.Bind(a,q,F(d).GetProperty("decision"),choices:true,potions:true);
   NativeSolverActionContract.BindReport(step,F(d).GetProperty("decision"));
   Need(Same(step.GetProperty("nativeBefore"),S(d))&&Same(S(d),S(qs[i]))&&Same(step.GetProperty("nativeAfter"),S(c)),"full deployment snapshots");
   NativeSolverFixtureContract.ReviewChildren(rows,step,d,c,q,step.GetProperty("nativeBefore"),step.GetProperty("nativeAfter"));
   foreach(string flag in new[]{"exact","parentUnchanged","consumedRejected","staleRejected","unconsumedAtRejection","zeroAdditionalDispatch"})Need(step.GetProperty(flag).GetBoolean(),"invariant "+flag);
   Need(T(step,"expected")==T(step,"actual")&&T(step,"actual")!=""&&step.GetProperty("currentSequence").GetInt64()==step.GetProperty("oldSequence").GetInt64()+1,"exact continuation and single-use ticket");
   if(i+1<qs.Length)Need(T(step,"actual")==T(Read($"solver-query-{idx+1}.json"),"root"),"real next query continuation");
   else if(!partial){
    Need(Pos(d)<Pos(terminal)&&Pos(terminal)<Pos(c)&&Pos(c)<Pos(verified)&&Pos(verified)<Pos(cleanup)&&Pos(cleanup)<Pos(end),"actual terminal/confirmed/verified/cleanup/completion order");
    Need(T(terminal,"root")==T(d,"root")&&T(terminal,"context")==T(d,"context")&&T(tf,"Root")==T(d,"root")&&T(tf,"Context")==T(d,"context")&&tf.GetProperty("Query").GetInt32()==idx&&T(tf,"Continuation")==T(step,"actual")&&Same(tf.GetProperty("State"),S(terminal)),"terminal actual action, full state and continuation");
    Need(F(verified).GetProperty("query").GetInt32()==idx&&T(F(verified),"outcome")==outcome&&Same(F(verified).GetProperty("preCleanup"),S(terminal))&&T(F(verified),"continuation")==T(tf,"Continuation"),"verification pre-cleanup authority");Owned(verified,c);Owned(cleanup,c);
    BindNativeTerminal(rows,terminal,c);
   }
  }
  if(partial)return;
  int hp=S(terminal).GetProperty("hp").GetInt32();
  if(outcome=="Victory"){
   Need(hp>0&&S(terminal).GetProperty("combat").GetProperty("enemies").EnumerateArray().All(e=>e.GetProperty("hp").GetInt32()<=0),"actual living player/dead enemies victory scope");
   Need(rows.Count(r=>K(r)=="battle-victory-before-cleanup")==1&&rows.Count(r=>K(r)=="battle-loss-confirmed")==0&&rows.Count(r=>K(r)=="history-clearing")>=1,"actual victory and history cleanup");
  }else Need(hp==0&&rows.Count(r=>K(r)=="battle-loss-start")==1&&rows.Count(r=>K(r)=="battle-loss-confirmed")==1&&rows.Count(r=>K(r)=="battle-victory-before-cleanup")==0,"actual dead player and loss callback");
  Need(T(F(cleanup),"outcome")==outcome&&F(cleanup).GetProperty("query").GetInt32()==queryOffset+ds.Length&&F(cleanup).GetProperty("battleVictories").GetInt32()==(outcome=="Victory"?1:0)&&F(cleanup).GetProperty("losses").GetInt32()==(outcome=="Defeat"?1:0)&&!F(cleanup).GetProperty("afterCombatSimulated").GetBoolean(),"native cleanup scope and counters");
  var normalized=JsonNode.Parse(S(end).GetRawText())!;Need(normalized["lifecycle"]!["native"]!["Closed"]!.GetValue<bool>()==closed,"closed terminal action gate");normalized["lifecycle"]!["native"]!["Closed"]=false;
  Need(Same(JsonSerializer.SerializeToElement(normalized),S(cleanup))&&T(end,"root")==T(cleanup,"root")&&T(end,"context")==T(cleanup,"context"),"only declared Closed change after cleanup");
  Need(ef.GetProperty("nativeActionsExecuted").GetInt32()==ds.Length&&ef.GetProperty("queries").GetInt32()==qs.Length&&T(ef,"outcome")==outcome&&ef.GetProperty("wholeBattle").GetBoolean()&&T(ef,"information")=="Oracle"&&T(ef,"origin")== (scenario=="solver-battle-death"?"api-fixture":"natural-run")&&ef.GetProperty("closedRejected").GetBoolean()==closed&&ef.GetProperty("zeroAdditionalDispatch").GetBoolean()&&Same(ef.GetProperty("opening"),S(opening)),"truthful complete battle summary");
  if(!closed)return;
  Need(Same(Read("final-state.json"),S(end)),"immutable final state");var result=Read("result.json").GetProperty("result");
  Need(T(result,"Terminal")== (outcome=="Defeat"?"Death":"Ongoing")&&T(result,"Stop")== (outcome=="Defeat"?"Completed":"SliceBoundary"),"battle versus run terminal result");
 }
}
