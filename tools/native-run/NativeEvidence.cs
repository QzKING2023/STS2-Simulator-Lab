using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Creatures;
using SimulatorLab.Core;

namespace OfflineSearchHarness;

internal sealed class NativeDataException(string message) : Exception(message);

// Identity registration is mandatory execution infrastructure, independent of optional observations.
internal static class NativeIdentity
{
    static readonly Dictionary<object,string> ids=new(ReferenceEqualityComparer.Instance);
    internal static string Get(object value)
    {
        if(ids.TryGetValue(value,out var id))return id;
        ids.Add(value,id="i"+(ids.Count+1));
        NativeEvidence.Emit("identity",new{id,type=value.GetType().Name,model=value is AbstractModel m?m.Id.ToString():null});
        return id;
    }
    internal static object? Ref(object? value)=>value==null?null:new Dictionary<string,object>{{"$ref",Get(value)}};
}

internal static class NativeEvidence
{
    static StreamWriter? writer;
    static long count,bytes;
    static string chain="";
    static readonly Dictionary<string,int> kinds=[];
    internal static Exception? Failure;
    internal static string Mode="detailed";
    internal static string Root="boot";
    internal static string Context="boot";
    internal static long Quota=512L*1024*1024;
    internal static bool SinkFault;
    internal static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal static void Open(string output,object header)
    {
        writer=new StreamWriter(new FileStream(Path.Combine(output,"trace.jsonl"),FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(false)){AutoFlush=true,NewLine="\n"};
        Emit("header",header);
    }
    internal static void Emit(string kind,object? detail=null)
    {
        if(writer==null||Failure!=null)return;
        using var measurement=NativeTiming.Measure("trace-serialize-and-write");
        try{
            if(SinkFault)throw new IOException("labelled trace sink control");
            var payload=JsonSerializer.SerializeToElement(new{schema="native-event/v1",kind,root=Root,context=Context,detail},Program.Json);
            string raw=JsonSerializer.Serialize(payload);
            string hash=Hash(chain+raw);
            string line=JsonSerializer.Serialize(new{seq=count+1,prev=chain,payload,hash});
            long next=Encoding.UTF8.GetByteCount(line)+1;
            if(bytes+next>Quota)throw new NativeDataException("Trace quota exceeded; data incomplete");
            writer.WriteLine(line);count++;bytes+=next;chain=hash;
            kinds[kind]=kinds.GetValueOrDefault(kind)+1;
        }catch(Exception ex){Failure=ex;}
    }
    internal static void Ensure()
    {
        if(Failure!=null)throw new NativeDataException("Native observation failed: "+Failure.Message);
    }
    internal static object Finish(string output,bool success)
    {
        Emit("footer",new{success,requiredScope="N2-native-slice",counts=kinds.ToDictionary(k=>k.Key,k=>k.Value),priorCount=count});
        writer?.Dispose();writer=null;
        var ledger=new{schema="native-trace-seal/v1",count,bytes,chain,complete=success&&Failure==null,mode=Mode,missingReasons=Failure==null?Array.Empty<string>():[Failure.Message]};
        File.WriteAllText(Path.Combine(output,"trace-seal.json"),JsonSerializer.Serialize(ledger,Program.Json));
        return ledger;
    }
    // No game assembly initialization is needed to review a persisted trace.
    internal static int Review(string directory,bool solverBudgetPrefix=false,bool solverRoomPrefix=false,bool solverRejectedPrefix=false,bool solverCoordinatorPrefix=false)
    {
        try{
            bool failurePrefix=solverBudgetPrefix||solverRoomPrefix||solverRejectedPrefix||solverCoordinatorPrefix;
            var contract=new NativeTraceContract();
            var seal=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"trace-seal.json"))).RootElement;
            if(seal.GetProperty("schema").GetString()!="native-trace-seal/v1")throw new NativeDataException("Unknown seal schema");
            string prev="";long seq=0;var identities=new HashSet<string>();var requests=new HashSet<string>();var resolved=new HashSet<string>();
            var requestData=new Dictionary<string,JsonElement>();var tasks=new HashSet<string>();var taskEnds=new HashSet<string>();
            var roots=new HashSet<string>();var completedRoots=new HashSet<string>();
            int dispatch=0,confirm=0;bool footer=false;var observed=new Dictionary<string,int>();string? buildId=null;int blocked=0,unblocked=0,energy=0,blockGained=0;var lifecycle=new List<string>();
            void References(JsonElement value){
                if(value.ValueKind==JsonValueKind.Object){foreach(var p in value.EnumerateObject()){
                    if(p.Name=="$ref"&&!identities.Contains(p.Value.GetString()!))throw new NativeDataException("Dangling identity");References(p.Value);
                }}else if(value.ValueKind==JsonValueKind.Array)foreach(var e in value.EnumerateArray())References(e);
            }
            foreach(string line in File.ReadLines(Path.Combine(directory,"trace.jsonl"))){
                var e=JsonDocument.Parse(line).RootElement;var p=e.GetProperty("payload");
                string raw=JsonSerializer.Serialize(p);string hash=Hash(prev+raw);
                if(footer||e.GetProperty("seq").GetInt64()!=++seq||e.GetProperty("prev").GetString()!=prev||e.GetProperty("hash").GetString()!=hash||p.GetProperty("schema").GetString()!="native-event/v1")throw new NativeDataException("Trace chain/order/schema error");
                prev=hash;string kind=p.GetProperty("kind").GetString()!;var d=p.GetProperty("detail");
                var snapshot=NativeTraceContract.ReadSnapshot(kind,d);
                if(seq==1&&kind!="header")throw new NativeDataException("Missing header");
                if(kind=="header"){
                    if(seq!=1||d.GetProperty("schema").GetString()!="native-header/v1")throw new NativeDataException("Unknown/duplicate header");
                    buildId=d.GetProperty("buildId").GetString();
                }
                if(snapshot is {} state){
                    foreach(var field in new[]{"act","floor","node","room","hp","maxHp","gold","block","runRng","playerRng","eventRng","powers","deck","relics","potions","combat"})
                        if(!state.TryGetProperty(field,out _))throw new NativeDataException("Missing state field "+field);
                    void CheckRng(JsonElement rng){if(rng.GetProperty("counter").GetInt64()<0)throw new NativeDataException("Negative RNG counter");foreach(string word in new[]{"s0","s1","s2","s3"})_=rng.GetProperty(word).GetUInt64();}
                    foreach(string stream in new[]{"runRng","playerRng"}){
                        var rng=state.GetProperty(stream);if(rng.ValueKind==JsonValueKind.Null)continue;
                        var streams=rng.GetProperty("rngs").EnumerateObject().ToArray();if(streams.Length==0)throw new NativeDataException("Empty RNG inventory");
                        foreach(var r in streams)CheckRng(r.Value);
                    }
                    if(state.GetProperty("eventRng").ValueKind!=JsonValueKind.Null)CheckRng(state.GetProperty("eventRng"));
                }
                lifecycle.Add(kind);
                if(kind.EndsWith("fault",StringComparison.Ordinal))throw new NativeDataException("Faulted inner task");
                if(kind=="combat-history"){
                    var record=d.GetProperty("facts");var facts=record.GetProperty("facts");
                    switch(record.GetProperty("type").GetString()){
                        case "DamageReceivedEntry":var result=facts.GetProperty("Result");blocked+=result.GetProperty("BlockedDamage").GetInt32();unblocked+=result.GetProperty("UnblockedDamage").GetInt32();break;
                        case "EnergySpentEntry":energy+=facts.GetProperty("Amount").GetInt32();break;
                        case "BlockGainedEntry":blockGained+=facts.GetProperty("Amount").GetInt32();break;
                    }
                }
                if(kind=="identity"&&!identities.Add(d.GetProperty("id").GetString()!))throw new NativeDataException("Repeated identity");
                References(d);
                contract.Accept(p,snapshot);
                if(kind=="choice-requested"){
                    string id=d.GetProperty("id").GetString()!;
                    if(!requests.Add(id)||!roots.Contains(d.GetProperty("root").GetString()!))throw new NativeDataException("Repeated/unparented request");
                    requestData.Add(id,d.Clone());
                }
                if(kind=="choice-resolved"){
                    var id=d.GetProperty("id").GetString()!;if(!requests.Contains(id)||!resolved.Add(id))throw new NativeDataException("Unmatched response");
                    var request=requestData[id];var selected=d.GetProperty("selected").EnumerateArray().Select(s=>s.GetString()).ToArray();
                    var handles=request.GetProperty("options").EnumerateArray().Select(o=>o.GetProperty("handle").GetString()).ToArray();
                    if(selected.Length<request.GetProperty("min").GetInt32()||selected.Length>request.GetProperty("max").GetInt32()||selected.Distinct().Count()!=selected.Length||selected.Any(h=>!handles.Contains(h))||request.GetProperty("context").GetString()!=p.GetProperty("context").GetString())throw new NativeDataException("Illegal persisted response");
                }
                if(kind=="task-start"&&!tasks.Add(d.GetProperty("id").GetString()!))throw new NativeDataException("Repeated task");
                if(kind=="task-completed"&&(!tasks.Contains(d.GetProperty("id").GetString()!)||!taskEnds.Add(d.GetProperty("id").GetString()!)))throw new NativeDataException("Unmatched task completion");
                if(kind=="dispatch"){if(!roots.Add(p.GetProperty("root").GetString()!))throw new NativeDataException("Repeated dispatch");dispatch++;}
                if(kind=="confirmed"){
                    var root=p.GetProperty("root").GetString()!;
                    if(!roots.Contains(root)||!completedRoots.Add(root))throw new NativeDataException("Unmatched root confirmation");confirm++;
                }
                if(kind=="footer"){
                    footer=true;if(d.GetProperty("success").GetBoolean()==failurePrefix||d.GetProperty("priorCount").GetInt64()!=seq-1)throw new NativeDataException("Incomplete footer");
                    foreach(var entry in d.GetProperty("counts").EnumerateObject())if(observed.GetValueOrDefault(entry.Name)!=entry.Value.GetInt32())throw new NativeDataException("Summary mismatch");
                    if(d.GetProperty("counts").EnumerateObject().Count()!=observed.Count)throw new NativeDataException("Missing summary kind");
                }
                observed[kind]=observed.GetValueOrDefault(kind)+1;
            }
            if(!footer||seal.GetProperty("complete").GetBoolean()==failurePrefix||seal.GetProperty("count").GetInt64()!=seq||seal.GetProperty("chain").GetString()!=prev||seal.GetProperty("bytes").GetInt64()!=new FileInfo(Path.Combine(directory,"trace.jsonl")).Length||dispatch==0||dispatch!=confirm||!requests.SetEquals(resolved)||!roots.SetEquals(completedRoots)||!tasks.SetEquals(taskEnds))throw new NativeDataException("Missing evidence/unfinished requests/tasks");
            var report=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"result.json"))).RootElement;
            var process=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"process.json"))).RootElement;
            if(report.GetProperty("result").GetProperty("Schema").GetString()!="native-run-result/v1"||process.GetProperty("schema").GetString()!="native-process/v1")throw new NativeDataException("Unknown result/process schema");
            var outcome=report.GetProperty("result").Deserialize<NativeRunResultV1>(Program.Json)??throw new NativeDataException("Missing typed result");
            if(outcome.Validate().Count>0||outcome.ExitCode!=report.GetProperty("rawExit").GetInt32())throw new NativeDataException("Invalid result contract");
            if(solverRoomPrefix){
                if(!NativeSolverJourney.CandidateScenario(contract.Scenario.Scenario)||report.GetProperty("rawExit").GetInt32()!=2||process.GetProperty("rawExit").GetInt32()!=2||!process.GetProperty("exitConsistent").GetBoolean()||process.GetProperty("buildId").GetString()!=buildId||outcome.Stop!=NativeRunStop.Unsupported||outcome.Terminal!=NativeRunTerminal.Ongoing||outcome.DataComplete||outcome.Confirmed!=confirm||outcome.Dispatched!=dispatch||seal.GetProperty("missingReasons").GetArrayLength()!=0||!outcome.Error!.Contains("Natural room requires capability composition: MerchantRoom"))throw new NativeDataException("Solver unsupported room prefix outcome/source mismatch");
                contract.FinishSolverBudgetPrefix(seal,report,process,unsupportedRoom:true);
                NativeSolverJourneyContract.Review(directory);
                var data=File.ReadLines(Path.Combine(directory,"trace.jsonl")).Select(l=>JsonDocument.Parse(l).RootElement.GetProperty("payload").Clone()).ToArray();var last=data.Last(e=>e.GetProperty("kind").GetString()=="confirmed");var arrived=data.Single(e=>e.GetProperty("kind").GetString()=="room-entered"&&e.GetProperty("root").GetString()==last.GetProperty("root").GetString());
                var final=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"final-state.json"))).RootElement;
                if(!NativeBoundaryContract.Equal(final,last.GetProperty("detail").GetProperty("state"))||!NativeBoundaryContract.Equal(final,arrived.GetProperty("detail").GetProperty("state"))||arrived.GetProperty("context").GetString()!=last.GetProperty("context").GetString())throw new NativeDataException("Solver unsupported room final immutable state/arrival");
                File.WriteAllText(Path.Combine(directory,"trace-review.json"),JsonSerializer.Serialize(new{schema="solver-room-prefix-review/v1",status="prefix-validated-unsupported-not-completed",rawExit=2,dataComplete=false,seq,dispatch,confirm,historyCheckpointPresent=observed.ContainsKey("history-checkpoint")},Program.Json));return 0;
            }
            if(solverCoordinatorPrefix){
                if(!NativeSolverCoordinator.IsScenario(contract.Scenario.Scenario)||report.GetProperty("rawExit").GetInt32()!=process.GetProperty("rawExit").GetInt32()||!process.GetProperty("exitConsistent").GetBoolean()||process.GetProperty("buildId").GetString()!=buildId||outcome.Stop is not (NativeRunStop.Unsupported or NativeRunStop.BudgetExceeded)||outcome.Terminal!=NativeRunTerminal.Ongoing||outcome.DataComplete||outcome.Confirmed!=confirm||outcome.Dispatched!=dispatch||confirm!=dispatch||seal.GetProperty("missingReasons").GetArrayLength()!=0)throw new NativeDataException("Coordinator prefix actual result/source/counters");
                bool actPrefix=outcome.Stop==NativeRunStop.Unsupported&&outcome.Error!.Contains("Next-act first-choice contract currently requires unblocked native Tezcatara");
                contract.FinishSolverBudgetPrefix(seal,report,process,unsupportedActPrefix:actPrefix);
                NativeSolverJourneyContract.Review(directory,budgetPrefix:!actPrefix,coordinator:true);
                if(NativeDarv.IsScenario(contract.Scenario.Scenario))NativeForeseerEvidence.Review(directory);
                File.WriteAllText(Path.Combine(directory,"trace-review.json"),JsonSerializer.Serialize(new{schema="coordinator-prefix-review/v1",status=actPrefix?"validated-boss-rewards-act-prefix-not-completed":"validated-prefix-not-completed",rawExit=process.GetProperty("rawExit").GetInt32(),dataComplete=false,seq,dispatch,confirm},Program.Json));return 0;
            }
            if(solverBudgetPrefix||solverRejectedPrefix){
                int expectedExit=solverRejectedPrefix?2:3;var expectedStop=solverRejectedPrefix?NativeRunStop.Unsupported:NativeRunStop.BudgetExceeded;
                if(!NativeSolverJourney.IsScenario(contract.Scenario.Scenario)||report.GetProperty("rawExit").GetInt32()!=expectedExit||process.GetProperty("rawExit").GetInt32()!=expectedExit||!process.GetProperty("exitConsistent").GetBoolean()||process.GetProperty("buildId").GetString()!=buildId||outcome.Stop!=expectedStop||outcome.Terminal!=NativeRunTerminal.Ongoing||outcome.DataComplete||outcome.Confirmed!=confirm||outcome.Dispatched!=dispatch||seal.GetProperty("missingReasons").GetArrayLength()!=0)throw new NativeDataException("Solver budget/rejected prefix outcome/source mismatch");
                contract.FinishSolverBudgetPrefix(seal,report,process);
                NativeSolverJourneyContract.Review(directory,budgetPrefix:true,rejectedCandidate:solverRejectedPrefix);
                File.WriteAllText(Path.Combine(directory,"trace-review.json"),JsonSerializer.Serialize(new{schema="solver-budget-prefix-review/v1",status=solverRejectedPrefix?"prefix-validated-rejected-candidate-not-completed":"prefix-validated-budget-not-completed",rawExit=expectedExit,dataComplete=false,seq,dispatch,confirm,historyCheckpointPresent=observed.ContainsKey("history-checkpoint")},Program.Json));return 0;
            }
            if(report.GetProperty("rawExit").GetInt32()!=0||process.GetProperty("rawExit").GetInt32()!=0||!process.GetProperty("exitConsistent").GetBoolean()||process.GetProperty("buildId").GetString()!=buildId||report.GetProperty("result").GetProperty("Confirmed").GetInt32()!=confirm)throw new NativeDataException("Contradictory result/exit/counter/build");
            contract.Finish(seal,report,process);
            if(NativeSolverBattle.IsScenario(contract.Scenario.Scenario))NativeSolverBattleContract.Review(directory);
            if(NativeSolverRun.IsScenario(contract.Scenario.Scenario))NativeSolverRunContract.Review(directory);
            if(NativeSolverJourney.IsScenario(contract.Scenario.Scenario))NativeSolverJourneyContract.Review(directory);
            if(NativeDarv.IsScenario(contract.Scenario.Scenario))NativeForeseerEvidence.Review(directory);
            if(contract.Scenario.Scenario=="slice-solver-gate")NativeSolverContract.Review(directory);
            if(NativeSolverOnline.IsScenario(contract.Scenario.Scenario))NativeSolverOnlineContract.Review(directory);
            if(contract.Scenario.Scenario=="solver-choice-potion")NativeSolverFixtureContract.Review(directory);
            if(contract.Scenario.Scenario=="slice-solver-loop")NativeSolverLoopContract.Review(directory);
            if(contract.Scenario.Scenario is "slice-skip" or "slice-take"){
                foreach(string kind in new[]{"new-run","opening-finished","first-battle-opening","battle-victory-before-cleanup","battle-victory-cleanup","reward-offer","rewards-completed","next-combat-first-play"})
                    if(observed.GetValueOrDefault(kind)!=1)throw new NativeDataException("Missing/duplicate lifecycle "+kind);
                int previous=-1;foreach(string kind in new[]{"new-run","opening-finished","first-battle-opening","battle-victory-before-cleanup","history-clearing","battle-victory-cleanup","reward-offer","rewards-completed","next-combat-first-play"}){
                    int index=lifecycle.FindIndex(previous+1,x=>x==kind);if(index<0)throw new NativeDataException("Lifecycle order/missing cleanup");previous=index;
                }
            }
            File.WriteAllText(Path.Combine(directory,"trace-review.json"),JsonSerializer.Serialize(new{schema="native-trace-review/v1",status="passed",seq,dispatch,confirm,requests=requests.Count,identities=identities.Count,counts=observed,effects=new{blocked,unblocked,energy,blockGained,complete=seal.GetProperty("mode").GetString()=="detailed",source="derived from native-history records, not complete trigger causal graph"}},Program.Json));
            return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
    }
    static bool SnapshotControlKind(string kind)=>new[]{"dispatch","confirmed","new-run","event-candidates","opening-finished","map-candidates","first-battle-opening","combat-candidates","combat-history","battle-victory-before-cleanup","battle-victory-cleanup","reward-offer","rewards-completed","card-reward-acquired","next-combat-first-play","fixture-setup-declared","fixture-assertion","fixture-contract","sink-failure-trigger","death"}.Contains(kind);
    internal static int Mutate(string directory,string control)
    {
        if(!Path.GetFullPath(directory).Contains(Path.Combine(".build","runs"))||!Path.GetFileName(directory).StartsWith("control-"))throw new InvalidOperationException("Use a fresh control copy");
        var path=Path.Combine(directory,"trace.jsonl");var rows=File.ReadAllLines(path).Select(x=>JsonNode.Parse(x)!.AsObject()).ToList();
        if(control=="gap")rows.RemoveAt(4);
        else if(control=="repeat")rows.Insert(4,rows[4].DeepClone().AsObject());
        else if(control=="truncate"){File.WriteAllText(path,string.Join('\n',rows.Select(r=>r.ToJsonString()))+"\n{\"seq\":");return 0;}
        else if(control=="wronghash")rows[3]["hash"]="corrupt";
        else{
            string Kind(JsonObject r)=>r["payload"]!["kind"]!.GetValue<string>();
            if(control.StartsWith("transaction-review:",StringComparison.Ordinal))NativeMerchantExpansionControls.Apply(rows,control.Split(':',2)[1]);
            else if(control.StartsWith("merchant-economy-review:",StringComparison.Ordinal))NativeMerchantEconomyControls.Apply(rows,control.Split(':',2)[1]);
            else if(control.StartsWith("merchant-review:",StringComparison.Ordinal))NativeMerchantControls.Apply(rows,control.Split(':',2)[1]);
            else if(control.StartsWith("n3c-review:",StringComparison.Ordinal))NativeRewardControls.Apply(rows,control.Split(':',2)[1]);
            else if(control.StartsWith("room-review:",StringComparison.Ordinal))NativeRoomControls.Apply(rows,control.Split(':',2)[1]);
            else if(control.StartsWith("terminal-review:",StringComparison.Ordinal))NativeTerminalControls.Apply(rows,control.Split(':',2)[1]);
            else if(control=="header-schema")rows[0]["payload"]!["detail"]!["schema"]="unknown";
            else if(control.StartsWith("lifecycle-remove:",StringComparison.Ordinal)){
                string kind=control.Split(':',2)[1];
                if(!new[]{"terminal-stamp","battle-loss-confirmed","run-end-return","run-win-completed","act-transition-completed","tail-contract"}.Contains(kind))throw new ArgumentException(control);
                if(rows.RemoveAll(r=>Kind(r)==kind)==0)throw new InvalidOperationException("Lifecycle control target missing");
            }
            else if(control=="lifecycle-missing-rng"){
                foreach(var row in rows)if(row["payload"]!["detail"] is JsonObject d&&d["state"] is JsonObject state&&state["lifecycle"]?["eventModel"] is JsonObject model)model["rng"]=null;
            }
            else if(control=="lifecycle-false-contract")rows.First(r=>Kind(r)=="tail-contract")["payload"]!["detail"]!["facts"]!["valid"]=false;
            else if(control=="missing-state-field")rows.First(r=>Kind(r)=="new-run")["payload"]!["detail"]!["state"]!.AsObject().Remove("gold");
            else if(control=="missing-rng-field")rows.First(r=>Kind(r)=="new-run")["payload"]!["detail"]!["state"]!.AsObject().Remove("runRng");
            else if(control is "missing-all-state" or "missing-one-boundary-state"){
                int changed=0;
                foreach(var row in rows){
                    if(row["payload"]!["detail"] is JsonObject d&&d.ContainsKey("state")&&(control=="missing-all-state"||Kind(row)=="confirmed"&&changed==0)){d.Remove("state");changed++;}
                }
                if(changed==0)throw new InvalidOperationException("No snapshot mutated");
            }
            else if(control.StartsWith("snapshot-",StringComparison.Ordinal)){
                var parts=control.Split(':',2);if(parts.Length!=2||!SnapshotControlKind(parts[1]))throw new ArgumentException(control);
                var target=rows.FirstOrDefault(r=>Kind(r)==parts[1]);
                // Absent death/sink events are schema controls only, never natural lifecycle evidence.
                if(target==null){if(parts[1] is not ("death" or "sink-failure-trigger"))throw new InvalidOperationException("Missing native control source");target=rows.First(r=>Kind(r)=="new-run");target["payload"]!["kind"]=parts[1];}
                var detail=target["payload"]!["detail"]!.AsObject();
                switch(parts[0]){
                    case "snapshot-missing":detail.Remove("state");break;
                    case "snapshot-null":detail["state"]=null;break;
                    case "snapshot-array":detail["state"]=new JsonArray();break;
                    case "snapshot-string":detail["state"]="invalid";break;
                    case "snapshot-number":detail["state"]=1;break;
                    case "snapshot-boolean":detail["state"]=true;break;
                    default:throw new ArgumentException(control);
                }
            }
            else if(control is "null-run-rng" or "null-player-rng" or "null-event-rng"){
                string field=control=="null-run-rng"?"runRng":control=="null-player-rng"?"playerRng":"eventRng";
                foreach(var row in rows)if(row["payload"]!["detail"] is JsonObject d&&d["state"] is JsonObject st)st[field]=null;
            }
            else if(control=="false-fixture-contract"){foreach(var row in rows.Where(r=>Kind(r) is "fixture-assertion" or "fixture-contract"))row["payload"]!["detail"]!["facts"]!["valid"]=false;}
            else if(control=="wrong-fixture-scenario")rows.First(r=>Kind(r)=="fixture-contract")["payload"]!["detail"]!["facts"]!["name"]="other-fixture";
            else if(control=="missing-fixture-assertion"){var r=rows.First(r=>Kind(r)=="fixture-assertion");rows.Remove(r);}
            else if(control is "missing-history" or "missing-choice-return" or "missing-checkpoint" or "missing-root-request"){
                string kind=control=="missing-history"?"combat-history":control=="missing-choice-return"?"choice-returned":control=="missing-checkpoint"?"history-checkpoint":"root-request";rows.RemoveAll(r=>Kind(r)==kind);
            }
            else if(control=="wrong-choice-root")rows.First(r=>Kind(r)=="choice-resolved")["payload"]!["root"]="a1";
            else if(control=="wrong-task-root")rows.First(r=>Kind(r)=="task-completed")["payload"]!["root"]="a0";
            else if(control=="early-root-confirm"){
                var request=rows.First(r=>Kind(r)=="choice-requested");string root=request["payload"]!["root"]!.GetValue<string>();
                var confirm=rows.First(r=>Kind(r)=="confirmed"&&r["payload"]!["root"]!.GetValue<string>()==root);rows.Remove(confirm);
                int index=rows.FindIndex(r=>Kind(r)=="dispatch"&&r["payload"]!["root"]!.GetValue<string>()==root);rows.Insert(index+1,confirm);
            }
            else if(control is "wrong-choice-object" or "wrong-returned-object"){
                var first=rows.First(r=>Kind(r)=="choice-requested")["payload"]!["detail"]!["oracleOptions"]![0]!["representative"]!;
                rows.First(r=>Kind(r)==(control=="wrong-choice-object"?"choice-resolved":"choice-returned"))["payload"]!["detail"]!["objects"]![0]=first.DeepClone();
            }
            else if(control is "history-duplicate" or "history-reorder"){
                var indices=Enumerable.Range(0,rows.Count).Where(i=>Kind(rows[i])=="combat-history").Take(2).ToArray();
                var first=rows[indices[0]]["payload"]!.DeepClone();rows[indices[0]]["payload"]=rows[indices[1]]["payload"]!.DeepClone();
                rows[indices[1]]["payload"]=control=="history-duplicate"?rows[indices[0]]["payload"]!.DeepClone():first;
            }
            else if(control=="unknown-event")rows.Insert(rows.Count-1,JsonNode.Parse("{\"payload\":{\"schema\":\"native-event/v1\",\"kind\":\"unknown\",\"detail\":{}}}")!.AsObject());
            else if(control=="dangling"){rows.First(r=>Kind(r)=="choice-resolved")["payload"]!["detail"]!["objects"]![0]!["$ref"]="unknown";}
            else if(control=="missing-response")rows.RemoveAll(r=>Kind(r)=="choice-resolved");
            else if(control=="cleanup")rows.RemoveAll(r=>Kind(r)=="history-clearing");
            else if(control=="emptyrng"){
                var rng=rows.First(r=>Kind(r)=="new-run")["payload"]!["detail"]!["state"]!["runRng"]!["rngs"]!.AsObject();rng[rng.First().Key]=new JsonObject();
            }else if(control is "inner-fault" or "selection-fault-record"){
                string faultKind=control=="inner-fault"?"task-fault":"selection-fault";
                rows.Insert(rows.Count-1,JsonSerializer.SerializeToNode(new{payload=new{schema="native-event/v1",kind=faultKind,detail=new{}}})!.AsObject());
            }
            else throw new ArgumentException(control);
            var footer=rows.Last()["payload"]!["detail"]!;
            footer["priorCount"]=rows.Count-1;
            footer["counts"]=JsonSerializer.SerializeToNode(rows.SkipLast(1).GroupBy(Kind).ToDictionary(g=>g.Key,g=>g.Count()));
            string chain="";int seq=0;
            foreach(var row in rows){row["seq"]=++seq;row["prev"]=chain;string raw=JsonSerializer.Serialize(JsonSerializer.SerializeToElement(row["payload"]));chain=Hash(chain+raw);row["hash"]=chain;}
            var seal=JsonNode.Parse(File.ReadAllText(Path.Combine(directory,"trace-seal.json")))!;seal["count"]=rows.Count;seal["chain"]=chain;
            string text=string.Join('\n',rows.Select(r=>r.ToJsonString()))+"\n";seal["bytes"]=Encoding.UTF8.GetByteCount(text);
            File.WriteAllText(Path.Combine(directory,"trace-seal.json"),seal.ToJsonString());
            var reportPath=Path.Combine(directory,"result.json");var report=JsonNode.Parse(File.ReadAllText(reportPath))!;
            report["trace"]=seal.DeepClone();File.WriteAllText(reportPath,report.ToJsonString());
        }
        File.WriteAllText(path,string.Join('\n',rows.Select(r=>r.ToJsonString()))+"\n");return 0;
    }
}
