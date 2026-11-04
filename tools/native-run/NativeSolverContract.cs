using System.Text.Json;
using SimulatorLab.Core;
namespace OfflineSearchHarness;

// Bounded evidence contract for this one-action gate. Collector and native engine
// are trusted producers; this detects omissions, mismatches and accidental drift.
internal static class NativeSolverContract
{
    internal static readonly HashSet<string> Kinds=["solver-root-pure","solver-query-complete","solver-gate-complete","solver-loop-complete","solver-online-complete","solver-battle-opening","solver-terminal-captured","solver-terminal-verified","solver-battle-cleanup","solver-battle-complete","solver-run-opening","solver-run-reward-entry","solver-run-reward-complete","solver-run-next-battle","solver-run-complete"];
    internal static readonly HashSet<string> JourneyKinds=["solver-coordinator-switch","solver-journey-battle-entry","solver-journey-battle-return"];
    static void Need(bool value,string message){if(!value)throw new NativeDataException("Solver evidence: "+message);}
    static bool Same(JsonElement a,JsonElement b)=>NativeDecisionTicket.SameActionJson(a.GetRawText(),b.GetRawText());
    internal static void Review(string directory)
    {
        JsonElement Read(string name)=>JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,name))).RootElement;
        var events=File.ReadLines(Path.Combine(directory,"trace.jsonl")).Select(l=>JsonDocument.Parse(l).RootElement.GetProperty("payload").Clone()).ToArray();
        var profile=Read("solver-profile.json");
        Need(profile.GetProperty("profile").GetString()=="solver-gate/v2"&&profile.GetProperty("solverVersion").GetString()=="0.50.1"&&!profile.GetProperty("nativeOnly").GetBoolean()&&!profile.GetProperty("entryInitializeCalled").GetBoolean()&&!profile.GetProperty("settingsLoadSaveCalled").GetBoolean()&&!profile.GetProperty("extraSearchPatches").GetBoolean(),"fixed solver configuration identity");
        Need(profile.GetProperty("information").GetString()=="Oracle"&&events.Single(e=>e.GetProperty("kind").GetString()=="header").GetProperty("detail").GetProperty("information").GetString()=="Oracle","mandatory consistent Oracle information mode");
        var queries=events.Where(e=>e.GetProperty("kind").GetString()=="solver-query-complete").ToArray();
        var ends=events.Where(e=>e.GetProperty("kind").GetString()=="solver-gate-complete").ToArray();
        Need(queries.Length==2&&ends.Length==1,"required unique query/completion records");
        var dispatch=events.Where(e=>e.GetProperty("kind").GetString()=="dispatch"&&e.GetProperty("detail").GetProperty("facts").GetProperty("kind").GetString()=="PlayCard").ToArray();
        Need(dispatch.Length==1,"exactly one actual combat action");var play=dispatch.Single();
        var confirm=events.Single(e=>e.GetProperty("kind").GetString()=="confirmed"&&e.GetProperty("root").GetString()==play.GetProperty("root").GetString());
        Need(Array.IndexOf(events,queries[0])<Array.IndexOf(events,play)&&Array.IndexOf(events,play)<Array.IndexOf(events,confirm)&&Array.IndexOf(events,confirm)<Array.IndexOf(events,queries[1])&&Array.IndexOf(events,queries[1])<Array.IndexOf(events,ends[0]),"query/action/confirmation order");
        var step=Read("solver-step.json");
        var captures=events.Where(e=>e.GetProperty("kind").GetString()=="solver-root-pure").ToArray();
        Need(captures.Length==3,"three required capture roles");
        for(int i=0;i<3;i++){
            int query=i==2?2:1;var capture=captures[i];var facts=capture.GetProperty("detail").GetProperty("facts");
            Need(facts.GetProperty("role").GetString()==(i==1?"shadow":"query")&&facts.GetProperty("query").GetInt32()==query,"capture role/order identity");
            var owner=events.Take(Array.IndexOf(events,capture)).Last(e=>e.GetProperty("kind").GetString()=="confirmed");
            Need(capture.GetProperty("root").GetString()==owner.GetProperty("root").GetString()&&capture.GetProperty("context").GetString()==owner.GetProperty("context").GetString(),"capture confirmed owner");
            Need(Same(capture.GetProperty("detail").GetProperty("state"),owner.GetProperty("detail").GetProperty("state"))&&Same(capture.GetProperty("detail").GetProperty("state"),step.GetProperty(i==2?"nativeAfter":"nativeBefore")),"capture full stationary state");
            Need(facts.GetProperty("information").GetString()=="Oracle"&&facts.GetProperty("continuation").GetString()==NativeEvidence.Hash(Read("solver-query-"+query+".json").GetProperty("root").GetString()!),"capture Oracle/continuation binding");
        }
        Need(Array.IndexOf(events,captures[0])<Array.IndexOf(events,queries[0])&&Array.IndexOf(events,queries[0])<Array.IndexOf(events,captures[1])&&Array.IndexOf(events,captures[1])<Array.IndexOf(events,play)&&Array.IndexOf(events,confirm)<Array.IndexOf(events,captures[2])&&Array.IndexOf(events,captures[2])<Array.IndexOf(events,queries[1]),"capture/query/action phase order");
        Need(step.GetProperty("exact").GetBoolean()&&step.GetProperty("parentUnchanged").GetBoolean()&&step.GetProperty("consumedRejected").GetBoolean(),"step invariants");
        Need(!string.IsNullOrEmpty(step.GetProperty("expected").GetString())&&step.GetProperty("expected").GetString()==step.GetProperty("actual").GetString(),"full native/predicted continuation");
        Need(Same(step.GetProperty("nativeBefore"),play.GetProperty("detail").GetProperty("state"))&&Same(step.GetProperty("nativeAfter"),confirm.GetProperty("detail").GetProperty("state")),"step snapshots bound to actual action");
        var action=play.GetProperty("detail").GetProperty("facts").GetProperty("decision");
        NativeSolverActionContract.Bind(step.GetProperty("action"),Read("solver-query-1.json"),action);
        Need(action.GetProperty("card").GetString()==step.GetProperty("card").GetString()&&Same(action.GetProperty("target"),step.GetProperty("target")),"actual card/target identity");
        for(int i=0;i<2;i++){
            var q=Read("solver-query-"+(i+1)+".json");var e=queries[i];var facts=e.GetProperty("detail").GetProperty("facts");
            Need(facts.GetProperty("query").GetInt32()==i+1&&q.GetProperty("query").GetInt32()==i+1,"query identity");
            Need(q.GetProperty("boundary").GetString()=="None"&&q.GetProperty("sourceUnchanged").GetBoolean()&&facts.GetProperty("root").GetString()==NativeEvidence.Hash(q.GetProperty("root").GetString()!),"query source/stop");
            Need(Same(q.GetProperty("nativeState"),e.GetProperty("detail").GetProperty("state"))&&Same(q.GetProperty("nativeState"),step.GetProperty(i==0?"nativeBefore":"nativeAfter")),"query full state binding");
            var owner=i==0?events.Take(Array.IndexOf(events,e)).Last(x=>x.GetProperty("kind").GetString()=="confirmed"):confirm;
            Need(e.GetProperty("context").GetString()==owner.GetProperty("context").GetString()&&e.GetProperty("root").GetString()==owner.GetProperty("root").GetString(),"query context/root");
        }
        Need(Same(step.GetProperty("action"),Read("solver-query-1.json").GetProperty("actions")[0]),"adopted first solver action");
        var chosen=step.GetProperty("action");Need(action.GetProperty("model").GetString()=="CARD."+chosen.GetProperty("CardId").GetString(),"actual action model");
        Need(Same(ends[0].GetProperty("detail").GetProperty("state"),step.GetProperty("nativeAfter"))&&Same(Read("final-state.json"),step.GetProperty("nativeAfter")),"final stationary state");
        var completion=ends[0].GetProperty("detail").GetProperty("facts");
        Need(completion.GetProperty("queries").GetInt32()==queries.Length&&completion.GetProperty("nativeActionsExecuted").GetInt32()==dispatch.Length&&!completion.GetProperty("wholeBattle").GetBoolean()&&completion.GetProperty("prediction").GetString()=="diff-certified-one-action"&&completion.GetProperty("staleRejected").GetBoolean(),"completion facts bound to actual one-action scope");
        var tickets=Read("solver-ticket-checks.json");
        Need(tickets.GetProperty("freshBefore").GetBoolean()&&tickets.GetProperty("unconsumedAtRejection").GetBoolean()&&tickets.GetProperty("staleRejected").GetBoolean()&&tickets.GetProperty("consumedRejected").GetBoolean()&&tickets.GetProperty("zeroAdditionalDispatch").GetBoolean()&&tickets.GetProperty("stateChanged").GetBoolean()&&tickets.GetProperty("currentSequence").GetInt64()==tickets.GetProperty("oldSequence").GetInt64()+1,"independent unconsumed stale and consumed ticket controls");
        var controls=Read("solver-controls.json");Need(controls.GetProperty("negativeMappings").GetInt32()==13&&controls.GetProperty("allRejected").GetBoolean()&&controls.GetProperty("zeroAdditionalDispatch").GetBoolean(),"mapping rejection controls");
    }
}
