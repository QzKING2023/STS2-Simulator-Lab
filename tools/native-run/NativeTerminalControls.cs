using System.Text.Json.Nodes;

namespace OfflineSearchHarness;

// Persisted-evidence fault injection only, reached by --mutate on fresh Lab copies.
// NativeEvidence.Mutate subsequently rebuilds the ordinary chain/footer/seals.
internal static class NativeTerminalControls
{
    internal static void Apply(List<JsonObject> rows,string control)
    {
        static string Kind(JsonObject r)=>r["payload"]!["kind"]!.GetValue<string>();
        JsonNode Target(string kind)=>rows.First(r=>Kind(r)==kind)["payload"]!;
        JsonNode State(string kind)=>Target(kind)["detail"]!["state"]!;
        JsonNode Facts(string kind)=>Target(kind)["detail"]!["facts"]!;
        var states=rows.Select(r=>r["payload"]!["detail"] as JsonObject).Where(d=>d?["state"] is JsonObject).Select(d=>d!["state"]!).ToArray();
        if(control.StartsWith("confirmed-state|",StringComparison.Ordinal)){
            var parts=control.Split('|');var s=State(parts[1]);
            switch(parts[2]){
                case "block":s["block"]=s["block"]!.GetValue<int>()+1;break;
                case "deck-upgrade":s["deck"]![0]!["upgrade"]=1;break;
                case "deck-instance":
                    var id=s["deck"]![0]!["id"]!.DeepClone();s["deck"]![0]!["id"]=s["deck"]![1]!["id"]!.DeepClone();s["deck"]![1]!["id"]=id;break;
                case "player-rng":s["playerRng"]!["rngs"]!["Rewards"]!["s0"]=s["playerRng"]!["rngs"]!["Rewards"]!["s0"]!.GetValue<ulong>()+1;break;
                case "added-field":s["futureStateField"]=new JsonObject{["value"]=1};break;
                case "lifecycle-extra":s["lifecycle"]!["futureStateField"]=1;break;
                case "missing-extension":
                    foreach(var st in states)st["futureStateField"]=new JsonObject{["value"]=1};s.AsObject().Remove("futureStateField");break;
                default:throw new ArgumentException("Unknown confirmed state control: "+control);
            }
            return;
        }
        switch(control){
            case "no-change":break;
            case "equivalent-state-property-order":
                void ReverseObjects(JsonNode? node){
                    if(node is JsonObject obj){foreach(var child in obj.ToArray())ReverseObjects(child.Value);var fields=obj.Select(p=>(p.Key,Value:p.Value?.DeepClone())).Reverse().ToArray();obj.Clear();foreach(var p in fields)obj.Add(p.Key,p.Value);}
                    else if(node is JsonArray array)foreach(var child in array)ReverseObjects(child);
                }
                foreach(var s in states)ReverseObjects(s);break;
            case "equivalent-state-extension":
                foreach(var s in states)s["futureStateField"]=new JsonObject{["value"]=1};break;
            case "equivalent-property-order":
                foreach(var s in states){var n=s["lifecycle"]!["native"]!.AsObject();var fields=n.Select(p=>(p.Key,Value:p.Value?.DeepClone())).Reverse().ToArray();n.Clear();foreach(var p in fields)n.Add(p.Key,p.Value);}
                var native=Facts("terminal-stamp")["native"]!.AsObject();var reversed=native.Select(p=>(p.Key,Value:p.Value?.DeepClone())).Reverse().ToArray();native.Clear();foreach(var p in reversed)native.Add(p.Key,p.Value);break;
            case "death-stamp-alive":State("terminal-stamp")["hp"]=80;break;
            case "architect-zero-authority":
                void Zero(JsonNode node){foreach(var p in node.AsObject().ToArray())node[p.Key]=p.Key is "Won" or "Closed"?JsonValue.Create(false):JsonValue.Create(0);}
                foreach(var s in states)Zero(s["lifecycle"]!["native"]!);Zero(Facts("terminal-stamp")["native"]!);break;
            case "terminal-contract-wrong-terminal":Facts("terminal-contract")["terminal"]="Death";break;
            case "architect-missing-event-identity":
                foreach(var s in states)if(s["lifecycle"]!["eventModel"] is JsonObject ev){ev.Remove("reference");ev.Remove("model");ev.Remove("IsFinished");}break;
            case "next-act-snapshot-contradiction":State("tail-contract")["act"]=0;State("tail-contract")["room"]="CombatRoom";break;
            case "architect-detached-lifecycle":
                foreach(var r in rows.Where(r=>Kind(r) is "run-win-start" or "run-end-start" or "run-end-return" or "run-win-completed" or "terminal-stamp" or "terminal-contract" or "tail-contract")){r["payload"]!["root"]="missing-root";r["payload"]!["context"]="missing-context";}break;
            case "terminal-contract-before-native-win":
                var contract=rows.First(r=>Kind(r)=="terminal-contract");rows.Remove(contract);rows.Insert(rows.FindIndex(r=>Kind(r)=="run-win-start"),contract);break;
            case "duplicate-terminal-stamp":
                int i=rows.FindIndex(r=>Kind(r)=="terminal-stamp");rows.Insert(i,rows[i].DeepClone().AsObject());break;
            case "missing-terminal-state":Target("terminal-stamp")["detail"]!.AsObject().Remove("state");break;
            case "null-win-state":Target("run-win-completed")["detail"]!["state"]=null;break;
            case "nonobject-transition-state":Target("act-transition-completed")["detail"]!["state"]=new JsonArray();break;
            case "missing-terminal-counter":State("terminal-stamp")["lifecycle"]!["native"]!.AsObject().Remove("WinReturns");break;
            case "wrong-win-context":Target("run-win-completed")["context"]="other-context";break;
            case "wrong-stamp-facts":Facts("terminal-stamp")["native"]!["WinReturns"]=0;break;
            case "unclosed-terminal-state":State("terminal-contract")["lifecycle"]!["native"]!["Closed"]=false;break;
            case "unfinished-architect":State("terminal-stamp")["lifecycle"]!["eventModel"]!["IsFinished"]=false;break;
            case "wrong-event-model":State("terminal-stamp")["lifecycle"]!["eventModel"]!["model"]="EVENT.NEOW";break;
            case "wrong-registered-event-instance":
                State("terminal-stamp")["lifecycle"]!["eventModel"]!["reference"]=State("new-run")["deck"]![0]!["id"] is {} cardId?new JsonObject{["$ref"]=cardId.DeepClone()}:throw new InvalidOperationException();break;
            case "duplicate-win-completion":
                int index=rows.FindIndex(r=>Kind(r)=="run-win-completed");rows.Insert(index,rows[index].DeepClone().AsObject());break;
            case "early-terminal-stamp":
                var stamp=rows.First(r=>Kind(r)=="terminal-stamp");rows.Remove(stamp);rows.Insert(rows.FindIndex(r=>Kind(r)=="confirmed"&&r["payload"]!["root"]!.GetValue<string>()==stamp["payload"]!["root"]!.GetValue<string>()),stamp);break;
            case "post-terminal-dispatch":
                var dispatch=rows.First(r=>Kind(r)=="dispatch");rows.Insert(rows.FindIndex(r=>Kind(r)=="terminal-contract")+1,dispatch.DeepClone().AsObject());break;
            case "alive-win-completion":State("run-win-completed")["hp"]=80;break;
            case "alive-terminal-continuation":State("terminal-contract")["hp"]=80;break;
            case "lost-pre-cleanup-hp":State("run-end-return")["hp"]=0;break;
            case "architect-stamp-wrong-act":State("terminal-stamp")["act"]=0;break;
            case "death-stamp-gold-created":State("terminal-stamp")["gold"]=State("terminal-stamp")["gold"]!.GetValue<int>()+1;break;
            case "architect-contract-rng-changed":
                State("terminal-contract")["runRng"]!["rngs"]!["Shuffle"]!["s0"]=State("terminal-contract")["runRng"]!["rngs"]!["Shuffle"]!["s0"]!.GetValue<ulong>()+1;break;
            case "next-act-tail-rng-changed":
                State("tail-contract")["runRng"]!["rngs"]!["Shuffle"]!["s0"]=State("tail-contract")["runRng"]!["rngs"]!["Shuffle"]!["s0"]!.GetValue<ulong>()+1;break;
            case "death-completion-wrong-root":Target("battle-loss-confirmed")["root"]="a1";break;
            case "win-completion-before-end-return":
                var completed=rows.First(r=>Kind(r)=="run-win-completed");rows.Remove(completed);rows.Insert(rows.FindIndex(r=>Kind(r)=="run-end-return"),completed);break;
            case "duplicate-act-completion":
                int act=rows.FindIndex(r=>Kind(r)=="act-transition-completed");rows.Insert(act,rows[act].DeepClone().AsObject());break;
            default:throw new ArgumentException("Unknown terminal review control: "+control);
        }
    }
}
