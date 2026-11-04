using System.Text.Json;
using System.Text.Json.Nodes;
namespace OfflineSearchHarness;

// Finite 4C contract. Native execution owns effects; this reader owns causality/completeness.
internal sealed class NativeRewardContract
{
    internal static readonly HashSet<string> SnapshotKinds=["n3c-open","n3c-answer","n3c-start","n3c-return","n3c-boundary","n3c-set","n3c-set-completed","n3c-assertion","n3c-contract","n3c-card-continuity"];
    sealed class Op(string id,string parent,string kind,JsonElement open,JsonElement options,JsonElement subject)
    {
        internal readonly string Id=id,Parent=parent,Kind=kind;
        internal readonly JsonElement Open=open.Clone(),Options=options.Clone(),Subject=subject.Clone();
        internal bool Answered,Started,Returned,SetDeclared,SetCompleted,Skipped;
        internal JsonElement Selected,ReturnedState,LastChild,SelectedOption;
        internal JsonElement SetOptions;
        internal JsonElement CompletedState,Result;
        internal readonly List<Op> Children=[];
        internal readonly HashSet<string> Taken=[];
        internal bool DisallowSkip;
        internal int NativeSet=-1;
    }
    readonly Dictionary<string,Op> operations=[];
    readonly Dictionary<string,(string Type,string? Model)> identities=[];
    readonly HashSet<int> nativeSets=[];
    readonly List<Op> stack=[];
    string scenario="",root="",context="",kind="",lastRoot="",lastContext="";
    JsonElement dispatch,confirmed,fixtureBefore,lastRootReturn,decision;
    bool rootReturned,boundaryRequired,contract,asserted;
    int roots,sets,nestedSets,potionUses,treasureObtains;
    int capacityFailures,discards;
    int fixtureSets,fixtureNested,fixtureUses,fixtureCapacityFailures,fixtureDiscards;
    bool closure,defaultRelics;
    sealed class ChestScope { internal bool normalChest,extraChest,chestOpen,chestPicked,chestExited,exitReturned,treasureSkipped; internal int Obtains; }
    readonly Dictionary<string,ChestScope> chests=[];
    ChestScope chest=new();
    bool normalChest { get=>chest.normalChest; set=>chest.normalChest=value; }
    bool extraChest { get=>chest.extraChest; set=>chest.extraChest=value; }
    bool chestOpen { get=>chest.chestOpen; set=>chest.chestOpen=value; }
    bool chestPicked { get=>chest.chestPicked; set=>chest.chestPicked=value; }
    bool chestExited { get=>chest.chestExited; set=>chest.chestExited=value; }
    bool exitReturned { get=>chest.exitReturned; set=>chest.exitReturned=value; }
    bool treasureSkipped { get=>chest.treasureSkipped; set=>chest.treasureSkipped=value; }

