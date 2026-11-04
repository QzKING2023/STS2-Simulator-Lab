using System.Text.Json;
using System.Text.Json.Nodes;

namespace OfflineSearchHarness;
// Finite 4B scenario contract, independent of fixture 'valid' claims and writer seals.
internal sealed class NativeRoomContract
{
    string scenario="",active="",kind="",ctx="",lastRoot="",lastCtx="",session="";
    JsonElement confirmed,dispatch,returned,fixtureBefore,decision;
    readonly HashSet<string> requests=[],starts=[],returns=[],sessions=[];
    readonly Dictionary<string,JsonElement> candidates=[];
    readonly Dictionary<string,JsonElement> candidateStates=[];
    readonly Dictionary<string,JsonElement> returnedFacts=[];
    // One request owns its entire dispatch -> return -> confirmation -> boundary.
    // Keep immutable snapshots; a prior request cannot close the current operation.
    sealed class ChoiceOperation(string request,string session,JsonElement room,string root,string context)
    {
        internal readonly string Request=request,Session=session,Root=root,Context=context;
        internal readonly JsonElement Room=room.Clone();
        internal bool Started,Returned,Confirmed,Boundary;
        internal JsonElement ReturnState,ConfirmedState;
    }
    ChoiceOperation? choice;
    string currentRequest="",battleRoot="",battleContext="",rewardRoot="",rewardContext="";
    bool battleConfirmed,rewardOffered,rewardSkipped;
    JsonElement rewardOfferState,rewardOptions;
    int? rewardSet;
    readonly HashSet<int> collectedRewards=[];
    readonly Dictionary<string,(string Type,string? Model)> identities=[];
    JsonElement sessionReference;
    string? upgraded;
    int cancellations;
    bool lossStarted,lossComplete,stamped,terminalContract,closed;
    string lossRoot="";
    int losses;
    bool complete,exiting,exited,finalized,goal,battleBefore,battleClean,rewardsComplete;
    int roomCount,assertions,victories,options,exitStarts,exitReturns;
    static string T(JsonElement e,string p)=>e.GetProperty(p).GetString()??throw new NativeDataException("Null room field: "+p);
    static void R([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool b,string m){if(!b)throw new NativeDataException(m);}
    static bool Eq(JsonElement a,JsonElement b)=>SimulatorLab.Core.NativeDecisionTicket.SameActionJson(a.GetRawText(),b.GetRawText());
    void Static(JsonElement p,JsonElement s,bool close=false){
        R(active==""&&T(p,"root")==lastRoot&&T(p,"context")==lastCtx,"Room static observation owner mismatch");
        R(confirmed.ValueKind==JsonValueKind.Object,"Missing room confirmed anchor");
        var expected=JsonNode.Parse(confirmed.GetRawText())!;if(close)expected["lifecycle"]!["native"]!["Closed"]=true;
        R(Eq(s,JsonSerializer.SerializeToElement(expected)),"Room complete confirmed state mismatch");
    }
    void Inside(JsonElement p,string required){R(active==T(p,"root")&&ctx==T(p,"context")&&kind==required,"Room active action owner mismatch");}
    void ChoiceClosed()=>R(choice==null||choice.Boundary,"Missing room choice result boundary");
    void CombatRoom(JsonElement s)=>R(session!=""&&!exited&&!finalized&&T(s,"room")=="CombatRoom"&&Eq(s.GetProperty("rooms").GetProperty("reference"),sessionReference),"Battle/reward room association mismatch");
    void State(JsonElement s){
        var n=s.GetProperty("lifecycle").GetProperty("native");R(n.GetProperty("Losses").GetInt32()==losses&&!n.GetProperty("Won").GetBoolean()&&n.GetProperty("Closed").GetBoolean()==closed,"Room lifecycle authority mismatch");
        var room=s.GetProperty("rooms");if(s.GetProperty("room").ValueKind==JsonValueKind.Null){R(room.ValueKind==JsonValueKind.Null,"Room snapshot without room");return;}
        R(room.ValueKind==JsonValueKind.Object&&room.GetProperty("reference").ValueKind==JsonValueKind.Object,"Missing room snapshot");
        string rid=T(room.GetProperty("reference"),"$ref");R(identities.TryGetValue(rid,out var roomIdentity)&&roomIdentity.Type==T(s,"room"),"Room snapshot identity/type mismatch");
        R(room.GetProperty("map").GetInt32()==s.GetProperty("lifecycle").GetProperty("mapGeneration").GetInt32()&&room.GetProperty("stack").GetInt32()>0,"Room map/stack snapshot mismatch");
        var ev=s.GetProperty("lifecycle").GetProperty("eventModel");
        if(s.GetProperty("room").GetString()=="EventRoom"){
            R(ev.ValueKind==JsonValueKind.Object&&ev.GetProperty("reference").ValueKind==JsonValueKind.Object&&ev.GetProperty("rng").ValueKind==JsonValueKind.Object,"Missing room event identity/RNG");
            var rng=ev.GetProperty("rng");R(rng.GetProperty("counter").GetInt64()>=0,"Bad event RNG");foreach(var word in new[]{"s0","s1","s2","s3"})_=rng.GetProperty(word).GetUInt64();_=ev.GetProperty("IsFinished").GetBoolean();
            R(identities.TryGetValue(T(ev.GetProperty("reference"),"$ref"),out var identity)&&identity.Model==T(ev,"model"),"Room event model identity mismatch");
        }else R(ev.ValueKind==JsonValueKind.Null,"Event snapshot outside event room");
        R(s.GetProperty("room").GetString()=="RestSiteRoom"?room.GetProperty("rest").ValueKind==JsonValueKind.Array:room.GetProperty("rest").ValueKind==JsonValueKind.Null,"Rest snapshot mismatch");
    }
    internal void Accept(JsonElement p,JsonElement? snapshot)
    {
        string k=T(p,"kind");var d=p.GetProperty("detail");var f=d.ValueKind==JsonValueKind.Object&&d.TryGetProperty("facts",out var facts)?facts:default;
        if(k=="header")scenario=T(d,"scenario");
        if(k=="identity")identities.Add(T(d,"id"),(T(d,"type"),d.GetProperty("model").GetString()));
        if(k=="root-request"){
            R(!stamped,"Room action after terminal stamp");ChoiceClosed();
            R(!battleBefore||battleClean,"Action before native battle cleanup completed");
        }
        if(k=="battle-loss-start"){Inside(p,"EndTurn");R(!lossStarted,"Duplicate room loss start");lossStarted=true;lossRoot=active;}
        if(k=="battle-loss-confirmed"){Inside(p,"EndTurn");R(lossStarted&&!lossComplete&&active==lossRoot,"Room loss completion owner/order mismatch");lossComplete=true;losses++;}
        if(k=="terminal-contract"){R(stamped&&!terminalContract,"Room terminal contract order/duplicate");closed=true;}
        if(snapshot is {} s)State(s);
        if(k=="dispatch"){
            R(active=="","Nested room root");active=T(p,"root");ctx=T(p,"context");kind=T(f,"kind");decision=f.GetProperty("decision").Clone();dispatch=snapshot!.Value.Clone();returned=default;
            if(kind=="RoomOption"){
                var choice=f.GetProperty("decision");string q=T(choice,"request");R(T(choice,"session")==session&&!complete&&candidates.ContainsKey(q)&&Eq(dispatch,candidateStates[q]),"Room dispatch changed request state/session");
                R(q==currentRequest&&!starts.Contains(q),"Room dispatch reused stale request");
                var a=candidates[q].GetProperty("options").EnumerateArray().SingleOrDefault(x=>x.GetProperty("index").GetInt32()==choice.GetProperty("index").GetInt32());
                R(a.ValueKind==JsonValueKind.Object&&a.GetProperty("enabled").GetBoolean()&&T(a,"key")==T(choice,"key")&&Eq(a.GetProperty("reference"),choice.GetProperty("option")),"Room dispatch option mismatch");
                this.choice=new(q,session,sessionReference,active,ctx);
            }
            if(rewardOffered&&!rewardsComplete){
                CombatRoom(dispatch);R(kind is "TakeGold" or "TakeCard" or "SkipRewards","Non-reward action inside reward collection");
                R(!rewardSkipped,"Reward action after collection skipped");
                int set=decision.GetProperty("setId").GetInt32();R(rewardSet==null||rewardSet==set,"Reward collection set mismatch");rewardSet=set;
                if(kind!="SkipRewards"){
                    int index=decision.GetProperty("index").GetInt32();
                    var item=rewardOptions.EnumerateArray().SingleOrDefault(x=>x.GetProperty("index").GetInt32()==index);
                    R(item.ValueKind==JsonValueKind.Object&&T(item,"kind")==(kind=="TakeGold"?"GoldReward":"CardReward")&&collectedRewards.Add(index),"Reward collection candidate/duplicate mismatch");
                }
            }
            else R(kind is not ("TakeGold" or "TakeCard" or "SkipRewards"),"Reward action without current room offer");
        }
        if(k=="confirmed"){
            R(active==T(p,"root")&&ctx==T(p,"context"),"Room confirmation owner mismatch");
            if(kind is "RoomOption" or "RoomExit"||kind=="Travel"&&session!="")R(returned.ValueKind==JsonValueKind.Object&&Eq(returned,snapshot!.Value),"Room confirmed before native return/state mismatch");
            if(kind=="RoomOption"){
                R(choice!=null&&choice.Started&&choice.Returned&&!choice.Confirmed&&choice.Root==active&&choice.Context==ctx,"Room choice confirmation lifecycle mismatch");
                choice.Confirmed=true;choice.ConfirmedState=snapshot!.Value.Clone();
            }
            if(battleBefore&&!battleClean&&active==battleRoot){R(ctx==battleContext,"Battle confirmation context mismatch");battleConfirmed=true;}
            if(rewardOffered&&!rewardsComplete){rewardRoot=active;rewardContext=ctx;rewardSkipped=kind=="SkipRewards";}
            lastRoot=active;lastCtx=ctx;active="";confirmed=snapshot!.Value.Clone();
        }
        if(k=="room-entered"){
            ChoiceClosed();Static(p,snapshot!.Value);R(!exiting&&(roomCount==0||finalized)&&sessions.Add(T(f,"session")),"Duplicate/unclosed room session");session=T(f,"session");roomCount++;complete=exited=finalized=false;
            R(Eq(f.GetProperty("room"),snapshot.Value.GetProperty("rooms").GetProperty("reference"))&&T(f,"type")==T(snapshot.Value,"room"),"Room entered identity/type mismatch");
            sessionReference=f.GetProperty("room").Clone();
            battleBefore=battleClean=battleConfirmed=rewardOffered=rewardSkipped=rewardsComplete=false;
            battleRoot=battleContext=rewardRoot=rewardContext="";rewardSet=null;collectedRewards.Clear();currentRequest="";
            if(scenario!="rooms-natural")fixtureBefore=snapshot.Value.Clone();
        }
        if(k.StartsWith("room-")&&k is not ("room-entered" or "room-slice-complete"))R(session!=""&&T(f,"session")==session,"Room session association mismatch");
        if(k.StartsWith("room-")&&k!="room-entered"&&f.TryGetProperty("room",out var associated))R(Eq(associated,sessionReference),"Room operation instance association mismatch");
        if(k=="room-candidates"){
            ChoiceClosed();Static(p,snapshot!.Value);R(!complete&&!exited&&requests.Add(T(f,"request")),"Room candidates after completion/duplicate request");currentRequest=T(f,"request");
            R(Eq(f.GetProperty("room"),snapshot.Value.GetProperty("rooms").GetProperty("reference")),"Room candidate object mismatch");
            var native=snapshot.Value.GetProperty("rooms").GetProperty(T(snapshot.Value,"room")=="RestSiteRoom"?"rest":"eventOptions");var opts=f.GetProperty("options");R(native.GetArrayLength()==opts.GetArrayLength(),"Missing room options");
            for(int i=0;i<opts.GetArrayLength();i++){
                var a=opts[i];var b=native[i];R(T(a,"handle")=="o"+i&&a.GetProperty("index").GetInt32()==i&&Eq(a.GetProperty("reference"),b.GetProperty("reference"))&&T(a,"key")==T(b,T(snapshot.Value,"room")=="RestSiteRoom"?"id":"TextKey")&&a.GetProperty("enabled").GetBoolean()==(T(snapshot.Value,"room")=="RestSiteRoom"?b.GetProperty("enabled").GetBoolean():!b.GetProperty("IsLocked").GetBoolean()),"Room option mapping mismatch");
            }
            candidates.Add(T(f,"request"),f.Clone());
            candidateStates.Add(T(f,"request"),snapshot.Value.Clone());
        }
        if(k=="room-option-start"){
            Inside(p,"RoomOption");string q=T(f,"request");R(q==T(decision,"request")&&T(f,"key")==T(decision,"key")&&Eq(f.GetProperty("option"),decision.GetProperty("option"))&&starts.Add(q)&&candidates.TryGetValue(q,out _),"Unrequested/repeated room execution");
            var action=dispatch;var req=candidates[q];var option=req.GetProperty("options").EnumerateArray().SingleOrDefault(x=>T(x,"key")==T(f,"key")&&Eq(x.GetProperty("reference"),f.GetProperty("option")));
            R(option.ValueKind==JsonValueKind.Object&&option.GetProperty("enabled").GetBoolean()&&Eq(action,snapshot!.Value),"Disabled/stale room dispatch");options++;
            R(choice!=null&&choice.Request==q&&!choice.Started&&choice.Root==active&&choice.Context==ctx&&choice.Session==session&&Eq(choice.Room,sessionReference),"Room choice start owner mismatch");choice.Started=true;
        }
        if(k=="room-option-return"){
            Inside(p,"RoomOption");string q=T(f,"request");R(starts.Contains(q)&&returns.Add(q),"Room native return without start/duplicate");returned=snapshot!.Value.Clone();
            R(choice!=null&&choice.Request==q&&q==T(decision,"request")&&choice.Started&&!choice.Returned&&choice.Root==active&&choice.Context==ctx,"Room choice return request/owner mismatch");
            choice.Returned=true;choice.ReturnState=returned.Clone();
            returnedFacts.Add(q,f.Clone());
            if(T(dispatch,"room")=="RestSiteRoom"){
                var success=f.GetProperty("success");R(success.ValueKind is JsonValueKind.True or JsonValueKind.False,"Missing native rest result");
                if(!success.GetBoolean()){R(Eq(dispatch,returned),"Cancelled rest changed state");cancellations++;}
                else{
                    R(returned.GetProperty("rooms").GetProperty("rest").GetArrayLength()<dispatch.GetProperty("rooms").GetProperty("rest").GetArrayLength(),"Successful rest did not consume option");
                    var before=dispatch.GetProperty("deck");var after=returned.GetProperty("deck");
                    R(before.GetArrayLength()==after.GetArrayLength(),"Rest changed permanent deck length");
                    for(int i=0;i<before.GetArrayLength();i++)if(before[i].GetProperty("upgrade").GetInt32()!=after[i].GetProperty("upgrade").GetInt32()){
                        R(upgraded==null&&T(before[i],"id")==T(after[i],"id")&&after[i].GetProperty("upgrade").GetInt32()==before[i].GetProperty("upgrade").GetInt32()+1,"Unexpected persistent upgrade");upgraded=T(after[i],"id");
                    }
                }
            }else R(f.GetProperty("success").ValueKind==JsonValueKind.Null&&f.GetProperty("eventFinished").GetBoolean()==returned.GetProperty("lifecycle").GetProperty("eventModel").GetProperty("IsFinished").GetBoolean(),"Event result/completion mismatch");
        }
        if(k=="room-option-boundary"){
            Static(p,snapshot!.Value);string q=T(f,"request");
            R(choice!=null&&choice.Request==q&&choice.Session==session&&choice.Root==lastRoot&&choice.Context==lastCtx&&choice.Confirmed&&!choice.Boundary&&Eq(choice.Room,sessionReference),"Room choice boundary request/owner/unique mismatch");
            R(returns.Contains(q)&&Eq(f.GetProperty("success"),returnedFacts[q].GetProperty("success"))&&Eq(f.GetProperty("eventFinished"),returnedFacts[q].GetProperty("eventFinished")),"Room boundary before native return/result mismatch");
            R(Eq(snapshot.Value,choice.ReturnState)&&Eq(snapshot.Value,choice.ConfirmedState),"Room choice boundary full state mismatch");choice.Boundary=true;
        }
        if(k=="room-leave-ready"){
            Static(p,snapshot!.Value);ChoiceClosed();R(!complete&&!exited,"Duplicate room completion");
            if(T(snapshot.Value,"room")=="EventRoom")R(snapshot.Value.GetProperty("lifecycle").GetProperty("eventModel").GetProperty("IsFinished").GetBoolean(),"Room completed with unfinished event");complete=true;
            if(T(snapshot.Value,"room")=="CombatRoom")R(battleClean&&rewardsComplete,"Combat room completed before native victory/rewards");
        }
        if(k=="room-exit-start"){
            ChoiceClosed();
            R(active==T(p,"root")&&ctx==T(p,"context")&&kind is "RoomExit" or "Travel"&&complete&&!exiting&&!exited,"Room exit before completion or repeated");
            // Both callers observe this before invoking native Exit/EnterMapCoord.
            // Bind the whole immutable dispatch state; native changes belong after this boundary.
            R(dispatch.ValueKind==JsonValueKind.Object&&snapshot is {} start&&start.ValueKind==JsonValueKind.Object&&Eq(dispatch,start),"Room exit start dispatch state mismatch");
            exiting=true;exitStarts++;
        }
        if(k=="room-exit-return"){R(exiting&&active==T(p,"root")&&ctx==T(p,"context"),"Room exit return owner/order mismatch");exiting=false;exited=true;exitReturns++;returned=snapshot!.Value.Clone();}
        if(k=="room-exited"){Static(p,snapshot!.Value);R(exited&&!exiting,"Room exited before original completion");}
        if(k=="room-complete"){Static(p,snapshot!.Value);R(exited&&!exiting&&complete&&!finalized,"Duplicate room completion or missing native Exit");finalized=true;}
        if(k=="battle-victory-before-cleanup"){
            CombatRoom(snapshot!.Value);R(!battleBefore&&!battleClean,"Duplicate/misplaced battle victory");
            R(active!=""&&active==T(p,"root")&&ctx==T(p,"context")&&kind is "PlayCard" or "EndTurn","Battle victory active action owner mismatch");
            battleRoot=active;battleContext=ctx;battleBefore=true;
        }
        if(k=="battle-victory-cleanup"){
            Static(p,snapshot!.Value);CombatRoom(snapshot.Value);
            R(battleBefore&&battleConfirmed&&!battleClean&&lastRoot==battleRoot&&lastCtx==battleContext,"Missing/duplicate native battle cleanup");battleClean=true;victories++;
        }
        if(k=="reward-offer"&&session!=""){
            CombatRoom(snapshot!.Value);R(active==""&&T(p,"root")==lastRoot&&T(p,"context")==lastCtx&&battleClean&&!rewardOffered&&!rewardsComplete,"Reward offer lifecycle owner/order mismatch");
            R(f.ValueKind==JsonValueKind.Array,"Missing room reward offer options");rewardOptions=f.Clone();rewardOfferState=snapshot.Value.Clone();rewardOffered=true;
        }
        if(k=="rewards-completed"&&session!=""){
            Static(p,snapshot!.Value);CombatRoom(snapshot.Value);
            R(battleClean&&rewardOffered&&!rewardsComplete&&rewardRoot!=""&&lastRoot==rewardRoot&&lastCtx==rewardContext,"Rewards completion without actual victory/collection");
            R(rewardSkipped||collectedRewards.Count==rewardOptions.GetArrayLength(),"Rewards completion before collection closed");
            R(f.GetProperty("offers").GetInt32()==1&&f.GetProperty("goldBefore").GetInt32()==rewardOfferState.GetProperty("gold").GetInt32()&&f.GetProperty("goldAfter").GetInt32()==snapshot.Value.GetProperty("gold").GetInt32(),"Reward completion facts/state mismatch");rewardsComplete=true;
        }
        if(k=="room-assertion"){
            Static(p,snapshot!.Value);assertions++;
            if(NativeEventFixtures.Scenarios.Contains(scenario)){
                R(f.GetProperty("valid").GetBoolean()&&T(f,"scenario")==scenario&&f.GetProperty("beforeGold").GetInt32()==fixtureBefore.GetProperty("gold").GetInt32()&&f.GetProperty("afterGold").GetInt32()==snapshot.Value.GetProperty("gold").GetInt32(),"Event fixture actual source/result");
                string expected=scenario.StartsWith("event-reflections")?"EVENT.REFLECTIONS":"EVENT.CRYSTAL_SPHERE";
                var beforeEvent=fixtureBefore.GetProperty("lifecycle").GetProperty("eventModel");var afterEvent=snapshot.Value.GetProperty("lifecycle").GetProperty("eventModel");
                R(T(beforeEvent,"model")==expected&&T(afterEvent,"model")==expected&&Eq(beforeEvent.GetProperty("reference"),afterEvent.GetProperty("reference"))&&!beforeEvent.GetProperty("IsFinished").GetBoolean()&&afterEvent.GetProperty("IsFinished").GetBoolean()&&options==1,"Event fixture actual model/instance/completed option");
                if(scenario=="event-reflections-shatter"){
                    var old=fixtureBefore.GetProperty("deck");var current=snapshot.Value.GetProperty("deck");int count=old.GetArrayLength();R(current.GetArrayLength()==2*count+1&&current.EnumerateArray().Select(x=>T(x,"id")).Distinct().Count()==current.GetArrayLength(),"Shatter fixture unique native clone identities");
                    for(int i=0;i<count;i++)R(Eq(old[i],current[i])&&T(old[i],"model")==T(current[i+count],"model")&&old[i].GetProperty("upgrade").GetInt32()==current[i+count].GetProperty("upgrade").GetInt32(),"Shatter fixture old/copy model and upgrade");
                    R(T(current[2*count],"model")=="CARD.BAD_LUCK","Shatter fixture original curse");
                }
            }else{
            R(f.GetProperty("valid").GetBoolean()&&Eq(f.GetProperty("before"),fixtureBefore)&&Eq(f.GetProperty("after"),snapshot.Value),"Room assertion snapshot/valid mismatch");
            var state=snapshot.Value;int hp=state.GetProperty("hp").GetInt32();
            R(fixtureBefore.GetProperty("maxHp").GetInt32()==80&&fixtureBefore.GetProperty("hp").GetInt32()==(scenario=="room-cap"?75:40),"Room fixture input contract mismatch");
            R(scenario!="room-heal"||hp==64,"Independent rest heal mismatch");R(scenario!="room-cap"||hp==80,"Independent rest cap mismatch");R(scenario!="room-abstain"||hp==50,"Independent event heal mismatch");
            R(scenario!="room-phases"||state.GetProperty("maxHp").GetInt32()==82&&hp==39&&options==2,"Independent event phase effect mismatch");
            if(scenario is "room-smith" or "room-cancel"){
                var deck=state.GetProperty("deck");var before=fixtureBefore.GetProperty("deck");R(deck.GetArrayLength()==before.GetArrayLength(),"Room permanent deck length mismatch");
                for(int i=0;i<deck.GetArrayLength();i++)R(T(deck[i],"id")==T(before[i],"id")&&T(deck[i],"model")==T(before[i],"model")&&deck[i].GetProperty("upgrade").GetInt32()==(i==1?1:0),"Room specified instance upgrade mismatch");
            }
            }
        }
        if(k is "death" or "terminal-stamp" or "terminal-contract")Static(p,snapshot!.Value,k=="terminal-contract");
        if(k=="death")R(lossComplete&&lastRoot==lossRoot&&snapshot!.Value.GetProperty("hp").GetInt32()==0,"Room death without native loss authority");
        if(k=="terminal-stamp"){
            R(!stamped&&lossComplete&&lastRoot==lossRoot&&T(f,"terminal")=="Death"&&snapshot!.Value.GetProperty("hp").GetInt32()==0&&Eq(f.GetProperty("native"),snapshot.Value.GetProperty("lifecycle").GetProperty("native")),"Invalid room terminal stamp");stamped=true;
        }
        if(k=="terminal-contract"){
            R(T(f,"terminal")=="Death"&&f.GetProperty("valid").GetBoolean()&&f.GetProperty("staleRejected").GetBoolean()&&f.GetProperty("zeroAdditionalDispatch").GetBoolean()&&!f.GetProperty("executed").GetBoolean(),"Failed room terminal closure");terminalContract=true;
        }
        if(k=="room-slice-complete"){
            Static(p,snapshot!.Value);R(!goal&&!exiting&&f.GetProperty("goal").GetBoolean()&&f.GetProperty("rooms").GetInt32()==roomCount&&f.GetProperty("victories").GetInt32()==victories,"Room slice goal/count mismatch");
            if(scenario=="rooms-natural"){
                R(victories>=2&&roomCount>=4&&snapshot.Value.GetProperty("combat").GetProperty("phase").GetString()=="Play"&&f.GetProperty("persistent").ValueKind==JsonValueKind.Object&&T(f.GetProperty("persistent"),"$ref")==upgraded,"Natural room goal missing actual battles/upgrade");
                var permanent=snapshot.Value.GetProperty("deck").EnumerateArray().Single(x=>T(x,"id")==upgraded);R(permanent.GetProperty("upgrade").GetInt32()==1,"Natural upgraded instance absent");
                R(new[]{"hand","draw","discard","exhaust"}.SelectMany(x=>snapshot.Value.GetProperty("combat").GetProperty(x).EnumerateArray()).Any(x=>x.GetProperty("deck").GetString()==upgraded&&T(x,"model")==T(permanent,"model")&&x.GetProperty("upgrade").GetInt32()==1),"Natural combat copy missing permanent link");
            }
            else R(complete&&exited&&finalized&&assertions==1&&exitStarts==1&&exitReturns==1,"Incomplete room fixture lifecycle");goal=true;
            if(scenario=="room-cancel")R(cancellations==1&&options==2,"Missing actual cancel then retry");
        }
    }
    internal void Finish(JsonElement report){
        ChoiceClosed();
        R(active==""&&!exiting&&starts.SetEquals(returns),"Unfinished room operation");
        var v=report.GetProperty("result");
        if(v.GetProperty("Terminal").GetString()=="Death")R(stamped&&terminalContract&&lossComplete&&confirmed.GetProperty("hp").GetInt32()==0&&confirmed.GetProperty("lifecycle").GetProperty("native").GetProperty("Losses").GetInt32()==1,"Room death lacks native authority");
        else R(goal&&v.GetProperty("Stop").GetString()=="SliceBoundary","Missing room slice goal");
    }
}
