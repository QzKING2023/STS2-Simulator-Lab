using System.Text.Json.Nodes;
namespace OfflineSearchHarness;
internal static class NativeMerchantEconomyControls
{
    internal static void Apply(List<JsonObject> rows,string control)
    {
        static string Kind(JsonObject r)=>r["payload"]!["kind"]!.GetValue<string>();
        static JsonNode Facts(JsonObject r)=>r["payload"]!["detail"]!["facts"]!;
        static JsonNode State(JsonObject r)=>r["payload"]!["detail"]!["state"]!;
        if(control.StartsWith("phase-")){
            string name=control[6..];var prices=rows.Where(x=>Kind(x) is "merchant-price-start" or "merchant-price").ToArray();
            string root=prices[0]["payload"]!["root"]!.GetValue<string>();
            var stats=rows.Where(x=>Kind(x) is "merchant-stats-start" or "merchant-stats-return"&&x["payload"]!["root"]!.GetValue<string>()==root).ToArray();
            if(name=="prices-after-room-return"){
                var end=rows.Single(x=>Kind(x)=="room-exit-return"&&x["payload"]!["root"]!.GetValue<string>()==root);
                foreach(var row in prices)rows.Remove(row);rows.InsertRange(rows.IndexOf(end)+1,prices);
            }else{
                var moved=name=="stats-return-after-price-start"?stats.Skip(1).ToArray():stats;
                if(name=="duplicate-stats-after-prices")moved=moved.Select(x=>x.DeepClone().AsObject()).ToArray();
                else foreach(var row in moved)rows.Remove(row);
                var anchor=name switch{
                    "stats-after-prices" or "duplicate-stats-after-prices"=>prices[^1],
                    "stats-after-first-price"=>prices[1],
                    "stats-return-after-price-start"=>prices[0],
                    _=>throw new ArgumentException(control)
                };
                rows.InsertRange(rows.IndexOf(anchor)+1,moved);
            }
            return;
        }
        if(control.StartsWith("owner-")){
            string name=control[6..];var oldRoom=Facts(rows.First(x=>Kind(x)=="room-entered"))["room"]!;
            if(name.StartsWith("attach-")){
                string mode=name[7..];string root=rows.First(x=>Kind(x)=="n3d-open"&&Facts(x)["kind"]!.GetValue<string>()=="MerchantAttach")["payload"]!["root"]!.GetValue<string>();
                foreach(var row in rows.Where(x=>x["payload"]!["root"]!.GetValue<string>()==root)){
                    JsonNode? decision=Kind(row) switch{
                        "root-request"=>row["payload"]!["detail"]!["action"]!["decision"],
                        "dispatch" or "n3d-open"=>Facts(row)["decision"],
                        "confirmed"=>Facts(row)["action"]!["decision"],_=>null
                    };
                    if(decision is not null)SetOwner(decision,"room",mode,oldRoom);
                }
            }else if(name.StartsWith("projection-")){
                string mode=name[11..];
                foreach(var row in rows){
                    if(row["payload"]!["detail"] is JsonObject d&&d["state"] is JsonObject state&&state["merchant"] is JsonObject merchant)SetOwner(merchant,"room",mode,oldRoom);
                    if(Kind(row) is "merchant-exit-start" or "merchant-exit-return")SetOwner(Facts(row),"room",mode,oldRoom);
                }
            }else if(name is "equivalent-reference-renaming" or "generation-old-room" or "inventory-old-room"){
                string field=name=="inventory-old-room"?"inventory":"room";
                var room=field=="room"?Facts(rows.First(x=>Kind(x)=="room-entered"&&Facts(x)["type"]!.GetValue<string>()=="MerchantRoom"))["room"]!["$ref"]!.GetValue<string>():State(rows.First(x=>Kind(x)=="n3d-return"&&Facts(x)["kind"]!.GetValue<string>()=="MerchantAttach"))["merchant"]!["reference"]!["$ref"]!.GetValue<string>();
                string replacement=name=="equivalent-reference-renaming"?"review-merchant-room":oldRoom["$ref"]!.GetValue<string>();
                void Rename(JsonNode node){
                    if(node is JsonObject obj){
                        if(obj["$ref"] is JsonValue v&&v.TryGetValue<string>(out var id)&&id==room)obj["$ref"]=replacement;
                        foreach(var value in obj.ToArray())if(value.Value is not null)Rename(value.Value);
                    }else if(node is JsonArray array)foreach(var value in array)if(value is not null)Rename(value);
                }
                foreach(var row in rows){
                    if(name=="equivalent-reference-renaming"&&Kind(row)=="identity"&&row["payload"]!["detail"]!["id"]!.GetValue<string>()==room)row["payload"]!["detail"]!["id"]=replacement;
                    Rename(row);
                }
            }else if(name=="equivalent-room-order"){
                foreach(var row in rows)if(row["payload"]!["detail"] is JsonObject d&&d["state"] is JsonObject state)
                    foreach(string field in new[]{"rooms","merchant"})if(state[field] is JsonObject obj)state[field]=new JsonObject(obj.Reverse().Select(x=>KeyValuePair.Create(x.Key,x.Value?.DeepClone())));
            }else throw new ArgumentException(control);
            return;
        }
        if(control.StartsWith("binding-")){
            string name=control[8..];
            if(name.StartsWith("price-")){
                var bits=name.Split('-');bool all=bits[1]=="all";string field=bits[2];
                var selected=rows.Where(x=>Kind(x) is "merchant-price-start" or "merchant-price");if(!all)selected=selected.Take(2);
                foreach(var row in selected){var state=State(row);switch(field){
                    case "gold":state["gold"]=9999;break;
                    case "hp":state["hp"]=0;break;
                    case "deck":state["deck"]=new JsonArray();break;
                    case "relics":state["relics"]=new JsonArray();break;
                    case "extension":state["futureEconomicState"]=new JsonObject{["nested"]=1};break;
                    case "rng":state["playerRng"]!["rngs"]!["Rewards"]!["counter"]=state["playerRng"]!["rngs"]!["Rewards"]!["counter"]!.GetValue<long>()+1;break;
                    default:throw new ArgumentException(control);
                }}
            }else if(name.StartsWith("stats-")){
                var pairs=rows.Where(x=>Kind(x) is "merchant-stats-start" or "merchant-stats-return").ToArray();
                if(name=="stats-missing-first"){var root=pairs[0]["payload"]!["root"]!.GetValue<string>();rows.RemoveAll(x=>pairs.Contains(x)&&x["payload"]!["root"]!.GetValue<string>()==root);}
                else if(name=="stats-missing-approach"){string root=rows.First(x=>Kind(x)=="merchant-exit-start")["payload"]!["root"]!.GetValue<string>();rows.RemoveAll(x=>pairs.Contains(x)&&x["payload"]!["root"]!.GetValue<string>()!=root);}
                else if(name=="stats-missing-all")rows.RemoveAll(pairs.Contains);
                else if(name=="stats-duplicate-pair"){int i=rows.IndexOf(pairs[1]);rows.InsertRange(i+1,pairs.Take(2).Select(x=>x.DeepClone().AsObject()));}
                else if(name=="stats-after-exit"){var end=rows.First(x=>Kind(x)=="room-exit-return");var root=end["payload"]!["root"]!.GetValue<string>();var group=pairs.Where(x=>x["payload"]!["root"]!.GetValue<string>()==root).ToArray();foreach(var row in group)rows.Remove(row);rows.InsertRange(rows.IndexOf(end)+1,group);}
                else throw new ArgumentException(control);
            }else if(name.StartsWith("arrival-")){
                var arrivals=rows.Where(x=>Kind(x)=="room-entered").ToArray();
                if(name=="arrival-missing-first")rows.Remove(arrivals[0]);
                else if(name=="arrival-missing-last")rows.Remove(arrivals[^1]);
                else if(name=="arrival-missing-all")rows.RemoveAll(x=>Kind(x)=="room-entered");
                else if(name=="arrival-and-completion-missing-last"){var root=arrivals[^1]["payload"]!["root"]!.GetValue<string>();rows.RemoveAll(x=>Kind(x) is "room-entered" or "room-complete"&&x["payload"]!["root"]!.GetValue<string>()==root);}
                else throw new ArgumentException(control);
            }else if(name.StartsWith("travel-result-")){
                var result=Facts(rows.First(x=>Kind(x)=="n3d-return"&&Facts(x)["kind"]!.GetValue<string>()=="MerchantTravel"))["result"]!;
                switch(name[14..]){
                    case "from":result["from"]="wrong";break;
                    case "to":result["to"]="wrong";break;
                    case "closed":result["closed"]=false;break;
                    case "null":result["from"]=null;break;
                    case "type":result["to"]=new JsonArray();break;
                    case "missing-from":result.AsObject().Remove("from");break;
                    case "missing-to":result.AsObject().Remove("to");break;
                    case "extra":result["unexpected"]=1;break;
                    default:throw new ArgumentException(control);
                }
            }else if(name=="equivalent-price-order"){
                foreach(var row in rows.Where(x=>Kind(x) is "merchant-price-start" or "merchant-price")){var state=State(row).AsObject();row["payload"]!["detail"]!["state"]=new JsonObject(state.Reverse().Select(x=>KeyValuePair.Create(x.Key,x.Value?.DeepClone())));}
            }else if(name=="equivalent-result-order"){
                var f=Facts(rows.First(x=>Kind(x)=="n3d-return"&&Facts(x)["kind"]!.GetValue<string>()=="MerchantTravel"));var result=f["result"]!.AsObject();f["result"]=new JsonObject(result.Reverse().Select(x=>KeyValuePair.Create(x.Key,x.Value?.DeepClone())));
            }else throw new ArgumentException(control);
            return;
        }
        var parts=control.Split('|');
        if(parts.Length==2){
            var row=rows.First(x=>Kind(x)==parts[1]);
            switch(parts[0]){
                case "missing":rows.RemoveAll(x=>Kind(x)==parts[1]);break;
                case "duplicate":rows.Insert(rows.IndexOf(row),row.DeepClone().AsObject());break;
                case "root":row["payload"]!["root"]="a1";break;
                case "context":row["payload"]!["context"]="wrong";break;
                case "state-missing":row["payload"]!["detail"]!.AsObject().Remove("state");break;
                case "state-null":row["payload"]!["detail"]!["state"]=null;break;
                case "state-array":row["payload"]!["detail"]!["state"]=new JsonArray();break;
                case "extension":State(row)["futureEconomicState"]=new JsonObject{["nested"]=1};break;
                case "before":rows.Remove(row);rows.Insert(1,row);break;
                default:throw new ArgumentException(control);
            }
        }else if(control=="price-cost")Facts(rows.First(x=>Kind(x)=="merchant-price"))["cost"]=500;
        else if(control=="price-state-word")Facts(rows.First(x=>Kind(x)=="merchant-price"))["after"]!["s0"]=0;
        else if(control=="price-entry")Facts(rows.First(x=>Kind(x)=="merchant-price"))["entry"]=Facts(rows.Last(x=>Kind(x)=="merchant-price"))["entry"]!.DeepClone();
        else if(control=="price-coherent-state"){
            var start=rows.First(x=>Kind(x)=="merchant-price-start");var end=rows.First(x=>Kind(x)=="merchant-price");
            Facts(end)["after"]!["s0"]=0;State(end)["playerRng"]!["rngs"]!["Shops"]!["s0"]=0;
        }else if(control.StartsWith("exit-history-")){
            var row=rows.First(x=>Kind(x)=="merchant-exit-return");var player=State(row)["mapHistory"]!.AsArray().Last()!.AsArray().Last()!["player"]!;
            switch(control[13..]){
                case "missing":State(row).AsObject().Remove("mapHistory");break;
                case "clear":player["card_choices"]=new JsonArray();break;
                case "model":player["card_choices"]![0]!["card"]!["id"]!["Entry"]="BASH";break;
                case "upgrade":player["card_choices"]![0]!["card"]!["current_upgrade_level"]=1;break;
                case "picked":player["card_choices"]![0]!["was_picked"]=true;break;
                case "duplicate":player["card_choices"]!.AsArray().Add(player["card_choices"]![0]!.DeepClone());break;
                case "gold":player["gold_spent"]=999;break;
                case "earlier":State(row)["mapHistory"]![0]![0]!["player"]!["gold_gained"]=999;break;
                case "reorder":var a=player["card_choices"]!.AsArray();var first=a[0]!.DeepClone();a[0]=a[1]!.DeepClone();a[1]=first;break;
                default:throw new ArgumentException(control);
            }
        }else if(control=="exit-inventory")Facts(rows.First(x=>Kind(x)=="merchant-exit-return"))["inventory"]=new JsonObject{["$ref"]="i1"};
        else if(control=="exit-before-start"){
            var a=rows.First(x=>Kind(x)=="merchant-exit-start");var b=rows.First(x=>Kind(x)=="merchant-exit-return");rows.Remove(b);rows.Insert(rows.IndexOf(a),b);
        }else if(control=="purchase-history"){
            var row=rows.First(x=>Kind(x)=="n3d-return"&&Facts(x)["kind"]!.GetValue<string>()=="MerchantBuy");State(row)["mapHistory"]!.AsArray().Last()!.AsArray().Last()!["player"]!["cards_gained"]=new JsonArray();
        }else if(control is "equivalent-order" or "equivalent-extension"){
            foreach(var row in rows)if(row["payload"]!["detail"] is JsonObject d&&d["state"] is JsonObject state){
                if(control=="equivalent-extension")state["futureEconomicState"]=new JsonObject{["nested"]=1};
                else d["state"]=new JsonObject(state.Reverse().Select(x=>KeyValuePair.Create(x.Key,x.Value?.DeepClone())));
            }
        }else throw new ArgumentException(control);
    }
    static void SetOwner(JsonNode node,string field,string mode,JsonNode oldRoom)
    {
        switch(mode){
            case "old-room":node[field]=oldRoom.DeepClone();break;
            case "missing":node.AsObject().Remove(field);break;
            case "null":node[field]=null;break;
            case "array":node[field]=new JsonArray();break;
            default:throw new ArgumentException(mode);
        }
    }
}
