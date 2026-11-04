using System.Text.Json;
using System.Text.Json.Nodes;
namespace OfflineSearchHarness;
// Explicit finite current event lifecycle, checked independently of fixture assertions.
internal sealed class NativeEventFlowContract
{
 internal static readonly HashSet<string> SnapshotKinds=["ef-enable","ef-parent","ef-subcombat","ef-battle-complete","ef-proceed-start","ef-room-resume-return","ef-resume-start","ef-resume-return","ef-resume-fault","ef-proceed-return","ef-complete"];
 readonly NativeRoomContract prefix=new();readonly NativeRewardContract rewards=new();
 readonly Dictionary<string,(string Type,string? Model)> identities=[];
 string scenario="",active="",ctx="",kind="",lastRoot="",lastCtx="";
 JsonElement rootDecision,dispatch,confirmed,parent,eventModel,child,resumeStart,resumeState,proceedStart,proceedState,parentState;
 bool parentExited,enabled,complete,subcombat,battle,proceed,outerReturned,resumeReturned,proceedReturned;
 string resume="",lastKind="",session="";int parentDepth,priorVictories,battleCallbacks;
 JsonElement optionState,optionFacts,optionReturn,optionReturnFacts,roomRef,exitReturn,hollowBefore;
 bool optionStarted,optionReturned,optionConfirmed,optionBoundary=true,leaveReady,exiting,exitReturned,finalized,hollowDone;
 string optionRequest="",optionRoot="",optionCtx="",exitRoot="",exitCtx="";
 readonly HashSet<string> sessions=[],hollowPotions=[],usedPotions=[];
 readonly HashSet<string> optionRequests=[],arrivals=[],victoryRoots=[];
 bool arrivalPending,victoryPending,victoryConfirmed,eventRewardsStarted,eventRewardsConfirmed,eventRewardsClosed,exitObserved;
 string arrivalRoot="",arrivalCtx="",victoryRoot="",victoryCtx="",cleanupRoot="",rewardsRoot="",rewardsCtx="";
 JsonElement arrivalRoom,victoryRoom;
 static string T(JsonElement e,string k)=>e.GetProperty(k).GetString()!;
 static bool Eq(JsonElement a,JsonElement b)=>SimulatorLab.Core.NativeDecisionTicket.SameActionJson(a.GetRawText(),b.GetRawText());
 static void R([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool b,string m){if(!b)throw new NativeDataException("EventFlow "+m);}
 void Static(JsonElement p,JsonElement s)=>R(active==""&&T(p,"root")==lastRoot&&T(p,"context")==lastCtx&&Eq(s,confirmed),"static complete confirmed state binding");
 internal void Accept(JsonElement p,JsonElement? snapshot){
  string k=T(p,"kind");var d=p.GetProperty("detail");var f=d.TryGetProperty("facts",out var ff)?ff:default;
  if(k=="header")scenario=T(d,"scenario");
  if(k=="identity")identities.Add(T(d,"id"),(T(d,"type"),d.GetProperty("model").GetString()));
  if(snapshot is {} actual){
   if(actual.GetProperty("room").ValueKind==JsonValueKind.Null)R(actual.GetProperty("rooms").ValueKind==JsonValueKind.Null,"room null projection");
   else {var rs=actual.GetProperty("rooms");R(identities.TryGetValue(T(rs.GetProperty("reference"),"$ref"),out var ri)&&ri.Type==T(actual,"room")&&rs.GetProperty("stack").GetInt32()>0&&rs.GetProperty("map").GetInt32()==actual.GetProperty("lifecycle").GetProperty("mapGeneration").GetInt32(),"snapshot native room identity/type/map/stack");
    var em=actual.GetProperty("lifecycle").GetProperty("eventModel");
    if(T(actual,"room")=="EventRoom"){
     R(em.ValueKind==JsonValueKind.Object&&identities.TryGetValue(T(em.GetProperty("reference"),"$ref"),out var ei)&&ei.Model==T(em,"model"),"snapshot actual event model identity");var rng=em.GetProperty("rng");R(rng.GetProperty("counter").GetInt64()>=0,"event RNG counter");foreach(var word in new[]{"s0","s1","s2","s3"})_=rng.GetProperty(word).GetUInt64();
    }else R(em.ValueKind==JsonValueKind.Null,"event model outside event room");
   }
  }
  if(k is "header" or "identity")rewards.Accept(p,snapshot);
  if(!enabled&&scenario=="event-natural"&&k!="ef-enable")prefix.Accept(p,snapshot);
  if(enabled&&k is not ("header" or "identity"))rewards.Accept(p,snapshot);
  if(k=="root-request")R(optionBoundary&&!exiting&&!arrivalPending&&!victoryPending,"unfinished choice/exit/arrival/cleanup before new root");
  if(k=="dispatch"){
   R(active==""&&!complete,"dispatch lifecycle");if(enabled)R(Eq(snapshot!.Value,confirmed),"dispatch prior confirmed complete state continuity");active=T(p,"root");ctx=T(p,"context");kind=T(f,"kind");rootDecision=f.GetProperty("decision").Clone();dispatch=snapshot!.Value.Clone();
   if(enabled&&kind=="RoomOption"){
    R(optionRequest!=""&&T(rootDecision,"session")==session&&T(rootDecision,"request")==optionRequest&&Eq(dispatch,optionState),"choice decision current request/state");
    var selected=optionFacts.GetProperty("options").EnumerateArray().SingleOrDefault(o=>o.GetProperty("index").GetInt32()==rootDecision.GetProperty("index").GetInt32());
    R(selected.ValueKind==JsonValueKind.Object&&T(selected,"key")==T(rootDecision,"key")&&Eq(selected.GetProperty("reference"),rootDecision.GetProperty("option"))&&selected.GetProperty("enabled").GetBoolean()&&selected.GetProperty("supported").GetBoolean(),"choice decision actual supported candidate");
   }
   if(enabled&&kind=="EventRewards"){
    R(battle&&!eventRewardsStarted&&cleanupRoot==lastRoot&&Eq(child,dispatch.GetProperty("rooms").GetProperty("reference")),"EventRewards requires current child battle cleanup");eventRewardsStarted=true;rewardsRoot=active;rewardsCtx=ctx;
   }
   if(enabled&&kind=="EventProceed")R(eventRewardsClosed&&Eq(rootDecision.GetProperty("parent"),parent)&&Eq(rootDecision.GetProperty("child"),child),"Proceed before native reward boundary or wrong root room identities");
   if(enabled&&kind=="EventSetup")R(T(rootDecision,"model")=="EVENT.BATTLEWORN_DUMMY","fixture root/actual event model");
  }
  if(k=="confirmed"){R(active==T(p,"root")&&ctx==T(p,"context"),"confirmation owner");if(kind=="EventProceed")R(proceedReturned&&Eq(snapshot!.Value,proceedState),"confirmation before actual Resume completion/full state");if(kind=="RoomOption"){R(optionReturned&&!optionConfirmed&&optionRoot==active&&optionCtx==ctx&&Eq(snapshot!.Value,optionReturn),"option confirmation complete return binding");optionConfirmed=true;}
   if(kind=="RoomExit"||kind=="Travel"&&exitRoot==active)R(exitReturned&&Eq(snapshot!.Value,exitReturn),"exit confirmation return binding");
   if(victoryPending&&active==victoryRoot){R(ctx==victoryCtx,"victory confirmation owner");victoryConfirmed=true;}
   if(kind=="EventRewards"){R(eventRewardsStarted&&active==rewardsRoot&&ctx==rewardsCtx&&!eventRewardsConfirmed,"EventRewards confirmation owner/unique");eventRewardsConfirmed=true;}
   if(kind is "Travel" or "EventSetup"){
    R(!arrivalPending,"unobserved previous room arrival");arrivalPending=true;arrivalRoot=active;arrivalCtx=ctx;arrivalRoom=snapshot!.Value.GetProperty("rooms").GetProperty("reference").Clone();
   }
   confirmed=snapshot!.Value.Clone();lastRoot=active;lastCtx=ctx;lastKind=kind;active="";}
  if(k=="ef-enable"){
   R(!enabled&&active==""&&T(p,"root")==lastRoot&&T(p,"context")==lastCtx&&Eq(f.GetProperty("before"),confirmed),"scope entry confirmed anchor");var s=JsonNode.Parse(snapshot!.Value.GetRawText())!;R(s.AsObject().Remove("inventory")&&Eq(JsonSerializer.SerializeToElement(s),confirmed),"scope may only add inventory projection");R(T(snapshot.Value.GetProperty("lifecycle").GetProperty("eventModel"),"model")=="EVENT.WHISPERING_HOLLOW"&&!snapshot.Value.GetProperty("lifecycle").GetProperty("eventModel").GetProperty("IsFinished").GetBoolean(),"declared natural Hollow scope entry");
   hollowBefore=snapshot.Value.Clone();confirmed=snapshot.Value.Clone();enabled=true;
  }
  if(k=="room-entered"){
   Static(p,snapshot!.Value);R(arrivalPending&&arrivalRoot==lastRoot&&arrivalCtx==lastCtx&&Eq(arrivalRoom,f.GetProperty("room"))&&arrivals.Add(lastRoot+":"+T(arrivalRoom,"$ref")),"required unique arrival/current action/room");arrivalPending=false;
   R(sessions.Add(T(f,"session")),"unique room session");session=T(f,"session");roomRef=f.GetProperty("room").Clone();R(Eq(roomRef,snapshot.Value.GetProperty("rooms").GetProperty("reference"))&&T(f,"type")==T(snapshot.Value,"room"),"room entered actual reference/type");leaveReady=exiting=exitReturned=finalized=exitObserved=false;
  }
  if(k.StartsWith("room-")&&f.TryGetProperty("session",out var ss)){R(ss.GetString()==session,"room session owner");if(f.TryGetProperty("room",out var rr))R(Eq(rr,roomRef),"room operation actual reference");}
  if(k=="room-candidates"){
   Static(p,snapshot!.Value);R(optionBoundary&&!leaveReady&&optionRequests.Add(T(f,"request")),"candidates lifecycle/unique request");optionRequest=T(f,"request");optionFacts=f.Clone();optionState=snapshot.Value.Clone();
   var native=snapshot.Value.GetProperty("rooms").GetProperty(T(snapshot.Value,"room")=="EventRoom"?"eventOptions":"rest");var opts=f.GetProperty("options");R(opts.GetArrayLength()==native.GetArrayLength(),"complete option inventory");
   for(int i=0;i<opts.GetArrayLength();i++){var a=opts[i];var b=native[i];string model=T(snapshot.Value,"room")=="EventRoom"?T(snapshot.Value.GetProperty("lifecycle").GetProperty("eventModel"),"model"):"";bool supported=model is "EVENT.WHISPERING_HOLLOW" or "EVENT.BATTLEWORN_DUMMY" or "EVENT.DENSE_VEGETATION"?i==0:model=="EVENT.SUNKEN_STATUE"?i==1:true;R(a.GetProperty("supported").GetBoolean()==supported,"declared finite option support");R(a.GetProperty("index").GetInt32()==i&&T(a,"handle")=="o"+i&&Eq(a.GetProperty("reference"),b.GetProperty("reference"))&&T(a,"key")==T(b,T(snapshot.Value,"room")=="EventRoom"?"TextKey":"id")&&a.GetProperty("enabled").GetBoolean()==(T(snapshot.Value,"room")=="EventRoom"?!b.GetProperty("IsLocked").GetBoolean():b.GetProperty("enabled").GetBoolean()),"option source/index/enabled");}
  }
  if(k=="room-option-start"){
   R(active==T(p,"root")&&ctx==T(p,"context")&&kind=="RoomOption"&&optionBoundary&&T(f,"request")==optionRequest&&Eq(snapshot!.Value,dispatch)&&Eq(snapshot.Value,optionState),"option dispatch source state");
   R(T(rootDecision,"session")==session&&T(rootDecision,"request")==T(f,"request")&&T(rootDecision,"key")==T(f,"key")&&Eq(rootDecision.GetProperty("option"),f.GetProperty("option")),"option execution/actual root decision");
   var candidate=optionFacts.GetProperty("options").EnumerateArray().SingleOrDefault(o=>Eq(o.GetProperty("reference"),f.GetProperty("option"))&&T(o,"key")==T(f,"key"));
   R(candidate.ValueKind==JsonValueKind.Object&&candidate.GetProperty("enabled").GetBoolean()&&candidate.GetProperty("supported").GetBoolean(),"option actual candidate");optionStarted=true;optionReturned=optionConfirmed=false;optionBoundary=false;optionRoot=active;optionCtx=ctx;
  }
  if(k=="room-option-return"){
   R(optionStarted&&!optionReturned&&active==optionRoot&&ctx==optionCtx&&T(p,"root")==active&&T(p,"context")==ctx&&T(f,"request")==optionRequest,"option unique result owner");optionReturned=true;optionReturn=snapshot!.Value.Clone();optionReturnFacts=f.Clone();
   if(enabled){
    R(f.GetProperty("success").ValueKind==JsonValueKind.Null,"finite event option has no Rest success result");
    var em=optionState.GetProperty("lifecycle").GetProperty("eventModel");bool actualFinished=T(em,"model")=="EVENT.BATTLEWORN_DUMMY"?false:snapshot.Value.GetProperty("lifecycle").GetProperty("eventModel").GetProperty("IsFinished").GetBoolean();
    R(f.GetProperty("eventFinished").GetBoolean()==actualFinished,"option completion fact/actual native state");
   }
   if(enabled&&!hollowDone&&T(hollowBefore.GetProperty("lifecycle").GetProperty("eventModel"),"model")=="EVENT.WHISPERING_HOLLOW"){
    int cost=hollowBefore.GetProperty("gold").GetInt32()-snapshot.Value.GetProperty("gold").GetInt32();var rng=hollowBefore.GetProperty("lifecycle").GetProperty("eventModel").GetProperty("rng");
    R(rng.GetProperty("counter").GetInt64()==1,"Hollow independent one native gold roll");ulong y=rng.GetProperty("s2").GetUInt64()^rng.GetProperty("s1").GetUInt64();ulong previousB=y^(y<<17)^(y<<34)^(y<<51);ulong output=unchecked(System.Numerics.BitOperations.RotateLeft(previousB*5,7)*9);int expectedCost=26+(int)(((ulong)(uint)(output>>32)*19)>>32);
    R(cost==expectedCost&&Eq(hollowBefore.GetProperty("hp"),snapshot.Value.GetProperty("hp"))&&Eq(hollowBefore.GetProperty("deck"),snapshot.Value.GetProperty("deck")),"independent Hollow cost range/HP/deck");
    R(hollowBefore.GetProperty("potions").GetArrayLength()==0&&snapshot.Value.GetProperty("potions").GetArrayLength()==2&&snapshot.Value.GetProperty("lifecycle").GetProperty("eventModel").GetProperty("IsFinished").GetBoolean(),"Hollow two actual acquisitions/event finished");foreach(var potion in snapshot.Value.GetProperty("potions").EnumerateArray())R(hollowPotions.Add(T(potion,"id")),"Hollow unique potion");hollowDone=true;
   }
  }
  if(k=="room-option-boundary"){Static(p,snapshot!.Value);R(optionReturned&&optionConfirmed&&!optionBoundary&&optionRoot==lastRoot&&optionCtx==lastCtx&&T(f,"request")==optionRequest&&Eq(f.GetProperty("success"),optionReturnFacts.GetProperty("success"))&&Eq(f.GetProperty("eventFinished"),optionReturnFacts.GetProperty("eventFinished"))&&Eq(snapshot.Value,optionReturn),"option required unique boundary/results");optionBoundary=true;}
  if(k=="room-leave-ready"){Static(p,snapshot!.Value);R(optionBoundary&&!leaveReady,"leave ready lifecycle");if(T(snapshot.Value,"room")=="EventRoom")R(snapshot.Value.GetProperty("lifecycle").GetProperty("eventModel").GetProperty("IsFinished").GetBoolean(),"unfinished event leave");leaveReady=true;}
  if(k=="room-exit-start"){R(leaveReady&&!exiting&&!exitReturned&&active==T(p,"root")&&ctx==T(p,"context")&&kind is "Travel" or "RoomExit"&&Eq(snapshot!.Value,dispatch),"exit start complete dispatch binding");exiting=true;exitRoot=active;exitCtx=ctx;}
  if(k=="room-exit-return"){if(kind=="Travel")R(T(rootDecision,"from")==T(dispatch,"node")&&T(rootDecision,"to")==T(snapshot!.Value,"node"),"actual Travel from/to state binding");R(exiting&&!exitReturned&&exitRoot==active&&exitCtx==ctx&&T(p,"root")==active&&T(p,"context")==ctx,"exit unique return owner");if(kind=="RoomExit")R(Eq(snapshot!.Value,dispatch),"finite event Exit full static state");exiting=false;exitReturned=true;exitReturn=snapshot!.Value.Clone();}
  if(k is "room-exited" or "room-complete"){Static(p,snapshot!.Value);R(exitReturned&&exitRoot==lastRoot&&exitCtx==lastCtx&&!exiting&&!finalized,"exit static result/owner");if(k=="room-exited"){R(!exitObserved,"unique room-exited observation");exitObserved=true;}if(k=="room-complete"){finalized=true;if(parent.ValueKind==JsonValueKind.Object&&Eq(roomRef,parent))parentExited=true;}}
  if(k=="n3c-return"&&f.GetProperty("result").TryGetProperty("consumed",out var consumed)){R(hollowPotions.Contains(T(consumed,"$ref"))&&usedPotions.Add(T(consumed,"$ref")),"only actual earned potions consumed once");}
  if(k=="ef-parent"){Static(p,snapshot!.Value);R(parent.ValueKind==JsonValueKind.Undefined&&T(f,"model")=="EVENT.BATTLEWORN_DUMMY"&&T(snapshot!.Value.GetProperty("lifecycle").GetProperty("eventModel"),"model")=="EVENT.BATTLEWORN_DUMMY","declared parent event");parent=f.GetProperty("parent").Clone();eventModel=f.GetProperty("eventModel").Clone();parentState=snapshot.Value.Clone();parentDepth=snapshot.Value.GetProperty("rooms").GetProperty("stack").GetInt32();R(Eq(parent,snapshot.Value.GetProperty("rooms").GetProperty("reference"))&&Eq(eventModel,snapshot.Value.GetProperty("lifecycle").GetProperty("eventModel").GetProperty("reference")),"parent actual identity");}
  if(k=="battle-victory-before-cleanup"){
   R(active==T(p,"root")&&ctx==T(p,"context")&&kind is "PlayCard" or "EndTurn" or "UsePotion"&&!victoryPending&&victoryRoots.Add(active)&&T(snapshot!.Value,"room")=="CombatRoom"&&Eq(dispatch.GetProperty("rooms").GetProperty("reference"),snapshot.Value.GetProperty("rooms").GetProperty("reference")),"victory actual battle/root owner/unique");
   victoryPending=true;victoryConfirmed=false;victoryRoot=active;victoryCtx=ctx;victoryRoom=snapshot.Value.GetProperty("rooms").GetProperty("reference").Clone();battleCallbacks++;
  }
  if(k=="battle-victory-cleanup"){
   Static(p,snapshot!.Value);R(victoryPending&&victoryConfirmed&&victoryRoot==lastRoot&&victoryCtx==lastCtx&&Eq(victoryRoom,snapshot.Value.GetProperty("rooms").GetProperty("reference"))&&f.GetProperty("historyEntries").GetInt32()==0,"required battle cleanup owner/order/full confirmed state");victoryPending=false;cleanupRoot=lastRoot;
  }
  if(k=="n3c-boundary"&&eventRewardsConfirmed&&lastKind=="EventRewards"){
   Static(p,snapshot!.Value);R(!eventRewardsClosed&&lastRoot==rewardsRoot&&lastCtx==rewardsCtx,"EventRewards boundary owner/unique");eventRewardsClosed=true;
  }
  if(k=="ef-subcombat"){Static(p,snapshot!.Value);R(!subcombat&&Eq(parent,f.GetProperty("parent"))&&Eq(eventModel,f.GetProperty("eventModel"))&&T(f,"encounter")=="ENCOUNTER.BATTLEWORN_DUMMY_EVENT_V1_ENCOUNTER"&&f.GetProperty("ShouldResumeParentEventAfterCombat").GetBoolean()&&T(f.GetProperty("ParentEventId"),"Category")=="EVENT"&&T(f.GetProperty("ParentEventId"),"Entry")=="BATTLEWORN_DUMMY","subcombat source/parent");child=f.GetProperty("child").Clone();R(T(snapshot.Value,"room")=="CombatRoom"&&Eq(child,snapshot.Value.GetProperty("rooms").GetProperty("reference"))&&snapshot.Value.GetProperty("rooms").GetProperty("stack").GetInt32()==parentDepth+1,"subcombat actual stack");priorVictories=battleCallbacks;subcombat=true;}
  if(k=="ef-battle-complete"){Static(p,snapshot!.Value);R(subcombat&&!battle&&!victoryPending&&cleanupRoot==lastRoot&&Eq(child,f.GetProperty("child"))&&f.GetProperty("victories").GetInt32()==priorVictories+1&&battleCallbacks==priorVictories+1&&lastKind=="PlayCard"&&!f.GetProperty("timedOut").GetBoolean(),"actual battle prerequisite");battle=true;}
  if(k=="ef-proceed-start"){R(active==T(p,"root")&&ctx==T(p,"context")&&kind=="EventProceed"&&battle&&!proceed&&Eq(dispatch,snapshot!.Value)&&Eq(parent,f.GetProperty("parent"))&&Eq(child,f.GetProperty("child")),"proceed dispatch/parent/child");proceedStart=snapshot.Value.Clone();proceed=true;}
  if(k=="ef-resume-start"){
   R(proceed&&!resumeReturned&&resume==""&&active==T(p,"root")&&ctx==T(p,"context")&&kind=="EventProceed"&&Eq(eventModel,f.GetProperty("eventModel"))&&Eq(child,f.GetProperty("exited")),"actual Resume owner/unique/source");
   R(Eq(parent,snapshot!.Value.GetProperty("rooms").GetProperty("reference"))&&snapshot.Value.GetProperty("rooms").GetProperty("stack").GetInt32()==parentDepth,"actual Resume parent stack");var expected=JsonNode.Parse(proceedStart.GetRawText())!;expected["room"]="EventRoom";expected["combat"]!["round"]=null;expected["combat"]!["side"]=null;expected["combat"]!["enemies"]=null;expected["rooms"]=JsonNode.Parse(parentState.GetProperty("rooms").GetRawText());expected["lifecycle"]!["eventModel"]=JsonNode.Parse(parentState.GetProperty("lifecycle").GetProperty("eventModel").GetRawText());
   R(Eq(snapshot.Value,JsonSerializer.SerializeToElement(expected)),"native child exit/actual Resume start full state binding");resume=T(f,"id");resumeStart=f.Clone();
  }
  if(k=="ef-resume-return"){R(resume!=""&&!resumeReturned&&active==T(p,"root")&&ctx==T(p,"context")&&Eq(f,resumeStart)&&snapshot!.Value.GetProperty("lifecycle").GetProperty("eventModel").GetProperty("IsFinished").GetBoolean(),"actual Resume complete/unique/content");resumeReturned=true;resumeState=snapshot.Value.Clone();}
  if(k=="ef-room-resume-return"){R(proceed&&resume!=""&&!outerReturned&&active==T(p,"root")&&ctx==T(p,"context")&&Eq(parent,f.GetProperty("parent"))&&Eq(child,f.GetProperty("child")),"outer Resume owner/order");if(resumeReturned)R(Eq(snapshot!.Value,resumeState),"outer return/actual completed full state");outerReturned=true;}
  if(k=="ef-proceed-return"){R(proceed&&resumeReturned&&outerReturned&&!proceedReturned&&active==T(p,"root")&&ctx==T(p,"context")&&Eq(snapshot!.Value,resumeState)&&Eq(parent,f.GetProperty("parent"))&&Eq(child,f.GetProperty("child")),"proceed after actual task full state");proceedReturned=true;proceedState=snapshot.Value.Clone();}
  if(k=="ef-complete"){Static(p,snapshot!.Value);R(enabled&&hollowDone&&optionBoundary&&!complete&&!arrivalPending&&!victoryPending&&arrivals.Contains(lastRoot+":"+T(snapshot.Value.GetProperty("rooms").GetProperty("reference"),"$ref"))&&T(f,"goal")== (scenario=="event-natural"?"natural hollow and next Play":"actual event combat reward resume and legal next Play"),"unique complete/required arrival and cleanup");if(scenario=="event-natural")R(T(snapshot.Value,"room")=="CombatRoom"&&snapshot.Value.GetProperty("combat").GetProperty("phase").GetString()=="Play","natural next decision");else {
   R(proceedReturned&&parentExited&&lastKind=="Travel"&&Eq(parent,f.GetProperty("parent"))&&T(snapshot.Value,"room")=="CombatRoom"&&T(snapshot.Value.GetProperty("combat"),"phase")=="Play"&&Eq(snapshot.Value.GetProperty("deck"),proceedState.GetProperty("deck")),"actual restored event/next Play/deck continuity");
   var copies=new[]{"hand","draw","discard","exhaust"}.SelectMany(x=>snapshot.Value.GetProperty("combat").GetProperty(x).EnumerateArray()).ToArray();var deck=snapshot.Value.GetProperty("deck").EnumerateArray().ToArray();
   R(copies.Length==deck.Length&&deck.All(card=>copies.Count(c=>c.GetProperty("deck").GetString()==T(card,"id")&&T(c,"model")==T(card,"model")&&c.GetProperty("upgrade").GetInt32()==card.GetProperty("upgrade").GetInt32())==1),"next battle every permanent DeckVersion copy/content");
  }complete=true;}
 }
 internal void Finish(){R(enabled&&complete&&active==""&&!arrivalPending&&!victoryPending,"required event lifecycle not complete");rewards.Finish();}
}
