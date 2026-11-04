using System.Text.Json;
using SimulatorLab.Core;
namespace OfflineSearchHarness;
internal static class NativeSolverOnlineContract
{
    static void Need(bool ok,string why){if(!ok)throw new NativeDataException("Online solver evidence: "+why);}
    static bool Same(JsonElement a,JsonElement b)=>NativeDecisionTicket.SameActionJson(a.GetRawText(),b.GetRawText());
    internal static void Review(string directory)
    {
        JsonElement Read(string n)=>JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,n))).RootElement;
        var rows=File.ReadLines(Path.Combine(directory,"trace.jsonl")).Select(x=>JsonDocument.Parse(x).RootElement.GetProperty("payload").Clone()).ToArray();
        string K(JsonElement r)=>r.GetProperty("kind").GetString()!;JsonElement F(JsonElement r)=>r.GetProperty("detail").GetProperty("facts");JsonElement S(JsonElement r)=>r.GetProperty("detail").GetProperty("state");
        var header=rows.Single(r=>K(r)=="header").GetProperty("detail");string scenario=header.GetProperty("scenario").GetString()!;
        Need(NativeSolverOnline.IsScenario(scenario)&&header.GetProperty("information").GetString()=="Oracle","registered Oracle fixture");
        var profile=Read("solver-profile.json");Need(profile.GetProperty("information").GetString()=="Oracle"&&profile.GetProperty("solverVersion").GetString()=="0.50.1"&&!profile.GetProperty("nativeOnly").GetBoolean()&&!profile.GetProperty("entryInitializeCalled").GetBoolean()&&!profile.GetProperty("settingsLoadSaveCalled").GetBoolean()&&!profile.GetProperty("extraSearchPatches").GetBoolean(),"isolated fixed profile");
        Need(rows.Count(r=>K(r)=="fixture-setup-declared")==1,"explicit fixture origin");
        var ds=rows.Where(r=>K(r)=="dispatch"&&F(r).GetProperty("kind").GetString() is "PlayCard" or "UsePotion" or "EndTurn").ToArray();
        var qs=rows.Where(r=>K(r)=="solver-query-complete").ToArray();var captures=rows.Where(r=>K(r)=="solver-root-pure").ToArray();
        Need(ds.Length is >0 and <=8&&qs.Length==ds.Length+1&&captures.Length==ds.Length*2+1,"bounded complete query/action set");
        JsonElement Owner(JsonElement e)=>rows.Take(Array.IndexOf(rows,e)).Last(r=>K(r)=="confirmed");
        void Owned(JsonElement e,JsonElement owner){Need(e.GetProperty("root").GetString()==owner.GetProperty("root").GetString()&&e.GetProperty("context").GetString()==owner.GetProperty("context").GetString()&&Same(S(e),S(owner)),"stationary full state/owner");}
        int last=-1;bool choice=false,potion=false;
        for(int i=0;i<qs.Length;i++){
            var q=Read("solver-query-"+(i+1)+".json");var qe=qs[i];var capture=captures[i*2];var owner=Owner(capture);string hash=NativeEvidence.Hash(q.GetProperty("root").GetString()!);
            if(NativeSolverOnline.SmartScenario(scenario)){
                Need(q.GetProperty("engine").GetString()=="CombatSearchCoordinator"&&q.GetProperty("potionPolicy").GetString()=="Smart"&&q.GetProperty("PotionStrategy").GetProperty("Directives").GetArrayLength()==0,"real Smart coordinator without forced directives");
                Need(q.GetProperty("FixedBudget").GetBoolean()&&!q.GetProperty("StopAtAcceptableBattleHpLoss").GetBoolean()&&q.GetProperty("IgnoreLongTermRewards").GetBoolean()&&!q.GetProperty("PredictPotionReward").GetBoolean()&&q.GetProperty("budget").GetProperty("cancelSeconds").GetInt32()==15&&q.GetProperty("TotalExpandedNodes").GetInt64()<=4096,"registered upstream-derived search budget");
            }
            Need(q.GetProperty("query").GetInt32()==i+1&&F(qe).GetProperty("query").GetInt32()==i+1&&q.GetProperty("boundary").GetString()=="None"&&q.GetProperty("sourceUnchanged").GetBoolean(),"actual query identity and successful boundary");
            Need(F(capture).GetProperty("role").GetString()=="query"&&F(capture).GetProperty("query").GetInt32()==i+1&&F(capture).GetProperty("information").GetString()=="Oracle"&&F(capture).GetProperty("continuation").GetString()==hash&&F(qe).GetProperty("root").GetString()==hash,"capture query continuation/role");
            Owned(capture,owner);Owned(qe,owner);Need(Same(q.GetProperty("nativeState"),S(qe)),"query file full state");
            int ci=Array.IndexOf(rows,capture),qi=Array.IndexOf(rows,qe);Need(last<ci&&ci<qi,"ordered query");last=qi;
            if(i==ds.Length)break;
            var step=Read("solver-online-step-"+(i+1)+".json");var d=ds[i];var confirmed=rows.Single(r=>K(r)=="confirmed"&&r.GetProperty("root").GetString()==d.GetProperty("root").GetString());var shadow=captures[i*2+1];var action=step.GetProperty("action");
            Need(F(shadow).GetProperty("role").GetString()=="shadow"&&F(shadow).GetProperty("query").GetInt32()==i+1&&F(shadow).GetProperty("information").GetString()=="Oracle"&&F(shadow).GetProperty("continuation").GetString()==hash,"required shadow capture");Owned(shadow,owner);
            int si=Array.IndexOf(rows,shadow),di=Array.IndexOf(rows,d),fi=Array.IndexOf(rows,confirmed);Need(last<si&&si<di&&di<fi,"shadow/action/confirmation order");last=fi;
            Need(step.GetProperty("query").GetInt32()==i+1&&step.GetProperty("root").GetString()==d.GetProperty("root").GetString()&&step.GetProperty("context").GetString()==d.GetProperty("context").GetString()&&Same(action,q.GetProperty("actions")[0]),"actual unmodified first solver action");
            Need(Same(step.GetProperty("nativeBefore"),S(d))&&Same(S(d),S(qe))&&Same(step.GetProperty("nativeAfter"),S(confirmed)),"full action state binding");
            Need(action.GetProperty("Kind").GetString()==F(d).GetProperty("kind").GetString(),"native action kind");NativeSolverActionContract.Bind(action,q,F(d).GetProperty("decision"),choices:true,potions:true);
            foreach(string flag in new[]{"exact","parentUnchanged","consumedRejected","staleRejected","unconsumedAtRejection","zeroAdditionalDispatch"})Need(step.GetProperty(flag).GetBoolean(),"invariant "+flag);
            Need(!string.IsNullOrEmpty(step.GetProperty("expected").GetString())&&step.GetProperty("expected").GetString()==step.GetProperty("actual").GetString()&&step.GetProperty("actual").GetString()==Read("solver-query-"+(i+2)+".json").GetProperty("root").GetString()&&step.GetProperty("currentSequence").GetInt64()==step.GetProperty("oldSequence").GetInt64()+1,"continuation and ticket sequence");
            Need(Same(step.GetProperty("nativeBefore").GetProperty("deck"),step.GetProperty("nativeAfter").GetProperty("deck")),"permanent deck unchanged");
            NativeSolverFixtureContract.ReviewChildren(rows,step,d,confirmed,q,step.GetProperty("nativeBefore"),step.GetProperty("nativeAfter"));
            choice|=step.GetProperty("children").GetArrayLength()>0;potion|=action.GetProperty("Kind").GetString()=="UsePotion";
            if(NativeSolverOnline.SmartScenario(scenario)&&action.GetProperty("Kind").GetString()=="UsePotion"){
                var before=step.GetProperty("nativeBefore");var after=step.GetProperty("nativeAfter");string id=F(d).GetProperty("decision").GetProperty("potion").GetString()!;
                Need(!after.GetProperty("potions").EnumerateArray().Any(x=>x.GetProperty("id").GetString()==id),"actual solver potion consumed");
                if(action.GetProperty("PotionId").GetString()=="ENERGY_POTION")Need(after.GetProperty("combat").GetProperty("energy").GetInt32()-before.GetProperty("combat").GetProperty("energy").GetInt32()==2&&before.GetProperty("hp").GetInt32()==after.GetProperty("hp").GetInt32(),"independent EnergyPotion two energy without blood payment");
                if(action.GetProperty("PotionId").GetString()=="BLOCK_POTION")Need(after.GetProperty("block").GetInt32()-before.GetProperty("block").GetInt32()==12,"independent BlockPotion twelve block");
            }
        }
        var end=rows.Single(r=>K(r)=="solver-online-complete");var facts=F(end);Owned(end,Owner(end));
        Need(Array.IndexOf(rows,end)>last&&facts.GetProperty("nativeActionsExecuted").GetInt32()==ds.Length&&facts.GetProperty("queries").GetInt32()==qs.Length&&facts.GetProperty("childChoice").GetBoolean()==choice&&facts.GetProperty("potion").GetBoolean()==potion&&!facts.GetProperty("wholeBattle").GetBoolean()&&facts.GetProperty("information").GetString()=="Oracle"&&facts.GetProperty("origin").GetString()=="api-fixture","completion scope and actual counts");
        Need(scenario=="solver-online-choice"?choice:potion,"required actual adopted capability");Need(Same(Read("final-state.json"),S(end)),"final full state");
    }
}
