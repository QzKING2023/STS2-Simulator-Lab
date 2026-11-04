using System.Text.Json;
using SimulatorLab.Core;
namespace OfflineSearchHarness;
internal static class NativeSolverJourneyContract
{
 static void Need(bool ok,string why){if(!ok)throw new NativeDataException("Solver journey evidence: "+why);}
 static bool Eq(JsonElement a,JsonElement b)=>NativeDecisionTicket.SameActionJson(a.GetRawText(),b.GetRawText());
  internal static void Review(string directory,bool budgetPrefix=false,bool rejectedCandidate=false,bool coordinator=false)
 {
  var rows=File.ReadLines(Path.Combine(directory,"trace.jsonl")).Select(x=>JsonDocument.Parse(x).RootElement.GetProperty("payload").Clone()).ToArray();
  string K(JsonElement e)=>e.GetProperty("kind").GetString()!;int P(JsonElement e)=>Array.IndexOf(rows,e);
  JsonElement F(JsonElement e)=>e.GetProperty("detail").GetProperty("facts");JsonElement S(JsonElement e)=>e.GetProperty("detail").GetProperty("state");
  var header=rows.Single(e=>K(e)=="header");var entries=rows.Where(e=>K(e)=="solver-journey-battle-entry").ToArray();var returns=rows.Where(e=>K(e)=="solver-journey-battle-return").ToArray();
  Need(entries.Length>0&&entries.Length==returns.Length+(budgetPrefix?1:0)&&!rows.Any(e=>K(e)=="fixture-setup-declared"),"actual natural complete battle set or explicit budget suffix");
  int total=0;
  for(int i=0;i<returns.Length;i++){
   var entry=entries[i];var ret=returns[i];int offset=i*64;var owner=rows.Take(P(entry)).Last(e=>K(e)=="confirmed");
   Need(P(entry)<P(ret)&&(i==0||P(returns[i-1])<P(entry)),"battle order");
   Need(F(entry).GetProperty("battle").GetInt32()==i+1&&F(entry).GetProperty("offset").GetInt32()==offset&&F(entry).GetProperty("priorRejected").GetBoolean()==(i>0)&&F(entry).GetProperty("information").GetString()=="Oracle","entry identity/ticket/information");
   Need(Eq(S(entry),S(owner))&&entry.GetProperty("root").GetString()==owner.GetProperty("root").GetString()&&entry.GetProperty("context").GetString()==owner.GetProperty("context").GetString(),"entry confirmed full boundary");
   var segment=new[]{header,owner}.Concat(rows.Skip(P(entry)+1).Take(P(ret)-P(entry)-1)).ToArray();
   NativeSolverBattleContract.ReviewBattle(directory,segment,offset,closed:false);
   var end=segment.Single(e=>K(e)=="solver-battle-complete");
   Need(Eq(S(ret),S(end))&&ret.GetProperty("root").GetString()==end.GetProperty("root").GetString()&&ret.GetProperty("context").GetString()==end.GetProperty("context").GetString()&&F(ret).GetProperty("battle").GetInt32()==i+1&&F(ret).GetProperty("offset").GetInt32()==offset&&F(ret).GetProperty("actions").GetInt32()==F(end).GetProperty("queries").GetInt32(),"return complete immutable battle boundary");
   total+=F(ret).GetProperty("actions").GetInt32();
  }
   if(coordinator){if(budgetPrefix)NativeSolverCoordinatorEvidence.Tail(directory,rows,entries.Last(),returns.Length,total);else NativeSolverCoordinatorEvidence.Completed(directory,rows,total);return;}
   if(budgetPrefix){
   var entry=entries.Last();int offset=returns.Length*64,index=offset+1;var q=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,$"solver-query-{index}.json"))).RootElement;
    NativeSolverBudgetContract.Review(q,JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"solver-profile.json"))).RootElement);
    var tail=rows.Skip(P(entry)).ToArray();var cap=tail.Single(e=>K(e)=="solver-root-pure");var query=tail.Single(e=>K(e)=="solver-query-complete");var owner=rows.Take(P(entry)).Last(e=>K(e)=="confirmed");
   Need(F(entry).GetProperty("battle").GetInt32()==returns.Length+1&&F(entry).GetProperty("offset").GetInt32()==offset&&F(entry).GetProperty("priorRejected").GetBoolean()==(returns.Length>0),"budget suffix battle identity/ticket");
   foreach(var e in new[]{entry,cap,query})Need(Eq(S(e),S(owner))&&e.GetProperty("root").GetString()==owner.GetProperty("root").GetString()&&e.GetProperty("context").GetString()==owner.GetProperty("context").GetString(),"budget suffix confirmed full state and owner");
   Need(P(entry)<P(cap)&&P(cap)<P(query)&&!tail.Any(e=>K(e)=="dispatch"||K(e)=="solver-battle-complete")&&F(cap).GetProperty("role").GetString()=="query"&&F(cap).GetProperty("query").GetInt32()==index&&F(query).GetProperty("query").GetInt32()==index,"budget query not adopted or dispatched");
   string hash=NativeEvidence.Hash(q.GetProperty("root").GetString()!);
    if(rejectedCandidate){
     var reason=NativeSolverCandidateContract.Rejection(q);var report=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"result.json"))).RootElement.GetProperty("result");
     Need(q.GetProperty("boundary").GetString()=="None"&&NativeSolverJourney.CandidateScenario(header.GetProperty("detail").GetProperty("scenario").GetString()!)&&reason!=null&&report.GetProperty("Error").GetString()!.Contains("Candidate not adopted: "+reason),"actual unadopted candidate rejection and report reason");
    }
    Need(q.GetProperty("query").GetInt32()==index&&(rejectedCandidate||q.GetProperty("boundary").GetString() is "NodeLimit" or "TimeLimit")&&q.GetProperty("sourceUnchanged").GetBoolean()&&Eq(q.GetProperty("nativeState"),S(query))&&F(cap).GetProperty("continuation").GetString()==hash&&F(query).GetProperty("root").GetString()==hash&&F(query).GetProperty("boundary").GetString()==q.GetProperty("boundary").GetString(),"actual bounded source-pure query");
   Need(Eq(JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"final-state.json"))).RootElement,S(query)),"budget stop final confirmed state");
  }
  Need(Directory.GetFiles(directory,"solver-query-*.json").Length==total+(budgetPrefix?1:0)&&Directory.GetFiles(directory,"solver-online-step-*.json").Length==total,"all battle query and action files retained");
 }
}
