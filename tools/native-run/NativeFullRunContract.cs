using System.Text.Json;
using SimulatorLab.Core;
namespace OfflineSearchHarness;

// Finite integrity contract for orchestration/combined room transitions. This
// binds observations; native Tasks remain authoritative during business intervals.
internal static class NativeFullRunContract
{
 internal static readonly HashSet<string> Kinds=["full-act-enter","full-act-boss-complete","map-transition-ready"];
 internal static object ReviewEvents(IEnumerable<JsonElement> events){
  void Need(bool ok,string why)=>NativeBoundaryContract.Require(ok,"Run controller: "+why);
  string root="",context="";JsonElement action=default,dispatch=default,ready=default,last=default;bool active=false,travel=false,start=false,returned=false,prepared=false,committed=false,complete=false;
  int roots=0,transitions=0,arrivals=0,act=-1;bool boss=false;
  void Owner(JsonElement p)=>Need(p.GetProperty("root").GetString()==root&&p.GetProperty("context").GetString()==context,"current root/context ownership");
  foreach(var p in events){
   string kind=p.GetProperty("kind").GetString()!;var d=p.GetProperty("detail");
   if(kind=="root-request"){
    Need(!active,"unfinished previous root");root=p.GetProperty("root").GetString()!;context=p.GetProperty("context").GetString()!;action=d.GetProperty("action").Clone();active=true;committed=complete=false;travel=action.GetProperty("kind").GetString()=="Travel";start=returned=prepared=false;dispatch=default;roots++;
   }else if(kind=="dispatch"){
    Owner(p);Need(active&&dispatch.ValueKind==JsonValueKind.Undefined,"unique dispatch");dispatch=d.GetProperty("state").Clone();Need(NativeBoundaryContract.Equal(action,d.GetProperty("facts")),"actual dispatched action");
   }else if(kind is "room-exit-start" or "room-exit-return" or "map-transition-ready"&&travel){
    Owner(p);Need(active&&travel&&dispatch.ValueKind==JsonValueKind.Object,"transition belongs to dispatched Travel");var state=d.GetProperty("state");var f=d.GetProperty("facts");var decision=action.GetProperty("decision");
    Need(f.GetProperty("from").GetString()==decision.GetProperty("from").GetString()&&f.GetProperty("to").GetString()==decision.GetProperty("to").GetString(),"actual edge");
    Need(NativeBoundaryContract.Equal(f.GetProperty("room"),dispatch.GetProperty("rooms").GetProperty("reference")),"source room identity");
    if(kind=="room-exit-start"){Need(!start&&!returned&&!prepared,"unique ordered transition start");NativeBoundaryContract.Snapshot(state,dispatch,"Transition start complete dispatch state");start=true;}
    else{
     Need(start&&(!returned||kind=="map-transition-ready")&&!prepared,"ordered unique return/ready");
     Need(state.GetProperty("act").GetInt32()==dispatch.GetProperty("act").GetInt32()&&state.GetProperty("node").GetString()==decision.GetProperty("to").GetString(),"actual destination context");
     Need(NativeBoundaryContract.Equal(f.GetProperty("arrived"),state.GetProperty("rooms").GetProperty("reference")),"actual destination room");
     if(kind=="room-exit-return")returned=true;
     else{Need(returned,"native EnterMapCoord returned before entry preparation");prepared=true;ready=state.Clone();}
    }
   }else if(kind=="confirmed"){
    Owner(p);Need(active&&dispatch.ValueKind==JsonValueKind.Object,"confirmed actual dispatch");last=d.GetProperty("state").Clone();
    Need(d.GetProperty("facts").GetProperty("after").GetString()==NativeEvidence.Hash(JsonSerializer.Serialize(last,Program.Json)),"confirmed state hash");
    if(travel){Need(start&&returned&&prepared,"all transition stages required for confirmed Travel");NativeBoundaryContract.Snapshot(last,ready,"Prepared destination complete confirmed state");transitions++;}
    active=false;committed=true;
   }else if(kind=="room-complete"&&travel){
    Owner(p);Need(committed&&!complete,"unique old room completion after confirmation");NativeBoundaryContract.Snapshot(d.GetProperty("state"),last,"Completed transition confirmed state");complete=true;
   }else if(kind=="room-entered"&&travel){
    Owner(p);Need(committed&&complete,"destination arrived after old completion");NativeBoundaryContract.Snapshot(d.GetProperty("state"),last,"Room arrival confirmed complete state");Need(NativeBoundaryContract.Equal(d.GetProperty("facts").GetProperty("room"),last.GetProperty("rooms").GetProperty("reference")),"arrival actual instance");complete=false;arrivals++;
   }else if(kind=="full-act-enter"){
    Owner(p);Need(committed&&!active,"act entry after confirmed native action");
    var state=d.GetProperty("state");NativeBoundaryContract.Snapshot(state,last,"Act entry complete confirmed state");int next=d.GetProperty("facts").GetProperty("act").GetInt32();Need(next==act+1&&(act<0||boss)&&next<3&&state.GetProperty("act").GetInt32()==next,"ordered actual act entry");act=next;boss=false;
   }else if(kind=="full-act-boss-complete"){
    Owner(p);Need(committed&&!active,"Boss observation after confirmed native action");
    var state=d.GetProperty("state");NativeBoundaryContract.Snapshot(state,last,"Boss rewards complete confirmed state");var major=state.GetProperty("majorCombat");Need(!boss&&act==state.GetProperty("act").GetInt32()&&act==d.GetProperty("facts").GetProperty("act").GetInt32()&&state.GetProperty("hp").GetInt32()>0&&major.GetProperty("type").GetString()=="Boss"&&!major.GetProperty("inProgress").GetBoolean(),"actual living Boss victory/reward boundary");boss=true;
   }
  }
  Need(roots>0&&transitions==arrivals,"nonempty evidence and each confirmed transition has arrival");
  return new{roots,transitions,arrivals,act,boss,partialRoot=active?root:null};
 }
 internal static object Review(string directory){
  string chain="";long count=0,bytes=0;var records=new List<JsonElement>();
  foreach(var line in File.ReadLines(Path.Combine(directory,"trace.jsonl"))){
   var e=JsonDocument.Parse(line).RootElement;var p=e.GetProperty("payload");string next=NativeEvidence.Hash(chain+p.GetRawText());
   NativeBoundaryContract.Require(e.GetProperty("prev").GetString()==chain&&e.GetProperty("hash").GetString()==next&&e.GetProperty("seq").GetInt64()==++count,"Run trace hash chain");chain=next;bytes+=System.Text.Encoding.UTF8.GetByteCount(line)+1;records.Add(p.Clone());
  }
  var seal=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"trace-seal.json"))).RootElement;
  NativeBoundaryContract.Require(seal.GetProperty("count").GetInt64()==count&&seal.GetProperty("bytes").GetInt64()==bytes&&seal.GetProperty("chain").GetString()==chain,"Run trace seal");
  var report=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"full-run-progress.json"))).RootElement;
  var final=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"final-state.json"))).RootElement;
  NativeBoundaryContract.Snapshot(report.GetProperty("finalState"),final,"Run progress actual partial final state");
  var budget=report.GetProperty("budget");NativeFullRun.Validate(budget.GetProperty("configuration").Deserialize<NativeFullRun.Limits>(Program.Json));
  var header=records.Single(p=>p.GetProperty("kind").GetString()=="header").GetProperty("detail");
  int completed=Directory.GetFiles(directory,"solver-query-*.json").Length;
  bool accounting=header.TryGetProperty("queryAccounting",out var mode)&&mode.GetString()=="started-query-events/v1";
  var admitted=records.Where(p=>p.GetProperty("kind").GetString()=="run-query-admitted").Select(p=>p.GetProperty("detail")).ToArray();
  NativeBoundaryContract.Require(budget.GetProperty("queries").GetInt32()==(accounting?admitted.Length:completed)&&completed<=budget.GetProperty("queries").GetInt32(),"Global attempted/completed query count");
  if(accounting)NativeBoundaryContract.Require(admitted.Select((p,i)=>p.GetProperty("ordinal").GetInt32()==i+1).All(x=>x)&&admitted.Select(p=>p.GetProperty("query").GetInt32()).Distinct().Count()==admitted.Length,"Unique ordered actual query admission");
  if(budget.TryGetProperty("probeQueries",out var probeCount))NativeBoundaryContract.Require(probeCount.GetInt32()==records.Count(p=>p.GetProperty("kind").GetString()=="solver-probe-admitted")&&budget.GetProperty("totalQueryAttempts").GetInt32()==probeCount.GetInt32()+budget.GetProperty("queries").GetInt32(),"Global main/probe query accounting");
  int dop=header.GetProperty("coordinatorProfile").TryGetProperty("SearchParallelism",out var declared)?declared.GetInt32():1;
  foreach(string file in Directory.GetFiles(directory,"solver-query-*.json")){var q=JsonDocument.Parse(File.ReadAllText(file)).RootElement;if(q.GetProperty("engine").GetString()=="CombatSearchCoordinator"){var cfg=header.GetProperty("coordinatorProfile");NativeSolverCoordinator.BindSettings(q,cfg);NativeSolverCoordinator.Budget(q,dop,NativeSolverCoordinator.Limit(cfg));}}
  var reviewed=JsonSerializer.SerializeToElement(ReviewEvents(records));
  NativeBoundaryContract.Require(reviewed.GetProperty("act").GetInt32()==report.GetProperty("controlledAct").GetInt32()&&reviewed.GetProperty("act").GetInt32()==final.GetProperty("act").GetInt32(),"Required observed act lifecycle matches actual run progress");
  _=NativeSlipperyEvidence.Review(records);_=NativeReflectionsEvidence.Review(records);
  _=NativeRelicPickupEvidence.Review(records);
  _=NativeShopLedgerEvidence.Review(records);
  _=NativeAcceptanceLifecycleEvidence.Review(records);_=NativeEventResourceEvidence.Review(records.ToArray());
  _=NativeSolverPhaseProtocol.Review(directory);
  NativeAncientEvidence.Review(directory,records.ToArray());
  NativeGeneratedChoiceEvidence.Review(directory,records.ToArray());
  NativeTerminalChoiceEvidence.Review(directory,records.ToArray());
  NativeTerminalPolicyEvidence.Review(directory,records.ToArray(),header.GetProperty("coordinatorProfile"));
  NativeFullRunTerminal.Review(records,JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"result.json"))).RootElement,final);
  return reviewed;
 }
}