    readonly HashSet<string> acquiredPotions=[];
    readonly HashSet<string> acquiredCards=[];
    bool cardContinuity;
    // The event override owns this task and its direct reward set. IsFinished is
    // set before OfferCustom is awaited, so it is not a task completion signal.
    string eventResume="";
    bool eventResumeCompleted;
    JsonElement eventResumeInput,eventResumeAnchor;
    static void R([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string message){if(!ok)throw new NativeDataException(message);}
    static string T(JsonElement e,string p)=>e.GetProperty(p).GetString()??throw new NativeDataException("Null reward field "+p);
    static string Ref(JsonElement e)=>e.ValueKind==JsonValueKind.Null?"":T(e,"$ref");
    static bool Eq(JsonElement a,JsonElement b)=>a.ValueKind!=JsonValueKind.Undefined&&b.ValueKind!=JsonValueKind.Undefined&&SimulatorLab.Core.NativeDecisionTicket.SameActionJson(a.GetRawText(),b.GetRawText());
    JsonElement DecisionReference(string name){
        var value=decision.GetProperty(name);
        if(!NativeSolverJourney.IsScenario(scenario)||value.ValueKind==JsonValueKind.Null)return value;
        R(value.ValueKind==JsonValueKind.String&&value.GetString() is {Length:>0} id&&identities.ContainsKey(id),"Solver decision registered string identity");
        return JsonSerializer.SerializeToElement(new Dictionary<string,string>{{"$ref",value.GetString()!}});
    }
    static bool CKind(string value)=>value is "RewardOffer" or "RewardSetup" or "UsePotion" or "TreasureOpen" or "TreasurePick" or "TreasureExit" or "NativeTravel" or "EventOption" or "RoomOption" or "EventSetup" or "EventRewards" or "EventProceed";
    bool AllowedChild(Op parent,string child)=>parent.Kind switch{
        "RewardOffer" or "EventOption" or "RoomOption" or "EventProceed" or "EventRewards" or "treasure-extra" or "treasure-obtain" or "card-menu"=>child=="reward-set",
        "reward-set"=>child=="reward-item",
        "RewardSetup"=>closure&&child=="card-add"||scenario=="reward-repeat-treasure"&&child=="treasure-exit",
        "reward-item"=>T(parent.SelectedOption,"Kind") switch{"CardReward"=>child=="card-menu","RelicReward"=>child=="reward-set",_=>false},
        "UsePotion"=>child=="potion-use",
        "TreasureOpen"=>child is "treasure-normal" or "treasure-extra",
        "TreasurePick"=>child=="treasure-choice",
        "treasure-choice"=>child=="treasure-obtain",
        "TreasureExit"=>child=="treasure-exit",
        _=>false};
    static JsonElement Latest(Op op)=>op.LastChild.ValueKind==JsonValueKind.Undefined?op.Open:op.LastChild;
    // These wrappers perform no native business between their direct children or after the last child.
    // Other parents (Offer, EventOption, acquisition hooks) deliberately retain business intervals.
    static bool StaticWrapper(string kind)=>kind is "UsePotion" or "TreasureOpen" or "TreasurePick" or "TreasureExit";
    static void CountChildren(Op op,string child,int count)=>R(op.Children.Count(x=>x.Kind==child)==count,"Reward required/unique child mismatch: "+op.Kind+"/"+child);
    void Owner(JsonElement p)=>R(root!=""&&T(p,"root")==root&&T(p,"context")==context,"Reward active owner mismatch");
    void Static(JsonElement p,JsonElement state)
    {
        // This profile explicitly closes the experiment after the last native
        // action. Its battle contract independently certifies this sole change.
        var comparable=state;
        if(NativeSolverRun.IsScenario(scenario)&&T(p,"kind")=="n3c-contract"){
            R(state.GetProperty("lifecycle").GetProperty("native").GetProperty("Closed").GetBoolean()&&!confirmed.GetProperty("lifecycle").GetProperty("native").GetProperty("Closed").GetBoolean(),"Solver experiment declared close boundary");
            var normalized=JsonNode.Parse(state.GetRawText())!;normalized["lifecycle"]!["native"]!["Closed"]=false;comparable=JsonSerializer.SerializeToElement(normalized);
        }
        R(root==""&&T(p,"root")==lastRoot&&T(p,"context")==lastContext&&Eq(comparable,confirmed),"Reward static confirmed state mismatch");
    }
    static bool Contains(JsonElement state,string list,string id)=>state.GetProperty(list).EnumerateArray().Any(x=>x.GetProperty("id").GetString()==id);
    static string[] Slots(JsonElement state)=>state.GetProperty("inventory").GetProperty("slots").EnumerateArray().Select(x=>Ref(x.GetProperty("potion"))).ToArray();
    // Fixed Dummy V1: SetEventFinished clears options, then NextItem consumes one
    // xoshiro256** step from Player.Rewards. Construct the expected transition from
    // the immutable input, never from the output's observed differences.
    static JsonElement ResumeCollectionState(JsonElement input)
    {
        var expected=JsonNode.Parse(input.GetRawText())!;
        R(T(input.GetProperty("lifecycle").GetProperty("eventModel"),"model")=="EVENT.BATTLEWORN_DUMMY","Resume generation model outside finite contract");
        expected["lifecycle"]!["eventModel"]!["IsFinished"]=true;
        expected["rooms"]!["eventOptions"]=new JsonArray();
        var rng=input.GetProperty("playerRng").GetProperty("rngs").GetProperty("Rewards");
        ulong a=rng.GetProperty("s0").GetUInt64(),b=rng.GetProperty("s1").GetUInt64(),c=rng.GetProperty("s2").GetUInt64(),d=rng.GetProperty("s3").GetUInt64();
        ulong t=b<<17;c^=a;d^=b;b^=c;a^=d;c^=t;d=System.Numerics.BitOperations.RotateLeft(d,45);
        var next=expected["playerRng"]!["rngs"]!["Rewards"]!;
        next["counter"]=checked(rng.GetProperty("counter").GetInt64()+1);
        next["s0"]=a;next["s1"]=b;next["s2"]=c;next["s3"]=d;
        return JsonSerializer.SerializeToElement(expected);
    }
    void ResumePotionResult(Op op,JsonElement state,JsonElement result)
    {
        // Current V1 has exactly one actual potion reward and no other acquisition
        // effect. Preserve all unrelated/unknown fields; a new hook needs a contract.
        R(T(op.SelectedOption,"Kind")=="PotionReward"&&result.GetProperty("success").GetBoolean(),"Resume potion branch outside finite acquisition contract");
        string acquired=Ref(result.GetProperty("acquired"));
        R(acquired!=""&&!Slots(op.Open).Contains(acquired),"Resume potion must be a new inventory instance");
        var expected=JsonNode.Parse(op.Open.GetRawText())!;
        var before=Slots(op.Open);int index=Array.IndexOf(before,"");R(index>=0,"Resume potion capacity branch unsupported");
        var offered=stack[^2].SetOptions.EnumerateArray().Single(x=>Eq(x.GetProperty("reference"),op.Selected));
        string model=T(offered,"model");R(Ref(offered.GetProperty("content"))==acquired&&identities.TryGetValue(acquired,out var identity)&&identity.Model==model,"Resume actual offered potion/model");
        var slot=expected["inventory"]!["slots"]![index]!;slot["potion"]=JsonNode.Parse(result.GetProperty("acquired").GetRawText());slot["model"]=model;slot["queued"]=false;
        // Player.Potions enumerates occupied slots in slot order, including a
        // newly refilled earlier hole. Preserve the exact ordered projection.
        var ordered=new JsonArray();foreach(var item in expected["inventory"]!["slots"]!.AsArray())if(item!["potion"] is JsonObject potion)ordered.Add(new JsonObject{["id"]=potion["$ref"]!.GetValue<string>(),["model"]=item["model"]!.GetValue<string>()});
        expected["potions"]=ordered;
        R(Eq(state,JsonSerializer.SerializeToElement(expected)),"Resume potion acquisition complete state transition");
    }
    JsonElement ChestRoom(JsonElement state,JsonElement action){
        var reference=action.GetProperty("room");R(T(state,"room")=="TreasureRoom"&&identities.TryGetValue(Ref(reference),out var identity)&&identity.Type=="TreasureRoom","Treasure registered room type");
        if(state.TryGetProperty("rooms",out var rooms))R(rooms.ValueKind==JsonValueKind.Object&&Eq(reference,rooms.GetProperty("reference")),"Treasure actual current room projection");
        else R(!NativeMajor.IsScenario(scenario),"Major treasure requires room identity projection");
        return reference;
    }
    internal void Accept(JsonElement p,JsonElement? snapshot)
    {
        string eventKind=T(p,"kind");var detail=p.GetProperty("detail");var f=detail.ValueKind==JsonValueKind.Object&&detail.TryGetProperty("facts",out var facts)?facts:default;
        if(eventKind=="header"){defaultRelics=detail.TryGetProperty("defaultRelicProfile",out var drp)&&drp.ValueKind==JsonValueKind.String&&drp.GetString()==NativeRelicPickup.Profile;scenario=T(detail,"scenario");closure=NativeRewardClosure.IsScenario(scenario);}
        if(eventKind=="identity")identities.Add(T(detail,"id"),(T(detail,"type"),detail.GetProperty("model").GetString()));
        if(NativeEventFlow.IsScenario(scenario)&&eventKind is "ef-resume-start" or "ef-resume-return"){
            Owner(p);R(kind=="EventProceed"&&stack.Count==1&&stack[0].Kind=="EventProceed"&&stack[0].Started,"Resume task/reward parent ownership");
            var parent=stack[0];
            if(eventKind=="ef-resume-start"){
                R(eventResume==""&&parent.Children.Count==0,"Resume task start before owned rewards");eventResume=T(f,"id");R(eventResume.Length>0,"Empty Resume task identity");eventResumeInput=eventResumeAnchor=snapshot!.Value.Clone();
            }else{
                R(eventResume==T(f,"id")&&!eventResumeCompleted&&parent.Children.Count==1&&parent.Children[0].Kind=="reward-set"&&parent.Children.All(x=>x.Returned)&&Eq(snapshot!.Value,Latest(parent)),"Resume task completion before owned reward return/full state");eventResumeCompleted=true;
            }
        }
        if(NativeEventFlow.IsScenario(scenario)&&eventKind=="ef-room-resume-return"){
            Owner(p);R(kind=="EventProceed"&&eventResume!=""&&Eq(snapshot!.Value,eventResumeAnchor),"Outer Resume return current task stage complete state");
        }
        if(snapshot is {} s){
            var inventory=s.GetProperty("inventory");var slots=inventory.GetProperty("slots");
            R(slots.ValueKind==JsonValueKind.Array&&slots.GetArrayLength()==inventory.GetProperty("capacity").GetInt32(),"Reward potion capacity/slots mismatch");
            int i=0;foreach(var slot in slots.EnumerateArray()){R(slot.GetProperty("index").GetInt32()==i++,"Potion slot order mismatch");var potion=Ref(slot.GetProperty("potion"));if(potion!="")R(Contains(s,"potions",potion),"Potion slot/instance mismatch");}
            R(Slots(s).Where(x=>x!="").ToHashSet().SetEquals(s.GetProperty("potions").EnumerateArray().Select(x=>T(x,"id"))),"Compact potion inventory/slots mismatch");
        }
        if(eventKind=="root-request"){R(!boundaryRequired&&stack.Count==0&&!contract,"Unclosed reward boundary/operation before root");}
        if(eventKind=="dispatch"){
            R(root=="","Nested reward root");root=T(p,"root");context=T(p,"context");kind=T(f,"kind");dispatch=snapshot!.Value.Clone();decision=f.GetProperty("decision").Clone();rootReturned=false;lastRootReturn=default;
            if(kind=="TreasureOpen"){
                string id=Ref(ChestRoom(dispatch,decision));
                R(!chests.ContainsKey(id),"Treasure unique concrete room scope");
                R(chests.Values.All(x=>x.chestExited),"Previous chest native Exit required before new room scope");chest=new();chests.Add(id,chest);
            }
            if(kind is "TreasurePick" or "TreasureExit")R(chests.TryGetValue(Ref(decision.GetProperty("room")),out var owned)&&ReferenceEquals(owned,chest)&&Eq(decision.GetProperty("room"),ChestRoom(dispatch,decision)),"Treasure action current room scope");
            if(kind=="TreasureOpen")R(!chestOpen&&!normalChest&&!extraChest,"Duplicate treasure open");
            if(kind=="TreasurePick")R(chestOpen&&!chestPicked,"Treasure pick before chest tasks/duplicate");
            if(kind=="TreasureExit")R(chestPicked&&!chestExited,"Treasure exit before selection/duplicate");
        }
        if(eventKind=="prep-chest-exit-return"){
            Owner(p);R(kind=="Travel"&&chests.TryGetValue(Ref(f.GetProperty("room")),out var exited)&&exited.chestPicked&&!exited.chestExited&&f.GetProperty("closed").GetBoolean(),"Treasure original exit current room scope");chests[Ref(f.GetProperty("room"))].chestExited=true;
        }
        if(eventKind=="confirmed"){
            Owner(p);R(stack.Count==0,"Reward confirmation with pending child");
            if(CKind(kind)){R(rootReturned&&Eq(snapshot!.Value,lastRootReturn),"Reward confirmation before return/state mismatch");boundaryRequired=true;}
            if(kind=="TreasureOpen"){R(normalChest&&extraChest,"Missing native chest reward stages");chestOpen=true;}
            if(kind=="TreasurePick")chestPicked=true;
            if(kind=="TreasureExit"){R(exitReturned,"Missing native chest Exit");chestExited=true;}
            lastRoot=root;lastContext=context;root="";confirmed=snapshot!.Value.Clone();
        }
        if(eventKind=="fixture-setup-declared"){Static(p,snapshot!.Value);R(NativeRewardProbe.Fixtures.Contains(scenario)||closure||NativeMajor.FixtureScenario(scenario),"Reward fixture origin mismatch");fixtureBefore=snapshot.Value.Clone();fixtureSets=sets;fixtureNested=nestedSets;fixtureUses=potionUses;fixtureCapacityFailures=capacityFailures;fixtureDiscards=discards;}
        if(eventKind=="n3c-open"){
            Owner(p);string id=T(f,"id"),parent=T(f,"parent"),opKind=T(f,"kind");
            R(!operations.ContainsKey(id)&&parent==(stack.Count==0?root:stack[^1].Id),"Reward operation parent/unique mismatch");
            if(stack.Count==0){R(CKind(kind)&&opKind==kind&&!rootReturned&&Eq(snapshot!.Value,dispatch)&&f.GetProperty("subject").ValueKind==JsonValueKind.Null,"Reward root start/dispatch mismatch");roots++;}
            else{
                var owner=stack[^1];R(owner.Started&&!owner.Returned&&AllowedChild(owner,opKind),"Reward child kind/start/order mismatch");
                if(NativeEventFlow.IsScenario(scenario)&&owner.Kind=="EventProceed"){
                    R(eventResume!=""&&!eventResumeCompleted&&owner.Children.Count==0,"Reward child outside actual Resume task/unique");
                    R(Eq(snapshot!.Value,ResumeCollectionState(eventResumeInput)),"Resume reward-set entry complete generation transition");
                }
                if(NativeEventFlow.IsScenario(scenario)&&owner.Kind=="EventRewards")R(owner.Children.Count==0&&Eq(snapshot!.Value,Latest(owner)),"EventRewards empty collection entry complete parent state");
                if(owner.Kind=="RoomOption"&&T(owner.Open,"room")=="EventRoom"&&T(owner.Open.GetProperty("lifecycle").GetProperty("eventModel"),"model")=="EVENT.POTION_COURIER"){
                    R(decision.GetProperty("index").GetInt32()==1&&owner.Children.Count==0&&opKind=="reward-set","Courier Ransack required unique reward child");
                    var input=owner.Open.GetProperty("playerRng").GetProperty("rngs").GetProperty("Rewards");var output=snapshot!.Value.GetProperty("playerRng").GetProperty("rngs").GetProperty("Rewards");NativeMerchantEconomyContract.CheckRng(input,output);R(output.GetProperty("counter").GetInt64()==input.GetProperty("counter").GetInt64()+1,"Courier native one random potion selection");
                    var expected=JsonNode.Parse(owner.Open.GetRawText())!;expected["playerRng"]!["rngs"]!["Rewards"]=JsonNode.Parse(output.GetRawText());R(Eq(JsonSerializer.SerializeToElement(expected),snapshot.Value),"Courier actual reward entry full frame");
                }
                if(owner.Kind=="reward-set")R(owner.SetDeclared&&!owner.SetCompleted&&!owner.Skipped&&Eq(snapshot!.Value,Latest(owner)),"Reward item static collection state mismatch");
                if(NativeMajor.IsScenario(scenario)&&owner.Kind=="RewardOffer"){
                    var expected=JsonNode.Parse(Latest(owner).GetRawText())!;
                    var before=Latest(owner).GetProperty("playerRng").GetProperty("rngs").GetProperty("Rewards");var after=snapshot!.Value.GetProperty("playerRng").GetProperty("rngs").GetProperty("Rewards");
                    long draws=after.GetProperty("counter").GetInt64()-before.GetProperty("counter").GetInt64();R(draws>=0&&draws<=128,"Major reward generation draw budget");
                    ulong a=before.GetProperty("s0").GetUInt64(),b=before.GetProperty("s1").GetUInt64(),c=before.GetProperty("s2").GetUInt64(),d=before.GetProperty("s3").GetUInt64();
                    for(long i=0;i<draws;i++){ulong t=b<<17;c^=a;d^=b;b^=c;a^=d;c^=t;d=System.Numerics.BitOperations.RotateLeft(d,45);}
                    R(after.GetProperty("s0").GetUInt64()==a&&after.GetProperty("s1").GetUInt64()==b&&after.GetProperty("s2").GetUInt64()==c&&after.GetProperty("s3").GetUInt64()==d,"Major reward RNG state/counter transition");
                    expected["playerRng"]!["rngs"]!["Rewards"]=JsonNode.Parse(after.GetRawText());
                    // Pinned WingCharm.TryModifyCardRewardOptionsLate selects one
                    // enchantable reward via RunState.Rng.Niche.NextItem. The
                    // generation frame owns that one draw, not arbitrary run RNG.
                    var nicheBefore=Latest(owner).GetProperty("runRng").GetProperty("rngs").GetProperty("Niche");var nicheAfter=snapshot.Value.GetProperty("runRng").GetProperty("rngs").GetProperty("Niche");
                    if(!Eq(nicheBefore,nicheAfter)){
                        R(Latest(owner).GetProperty("relics").EnumerateArray().Any(r=>T(r,"model")=="RELIC.WING_CHARM")&&nicheAfter.GetProperty("counter").GetInt64()==nicheBefore.GetProperty("counter").GetInt64()+1,"WingCharm reward generation owns one Niche draw");
                        NativeMerchantEconomyContract.CheckRng(nicheBefore,nicheAfter);expected["runRng"]!["rngs"]!["Niche"]=JsonNode.Parse(nicheAfter.GetRawText());
                    }
                    R(Eq(snapshot.Value,JsonSerializer.SerializeToElement(expected)),"Major reward generation complete parent frame");
                }
                if(StaticWrapper(owner.Kind))R(Eq(snapshot!.Value,Latest(owner)),"Reward wrapper child static state mismatch");
                if(closure&&(opKind=="card-menu"&&owner.Kind=="reward-item"||opKind=="card-add"&&owner.Kind=="RewardSetup"))R(Eq(snapshot!.Value,Latest(owner)),"Closure child entry/parent complete state mismatch");
            }
            var options=f.GetProperty("options");
            R(options.ValueKind is JsonValueKind.Array or JsonValueKind.Null,"Missing reward options contract");
            if(options.ValueKind==JsonValueKind.Array){var handles=options.EnumerateArray().Select(x=>T(x,"Handle")).ToArray();R(handles.Length>0&&handles.Distinct().Count()==handles.Length&&handles.Select((h,i)=>h=="o"+i).All(x=>x),"Reward candidate handle/order mismatch");}
            R((opKind is "reward-item" or "card-menu" or "treasure-choice")== (options.ValueKind==JsonValueKind.Array),"Reward operation decision shape mismatch");
            var op=new Op(id,parent,opKind,snapshot!.Value,options,f.GetProperty("subject"));operations.Add(id,op);if(stack.Count>0)stack[^1].Children.Add(op);stack.Add(op);
            if(opKind=="reward-set")R(identities.TryGetValue(Ref(op.Subject),out var setIdentity)&&setIdentity.Type=="RewardsSet","Reward collection subject type mismatch");
            if(opKind=="reward-item"){
                R(stack.Count>=2&&stack[^2].Kind=="reward-set"&&Eq(op.Subject,stack[^2].Subject),"Reward request collection instance mismatch");var set=stack[^2];
                var expected=new List<NativeRewards.Option>();
                foreach(var item in set.SetOptions.EnumerateArray().Where(x=>!set.Taken.Contains(Ref(x.GetProperty("reference")))))expected.Add(new("o"+expected.Count,T(item,"kind"),item.GetProperty("reference").Clone(),item.GetProperty("model").GetString(),item.GetProperty("amount").ValueKind==JsonValueKind.Null?null:item.GetProperty("amount").GetInt32()));
                if(Slots(op.Open).All(x=>x!="")&&op.Open.GetProperty("inventory").GetProperty("canUseOrRemove").GetBoolean())foreach(var slot in op.Open.GetProperty("inventory").GetProperty("slots").EnumerateArray())expected.Add(new("o"+expected.Count,"DiscardPotion",slot.GetProperty("potion").Clone(),slot.GetProperty("model").GetString()));
                if(!set.DisallowSkip)expected.Add(new("o"+expected.Count,"SkipRewards",null));
                R(Eq(op.Options,JsonSerializer.SerializeToElement(expected,Program.Json)),"Reward legal candidate inventory/order incomplete");
            }
            if(opKind=="card-menu")R(stack.Count>=2&&stack[^2].Kind=="reward-item"&&Eq(op.Subject,stack[^2].Selected),"Card menu reward instance mismatch");
            if(opKind=="card-menu")foreach(var option in op.Options.EnumerateArray()){
                R(identities.TryGetValue(Ref(option.GetProperty("Reference")),out var content),"Missing native card menu identity");
                if(T(option,"Kind")=="Card")R(content.Model==option.GetProperty("Model").GetString()&&option.GetProperty("Upgrade").GetInt32()>=0,"Native card menu model/upgrade mismatch");
                else R(T(option,"Kind")=="Alternative"&&content.Type=="CardRewardAlternative"&&option.GetProperty("Model").ValueKind==JsonValueKind.String,"Native card alternative identity mismatch");
            }
            if(opKind=="treasure-choice")R(kind=="TreasurePick"&&Eq(op.Subject,decision.GetProperty("room")),"Treasure choice room instance mismatch");
            if(scenario=="reward-repeat-treasure"&&opKind=="treasure-exit"){
                R(kind=="RewardSetup"&&stack.Count==2&&stack[0].Kind=="RewardSetup"&&T(dispatch,"room")=="TreasureRoom"&&Eq(op.Subject,dispatch.GetProperty("rooms").GetProperty("reference"))&&chests.TryGetValue(Ref(op.Subject),out var exiting)&&ReferenceEquals(exiting,chest)&&exiting.chestPicked&&!exiting.chestExited,"Repeated chest exit current original manager transition");
                var expected=JsonNode.Parse(dispatch.GetRawText())!;expected["room"]=null;expected["rooms"]=null;
                R(Eq(snapshot!.Value,JsonSerializer.SerializeToElement(expected)),"Repeated chest exit popped original room full state");
            }else if(opKind is "treasure-normal" or "treasure-extra" or "treasure-exit")R(Eq(op.Subject,decision.GetProperty("room")),"Treasure business room instance mismatch");
            if(opKind=="treasure-obtain")R(stack.Count>=2&&stack[^2].Kind=="treasure-choice"&&Eq(op.Subject,stack[^2].Selected),"Treasure distribution candidate mismatch");
            if(opKind=="treasure-normal")R(kind=="TreasureOpen"&&!normalChest&&!extraChest,"Chest normal stage order/duplicate");
            if(opKind=="treasure-extra")R(kind=="TreasureOpen"&&normalChest&&!extraChest,"Chest extra stage order/duplicate");
            if(opKind=="potion-use")R(kind=="UsePotion"&&Eq(op.Subject,DecisionReference("potion")),"Potion request/root instance mismatch");
            if(opKind=="potion-use")R(Slots(op.Open)[decision.GetProperty("slot").GetInt32()]==Ref(op.Subject),"Potion root slot/instance mismatch");
        }
        if(eventKind.StartsWith("n3c-")&&eventKind is not ("n3c-open" or "n3c-boundary" or "n3c-contract" or "n3c-assertion" or "n3c-card-continuity")){
            Owner(p);R(stack.Count>0&&stack[^1].Id==T(f,"id"),"Reward operation owner/order mismatch");var op=stack[^1];var state=snapshot!.Value;
            if(eventKind=="n3c-answer"){
                R(!op.Answered&&!op.Started&&op.Options.ValueKind==JsonValueKind.Array&&Eq(state,op.Open),"Reward answer order/state mismatch");
                var option=op.Options.EnumerateArray().SingleOrDefault(x=>T(x,"Handle")==T(f,"handle"));
                R(option.ValueKind==JsonValueKind.Object&&Eq(option.GetProperty("Reference"),f.GetProperty("selected")),"Reward answer candidate instance mismatch");op.Selected=f.GetProperty("selected").Clone();op.SelectedOption=option.Clone();op.Answered=true;
            }
            if(eventKind=="n3c-start"){R(!op.Started&&(op.Options.ValueKind==JsonValueKind.Null||op.Answered)&&Eq(state,op.Open),"Reward start answer/complete state mismatch");op.Started=true;}
            if(eventKind=="n3c-set"){
                R(op.Kind=="reward-set"&&op.Started&&!op.SetDeclared&&Eq(state,op.Open)&&Eq(f.GetProperty("reference"),op.Subject),"Native reward set declaration owner/state mismatch");
                op.NativeSet=f.GetProperty("set").GetInt32();R(op.NativeSet>=0&&nativeSets.Add(op.NativeSet),"Duplicate/invalid native reward set id");
                op.SetDeclared=true;op.SetOptions=f.GetProperty("options").Clone();op.DisallowSkip=f.GetProperty("disallowSkip").GetBoolean();sets++;if(stack.Count>2)nestedSets++;
                if(NativeEventFlow.IsScenario(scenario)&&kind=="EventRewards")R(op.SetOptions.GetArrayLength()==0&&!op.DisallowSkip,"Current event combat has a native empty reward collection");
                if(NativeEventFlow.IsScenario(scenario)&&kind=="EventProceed")R(op.SetOptions.GetArrayLength()==1&&T(op.SetOptions[0],"kind")=="PotionReward"&&!op.DisallowSkip,"Current V1 Resume has one native potion reward");
                int index=0;var refs=new HashSet<string>();foreach(var item in op.SetOptions.EnumerateArray()){
                    string reference=Ref(item.GetProperty("reference")),itemKind=T(item,"kind");R(item.GetProperty("index").GetInt32()==index++&&refs.Add(reference)&&identities.TryGetValue(reference,out var identity)&&identity.Type==itemKind,"Reward offered identity/type/order mismatch");
                    if(itemKind is "PotionReward" or "RelicReward" or "SpecialCardReward")R(identities.TryGetValue(Ref(item.GetProperty("content")),out var content)&&content.Model==item.GetProperty("model").GetString(),"Reward offered content/model mismatch");
                    if(itemKind=="SpecialCardReward"){
                        var card=item.GetProperty("card");R(Eq(card.GetProperty("reference"),item.GetProperty("content"))&&T(card,"model")==T(item,"model"),"Special card offered identity/content binding");
                        if(NativeNaturalBoss.IsSecondScenario(scenario))NativeCardRecordContract.Card(card);
                    }
                }
            }
            if(eventKind=="n3c-set-completed"){
                R(op.Kind=="reward-set"&&op.SetDeclared&&!op.SetCompleted&&f.GetProperty("set").GetInt32()==op.NativeSet&&Eq(state,op.LastChild.ValueKind==JsonValueKind.Undefined?op.Open:op.LastChild),"Reward set completion order/state mismatch");
                bool skipped=f.GetProperty("skipped").GetBoolean();R(skipped==op.Skipped&&(!skipped||!op.DisallowSkip)&& (skipped||op.Taken.Count==op.SetOptions.GetArrayLength()),"Reward collection closed before native selection/skip");
                R(f.GetProperty("allTaken").GetBoolean()==(op.Taken.Count==op.SetOptions.GetArrayLength()),"Reward all-taken mismatch");op.SetCompleted=true;op.CompletedState=state.Clone();
            }
            if(eventKind=="n3c-return"){
                R(op.Started&&!op.Returned&&(op.Kind!="reward-set"||op.SetCompleted),"Reward return before start/completion");var result=f.GetProperty("result");
                R(result.ValueKind==JsonValueKind.Object,"Reward result object required");
                if(CKind(op.Kind))R(result.GetProperty("completed").GetBoolean(),"Reward root completion false");
                if(op.Kind=="RewardOffer")CountChildren(op,"reward-set",1);
                if(scenario=="reward-repeat-treasure"&&op.Kind=="RewardSetup"){
                    CountChildren(op,"treasure-exit",T(op.Open,"room")=="TreasureRoom"?1:0);
                    R(T(state,"room")== (T(decision,"room")=="Treasure"?"TreasureRoom":"RestSiteRoom")&&state.GetProperty("rooms").ValueKind==JsonValueKind.Object&&!Eq(state.GetProperty("rooms").GetProperty("reference"),op.Open.GetProperty("rooms").GetProperty("reference")),"Repeated chest actual new room after native exit");
                }
                if(NativeEventFlow.IsScenario(scenario)&&op.Kind=="EventRewards"){
                    CountChildren(op,"reward-set",1);R(op.Children.All(x=>x.Returned)&&Eq(state,Latest(op)),"EventRewards return/owned native set full state");
                }
                if(op.Kind=="RoomOption"&&T(op.Open,"room")=="EventRoom"&&T(op.Open.GetProperty("lifecycle").GetProperty("eventModel"),"model")=="EVENT.POTION_COURIER"){
                    R(op.Children.Count==1&&op.Children[0].Kind=="reward-set"&&op.Children.All(x=>x.Returned),"Courier owned reward task complete");
                    var expected=JsonNode.Parse(Latest(op).GetRawText())!;expected["lifecycle"]!["eventModel"]!["IsFinished"]=true;expected["rooms"]!["eventOptions"]=new JsonArray();R(Eq(state,JsonSerializer.SerializeToElement(expected)),"Courier completion full state/child binding");
                }
                if(op.Kind=="UsePotion")CountChildren(op,"potion-use",1);
                if(op.Kind=="TreasureOpen"){CountChildren(op,"treasure-normal",1);CountChildren(op,"treasure-extra",1);}
                if(op.Kind=="TreasurePick")CountChildren(op,"treasure-choice",1);
                if(op.Kind=="TreasureExit")CountChildren(op,"treasure-exit",1);
                if(StaticWrapper(op.Kind))R(op.Children.Count>0&&op.Children.All(x=>x.Returned)&&Eq(state,Latest(op)),"Reward wrapper return static state mismatch");
                if(NativeEventFlow.IsScenario(scenario)&&op.Kind=="EventProceed")R(eventResumeCompleted&&op.Children.Count==1&&op.Children[0].Kind=="reward-set"&&op.Children.All(x=>x.Returned)&&Eq(state,Latest(op)),"Event proceed return actual child complete state mismatch");
                if(NativeEventFlow.IsScenario(scenario)&&op.Kind=="RoomOption"&&op.Open.GetProperty("lifecycle").GetProperty("eventModel").GetProperty("model").GetString()=="EVENT.WHISPERING_HOLLOW"){
                    R(op.Children.Count==1&&op.Children[0].Kind=="reward-set"&&op.Children.All(x=>x.Returned),"Hollow native reward child required unique");
                    var expected=System.Text.Json.Nodes.JsonNode.Parse(Latest(op).GetRawText())!;expected["lifecycle"]!["eventModel"]!["IsFinished"]=true;expected["rooms"]!["eventOptions"]=new System.Text.Json.Nodes.JsonArray();
                    R(Eq(state,JsonSerializer.SerializeToElement(expected)),"Hollow completion full state/child binding");
                }
                if((closure||NativeMajor.IsScenario(scenario))&&(op.Kind=="RewardOffer"||op.Kind=="RewardSetup"&&op.Children.Count>0))R(op.Children.Count>0&&op.Children.All(x=>x.Returned)&&Eq(state,Latest(op)),"Closure parent return/latest child complete state mismatch");
                if(op.Kind=="reward-set")R(result.GetProperty("complete").GetBoolean()&&Eq(state,op.CompletedState),"Reward set return completion/static state mismatch");
                if(op.Kind=="reward-item"){
                    R(stack.Count>=2&&stack[^2].Kind=="reward-set","Reward selection outside collection");var set=stack[^2];string selected=Ref(op.Selected),selectedKind=T(op.SelectedOption,"Kind");
                    R(Eq(result.GetProperty("selected"),op.Selected),"Reward selected result identity mismatch");
                    R(result.GetProperty("skipped").GetBoolean()==(selectedKind=="SkipRewards"),"Reward skip/result mismatch");
                    if(selectedKind is "SkipRewards" or "DiscardPotion")R(result.GetProperty("success").GetBoolean()&&result.GetProperty("acquired").ValueKind==JsonValueKind.Null,"Reward non-acquisition result mismatch");
                    if(selectedKind=="GoldReward")R(result.GetProperty("acquired").ValueKind==JsonValueKind.Null,"Gold reward reported object acquisition");
                    if(selectedKind=="SpecialCardReward"){
                        var offered=set.SetOptions.EnumerateArray().Single(x=>Eq(x.GetProperty("reference"),op.Selected));var original=offered.GetProperty("card");var actual=result.GetProperty("acquiredCard");string id=Ref(result.GetProperty("acquired"));
                        R(result.GetProperty("success").GetBoolean()&&id==Ref(offered.GetProperty("content"))&&id==Ref(actual.GetProperty("reference"))&&!Contains(op.Open,"deck",id)&&Contains(state,"deck",id),"Special card actual offered instance acquisition");
                        R(T(original,"model")==T(actual,"model")&&Eq(original.GetProperty("upgrade"),actual.GetProperty("upgrade")),"Special card source model/upgrade");
                        var added=new JsonObject{["id"]=id,["model"]=T(actual,"model"),["upgrade"]=actual.GetProperty("upgrade").GetInt32()};
                        if(NativeNaturalBoss.IsSecondScenario(scenario)){
                            R(Eq(original.GetProperty("runFloor"),actual.GetProperty("runFloor")),"Special card current acquisition floor");var native=JsonNode.Parse(original.GetProperty("native").GetRawText())!;native["floor_added_to_deck"]=original.GetProperty("runFloor").GetInt32();R(Eq(JsonSerializer.SerializeToElement(native),actual.GetProperty("native")),"Special card original serialized content/entry floor");added["native"]=JsonNode.Parse(actual.GetProperty("native").GetRawText());
                        }
                        var expected=JsonNode.Parse(op.Open.GetRawText())!;expected["deck"]!.AsArray().Add(added);R(Eq(state,JsonSerializer.SerializeToElement(expected)),"Special card complete native deck acquisition frame");acquiredCards.Add(id);
                    }
                    if(selectedKind=="CardReward"){
                        if(!closure)CountChildren(op,"card-menu",1);else R(op.Children.Count>0&&op.Children.All(x=>x.Kind=="card-menu"&&x.Returned),"Closure card menus required/complete");var menu=op.Children.Last(x=>x.Kind=="card-menu");
                        R(menu.Returned&&result.GetProperty("success").GetBoolean()==menu.Result.GetProperty("success").GetBoolean()&&Eq(state,menu.ReturnedState),"Card reward/menu completion mismatch");
                        R(Eq(result.GetProperty("acquired"),menu.Result.GetProperty("acquired")),"Card reward acquired instance/child mismatch");
                    }
                    if(selectedKind=="SkipRewards"){R(!set.DisallowSkip&&result.GetProperty("skipped").GetBoolean(),"Required rewards skipped");set.Skipped=true;}
                    else if(selectedKind=="DiscardPotion"){R(Slots(op.Open).Contains(selected)&&!Slots(state).Contains(selected),"Native discard inventory mismatch");discards++;}
                    else{
                        var item=set.SetOptions.EnumerateArray().SingleOrDefault(x=>Ref(x.GetProperty("reference"))==selected);R(item.ValueKind==JsonValueKind.Object&&T(item,"kind")==selectedKind,"Reward selection source mismatch");
                        if(result.GetProperty("success").GetBoolean()){
                            R(set.Taken.Add(selected),"Duplicate successful native reward");
                            if(selectedKind is "PotionReward" or "RelicReward"&&!(closure&&selectedKind=="PotionReward")){
                                string acquired=Ref(result.GetProperty("acquired"));R(acquired!=""&&acquired==Ref(item.GetProperty("content")),"Reward claimed/offered instance mismatch");
                                R(selectedKind=="PotionReward"?Slots(state).Contains(acquired)&&!Slots(op.Open).Contains(acquired):Contains(state,"relics",acquired)&&!Contains(op.Open,"relics",acquired),"Missing actual native inventory acquisition");
                                if(selectedKind=="PotionReward")acquiredPotions.Add(acquired);
                            }
                        }
                        else if(selectedKind=="PotionReward"){R(Slots(op.Open).All(x=>x!="")&&Eq(op.Open.GetProperty("inventory"),state.GetProperty("inventory")),"Failed full potion attempt changed inventory");capacityFailures++;}
                    }
                }
                if(op.Kind=="card-menu"&&!closure){
                    var source=result.GetProperty("sourceCards");R(source.ValueKind==JsonValueKind.Array,"Native card source inventory missing");
                    var offered=op.Options.EnumerateArray().Where(x=>T(x,"Kind")=="Card").ToArray();R(source.GetArrayLength()==offered.Length,"Native card source inventory incomplete");
                    for(int i=0;i<offered.Length;i++)R(Eq(source[i].GetProperty("reference"),offered[i].GetProperty("Reference"))&&T(source[i],"model")==T(offered[i],"Model")&&source[i].GetProperty("upgrade").GetInt32()==offered[i].GetProperty("Upgrade").GetInt32(),"Native card source model/upgrade mismatch");
                    if(scenario is "reward-card" or "reward-card-skip")R(source.GetArrayLength()==2&&T(source[0],"model")=="CARD.STRIKE_IRONCLAD"&&T(source[1],"model")=="CARD.DEFEND_IRONCLAD"&&source.EnumerateArray().All(x=>x.GetProperty("upgrade").GetInt32()==0),"Independent fixed unupgraded card source contract");
                    var alternatives=result.GetProperty("sourceAlternatives");var alternativeOptions=op.Options.EnumerateArray().Where(x=>T(x,"Kind")=="Alternative").ToArray();R(alternatives.ValueKind==JsonValueKind.Array&&alternatives.GetArrayLength()==alternativeOptions.Length,"Native card alternative source inventory incomplete");
                    for(int i=0;i<alternativeOptions.Length;i++)R(Eq(alternatives[i].GetProperty("reference"),alternativeOptions[i].GetProperty("Reference"))&&T(alternatives[i],"option")==T(alternativeOptions[i],"Model"),"Native card alternative source mismatch");
                    if(T(op.SelectedOption,"Kind")=="Card")R(result.GetProperty("success").GetBoolean(),"Native selected card reward must complete");
                    else{
                        var alternative=alternatives.EnumerateArray().Single(x=>Eq(x.GetProperty("reference"),op.Selected));string after=T(alternative,"afterSelected");
                        R(after is "EndSelectionAndCompleteReward" or "EndSelectionAndDoNotCompleteReward","Unfinished/unsupported card alternative loop");
                        R(result.GetProperty("success").GetBoolean()==(after=="EndSelectionAndCompleteReward"),"Native alternative completion mismatch");
                    }
                    R(T(result,"identityMode")=="same-instance","Unsupported card identity result mode");
                    string acquired=Ref(result.GetProperty("acquired"));if(T(op.SelectedOption,"Kind")=="Card")R(acquired==Ref(op.Selected)&&Contains(state,"deck",acquired)&&!Contains(op.Open,"deck",acquired),"Card menu without native permanent acquisition");else R(acquired=="","Alternative unexpectedly reported card acquisition");
                    if(acquired!=""){
                        var actual=result.GetProperty("acquiredCard");var card=state.GetProperty("deck").EnumerateArray().Single(x=>T(x,"id")==acquired);
                        R(Eq(actual.GetProperty("reference"),op.Selected)&&T(actual,"model")==T(op.SelectedOption,"Model")&&T(actual,"model")==T(card,"model")&&actual.GetProperty("upgrade").GetInt32()==card.GetProperty("upgrade").GetInt32(),"Native acquired card state mismatch");
                    }else R(result.GetProperty("acquiredCard").ValueKind==JsonValueKind.Null,"Alternative reported acquired card state");
                    if(acquired!="")acquiredCards.Add(acquired);
                }
                if(closure&&op.Kind=="card-menu"&&result.GetProperty("acquired").ValueKind==JsonValueKind.Object)acquiredCards.Add(Ref(result.GetProperty("acquired")));
                if(op.Kind=="potion-use"){
                    string consumed=Ref(result.GetProperty("consumed"));R(consumed==Ref(op.Subject)&&Eq(result.GetProperty("target"),DecisionReference("target"))&&Slots(op.Open).Contains(consumed)&&!Slots(state).Contains(consumed),"Potion use identity/consumption mismatch");potionUses++;
                    if(scenario=="rewards-natural")R(acquiredPotions.Contains(consumed),"Natural potion use without earlier native reward acquisition");
                    if(scenario=="reward-block")R(state.GetProperty("block").GetInt32()-op.Open.GetProperty("block").GetInt32()==12,"Independent BlockPotion 12 block contract");
                    if(scenario=="reward-fire"){
                        string target=Ref(result.GetProperty("target"));var before=op.Open.GetProperty("combat").GetProperty("enemies").EnumerateArray().Single(x=>T(x,"id")==target);var after=state.GetProperty("combat").GetProperty("enemies").EnumerateArray().Single(x=>T(x,"id")==target);
                        int hp=before.GetProperty("hp").GetInt32(),block=before.GetProperty("block").GetInt32();R(after.GetProperty("hp").GetInt32()==hp-Math.Min(hp,Math.Max(0,20-block)),"Independent FirePotion 20 damage contract");
                    }
                }
                if(op.Kind=="treasure-obtain"){
                    string acquired=Ref(result.GetProperty("acquired"));
                    R(T(result,"identityMode")=="same-instance"&&Eq(result.GetProperty("canonical"),op.Subject)&&Eq(result.GetProperty("created"),result.GetProperty("acquired"))&&acquired!=Ref(op.Subject)&&!Contains(op.Open,"relics",acquired)&&Contains(state,"relics",acquired),"Treasure native new instance/source mismatch");
                    var canonical=identities[Ref(op.Subject)];var created=identities[acquired];
                    R(canonical.Type==created.Type&&canonical.Model==created.Model&&canonical.Model==T(result,"model")&&state.GetProperty("relics").EnumerateArray().Single(x=>T(x,"id")==acquired).GetProperty("model").GetString()==canonical.Model,"Treasure canonical/mutable model mismatch");treasureObtains++;chest.Obtains++;
                }
                if(op.Kind=="treasure-normal"){
                    int gold=result.GetProperty("gold").GetInt32();R((T(result,"accounting")=="native-unmodified"||scenario=="closure-spoils"&&T(result,"accounting")=="native-spoils-quest")&&gold>=0&&gold==result.GetProperty("actualGoldDelta").GetInt32()&&gold==state.GetProperty("gold").GetInt32()-op.Open.GetProperty("gold").GetInt32(),"Treasure native gold result/state mismatch");normalChest=true;
                }
                if(op.Kind=="treasure-extra"){R(result.GetProperty("completed").GetBoolean(),"Chest extra tasks incomplete");extraChest=true;}
                if(op.Kind=="treasure-choice"){
                    treasureSkipped=T(op.SelectedOption,"Kind")=="SkipTreasure";
                    R(kind=="TreasurePick"&&result.GetProperty("skip").GetBoolean()==treasureSkipped&&result.GetProperty("notifications").GetInt32()==(treasureSkipped?0:1)&&chest.Obtains==(treasureSkipped?0:1),"Treasure selection/distribution/acquisition mismatch");
                }
                if(op.Kind=="treasure-exit"){
                    R((kind=="TreasureExit"||scenario=="reward-repeat-treasure"&&kind=="RewardSetup")&&result.GetProperty("pickingClosed").GetBoolean(),"Chest original Exit did not close picking");
                    if(scenario=="reward-repeat-treasure"){R(Eq(state,op.Open),"Repeated chest original Exit immutable return");chest.chestExited=true;}
                    exitReturned=true;
                }
                if(NativeMajor.IsScenario(scenario)&&op.Kind=="reward-item"&&T(op.SelectedOption,"Kind")=="RelicReward"){
                    // MealTicket/Orichalcum have no immediate acquisition effect.
                    string acquired=Ref(result.GetProperty("acquired")),model=T(op.SelectedOption,"Model");
                    var expectedType=defaultRelics?NativeRelicPickup.Resolve(model):NativeRewards.MajorRelicType(model);
                    R(expectedType!=null&&result.GetProperty("success").GetBoolean()&&op.Children.Count==0&&identities.TryGetValue(acquired,out var registered)&&registered.Type==expectedType.Name&&registered.Model==model,"Major audited relic identity/acquisition hook contract");
                    var expected=JsonNode.Parse(op.Open.GetRawText())!;expected["relics"]!.AsArray().Add(new JsonObject{["id"]=acquired,["model"]=model});
                    R(Eq(state,JsonSerializer.SerializeToElement(expected)),"Major relic acquisition complete state frame");
                }
                if(NativeMajor.IsScenario(scenario)&&op.Kind=="reward-item"&&T(op.SelectedOption,"Kind")=="GoldReward"){
                    var expected=JsonNode.Parse(op.Open.GetRawText())!;expected["gold"]=op.Open.GetProperty("gold").GetInt32()+op.SelectedOption.GetProperty("Amount").GetInt32();R(result.GetProperty("success").GetBoolean()&&Eq(state,JsonSerializer.SerializeToElement(expected)),"Major native gold amount/complete state frame");
                }
                if(NativeMajor.IsScenario(scenario)&&op.Kind=="card-menu"&&T(op.SelectedOption,"Kind")=="Card"){
                    var expected=JsonNode.Parse(op.Open.GetRawText())!;var actual=result.GetProperty("acquiredCard");var added=new JsonObject{["id"]=Ref(actual.GetProperty("reference")),["model"]=T(actual,"model"),["upgrade"]=actual.GetProperty("upgrade").GetInt32()};if(NativeNaturalBoss.IsSecondScenario(scenario)){var src=result.GetProperty("sourceCards").EnumerateArray().Single(x=>Eq(x.GetProperty("reference"),actual.GetProperty("reference")));var nativeExpected=JsonNode.Parse(src.GetProperty("native").GetRawText())!;R(src.GetProperty("runFloor").GetInt32()>=0&&Eq(src.GetProperty("runFloor"),actual.GetProperty("runFloor")),"card acquisition current run floor scope");nativeExpected["floor_added_to_deck"]=src.GetProperty("runFloor").GetInt32();R(Eq(JsonSerializer.SerializeToElement(nativeExpected),actual.GetProperty("native")),"native card source/acquired serialization with actual entry floor");added["native"]=JsonNode.Parse(actual.GetProperty("native").GetRawText());}expected["deck"]!.AsArray().Add(added);R(Eq(state,JsonSerializer.SerializeToElement(expected)),"Major card acquisition complete permanent state frame");
                }
                if(NativeMajor.IsScenario(scenario)&&op.Kind=="reward-item"&&T(op.SelectedOption,"Kind")=="PotionReward"){
                    if(NativePreparation.IsScenario(scenario)&&!result.GetProperty("success").GetBoolean())R(Slots(op.Open).All(x=>x!="")&&Eq(state,op.Open)&&result.GetProperty("acquired").ValueKind==JsonValueKind.Null,"Prepared full potion refusal complete unchanged frame");
                    else ResumePotionResult(op,state,result);
                }
                if(NativeEventFlow.IsScenario(scenario)&&kind=="EventProceed"&&op.Kind=="reward-item")ResumePotionResult(op,state,result);
                op.Returned=true;op.ReturnedState=state.Clone();op.Result=result.Clone();stack.RemoveAt(stack.Count-1);
                if(stack.Count>0)stack[^1].LastChild=state.Clone();else{R(!rootReturned,"Duplicate reward root result");rootReturned=true;lastRootReturn=state.Clone();}
            }
        }
        // Only successfully validated business boundaries may advance the task's
        // observation anchor. Metadata and the outer return cannot rewrite it.
        if(NativeEventFlow.IsScenario(scenario)&&kind=="EventProceed"&&eventResume!=""&&eventKind.StartsWith("n3c-")&&snapshot is {} resumeSnapshot)eventResumeAnchor=resumeSnapshot.Clone();
        if(eventKind=="n3c-boundary"){
            Static(p,snapshot!.Value);R(boundaryRequired&&operations.TryGetValue(T(f,"id"),out var op)&&op.Parent==lastRoot&&op.Returned&&Eq(snapshot.Value,op.ReturnedState),"Reward boundary owner/required/unique mismatch");boundaryRequired=false;
        }
        if(eventKind=="n3c-assertion"){
            Static(p,snapshot!.Value);R(!asserted&&fixtureBefore.ValueKind==JsonValueKind.Object&&T(f,"name")==scenario&&f.GetProperty("goldBefore").GetInt32()==fixtureBefore.GetProperty("gold").GetInt32()&&f.GetProperty("deckBefore").GetInt32()==fixtureBefore.GetProperty("deck").GetArrayLength(),"Reward fixture assertion source mismatch");asserted=true;
            var state=snapshot.Value;int gold=state.GetProperty("gold").GetInt32(),beforeGold=fixtureBefore.GetProperty("gold").GetInt32();
            if(scenario is "reward-gold" or "reward-required")R(gold-beforeGold==7,"Independent custom gold 7 contract");
            if(scenario is "reward-zero" or "reward-empty" or "reward-skip" or "reward-card-skip")R(gold==beforeGold,"Independent no-gold contract");
            if(scenario=="reward-consecutive")R(gold-beforeGold==18&&sets-fixtureSets==2,"Independent consecutive gold 7+11 contract");
            if(scenario=="reward-card")R(state.GetProperty("deck").GetArrayLength()==fixtureBefore.GetProperty("deck").GetArrayLength()+1&&state.GetProperty("deck")[state.GetProperty("deck").GetArrayLength()-1].GetProperty("model").GetString()=="CARD.DEFEND_IRONCLAD"&&state.GetProperty("deck")[state.GetProperty("deck").GetArrayLength()-1].GetProperty("upgrade").GetInt32()==0,"Specified nonzero unupgraded native card acquisition");
            if(scenario is "reward-card" or "reward-card-skip")R(Eq(JsonSerializer.SerializeToElement(state.GetProperty("deck").EnumerateArray().Take(fixtureBefore.GetProperty("deck").GetArrayLength()).ToArray()),fixtureBefore.GetProperty("deck"))&&(scenario!="reward-card-skip"||state.GetProperty("deck").GetArrayLength()==fixtureBefore.GetProperty("deck").GetArrayLength()),"Reward permanent deck content/upgrade/reference changed");
            if(scenario=="reward-nested")R(nestedSets>fixtureNested,"Missing actual native nested Offer");
            if(scenario=="reward-relic")R(state.GetProperty("powers").EnumerateArray().Any(x=>T(x,"model")=="POWER.STRENGTH_POWER"&&x.GetProperty("amount").GetDecimal()==1),"Independent Vajra start-room Strength 1 contract");
            if(scenario is "reward-fire" or "reward-block")R(potionUses-fixtureUses==1,"Missing native potion use");
            if(scenario=="reward-treasure")R(treasureObtains==1,"Missing native treasure relic acquisition");
            if(scenario=="reward-treasure-skip")R(treasureObtains==0,"Skipped treasure acquired relic");
            if(scenario=="reward-repeat-treasure"){
                var rooms=chests.Values.ToArray();R(rooms.Length==2&&rooms.All(x=>x.chestOpen&&x.normalChest&&x.extraChest&&x.chestPicked&&x.chestExited&&x.exitReturned)&&!rooms[0].treasureSkipped&&rooms[0].Obtains==1&&rooms[1].treasureSkipped&&rooms[1].Obtains==0&&treasureObtains==1&&T(state,"room")=="RestSiteRoom","Two independent native chest lifecycles take then skip");
            }
            if(scenario is "reward-treasure" or "reward-treasure-skip")R(normalChest&&extraChest&&chestOpen&&chestPicked&&chestExited,"Incomplete native treasure lifecycle");
            if(scenario=="reward-capacity")R(capacityFailures-fixtureCapacityFailures==1&&discards-fixtureDiscards==1&&state.GetProperty("inventory").GetProperty("capacity").GetInt32()==3&&Slots(state).All(x=>x!="")&&state.GetProperty("potions").EnumerateArray().Any(x=>T(x,"model")=="POTION.FIRE_POTION"),"Independent full capacity/discard/retry contract");
        }
        if(eventKind=="n3c-card-continuity"){
            Static(p,snapshot!.Value);R(!cardContinuity&&acquiredCards.Count>0&&f.GetProperty("permanent").EnumerateArray().Select(Ref).ToHashSet().SetEquals(acquiredCards),"Reward card continuity source mismatch");
            var state=snapshot.Value;R(state.GetProperty("combat").GetProperty("phase").GetString()=="Play"&&acquiredCards.All(id=>Contains(state,"deck",id)&&new[]{"hand","draw","discard","exhaust"}.SelectMany(k=>state.GetProperty("combat").GetProperty(k).EnumerateArray()).Any(c=>c.GetProperty("deck").GetString()==id)),"Reward permanent/next combat copy mismatch");cardContinuity=true;
        }
        if(eventKind=="n3c-contract"){Static(p,snapshot!.Value);R(!contract&&!boundaryRequired&&stack.Count==0&&T(f,"name")==scenario&&f.GetProperty("complete").GetBoolean()&&(scenario=="rewards-natural"?potionUses==1&&sets>=1&&cardContinuity:scenario=="closure-neow"?cardContinuity&&sets>=1:NativeSolverRun.IsScenario(scenario)||NativeEventFlow.IsScenario(scenario)||NativeMajor.IsScenario(scenario)?sets>=1:asserted)&&(scenario!="reward-card"||cardContinuity),"Incomplete reward scenario contract");contract=true;}
    }
    internal void FinishMajorDeathPrefix()=>R(NativeMajor.IsScenario(scenario)&&!boundaryRequired&&stack.Count==0&&root==""&&roots>0&&operations.Values.All(x=>x.Returned),"Unclosed reward prefix at certified major death");
    internal void Finish()=>R(chests.Values.All(x=>x.chestPicked&&x.chestExited)&&contract&&!boundaryRequired&&stack.Count==0&&root==""&&roots>0&&operations.Values.All(x=>x.Returned),"Unclosed reward operations/contract");
}
