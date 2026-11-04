using System.Text.Json;

namespace OfflineSearchHarness;

// Semantic validation in the production reader; independent of writer totals/hash-chain checks.
internal sealed class NativeTraceContract
{
    // Program.Observe events carry mandatory state; direct Emit metadata events do not.
    // New kinds must be explicitly classified before the reader accepts them.
    static readonly HashSet<string> SnapshotKinds=["solver-phase-choice","full-act-enter","full-act-boss-complete","map-transition-ready","dispatch","confirmed","new-run","event-candidates","opening-finished","map-candidates","first-battle-opening","combat-candidates","combat-history","battle-victory-before-cleanup","battle-victory-cleanup","reward-offer","rewards-completed","card-reward-acquired","next-combat-first-play","fixture-setup-declared","fixture-assertion","fixture-contract","sink-failure-trigger","death","room-entered","room-candidates","room-option-start","room-option-return","room-option-boundary","room-complete","room-leave-ready","room-exit-start","room-exit-return","room-exited","room-assertion","room-slice-complete"];
    static readonly HashSet<string> MetadataKinds=["creature-visual-guard","solver-probe-admitted","run-query-admitted","third-tip-presentation","header","profile-lock","identity","root-request","history-clearing","history-checkpoint","choice-requested","choice-resolved","choice-returned","choice-reward-completed","native-choice-reserved","task-start","task-completed","task-fault","selection-fault","footer","boss-audio-guard","branch-choice"];
    internal static JsonElement? ReadSnapshot(string kind,JsonElement detail)
    {
        bool required=NativeReflections.Kinds.Contains(kind)||NativeSlipperyBridge.Kinds.Contains(kind)||NativeRelicPickupEvidence.Kinds.Contains(kind)||NativeShopLedgerContract.Kinds.Contains(kind)||NativeSolverContract.Kinds.Contains(kind)||NativeBranchContract.Snapshots.Contains(kind)||NativeThirdShopContract.Kinds.Contains(kind)||NativeDeathContract.Kinds.Contains(kind)||NativeMajorContract.SnapshotKinds.Contains(kind)||NativeEventFlowContract.SnapshotKinds.Contains(kind)||NativeRewardClosureContract.SnapshotKinds.Contains(kind)||SnapshotKinds.Contains(kind)||LifecycleSnapshots.Contains(kind)||NativeRewardContract.SnapshotKinds.Contains(kind)||NativeMerchantContract.SnapshotKinds.Contains(kind)||NativeMerchantExpansionContract.SnapshotKinds.Contains(kind);
        required|=NativeSolverContract.JourneyKinds.Contains(kind)||NativeAcceptanceLifecycleEvidence.Kinds.Contains(kind);
        if(!required&&!MetadataKinds.Contains(kind)&&kind is not ("lifecycle-fault" or "platform-offline-guard" or "architect-presentation"))throw new NativeDataException("Unknown event kind");
        bool present=detail.ValueKind==JsonValueKind.Object&&detail.TryGetProperty("state",out _);
        if(!present){if(required)throw new NativeDataException("Missing required state snapshot: "+kind);return null;}
        var state=detail.GetProperty("state");
        if(state.ValueKind==JsonValueKind.Null)throw new NativeDataException("Null state snapshot: "+kind);
        if(state.ValueKind!=JsonValueKind.Object)throw new NativeDataException("Nonobject state snapshot: "+kind);
        return state;
    }
    static readonly HashSet<string> LifecycleSnapshots=["run-end-start","run-end-return","run-win-start","run-win-completed","act-transition-start","act-transition-completed","battle-loss-start","battle-loss-confirmed","terminal-stamp","terminal-contract","tail-event-candidates","tail-contract"];
    readonly NativeEventFlowContract eventContract=new();
    readonly NativeMajorContract majorContract=new();
    readonly NativeBranchContract branchContract=new();
    readonly NativeThirdShopContract thirdShopContract=new();
    readonly NativeRewardClosureContract closureContract=new();
    readonly NativeTerminalContract terminalContract=new();
    readonly NativeRoomContract roomContract=new();
    readonly NativeRewardContract rewardContract=new();
    readonly NativeMerchantContract merchantContract=new();
    readonly NativeMerchantExpansionContract expansionContract=new();
    internal NativeScenario Scenario=null!;
    internal string Mode=>Scenario.Observation;
    string? active,context,requested,lastConfirmedRoot,lastConfirmedContext;
    bool dispatched,checkpoint,fixtureDone;
    int roots,epoch,historyCount;
    JsonElement rootAction;
    readonly Dictionary<string,(string Root,string Context)> tasks=[];
    readonly Dictionary<string,JsonElement> choices=[];
    readonly Dictionary<string,string[]> resolutions=[];
    readonly HashSet<string> returned=[],assertions=[];
    static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition,string message){if(!condition)throw new NativeDataException(message);}
    static bool Same(JsonElement a,JsonElement b)=>SimulatorLab.Core.NativeDecisionTicket.SameActionJson(a.GetRawText(),b.GetRawText());
    static string Text(JsonElement e,string key)=>e.GetProperty(key).GetString()??throw new NativeDataException("Null "+key);
    static string[] Refs(JsonElement a)=>a.EnumerateArray().Select(x=>Text(x,"$ref")).ToArray();
    internal void Accept(JsonElement p,JsonElement? snapshot)
    {
        string kind=Text(p,"kind"),root=Text(p,"root"),ctx=Text(p,"context");var d=p.GetProperty("detail");
        if(kind=="header"){
            Scenario=NativeScenario.Read(d.GetProperty("provenance"));
            Require(Scenario==NativeScenario.Describe(Scenario.Scenario,Scenario.Replay,Scenario.Observation,Scenario.HostControl,Scenario.InputControl,Scenario.Branch),"Unregistered/inconsistent provenance");
            Require(Scenario.ScenarioKind is "natural-run" or "api-fixture","Fault control cannot certify a successful run");
            Require(Text(d,"scenario")==Scenario.Scenario&&Text(d,"mode")==Mode,"Header scenario/mode mismatch");
        }
        Require(Scenario!=null,"Missing header provenance");
        Require(!kind.StartsWith("prep-",StringComparison.Ordinal)||NativePreparation.IsScenario(Scenario.Scenario),"Preparation evidence outside declared profile");
        Require(!NativeDeathContract.Kinds.Contains(kind)||NativeMajor.IsScenario(Scenario.Scenario),"Death anchors outside declared major profile");
        Require(!kind.StartsWith("major-",StringComparison.Ordinal)||NativeMajor.IsScenario(Scenario.Scenario),"Major evidence outside declared profile");
        Require(!kind.StartsWith("ef-",StringComparison.Ordinal)||NativeEventFlow.IsScenario(Scenario.Scenario),"Event flow evidence outside declared profile");
        Require(!kind.StartsWith("merchant-",StringComparison.Ordinal)||NativeMerchantEconomy.IsScenario(Scenario.Scenario)||NativeThirdShop.IsScenario(Scenario.Scenario),"Merchant economy event outside declared profile");
        if(snapshot is {} state)CheckState(state);
        branchContract.Accept(p,snapshot);
        if(NativeMajor.IsScenario(Scenario.Scenario))majorContract.Accept(p,snapshot);
        thirdShopContract.Accept(p,snapshot);
        if(NativeEventFlow.IsScenario(Scenario.Scenario))eventContract.Accept(p,snapshot);
        if(NativeLifecycle.IsScenario(Scenario.Scenario))terminalContract.Accept(p,snapshot);
        if(NativeRooms.IsScenario(Scenario.Scenario)&&Scenario.Scenario!="merchant-natural"&&!NativeMerchantExpansion.IsScenario(Scenario.Scenario)&&!NativeEventFlow.IsScenario(Scenario.Scenario))roomContract.Accept(p,snapshot);
        if(NativeRewardProbe.IsScenario(Scenario.Scenario))rewardContract.Accept(p,snapshot);
        if(NativeRewardClosure.IsScenario(Scenario.Scenario))closureContract.Accept(p,snapshot);
        if(NativeMerchantExpansion.IsScenario(Scenario.Scenario))expansionContract.Accept(p,snapshot);
        else if(NativeMerchant.IsScenario(Scenario.Scenario)||NativeMerchantEconomy.IsScenario(Scenario.Scenario))merchantContract.Accept(p,snapshot);
        if(kind=="root-request"){
            Require(active==null&&!fixtureDone&&!checkpoint&&root=="a"+(roots+1),"Unfinished/out-of-order root request");
            active=root;requested=root;context=ctx;dispatched=false;rootAction=d.GetProperty("action").Clone();
        }else if(kind=="dispatch"){
            Require(active==root&&requested==root&&context==ctx&&!dispatched,"Dispatch without matching root request");
            Require(Same(rootAction,d.GetProperty("facts")),"Root request/dispatch action mismatch");dispatched=true;roots++;
        }else if(kind=="confirmed"){
            Require(active==root&&context==ctx&&dispatched,"Unmatched root confirmation");
            Require(tasks.Count==0&&choices.Keys.All(returned.Contains),"Confirmation before child choice/task completion");
            Require(Same(rootAction,d.GetProperty("facts").GetProperty("action")),"Confirmed action mismatch");
            lastConfirmedRoot=root;lastConfirmedContext=ctx;active=null;requested=null;
        }else if(kind=="task-start"){
            Require(active==root&&context==ctx&&dispatched,"Unparented task");
            Require(tasks.TryAdd(Text(d,"id"),(root,ctx)),"Repeated task");
        }else if(kind=="task-completed"){
            string id=Text(d,"id");Require(tasks.TryGetValue(id,out var owner)&&owner==(root,ctx)&&active==root,"Task owner/order mismatch");tasks.Remove(id);
        }else if(kind=="choice-requested"){
            Require(active==root&&dispatched&&context==ctx&&Text(d,"root")==root&&Text(d,"context")==ctx,"Choice root/context mismatch");
            string id=Text(d,"id"),parent=Text(d,"parent");
            Require(parent==root||choices.TryGetValue(parent,out var ancestor)&&Text(ancestor,"root")==root&&!returned.Contains(parent),"Choice parent mismatch");
            Require(choices.TryAdd(id,d.Clone()),"Duplicate request");
            string source=Text(d,"source"),completion=Text(d,"completion");
            Require(new[]{"CardReward","Bundle","FromChooseACardScreen","FromSimpleGrid","FromCombatPile","FromHand","FromHandForUpgrade","FromDeckForUpgrade","FromDeckGeneric"}.Contains(source),"Unknown choice source");
            Require(completion==(source=="CardReward"?"reward-acquired":"method-return"),"Choice completion contract mismatch");
            var options=d.GetProperty("options").EnumerateArray().ToArray();var mapping=d.GetProperty("oracleOptions").EnumerateArray().ToArray();
            Require(mapping.Length==options.Length&&options.Select(o=>Text(o,"handle")).Distinct().Count()==options.Length,"Missing/duplicate candidate mapping");
            for(int i=0;i<options.Length;i++){
                Require(Text(mapping[i],"handle")==Text(options[i],"handle"),"Candidate handle mapping order mismatch");
                string rep=Text(mapping[i].GetProperty("representative"),"$ref");var refs=Refs(mapping[i].GetProperty("objects"));
                Require(refs.Length>0&&refs[0]==rep,"Candidate representative/group mismatch");
                Require(source=="Bundle"?refs.Length==options[i].GetProperty("group").GetArrayLength():refs.Length==1,"Candidate object cardinality mismatch");
            }
        }else if(kind=="choice-resolved"){
            string id=Text(d,"id");Require(choices.TryGetValue(id,out var request),"Unrequested resolution");CheckOwner(request,root,ctx);
            var handles=d.GetProperty("selected").EnumerateArray().Select(x=>x.GetString()!).ToArray();
            var mapping=request.GetProperty("oracleOptions").EnumerateArray().ToDictionary(o=>Text(o,"handle"));
            Require(handles.All(mapping.ContainsKey),"Unknown choice handle");
            var expected=handles.Select(h=>Text(mapping[h].GetProperty("representative"),"$ref")).ToArray();
            Require(expected.SequenceEqual(Refs(d.GetProperty("objects"))),"Resolved candidate instance mismatch");
            Require(resolutions.TryAdd(id,handles.SelectMany(h=>Refs(mapping[h].GetProperty("objects"))).ToArray()),"Repeated resolution");
        }else if(kind is "choice-returned" or "choice-reward-completed"){
            string id=Text(d,"id");Require(choices.TryGetValue(id,out var request),"Return without request");CheckOwner(request,root,ctx);
            Require(Text(request,"completion")== (kind=="choice-returned"?"method-return":"reward-acquired"),"Wrong choice completion source");
            Require(resolutions.TryGetValue(id,out var expected)&&expected.SequenceEqual(Refs(d.GetProperty("objects")))&&returned.Add(id),"Returned candidate instance/order mismatch");
        }else if(kind=="combat-history"){
            Require(active==root&&dispatched&&context==ctx,"History active action/room context mismatch");
            var facts=d.GetProperty("facts");Require(!checkpoint&&Mode=="detailed"&&facts.GetProperty("epoch").GetInt32()==epoch&&facts.GetProperty("index").GetInt32()==historyCount+1,"Missing/duplicate/reordered history entry");historyCount++;
        }else if(kind is "history-clearing" or "history-checkpoint"){
            int native=d.GetProperty("entries").GetInt32();
            Require(!checkpoint&&d.GetProperty("epoch").GetInt32()==epoch&&native>=0&&d.GetProperty("observed").GetInt32()==native,"History epoch/native observation mismatch");
            Require(Mode=="detailed"?historyCount==native:historyCount==0,"Missing/unexpected detailed history");
            if(kind=="history-clearing"){
                Require(active==root&&dispatched&&context==ctx,"History clear active action/room context mismatch");epoch++;historyCount=0;
            }else{Require(active==null&&root==lastConfirmedRoot&&ctx==lastConfirmedContext,"Final history checkpoint confirmed owner mismatch");checkpoint=true;}
        }else if(kind is "fixture-setup-declared" or "fixture-assertion" or "fixture-contract"){
            Require(Scenario.ScenarioKind=="api-fixture"&&!checkpoint&&!fixtureDone,"Fixture evidence in wrong scenario/order");
            var facts=d.GetProperty("facts");
            if(NativeSolverBattle.IsScenario(Scenario.Scenario)||NativeLifecycle.IsScenario(Scenario.Scenario)||NativeRooms.IsScenario(Scenario.Scenario)||NativeRewardProbe.IsScenario(Scenario.Scenario)||NativeMerchant.IsScenario(Scenario.Scenario)||NativeMerchantEconomy.IsScenario(Scenario.Scenario)||(Scenario.Scenario=="solver-choice-potion"||NativeSolverOnline.IsScenario(Scenario.Scenario)))return;
            if(kind=="fixture-assertion")Require(facts.GetProperty("valid").GetBoolean()&&assertions.Add(Text(facts,"name")),"Failed/duplicate fixture assertion");
            else{
                Require(Text(facts,"name")==Scenario.Scenario,"Fixture scenario mismatch");
                if(kind=="fixture-contract"){
                    Require(active==null&&facts.GetProperty("valid").GetBoolean()&&Text(facts,"scenarioKind")=="api-fixture","Failed fixture contract");
                    Require(assertions.SetEquals(RequiredAssertions(Scenario.Scenario)),"Missing/unexpected fixture assertions");fixtureDone=true;
                }
            }
        }
    }
    void CheckOwner(JsonElement request,string root,string ctx)=>Require(active==root&&dispatched&&context==ctx&&Text(request,"root")==root&&Text(request,"context")==ctx,"Choice owner/order mismatch");
    internal static void CheckState(JsonElement state)
    {
        foreach(string field in new[]{"act","floor","hp","maxHp","gold","block"})_=state.GetProperty(field).GetInt32();
        foreach(string field in new[]{"powers","deck","relics","potions"})Require(state.GetProperty(field).ValueKind==JsonValueKind.Array,"Missing runtime state array "+field);
        foreach(string stream in new[]{"runRng","playerRng"}){
            var rng=state.GetProperty(stream);Require(rng.ValueKind==JsonValueKind.Object,"Null/nonobject runtime RNG "+stream);
            string[] expected=stream=="runRng"?["UpFront","Shuffle","UnknownMapPoint","CombatCardGeneration","CombatPotionGeneration","CombatCardSelection","CombatEnergyCosts","CombatTargets","MonsterAi","Niche","CombatOrbs","TreasureRoomRelics"]:["Rewards","Shops","Transformations"];
            Require(rng.GetProperty("rngs").EnumerateObject().Select(x=>x.Name).ToHashSet().SetEquals(expected),"Runtime RNG inventory mismatch");
        }
        Require(state.GetProperty("eventRng").ValueKind==JsonValueKind.Object,"Missing event synchronizer RNG");
    }
    internal void FinishSolverBudgetPrefix(JsonElement seal,JsonElement report,JsonElement process,bool unsupportedRoom=false,bool unsupportedActPrefix=false)
    {
        Require(NativeSolverJourney.IsScenario(Scenario.Scenario)&&active==null&&tasks.Count==0&&choices.Keys.All(returned.Contains),"Solver budget prefix safe closed action/choice/task boundary");
        Require(Text(seal,"mode")==Mode&&Text(process,"observation")==Mode&&NativeScenario.Read(report.GetProperty("provenance"))==Scenario&&NativeScenario.Read(process.GetProperty("provenance"))==Scenario&&Text(process,"scenarioKind")==Scenario.ScenarioKind&&Text(process,"executionKind")==Scenario.ExecutionKind,"Solver budget prefix provenance/mode");
        Require(Same(report.GetProperty("trace"),seal),"Solver budget prefix report/seal");
        // Existing finite major contract already certifies a safe, unfinished
        // combat prefix. This does not assert completion or a missing final history checkpoint.
        majorContract.Finish(report,branchHorizon:!unsupportedRoom&&!unsupportedActPrefix,unsupportedRoomPrefix:unsupportedRoom,unsupportedActPrefix:unsupportedActPrefix);
        thirdShopContract.Finish();
    }
    internal void Finish(JsonElement seal,JsonElement report,JsonElement process)
    {
        Require(checkpoint&&active==null&&tasks.Count==0&&choices.Keys.All(returned.Contains),"Unclosed roots/choices/tasks/history");
        Require(Scenario.ScenarioKind!="api-fixture"||NativeSolverBattle.IsScenario(Scenario.Scenario)||NativeMajor.IsScenario(Scenario.Scenario)||NativeLifecycle.IsScenario(Scenario.Scenario)||NativeRooms.IsScenario(Scenario.Scenario)||NativeRewardProbe.IsScenario(Scenario.Scenario)||NativeMerchant.IsScenario(Scenario.Scenario)||NativeMerchantEconomy.IsScenario(Scenario.Scenario)||(Scenario.Scenario=="solver-choice-potion"||NativeSolverOnline.IsScenario(Scenario.Scenario))||fixtureDone,"Missing fixture contract");
        Require(Text(seal,"mode")==Mode&&Text(process,"observation")==Mode,"Observation mode mismatch");
        Require(NativeScenario.Read(report.GetProperty("provenance"))==Scenario&&NativeScenario.Read(process.GetProperty("provenance"))==Scenario,"Scenario provenance mismatch");
        Require(Text(process,"scenarioKind")==Scenario.ScenarioKind&&Text(process,"executionKind")==Scenario.ExecutionKind,"Process origin/execution mismatch");
        Require(Same(report.GetProperty("trace"),seal),"Result/seal mismatch");
        branchContract.Finish(report);
        if(NativeMajor.IsScenario(Scenario.Scenario))majorContract.Finish(report,branchContract.Horizon);
        thirdShopContract.Finish();
        if(NativeEventFlow.IsScenario(Scenario.Scenario))eventContract.Finish();
        if(NativeLifecycle.IsScenario(Scenario.Scenario))terminalContract.Finish(Scenario,report);
        if(NativeRooms.IsScenario(Scenario.Scenario)&&Scenario.Scenario!="merchant-natural"&&!NativeMerchantExpansion.IsScenario(Scenario.Scenario)&&!NativeEventFlow.IsScenario(Scenario.Scenario))roomContract.Finish(report);
        if(NativeRewardProbe.IsScenario(Scenario.Scenario))rewardContract.Finish();
        if(NativeRewardClosure.IsScenario(Scenario.Scenario))closureContract.Finish();
        if(NativeMerchantExpansion.IsScenario(Scenario.Scenario))expansionContract.Finish();
        else if(NativeMerchant.IsScenario(Scenario.Scenario)||NativeMerchantEconomy.IsScenario(Scenario.Scenario))merchantContract.Finish();
    }
    static string[] RequiredAssertions(string name)
    {
        const string deck="full permanent deck content/upgrade/references unchanged",cardinality="native cardinality",specified="specified duplicate selected by reference";
        if(name.StartsWith("deck-"))return ["nonzero permanent instance selected","permanent reference continuity","only specified permanent card upgraded"];
        if(name=="armaments")return ["Armaments independent 5 block / 1 energy","Armaments exact duplicate upgraded",deck];
        if(name.StartsWith("headbutt"))return name=="headbutt-empty"?["Headbutt independent nine actual hp loss",deck]:["Headbutt independent nine actual hp loss","specified duplicate on draw top",deck];
        if(name=="bundle-shared")return ["Bundle native reserve exactly once","shared-first group identity retained",deck];
        if(name=="bundle")return ["Bundle native reserve exactly once",cardinality,specified,deck];
        if(name=="multi")return [cardinality,specified,"explicit multi order preserved",deck];
        if(name=="sequential")return [cardinality,specified,"second request consumed in order",deck];
        if(name is "empty" or "single" or "manual-single")return [cardinality,deck];
        return [cardinality,specified,deck];
    }
}
