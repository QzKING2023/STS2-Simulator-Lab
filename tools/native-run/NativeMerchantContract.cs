using System.Text.Json;
using System.Text.Json.Nodes;
using SimulatorLab.Core;
namespace OfflineSearchHarness;

// Finite 4D1 contract; source effects remain native. Full snapshots bind all static edges.
internal sealed class NativeMerchantContract
{
    internal static readonly HashSet<string> SnapshotKinds=["n3d-open","n3d-return","n3d-boundary","n3d-contract","n3d-removal-marked","n3d-removal-hook","merchant-price-start","merchant-price","merchant-exit-start","merchant-exit-return","merchant-stats-start","merchant-stats-return"];
    bool economy,natural;
    int tailPlays;
    readonly NativeMerchantEconomyContract business=new();
    string root="",context="",kind="",id="",lastRoot="",lastContext="",scenario="";
    JsonElement dispatch,returned,confirmed,decision;
    bool opened,returnedOnce,pendingBoundary,setup,exited,done;
    readonly HashSet<string> ids=[];
    int ops,buys,removes;
    int removalPhase;
    JsonElement removalMarked;
    int choicePhase;
    string choiceId="";
    JsonElement choiceRequest;
    string[] chosen=[];
    static void R(bool value,string message){if(!value)throw new NativeDataException("Merchant "+message);}
    static string T(JsonElement e,string k)=>e.GetProperty(k).GetString()??throw new NativeDataException("Null merchant field");
    static string Ref(JsonElement e)=>e.ValueKind==JsonValueKind.Null?"":T(e,"$ref");
    static bool Eq(JsonElement a,JsonElement b)=>a.ValueKind!=JsonValueKind.Undefined&&b.ValueKind!=JsonValueKind.Undefined&&NativeDecisionTicket.SameActionJson(a.GetRawText(),b.GetRawText());
    static bool IsM(string s)=>s is "MerchantSetup" or "MerchantAttach" or "MerchantBuy" or "MerchantRemove" or "MerchantExit" or "MerchantTravel";
    static JsonElement[] List(JsonElement s,string field)=>s.GetProperty(field).EnumerateArray().ToArray();
    static void Unchanged(JsonElement a,JsonElement b,params string[] except)
    {
        var skip=except.ToHashSet();R(a.EnumerateObject().Select(x=>x.Name).ToHashSet().SetEquals(b.EnumerateObject().Select(x=>x.Name)),"snapshot field inventory mismatch");
        foreach(var p in a.EnumerateObject())if(!skip.Contains(p.Name))R(Eq(p.Value,b.GetProperty(p.Name)),"unexpected business mutation "+p.Name);
    }
    static JsonElement Entry(JsonElement s,string reference)=>List(s.GetProperty("merchant"),"entries").Single(x=>Ref(x.GetProperty("reference"))==reference);
    internal void Accept(JsonElement p,JsonElement? snapshot)
    {
        string ev=T(p,"kind");var d=p.GetProperty("detail");var f=d.ValueKind==JsonValueKind.Object&&d.TryGetProperty("facts",out var x)?x:default;
        if(ev=="header"){scenario=NativeMerchantEconomy.BaseScenario(T(d,"scenario"));economy=NativeMerchantEconomy.IsScenario(T(d,"scenario"));natural=scenario=="merchant-natural";}
        if(economy)business.Accept(p,snapshot);
        if(ev=="root-request"){R(!pendingBoundary&&!done,"unclosed boundary or root after contract");}
        if(ev=="dispatch"){
            R(root=="","overlapping root");root=T(p,"root");context=T(p,"context");kind=T(f,"kind");dispatch=snapshot!.Value.Clone();decision=f.GetProperty("decision").Clone();opened=returnedOnce=false;removalPhase=choicePhase=0;choiceId="";chosen=[];
            if(setup){R((!exited||natural&&kind=="PlayCard"&&tailPlays==0)&&Eq(dispatch,confirmed),"dispatch not bound to previous confirmation");if(exited)tailPlays++;}
            if(IsM(kind))R(kind is "MerchantSetup" or "MerchantAttach"?!setup:setup&&!exited,"setup/operation order");
        }
        if(ev=="n3d-open"){
            Owner(p);R(IsM(kind)&&!opened&&T(f,"kind")==kind&&Eq(f.GetProperty("decision"),decision)&&Eq(snapshot!.Value,dispatch),"open/dispatch mismatch");
            id=T(f,"id");R(ids.Add(id),"duplicate operation id");opened=true;ops++;
        }
        if(ev=="n3d-return"){
            Owner(p);R(opened&&!returnedOnce&&T(f,"id")==id&&T(f,"kind")==kind,"return missing/duplicate/owner mismatch");
            R(choicePhase==(NeedsRemovalChoice()?3:0),"missing/extra required operation choice");
            returned=snapshot!.Value.Clone();ValidateResult(f.GetProperty("result"));returnedOnce=true;
        }
        if(ev is "choice-requested" or "choice-resolved" or "choice-returned" or "choice-reward-completed" && IsM(kind))ValidateChoice(p,d,ev);
        if(ev is "n3d-removal-marked" or "n3d-removal-hook"){
            Owner(p);R(kind=="MerchantRemove"&&opened&&!returnedOnce&&choicePhase==3&&chosen.Length==1&&Ref(f.GetProperty("entry"))==Ref(decision.GetProperty("entry")),"removal phase owner/choice order mismatch");
            var state=snapshot!.Value;var entry=Entry(state,Ref(decision.GetProperty("entry")));R(entry.GetProperty("used").GetBoolean()&&!entry.GetProperty("stock").GetBoolean(),"removal phase not marked used");
            if(ev=="n3d-removal-marked"){R(removalPhase==0&&T(f,"source")=="OneOff-task-return/native-SetUsed","duplicate/out-of-order removal mark");removalMarked=state.Clone();removalPhase=1;}
            else{R(removalPhase==1&&Eq(removalMarked,state)&&f.GetProperty("used").GetBoolean()&&T(f,"source")=="Hook.AfterItemPurchased-prefix","removal hook order/static state mismatch");removalPhase=2;}
        }
        if(ev=="confirmed"){
            Owner(p);if(IsM(kind)){R(opened&&returnedOnce&&Eq(snapshot!.Value,returned),"confirmation before native return/state mismatch");pendingBoundary=true;}
            confirmed=snapshot!.Value.Clone();lastRoot=root;lastContext=context;root="";
        }
        if(ev=="n3d-boundary"){
            Static(p,snapshot!.Value);R(pendingBoundary&&T(f,"id")==id&&T(f,"kind")==kind,"missing/duplicate/wrong operation boundary");pendingBoundary=false;
        }
        if(ev=="n3d-contract"){
            Static(p,snapshot!.Value);R(!done&&!pendingBoundary&&setup&&exited&&(!natural||tailPlays==1)&&f.GetProperty("valid").GetBoolean()&&f.GetProperty("fixture").GetBoolean()==!natural&&NativeMerchantEconomy.BaseScenario(T(f,"scenario"))==scenario&&f.GetProperty("operations").GetInt32()==ops,"final contract mismatch");done=true;
        }
        if(snapshot is {} s&&s.GetProperty("merchant").ValueKind==JsonValueKind.Object){
            var m=s.GetProperty("merchant");var entries=List(m,"entries");R(entries.Length==14&&entries.Select(e=>Ref(e.GetProperty("reference"))).Distinct().Count()==14,"normal inventory identity/cardinality");
            R(entries.Count(e=>T(e,"kind")=="MerchantCardEntry")==7&&entries.Count(e=>T(e,"kind")=="MerchantPotionEntry")==3&&entries.Count(e=>T(e,"kind")=="MerchantRelicEntry")==3,"normal inventory kinds");
            for(int i=0;i<entries.Length;i++){
                var e=entries[i];R(e.GetProperty("index").GetInt32()==i&&e.GetProperty("cost").GetInt32()>=0,"entry order/cost");
                bool stocked=e.GetProperty("stock").GetBoolean();string entryKind=T(e,"kind");
                if(entryKind=="MerchantCardRemovalEntry")R(stocked!=e.GetProperty("used").GetBoolean()&&Ref(e.GetProperty("content"))==""&&e.GetProperty("model").ValueKind==JsonValueKind.Null,"removal stock/used invariant");
                else R(stocked?(Ref(e.GetProperty("content"))!=""&&e.GetProperty("model").ValueKind==JsonValueKind.String):(Ref(e.GetProperty("content"))==""&&e.GetProperty("model").ValueKind==JsonValueKind.Null&&e.GetProperty("upgrade").ValueKind==JsonValueKind.Null),"entry content/stock invariant");
            }
        }
    }
    void Owner(JsonElement p)=>R(root!=""&&T(p,"root")==root&&T(p,"context")==context,"operation root/context mismatch");
    void Static(JsonElement p,JsonElement s)=>R(root==""&&T(p,"root")==lastRoot&&T(p,"context")==lastContext&&Eq(s,confirmed),"static confirmed boundary mismatch");
    bool NeedsRemovalChoice()=>kind=="MerchantRemove"&&!Entry(dispatch,Ref(decision.GetProperty("entry"))).GetProperty("used").GetBoolean()&&dispatch.GetProperty("gold").GetInt32()>=Entry(dispatch,Ref(decision.GetProperty("entry"))).GetProperty("cost").GetInt32();
    void ValidateChoice(JsonElement p,JsonElement d,string ev)
    {
        Owner(p);R(opened&&!returnedOnce&&NeedsRemovalChoice()&&removalPhase==0,"unexpected/out-of-order merchant child choice");
        if(ev=="choice-requested"){
            R(choicePhase==0&&T(d,"root")==root&&T(d,"context")==context&&T(d,"parent")==root&&T(d,"source")=="FromDeckGeneric"&&T(d,"completion")=="method-return"&&T(d,"transport")=="native-selector"&&!d.GetProperty("implicitChoice").GetBoolean()&&d.GetProperty("min").GetInt32()==0&&d.GetProperty("max").GetInt32()==1,"removal choice request contract");
            choiceId=T(d,"id");choiceRequest=d.Clone();choicePhase=1;
            var mapping=List(d,"oracleOptions");var options=List(d,"options");var deck=List(dispatch,"deck");
            R(mapping.Length==options.Length&&mapping.Length>0&&mapping.Select(x=>Ref(x.GetProperty("representative"))).Distinct().Count()==mapping.Length,"removal candidate identity/cardinality");
            for(int i=0;i<mapping.Length;i++){
                var candidate=deck.Single(c=>T(c,"id")==Ref(mapping[i].GetProperty("representative")));
                R(T(options[i],"handle")==T(mapping[i],"handle")&&T(options[i],"model")==T(candidate,"model")&&options[i].GetProperty("upgrade").GetInt32()==candidate.GetProperty("upgrade").GetInt32()&&mapping[i].GetProperty("objects").GetArrayLength()==1&&Ref(mapping[i].GetProperty("objects")[0])==T(candidate,"id"),"removal candidate permanent instance/content mismatch");
            }
        }else{
            R(T(d,"id")==choiceId,"choice belongs to different operation");
            if(ev=="choice-resolved"){
                R(choicePhase==1&&!d.GetProperty("implicitChoice").GetBoolean(),"duplicate/out-of-order removal resolution");
                var handles=List(d,"selected").Select(x=>x.GetString()).ToArray();R(handles.Length<=1,"removal cardinality");
                chosen=handles.Select(h=>Ref(List(choiceRequest,"oracleOptions").Single(x=>T(x,"handle")==h).GetProperty("representative"))).ToArray();
                R(chosen.SequenceEqual(List(d,"objects").Select(Ref)),"removal resolved instance mismatch");choicePhase=2;
            }else{
                R(ev=="choice-returned"&&choicePhase==2&&chosen.SequenceEqual(List(d,"objects").Select(Ref)),"removal return/completion mismatch");choicePhase=3;
            }
        }
    }
    void ValidateResult(JsonElement result)
    {
        if(kind is "MerchantSetup" or "MerchantAttach"){
            R(T(returned,"room")=="MerchantRoom"&&Ref(result.GetProperty("nativeInventory"))==Ref(returned.GetProperty("merchant").GetProperty("reference"))&&result.GetProperty("initialRemovalCost").GetInt32()==75,"native setup result");
            R(List(returned.GetProperty("merchant"),"entries").Single(e=>T(e,"kind")=="MerchantCardRemovalEntry").GetProperty("cost").GetInt32()==75,"independent first removal cost");setup=true;return;
        }
        if(kind!="MerchantTravel")R(Ref(decision.GetProperty("inventory"))==Ref(dispatch.GetProperty("merchant").GetProperty("reference")),"decision inventory mismatch");
        if(kind is "MerchantExit" or "MerchantTravel"){
            R(result.GetProperty("closed").GetBoolean()&&returned.GetProperty("merchant").GetProperty("closed").GetBoolean(),"exit incomplete");
            if(kind=="MerchantExit"){Unchanged(dispatch,returned,economy?["merchant","mapHistory"]:["merchant"]);Unchanged(dispatch.GetProperty("merchant"),returned.GetProperty("merchant"),"closed");}
            else R(result.EnumerateObject().Select(p=>p.Name).ToHashSet().SetEquals(["closed","from","to"])&&T(result,"from")==T(decision,"from")&&T(result,"to")==T(decision,"to")&&natural&&T(returned,"node")==T(decision,"to")&&T(dispatch,"node")==T(decision,"from")&&T(returned,"room")=="CombatRoom"&&returned.GetProperty("rooms").GetProperty("stack").GetInt32()==1,"natural merchant travel result");
            exited=true;return;
        }
        var before=Entry(dispatch,Ref(decision.GetProperty("entry")));var after=Entry(returned,Ref(decision.GetProperty("entry")));
        bool success=result.GetProperty("success").GetBoolean();int cost=before.GetProperty("cost").GetInt32(),spent=dispatch.GetProperty("gold").GetInt32()-returned.GetProperty("gold").GetInt32();
        R(result.GetProperty("cost").GetInt32()==cost&&result.GetProperty("spent").GetInt32()==spent&&spent==(success?cost:0),"purchase cost/spent mismatch");
        if(kind=="MerchantBuy"){
            buys++;bool expected=scenario is not ("merchant-full" or "merchant-poor")&&!(scenario=="merchant-outstock"&&buys==2);R(success==expected,"scenario native buy outcome mismatch");
            R(Ref(result.GetProperty("offered"))==Ref(before.GetProperty("content"))&&result.GetProperty("completions").GetInt32()==(success?1:0),"purchase callback/offered mismatch");
            string? failure=result.GetProperty("failure").GetString();R(success?failure==null:failure==(scenario=="merchant-full"?"FailureSpace":scenario=="merchant-poor"?"FailureGold":"FailureOutOfStock"),"native failure status mismatch");
        }else{
            removes++;R(success==(scenario!="merchant-cancel"&&removes==1&&!natural),"scenario native removal outcome mismatch");R(removalPhase==(success?2:0)&&result.GetProperty("marks").GetInt32()==(success?1:0)&&result.GetProperty("hooks").GetInt32()==(success?1:0),"required removal mark/hook lifecycle");
            R(success?chosen.Length==1:chosen.Length==0,"choice/result success or cancellation mismatch");
            if(NeedsRemovalChoice())R(decision.GetProperty("cancel").GetBoolean()==(chosen.Length==0),"declared cancellation/result mismatch");
            // This finite no-purchase-effect profile has no business change between
            // native OneOff completion/SetUsed, Hook entry and purchase return.
            // Future real Hook effects require an explicit business interval.
            if(success)R(Eq(removalMarked,returned),"removal completed effects/Hook/return full state mismatch");
        }
        if(!success){R(Eq(dispatch,returned),"failed/cancelled purchase changed full state");if(kind=="MerchantBuy")R(Ref(result.GetProperty("acquired"))==""&&result.GetProperty("newCards").GetArrayLength()==0&&result.GetProperty("newPotions").GetArrayLength()==0,"failed buy acquired object");else R(result.GetProperty("removed").GetArrayLength()==0&&T(result,"completion")=="none","failed removal result");return;}
        R(before.GetProperty("stock").GetBoolean()&&!after.GetProperty("stock").GetBoolean(),"stock native completion mismatch");
        if(kind=="MerchantRemove"){
            Unchanged(before,after,"stock","used");
            R(T(before,"kind")=="MerchantCardRemovalEntry"&&!before.GetProperty("used").GetBoolean()&&after.GetProperty("used").GetBoolean()&&T(result,"completion")=="native-SetUsed-at-OneOff-boundary","removal native completion");
            var old=List(dispatch,"deck");var now=List(returned,"deck");var missing=old.Where(c=>!now.Any(n=>T(n,"id")==T(c,"id"))).ToArray();
            R(missing.Length==1&&now.Length==old.Length-1&&old.Where(c=>T(c,"id")!=T(missing[0],"id")).Zip(now).All(z=>Eq(z.First,z.Second))&&result.GetProperty("removed").GetArrayLength()==1&&Ref(result.GetProperty("removed")[0])==T(missing[0],"id")&&chosen.SequenceEqual(List(result,"removed").Select(Ref)),"exact selected/removed instance/content mismatch");
            R(!decision.GetProperty("cancel").GetBoolean()&&T(missing[0],"id")==T(old[1],"id"),"nonzero specified removal mismatch");
            R(returned.GetProperty("merchant").GetProperty("removals").GetInt32()==dispatch.GetProperty("merchant").GetProperty("removals").GetInt32()+1,"native removal count mismatch");
            Unchanged(dispatch,returned,economy?["gold","deck","merchant","mapHistory"]:["gold","deck","merchant"]);
        }else{
            Unchanged(before,after,economy?["stock","content","model","upgrade","nativeCard"]:["stock","content","model","upgrade"]);
            string content=Ref(before.GetProperty("content"));R(content!=""&&Ref(result.GetProperty("offered"))==content&&Ref(result.GetProperty("acquired"))==content,"actual purchased identity mismatch");
            string field=T(before,"kind")=="MerchantCardEntry"?"deck":T(before,"kind")=="MerchantPotionEntry"?"potions":throw new NativeDataException("Merchant entry outside profile");
            var old=List(dispatch,field);var now=List(returned,field);R(now.Length==old.Length+1&&old.All(c=>now.Any(n=>Eq(c,n)))&&!old.Any(c=>T(c,"id")==content),"purchase inventory content continuity");
            var acquired=now.Single(c=>T(c,"id")==content);R(T(acquired,"model")==T(before,"model"),"purchase model mismatch");if(field=="deck")R(acquired.GetProperty("upgrade").GetInt32()==before.GetProperty("upgrade").GetInt32(),"purchase upgrade mismatch");
            string newField=field=="deck"?"newCards":"newPotions",emptyField=field=="deck"?"newPotions":"newCards";R(result.GetProperty(newField).GetArrayLength()==1&&Ref(result.GetProperty(newField)[0])==content&&result.GetProperty(emptyField).GetArrayLength()==0,"new acquisition result mismatch");
            Unchanged(dispatch,returned,economy?["gold",field,"merchant","mapHistory"]:["gold",field,"merchant"]);
        }
        var a=dispatch.GetProperty("merchant");var b=returned.GetProperty("merchant");Unchanged(a,b,kind=="MerchantRemove"?["entries","removals"]:["entries"]);
        var aa=List(a,"entries");var bb=List(b,"entries");for(int i=0;i<aa.Length;i++)if(Ref(aa[i].GetProperty("reference"))!=Ref(decision.GetProperty("entry")))R(Eq(aa[i],bb[i]),"unrelated entry mutation");
    }
    internal void Finish(){R(done&&root==""&&!pendingBoundary&&setup&&exited,"incomplete required merchant lifecycle");bool removal=scenario is "merchant-remove" or "merchant-cancel" or "merchant-repeat";R(buys==(removal?0:scenario=="merchant-outstock"?2:1)&&(natural?removes<=1:removes==(removal?scenario=="merchant-repeat"?2:1:0)),"missing/extra scenario transaction");if(economy)business.Finish();}
}
