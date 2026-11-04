using System.Text.Json;
using System.Text.Json.Nodes;
namespace OfflineSearchHarness;

// Finite current-room authority. This profile explicitly owns its reward, death
// and act-task contracts instead of inheriting unrelated fixture completion gates.
internal sealed class NativeMajorContract
{
 internal static readonly HashSet<string> SnapshotKinds=["natural-event-declined","natural-third-boss-complete","natural-third-ancient-selected","natural-third-first-complete","natural-elite-complete","natural-act-scope","natural-act2-complete","natural-ancient-selected","natural-start-candidate","natural-first-node","major-victory-condition","major-plan","major-battle-start","major-battle-result","major-rewards-start","major-rewards-return","major-ready-start","major-ready-return","major-complete","prep-chest-exit-start","prep-chest-exit-return"];
 readonly NativeRewardContract rewards=new();
 readonly NativeDeathContract deaths=new();
 bool NaturalBoss=>NativeNaturalBoss.IsScenario(scenario);
 bool BossGoal=>NaturalBoss&&!(NativeNaturalBoss.IsElite2(scenario)&&actScope.Index==1)||scenario is "major-boss" or "major-prepared-boss";
 string[] bossPool=[];
 readonly Dictionary<string,(string Type,JsonElement Model)> identities=[];HashSet<string> actPriorIdentities=[];
 readonly HashSet<string> actionIdentities=[];
  JsonElement victoryState,actInput,historyFacts;bool victoryAnchored,victoryHistoryCleared;int victoryEpoch,victoryIndex;string victoryRoot="",victoryContext="";
 static JsonElement Element(JsonNode n)=>JsonSerializer.SerializeToElement(n);
 static void VictoryFacts(JsonElement state){
  R(state.GetProperty("hp").GetInt32()>0,"victory requires surviving player");
  R(state.GetProperty("majorCombat").GetProperty("inProgress").GetBoolean()&&state.GetProperty("combat").GetProperty("enemies").ValueKind==JsonValueKind.Array,"victory requires native combat before cleanup");
  foreach(var e in state.GetProperty("combat").GetProperty("enemies").EnumerateArray())R(e.TryGetProperty("primary",out var primary)&&e.TryGetProperty("alive",out var alive)&&(!primary.GetBoolean()||!alive.GetBoolean())&&(!primary.GetBoolean()||e.GetProperty("hp").GetInt32()<=0),"victory requires no living primary enemies before cleanup");
 }
 string scenario="",mode="",source="",root="",ctx="",kind="",lastRoot="",lastCtx="",combatRoot="",combatCtx="",arrivalRoot="",historyRoot="";
 JsonElement confirmed,dispatch,decision,room,battle,cleanup,rewardState,actState,mapCandidates,mapState,lossInput,lossOutput,lastHistory,fixtureState,fixtureRoom,combatLocation;
 string combatLocationRoot="";
 // Finite profile actions are validated against the observed lifecycle before
 // their kind may select a location exemption. Matching three self-reported
 // action copies alone is not authority to enter a different phase.
 enum ActionKind { EnterAct,EventOption,Travel,MajorFixture,PlayCard,EndTurn,UsePotion,RoomOption,RewardOffer,ActReady,TreasureOpen,TreasurePick,NextActStart,ThirdShop }
 JsonElement boot,openingCandidates,openingState,chestExitState;bool chestExitStarted,chestExitReturned;int dispatchedActions;
 bool enteredAct,openingSelected,openingDone,combatAction;
 readonly List<JsonElement> transitionHistories=[];string transitionRoot="";
 bool fixtureDeclared,fixtureDispatched,fixtureArrived;
 HashSet<string>? allowedTop;readonly HashSet<string> roomSessions=[],choiceRequests=[];JsonElement sessionRoom;string roomSession="";bool haveRoom,leaveReady;int platforms;
 bool planned,arrival,opened,deathObserved,battleStarted,won,victoryConfirmed,cleaned,result,rewardStarted,rewardReturned,complete,lossStarted,lossReturned,stamped,closed,terminalContract;
 JsonElement choiceCandidates,choiceState,choiceResult,choiceResultFacts;string choiceRequest="",choiceSession="";bool choiceStarted,choiceReturned,choiceConfirmed,choiceClosed=true;
 readonly NativeStageLedger stages=new();
 JsonElement nextStartState;
 readonly NativeNaturalMapContract naturalMaps=new();
 int battles,majorResults,act,actMap,actTaskStarts,actTaskReturns;string lossRoot="";
 sealed class ActScope(int index) { internal readonly int Index=index; internal string Target=""; internal int TargetResults; internal bool readyStarted,actStarted,actReturned,readyReturned,ancientSelected,secondBattleWon,nextCandidate,nextStart,nextFirst; }
 readonly Dictionary<int,ActScope> actScopes=[];ActScope actScope=new(0);
 string target {get=>actScope.Target;set=>actScope.Target=value;}
 int targetResults {get=>actScope.TargetResults;set=>actScope.TargetResults=value;}
 bool readyStarted {get=>actScope.readyStarted;set=>actScope.readyStarted=value;}
 bool actStarted {get=>actScope.actStarted;set=>actScope.actStarted=value;}
 bool actReturned {get=>actScope.actReturned;set=>actScope.actReturned=value;}
 bool readyReturned {get=>actScope.readyReturned;set=>actScope.readyReturned=value;}
 bool ancientSelected {get=>actScope.ancientSelected;set=>actScope.ancientSelected=value;}
 bool secondBattleWon {get=>actScope.secondBattleWon;set=>actScope.secondBattleWon=value;}
 bool nextCandidate {get=>actScope.nextCandidate;set=>actScope.nextCandidate=value;}
 bool nextStart {get=>actScope.nextStart;set=>actScope.nextStart=value;}
 bool nextFirst {get=>actScope.nextFirst;set=>actScope.nextFirst=value;}
 static string T(JsonElement e,string p)=>e.GetProperty(p).GetString()!;
 static bool Eq(JsonElement a,JsonElement b)=>NativeBoundaryContract.Equal(a,b);
 static void R([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool v,string m){if(!v)throw new NativeDataException("Major "+m);}
 void Inside(JsonElement p)=>NativeBoundaryContract.Owner(p,root,ctx,root!="","Major active action owner");
 void Static(JsonElement p,JsonElement s,bool closing=false){NativeBoundaryContract.Owner(p,lastRoot,lastCtx,root=="","Major static action owner");NativeBoundaryContract.Snapshot(s,confirmed,"Major complete immutable confirmed state",closing);}
 void Combat(JsonElement s)=>NativeBoundaryContract.Room(s,room,"CombatRoom","Major concrete combat room");
 static JsonElement Location(JsonElement s)=>JsonSerializer.SerializeToElement(new{act=s.GetProperty("act"),node=s.GetProperty("node"),room=s.GetProperty("rooms").GetProperty("reference")});
 void CurrentCombat(JsonElement s){
  R(combatLocationRoot==root&&Eq(Location(s),combatLocation),"immutable combat action location");
  Combat(s);var actual=s.GetProperty("majorCombat");var initial=battle.GetProperty("majorCombat");
  foreach(var field in new[]{"room","encounter","type","shouldGiveRewards"})R(Eq(actual.GetProperty(field),initial.GetProperty(field)),"current combat immutable identity "+field);
 }
 static JsonElement ArrivalCombat(JsonElement s){
  // These are the only mutable fields of the pinned majorCombat projection.
  // Compare all remaining identity fields structurally, including extensions.
  var identity=JsonNode.Parse(s.GetProperty("majorCombat").GetRawText())!.AsObject();
  identity.Remove("inProgress");identity.Remove("intentAttackers");
  return JsonSerializer.SerializeToElement(new{location=Location(s),combat=identity});
 }
 void TransitionLocation(JsonElement s){
  R(Eq(s.GetProperty("act"),dispatch.GetProperty("act")),"transition stays in dispatch act");
  R(kind=="Travel"?T(s,"node")==T(decision,"to"):Eq(s.GetProperty("node"),dispatch.GetProperty("node")),"transition actual destination node");
 }
 void TransitionHistory(JsonElement s){
  R(transitionRoot==root,"transition history current root");TransitionLocation(s);
  R(T(s,"room")=="CombatRoom"&&Eq(s.GetProperty("rooms").GetProperty("reference"),s.GetProperty("majorCombat").GetProperty("room"))&&!Eq(s.GetProperty("rooms").GetProperty("reference"),dispatch.GetProperty("rooms").GetProperty("reference")),"transition history actual new combat room");
  transitionHistories.Add(ArrivalCombat(s).Clone());
 }
 void ConfirmTransition(JsonElement s){
  R(transitionRoot==root,"transition confirmation current root");TransitionLocation(s);
  if(transitionHistories.Count>0){
   R(T(s,"room")=="CombatRoom"&&Eq(s.GetProperty("rooms").GetProperty("reference"),s.GetProperty("majorCombat").GetProperty("room")),"transition history requires actual combat arrival");
   var arrived=ArrivalCombat(s);R(transitionHistories.All(x=>Eq(x,arrived)),"transition history immutable arrival context/identity");
  }
  transitionHistories.Clear();transitionRoot="";
 }
 ActionKind ValidateAction(JsonElement a,JsonElement state){
  var text=T(a,"kind");R(Enum.TryParse<ActionKind>(text,out var type)&&Enum.IsDefined(type)&&type.ToString()==text,"unregistered action kind");
  var value=a.GetProperty("decision");R(value.ValueKind==JsonValueKind.Object,"action decision object");
  switch(type){
   case ActionKind.EnterAct:
    R(boot.ValueKind==JsonValueKind.Object&&dispatchedActions==0&&!enteredAct&&!planned&&!openingDone&&Eq(state,boot)&&state.GetProperty("act").GetInt32()==0&&state.GetProperty("node").ValueKind==JsonValueKind.Null&&state.GetProperty("room").ValueKind==JsonValueKind.Null&&value.GetProperty("act").GetInt32()==0,"EnterAct initial lifecycle phase");break;
   case ActionKind.EventOption:
    R(enteredAct&&!openingSelected&&!openingDone&&!planned&&openingCandidates.ValueKind==JsonValueKind.Array&&Eq(state,openingState)&&T(state,"room")=="EventRoom"&&T(state.GetProperty("lifecycle").GetProperty("eventModel"),"model")=="EVENT.NEOW","EventOption opening lifecycle phase");
    R(T(value,"model")=="EVENT.NEOW"&&openingCandidates.EnumerateArray().Any(x=>x.GetProperty("index").GetInt32()==value.GetProperty("index").GetInt32()&&!x.GetProperty("IsLocked").GetBoolean()&&T(x,"TextKey")==T(value,"TextKey")),"opening actual candidate");break;
   default:
    R(enteredAct&&openingDone&&planned&&!complete&&!stamped&&!arrival,"action requires completed opening/current plan");
    switch(type){
     case ActionKind.PlayCard:case ActionKind.EndTurn:case ActionKind.UsePotion:
      R(haveRoom&&battleStarted&&opened&&!won&&!cleaned&&!result&&!rewardStarted&&!lossStarted&&T(state,"room")=="CombatRoom"&&state.GetProperty("majorCombat").GetProperty("inProgress").GetBoolean(),"combat action lifecycle phase");break;
     case ActionKind.Travel:case ActionKind.MajorFixture:
      R(choiceClosed&&(!haveRoom||leaveReady)&&(!battleStarted||rewardReturned),"travel/fixture lifecycle phase");
      if(NativeNaturalBoss.IsAct2(scenario)&&state.GetProperty("act").GetInt32()>0&&secondBattleWon)R(actScope.Index==state.GetProperty("act").GetInt32(),"required second-act scope before continued Travel");break;
     case ActionKind.ThirdShop:
       R(NativeThirdShop.IsScenario(scenario)&&haveRoom&&!leaveReady&&choiceClosed&&T(state,"room")=="MerchantRoom"&&NativeThirdShop.ValidAct(scenario,state.GetProperty("act").GetInt32())&&Eq(value,JsonSerializer.SerializeToElement(new{room=state.GetProperty("rooms").GetProperty("reference"),profile=NativeThirdShop.Profile})),"natural integrated shop actual act/current room lifecycle");break;
     case ActionKind.RoomOption:
      R(haveRoom&&!leaveReady&&choiceClosed&&(!battleStarted||rewardReturned)&&T(state,"room") is "EventRoom" or "RestSiteRoom","room choice lifecycle phase");break;
     case ActionKind.RewardOffer:
      R(battleStarted&&won&&cleaned&&result&&rewardStarted&&!rewardReturned&&T(state,"room")=="CombatRoom"&&!state.GetProperty("majorCombat").GetProperty("inProgress").GetBoolean(),"reward lifecycle phase");break;
     case ActionKind.TreasureOpen:case ActionKind.TreasurePick:
      R(NativePreparation.IsScenario(scenario)&&haveRoom&&!leaveReady&&T(state,"room")=="TreasureRoom"&&choiceClosed&&(!battleStarted||rewardReturned),"treasure root lifecycle phase");break;
     case ActionKind.NextActStart:
      R(NaturalBoss&&nextCandidate&&readyReturned&&!nextStart&&state.GetProperty("act").GetInt32()==act+1&&T(state,"room")=="MapRoom"&&state.GetProperty("node").ValueKind==JsonValueKind.Null&&Eq(state,nextStartState)&&value.GetProperty("act").GetInt32()==act+1&&T(value,"to")==T(state.GetProperty("naturalMap"),"start"),"next act start lifecycle/candidate");break;
     case ActionKind.ActReady:
      R(BossGoal&&(NativePreparation.IsScenario(scenario)?targetResults==1:majorResults==1)&&rewardReturned&&!actStarted&&T(state,"room")=="CombatRoom"&&T(state.GetProperty("majorCombat"),"type")=="Boss"&&!state.GetProperty("majorCombat").GetProperty("inProgress").GetBoolean(),"ActReady lifecycle phase");
       R(Eq(value,JsonSerializer.SerializeToElement(new{act=state.GetProperty("act").GetInt32(),map=state.GetProperty("lifecycle").GetProperty("mapGeneration").GetInt32()})),"ActReady current act/map decision");break;
     default:throw new NativeDataException("Major unregistered action phase");
    }break;
  }
  return type;
 }
 internal void Accept(JsonElement p,JsonElement? snapshot)
 {
  var k=T(p,"kind");var d=p.GetProperty("detail");var f=d.TryGetProperty("facts",out var ff)?ff:default;
  if(k=="header"){scenario=T(d,"scenario");mode=T(d,"mode");source=T(d.GetProperty("provenance"),"ScenarioKind");if(NativePreparation.IsScenario(scenario))R(NativePreparation.MatchesDescription(scenario,d.GetProperty("preparationProfile")),"locked preparation capability profile");}
  if(k=="identity"){string id=T(d,"id");identities.Add(id,(T(d,"type"),d.TryGetProperty("model",out var model)?model.Clone():default));if(root!="")actionIdentities.Add(id);}
  rewards.Accept(p,snapshot);
  deaths.Accept(p,snapshot,lastHistory,historyRoot,historyFacts);
  if(NaturalBoss)naturalMaps.Accept(p,snapshot);
  if(NativeNaturalBoss.IsSecondScenario(scenario)&&snapshot is {} cardState)NativeCardRecordContract.State(cardState,identities.ContainsKey);
  // A combat action cannot move to a new act/node/room. Bind every in-action
  // observation (including confirmation/loss) to the immutable dispatch anchor.
  // Travel/MajorFixture intentionally own histories emitted in their new room.
  if(snapshot is {} combatState&&root!=""&&combatAction)CurrentCombat(combatState);
  if(k=="new-run"){R(boot.ValueKind==JsonValueKind.Undefined&&dispatchedActions==0,"unique initial run");boot=snapshot!.Value.Clone();}
  if(k=="root-request"){
   R(choiceClosed&&!complete&&!stamped&&!arrival&&(!won||cleaned),"unfinished arrival/cleanup or action after terminal");
   ValidateAction(d.GetProperty("action"),confirmed.ValueKind==JsonValueKind.Object?confirmed:boot);
  }
  if(k=="dispatch"){
   R(root=="","nested root");if(confirmed.ValueKind==JsonValueKind.Object)R(Eq(snapshot!.Value,confirmed),"dispatch complete prior confirmed continuity");
   var actionType=ValidateAction(f,snapshot!.Value);dispatchedActions++;combatAction=actionType is ActionKind.PlayCard or ActionKind.EndTurn or ActionKind.UsePotion;
   root=T(p,"root");ctx=T(p,"context");kind=T(f,"kind");decision=f.GetProperty("decision").Clone();dispatch=snapshot!.Value.Clone();actionIdentities.Clear();
   // A native ending predicate is authority only for the action that observed it.
   // Repeated true predicates within that action may update the complete anchor.
   victoryAnchored=victoryHistoryCleared=false;victoryState=default;victoryRoot=victoryContext="";victoryEpoch=victoryIndex=0;
   if(kind is "Travel" or "MajorFixture"){chestExitStarted=chestExitReturned=false;R(!haveRoom||leaveReady,"required room leave-ready before travel");R(!battleStarted||rewardReturned,"room change before current battle/rewards completed");R(transitionRoot==""&&transitionHistories.Count==0,"unfinished transition histories");transitionRoot=root;}
   if(kind=="Travel")R(mapCandidates.ValueKind==JsonValueKind.Array&&Eq(dispatch,mapState)&&mapCandidates.EnumerateArray().Any(x=>T(x,"from")==T(decision,"from")&&T(x,"to")==T(decision,"to"))&&T(decision,"from")==T(dispatch,"node"),"travel current legal candidate/state");
   if(kind=="MajorFixture"){
    R(fixtureDeclared&&!fixtureDispatched&&(NativePreparation.IsScenario(scenario)?battles>=1:battles==1)&&rewardReturned&&Eq(dispatch,fixtureState)&&T(decision,"encounter")==target&&T(decision,"type")== (BossGoal?"Boss":"Elite"),"fixture dispatch declared target/type/full state");fixtureDispatched=true;
   }
   if(kind is "PlayCard" or "EndTurn" or "UsePotion"){
    R(battleStarted&&opened,"combat dispatch before opening");
    combatLocation=Location(dispatch).Clone();combatLocationRoot=root;CurrentCombat(dispatch);
    R(ctx==dispatch.GetProperty("act").GetInt32()+":"+T(dispatch,"node")+":"+T(dispatch.GetProperty("rooms").GetProperty("reference"),"$ref"),"combat action concrete room context");
   }
   if(kind=="RoomOption"){
    R(choiceCandidates.ValueKind==JsonValueKind.Array&&choiceClosed&&T(decision,"request")==choiceRequest&&T(decision,"session")==choiceSession&&Eq(dispatch,choiceState),"choice dispatch current request/state");
    var candidate=choiceCandidates.EnumerateArray().SingleOrDefault(x=>x.GetProperty("index").GetInt32()==decision.GetProperty("index").GetInt32());R(candidate.ValueKind==JsonValueKind.Object&&candidate.GetProperty("enabled").GetBoolean()&&candidate.GetProperty("supported").GetBoolean()&&T(candidate,"key")==T(decision,"key")&&Eq(candidate.GetProperty("reference"),decision.GetProperty("option")),"choice actual enabled candidate");choiceClosed=false;choiceStarted=choiceReturned=choiceConfirmed=false;
   }
   if(kind=="RewardOffer"){R(result&&rewardStarted&&!rewardReturned,"reward action before actual battle victory/duplicate");Combat(dispatch);R(Eq(decision.GetProperty("room"),room),"reward root room instance");}
   if(kind=="ActReady"){R(BossGoal&&(NativePreparation.IsScenario(scenario)?targetResults==1:majorResults==1)&&rewardReturned&&!actStarted,"ready before real Boss reward return");act=dispatch.GetProperty("act").GetInt32();actMap=dispatch.GetProperty("lifecycle").GetProperty("mapGeneration").GetInt32();}
  }
  if(k=="confirmed"){
   Inside(p);if(kind=="RoomOption"){R(choiceStarted&&choiceReturned&&!choiceConfirmed&&Eq(snapshot!.Value,choiceResult),"choice confirmation result full state");choiceConfirmed=true;}if(won&&root==combatRoot){R(ctx==combatCtx,"victory confirmation owner");victoryConfirmed=true;}
   if(kind=="Travel"&&T(dispatch,"room")=="EventRoom"&&T(dispatch.GetProperty("lifecycle").GetProperty("eventModel"),"model")=="EVENT.FAKE_MERCHANT")stages.Require(root,"custom-decline");
   if(kind is "Travel" or "MajorFixture"){if(T(dispatch,"room")=="TreasureRoom")R(chestExitStarted&&chestExitReturned,"required actual chest exit completion");ConfirmTransition(snapshot!.Value);}
   if(kind=="NextActStart"){R(NaturalBoss&&!nextStart&&snapshot!.Value.GetProperty("act").GetInt32()==act+1&&T(snapshot.Value,"node")==T(decision,"to")&&T(snapshot.Value,"room")=="EventRoom"&&snapshot.Value.GetProperty("rooms").GetProperty("eventOptions").GetArrayLength()>0,"actual next act starting decision");var after=snapshot.Value;var expected=JsonNode.Parse(dispatch.GetRawText())!;
    R(after.GetProperty("maxHp").GetInt32()==dispatch.GetProperty("maxHp").GetInt32()&&after.GetProperty("hp").GetInt32()==after.GetProperty("maxHp").GetInt32(),"A0 Ancient native full heal");
    var ev=after.GetProperty("lifecycle").GetProperty("eventModel");R(T(ev,"model")==T(after.GetProperty("naturalMap"),"ancientModel")&&!ev.GetProperty("IsFinished").GetBoolean(),"actual generated actionable Ancient");
    R(ev.EnumerateObject().Select(x=>x.Name).Order().SequenceEqual(new[]{"reference","model","rng","IsFinished"}.Order())&&ev.GetProperty("rng").EnumerateObject().Select(x=>x.Name).Order().SequenceEqual(new[]{"counter","s0","s1","s2","s3"}.Order()),"closed native Ancient projection");
    var nr=after.GetProperty("rooms");R(T(nr.GetProperty("reference"),"$ref")!=T(dispatch.GetProperty("rooms").GetProperty("reference"),"$ref")&&nr.GetProperty("map").GetInt32()==actMap+1&&nr.GetProperty("stack").GetInt32()==1&&nr.GetProperty("rest").ValueKind==JsonValueKind.Null,"new actual Ancient room");
     expected["hp"]=dispatch.GetProperty("maxHp").GetInt32();expected["floor"]=1;expected["node"]=T(decision,"to");expected["room"]="EventRoom";expected["rooms"]=JsonNode.Parse(nr.GetRawText());expected["lifecycle"]!["eventModel"]=JsonNode.Parse(ev.GetRawText());
     // Original Darv.GenerateInitialOptions sets up an offered DustyTome before
     // selection. SetupForPlayer consumes one player Rewards draw even unchosen.
     if(NativeDarv.IsScenario(scenario)&&nr.GetProperty("eventOptions").EnumerateArray().Any(o=>T(o,"TextKey")=="DARV.pages.INITIAL.options.DUSTY_TOME")){
      var br=dispatch.GetProperty("playerRng").GetProperty("rngs").GetProperty("Rewards");var ar=after.GetProperty("playerRng").GetProperty("rngs").GetProperty("Rewards");R(ar.GetProperty("counter").GetInt64()==br.GetProperty("counter").GetInt64()+1,"Darv offered DustyTome one native player reward draw");NativeMerchantEconomyContract.CheckRng(br,ar);expected["playerRng"]!["rngs"]!["Rewards"]=JsonNode.Parse(ar.GetRawText());
     }
    R(Eq(after,Element(expected)),"next act first-node complete native transition frame");nextStart=true;secondBattleWon=false;}
   if(kind=="ActReady")R(readyReturned&&Eq(snapshot!.Value,actState),"ready confirmation complete return");
   if(lossStarted){R(lossReturned&&root==lossRoot&&Eq(snapshot!.Value,lossOutput),"death completion immutable root confirmation");}
   if(kind=="MajorFixture"){
    var mc=snapshot!.Value.GetProperty("majorCombat");R(fixtureDispatched&&!fixtureArrived&&T(mc,"encounter")==target&&T(mc,"type")==T(decision,"type")&&T(snapshot.Value,"room")=="CombatRoom"&&Eq(mc.GetProperty("room"),snapshot.Value.GetProperty("rooms").GetProperty("reference"))&&!Eq(mc.GetProperty("room"),room),"fixture actual new room target");fixtureRoom=mc.GetProperty("room").Clone();
   }
   if(kind=="EnterAct"){R(!enteredAct&&dispatchedActions==1&&T(snapshot!.Value,"room")=="EventRoom"&&T(snapshot.Value.GetProperty("lifecycle").GetProperty("eventModel"),"model")=="EVENT.NEOW","EnterAct actual opening return");enteredAct=true;}
   if(kind=="EventOption"){R(!openingSelected&&snapshot!.Value.GetProperty("lifecycle").GetProperty("eventModel").GetProperty("IsFinished").GetBoolean(),"opening actual selected return");openingSelected=true;}
   confirmed=snapshot!.Value.Clone();lastRoot=root;lastCtx=ctx;
   if(kind=="Travel")R(T(decision,"to")==T(snapshot!.Value,"node"),"travel actual destination");
   if(kind is "Travel" or "MajorFixture" or "NextActStart"){R(!arrival,"duplicate arrival pending");arrival=true;arrivalRoot=root;}
   root="";
  }
  if(k=="event-candidates"){
   Static(p,snapshot!.Value);R(enteredAct&&!openingSelected&&!openingDone&&openingCandidates.ValueKind==JsonValueKind.Undefined,"unique opening candidate lifecycle");
   openingCandidates=f.Clone();openingState=snapshot.Value.Clone();var actual=snapshot.Value.GetProperty("rooms").GetProperty("eventOptions");
   R(f.ValueKind==JsonValueKind.Array&&f.GetArrayLength()==actual.GetArrayLength(),"complete opening candidates");
   for(int i=0;i<actual.GetArrayLength();i++)foreach(var field in new[]{"index","TextKey","IsLocked","IsProceed"})R(Eq(f[i].GetProperty(field),actual[i].GetProperty(field)),"opening native candidate "+field);
  }
  if(k=="opening-finished"){Static(p,snapshot!.Value);R(enteredAct&&openingSelected&&!openingDone&&kind=="EventOption"&&f.GetString()=="EVENT.NEOW"&&snapshot.Value.GetProperty("lifecycle").GetProperty("eventModel").GetProperty("IsFinished").GetBoolean(),"unique completed opening lifecycle");openingDone=true;}
  if(k=="major-plan"){
   Static(p,snapshot!.Value);R(openingDone&&!planned,"unique plan after opening");planned=true;
   var list=f.GetProperty(BossGoal?"bosses":"elites").EnumerateArray().Select(x=>x.GetString()!).ToArray();
   R(list.Length>0&&list.SequenceEqual(list.OrderBy(x=>x,StringComparer.Ordinal))&&T(f,"target")== (NaturalBoss?"natural-generated-boss":list[0])&&f.GetProperty("natural").GetBoolean()==NativeMajor.NaturalScenario(scenario),"preregistered ordinal native pool target/source");target=T(f,"target");bossPool=f.GetProperty("bosses").EnumerateArray().Select(x=>x.GetString()!).ToArray();
  }
  if(k=="map-candidates"){Static(p,snapshot!.Value);R(f.ValueKind==JsonValueKind.Array&&f.GetArrayLength()>0&&f.EnumerateArray().All(x=>T(x,"from")==T(snapshot.Value,"node"))&&f.EnumerateArray().Select(x=>T(x,"to")).Distinct().Count()==f.GetArrayLength(),"map candidate source/unique");mapCandidates=f.Clone();mapState=snapshot.Value.Clone();}
  if(k=="fixture-setup-declared"){
   Static(p,snapshot!.Value);R(NativeMajor.FixtureScenario(scenario)&&!fixtureDeclared&&(NativePreparation.IsScenario(scenario)?battles>=1:battles==1)&&rewardReturned&&leaveReady&&T(f,"name")==scenario&&T(f,"fixtureKind")=="native-major-encounter"&&T(f,"encounter")==target,"required unique fixture declaration target/order/source");fixtureDeclared=true;fixtureState=snapshot.Value.Clone();
  }
  if(k=="room-entered"){
   Static(p,snapshot!.Value);R(planned&&arrival&&arrivalRoot==lastRoot&&Eq(f.GetProperty("room"),snapshot.Value.GetProperty("rooms").GetProperty("reference")),"required unique arrival current root/room");arrival=false;R(roomSessions.Add(T(f,"session"))&&T(f,"type")==T(snapshot.Value,"room")&&T(f,"source")== (NativeMajor.NaturalScenario(scenario)?"natural-run":"api-fixture"),"arrival session/type/source");roomSession=T(f,"session");sessionRoom=f.GetProperty("room").Clone();haveRoom=true;leaveReady=false;
   if(kind=="MajorFixture"){R(fixtureDispatched&&!fixtureArrived&&Eq(sessionRoom,fixtureRoom),"fixture arrival actual room");fixtureArrived=true;}
  }
  if(k.StartsWith("room-")&&k!="room-entered"){R(haveRoom&&T(f,"session")==roomSession,"room observation current session");if(f.TryGetProperty("room",out var observedRoom))R(Eq(observedRoom,sessionRoom),"room observation concrete instance");}
  if(k=="room-candidates"){
   Static(p,snapshot!.Value);R(choiceClosed&&choiceRequests.Add(T(f,"request")),"unique closed choice request");choiceCandidates=f.GetProperty("options").Clone();choiceState=snapshot.Value.Clone();choiceRequest=T(f,"request");choiceSession=T(f,"session");
   var native=snapshot.Value.GetProperty("rooms").GetProperty(T(snapshot.Value,"room")=="RestSiteRoom"?"rest":"eventOptions");R(native.GetArrayLength()==choiceCandidates.GetArrayLength(),"complete native choice options");
   for(int i=0;i<native.GetArrayLength();i++){var actual=native[i];var option=choiceCandidates[i];R(option.GetProperty("index").GetInt32()==i&&T(option,"handle")=="o"+i&&Eq(option.GetProperty("reference"),actual.GetProperty("reference"))&&T(option,"key")==T(actual,T(snapshot.Value,"room")=="RestSiteRoom"?"id":"TextKey")&&option.GetProperty("enabled").GetBoolean()==(T(snapshot.Value,"room")=="RestSiteRoom"?actual.GetProperty("enabled").GetBoolean():!actual.GetProperty("IsLocked").GetBoolean()),"native choice candidate mapping");}
  }
  if(k=="room-option-start"){Inside(p);R(kind=="RoomOption"&&!choiceClosed&&!choiceStarted&&T(f,"request")==choiceRequest&&T(f,"session")==choiceSession&&Eq(snapshot!.Value,dispatch)&&Eq(f.GetProperty("option"),decision.GetProperty("option"))&&T(f,"key")==T(decision,"key"),"choice unique start owner/state");choiceStarted=true;}
  if(k=="room-option-return"){Inside(p);R(kind=="RoomOption"&&choiceStarted&&!choiceReturned&&T(f,"request")==choiceRequest&&T(f,"session")==choiceSession,"choice required unique return owner");choiceReturned=true;choiceResult=snapshot!.Value.Clone();choiceResultFacts=f.Clone();if(T(dispatch,"room")=="EventRoom"&&T(dispatch.GetProperty("lifecycle").GetProperty("eventModel"),"model")=="EVENT.HUNGRY_FOR_MUSHROOMS")MushroomResult(snapshot.Value);if(T(dispatch,"room")=="EventRoom")R(f.GetProperty("eventFinished").GetBoolean()==snapshot.Value.GetProperty("lifecycle").GetProperty("eventModel").GetProperty("IsFinished").GetBoolean(),"choice finished fact actual event");}
  if(k=="room-option-boundary"){Static(p,snapshot!.Value);R(!choiceClosed&&choiceStarted&&choiceReturned&&choiceConfirmed&&T(f,"request")==choiceRequest&&T(f,"session")==choiceSession&&Eq(snapshot.Value,choiceResult)&&Eq(f.GetProperty("success"),choiceResultFacts.GetProperty("success"))&&Eq(f.GetProperty("eventFinished"),choiceResultFacts.GetProperty("eventFinished")),"choice required unique complete boundary");choiceClosed=true;}
  if(k=="major-battle-start"){
   Static(p,snapshot!.Value);R(planned&&!arrival&&(!battleStarted||rewardReturned),"battle requires arrival and previous rewards");
   battleStarted=true;opened=false;victoryAnchored=victoryHistoryCleared=false;won=victoryConfirmed=cleaned=result=rewardStarted=rewardReturned=false;battles++;room=f.GetProperty("room").Clone();battle=snapshot.Value.Clone();Combat(battle);
   var mc=battle.GetProperty("majorCombat");if(NaturalBoss&&T(mc,"type")=="Boss"){R(target=="natural-generated-boss"&&(NativeNaturalBoss.IsAct2(scenario)?T(mc,"encounter")==T(snapshot.Value.GetProperty("naturalMap"),"bossModel"):bossPool.Contains(T(mc,"encounter"))),"natural unique generated Boss from native pool");target=T(mc,"encounter");}
   if(NativeNaturalBoss.IsElite2(scenario)&&actScope.Index==1&&T(mc,"type")=="Elite"){R(target=="natural-first-elite","unique actual second-act Elite target");target=T(mc,"encounter");}
    R(Eq(mc.GetProperty("room"),room)&&T(mc,"encounter")==T(f,"encounter")&&T(mc,"type")==T(f,"type")&&mc.GetProperty("inProgress").GetBoolean()&&mc.GetProperty("shouldGiveRewards").GetBoolean()==f.GetProperty("ShouldGiveRewards").GetBoolean(),"actual encounter/type/reward/start projection");
   if(!NativeMajor.NaturalScenario(scenario)&&(NativePreparation.IsScenario(scenario)?fixtureArrived:battles==2))R(fixtureDeclared&&fixtureDispatched&&fixtureArrived&&Eq(room,fixtureRoom)&&T(mc,"encounter")==target&&T(mc,"type")== (BossGoal?"Boss":"Elite"),"actual fixture matches predeclared encounter");
  }
  if(k=="first-battle-opening"){Static(p,snapshot!.Value);R(battleStarted&&!opened&&Eq(snapshot.Value,battle)&&T(f,"encounter")==T(battle.GetProperty("majorCombat"),"encounter"),"battle opening state/owner/encounter");opened=true;}
  if(k=="major-victory-condition"){
   Inside(p);R(battleStarted&&opened&&!won&&!lossStarted&&combatAction,"current native victory predicate");CurrentCombat(snapshot!.Value);VictoryFacts(snapshot.Value);
   R(T(f,"source")=="CombatManager.IsCombatEnding"&&f.GetProperty("ending").GetBoolean()&&f.GetProperty("noPendingLoss").GetBoolean()&&f.GetProperty("livingPrimaryEnemies").GetInt32()==0&&f.GetProperty("index").GetInt32()>0&&f.GetProperty("epoch").GetInt32()>=0,"actual current native history witness");
   if(mode=="detailed"){
    R(Eq(f.GetProperty("epoch"),historyFacts.GetProperty("epoch"))&&Eq(f.GetProperty("index"),historyFacts.GetProperty("index")),"victory predicate current native history cursor");
    // Damage can make the native predicate true before PotionUsedEntry is
    // appended. Observe that query, but it cannot authorize the winning callback
    // until this root has its own observed history witness.
   }
   if(mode!="detailed"||historyRoot==root){victoryAnchored=true;victoryState=snapshot.Value.Clone();victoryRoot=root;victoryContext=ctx;victoryEpoch=f.GetProperty("epoch").GetInt32();victoryIndex=f.GetProperty("index").GetInt32();}
  }
  if(k=="history-clearing"&&won&&!cleaned){R(victoryAnchored&&!victoryHistoryCleared&&root==combatRoot&&victoryRoot==combatRoot&&victoryContext==combatCtx&&T(p,"root")==victoryRoot&&T(p,"context")==victoryContext&&d.GetProperty("epoch").GetInt32()==victoryEpoch&&d.GetProperty("entries").GetInt32()==victoryIndex,"victory required history clear witness");victoryHistoryCleared=true;}
  if(k=="battle-victory-before-cleanup"){
   Inside(p);R(battleStarted&&opened&&!won&&!lossStarted&&kind is "PlayCard" or "EndTurn" or "UsePotion","unique victory current action");Combat(snapshot!.Value);VictoryFacts(snapshot.Value);R(victoryAnchored&&Eq(snapshot.Value,victoryState),"victory complete immutable native predicate anchor");R(victoryRoot==root&&victoryContext==ctx,"victory predicate current action owner");won=true;combatRoot=root;combatCtx=ctx;
  }
  if(k=="battle-victory-cleanup"){
   Static(p,snapshot!.Value);R(won&&victoryAnchored&&victoryRoot==combatRoot&&victoryContext==combatCtx&&victoryHistoryCleared&&victoryConfirmed&&!cleaned&&lastRoot==combatRoot&&lastCtx==combatCtx&&f.GetProperty("historyEntries").GetInt32()==0,"required unique victory cleanup owner/order");Combat(snapshot.Value);R(!snapshot.Value.GetProperty("majorCombat").GetProperty("inProgress").GetBoolean(),"cleanup actual stopped combat");cleaned=true;cleanup=snapshot.Value.Clone();
  }
  if(k=="major-battle-result"){
   Static(p,snapshot!.Value);R(cleaned&&!result&&Eq(snapshot.Value,cleanup)&&Eq(f.GetProperty("room"),room)&&T(f,"terminal")=="BattleVictory","actual battle result after cleanup");result=true;
     if((NativeDarv.IsScenario(scenario)||NativeNaturalBoss.IsSecondScenario(scenario))&&battle.GetProperty("act").GetInt32()>0){
     R(ancientSelected||NativeNaturalBoss.IsAct2(scenario)&&actScopes.TryGetValue(battle.GetProperty("act").GetInt32()-1,out var previousAct)&&previousAct.ancientSelected,"next-act battle after actual Ancient choice");
     // The original first-node slice requires a Monster. Subsequent battles are
     // bound to the arrived native map/encounter, including Elite and Boss.
     var battleType=T(battle.GetProperty("majorCombat"),"type");
     R(actScope.Index<battle.GetProperty("act").GetInt32()?battleType=="Monster":NativeNaturalBoss.IsAct2(scenario)&&battleType is "Monster" or "Elite" or "Boss","next-act battle type for current scope");secondBattleWon=true;
    }
   if(T(battle.GetProperty("majorCombat"),"type") is "Elite" or "Boss")majorResults++;
   if(T(battle.GetProperty("majorCombat"),"encounter")==target&&T(battle.GetProperty("majorCombat"),"type")==(BossGoal?"Boss":"Elite"))targetResults++;
  }
  if(k=="major-rewards-start"){
   Static(p,snapshot!.Value);R(result&&!rewardStarted&&Eq(f.GetProperty("room"),room)&&f.GetProperty("ShouldGiveRewards").GetBoolean()==battle.GetProperty("majorCombat").GetProperty("shouldGiveRewards").GetBoolean(),"reward start actual encounter authority");rewardStarted=true;rewardState=snapshot.Value.Clone();
  }
  if(k=="major-rewards-return"){
   Static(p,snapshot!.Value);R(rewardStarted&&!rewardReturned&&Eq(f.GetProperty("room"),room)&&f.GetProperty("offered").GetBoolean()==battle.GetProperty("majorCombat").GetProperty("shouldGiveRewards").GetBoolean(),"reward return owner/unique/should-give");
   R(f.GetProperty("offered").GetBoolean()?kind=="RewardOffer":Eq(snapshot.Value,rewardState),"required native reward action or unmodified no-reward branch");rewardReturned=true;
  }
  if(k=="room-leave-ready"){Static(p,snapshot!.Value);R(haveRoom&&!leaveReady,"required unique room leave-ready");R(choiceClosed,"leave before choice boundary");if(T(snapshot.Value,"room")=="EventRoom")R(snapshot.Value.GetProperty("lifecycle").GetProperty("eventModel").GetProperty("IsFinished").GetBoolean(),"leave unfinished event");if(T(snapshot.Value,"room")=="CombatRoom"&&battleStarted)R(rewardReturned,"leave before actual rewards");leaveReady=true;}
  if(k=="major-ready-start"){Inside(p);R(kind=="ActReady"&&!actStarted&&!readyStarted&&Eq(snapshot!.Value,dispatch)&&Eq(f.GetProperty("room"),room),"ready start full dispatch/room");readyStarted=true;}
  if(k=="act-transition-start"){
   Inside(p);R(kind=="ActReady"&&readyStarted&&!actStarted&&rewardReturned,"actual act task start/unique");
   var expected=JsonNode.Parse(dispatch.GetRawText())!;expected["floor"]=dispatch.GetProperty("floor").GetInt32()+1;expected["lifecycle"]!["native"]!["ActStarts"]=++actTaskStarts;
   R(Eq(snapshot!.Value,Element(expected)),"act start complete native ready transition");actInput=snapshot.Value.Clone();actPriorIdentities=identities.Keys.ToHashSet();actStarted=true;
  }
  if(k=="act-transition-completed"){
   Inside(p);R(actStarted&&!actReturned&&kind=="ActReady"&&snapshot!.Value.GetProperty("act").GetInt32()==act+1&&snapshot.Value.GetProperty("lifecycle").GetProperty("mapGeneration").GetInt32()>actMap,"actual next act task completion");
   var after=snapshot.Value;var reference=after.GetProperty("rooms").GetProperty("reference");string id=T(reference,"$ref");
   R(identities.TryGetValue(id,out var type)&&type.Type=="MapRoom"&&!actPriorIdentities.Contains(id),"actual newly registered next-act MapRoom");
   var expected=JsonNode.Parse(actInput.GetRawText())!.AsObject();expected["act"]=act+1;expected["floor"]=0;expected["node"]=null;expected["room"]="MapRoom";expected.Remove("majorCombat");
   expected["rooms"]=new JsonObject{["reference"]=new JsonObject{["$ref"]=id},["map"]=actMap+1,["stack"]=1,["rest"]=null,["eventOptions"]=null};
   expected["combat"]!["round"]=null;expected["combat"]!["side"]=null;expected["combat"]!["enemies"]=null;
   if(NaturalBoss)expected["naturalMap"]=JsonNode.Parse(after.GetProperty("naturalMap").GetRawText());
    expected["lifecycle"]!["native"]!["ActReturns"]=++actTaskReturns;expected["lifecycle"]!["mapGeneration"]=actMap+1;
   R(Eq(after,Element(expected)),"next act complete native MapRoom transition frame");actReturned=true;actState=after.Clone();
  }
  if(k=="major-ready-return"){Inside(p);R(actReturned&&!readyReturned&&Eq(snapshot!.Value,actState)&&f.GetProperty("act").GetInt32()==snapshot.Value.GetProperty("act").GetInt32()&&f.GetProperty("map").GetInt32()==snapshot.Value.GetProperty("lifecycle").GetProperty("mapGeneration").GetInt32(),"ready after actual task complete state");readyReturned=true;}
  if(k=="prep-chest-exit-start"){
   Inside(p);R(NativePreparation.IsScenario(scenario)&&kind=="Travel"&&!chestExitStarted&&T(dispatch,"room")=="TreasureRoom"&&Eq(f.GetProperty("room"),dispatch.GetProperty("rooms").GetProperty("reference")),"chest actual exit owner/unique");
   var expected=JsonNode.Parse(dispatch.GetRawText())!;expected["node"]=T(decision,"to");expected["floor"]=dispatch.GetProperty("floor").GetInt32()+1;expected["room"]=null;expected["rooms"]=null;
   R(Eq(snapshot!.Value,JsonSerializer.SerializeToElement(expected)),"chest exit popped current room complete frame");chestExitState=snapshot.Value.Clone();chestExitStarted=true;
  }
  if(k=="prep-chest-exit-return"){
   Inside(p);R(chestExitStarted&&!chestExitReturned&&Eq(f.GetProperty("room"),dispatch.GetProperty("rooms").GetProperty("reference"))&&f.GetProperty("closed").GetBoolean()&&Eq(snapshot!.Value,chestExitState),"chest original task complete immutable return");chestExitReturned=true;
  }
  if(k=="natural-event-declined"){
   Inside(p);R(NativeNaturalBoss.IsThird(scenario)&&kind=="Travel"&&T(dispatch,"room")=="EventRoom"&&Eq(snapshot!.Value,dispatch),"custom decline complete dispatch frame");
   var ev=dispatch.GetProperty("lifecycle").GetProperty("eventModel");R(T(ev,"model")=="EVENT.FAKE_MERCHANT"&&ev.GetProperty("IsFinished").GetBoolean()&&dispatch.GetProperty("rooms").GetProperty("eventOptions").GetArrayLength()==0,"custom decline actual finished FakeMerchant");
   R(Eq(f.GetProperty("model"),ev.GetProperty("reference"))&&Eq(f.GetProperty("room"),dispatch.GetProperty("rooms").GetProperty("reference"))&&T(f,"policy")=="leave-without-transactions"&&f.GetProperty("purchases").GetInt32()==0&&!f.GetProperty("pricesEvaluated").GetBoolean()&&f.GetProperty("nativeProceedAllowed").GetBoolean(),"custom decline current room/model/legal policy");
   R(f.EnumerateObject().Select(x=>x.Name).Order().SequenceEqual(new[]{"room","model","policy","offered","purchases","pricesEvaluated","nativeProceedAllowed"}.Order()),"custom decline closed decision schema");
   var offered=f.GetProperty("offered").EnumerateArray().ToArray();var seen=new HashSet<string>();
   string[] pool=["FAKE_ANCHOR","FAKE_BLOOD_VIAL","FAKE_HAPPY_FLOWER","FAKE_LEES_WAFFLE","FAKE_MANGO","FAKE_ORICHALCUM","FAKE_SNECKO_EYE","FAKE_STRIKE_DUMMY","FAKE_VENERABLE_TEA_SET"];
   R(offered.Length==6&&offered.Select(x=>T(x,"model")).Distinct().Count()==6,"custom decline full six-item native stock");
   foreach(var item in offered){R(item.EnumerateObject().Select(x=>x.Name).Order().SequenceEqual(new[]{"reference","model","stocked"}.Order()),"custom decline closed stock schema");string id=T(item.GetProperty("reference"),"$ref"),model=T(item,"model");R(seen.Add(id)&&actionIdentities.Contains(id)&&item.GetProperty("stocked").GetBoolean()&&pool.Any(x=>model=="RELIC."+x)&&identities.TryGetValue(id,out var identity)&&identity.Model.ValueKind==JsonValueKind.String&&identity.Model.GetString()==model&&identity.Type==NativeThirdAncientContract.TypeForKey(model),"custom decline actual unique stocked instance/model");R(!dispatch.GetProperty("relics").EnumerateArray().Any(x=>T(x,"id")==id),"custom decline stock not already held");}
   stages.Observe(root,"custom-decline");
  }
  if(k=="combat-history"){
   Inside(p);if(kind is "PlayCard" or "EndTurn" or "UsePotion")CurrentCombat(snapshot!.Value);
   if(kind is "Travel" or "MajorFixture")TransitionHistory(snapshot!.Value);
   lastHistory=snapshot!.Value.Clone();historyFacts=f.Clone();historyRoot=root;
  }
  if(k=="battle-loss-start"){
   Inside(p);R(battleStarted&&opened&&!won&&!lossStarted&&kind is "PlayCard" or "EndTurn" or "UsePotion","actual loss before terminal");CurrentCombat(snapshot!.Value);
   R(snapshot.Value.GetProperty("hp").GetInt32()==0&&snapshot.Value.GetProperty("majorCombat").GetProperty("inProgress").GetBoolean(),"native loss input dead/in-progress prerequisite");
   if(!deaths.Enabled&&mode=="detailed")R(historyRoot==root&&Eq(snapshot.Value,lastHistory),"loss input complete latest native history state");
   lossInput=snapshot.Value.Clone();lossStarted=true;lossRoot=root;
  }
  if(k=="battle-loss-confirmed"){
   Inside(p);R(lossStarted&&!lossReturned&&root==lossRoot,"native loss confirmation owner/order");CurrentCombat(snapshot!.Value);
   // Pinned ProcessPendingLoss in this no-UI/TestMode host stops combat and returns.
   // All other projected state, including future nested fields, is preserved.
   var expected=JsonNode.Parse(lossInput.GetRawText())!;expected["majorCombat"]!["inProgress"]=false;expected["lifecycle"]!["native"]!["Losses"]=1;
   R(Eq(snapshot.Value,JsonSerializer.SerializeToElement(expected)),"native loss complete input/output transition");lossOutput=snapshot.Value.Clone();lossReturned=true;
  }
  if(k=="death"){Static(p,snapshot!.Value);R(lossReturned&&!stamped&&!deathObserved&&lastRoot==lossRoot,"death confirmed prefix");deathObserved=true;}
  if(k=="terminal-stamp"){Static(p,snapshot!.Value);R(lossReturned&&deathObserved&&!stamped&&!rewardStarted&&T(f,"terminal")=="Death"&&snapshot.Value.GetProperty("lifecycle").GetProperty("native").GetProperty("Losses").GetInt32()==1&&Eq(f.GetProperty("native"),snapshot.Value.GetProperty("lifecycle").GetProperty("native"))&&T(f,"scope")==source&&!f.GetProperty("retailLossFinalization").GetBoolean(),"terminal death authority/facts/source");stamped=true;}
  if(k=="terminal-contract"){Static(p,snapshot!.Value,true);R(stamped&&!closed&&T(f,"terminal")=="Death"&&f.GetProperty("valid").GetBoolean()&&f.GetProperty("staleRejected").GetBoolean()&&f.GetProperty("zeroAdditionalDispatch").GetBoolean()&&!f.GetProperty("executed").GetBoolean(),"terminal stale dispatch contract");closed=terminalContract=true;}
  if(k=="platform-offline-guard"){R(!d.GetProperty("initialized").GetBoolean(),"offline platform guard");platforms++;}
  if(k=="third-tip-presentation"){Inside(p);R(NativeNaturalBoss.IsThird(scenario)&&kind=="NextActStart"&&T(d,"source")=="StunIntent.GetStaticHoverTip"&&d.GetProperty("textureMissing").GetBoolean()&&d.GetProperty("testMode").GetBoolean(),"third visual tip exact initial action owner");}
  if(k=="boss-audio-guard"){Inside(p);R(NativeNaturalBoss.IsAct2(scenario)&&combatAction&&T(d,"source") is "Rocket.BeforeDeath" or "Crusher.BeforeDeath"&&d.GetProperty("receiverMissing").GetBoolean()&&d.GetProperty("testMode").GetBoolean(),"Boss presentation guard current combat ownership");}
  if(k=="natural-act-scope"){
   Static(p,snapshot!.Value);R(NativeNaturalBoss.IsAct2(scenario)&&actScope.Index<2&&actReturned&&readyReturned&&nextFirst&&ancientSelected&&secondBattleWon&&rewardReturned&&leaveReady,"next act scope requires previous native lifecycle and actual first battle/rewards");
   R(snapshot.Value.GetProperty("act").GetInt32()==actScope.Index+1&&f.GetProperty("act").GetInt32()==actScope.Index+1&&Eq(f.GetProperty("map"),snapshot.Value.GetProperty("lifecycle").GetProperty("mapGeneration"))&&T(f,"boss")==T(snapshot.Value.GetProperty("naturalMap"),"bossModel"),"scope actual generated act/map/Boss");
   R(actScopes.TryAdd(actScope.Index,actScope),"unique completed act scope");actScope=new(actScope.Index+1){Target=NativeNaturalBoss.IsElite2(scenario)?"natural-first-elite":"natural-generated-boss"};
  }
  if(k=="natural-act2-complete"){
   Static(p,snapshot!.Value);R(NativeNaturalBoss.IsAct2(scenario)&&actScopes.Count==1&&actScope.Index==1&&targetResults==1&&actReturned&&readyReturned&&snapshot.Value.GetProperty("act").GetInt32()==2&&T(snapshot.Value,"room")=="MapRoom"&&snapshot.Value.GetProperty("node").ValueKind==JsonValueKind.Null&&actTaskReturns==2,"two natural acts completion authority");stages.Observe("act2","complete");
   R(Eq(f.GetProperty("act"),snapshot.Value.GetProperty("act"))&&Eq(f.GetProperty("map"),snapshot.Value.GetProperty("lifecycle").GetProperty("mapGeneration")),"two acts completion current map facts");
  }
  if(k=="natural-elite-complete"){Static(p,snapshot!.Value);R(NativeNaturalBoss.IsElite2(scenario)&&actScope.Index==1&&targetResults==1&&rewardReturned&&leaveReady&&T(battle.GetProperty("majorCombat"),"type")=="Elite"&&snapshot.Value.GetProperty("act").GetInt32()==1&&T(f,"encounter")==target,"actual second-act Elite completed native rewards");stages.Observe("act2","elite");}
  if(k=="natural-start-candidate"){Static(p,snapshot!.Value);R(NaturalBoss&&readyReturned&&!nextCandidate&&!nextStart&&T(snapshot.Value,"room")=="MapRoom"&&Eq(f.GetProperty("act"),snapshot.Value.GetProperty("act"))&&T(f,"coord")==T(snapshot.Value.GetProperty("naturalMap"),"start")&&T(f,"type")=="Ancient","required next act start candidate");nextCandidate=true;nextStartState=snapshot.Value.Clone();}
   if(k=="natural-first-node"){stages.Observe("act"+snapshot!.Value.GetProperty("act").GetInt32(),"first-node");Static(p,snapshot!.Value);R(NaturalBoss&&nextCandidate&&nextStart&&!nextFirst&&!arrival&&kind=="NextActStart"&&Eq(f.GetProperty("act"),snapshot.Value.GetProperty("act"))&&T(f,"coord")==T(snapshot.Value,"node")&&Eq(f.GetProperty("room"),snapshot.Value.GetProperty("rooms").GetProperty("reference")),"required actual first-node boundary");nextFirst=true;}
   if(k=="natural-ancient-selected"){
    if(NativeDarv.IsScenario(scenario)){
     Static(p,snapshot!.Value);R(nextFirst&&!ancientSelected&&kind=="RoomOption"&&T(f,"model")=="EVENT.DARV"&&T(dispatch.GetProperty("lifecycle").GetProperty("eventModel"),"model")=="EVENT.DARV"&&T(decision,"key")==T(f,"key")&&Eq(f.GetProperty("option"),decision.GetProperty("option")),"Darv actual current option/action ownership");
     NativeDarv.CheckPickup(dispatch,snapshot.Value,T(f,"relic"));stages.Require("act1","first-node");stages.Observe("act1","ancient-selected");ancientSelected=true;
    }else{
    Static(p,snapshot!.Value);R(NativeNaturalBoss.IsSecondScenario(scenario)&&nextFirst&&!ancientSelected&&kind=="RoomOption"&&T(f,"model")=="EVENT.TEZCATARA"&&T(f,"key")=="TEZCATARA.pages.INITIAL.options.NUTRITIOUS_SOUP"&&T(decision,"key")==T(f,"key")&&Eq(f.GetProperty("option"),decision.GetProperty("option"))&&snapshot.Value.GetProperty("lifecycle").GetProperty("eventModel").GetProperty("IsFinished").GetBoolean(),"actual native Ancient selected completion");
    var before=dispatch.GetProperty("relics").EnumerateArray().Select(x=>T(x,"id")).ToHashSet();var added=snapshot.Value.GetProperty("relics").EnumerateArray().Where(x=>!before.Contains(T(x,"id"))).ToArray();R(added.Length==1&&T(added[0],"model")=="RELIC.NUTRITIOUS_SOUP","actual native Ancient relic acquisition");
    R(dispatch.GetProperty("deck").EnumerateArray().Select(x=>T(x,"id")).SequenceEqual(snapshot.Value.GetProperty("deck").EnumerateArray().Select(x=>T(x,"id"))),"Ancient preserves permanent card instances");NativeCardRecordContract.Soup(dispatch,snapshot.Value);stages.Require("act1","first-node");stages.Observe("act1","ancient-selected");ancientSelected=true;}
   }
   if(k=="natural-third-ancient-selected"){
   Static(p,snapshot!.Value);R(NativeNaturalBoss.IsThird(scenario)&&actScope.Index==1&&nextFirst&&!ancientSelected&&kind=="RoomOption"&&T(f,"model")=="EVENT.TANX"&&T(decision,"key")==T(f,"key")&&Eq(f.GetProperty("option"),decision.GetProperty("option")),"third Ancient actual option owner");
   stages.Require("act2","first-node");NativeThirdAncientContract.Selected(dispatch,snapshot.Value,T(f,"key"));
   var newRelic=snapshot.Value.GetProperty("relics").EnumerateArray().Single(x=>!dispatch.GetProperty("relics").EnumerateArray().Any(y=>T(y,"id")==T(x,"id")));
   R(identities.TryGetValue(T(newRelic,"id"),out var newRelicIdentity)&&newRelicIdentity.Type==NativeThirdAncientContract.TypeForKey(T(f,"key")),"third Ancient registered concrete relic type");
   R(newRelicIdentity.Model.ValueKind==JsonValueKind.String&&Eq(newRelicIdentity.Model,newRelic.GetProperty("model")),"third Ancient registered relic model matches actual acquired model");
   ancientSelected=true;stages.Observe("act2","ancient-selected");
  }
  if(k=="natural-third-first-complete"){
   Static(p,snapshot!.Value);R(NativeNaturalBoss.IsThird(scenario)&&actScopes.Count==2&&actScope.Index==2&&actScopes[1].ancientSelected&&actScopes[1].secondBattleWon&&rewardReturned&&leaveReady&&snapshot.Value.GetProperty("act").GetInt32()==2&&f.GetProperty("act").GetInt32()==2,"third first battle/rewards complete scope");stages.Require("act2","ancient-selected");stages.Observe("act2","first-complete");
  }
  if(k=="natural-third-boss-complete"){Static(p,snapshot!.Value);stages.Require("act2","first-complete");R(scenario=="major-natural-act3-route"&&actScope.Index==2&&targetResults==1&&rewardReturned&&leaveReady&&T(battle.GetProperty("majorCombat"),"type")=="Boss"&&T(f,"encounter")==target&&snapshot.Value.GetProperty("act").GetInt32()==2,"actual third Boss and native rewards");stages.Observe("act2","boss-complete");}
  if(k=="major-complete"){if(scenario=="major-natural-act3-route"){stages.Require("act2","boss-complete");}else if(NativeNaturalBoss.IsThird(scenario)){stages.Require("act2","first-complete");}else if(NativeNaturalBoss.IsElite2(scenario)){stages.Require("act2","elite");}else if(NativeNaturalBoss.IsAct2(scenario)){stages.Require("act2","complete");}else if(NativeDarv.IsScenario(scenario)||NativeNaturalBoss.IsSecondScenario(scenario)){stages.Require("act1","first-node","ancient-selected");R(secondBattleWon,"actual second-act battle/reward prerequisite");}if(NaturalBoss&&!NativeNaturalBoss.IsAct2(scenario))R(nextFirst,"natural first node prerequisite");R(f.GetProperty("natural").GetBoolean()==NativeMajor.NaturalScenario(scenario),"complete scenario origin");Static(p,snapshot!.Value);R(planned&&!complete&&!arrival&&(NativeNaturalBoss.IsThird(scenario)?actScope.Index==2:NativePreparation.IsScenario(scenario)?targetResults==1:majorResults==1)&&rewardReturned&&T(f,"scenario")==scenario&&(NativeNaturalBoss.IsThird(scenario)||!BossGoal||actReturned&&readyReturned),"major completion prerequisites");complete=true;}
  if(snapshot is {} state){
   allowedTop??=state.EnumerateObject().Select(x=>x.Name).Append("majorCombat").ToHashSet();R(state.EnumerateObject().All(x=>allowedTop.Contains(x.Name)),"unowned state projection field");
   var n=state.GetProperty("lifecycle").GetProperty("native");
   R(n.GetProperty("Losses").GetInt32()==(lossReturned?1:0)&&n.GetProperty("Closed").GetBoolean()==closed&&!n.GetProperty("Won").GetBoolean(),"native loss/closed authority");
   foreach(var field in new[]{"WinStarts","WinReturns","RunEndStarts","RunEndReturns"})R(n.GetProperty(field).GetInt32()==0,"unexpected native run-terminal authority "+field);
   R(n.GetProperty("ActStarts").GetInt32()==actTaskStarts&&n.GetProperty("ActReturns").GetInt32()==actTaskReturns&&n.GetProperty("PlatformRefreshes").GetInt32()==platforms,"native act/platform task authority");
  }
 }
 void MushroomResult(JsonElement state){
  R(decision.GetProperty("index").GetInt32()==0,"registered BigMushroom option");
  var old=dispatch.GetProperty("relics").EnumerateArray().ToArray();var now=state.GetProperty("relics").EnumerateArray().ToArray();
  R(now.Length==old.Length+1&&old.Zip(now).All(x=>Eq(x.First,x.Second)),"Mushroom original relic continuity");
  var added=now[^1];string id=T(added,"id");R(T(added,"model")=="RELIC.BIG_MUSHROOM"&&actionIdentities.Contains(id)&&identities.TryGetValue(id,out var registered)&&registered.Type=="BigMushroom"&&registered.Model.GetString()=="RELIC.BIG_MUSHROOM","Mushroom actual new registered relic");
  var expected=JsonNode.Parse(dispatch.GetRawText())!;expected["hp"]=dispatch.GetProperty("hp").GetInt32()+20;expected["maxHp"]=dispatch.GetProperty("maxHp").GetInt32()+20;expected["relics"]=JsonNode.Parse(state.GetProperty("relics").GetRawText());expected["rooms"]!["eventOptions"]=new JsonArray();expected["lifecycle"]!["eventModel"]!["IsFinished"]=true;
  R(Eq(Element(expected),state),"native BigMushroom gain20 full state and RNG");
 }
  internal void Finish(JsonElement report,bool branchHorizon=false,bool unsupportedRoomPrefix=false,bool unsupportedActPrefix=false)
 {
  R(transitionRoot==""&&transitionHistories.Count==0,"unconfirmed transition history suffix");
   if(unsupportedActPrefix){
    R(NativeSolverCoordinator.IsScenario(scenario)&&!stamped&&!complete&&!arrival&&root==""&&won&&victoryConfirmed&&cleaned&&result&&rewardReturned&&choiceClosed&&targetResults==1&&readyStarted&&actStarted&&actReturned&&readyReturned&&!nextCandidate&&!nextStart&&actTaskStarts==1&&actTaskReturns==1,"actual completed Boss rewards and native act task before unsupported Ancient");
    R(Eq(confirmed,actState)&&confirmed.GetProperty("hp").GetInt32()>0&&confirmed.GetProperty("act").GetInt32()==1&&T(confirmed,"room")=="MapRoom"&&confirmed.GetProperty("node").ValueKind==JsonValueKind.Null&&T(confirmed.GetProperty("naturalMap"),"ancientModel")!="EVENT.TEZCATARA"&&T(report.GetProperty("result"),"Stop")=="Unsupported"&&T(report.GetProperty("result"),"Terminal")=="Ongoing","unsupported generated Ancient immutable actual next-act map");
    rewards.FinishMajorDeathPrefix();return;
   }
   if(unsupportedRoomPrefix){
    R(NativeSolverJourney.CandidateScenario(scenario)&&!stamped&&!complete&&!arrival&&root==""&&haveRoom&&!leaveReady&&choiceClosed&&rewardReturned&&cleaned&&won&&T(confirmed,"room")=="MerchantRoom"&&confirmed.GetProperty("hp").GetInt32()>0&&T(report.GetProperty("result"),"Stop")=="Unsupported"&&T(report.GetProperty("result"),"Terminal")=="Ongoing","safe actual unsupported merchant arrival after completed battles/rewards");
    rewards.FinishMajorDeathPrefix();return;
   }
  if(branchHorizon){R(!stamped&&!complete&&!arrival&&root==""&&battleStarted&&opened&&!won&&!rewardStarted,"branch safe unfinished combat slice");rewards.FinishMajorDeathPrefix();return;}
   if(stamped){R(choiceClosed&&terminalContract&&closed&&!complete&&T(report.GetProperty("result"),"Terminal")=="Death"&&T(report.GetProperty("result"),"Stop")=="Completed","certified Death report");rewards.FinishMajorDeathPrefix();}
  else{R(complete&&!arrival&&root==""&&T(report.GetProperty("result"),"Terminal")=="Ongoing","complete slice report");rewards.Finish();}
 }
}
