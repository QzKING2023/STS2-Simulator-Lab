using System.Text.Json.Nodes;
namespace OfflineSearchHarness;
internal static class NativeRewardControls
{
    internal static void Apply(List<JsonObject> rows,string control)
    {
        string K(JsonObject r)=>r["payload"]!["kind"]!.GetValue<string>();
        JsonNode P(string kind)=>rows.First(r=>K(r)==kind)["payload"]!;
        JsonNode F(string kind)=>P(kind)["detail"]!["facts"]!;
        JsonNode S(string kind)=>P(kind)["detail"]!["state"]!;
        var parts=control.Split('|');string operation=parts[0];
        JsonObject Open(string kind)=>rows.Last(r=>K(r)=="n3c-open"&&r["payload"]!["detail"]!["facts"]!["kind"]!.GetValue<string>()==kind);
        string OpId(JsonObject open)=>open["payload"]!["detail"]!["facts"]!["id"]!.GetValue<string>();
        JsonNode OpEvent(string kind,string id)=>rows.Single(r=>K(r)==kind&&r["payload"]!["detail"]!["facts"]?["id"]?.GetValue<string>()==id)["payload"]!;
        JsonNode Result(string kind)=>OpEvent("n3c-return",OpId(Open(kind)))["detail"]!["facts"]!["result"]!;
        // Parameterized controls exercise every declared operation, not only the first gold item.
        if(operation=="lifecycle"){
            string id=OpId(Open(parts[1])),mode=parts[2];
            if(mode=="omit-tree"){
                var ids=new HashSet<string>{id};bool changed;
                do{changed=false;foreach(var candidate in rows.Where(r=>K(r)=="n3c-open")){var f=candidate["payload"]!["detail"]!["facts"]!;if(ids.Contains(f["parent"]!.GetValue<string>()))changed|=ids.Add(f["id"]!.GetValue<string>());}}while(changed);
                rows.RemoveAll(r=>K(r).StartsWith("n3c-")&&r["payload"]!["detail"]!["facts"]?["id"] is JsonValue v&&ids.Contains(v.GetValue<string>()));return;
            }
            string e=mode switch{"duplicate-return" or "wrong-owner-return"=>"n3c-return",_=>"n3c-start"};
            var row=rows.Single(r=>K(r)==e&&r["payload"]!["detail"]!["facts"]?["id"]?.GetValue<string>()==id);
            switch(mode){
                case "missing-start":rows.Remove(row);break;
                case "duplicate-start":case "duplicate-return":rows.Insert(rows.IndexOf(row),row.DeepClone().AsObject());break;
                case "wrong-owner-return":row["payload"]!["root"]="a0";row["payload"]!["context"]="wrong-operation-context";break;
                case "wrong-parent":Open(parts[1])["payload"]!["detail"]!["facts"]!["parent"]="unrelated-operation";break;
                case "return-before-start":var end=rows.Single(r=>K(r)=="n3c-return"&&r["payload"]!["detail"]!["facts"]?["id"]?.GetValue<string>()==id);rows.Remove(end);rows.Insert(rows.IndexOf(row),end);break;
                default:throw new ArgumentException(control);
            }return;
        }
        if(operation=="static-edge"){
            string id=OpId(Open(parts[1]));bool entry=parts[2]=="entry";
            foreach(var row in rows.Where(r=>(entry?K(r) is "n3c-open" or "n3c-answer" or "n3c-start":K(r)=="n3c-return")&&r["payload"]!["detail"]!["facts"]?["id"]?.GetValue<string>()==id)){
                var state=row["payload"]!["detail"]!["state"]!;
                switch(parts[3]){case "gold":state["gold"]=999;break;case "rng":state["playerRng"]!["rngs"]!["Rewards"]!["counter"]=999;break;case "extension":state["futureBoundary"]=new JsonObject{["nested"]=new JsonArray(17)};break;default:throw new ArgumentException(control);}
            }return;
        }
        if(operation=="outer-acquired"){
            var res=Result("reward-item");
            switch(parts[1]){case "old":res["acquired"]=new JsonObject{["$ref"]=Open("reward-item")["payload"]!["detail"]!["state"]!["deck"]![0]!["id"]!.GetValue<string>()};break;case "null":res["acquired"]=null;break;case "missing":res.AsObject().Remove("acquired");break;case "wrong-source":res["acquired"]=Open("reward-item")["payload"]!["detail"]!["facts"]!["subject"]!.DeepClone();break;default:throw new ArgumentException(control);}return;
        }
        if(operation=="repair-missing-menu"){Apply(rows,"omit-operation|card-menu");return;}
        if(operation=="repair-child-before-start"){
            string id=OpId(Open("reward-item"));var start=rows.Single(r=>K(r)=="n3c-start"&&r["payload"]!["detail"]!["facts"]!["id"]!.GetValue<string>()==id);rows.Remove(start);
            int end=rows.FindIndex(r=>K(r)=="n3c-return"&&r["payload"]!["detail"]!["facts"]!["id"]!.GetValue<string>()==id);rows.Insert(end,start);return;
        }
        if(operation.StartsWith("repair-set-return-",StringComparison.Ordinal)){
            var p=OpEvent("n3c-return",OpId(Open("reward-set")));var state=p["detail"]!["state"]!;
            switch(operation){case "repair-set-return-gold":state["gold"]=999;break;case "repair-set-return-rng":state["playerRng"]!["rngs"]!["Rewards"]!["counter"]=999;break;case "repair-set-return-extension":state["futureUnknown"]=new JsonObject{["nested"]=new JsonArray(17)};break;default:throw new ArgumentException(control);}return;
        }
        if(operation is "repair-item-open-drift" or "repair-item-open-extension"){
            var open=Open("reward-item");string id=OpId(open),field=operation=="repair-item-open-drift"?"gold":"futureUnknown";open["payload"]!["detail"]!["state"]![field]=999;
            foreach(string k in new[]{"n3c-answer","n3c-start"})OpEvent(k,id)["detail"]!["state"]![field]=999;return;
        }
        if(operation is "repair-false-set" or "repair-false-root"){
            Result(operation=="repair-false-set"?"reward-set":"RewardOffer")[operation=="repair-false-set"?"complete":"completed"]=false;return;
        }
        if(operation is "repair-missing-set-complete" or "repair-null-set-complete" or "repair-wrongtype-set-complete"){
            var result=Result("reward-set").AsObject();if(operation=="repair-missing-set-complete")result.Remove("complete");else result["complete"]=operation=="repair-null-set-complete"?null:JsonValue.Create("true");return;
        }
        if(operation is "repair-unknown-kind" or "repair-duplicate-menu"){
            var open=Open("card-menu");string id=OpId(open);
            if(operation=="repair-unknown-kind")open["payload"]!["detail"]!["facts"]!["kind"]="undeclared-menu";
            else{var transaction=rows.Where(r=>K(r).StartsWith("n3c-")&&r["payload"]!["detail"]!["facts"]?["id"]?.GetValue<string>()==id).Select(r=>r.DeepClone().AsObject()).ToArray();foreach(var row in transaction)row["payload"]!["detail"]!["facts"]!["id"]=id+"copy";int index=rows.FindLastIndex(r=>K(r)=="n3c-return"&&r["payload"]!["detail"]!["facts"]?["id"]?.GetValue<string>()==id);rows.InsertRange(index+1,transaction);}return;
        }
        if(operation is "repair-old-relic" or "repair-old-created-relic" or "repair-wrong-relic-model" or "repair-missing-created"){
            var result=Result("treasure-obtain");string old=Open("treasure-obtain")["payload"]!["detail"]!["state"]!["relics"]![0]!["id"]!.GetValue<string>();
            if(operation=="repair-old-relic")result["acquired"]=new JsonObject{["$ref"]=old};
            if(operation=="repair-old-created-relic"){result["acquired"]=new JsonObject{["$ref"]=old};result["created"]=new JsonObject{["$ref"]=old};}
            if(operation=="repair-wrong-relic-model")result["model"]="RELIC.NOT_THE_CREATED_RELIC";
            if(operation=="repair-missing-created")result.AsObject().Remove("created");return;
        }
        if(operation is "repair-gold-negative" or "repair-gold-positive" or "repair-gold-delta" or "repair-gold-null"){
            var result=Result("treasure-normal");if(operation=="repair-gold-delta")result["actualGoldDelta"]=999;else result["gold"]=operation=="repair-gold-null"?null:JsonValue.Create(operation=="repair-gold-negative"?-999:999);return;
        }
        if(operation is "repair-menu-upgrade" or "repair-menu-model" or "repair-source-upgrade" or "repair-acquired-upgrade" or "repair-missing-source" or "repair-coherent-upgrade"){
            var open=Open("card-menu");var result=Result("card-menu");
            if(operation is "repair-menu-upgrade" or "repair-coherent-upgrade")open["payload"]!["detail"]!["facts"]!["options"]![1]!["Upgrade"]=99;
            if(operation=="repair-menu-model")open["payload"]!["detail"]!["facts"]!["options"]![1]!["Model"]="CARD.STRIKE_IRONCLAD";
            if(operation is "repair-source-upgrade" or "repair-coherent-upgrade")result["sourceCards"]![1]!["upgrade"]=99;
            if(operation=="repair-acquired-upgrade")result["acquiredCard"]!["upgrade"]=99;
            if(operation=="repair-missing-source")result.AsObject().Remove("sourceCards");return;
        }
        if(operation=="repair-menu-success") {Result("card-menu")["success"]=false;return;}
        if(operation is "repair-alternative-completion" or "repair-missing-alternatives" or "repair-alternative-source"){
            var result=Result("card-menu");if(operation=="repair-missing-alternatives")result.AsObject().Remove("sourceAlternatives");
            else result["sourceAlternatives"]![0]![operation=="repair-alternative-completion"?"afterSelected":"option"]=operation=="repair-alternative-completion"?"EndSelectionAndCompleteReward":"REROLL";return;
        }
        if(operation is "missing-state" or "null-state" or "array-state"){
            var detail=P(parts[1])["detail"]!.AsObject();
            if(operation=="missing-state")detail.Remove("state");else detail["state"]=operation=="null-state"?null:new JsonArray();return;
        }
        if(operation=="remove"){if(rows.RemoveAll(r=>K(r)==parts[1])==0)throw new InvalidOperationException("Missing control target");return;}
        if(operation=="duplicate"){int index=rows.FindIndex(r=>K(r)==parts[1]);rows.Insert(index,rows[index].DeepClone().AsObject());return;}
        if(operation=="drift"){S(parts[1])["futureStateField"]=new JsonObject{["value"]=1};return;}
        if(operation=="detach"){P(parts[1])["root"]="a0";P(parts[1])["context"]="unrelated-context";return;}
        if(operation=="false-contract"){F("n3c-contract")["complete"]=false;return;}
        if(operation=="wrong-answer"){F("n3c-answer")["handle"]="not-a-candidate";return;}
        if(operation=="wrong-set-id"){F("n3c-set-completed")["set"]=999;return;}
        if(operation=="wrong-reward-kind"){F("n3c-set")["options"]![0]!["kind"]="CardReward";return;}
        if(operation=="wrong-content-model"){F("n3c-set")["options"]![0]!["model"]="RELIC.NOT_THE_OFFERED_RELIC";return;}
        if(operation=="incomplete-candidates"){
            var open=rows.First(r=>K(r)=="n3c-open"&&r["payload"]!["detail"]!["facts"]!["kind"]!.GetValue<string>()=="reward-item")["payload"]!;
            var options=open["detail"]!["facts"]!["options"]!.AsArray();options.RemoveAt(options.Count-1);return;
        }
        if(operation=="wrong-child-subject"){
            var p=rows.First(r=>K(r)=="n3c-open"&&r["payload"]!["detail"]!["facts"]!["kind"]!.GetValue<string>()=="reward-item")["payload"]!;
            p["detail"]!["facts"]!["subject"]=F("n3c-set")["options"]![0]!["reference"]!.DeepClone();return;
        }
        if(operation=="wrong-acquired"){
            var p=rows.First(r=>K(r)=="n3c-return"&&r["payload"]!["detail"]!["facts"]!["result"] is JsonObject result&&result["acquired"]!=null)["payload"]!;
            p["detail"]!["facts"]!["result"]!["acquired"]=F("n3c-set")["reference"]!.DeepClone();return;
        }
        if(operation=="omit-operation"){
            var ids=rows.Where(r=>K(r)=="n3c-open"&&r["payload"]!["detail"]!["facts"]!["kind"]!.GetValue<string>()==parts[1]).Select(r=>r["payload"]!["detail"]!["facts"]!["id"]!.GetValue<string>()).ToHashSet();
            if(ids.Count==0)throw new InvalidOperationException("Missing operation target");
            rows.RemoveAll(r=>K(r).StartsWith("n3c-")&&r["payload"]!["detail"]!["facts"] is JsonObject f&&f["id"] is JsonValue id&&ids.Contains(id.GetValue<string>()));return;
        }
        if(operation=="coherent-gold"){
            string id=rows.Last(r=>K(r)=="n3c-open"&&r["payload"]!["detail"]!["facts"]!["kind"]!.GetValue<string>()=="reward-item")["payload"]!["detail"]!["facts"]!["id"]!.GetValue<string>();
            bool after=false;
            foreach(var row in rows){if(K(row)=="n3c-return"&&row["payload"]!["detail"]!["facts"]!["id"]!.GetValue<string>()==id)after=true;if(after&&row["payload"]!["detail"] is JsonObject d&&d["state"] is JsonObject s)s["gold"]=s["gold"]!.GetValue<int>()+1;}return;
        }
        if(operation=="property-order"){NativeTerminalControls.Apply(rows,"equivalent-state-property-order");return;}
        if(operation=="equivalent-extension"){
            foreach(var row in rows)if(row["payload"]!["detail"] is JsonObject d&&d["state"] is JsonObject s)s["futureStateField"]=new JsonObject{["value"]=1};return;
        }
        throw new ArgumentException("Unknown reward control "+control);
    }
}
