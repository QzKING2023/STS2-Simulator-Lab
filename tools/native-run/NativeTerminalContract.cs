using System.Text.Json;
using System.Text.Json.Nodes;

namespace OfflineSearchHarness;
// Bounded N1-T contract: callbacks belong to dispatched roots, terminal stamps to
// confirmed boundaries. Transition contexts deliberately name the starting room.
internal sealed class NativeTerminalContract
{
    sealed record Owner(string Root,string Context,string Kind,JsonElement Action);
    readonly Dictionary<string,int> kinds=[];
    readonly Dictionary<string,(string Type,string? Model)> identities=[];
    readonly Dictionary<string,int> counters=new(){["Losses"]=0,["RunEndStarts"]=0,["RunEndReturns"]=0,["WinStarts"]=0,["WinReturns"]=0,["ActStarts"]=0,["ActReturns"]=0,["PlatformRefreshes"]=0};
    Owner? active,lastConfirmed,actOwner,winOwner,lossOwner;
    JsonElement lastState,lastConfirmedState,actBefore,actAfter,dispatchState;
    string scenario="",terminal="";
    string? architectId,firstDecision;
    bool fixture,tailValid,terminalValid,won,closed;
    int position,loss=-1,end=-1,win=-1,stamp=-1,contract=-1,transition=-1,transitionDone=-1;
    static void Require(bool value,string message){if(!value)throw new NativeDataException(message);}
    static string Text(JsonElement e,string key)=>e.GetProperty(key).GetString()??throw new NativeDataException("Null lifecycle field "+key);
    static bool Same(JsonElement a,JsonElement b)=>SimulatorLab.Core.NativeDecisionTicket.SameActionJson(a.GetRawText(),b.GetRawText());
    static int Map(JsonElement s)=>s.GetProperty("lifecycle").GetProperty("mapGeneration").GetInt32();
    static JsonElement Event(JsonElement s)=>s.GetProperty("lifecycle").GetProperty("eventModel");
    static bool Matches(Owner? owner,string root,string context)=>owner!=null&&owner.Root==root&&owner.Context==context;
    void Inside(string root,string context,string kind)=>Require(Matches(active,root,context)&&active!.Kind==kind,"Lifecycle active action owner mismatch");
    void After(string root,string context)=>Require(active==null&&Matches(lastConfirmed,root,context),"Lifecycle confirmed boundary owner mismatch");
    void Once(string kind)=>Require(kinds[kind]==1,"Duplicate lifecycle event: "+kind);
    // Only post-confirmation observations use this invariant. Native callbacks
    // before confirmation may legitimately change HP, RNG and other game state.
    void CheckConfirmedState(string kind,bool close=false)
    {
        Require(lastConfirmedState.ValueKind==JsonValueKind.Object,"Missing confirmed lifecycle state");
        var expected=JsonNode.Parse(lastConfirmedState.GetRawText())!;
        if(close){
            Require(!expected["lifecycle"]!["native"]!["Closed"]!.GetValue<bool>(),"Confirmed terminal boundary already closed");
            expected["lifecycle"]!["native"]!["Closed"]=true;
        }
        Require(Same(lastState,JsonSerializer.SerializeToElement(expected)),"Confirmed lifecycle state mismatch: "+kind);
    }
    void CheckState(JsonElement state)
    {
        var l=state.GetProperty("lifecycle");
        Require(l.ValueKind==JsonValueKind.Object&&l.GetProperty("native").ValueKind==JsonValueKind.Object,"Missing lifecycle snapshot");
        var native=l.GetProperty("native");
        foreach(var (key,value) in counters)Require(native.TryGetProperty(key,out var count)&&count.ValueKind==JsonValueKind.Number&&count.GetInt32()==value,"Lifecycle counter mismatch: "+key);
        Require(native.GetProperty("Won").GetBoolean()==won&&native.GetProperty("Closed").GetBoolean()==closed,"Lifecycle Won/Closed mismatch");
        Require(Map(state)>=0,"Invalid lifecycle map generation");
        if(state.GetProperty("room").GetString()=="EventRoom"){
            var ev=Event(state);Require(ev.ValueKind==JsonValueKind.Object,"Missing event model snapshot");
            var rng=ev.GetProperty("rng");Require(rng.ValueKind==JsonValueKind.Object,"Missing event model RNG");
            Require(rng.GetProperty("counter").GetInt64()>=0,"Invalid event model RNG");foreach(string word in new[]{"s0","s1","s2","s3"})_=rng.GetProperty(word).GetUInt64();
            Require(ev.TryGetProperty("reference",out var reference)&&reference.ValueKind==JsonValueKind.Object&&ev.TryGetProperty("model",out var modelValue)&&modelValue.ValueKind==JsonValueKind.String&&ev.TryGetProperty("IsFinished",out var finished)&&finished.ValueKind is JsonValueKind.True or JsonValueKind.False,"Missing event model identity/completion");
            string id=Text(ev.GetProperty("reference"),"$ref"),model=Text(ev,"model");_=ev.GetProperty("IsFinished").GetBoolean();
            Require(identities.TryGetValue(id,out var identity)&&identity.Model==model,"Event model identity mismatch");
            if(model=="EVENT.THE_ARCHITECT"){
                Require(identity.Type=="TheArchitect"&&(architectId==null||architectId==id),"Architect instance identity mismatch");architectId=id;
            }
            if(architectId!=null)Require(id==architectId&&model=="EVENT.THE_ARCHITECT","Architect event continuity mismatch");
        }else Require(Event(state).ValueKind==JsonValueKind.Null,"Event snapshot outside EventRoom");
        if(stamp>=0){
            Require(state.GetProperty("hp").GetInt32()==0,"Terminal continuation snapshot is alive");
            if(terminal=="RunVictory")Require(Event(state).GetProperty("IsFinished").GetBoolean(),"Terminal Architect completion regressed");
        }
        lastState=state.Clone();
    }
    internal void Accept(JsonElement p,JsonElement? state)
    {
        string kind=Text(p,"kind"),root=Text(p,"root"),ctx=Text(p,"context");var d=p.GetProperty("detail");position++;kinds[kind]=kinds.GetValueOrDefault(kind)+1;
        if(kind=="header")scenario=Text(d,"scenario");
        if(kind=="identity")identities.Add(Text(d,"id"),(Text(d,"type"),d.GetProperty("model").GetString()));
        if(kind is "root-request" or "dispatch")Require(stamp<0,"Action after terminal stamp");
        if(kind=="dispatch"){
            active=new(root,ctx,Text(d.GetProperty("facts"),"kind"),d.GetProperty("facts").Clone());dispatchState=state!.Value.Clone();
            if(active.Kind=="OldActVoteControl")Require(transitionDone>=0&&Same(dispatchState,actAfter),"Old vote before/after transition mismatch");
        }
        // Native counters increment before their corresponding observation snapshot.
        switch(kind){
            case "act-transition-start":
                Once(kind);Inside(root,ctx,"ActReady");actOwner=active;transition=position;actBefore=state!.Value.Clone();counters["ActStarts"]++;break;
            case "act-transition-completed":
                Once(kind);Inside(root,ctx,"ActReady");Require(transition>=0&&Matches(actOwner,root,ctx),"Incomplete tail fixture lifecycle: transition owner/start");transitionDone=position;actAfter=state!.Value.Clone();counters["ActReturns"]++;break;
            case "run-win-start":
                Once(kind);Inside(root,ctx,"ArchitectOption");Require(transitionDone>=0&&Text(active!.Action.GetProperty("decision"),"model")=="EVENT.THE_ARCHITECT"&&Text(active.Action.GetProperty("decision"),"TextKey")=="PROCEED","Win without native Architect proceed");winOwner=active;counters["WinStarts"]++;break;
            case "run-end-start":
                Once(kind);Inside(root,ctx,"ArchitectOption");Require(counters["WinStarts"]==1&&Matches(winOwner,root,ctx)&&d.GetProperty("facts").GetProperty("victory").GetBoolean(),"OnEnded without matching WinRun");counters["RunEndStarts"]++;break;
            case "run-end-return":
                Once(kind);Inside(root,ctx,"ArchitectOption");Require(counters["RunEndStarts"]==1&&Matches(winOwner,root,ctx)&&d.GetProperty("facts").GetProperty("victory").GetBoolean(),"Invalid native OnEnded completion");end=position;counters["RunEndReturns"]++;won=true;break;
            case "run-win-completed":
                Once(kind);Inside(root,ctx,"ArchitectOption");Require(end>=0&&counters["WinStarts"]==1&&Matches(winOwner,root,ctx),"Win completion without successful OnEnded");win=position;counters["WinReturns"]++;break;
            case "battle-loss-start":Once(kind);Inside(root,ctx,"EndTurn");lossOwner=active;break;
            case "battle-loss-confirmed":
                Once(kind);Inside(root,ctx,"EndTurn");Require(kinds.GetValueOrDefault("battle-loss-start")==1&&Matches(lossOwner,root,ctx),"Loss completion missing start/owner");loss=position;counters["Losses"]++;break;
            case "platform-offline-guard":
                Inside(root,ctx,"EventRoomEnter");Require(Text(d,"source")=="SteamStatsManager.RefreshGlobalStats"&&!d.GetProperty("initialized").GetBoolean(),"Invalid offline platform guard");counters["PlatformRefreshes"]++;break;
            case "architect-presentation":Require(Matches(active,root,ctx)&&active!.Kind is "EventRoomEnter" or "ArchitectOption","Architect presentation owner mismatch");break;
            case "terminal-contract":Once(kind);After(root,ctx);Require(stamp>=0,"Terminal contract before terminal stamp");closed=true;break;
        }
        if(state is {} s)CheckState(s);
        if(kind=="run-end-return")Require(lastState.GetProperty("hp").GetInt32()==dispatchState.GetProperty("hp").GetInt32(),"OnEnded changed pre-cleanup HP snapshot");
        if(kind is "run-win-completed" or "battle-loss-confirmed")Require(lastState.GetProperty("hp").GetInt32()==0,"Native terminal completion snapshot is alive");
        if(kind=="confirmed"){
            Require(Matches(active,root,ctx),"Lifecycle root confirmation mismatch");
            if(active!.Kind=="OldActVoteControl")Require(Same(dispatchState,state!.Value),"Old vote changed actual state");
            lastConfirmed=active;lastConfirmedState=state!.Value.Clone();active=null;
        }
        if(kind=="fixture-setup-declared"){
            Once(kind);After(root,ctx);var f=d.GetProperty("facts");Require(Text(f,"name")==scenario&&Text(f,"fixtureKind")=="lifecycle","Invalid lifecycle fixture declaration");fixture=true;
        }
        if(kind=="act-transition-start")Require(fixture&&active!.Action.GetProperty("decision").GetProperty("act").GetInt32()==actBefore.GetProperty("act").GetInt32()&&active.Action.GetProperty("decision").GetProperty("map").GetInt32()==Map(actBefore),"ActReady state/action mismatch");
        if(kind=="act-transition-completed"){
            Require(actAfter.GetProperty("hp").GetInt32()==actBefore.GetProperty("hp").GetInt32()&&actAfter.GetProperty("gold").GetInt32()==actBefore.GetProperty("gold").GetInt32()&&Same(actAfter.GetProperty("deck"),actBefore.GetProperty("deck")),"Transition persistent state mismatch");
            if(scenario=="tail-next-act")Require(actAfter.GetProperty("act").GetInt32()==actBefore.GetProperty("act").GetInt32()+1&&Map(actAfter)>Map(actBefore)&&Text(actAfter,"room")=="MapRoom","Next-act completion snapshot mismatch");
            else Require(actAfter.GetProperty("act").GetInt32()==actBefore.GetProperty("act").GetInt32()&&Map(actAfter)==Map(actBefore)&&Text(actAfter,"room")=="EventRoom"&&Text(Event(actAfter),"model")=="EVENT.THE_ARCHITECT","Architect transition snapshot mismatch");
        }
        if(kind=="map-candidates"&&scenario=="tail-next-act"){
            After(root,ctx);Require(lastConfirmed==actOwner&&transitionDone>=0,"Next-act candidates before transition confirmation");var f=d.GetProperty("facts");Require(Text(f,"stage")=="next-act-first-decision"&&f.GetProperty("mapGeneration").GetInt32()==Map(actAfter),"Next-act map candidate mismatch");firstDecision=Text(f,"to");
        }
        if(kind=="tail-event-candidates"){
            After(root,ctx);Require(architectId!=null&&!Event(lastState).GetProperty("IsFinished").GetBoolean()&&d.GetProperty("facts").ValueKind==JsonValueKind.Array&&d.GetProperty("facts").GetArrayLength()>0,"Invalid Architect decision candidates");
        }
        if(kind=="death"){Once(kind);After(root,ctx);Require(Matches(lossOwner,root,ctx)&&loss>=0&&lastState.GetProperty("hp").GetInt32()==0,"Death without confirmed native loss");CheckConfirmedState(kind);}
        if(kind=="terminal-stamp"){
            Once(kind);After(root,ctx);var f=d.GetProperty("facts");terminal=Text(f,"terminal");stamp=position;
            Require(terminal=="RunVictory"?win>=0&&end<win&&Matches(winOwner,root,ctx):terminal=="Death"&&loss>=0&&win<0&&Matches(lossOwner,root,ctx),"Terminal without native completion authority");
            Require(lastState.GetProperty("hp").GetInt32()==0,"Terminal stamp requires actual dead player snapshot");
            Require(Same(f.GetProperty("native"),lastState.GetProperty("lifecycle").GetProperty("native")),"Terminal facts/snapshot authority mismatch");
            Require(Text(f,"scope")== (scenario=="natural-death"?"natural-run":"api-fixture"),"Terminal origin mismatch");
            if(terminal=="RunVictory")Require(Event(lastState).GetProperty("IsFinished").GetBoolean(),"Architect option not finished at terminal stamp");
            CheckConfirmedState(kind);
        }
        if(kind=="terminal-contract"){
            var f=d.GetProperty("facts");Require(Text(f,"terminal")==terminal,"Terminal contract/stamp mismatch");terminalValid=f.GetProperty("valid").GetBoolean()&&f.GetProperty("staleRejected").GetBoolean()&&f.GetProperty("zeroAdditionalDispatch").GetBoolean()&&!f.GetProperty("executed").GetBoolean();Require(terminalValid,"Failed terminal contract");contract=position;
            CheckConfirmedState(kind,close:true);
        }
        if(kind=="tail-contract"){
            Once(kind);After(root,ctx);var f=d.GetProperty("facts");tailValid=f.GetProperty("valid").GetBoolean();Require(tailValid,"Failed tail contract");Require(transitionDone>=0,"Incomplete tail fixture lifecycle: missing transition completion");
            if(scenario=="tail-next-act"){
                Require(Text(f,"kind")=="next-act"&&lastConfirmed!.Kind=="OldActVoteControl"&&lastConfirmed.Action.GetProperty("decision").GetProperty("oldAct").GetInt32()==actBefore.GetProperty("act").GetInt32(),"Old act vote owner mismatch");
                Require(f.GetProperty("from").GetInt32()==actBefore.GetProperty("act").GetInt32()&&f.GetProperty("to").GetInt32()==actAfter.GetProperty("act").GetInt32()&&f.GetProperty("beforeMap").GetInt32()==Map(actBefore)&&f.GetProperty("afterMap").GetInt32()==Map(actAfter),"Tail transition facts/snapshot mismatch");
                Require(Text(f,"room")=="MapRoom"&&Text(lastState,"room")=="MapRoom"&&lastState.GetProperty("act").GetInt32()==actAfter.GetProperty("act").GetInt32()&&Map(lastState)==Map(actAfter)&&Same(lastState.GetProperty("deck"),actAfter.GetProperty("deck"))&&lastState.GetProperty("hp").GetInt32()==actAfter.GetProperty("hp").GetInt32()&&lastState.GetProperty("gold").GetInt32()==actAfter.GetProperty("gold").GetInt32(),"Tail current snapshot mismatch");
                Require(f.GetProperty("staleIgnored").GetBoolean()&&firstDecision!=null&&Text(f,"firstDecision")==firstDecision&&kinds.GetValueOrDefault("map-candidates")==1,"Invalid act transition contract");
            }else Require(Text(f,"kind")=="architect"&&contract>stamp&&f.GetProperty("eventFinished").GetBoolean()&&Event(lastState).GetProperty("IsFinished").GetBoolean()&&!f.GetProperty("normalStartFullRun").GetBoolean(),"Invalid Architect tail completion");
            CheckConfirmedState(kind,close:scenario=="tail-architect");
        }
    }
    internal void Finish(NativeScenario scenario,JsonElement report)
    {
        var result=report.GetProperty("result");Require(active==null,"Unconfirmed lifecycle root");
        if(scenario.Scenario=="natural-death"){
            Require(terminal=="Death"&&loss<stamp&&contract>stamp&&terminalValid&&kinds.GetValueOrDefault("battle-loss-confirmed")==1&&kinds.GetValueOrDefault("death")==1&&kinds.GetValueOrDefault("reward-offer")==0&&counters["RunEndReturns"]==0,"Incomplete actual death lifecycle");
            Require(result.GetProperty("Terminal").GetString()=="Death"&&result.GetProperty("Stop").GetString()=="Completed","Death result mismatch");
        }else{
            Require(fixture&&tailValid&&transition>=0&&transitionDone>transition&&counters["ActReturns"]==1,"Incomplete tail fixture lifecycle");
            if(scenario.Scenario=="tail-architect")Require(terminal=="RunVictory"&&terminalValid&&contract>stamp&&stamp>win&&kinds.GetValueOrDefault("tail-event-candidates")>0&&counters["RunEndReturns"]==1&&counters["WinReturns"]==1&&counters["PlatformRefreshes"]==1&&result.GetProperty("Terminal").GetString()=="RunVictory"&&result.GetProperty("Stop").GetString()=="Completed","Incomplete actual Architect victory");
            else Require(result.GetProperty("Terminal").GetString()=="Ongoing"&&result.GetProperty("Stop").GetString()=="SliceBoundary","Next-act fixture result mismatch");
        }
    }
}
