using System.Text.Json;
using SimulatorLab.Core;
namespace OfflineSearchHarness;
internal static class NativeSolverFixtureContract
{
    static void Need(bool ok,string why){if(!ok)throw new NativeDataException("Solver fixture evidence: "+why);}
    static bool Same(JsonElement a,JsonElement b)=>NativeDecisionTicket.SameActionJson(a.GetRawText(),b.GetRawText());
    internal static void Review(string directory)
    {
        JsonElement Read(string n)=>JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,n))).RootElement;
        var rows=File.ReadLines(Path.Combine(directory,"trace.jsonl")).Select(x=>JsonDocument.Parse(x).RootElement.GetProperty("payload").Clone()).ToArray();
        string Kind(JsonElement r)=>r.GetProperty("kind").GetString()!;JsonElement State(JsonElement r)=>r.GetProperty("detail").GetProperty("state");JsonElement Facts(JsonElement r)=>r.GetProperty("detail").GetProperty("facts");
        var report=Read("solver-choice-potion-report.json");Need(report.GetProperty("actions").GetInt32()==2&&!report.GetProperty("wholeBattle").GetBoolean()&&report.GetProperty("information").GetString()=="Oracle","fixed API fixture scope");
        Need(rows.Single(x=>Kind(x)=="header").GetProperty("detail").GetProperty("information").GetString()=="Oracle","Oracle header");
        var profile=Read("solver-profile.json");Need(profile.GetProperty("profile").GetString()=="solver-gate/v2"&&profile.GetProperty("information").GetString()=="Oracle"&&profile.GetProperty("solverVersion").GetString()=="0.50.1"&&!profile.GetProperty("nativeOnly").GetBoolean()&&!profile.GetProperty("entryInitializeCalled").GetBoolean()&&!profile.GetProperty("settingsLoadSaveCalled").GetBoolean()&&!profile.GetProperty("extraSearchPatches").GetBoolean(),"required isolated Oracle profile");
        var asserts=rows.Where(r=>Kind(r)=="fixture-assertion").ToArray();Need(asserts.Length==4,"complete per-action assertion set");
        Need(rows.Count(r=>Kind(r)=="fixture-setup-declared")==1,"one explicit API fixture declaration");
        var ds=rows.Where(r=>Kind(r)=="dispatch"&&Facts(r).GetProperty("kind").GetString() is "PlayCard" or "UsePotion").ToArray();Need(ds.Length==2,"actual two required actions");
        Need(rows.Count(r=>Kind(r)=="solver-root-pure")==4&&rows.Count(r=>Kind(r)=="solver-query-complete")==2,"exact fixture capture and requery set");
        for(int i=0;i<2;i++){
            var step=Read("solver-fixture-step-"+(i+1)+".json");var d=ds[i];string root=d.GetProperty("root").GetString()!;var c=rows.Single(r=>Kind(r)=="confirmed"&&r.GetProperty("root").GetString()==root);
            var input=step.GetProperty("input");var before=input.GetProperty("nativeState");var after=step.GetProperty("nativeAfter");var a=step.GetProperty("action");
            Need(step.GetProperty("number").GetInt32()==i+1&&step.GetProperty("root").GetString()==root&&step.GetProperty("context").GetString()==d.GetProperty("context").GetString()&&Same(before,State(d))&&Same(after,State(c)),"input/actual action identity and full states");
            Need(a.GetProperty("Kind").GetString()==Facts(d).GetProperty("kind").GetString()&&a.GetProperty("Kind").GetString()==(i==0?"PlayCard":"UsePotion"),"expected actual kind");
            var expectedNames=new[]{"solver fixture permanent deck content and references unchanged",i==0?"upgraded TrueGrit actual nine block / one energy":"native FirePotion twenty damage and actual slot removal"};
            var actualAsserts=asserts.Where(r=>r.GetProperty("root").GetString()==root).ToArray();
            Need(actualAsserts.Length==2&&actualAsserts.All(r=>Facts(r).GetProperty("valid").GetBoolean()&&r.GetProperty("context").GetString()==c.GetProperty("context").GetString()&&Same(State(r),after))&&actualAsserts.Select(r=>Facts(r).GetProperty("name").GetString()).Order().SequenceEqual(expectedNames.Order()),"unique per-action assertions / actual confirmation state");
            Need(Same(before.GetProperty("deck"),after.GetProperty("deck")),"full permanent deck unchanged");
            if(i==0)Need(after.GetProperty("block").GetInt32()-before.GetProperty("block").GetInt32()==9&&before.GetProperty("combat").GetProperty("energy").GetInt32()-after.GetProperty("combat").GetProperty("energy").GetInt32()==1,"independent upgraded TrueGrit rule");
            else Need(before.GetProperty("combat").GetProperty("enemies")[0].GetProperty("hp").GetInt32()-after.GetProperty("combat").GetProperty("enemies")[0].GetProperty("hp").GetInt32()==20&&!after.GetProperty("potions").EnumerateArray().Any(x=>x.GetProperty("id").GetString()==Facts(d).GetProperty("decision").GetProperty("potion").GetString()),"independent FirePotion damage and removal");
            NativeSolverActionContract.Bind(a,input,Facts(d).GetProperty("decision"),choices:true,potions:true);
            Need(step.GetProperty("exact").GetBoolean()&&step.GetProperty("parentUnchanged").GetBoolean()&&step.GetProperty("expected").GetString()==step.GetProperty("actual").GetString()&&!string.IsNullOrEmpty(step.GetProperty("actual").GetString()),"exact prediction and parent");
            var captures=rows.Where(r=>Kind(r)=="solver-root-pure"&&Facts(r).GetProperty("role").GetString()=="fixture"&&Facts(r).GetProperty("query").GetInt32()==i+1).ToArray();Need(captures.Length==1&&Same(State(captures[0]),before)&&Facts(captures[0]).GetProperty("continuation").GetString()==NativeEvidence.Hash(input.GetProperty("root").GetString()!),"fixture capture binding");
            var owner=rows.Take(Array.IndexOf(rows,captures[0])).Last(r=>Kind(r)=="confirmed");Need(captures[0].GetProperty("root").GetString()==owner.GetProperty("root").GetString()&&captures[0].GetProperty("context").GetString()==owner.GetProperty("context").GetString()&&Same(State(owner),before),"fixture immutable confirmed owner");
            Need(Array.IndexOf(rows,captures[0])<Array.IndexOf(rows,d)&&Array.IndexOf(rows,d)<Array.IndexOf(rows,c),"capture dispatch confirmation order");
            var q=Read("solver-query-"+(i+1)+".json");Need(q.GetProperty("sourceUnchanged").GetBoolean()&&Same(q.GetProperty("nativeState"),after)&&q.GetProperty("root").GetString()==step.GetProperty("actual").GetString(),"real post-action requery");
            var qe=rows.Single(r=>Kind(r)=="solver-query-complete"&&Facts(r).GetProperty("query").GetInt32()==i+1);Need(qe.GetProperty("root").GetString()==root&&qe.GetProperty("context").GetString()==c.GetProperty("context").GetString()&&Same(State(qe),after)&&Array.IndexOf(rows,qe)>Array.IndexOf(rows,c)&&Facts(qe).GetProperty("root").GetString()==NativeEvidence.Hash(q.GetProperty("root").GetString()!),"post-action query observation owner/hash/order");
            var qc=rows.Single(r=>Kind(r)=="solver-root-pure"&&Facts(r).GetProperty("role").GetString()=="query"&&Facts(r).GetProperty("query").GetInt32()==i+1);
            Need(qc.GetProperty("root").GetString()==root&&qc.GetProperty("context").GetString()==c.GetProperty("context").GetString()&&Same(State(qc),after)&&Facts(qc).GetProperty("information").GetString()=="Oracle"&&Facts(captures[0]).GetProperty("information").GetString()=="Oracle"&&Facts(qc).GetProperty("continuation").GetString()==NativeEvidence.Hash(q.GetProperty("root").GetString()!)&&Array.IndexOf(rows,c)<Array.IndexOf(rows,qc)&&Array.IndexOf(rows,qc)<Array.IndexOf(rows,qe),"required owned post-action capture");
            ReviewChildren(rows,step,d,c,input,before,after,fixedNonFirst:i==0);

        }
        Need(Same(Read("final-state.json"),Read("solver-fixture-step-2.json").GetProperty("nativeAfter")),"final stationary state");
    }
    internal static void ReviewChildren(JsonElement[] rows,JsonElement step,JsonElement d,JsonElement c,JsonElement input,JsonElement before,JsonElement after,bool fixedNonFirst=false)
    {
        string Kind(JsonElement r)=>r.GetProperty("kind").GetString()!;var a=step.GetProperty("action");string root=d.GetProperty("root").GetString()!;
        if(a.GetProperty("Kind").GetString()=="UsePotion"&&a.GetProperty("Choice").ValueKind!=JsonValueKind.Null){NativeGeneratedChoiceEvidence.Check(rows,step,input);return;}
        if(a.GetProperty("TurnStartChoices").ValueKind==JsonValueKind.Array&&a.GetProperty("TurnStartChoices").GetArrayLength()>0){NativeSolverPhaseProtocol.ReviewChildren(rows,step,d,c,input);return;}
        Need(a.GetProperty("NestedChoices").ValueKind==JsonValueKind.Null||a.GetProperty("NestedChoices").GetArrayLength()==0,"no nested choice capability");
        Need(a.GetProperty("NestedChoicesBeforePrimary").GetInt32()==0,"exact primary-only choice set");
        if(a.GetProperty("Choice").ValueKind==JsonValueKind.Object&&a.GetProperty("Choice").GetProperty("Effect").GetString() is "Discard" or "Duplicate" or "ApplyRetain" or "MoveToDrawTop"){NativeHandDiscardEvidence.Check(rows,step,d,c,input);return;}
            var reqs=rows.Where(r=>Kind(r)=="choice-requested"&&r.GetProperty("root").GetString()==root).ToArray();var children=step.GetProperty("children");
            Need(reqs.Length==(a.GetProperty("Choice").ValueKind==JsonValueKind.Null?0:1)&&children.GetArrayLength()==reqs.Length,"complete native child set");
            if(a.GetProperty("Choice").ValueKind!=JsonValueKind.Null){
                var child=children[0];var choice=a.GetProperty("Choice");Need(Same(child.GetProperty("choice"),choice)&&child.GetProperty("root").GetString()==root&&child.GetProperty("context").GetString()==d.GetProperty("context").GetString()&&child.GetProperty("source").GetString()==a.GetProperty("CardId").GetString()&&child.GetProperty("leaf").GetString()=="FromHand","child action provenance");
                Need(choice.GetProperty("Effect").GetString()=="Exhaust"&&choice.GetProperty("SourcePile").GetString()=="Hand"&&choice.GetProperty("Timing").GetString()=="Action","narrow native choice capability");
                Need(choice.GetProperty("ContextId").GetString()==""&&(choice.GetProperty("SourceId").GetString()==""||choice.GetProperty("SourceId").GetString()==a.GetProperty("CardId").GetString()),"supported choice context and source");
                string id=child.GetProperty("request").GetString()!;var req=reqs.Single();Need(req.GetProperty("detail").GetProperty("id").GetString()==id,"actual request ID");
                var resolved=rows.Single(r=>Kind(r)=="choice-resolved"&&r.GetProperty("detail").GetProperty("id").GetString()==id);var returned=rows.Single(r=>Kind(r)=="choice-returned"&&r.GetProperty("detail").GetProperty("id").GetString()==id);
                Need(Array.IndexOf(rows,d)<Array.IndexOf(rows,req)&&Array.IndexOf(rows,req)<Array.IndexOf(rows,resolved)&&Array.IndexOf(rows,resolved)<Array.IndexOf(rows,returned)&&Array.IndexOf(rows,returned)<Array.IndexOf(rows,c),"native child execution order");
                var token=choice.GetProperty("Cards")[0];var candidates=child.GetProperty("candidates").EnumerateArray().ToArray();int index=child.GetProperty("indices")[0].GetInt32();Need(index>=(fixedNonFirst?1:0)&&index<candidates.Length,"actual child index");var selected=candidates[index];string selectedId=selected.GetProperty("id").GetString()!;
                Need(child.GetProperty("selected").GetArrayLength()==1&&child.GetProperty("selected")[0].GetString()==selectedId&&selected.GetProperty("model").GetString()=="CARD."+token.GetProperty("CardId").GetString()&&selected.GetProperty("upgrade").GetInt32()==token.GetProperty("UpgradeLevel").GetInt32()&&selected.GetProperty("key").GetString()==token.GetProperty("StateKey").GetString(),"planned selected child token");
                var options=req.GetProperty("detail").GetProperty("oracleOptions");Need(options.GetArrayLength()==candidates.Length&&options[index].GetProperty("representative").GetProperty("$ref").GetString()==selectedId&&resolved.GetProperty("detail").GetProperty("selected")[0].GetString()=="o"+index&&returned.GetProperty("detail").GetProperty("objects")[0].GetProperty("$ref").GetString()==selectedId,"native candidate/response/return instance");
                Need(choice.GetProperty("Cards").GetArrayLength()==1&&child.GetProperty("indices").GetArrayLength()==1,"one complete planned token");
                bool Matches(JsonElement x)=>x.GetProperty("model").GetString()=="CARD."+token.GetProperty("CardId").GetString()&&x.GetProperty("upgrade").GetInt32()==token.GetProperty("UpgradeLevel").GetInt32();
                Need(candidates.Take(index).Count(Matches)==token.GetProperty("OptionOccurrence").GetInt32(),"actual child option occurrence");
                var hand=before.GetProperty("combat").GetProperty("hand").EnumerateArray().ToArray();
                Need(hand.Any(x=>x.GetProperty("id").GetString()==selectedId)&&hand.TakeWhile(x=>x.GetProperty("id").GetString()!=selectedId).Count(Matches)==token.GetProperty("SourceOccurrence").GetInt32(),"actual child source occurrence");
                for(int j=0;j<candidates.Length;j++){
                    string candidateId=candidates[j].GetProperty("id").GetString()!;
                    var actual=hand.Single(x=>x.GetProperty("id").GetString()==candidateId);
                    Need(options[j].GetProperty("representative").GetProperty("$ref").GetString()==candidateId&&actual.GetProperty("model").GetString()==candidates[j].GetProperty("model").GetString()&&actual.GetProperty("upgrade").GetInt32()==candidates[j].GetProperty("upgrade").GetInt32()&&input.GetProperty("binding").GetProperty("hand").EnumerateArray().Single(x=>x.GetProperty("id").GetString()==candidateId).GetProperty("key").GetString()==candidates[j].GetProperty("key").GetString(),"all native candidates and captured keys/model/upgrade");
                }
                Need(after.GetProperty("combat").GetProperty("exhaust").EnumerateArray().Any(x=>x.GetProperty("id").GetString()==selectedId)&&!after.GetProperty("combat").GetProperty("hand").EnumerateArray().Any(x=>x.GetProperty("id").GetString()==selectedId),"actual selected object exhausted");
            }
    }

}
