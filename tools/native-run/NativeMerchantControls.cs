using System.Text.Json.Nodes;
namespace OfflineSearchHarness;
internal static class NativeMerchantControls
{
    internal static void Apply(List<JsonObject> rows,string control)
    {
        static string Kind(JsonObject r)=>r["payload"]!["kind"]!.GetValue<string>();
        JsonObject Find(string kind)=>rows.First(r=>Kind(r)==kind);
        JsonObject Buy()=>rows.First(r=>Kind(r)=="n3d-return"&&r["payload"]!["detail"]!["facts"]!["kind"]!.GetValue<string>()=="MerchantBuy");
        static JsonObject Facts(JsonObject r)=>r["payload"]!["detail"]!["facts"]!.AsObject();
        static JsonObject State(JsonObject r)=>r["payload"]!["detail"]!["state"]!.AsObject();
        var parts=control.Split('|');
        if(parts.Length>=3&&parts[0]=="repair"){
            if(parts[1]=="choice"){
                var op=rows.First(r=>Kind(r)=="n3d-open"&&Facts(r)["kind"]!.GetValue<string>()=="MerchantRemove");
                string root=op["payload"]!["root"]!.GetValue<string>();
                var children=rows.Where(r=>r["payload"]!["root"]!.GetValue<string>()==root&&Kind(r).StartsWith("choice-")).ToArray();
                JsonObject Child(string k)=>children.Single(r=>Kind(r)==k);
                var change=parts[2];
                if(change=="missing-all")foreach(var row in children)rows.Remove(row);
                else if(change.StartsWith("missing-"))rows.Remove(Child("choice-"+change[8..]));
                else if(change.StartsWith("duplicate-")){var row=Child("choice-"+change[10..]);rows.Insert(rows.IndexOf(row),row.DeepClone().AsObject());}
                else if(change is "wrong-selected" or "nonempty-cancel"){
                    var first=Child("choice-requested")["payload"]!["detail"]!["oracleOptions"]![0]!["representative"]!;
                    Child("choice-resolved")["payload"]!["detail"]!["selected"]=new JsonArray("o0");
                    foreach(var k in new[]{"choice-resolved","choice-returned"})Child(k)["payload"]!["detail"]!["objects"]=new JsonArray(first.DeepClone());
                }else if(change is "before-open" or "after-mark-hook" or "after-return"){
                    foreach(var row in children)rows.Remove(row);
                    var target=change=="before-open"?op:rows.First(r=>r["payload"]!["root"]!.GetValue<string>()==root&&Kind(r)==(change=="after-return"?"n3d-return":"n3d-removal-hook"));
                    rows.InsertRange(rows.IndexOf(target)+(change=="before-open"?0:1),children);
                }else if(change is "extra-complete" or "borrow-for-repeat"){
                    var extra=children.Select(r=>r.DeepClone().AsObject()).ToArray();
                    foreach(var row in extra)row["payload"]!["detail"]!["id"]="q-extra";
                    var target=change=="extra-complete"?rows.First(r=>Kind(r)=="n3d-removal-marked"):rows.Last(r=>Kind(r)=="n3d-return"&&Facts(r)["kind"]!.GetValue<string>()=="MerchantRemove");
                    if(change=="borrow-for-repeat")foreach(var row in extra){row["payload"]!["root"]=target["payload"]!["root"]!.DeepClone();if(Kind(row)=="choice-requested"){row["payload"]!["detail"]!["root"]=target["payload"]!["root"]!.DeepClone();row["payload"]!["detail"]!["parent"]=target["payload"]!["root"]!.DeepClone();}}
                    rows.InsertRange(rows.IndexOf(target),extra);
                }else{
                    var request=Child("choice-requested")["payload"]!["detail"]!;
                    switch(change){
                        case "wrong-source":request["source"]="FromDeckForUpgrade";break;
                        case "wrong-parent":request["parent"]="a1";break;
                        case "wrong-context":foreach(var row in children)row["payload"]!["context"]="other";break;
                        case "wrong-result-id":Child("choice-returned")["payload"]!["detail"]!["id"]="q-other";break;
                        case "wrong-min":request["min"]=1;break;
                        case "wrong-candidate-model":request["options"]![1]!["model"]="CARD.BASH";break;
                        case "wrong-candidate-upgrade":request["options"]![1]!["upgrade"]=1;break;
                        case "wrong-candidate-instance":request["oracleOptions"]![1]!["representative"]=request["oracleOptions"]![0]!["representative"]!.DeepClone();request["oracleOptions"]![1]!["objects"]=request["oracleOptions"]![0]!["objects"]!.DeepClone();break;
                        default:throw new ArgumentException(control);
                    }
                }
            }else if(parts[1]=="phase"){
                foreach(var row in rows.Where(r=>Kind(r) is "n3d-removal-marked" or "n3d-removal-hook"))switch(parts[2]){
                    case "gold":State(row)["gold"]=1249;break;
                    case "hp":State(row)["hp"]=0;break;
                    case "rng":State(row)["playerRng"]!["rngs"]!["Shops"]!["counter"]=State(row)["playerRng"]!["rngs"]!["Shops"]!["counter"]!.GetValue<int>()+1;break;
                    case "extension":State(row)["independentExtension"]=new JsonObject{["value"]=1};break;
                    case "deck":State(row)["deck"]!.AsArray().RemoveAt(0);break;
                    case "removals":State(row)["merchant"]!["removals"]=0;break;
                    default:throw new ArgumentException(control);
                }
            }else if(parts.Length==4){
                var row=rows.First(r=>Kind(r)==parts[2]&&Facts(r)["kind"]?.GetValue<string>()==parts[1]);
                switch(parts[3]){
                    case "missing":rows.Remove(row);break;
                    case "duplicate":rows.Insert(rows.IndexOf(row),row.DeepClone().AsObject());break;
                    case "detach":row["payload"]!["root"]="a999";break;
                    case "wrong-context":row["payload"]!["context"]="other";break;
                    case "drift":State(row)["independentExtension"]=new JsonObject{["value"]=1};break;
                    case "wrong-id":Facts(row)["id"]="m999";break;
                    case "extra-choice":{
                        string root=row["payload"]!["root"]!.GetValue<string>(),context=row["payload"]!["context"]!.GetValue<string>();
                        var card=State(row)["deck"]![0]!;string candidate=card["id"]!.GetValue<string>();
                        JsonObject Choice(string k,object detail)=>System.Text.Json.JsonSerializer.SerializeToNode(new{payload=new{schema="native-event/v1",kind=k,root,context,detail}})!.AsObject();
                        var reference=new Dictionary<string,string>{{"$ref",candidate}};
                        var options=new[]{new{handle="o0",model=card["model"]!.GetValue<string>(),upgrade=card["upgrade"]!.GetValue<int>()}};
                        var mapping=new[]{new{handle="o0",representative=reference,objects=new[]{reference}}};
                        var request=Choice("choice-requested",new{id="q-extra",root,context,parent=root,source="FromDeckGeneric",completion="method-return",transport="native-selector",implicitChoice=false,min=0,max=1,options,oracleOptions=mapping});
                        var resolve=Choice("choice-resolved",new{id="q-extra",selected=Array.Empty<string>(),implicitChoice=false,objects=Array.Empty<object>()});
                        var returned=Choice("choice-returned",new{id="q-extra",objects=Array.Empty<object>()});
                        rows.InsertRange(rows.IndexOf(row)+1,[request,resolve,returned]);break;
                    }
                    default:throw new ArgumentException(control);
                }
            }else throw new ArgumentException(control);
        }
        else if(parts.Length==2){
            var row=Find(parts[1]);var detail=row["payload"]!["detail"]!.AsObject();
            switch(parts[0]){
                case "remove":rows.Remove(row);break;
                case "duplicate":rows.Insert(rows.IndexOf(row),row.DeepClone().AsObject());break;
                case "missing-state":detail.Remove("state");break;
                case "null-state":detail["state"]=null;break;
                case "array-state":detail["state"]=new JsonArray();break;
                case "drift":State(row)["independentExtension"]=new JsonObject{["value"]=1};break;
                case "detach":row["payload"]!["root"]="a999";break;
                case "wrong-id":Facts(row)["id"]="m999";break;
                default:throw new ArgumentException(control);
            }
        }else switch(control){
            case "false-contract":Facts(Find("n3d-contract"))["valid"]=false;break;
            case "wrong-operation-count":Facts(Find("n3d-contract"))["operations"]=0;break;
            case "false-success":Facts(Buy())["result"]!["success"]=false;break;
            case "wrong-cost":Facts(Buy())["result"]!["cost"]=0;break;
            case "wrong-spent":Facts(Buy())["result"]!["spent"]=0;break;
            case "wrong-completion":Facts(Buy())["result"]!["completions"]=0;break;
            case "wrong-failure":Facts(Buy())["result"]!["failure"]="FailureSpace";break;
            case "old-acquired":Facts(Buy())["result"]!["acquired"]=new JsonObject{["$ref"]=State(Buy())["deck"]![0]!["id"]!.GetValue<string>()};break;
            case "missing-new-object":Facts(Buy())["result"]!["newCards"]=new JsonArray();break;
            case "wrong-content":Facts(Buy())["result"]!["offered"]=new JsonObject{["$ref"]=State(Buy())["deck"]![0]!["id"]!.GetValue<string>()};break;
            case "missing-removed":Facts(rows.First(r=>Kind(r)=="n3d-return"&&Facts(r)["kind"]!.GetValue<string>()=="MerchantRemove"))["result"]!["removed"]=new JsonArray();break;
            case "missing-used":{
                var row=rows.First(r=>Kind(r)=="n3d-return"&&Facts(r)["kind"]!.GetValue<string>()=="MerchantRemove");
                State(row)["merchant"]!["entries"]!.AsArray().Single(n=>n!["kind"]!.GetValue<string>()=="MerchantCardRemovalEntry")!["used"]=false;break;}
            case "wrong-removal-source":{
                var row=rows.First(r=>Kind(r)=="n3d-return"&&Facts(r)["kind"]!.GetValue<string>()=="MerchantRemove");Facts(row)["result"]!["completion"]="UI presumed";break;}
            case "return-before-open":{
                var row=Find("n3d-return");rows.Remove(row);rows.Insert(rows.IndexOf(Find("n3d-open")),row);break;}
            case "coherent-entry-cost":case "coherent-entry-extension":{
                var buy=Buy();var selected=rows.First(r=>Kind(r)=="dispatch"&&r["payload"]!["root"]!.GetValue<string>()==buy["payload"]!["root"]!.GetValue<string>())["payload"]!["detail"]!["facts"]!["decision"]!["entry"]!.ToJsonString();
                foreach(var row in rows.Skip(rows.IndexOf(buy)))if(row["payload"]?["detail"]?["state"]?["merchant"] is JsonObject merchant){
                    var entry=merchant["entries"]!.AsArray().Single(e=>e!["reference"]!.ToJsonString()==selected)!.AsObject();
                    if(control=="coherent-entry-cost")entry["cost"]=entry["cost"]!.GetValue<int>()+1;else entry["independentExtension"]=new JsonObject{["value"]=1};
                }break;}
            case "property-order":foreach(var row in rows)Reverse(row);break;
            case "equivalent-extension":foreach(var row in rows)if(row["payload"]?["detail"]?["state"] is JsonObject state)state["independentExtension"]=new JsonObject{["value"]=1};break;
            default:throw new ArgumentException(control);
        }
    }
    static void Reverse(JsonNode node)
    {
        if(node is JsonObject o){var properties=o.Select(p=>(p.Key,p.Value)).Reverse().ToArray();o.Clear();foreach(var (key,value) in properties){o[key]=value;if(value!=null)Reverse(value);}}
        else if(node is JsonArray a)foreach(var child in a)if(child!=null)Reverse(child);
    }
}
