using System.Text.Json;
using System.Text.Json.Nodes;
namespace OfflineSearchHarness;
// Native economic profile, separate from legacy TestMode evidence. No effects are executed here.
internal sealed class NativeMerchantEconomyContract
{
    static string T(JsonElement e,string p)=>e.GetProperty(p).GetString()!;
    static bool Eq(JsonElement a,JsonElement b)=>SimulatorLab.Core.NativeDecisionTicket.SameActionJson(a.GetRawText(),b.GetRawText());
    static void R(bool ok,string m){if(!ok)throw new NativeDataException("Merchant economy "+m);}
    JsonElement priceStart,priceFacts,exitStart,dispatch,confirmed,priceTail;
    string generationRoot="",generationContext="";
    bool generationConfirmed,inventoryBound,statsComplete,arrivalPending,completionPending;
    // One-way parent Travel phases. Child pairs may not establish their own chronology.
    enum TravelPhase { None,Dispatched,Exiting,StatsStarted,StatsCompleted,Generating,Exited,Confirmed,Arrived }
    TravelPhase travelPhase;
    JsonElement generationRoom,merchantRoom,merchantInventory;
    bool generationArrived;
    string active="",context="",kind="",exitRoot="";
    bool exiting,exited,closed;
    bool rewardGeneration;
    int prices,exitStarts,exitReturns;
    readonly HashSet<string> priceEntries=[];
    readonly Dictionary<string,JsonElement> priceRecords=[];
    JsonElement mapCandidates;
    JsonElement statsStart,statsReturn,rootDecision,travelReturned,roomExitStart;
    bool statsPending,roomExiting,roomReturned;
    bool roomCompleted;
    bool roomReady,natural;
    string session="";
    string lastRoot="",lastContext="";
    JsonElement sessionReference,exitReturn;
    readonly HashSet<string> sessions=[];
    readonly Dictionary<string,string> identityTypes=[];
    internal void Accept(JsonElement p,JsonElement? s)
    {
        string k=T(p,"kind");var d=p.GetProperty("detail");var f=d.ValueKind==JsonValueKind.Object&&d.TryGetProperty("facts",out var v)?v:default;
        if(k=="header")natural=T(d,"scenario") is "merchant-natural" or "merchant-natural-relic";
        if(k=="identity")identityTypes.Add(T(d,"id"),T(d,"type"));
        if(k=="root-request")R(!arrivalPending&&!completionPending,"next root before required room arrival/completion");
        if(k=="map-candidates")mapCandidates=f.Clone();
        if(k=="battle-victory-cleanup"){R(active==""&&Eq(confirmed,s!.Value),"battle cleanup anchor");rewardGeneration=true;}
        if(k=="reward-offer"){
            R(active==""&&rewardGeneration,"unowned/duplicate native reward generation");
            var expected=JsonNode.Parse(confirmed.GetRawText())!;expected["playerRng"]=JsonNode.Parse(s!.Value.GetProperty("playerRng").GetRawText());R(Eq(JsonSerializer.SerializeToElement(expected),s.Value),"unexpected reward generation mutation");
            confirmed=s.Value.Clone();rewardGeneration=false;
        }
        if(k=="dispatch"){
            R(!arrivalPending&&!completionPending,"dispatch before required room arrival/completion");
            active=T(p,"root");context=T(p,"context");kind=T(f,"kind");dispatch=s!.Value.Clone();statsComplete=false;
            rootDecision=f.GetProperty("decision").Clone();statsReturn=default;roomExitStart=default;travelReturned=default;roomReturned=false;roomCompleted=false;
            travelPhase=kind is "Travel" or "MerchantTravel"?TravelPhase.Dispatched:TravelPhase.None;
            if(confirmed.ValueKind==JsonValueKind.Object)R(Eq(confirmed,dispatch),"dispatch not bound to last full confirmation");
            if(kind is "Travel" or "MerchantTravel")R(T(f.GetProperty("decision"),"from")==T(dispatch,"node")&&mapCandidates.EnumerateArray().Any(x=>T(x,"node")==T(f.GetProperty("decision"),"to")),"unoffered/illegal travel edge");
            if(kind is "Travel" or "MerchantTravel")R(session==""||roomReady,"travel before current room ready");
            if(kind=="MerchantAttach"){
                R(natural&&generationConfirmed&&generationArrived&&!inventoryBound&&Eq(rootDecision.GetProperty("room"),generationRoom)&&Eq(sessionReference,generationRoom),"attachment action not owned by generated/arrived room");
                CurrentMerchantRoom(dispatch,generationRoom);
            }
            if(kind is "MerchantExit" or "MerchantTravel")CurrentMerchantRoom(dispatch,merchantRoom);
        }
        if(k=="room-exit-start"){
            R(travelPhase==TravelPhase.Dispatched&&active==T(p,"root")&&context==T(p,"context")&&!roomExiting&&!roomReturned&&kind is "Travel" or "MerchantTravel"&&Eq(s!.Value,dispatch)&&T(f,"session")==session&&Eq(f.GetProperty("room"),sessionReference),"room exit start/dispatch full binding");roomExiting=true;roomExitStart=f.Clone();travelPhase=TravelPhase.Exiting;
        }
        if(k=="merchant-stats-start"){
            R(natural&&kind is "Travel" or "MerchantTravel"&&travelPhase==(session==""?TravelPhase.Dispatched:TravelPhase.Exiting)&&active==T(p,"root")&&context==T(p,"context")&&!statsPending&&!statsComplete&&statsReturn.ValueKind==JsonValueKind.Undefined&&(session==""||roomExiting)&&!roomReturned,"stats owner/required phase/unique");
            statsStart=s!.Value.Clone();statsPending=true;travelPhase=TravelPhase.StatsStarted;
            if(kind is "Travel" or "MerchantTravel"){
                var expected=JsonNode.Parse(dispatch.GetRawText())!;expected["node"]=T(rootDecision,"to");R(Eq(JsonSerializer.SerializeToElement(expected),statsStart),"native visited coordinate/stats start binding");
            }
        }
        if(k=="merchant-stats-return"){
            R(travelPhase==TravelPhase.StatsStarted&&statsPending&&active==T(p,"root")&&context==T(p,"context"),"stats return owner/order");
            var expected=JsonNode.Parse(statsStart.GetRawText())!;var history=LastPlayer(expected["mapHistory"]!);history["current_gold"]=statsStart.GetProperty("gold").GetInt32();history["current_hp"]=statsStart.GetProperty("hp").GetInt32();history["max_hp"]=statsStart.GetProperty("maxHp").GetInt32();
            R(Eq(JsonSerializer.SerializeToElement(expected),s!.Value),"native stats full state mismatch");statsReturn=s.Value.Clone();statsPending=false;statsComplete=true;travelPhase=TravelPhase.StatsCompleted;
        }
        if(k=="room-exit-return"){
            R(travelPhase is TravelPhase.StatsCompleted or TravelPhase.Generating&&statsComplete&&(travelPhase!=TravelPhase.Generating||prices==3&&priceStart.ValueKind==JsonValueKind.Undefined)&&roomExiting&&!roomReturned&&active==T(p,"root")&&context==T(p,"context")&&Eq(roomExitStart,f),"room exit return owner/unique");roomReturned=true;roomExiting=false;travelReturned=s!.Value.Clone();travelPhase=TravelPhase.Exited;
        }
        if(k=="room-complete"){
            R(active==""&&T(p,"root")==lastRoot&&T(p,"context")==lastContext&&completionPending&&roomReturned&&!roomCompleted&&Eq(s!.Value,confirmed)&&Eq(f.GetProperty("room"),roomExitStart.GetProperty("room"))&&T(f,"session")==T(roomExitStart,"session"),"room completion full state/owner/unique");roomCompleted=true;completionPending=false;
        }
        if(k=="room-entered"){
            R(active==""&&T(p,"root")==lastRoot&&T(p,"context")==lastContext&&arrivalPending&&!completionPending&&(session==""||roomCompleted)&&sessions.Add(T(f,"session"))&&Eq(s!.Value,confirmed)&&Eq(f.GetProperty("room"),s.Value.GetProperty("rooms").GetProperty("reference"))&&T(f,"type")==T(s.Value,"room"),"room arrival full state/identity/unique");
            session=T(f,"session");sessionReference=f.GetProperty("room").Clone();roomReady=false;arrivalPending=false;
            R(travelPhase==TravelPhase.Confirmed,"room arrival outside confirmed Travel phase");travelPhase=TravelPhase.Arrived;
            if(lastRoot==generationRoot){R(generationConfirmed&&!generationArrived&&Eq(sessionReference,generationRoom),"generated room arrival identity");CurrentMerchantRoom(s!.Value,generationRoom);generationArrived=true;}
        }
        if(k=="room-leave-ready"){
            R(active==""&&T(p,"root")==lastRoot&&T(p,"context")==lastContext&&!arrivalPending&&!completionPending&&!roomReady&&T(f,"session")==session&&Eq(f.GetProperty("room"),sessionReference)&&Eq(s!.Value,confirmed)&&Eq(f.GetProperty("room"),s.Value.GetProperty("rooms").GetProperty("reference")),"room ready full state/identity/unique");roomReady=true;
        }
        if(k=="merchant-price-start"){
            R(active==T(p,"root")&&context==T(p,"context")&&priceStart.ValueKind==JsonValueKind.Undefined,"price owner/overlap");
            R(kind is "MerchantSetup" or "Travel"&&!generationConfirmed&&prices<3,"price outside native inventory generation");
            if(kind=="Travel"){
                R(natural&&statsComplete&&!statsPending&&!roomReturned&&travelPhase==(prices==0?TravelPhase.StatsCompleted:TravelPhase.Generating),"inventory generation outside post-stats native Travel phase");
                travelPhase=TravelPhase.Generating;
            }
            if(prices==0){
                generationRoot=active;generationContext=context;
                if(natural){generationRoom=s!.Value.GetProperty("rooms").GetProperty("reference").Clone();CurrentMerchantRoom(s.Value,generationRoom);}
                else R(T(s!.Value,"room")=="MerchantRoom","setup prices outside native merchant room");
            }
            else R(active==generationRoot&&context==generationContext&&Eq(priceTail,s!.Value),"successive native prices not bound in full");
            priceStart=s!.Value.Clone();priceFacts=f.Clone();R(Eq(f.GetProperty("before"),priceStart.GetProperty("playerRng").GetProperty("rngs").GetProperty("Shops")),"price start RNG binding");
        }
        if(k=="merchant-price"){
            R(priceStart.ValueKind==JsonValueKind.Object&&active==T(p,"root")&&context==T(p,"context")&&Eq(priceFacts.GetProperty("entry"),f.GetProperty("entry")),"price return owner/entry");
            R(T(f,"profile")==NativeMerchantEconomy.Profile&&priceEntries.Add(T(f.GetProperty("entry"),"$ref")),"price profile/duplicate");
            var before=f.GetProperty("before");var after=f.GetProperty("after");
            R(Eq(before,priceFacts.GetProperty("before"))&&Eq(after,s!.Value.GetProperty("playerRng").GetProperty("rngs").GetProperty("Shops"))&&after.GetProperty("counter").GetInt64()==before.GetProperty("counter").GetInt64()+1&&!Eq(before,after),"native price RNG advance");
            CheckRng(before,after);
            var expected=JsonNode.Parse(priceStart.GetRawText())!;expected["playerRng"]!["rngs"]!["Shops"]=JsonNode.Parse(after.GetRawText());R(Eq(JsonSerializer.SerializeToElement(expected),s!.Value),"price unexpected state mutation");
            int baseline=T(f,"rarity")=="Rare"?100:T(f,"rarity")=="Uncommon"?75:50;int cost=f.GetProperty("cost").GetInt32();R(cost>=Math.Floor(baseline*.95)&&cost<=Math.Ceiling(baseline*1.05),"independent price bounds");
            ulong output=unchecked(System.Numerics.BitOperations.RotateLeft(before.GetProperty("s1").GetUInt64()*5,7)*9);
            double uniform=(output>>11)*(1.0/9007199254740992.0);
            float jitter=(float)(uniform*(double)(1.05f-.95f)+(double).95f);
            R(cost==(int)MathF.Round(baseline*jitter),"independent deterministic price");
            priceRecords.Add(T(f.GetProperty("entry"),"$ref"),f.Clone());prices++;priceTail=s.Value.Clone();priceStart=default;
        }
        if(k=="n3d-return"&&kind is "MerchantSetup" or "MerchantAttach"){
            R(!inventoryBound&&prices==3&&priceStart.ValueKind==JsonValueKind.Undefined&&(kind=="MerchantSetup"?active==generationRoot:generationConfirmed),"inventory attachment outside completed native generation");
            var expected=JsonNode.Parse((kind=="MerchantSetup"?priceTail:dispatch).GetRawText())!;
            expected["merchant"]=JsonNode.Parse(s!.Value.GetProperty("merchant").GetRawText());
            R(Eq(JsonSerializer.SerializeToElement(expected),s.Value),"inventory bind changed full generated state");
            var inventory=s.Value.GetProperty("merchant");
            // API Setup snapshots do not carry the natural room-controller projection.
            // Bind their room at the original setup return, when it becomes observable.
            if(kind=="MerchantSetup")generationRoom=inventory.GetProperty("room").Clone();
            CurrentMerchantRoom(s.Value,generationRoom);
            R(Eq(inventory.GetProperty("room"),generationRoom)&&Eq(f.GetProperty("result").GetProperty("nativeInventory"),inventory.GetProperty("reference")),"inventory/returned identity not owned by native generated room");
            R(identityTypes.GetValueOrDefault(T(inventory.GetProperty("reference"),"$ref"))=="MerchantInventory","wrong native inventory object type");
            merchantRoom=generationRoom.Clone();merchantInventory=inventory.GetProperty("reference").Clone();inventoryBound=true;
            var entries=s!.Value.GetProperty("merchant").GetProperty("entries").EnumerateArray().Where(e=>T(e,"kind")=="MerchantPotionEntry").ToArray();
            R(prices==3&&entries.Length==3&&entries.All(e=>priceRecords.TryGetValue(T(e.GetProperty("reference"),"$ref"),out var record)&&T(record,"model")==T(e,"model")&&record.GetProperty("cost").GetInt32()==e.GetProperty("cost").GetInt32()),"generated price/instance/inventory binding");
        }
        if(k=="merchant-exit-start"){
            R(!exiting&&!exited&&active==T(p,"root")&&context==T(p,"context")&&kind is "MerchantExit" or "MerchantTravel","exit owner/unique");
            exitRoot=active;exiting=true;exitStarts++;exitStart=s!.Value.Clone();R(!exitStart.GetProperty("merchant").GetProperty("closed").GetBoolean(),"exit starts closed");
            R(inventoryBound&&Eq(f.GetProperty("inventory"),merchantInventory)&&Eq(f.GetProperty("room"),merchantRoom),"exit session identity");
            if(kind=="MerchantExit")R(Eq(dispatch,exitStart),"exit not bound to dispatch");
            else{
                R(statsReturn.ValueKind==JsonValueKind.Object&&roomExiting,"missing native stats/outer travel prefix");
                var expected=JsonNode.Parse(statsReturn.GetRawText())!;expected["floor"]=dispatch.GetProperty("floor").GetInt32()+1;expected["room"]=null;expected["rooms"]=null;
                R(Eq(JsonSerializer.SerializeToElement(expected),exitStart),"native pop/floor/exit full binding");
            }
        }
        if(k=="merchant-exit-return"){
            R(exiting&&!exited&&active==exitRoot&&active==T(p,"root")&&context==T(p,"context"),"exit completion owner/order");
            R(Eq(f.GetProperty("inventory"),exitStart.GetProperty("merchant").GetProperty("reference"))&&Eq(f.GetProperty("room"),exitStart.GetProperty("merchant").GetProperty("room")),"exit completion session identity");
            var expected=JsonNode.Parse(exitStart.GetRawText())!;expected["merchant"]!["closed"]=true;expected["mapHistory"]=JsonNode.Parse(s!.Value.GetProperty("mapHistory").GetRawText());R(Eq(JsonSerializer.SerializeToElement(expected),s.Value),"exit non-history/static mutation");
            CheckExitHistory(exitStart,s.Value);exitReturn=s.Value.Clone();exiting=false;exited=true;exitReturns++;
        }
        if(k=="n3d-return"&&kind is "MerchantBuy" or "MerchantRemove")CheckPurchaseHistory(dispatch,s!.Value);
        if(k=="n3d-return"&&kind=="MerchantTravel"){
            R(exited&&roomReturned,"travel returned before native merchant exit");
            var old=exitReturn.GetProperty("mapHistory");var now=s!.Value.GetProperty("mapHistory");R(old.GetArrayLength()==now.GetArrayLength(),"travel history act mismatch");
            for(int a=0;a<old.GetArrayLength();a++){
                R(now[a].GetArrayLength()==old[a].GetArrayLength()+(a==old.GetArrayLength()-1?1:0),"native travel history append cardinality");
                for(int i=0;i<old[a].GetArrayLength();i++)R(Eq(old[a][i],now[a][i]),"native merchant exit history lost during travel");
            }
        }
        if(k=="confirmed"){
            R(!statsPending,"confirmation before stats completed");
            if(active==generationRoot){
                R(!generationConfirmed&&prices==3&&priceStart.ValueKind==JsonValueKind.Undefined&&context==generationContext,"incomplete/duplicate generation confirmation");
                var expected=JsonNode.Parse(priceTail.GetRawText())!;
                if(kind=="MerchantSetup")expected["merchant"]=JsonNode.Parse(s!.Value.GetProperty("merchant").GetRawText());
                R(Eq(JsonSerializer.SerializeToElement(expected),s!.Value)&&T(s.Value,"room")=="MerchantRoom","native price lifecycle/full parent confirmation mismatch");generationConfirmed=true;
                CurrentMerchantRoom(s.Value,generationRoom);
            }
            if(kind is "Travel" or "MerchantTravel")R(statsComplete,"missing required native stats lifecycle");
            if(kind is "Travel" or "MerchantTravel")R(session==""?travelPhase is TravelPhase.StatsCompleted or TravelPhase.Generating:travelPhase==TravelPhase.Exited,"Travel confirmation phase mismatch");
            if(kind is "Travel" or "MerchantTravel")R(session==""||roomReturned&&!roomExiting,"confirmation before room exit returned");
            if(roomReturned)R(Eq(travelReturned,s!.Value),"travel return/confirmation full state mismatch");
            if(kind is "Travel" or "MerchantTravel")R(T(s!.Value,"node")==T(f.GetProperty("action").GetProperty("decision"),"to")&&s.Value.GetProperty("floor").GetInt32()==dispatch.GetProperty("floor").GetInt32()+1&&s.Value.GetProperty("rooms").GetProperty("stack").GetInt32()==1,"native travel destination/floor/stack");
            if(kind is "Travel" or "MerchantTravel"){arrivalPending=true;completionPending=session!="";travelPhase=TravelPhase.Confirmed;}
            confirmed=s!.Value.Clone();lastRoot=active;lastContext=context;active="";
        }
        if(k=="n3d-contract"){
            R(!arrivalPending&&!completionPending&&active==""&&Eq(confirmed,s!.Value)&&exited&&exitStarts==1&&exitReturns==1,"final static/history lifecycle");closed=true;
        }
        // The projection retains the old merchant after travel; bind it to its owner,
        // not to whichever room is current after the real native exit.
        if(inventoryBound&&s is {} state&&state.GetProperty("merchant").ValueKind==JsonValueKind.Object){
            var inventory=state.GetProperty("merchant");
            R(Eq(inventory.GetProperty("reference"),merchantInventory)&&Eq(inventory.GetProperty("room"),merchantRoom),"merchant projection changed its bound inventory/room owner");
        }
    }
    void CurrentMerchantRoom(JsonElement state,JsonElement reference)
    {
        R(reference.ValueKind==JsonValueKind.Object&&reference.GetProperty("$ref").ValueKind==JsonValueKind.String&&identityTypes.GetValueOrDefault(T(reference,"$ref"))=="MerchantRoom"&&T(state,"room")=="MerchantRoom","native current merchant room identity mismatch");
        if(natural)R(state.GetProperty("rooms").GetProperty("stack").GetInt32()==1&&Eq(state.GetProperty("rooms").GetProperty("reference"),reference),"native natural room-controller identity mismatch");
    }
    static JsonElement LastPlayer(JsonElement s)=>s.GetProperty("mapHistory").EnumerateArray().Last().EnumerateArray().Last().GetProperty("player");
    static JsonNode LastPlayer(JsonNode h)=>h.AsArray().Last()!.AsArray().Last()!["player"]!;
    static JsonNode Model(string model){var parts=model.Split('.');R(parts.Length==2,"invalid model identity");return JsonSerializer.SerializeToNode(new{Category=parts[0],Entry=parts[1]})!;}
    static void Append(JsonNode player,string field,JsonNode item)=>player[field]!.AsArray().Add(item);
    static void CheckPurchaseHistory(JsonElement before,JsonElement after)
    {
        var expected=JsonNode.Parse(before.GetProperty("mapHistory").GetRawText())!;var player=LastPlayer(expected);
        int spent=before.GetProperty("gold").GetInt32()-after.GetProperty("gold").GetInt32();player["gold_spent"]=player["gold_spent"]!.GetValue<int>()+spent;
        var old=before.GetProperty("deck").EnumerateArray().ToArray();var now=after.GetProperty("deck").EnumerateArray().ToArray();
        foreach(var card in now.Where(c=>!old.Any(x=>T(x,"id")==T(c,"id")))){
            var offered=before.GetProperty("merchant").GetProperty("entries").EnumerateArray().Single(e=>e.GetProperty("content").ValueKind==JsonValueKind.Object&&T(e.GetProperty("content"),"$ref")==T(card,"id"));
            // Native add history serializes the offered card before FloorAddedToDeck is assigned.
            Append(player,"cards_gained",JsonNode.Parse(offered.GetProperty("nativeCard").GetRawText())!);
        }
        foreach(var card in old.Where(c=>!now.Any(x=>T(x,"id")==T(c,"id"))))Append(player,"cards_removed",JsonNode.Parse(card.GetProperty("native").GetRawText())!);
        var potions=before.GetProperty("potions").EnumerateArray().ToArray();
        foreach(var potion in after.GetProperty("potions").EnumerateArray().Where(c=>!potions.Any(x=>Eq(x,c)))){
            var model=Model(T(potion,"model"));Append(player,"bought_potions",model.DeepClone());Append(player,"potion_choices",new JsonObject{["choice"]=model,["was_picked"]=true});
        }
        var bought=before.GetProperty("merchant").GetProperty("entries").EnumerateArray().SingleOrDefault(e=>T(e,"kind")=="MerchantCardEntry"&&now.Any(c=>!old.Any(x=>T(x,"id")==T(c,"id"))&&T(c,"id")==T(e.GetProperty("content"),"$ref")));
        if(bought.ValueKind!=JsonValueKind.Undefined&&bought.GetProperty("index").GetInt32()>=5)Append(player,"bought_colorless",Model(T(bought,"model")));
        foreach(var relic in after.GetProperty("relics").EnumerateArray().Where(c=>!before.GetProperty("relics").EnumerateArray().Any(x=>T(x,"id")==T(c,"id")))){Append(player,"bought_relics",Model(T(relic,"model")));Append(player,"relic_choices",new JsonObject{["choice"]=Model(T(relic,"model")),["was_picked"]=true});}
        R(Eq(JsonSerializer.SerializeToElement(expected),after.GetProperty("mapHistory")),"purchase full native history/result mismatch");
    }
    internal static void CheckExitHistory(JsonElement before,JsonElement after)
    {
        var expected=JsonNode.Parse(before.GetProperty("mapHistory").GetRawText())!;var player=LastPlayer(expected);
        foreach(var entry in before.GetProperty("merchant").GetProperty("entries").EnumerateArray().Where(x=>x.GetProperty("stock").GetBoolean())){
            string type=T(entry,"kind");if(type=="MerchantCardRemovalEntry")continue;
            string field=type=="MerchantCardEntry"?"card_choices":type=="MerchantRelicEntry"?"relic_choices":"potion_choices";
            var choice=new JsonObject{[type=="MerchantCardEntry"?"card":"choice"]=type=="MerchantCardEntry"?JsonNode.Parse(entry.GetProperty("nativeCard").GetRawText()):Model(T(entry,"model")),["was_picked"]=false};Append(player,field,choice);
        }
        R(Eq(JsonSerializer.SerializeToElement(expected),after.GetProperty("mapHistory")),"full exit history instance/content/order mismatch");
    }
    internal static void CheckRng(JsonElement before,JsonElement after)
    {
        ulong a=before.GetProperty("s0").GetUInt64(),b=before.GetProperty("s1").GetUInt64(),c=before.GetProperty("s2").GetUInt64(),d=before.GetProperty("s3").GetUInt64(),t=b<<17;
        c^=a;d^=b;b^=c;a^=d;c^=t;d=(d<<45)|(d>>19);
        R(after.GetProperty("s0").GetUInt64()==a&&after.GetProperty("s1").GetUInt64()==b&&after.GetProperty("s2").GetUInt64()==c&&after.GetProperty("s3").GetUInt64()==d,"independent xoshiro state transition");
    }
    internal void Finish()=>R(generationConfirmed&&inventoryBound&&!arrivalPending&&!completionPending&&closed&&!exiting&&!statsPending&&!roomExiting&&(!natural||sessions.Count>=3)&&prices==3&&priceStart.ValueKind==JsonValueKind.Undefined,"missing native prices/exit proof");
}
