using System.Text.Json;
namespace OfflineSearchHarness;

// Finite original Hand/Discard transaction: source tokens, original selector,
// returned instances and actual destination. No card-name special cases.
internal static class NativeHandDiscardEvidence
{
    static void R(bool value,string why){if(!value)throw new NativeDataException("Hand discard: "+why);}
    static string S(JsonElement e,string n)=>e.GetProperty(n).GetString()!;
    static bool Eq(JsonElement a,JsonElement b)=>NativeBoundaryContract.Equal(a,b);
    internal static void Check(JsonElement[] rows,JsonElement step,JsonElement dispatch,JsonElement confirmed,JsonElement query)
    {
        var action=step.GetProperty("action");var plan=action.GetProperty("Choice");string root=S(dispatch,"root"),context=S(dispatch,"context");
        string effect=S(plan,"Effect");
        R(S(action,"Kind")=="PlayCard"&&effect is "Discard" or "Duplicate" or "ApplyRetain" or "MoveToDrawTop"&&S(plan,"SourcePile")=="Hand"&&S(plan,"Timing")=="Action"&&S(plan,"ContextId")==""&&(S(plan,"SourceId")==""||S(plan,"SourceId")==S(action,"CardId")),"registered action/source/effect");
        var requests=rows.Where(r=>S(r,"kind")=="choice-requested"&&S(r,"root")==root).ToArray();var children=step.GetProperty("children");
        R(requests.Length==1&&children.GetArrayLength()==1,"complete unique native choice");
        var child=children[0];var request=requests[0];string id=S(child,"request");
        R(S(child,"root")==root&&S(child,"context")==context&&S(child,"leaf")=="FromHand"&&S(child,"source")==S(action,"CardId")&&Eq(child.GetProperty("choice"),plan)&&S(request.GetProperty("detail"),"id")==id,"request ownership and plan");
        var resolved=rows.Single(r=>S(r,"kind")=="choice-resolved"&&S(r.GetProperty("detail"),"id")==id);
        var returned=rows.Single(r=>S(r,"kind")=="choice-returned"&&S(r.GetProperty("detail"),"id")==id);
        foreach(var r in new[]{request,resolved,returned,confirmed})R(S(r,"root")==root&&S(r,"context")==context,"choice action/context");
        int Pos(JsonElement r)=>Array.IndexOf(rows,r);
        R(Pos(dispatch)<Pos(request)&&Pos(request)<Pos(resolved)&&Pos(resolved)<Pos(returned)&&Pos(returned)<Pos(confirmed),"native task order");
        var candidates=child.GetProperty("candidates").EnumerateArray().ToArray();var options=request.GetProperty("detail").GetProperty("oracleOptions");
        bool top=effect=="MoveToDrawTop";
        var sourceState=top?child.GetProperty("mutation").GetProperty("choiceState"):query.GetProperty("nativeState");
        if(top)R(NativeEvidence.Hash(JsonSerializer.Serialize(sourceState,Program.Json))==S(resolved.GetProperty("detail"),"stamp"),"choice-time complete state bound to actual response stamp");
        var hand=sourceState.GetProperty("combat").GetProperty("hand").EnumerateArray().ToArray();
        var keys=top?child.GetProperty("mutation").GetProperty("before").EnumerateArray().Where(x=>S(x,"pile")=="Hand").ToArray():query.GetProperty("binding").GetProperty("hand").EnumerateArray().ToArray();
        R(options.GetArrayLength()==candidates.Length&&candidates.Select(c=>S(c,"id")).Distinct().Count()==candidates.Length,"complete unique candidate set");
        for(int i=0;i<candidates.Length;i++){
            var c=candidates[i];string cid=S(c,"id");var native=hand.Single(h=>S(h,"id")==cid);var key=keys.Single(h=>S(h,"id")==cid);
            R(S(options[i].GetProperty("representative"),"$ref")==cid&&S(native,"model")==S(c,"model")&&native.GetProperty("upgrade").GetInt32()==c.GetProperty("upgrade").GetInt32()&&S(key,"key")==S(c,"key"),"actual hand/candidate/key");
        }
        var tokens=plan.GetProperty("Cards").EnumerateArray().ToArray();var indices=child.GetProperty("indices").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
        var selected=child.GetProperty("selected");var answers=resolved.GetProperty("detail").GetProperty("selected");var objects=returned.GetProperty("detail").GetProperty("objects");
        R(indices.Length==tokens.Length&&selected.GetArrayLength()==tokens.Length&&answers.GetArrayLength()==tokens.Length&&objects.GetArrayLength()==tokens.Length&&indices.Distinct().Count()==indices.Length,"exact consumed selection cardinality");
        var after=step.GetProperty("nativeAfter").GetProperty("combat");
        for(int i=0;i<tokens.Length;i++){
            int ix=indices[i];R(ix>=0&&ix<candidates.Length,"legal index");var c=candidates[ix];var token=tokens[i];string cid=S(c,"id");
            bool Matches(JsonElement x)=>S(x,"model")=="CARD."+S(token,"CardId")&&x.GetProperty("upgrade").GetInt32()==token.GetProperty("UpgradeLevel").GetInt32();
            R(Matches(c)&&S(c,"key")==S(token,"StateKey")&&selected[i].GetString()==cid&&answers[i].GetString()=="o"+ix&&S(objects[i],"$ref")==cid,"actual chosen instance/token/return");
            R(candidates.Take(ix).Count(Matches)==token.GetProperty("OptionOccurrence").GetInt32()&&hand.TakeWhile(h=>S(h,"id")!=cid).Count(Matches)==token.GetProperty("SourceOccurrence").GetInt32(),"stable duplicate occurrence");
            if(effect=="Discard")R(!after.GetProperty("hand").EnumerateArray().Any(h=>S(h,"id")==cid)&&after.GetProperty("discard").EnumerateArray().Any(h=>S(h,"id")==cid),"original discard destination; later auto-play/transform requires separate coverage");
            else{
                var mutation=child.GetProperty("mutation");R(mutation.GetProperty("complete").GetBoolean(),"native mutation task complete");
                var mb=mutation.GetProperty("before").EnumerateArray().ToArray();var ma=mutation.GetProperty("after").EnumerateArray().ToArray();
                R(mb.Select(x=>S(x,"id")).Distinct().Count()==mb.Length&&ma.Select(x=>S(x,"id")).Distinct().Count()==ma.Length,"unique mutation instances");
                var picked=mb.Single(x=>S(x,"id")==cid);R(S(picked,"pile")=="Hand"&&Matches(picked)&&S(picked,"key")==S(token,"StateKey"),"mutation captured actual selected card");
                foreach(var x in ma){var actual=after.GetProperty(S(x,"pile").ToLowerInvariant()).EnumerateArray().Single(z=>S(z,"id")==S(x,"id"));R(S(x,"model")==S(actual,"model")&&x.GetProperty("upgrade").GetInt32()==actual.GetProperty("upgrade").GetInt32(),"mutation actual final instance/model/upgrade");}
                var remaining=ma.Single(x=>S(x,"id")==cid);R(S(remaining,"pile")== (top?"Draw":"Hand")&&Matches(remaining),"original selected card preserved at destination");
                if(top){
                    R(tokens.Length==1&&S(after.GetProperty("draw")[0],"id")==cid&&!after.GetProperty("hand").EnumerateArray().Any(x=>S(x,"id")==cid),"single original card at draw top, removed from hand");
                    R(mb.Select(x=>S(x,"id")).Order().SequenceEqual(ma.Select(x=>S(x,"id")).Order()),"topdeck creates/removes no combat instance");
                }
                else
                if(effect=="ApplyRetain")R(remaining.GetProperty("retain").GetBoolean()&&S(remaining,"key").Contains("Retain",StringComparison.Ordinal),"original retain keyword actually present");
                else{
                    var added=ma.Where(x=>!mb.Any(b=>S(b,"id")==S(x,"id"))).ToArray();
                    R(added.Length>0&&added.All(x=>Matches(x)&&x.GetProperty("clone").GetBoolean()&&S(x,"id")!=cid),"actual new cloned instances; no creation requires separate support");
                }
                R(Eq(query.GetProperty("nativeState").GetProperty("deck"),step.GetProperty("nativeAfter").GetProperty("deck")),"combat mutation leaves full permanent deck unchanged");
            }
        }
    }
}
