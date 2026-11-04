using System.Text.Json;
using SimulatorLab.Core;
namespace OfflineSearchHarness;
internal static class NativeSolverRunContract
{
 static void Need(bool ok,string why){if(!ok)throw new NativeDataException("Solver run evidence: "+why);}
 static bool Eq(JsonElement a,JsonElement b)=>NativeDecisionTicket.SameActionJson(a.GetRawText(),b.GetRawText());
 internal static void Review(string directory)
 {
  JsonElement Read(string n)=>JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,n))).RootElement;
  var rows=File.ReadLines(Path.Combine(directory,"trace.jsonl")).Select(x=>JsonDocument.Parse(x).RootElement.GetProperty("payload").Clone()).ToArray();
  string K(JsonElement e)=>e.GetProperty("kind").GetString()!;string T(JsonElement e,string k)=>e.GetProperty(k).GetString()!;
  JsonElement F(JsonElement e)=>e.GetProperty("detail").GetProperty("facts");JsonElement S(JsonElement e)=>e.GetProperty("detail").GetProperty("state");
  int P(JsonElement e)=>Array.IndexOf(rows,e);JsonElement One(string k)=>rows.Single(e=>K(e)==k);
  void Owned(JsonElement a,JsonElement b)=>Need(T(a,"root")==T(b,"root")&&T(a,"context")==T(b,"context")&&Eq(S(a),S(b)),"stationary full state/owner");
  var header=One("header");string scenario=T(header.GetProperty("detail"),"scenario");bool take=scenario=="solver-run-take";
  Need(NativeSolverRun.IsScenario(scenario)&&!rows.Any(e=>K(e)=="fixture-setup-declared"),"natural registered no-fixture flow");
  var opens=rows.Where(e=>K(e)=="solver-battle-opening").ToArray();var ends=rows.Where(e=>K(e)=="solver-battle-complete").ToArray();Need(opens.Length==2&&ends.Length==2,"two actual whole battles");
  for(int i=0;i<2;i++){
   var owner=rows.Take(P(opens[i])).Last(e=>K(e)=="confirmed");
   Need(P(opens[i])<P(ends[i])&&(i==0||P(ends[i-1])<P(opens[i])),"battle lifecycle order");
   var segment=new[]{header,owner}.Concat(rows.Skip(P(opens[i])).Take(P(ends[i])-P(opens[i])+1)).ToArray();
   NativeSolverBattleContract.ReviewBattle(directory,segment,i*64,closed:i==1);
  }
  Need(T(F(ends[0]),"outcome")=="Victory","first victory before rewards");
  var entry=One("solver-run-reward-entry");var reward=One("solver-run-reward-complete");var next=One("solver-run-next-battle");var end=One("solver-run-complete");var start=One("solver-run-opening");
  Owned(entry,ends[0]);Owned(start,rows.Take(P(start)).Last(e=>K(e)=="confirmed"));
  Need(Eq(F(reward).GetProperty("before"),S(entry))&&P(ends[0])<P(entry)&&P(entry)<P(reward)&&P(reward)<P(next)&&P(next)<P(opens[1])&&P(ends[1])<P(end),"run lifecycle and reward input");
  var offer=rows.Single(e=>K(e)=="dispatch"&&T(F(e),"kind")=="RewardOffer");var returned=rows.Single(e=>K(e)=="confirmed"&&T(e,"root")==T(offer,"root"));
  Need(Eq(S(offer),S(entry))&&T(offer,"context")==T(entry,"context"),"reward dispatch unchanged entry state/context");Owned(reward,returned);Need(P(entry)<P(offer)&&P(offer)<P(returned)&&P(returned)<P(reward),"native reward action owner/order");
  var gold=rows.Where(e=>P(e)>P(offer)&&P(e)<P(returned)&&K(e)=="n3c-open"&&F(e).GetProperty("kind").GetString()=="reward-item").Select(e=>F(e).GetProperty("options")).First().EnumerateArray().Single(e=>T(e,"Kind")=="GoldReward").GetProperty("Amount").GetInt32();
  Need(S(reward).GetProperty("gold").GetInt32()-S(entry).GetProperty("gold").GetInt32()==gold&&gold>0,"native offered gold amount actually claimed");
  var before=S(entry).GetProperty("deck").EnumerateArray().ToArray();var after=S(reward).GetProperty("deck").EnumerateArray().ToArray();var acquired=F(reward).GetProperty("acquired").EnumerateArray().Select(x=>T(x,"$ref")).ToArray();
  Need(after.Length==before.Length+(take?1:0)&&after.Take(before.Length).Zip(before).All(x=>Eq(x.First,x.Second))&&acquired.Length==(take?1:0),"reward deck content and skip/take cardinality");
  if(take)Need(acquired[0]==T(after.Last(),"id")&&!before.Any(x=>T(x,"id")==acquired[0])&&rows.Count(e=>K(e)=="n3c-card-continuity")==1,"actual new reward instance and next combat copy");
  else Need(!rows.Any(e=>K(e)=="n3c-card-continuity"),"skip has no acquired card");
  var travel=rows.Single(e=>K(e)=="dispatch"&&T(F(e),"kind")=="NativeTravel");var travelEnd=rows.Single(e=>K(e)=="confirmed"&&T(e,"root")==T(travel,"root"));
  var proceed=rows.Single(e=>K(e)=="dispatch"&&T(F(e),"kind")=="ProceedRewards");
  Need(P(reward)<P(proceed)&&P(proceed)<P(travel)&&P(travelEnd)<P(next)&&T(F(travel).GetProperty("decision"),"from")==T(S(travel),"node")&&T(F(travel).GetProperty("decision"),"to")==T(S(travelEnd),"node"),"legal actual map travel coordinates and order");
  Owned(next,travelEnd);Owned(opens[1],next);
  foreach(string k in new[]{"hp","maxHp","gold","deck","relics","potions"})Need(Eq(S(reward).GetProperty(k),S(next).GetProperty(k)),"persistent reward-to-next-battle "+k);
  // A Travel confirmation retains its dispatch/source context. The immutable
  // arrived state and actual to-coordinate identify the destination battle.
  Need(T(S(next),"node")!=T(S(ends[0]),"node")&&S(next).GetProperty("combat").GetProperty("turn").GetInt32()==1&&T(S(next).GetProperty("combat"),"phase")=="Play","new battle destination and initialization");
  foreach(string k in new[]{"previousBattleTicketRejected","unconsumed","zeroAdditionalDispatch","differentCombat"})Need(F(next).GetProperty(k).GetBoolean(),"cross-battle ticket "+k);
  Need(F(next).GetProperty("oldSequence").GetInt64()<F(next).GetProperty("currentSequence").GetInt64(),"previous battle ticket sequence invalidation");
  Owned(end,ends[1]);Need(Eq(Read("final-state.json"),S(end))&&F(end).GetProperty("battles").GetInt32()==2&&T(F(end),"information")=="Oracle"&&T(F(end),"origin")=="natural-run","truthful run completion");
  int count=ends.Sum(e=>F(e).GetProperty("queries").GetInt32());Need(F(end).GetProperty("solverActions").GetInt32()==count&&Directory.GetFiles(directory,"solver-query-*.json").Length==count&&Directory.GetFiles(directory,"solver-online-step-*.json").Length==count,"all queries/actions retained");
 }
}
