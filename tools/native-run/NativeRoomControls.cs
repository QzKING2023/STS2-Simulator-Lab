using System.Text.Json.Nodes;
namespace OfflineSearchHarness;
internal static class NativeRoomControls
{
    internal static void Apply(List<JsonObject> rows,string control)
    {
        string K(JsonObject r)=>r["payload"]!["kind"]!.GetValue<string>();
        JsonNode P(string k)=>rows.First(r=>K(r)==k)["payload"]!;
        JsonNode S(string k)=>P(k)["detail"]!["state"]!;
        JsonNode F(string k)=>P(k)["detail"]!["facts"]!;
        void Duplicate(string k){int i=rows.FindIndex(r=>K(r)==k);rows.Insert(i,rows[i].DeepClone().AsObject());}
        if(control.StartsWith("repair|",StringComparison.Ordinal)){
            var parts=control.Split('|');string op=parts[1];
            if(op=="remove")rows.RemoveAll(r=>K(r)==parts[2]);
            else if(op=="duplicate")Duplicate(parts[2]);
            else if(op=="detach"){P(parts[2])["root"]="a0";P(parts[2])["context"]="missing-context";}
            else if(op=="state"){
                var s=S(parts[2]);
                switch(parts[3]){
                    case "gold":s["gold"]=999;break;
                    case "deck-upgrade":s["deck"]![0]!["upgrade"]=99;break;
                    case "rng":s["runRng"]!["rngs"]!["Shuffle"]!["s0"]=s["runRng"]!["rngs"]!["Shuffle"]!["s0"]!.GetValue<ulong>()^1UL;break;
                    case "player-rng":s["playerRng"]!["rngs"]!["Rewards"]!["s0"]=s["playerRng"]!["rngs"]!["Rewards"]!["s0"]!.GetValue<ulong>()^1UL;break;
                    case "extension":s["futureStateField"]=new JsonObject{["value"]=1};break;
                    case "missing-extension":
                        foreach(var r in rows)if(r["payload"]!["detail"] is JsonObject d&&d["state"] is JsonObject st)st["futureStateField"]=new JsonObject{["value"]=1};
                        s.AsObject().Remove("futureStateField");break;
                    default:throw new ArgumentException(control);
                }
            }
            else if(op=="early-boundary"){
                var boundary=rows.First(r=>K(r)=="room-option-boundary");rows.Remove(boundary);
                rows.Insert(rows.FindIndex(r=>K(r)=="confirmed"&&r["payload"]!["root"]!.GetValue<string>()==boundary["payload"]!["root"]!.GetValue<string>()),boundary);
            }
            else if(op=="stale-boundary"||op=="stale-return"){
                var items=rows.Where(r=>K(r)==(op=="stale-boundary"?"room-option-boundary":"room-option-return")).ToArray();
                items[1]["payload"]!["detail"]!["facts"]!["request"]=items[0]["payload"]!["detail"]!["facts"]!["request"]!.DeepClone();
                items[1]["payload"]!["detail"]!["facts"]!["success"]=false;
            }
            else if(op=="wrong-session")F("room-option-boundary")["session"]="old-session";
            else if(op=="cross-set"){
                string root=rows.First(r=>K(r)=="dispatch"&&r["payload"]!["detail"]!["facts"]!["kind"]!.GetValue<string>()=="SkipRewards")["payload"]!["root"]!.GetValue<string>();
                foreach(var r in rows.Where(r=>r["payload"]!["root"]!.GetValue<string>()==root)){
                    var p=r["payload"]!;
                    if(K(r)=="root-request")p["detail"]!["action"]!["decision"]!["setId"]=999;
                    if(K(r)=="dispatch")p["detail"]!["facts"]!["decision"]!["setId"]=999;
                    if(K(r)=="confirmed")p["detail"]!["facts"]!["action"]!["decision"]!["setId"]=999;
                }
            }
            else if(op=="early-rewards"){
                var completion=rows.First(r=>K(r)=="rewards-completed");rows.Remove(completion);
                rows.Insert(rows.FindIndex(r=>K(r)=="confirmed"&&r["payload"]!["root"]!.GetValue<string>()==completion["payload"]!["root"]!.GetValue<string>()),completion);
            }
            else if(op=="unclosed-rewards"){
                var completion=rows.First(r=>K(r)=="rewards-completed");rows.Remove(completion);
                var gold=rows.First(r=>K(r)=="confirmed"&&r["payload"]!["detail"]!["facts"]!["action"]!["kind"]!.GetValue<string>()=="TakeGold");
                completion["payload"]!["root"]=gold["payload"]!["root"]!.DeepClone();completion["payload"]!["context"]=gold["payload"]!["context"]!.DeepClone();
                completion["payload"]!["detail"]!["state"]=gold["payload"]!["detail"]!["state"]!.DeepClone();
                rows.Insert(rows.IndexOf(gold)+1,completion);
            }
            else if(op=="equivalent-extension"){
                foreach(var r in rows)if(r["payload"]!["detail"] is JsonObject d&&d["state"] is JsonObject st)st["futureStateField"]=new JsonObject{["value"]=1};
            }
            else throw new ArgumentException(control);
            return;
        }
        switch(control){
            case "no-change":break;
            case "property-order":NativeTerminalControls.Apply(rows,"equivalent-state-property-order");break;
            case "missing-room-state":S("room-entered").AsObject().Remove("rooms");break;
            case "null-event-rng":S("room-option-return")["lifecycle"]!["eventModel"]!["rng"]=null;break;
            case "wrong-event-identity":S("room-option-return")["lifecycle"]!["eventModel"]!["reference"]=S("room-entered")["rooms"]!["reference"]!.DeepClone();break;
            case "wrong-instance":F("room-complete")["room"]=new JsonObject{["$ref"]=S("new-run")["deck"]![0]!["id"]!.DeepClone()};break;
            case "duplicate-complete":Duplicate("room-complete");break;
            case "duplicate-native-return":Duplicate("room-option-return");break;
            case "missing-exit-return":rows.RemoveAll(r=>K(r)=="room-exit-return");break;
            case "missing-option-return":rows.RemoveAll(r=>K(r)=="room-option-return");break;
            case "missing-assertion":rows.RemoveAll(r=>K(r)=="room-assertion");break;
            case "missing-candidates":rows.RemoveAll(r=>K(r)=="room-candidates");break;
            case "false-assertion":F("room-assertion")["valid"]=false;break;
            case "static-new-field":S("room-option-boundary")["newStateField"]=123;break;
            case "detached-return":P("room-option-return")["root"]="other-root";break;
            case "wrong-boundary-finished":F("room-option-boundary")["eventFinished"]=true;break;
            case "fake-cancel-success":F("room-option-return")["success"]=true;F("room-option-boundary")["success"]=true;break;
            case "wrong-dispatch":
                string root=P("room-option-start")["root"]!.GetValue<string>();
                foreach(var row in rows.Where(r=>r["payload"]!["root"]!.GetValue<string>()==root)){
                    var p=row["payload"]!;
                    if(K(row)=="root-request")p["detail"]!["action"]!["decision"]!["index"]=0;
                    if(K(row)=="dispatch")p["detail"]!["facts"]!["decision"]!["index"]=0;
                    if(K(row)=="confirmed")p["detail"]!["facts"]!["action"]!["decision"]!["index"]=0;
                }break;
            case "coherent-wrong-heal":
                bool started=false;
                foreach(var row in rows){
                    if(K(row)=="room-option-return")started=true;
                    if(started&&row["payload"]!["detail"] is JsonObject d&&d["state"] is JsonObject state&&state["hp"]!.GetValue<int>()==64)state["hp"]=65;
                }
                F("room-assertion")["after"]!["hp"]=65;break;
            case "unfinished-event-complete":
                int at=rows.FindIndex(r=>K(r)=="room-option-boundary");var insert=P("room-leave-ready").DeepClone();var boundary=rows[at]["payload"]!;
                insert["root"]=boundary["root"]!.DeepClone();insert["context"]=boundary["context"]!.DeepClone();insert["detail"]!["state"]=boundary["detail"]!["state"]!.DeepClone();
                rows.Insert(at+1,new JsonObject{["payload"]=insert});break;
            case "early-complete":
                var complete=rows.First(r=>K(r)=="room-leave-ready");rows.Remove(complete);rows.Insert(rows.FindIndex(r=>K(r)=="room-option-return"),complete);break;
            case "missing-rewards":
                int reward=rows.FindIndex(r=>K(r)=="rewards-completed");rows.RemoveAt(reward);break;
            case "wrong-persistent":F("room-slice-complete")["persistent"]=new JsonObject{["$ref"]=S("new-run")["deck"]![0]!["id"]!.DeepClone()};break;
            default:throw new ArgumentException("Unknown room control: "+control);
        }
    }
}
