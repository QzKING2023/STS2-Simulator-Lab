using System.Text.Json;
using SimulatorLab.Core;
namespace OfflineSearchHarness;
internal static class NativeSolverLoopContract
{
    static void Need(bool ok,string reason){if(!ok)throw new NativeDataException("Solver loop evidence: "+reason);}
    static bool Same(JsonElement a,JsonElement b)=>NativeDecisionTicket.SameActionJson(a.GetRawText(),b.GetRawText());
    internal static void Review(string directory)
    {
        JsonElement Read(string name)=>JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,name))).RootElement;
        var events=File.ReadLines(Path.Combine(directory,"trace.jsonl")).Select(l=>JsonDocument.Parse(l).RootElement.GetProperty("payload").Clone()).ToArray();
        var controls=Read("solver-loop-controls.json");Need(controls.GetProperty("negativeMappings").GetInt32()==11&&controls.GetProperty("allRejected").GetBoolean()&&controls.GetProperty("zeroAdditionalDispatch").GetBoolean()&&controls.GetProperty("sourceUnchanged").GetBoolean()&&controls.GetProperty("endTurnFieldsAccepted").GetBoolean(),"runtime action-mapping controls");
        string Kind(JsonElement e)=>e.GetProperty("kind").GetString()!;
        JsonElement Facts(JsonElement e)=>e.GetProperty("detail").GetProperty("facts");
        JsonElement State(JsonElement e)=>e.GetProperty("detail").GetProperty("state");
        var profile=Read("solver-profile.json");
        Need(profile.GetProperty("profile").GetString()=="solver-gate/v2"&&profile.GetProperty("information").GetString()=="Oracle"&&events.Single(e=>Kind(e)=="header").GetProperty("detail").GetProperty("information").GetString()=="Oracle","fixed Oracle profile/header");
        Need(!profile.GetProperty("nativeOnly").GetBoolean()&&!profile.GetProperty("entryInitializeCalled").GetBoolean()&&!profile.GetProperty("settingsLoadSaveCalled").GetBoolean()&&!profile.GetProperty("extraSearchPatches").GetBoolean()&&profile.GetProperty("solverVersion").GetString()=="0.50.1","isolated solver identity");
        var dispatch=events.Where(e=>Kind(e)=="dispatch"&&Facts(e).GetProperty("kind").GetString() is "PlayCard" or "EndTurn").ToArray();
        var queries=events.Where(e=>Kind(e)=="solver-query-complete").ToArray();
        var captures=events.Where(e=>Kind(e)=="solver-root-pure").ToArray();
        var complete=events.Single(e=>Kind(e)=="solver-loop-complete");
        Need(dispatch.Length is >0 and <=8&&queries.Length==dispatch.Length+1&&captures.Length==dispatch.Length*2+1,"bounded required operations");
        JsonElement Owner(JsonElement e)=>events.Take(Array.IndexOf(events,e)).Last(x=>Kind(x)=="confirmed");
        void Owned(JsonElement e,JsonElement owner){Need(e.GetProperty("root").GetString()==owner.GetProperty("root").GetString()&&e.GetProperty("context").GetString()==owner.GetProperty("context").GetString()&&Same(State(e),State(owner)),"stationary full state and confirmed owner");}
        int last=-1;
        for(int i=0;i<queries.Length;i++){
            var q=Read("solver-query-"+(i+1)+".json");var e=queries[i];var capture=captures[i*2];var owner=Owner(capture);
            Need(q.GetProperty("query").GetInt32()==i+1&&Facts(e).GetProperty("query").GetInt32()==i+1&&q.GetProperty("boundary").GetString()=="None"&&q.GetProperty("sourceUnchanged").GetBoolean(),"query identity/stop");
            Need(Facts(capture).GetProperty("role").GetString()=="query"&&Facts(capture).GetProperty("query").GetInt32()==i+1,"capture query role");
            string hash=NativeEvidence.Hash(q.GetProperty("root").GetString()!);
            Need(Facts(e).GetProperty("root").GetString()==hash&&Facts(capture).GetProperty("continuation").GetString()==hash&&Facts(capture).GetProperty("information").GetString()=="Oracle","query/capture continuation mode");
            Owned(capture,owner);Owned(e,owner);Need(Same(q.GetProperty("nativeState"),State(e)),"query file state");
            int ci=Array.IndexOf(events,capture),qi=Array.IndexOf(events,e);Need(last<ci&&ci<qi,"capture/query ordered");last=qi;
            if(i==dispatch.Length)break;
            var step=Read("solver-loop-step-"+(i+1)+".json");var d=dispatch[i];var confirm=events.Single(x=>Kind(x)=="confirmed"&&x.GetProperty("root").GetString()==d.GetProperty("root").GetString());var shadow=captures[i*2+1];
            Need(Facts(shadow).GetProperty("role").GetString()=="shadow"&&Facts(shadow).GetProperty("query").GetInt32()==i+1&&Facts(shadow).GetProperty("continuation").GetString()==hash&&Facts(shadow).GetProperty("information").GetString()=="Oracle","shadow role/continuation");Owned(shadow,owner);
            int si=Array.IndexOf(events,shadow),di=Array.IndexOf(events,d),fi=Array.IndexOf(events,confirm);Need(last<si&&si<di&&di<fi,"shadow/dispatch/confirmation order");last=fi;
            Need(Same(step.GetProperty("nativeBefore"),State(d))&&Same(step.GetProperty("nativeBefore"),State(e))&&Same(step.GetProperty("nativeAfter"),State(confirm)),"step full state binding");
            Need(step.GetProperty("query").GetInt32()==i+1&&Same(step.GetProperty("action"),q.GetProperty("actions")[0]),"actual first solver action");
            foreach(string flag in new[]{"exact","parentUnchanged","consumedRejected","staleRejected","unconsumedAtRejection","zeroAdditionalDispatch"})Need(step.GetProperty(flag).GetBoolean(),"step/ticket invariant "+flag);
            Need(step.GetProperty("currentSequence").GetInt64()==step.GetProperty("oldSequence").GetInt64()+1&&!string.IsNullOrEmpty(step.GetProperty("expected").GetString())&&step.GetProperty("expected").GetString()==step.GetProperty("actual").GetString(),"actual continuation and stale boundary");
            var chosen=step.GetProperty("action");var decision=Facts(d).GetProperty("decision");NativeSolverActionContract.Bind(chosen,q,decision);Need(Facts(d).GetProperty("kind").GetString()==chosen.GetProperty("Kind").GetString(),"action kind");
            if(chosen.GetProperty("Kind").GetString()=="EndTurn"){
                Need(decision.GetProperty("turn").GetInt32()==chosen.GetProperty("Turn").GetInt32()&&State(confirm).GetProperty("combat").GetProperty("turn").GetInt32()==chosen.GetProperty("Turn").GetInt32()+1&&State(confirm).GetProperty("combat").GetProperty("phase").GetString()=="Play"&&State(confirm).GetProperty("combat").GetProperty("side").GetString()=="Player","native enemy/next player boundary");
            }else{
                Need(Same(decision.GetProperty("card"),step.GetProperty("card"))&&Same(decision.GetProperty("target"),step.GetProperty("target"))&&decision.GetProperty("model").GetString()=="CARD."+chosen.GetProperty("CardId").GetString(),"native card and target");
            }
        }
        var f=Facts(complete);Need(f.GetProperty("nativeActionsExecuted").GetInt32()==dispatch.Length&&f.GetProperty("queries").GetInt32()==queries.Length&&f.GetProperty("targetedAttack").GetBoolean()&&f.GetProperty("endTurn").GetBoolean()&&!f.GetProperty("wholeBattle").GetBoolean()&&f.GetProperty("information").GetString()=="Oracle","completion scope/counts");
        Need(dispatch.Any(e=>Facts(e).GetProperty("kind").GetString()=="EndTurn")&&dispatch.Any(e=>Facts(e).GetProperty("kind").GetString()=="PlayCard"&&Facts(e).GetProperty("decision").GetProperty("target").ValueKind==JsonValueKind.String),"actual target play and EndTurn required");
        Need(last<Array.IndexOf(events,complete),"completion after requery");Owned(complete,Owner(complete));Need(Same(Read("final-state.json"),State(complete)),"final full state");
    }
}
