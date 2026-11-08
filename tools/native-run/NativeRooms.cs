using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SimulatorLab.Core;
using SimulatorLab.Core.Mods;

namespace OfflineSearchHarness;
internal static class NativeRooms
{
    internal static readonly HashSet<string> Fixtures=["room-heal","room-cap","room-smith","room-cancel","room-abstain","room-phases","room-exit"];
    internal static readonly HashSet<string> Faults=["room-negative","room-stale","room-duplicate","room-extra","room-disabled","room-early-exit","room-task-fault","room-budget","room-invalid-child"];
    internal static bool IsFixture(string s)=>Fixtures.Contains(s);
    internal static bool IsFault(string s)=>Faults.Contains(s);
    internal static bool IsScenario(string s)=>NativeEventFlow.IsScenario(s)||s=="rooms-natural"||s=="merchant-natural"||s=="merchant-natural-relic"||IsFixture(s)||IsFault(s)||NativeEventFixtures.Scenarios.Contains(s);
    internal static bool Active=>IsScenario(NativeLifecycle.Scenario)||NativeMajor.Active;
    internal static object Profile=>new{id="4B-native-rooms/v1",actionSeconds=45,actionCount=5000,wallSeconds=600,nodeCount=12,roomChoices=32,information="visible-map-and-current-options; fixture scripts separately labelled"};
}
internal static partial class Program
{
    static readonly DateTime roomsStarted=DateTime.UtcNow;
    static AbstractRoom? sessionRoom;
    static int roomNumber,roomRequest,roomDecisions,roomVictories;
    static string Session=>"s"+roomNumber;
    static bool roomComplete;
    static CardModel? persistentUpgrade;
    static void RoomBudget(){NativeFullRun.Check(dispatched);if(dispatched>=5000||DateTime.UtcNow-roomsStarted>TimeSpan.FromMinutes(10))throw new TimeoutException("Native room slice budget");}
    static TimeSpan RoomWaitBudget(){RoomBudget();return TimeSpan.FromSeconds(Math.Min(NativeFullRun.RemainingActionSeconds,(roomsStarted.AddMinutes(10)-DateTime.UtcNow).TotalSeconds));}
    static object? RoomSnapshot()
    {
        var room=run?.CurrentRoom;if(room==null)return null;
        var ev=room is EventRoom?RunManager.Instance.EventSynchronizer.GetLocalEvent():null;
        return new{reference=NativeIdentity.Ref(room),map=RunManager.Instance.MapSelectionSynchronizer.MapGenerationCount,stack=run!.CurrentRoomCount,
            rest=room is RestSiteRoom rest?rest.Options.Select((o,i)=>new{index=i,reference=NativeIdentity.Ref(o),id=o.OptionId,enabled=o.IsEnabled}).ToArray():null,
            eventOptions=ev?.CurrentOptions.Select((o,i)=>new{index=i,reference=NativeIdentity.Ref(o),o.TextKey,o.IsLocked,o.IsProceed}).ToArray()};
    }
    static void RoomArrived()
    {
        if(ReferenceEquals(sessionRoom,run!.CurrentRoom))throw new NativeActionRejectedException("Room session entered twice");
        sessionRoom=run.CurrentRoom;roomNumber++;roomDecisions=0;roomComplete=false;
        Observe("room-entered",new{session=Session,room=NativeIdentity.Ref(sessionRoom),type=sessionRoom!.GetType().Name,source=provenance.ScenarioKind});
    }
    static void RoomComplete(string reason)
    {
        if(roomComplete)throw new NativeActionRejectedException("Duplicate room completion");
        if(run!.CurrentRoom is EventRoom&& !RunManager.Instance.EventSynchronizer.GetLocalEvent().IsFinished)throw new NativeActionRejectedException("Unfinished event cannot complete");
        roomComplete=true;Observe("room-leave-ready",new{session=Session,room=NativeIdentity.Ref(sessionRoom),reason});
    }
    sealed record RoomAnswer(string Request,string Session,string Stamp,string Selected);
    static void SelectRoomOption(int desired)
    {
        if(!ReferenceEquals(sessionRoom,run!.CurrentRoom)||roomComplete)throw new NativeActionRejectedException("Stale/closed room session");
        if(++roomDecisions>32)throw new TimeoutException("Room decision budget");
        var rest=run.CurrentRoom as RestSiteRoom;var ev=run.CurrentRoom is EventRoom?RunManager.Instance.EventSynchronizer.GetLocalEvent():null;
        object[] objects=rest!=null?rest.Options.Cast<object>().ToArray():ev!=null?ev.CurrentOptions.Cast<object>().ToArray():throw new NotSupportedException("Room options unsupported");
        var options=objects.Select((o,i)=>new{handle="o"+i,index=i,reference=NativeIdentity.Ref(o),key=o is RestSiteOption ro?ro.OptionId:((EventOption)o).TextKey,enabled=o is RestSiteOption r?r.IsEnabled:!((EventOption)o).IsLocked,supported=ev is Symbiote?i==1:ev is StoneOfAllTime or ThisOrThat?i==0:NativeNaturalBoss.Third&&ev is PotionCourier?i==1:NativeNaturalBoss.Third&&ev is HungryForMushrooms?i==0:(NativeEventFlow.Active||NativePreparation.Active)&&ev is WhisperingHollow or BattlewornDummy?i==0:ev is SunkenStatue?i==1:ev is not DenseVegetation||i==0}).ToArray();
        string request=Session+"q"+(++roomRequest),stamp=StateStamp();
        Observe("room-candidates",new{session=Session,request,room=NativeIdentity.Ref(sessionRoom),options});
        var decisions=options.Select(x=>(object)new{session=Session,request,index=x.index,key=x.key,option=NativeIdentity.Ref(objects[x.index])}).ToArray();
        NativePolicy.PublicRequests.Add(new{kind="RoomOption",session=Session,request,options=options.Select(x=>new{x.handle,x.key,x.enabled,x.supported}).ToArray(),information="player-visible-current-options"});
        int index=replayPath!=null?PolicyIndex("RoomOption",decisions):desired;
        if(scenario=="room-negative")index=-1;
        var answer=new RoomAnswer(request,Session,scenario=="room-stale"?"old":stamp,"o"+index);
        if(scenario=="room-extra"){
            _=JsonSerializer.Deserialize<RoomAnswer>(JsonSerializer.Serialize(answer).TrimEnd('}')+",\"extra\":true}",new JsonSerializerOptions{UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow});
        }
        if(answer.Request!=request||answer.Session!=Session||answer.Stamp!=StateStamp()||index<0||index>=objects.Length||!options[index].enabled)throw new NativeActionRejectedException("Invalid/stale/disabled room option answer");
        if(!options[index].supported)throw new NotSupportedException("4B selected event branch deferred to later node package");
        var ticket=new NativeDecisionTicket(sessionRoom!,roomRequest,stamp,JsonSerializer.Serialize(answer));
        ticket.Consume(run.CurrentRoom!,roomRequest,StateStamp(),JsonSerializer.Serialize(answer));
        if(scenario=="room-duplicate")ticket.Consume(run.CurrentRoom!,roomRequest,StateStamp(),JsonSerializer.Serialize(answer));
        bool? success=null;
        var stone=StoneLiftBefore(ev,index);
        var thisOrThat=ThisOrThatBefore(ev,index);
        var symbiote=SymbioteBefore(ev,index);
        Action("RoomOption",decisions[index],()=>{
            using var resources=thisOrThat!=null?new NativeEventResourceLedger(player!):null;
            var slippery=SlipperyBefore(ev,index);var reflection=ReflectionBefore(ev,index);
            var rewardRoot=NativeEventFlow.Unified||NativeMajor.Active||NativeEventFixtures.Active?NativeRewards.Begin("RoomOption"):null;
            Observe("room-option-start",new{session=Session,request,room=NativeIdentity.Ref(sessionRoom),option=NativeIdentity.Ref(objects[index]),key=options[index].key});
            if(rest!=null){var task=RunManager.Instance.RestSiteSynchronizer.ChooseLocalOption(index);Await(task,"Native rest option");success=task.GetAwaiter().GetResult();}
            else{
                NativeObservation.EventFault=scenario=="room-task-fault";
                var option=(EventOption)objects[index];
                if(option.IsProceed)Await(option.Chosen(),"Native event proceed");
                else{RunManager.Instance.EventSynchronizer.ChooseLocalOption(index);Await(RunManager.Instance.EventSynchronizer.AwaitPendingOptionTasks(),"Native event option");}
            }
            NativeHost.Ensure();StoneLiftAfter(stone);ThisOrThatAfter(thisOrThat,resources);SymbioteAfter(symbiote);SlipperyAfter(slippery);ReflectionAfter(reflection);
            if(rewardRoot!=null){NativeRewards.End(rewardRoot,new{completed=true});NativeRewards.LastRoot=rewardRoot;}
            NativeHost.Ensure();Observe("room-option-return",new{session=Session,request,room=NativeIdentity.Ref(sessionRoom),success,eventFinished=ev?.IsFinished});
        });
        if(NativeEventFlow.Unified||NativeMajor.Active||NativeEventFixtures.Active)NativeRewards.Boundary(NativeRewards.LastRoot??throw new NativeDataException("Missing event root"));
        Observe("room-option-boundary",new{session=Session,request,success,eventFinished=ev?.IsFinished});
    }
    static void LeaveRoom()
    {
        if(!ReferenceEquals(run!.CurrentRoom,sessionRoom)||!roomComplete)throw new NativeActionRejectedException("Room exit before completion");
        var previous=sessionRoom;
        Action("RoomExit",new{session=Session,room=NativeIdentity.Ref(previous)},()=>{
            Observe("room-exit-start",new{session=Session,room=NativeIdentity.Ref(previous)});
            Await(previous!.Exit(run),"Original room exit");Observe("room-exit-return",new{session=Session,room=NativeIdentity.Ref(previous)});
        });
        Observe("room-exited",new{session=Session,room=NativeIdentity.Ref(previous)});
        Observe("room-complete",new{session=Session,room=NativeIdentity.Ref(previous),reason="original Exit returned and root confirmed"});
    }
    static void RoomFixture()
    {
        bool eventCase=scenario is "room-abstain" or "room-phases" or "room-early-exit" or "room-task-fault";
        Observe("fixture-setup-declared",new{name=scenario,fixtureKind="room",changes="Original EnterRoomDebug after normal Neow; declared native damage to HP40/75; no seed/reward edits"});
        Action("RoomFixtureSetup",new{eventCase},()=>{
            Await(RunManager.Instance.EnterRoomDebug(eventCase?RoomType.Event:RoomType.RestSite,model:eventCase?ModelDb.Event<AbyssalBaths>():null,showTransition:false),"Declared native room fixture");
            if(eventCase)EnterAuditedEvent();
            int hp=scenario=="room-cap"?75:40;
            Await(CreatureCmd.Damage(new ThrowingPlayerChoiceContext(),player!.Creature,player.Creature.CurrentHp-hp,ValueProp.Unblockable|ValueProp.Unpowered,null,null),"Declared fixture injury");
            if(scenario=="room-disabled")foreach(var c in player.Deck.Cards)while(c.IsUpgradable)CardCmd.Upgrade(c);
        });
        RoomArrived();
        if(scenario=="room-budget")throw new TimeoutException("labelled room budget at actual decision boundary");
        var before=State();var original=player!.Deck.Cards.ToArray();
        if(eventCase){
            SelectRoomOption(scenario is "room-phases" or "room-early-exit"?0:1);
            if(scenario=="room-early-exit")LeaveRoom();
            if(scenario=="room-phases"){
                if(RunManager.Instance.EventSynchronizer.GetLocalEvent().IsFinished)throw new NativeDataException("Event prematurely finished");
                SelectRoomOption(1);
                if(player!.Creature.MaxHp!=82||player.Creature.CurrentHp!=39)throw new NativeDataException("Independent gain-max-HP then damage contract");
            }
        }else if(scenario=="room-exit"){}
        else if(scenario is "room-smith" or "room-cancel" or "room-disabled" or "room-invalid-child"){
            if(scenario=="room-cancel"){Plan();SelectRoomOption(1);if(!player.Deck.Cards.SequenceEqual(original,ReferenceEqualityComparer.Instance)||original.Any(c=>c.CurrentUpgradeLevel!=0))throw new NativeDataException("Cancel changed permanent deck");}
            if(scenario=="room-invalid-child")NativeChoices.Fault="invalid";
            Plan(1);SelectRoomOption(1);persistentUpgrade=original[1];
        }else SelectRoomOption(0);
        if(scenario=="room-heal"&&player.Creature.CurrentHp!=64||scenario=="room-cap"&&player.Creature.CurrentHp!=80||scenario=="room-abstain"&&player.Creature.CurrentHp!=50)throw new NativeDataException("Independent room heal contract");
        if(scenario is "room-smith" or "room-cancel")if(!ReferenceEquals(player.Deck.Cards[1],original[1])||original[1].CurrentUpgradeLevel!=1||original.Where((_,i)=>i!=1).Any(c=>c.CurrentUpgradeLevel!=0))throw new NativeDataException("Independent specified permanent upgrade contract");
        Observe("room-assertion",new{session=Session,scenario,valid=true,before,after=State(),rules="4B fixed heal 24/cap80/event10; exact second permanent instance upgrade; native phase completion"});
        RoomComplete("fixture native decision complete or explicit rest leave");LeaveRoom();
        Observe("room-slice-complete",new{fixture=true,rooms=roomNumber,victories=roomVictories,persistent=NativeIdentity.Ref(persistentUpgrade),goal=true});
    }
    static void RoomsProbe()
    {
        if(NativeEventFlow.Active){EventFlowProbe();return;}
        if(scenario!="rooms-natural"){RoomFixture();return;}
        NaturalRooms();
    }
    static void NaturalRooms()
    {
        // Travel uses only the public generated map. Unknown nodes are never re-rolled or replaced.
        for(int n=0;n<12;n++){
            if(n>0&&!roomComplete)throw new NativeActionRejectedException("Travel before room completion");
            var current=run!.CurrentMapPoint!;
            int Distance(MapPoint p,HashSet<MapPoint> visited){if(!visited.Add(p))return 1000;if(p.PointType==MapPointType.RestSite)return 0;return p.Children.Count==0?1000:1+p.Children.Min(x=>Distance(x,new(visited)));}
            var children=current.Children.OrderBy(p=>p.PointType is MapPointType.Shop or MapPointType.Elite or MapPointType.Treasure?1:0).ThenBy(p=>persistentUpgrade==null?Distance(p,[]):0).ThenBy(p=>p.PointType==MapPointType.Monster?0:1).ThenBy(p=>p.coord.col).ToArray();
            if(children.Length==0||children[0].PointType==MapPointType.Boss)throw new NotSupportedException("4B natural boundary before Boss/no child");
            var choices=children.Select(p=>(object)new{from=current.coord.ToString(),to=p.coord.ToString()}).ToArray();
            var next=children[PolicyIndex("Travel",choices)];
            if(!Hook.ShouldProceedToNextMapPoint(run))throw new NativeActionRejectedException("Native map hook forbids travel");
            Action("Travel",new{from=current.coord.ToString(),to=next.coord.ToString()},()=>{
                if(sessionRoom!=null)Observe("room-exit-start",new{session=Session,room=NativeIdentity.Ref(sessionRoom)});
                Await(RunManager.Instance.EnterMapCoord(next.coord),"Actual natural map travel");
                if(run.CurrentRoom is EventRoom)EnterAuditedEvent();
                if(sessionRoom!=null)Observe("room-exit-return",new{session=Session,room=NativeIdentity.Ref(sessionRoom)});
            });
            if(sessionRoom!=null)Observe("room-complete",new{session=Session,room=NativeIdentity.Ref(sessionRoom),reason="original map travel exited previous room and root confirmed"});
            RoomArrived();
            if(NativeEventFlow.Natural&&NativeEventFlow.HollowDone){Ready();Observe("ef-complete",new{goal="natural hollow and next Play",parent=(object?)null});return;}
            if(run.CurrentRoom is CombatRoom){
                Ready();
                if(!NativeEventFlow.Active&&persistentUpgrade!=null&&roomVictories>=2){Observe("next-combat-first-play");Observe("room-slice-complete",new{fixture=false,rooms=roomNumber,victories=roomVictories,persistent=NativeIdentity.Ref(persistentUpgrade),goal=player!.Deck.Cards.Contains(persistentUpgrade)&&persistentUpgrade.CurrentUpgradeLevel==1});return;}
                Battle();if(terminal==NativeRunTerminal.Death)return;roomVictories++;Rewards();RoomComplete("native combat victory and rewards completed");
            }else if(run.CurrentRoom is RestSiteRoom rest){
                var selected=player!.Deck.Cards.Where(c=>c.IsUpgradable).ToArray();if(selected.Length==0)throw new NotSupportedException("No upgrade for natural fixture goal");
                Plan(Math.Min(1,selected.Length-1));SelectRoomOption(rest.Options.ToList().FindIndex(o=>o.OptionId=="SMITH"));persistentUpgrade=selected[Math.Min(1,selected.Length-1)];RoomComplete("native smith completed");
            }else if(run.CurrentRoom is EventRoom){
                var ev=RunManager.Instance.EventSynchronizer.GetLocalEvent();
                if(NativeEventFlow.Active&&ev is WhisperingHollow){EnableEventRewards();SelectRoomOption(0);NativeEventFlow.HollowDone=true;}
                else SelectRoomOption(ev is DenseVegetation?0:1);RoomComplete("native event finished");
                if(NativeEventFlow.Active&&!NativeEventFlow.Natural&&NativeEventFlow.HollowDone)return;
            }else throw new NotSupportedException("4B room unsupported: "+run.CurrentRoom!.GetType().Name);
            // Natural travel's original EnterMapCoord owns Exit; do not run it twice.
        }
        throw new TimeoutException("4B natural node budget");
    }
    static void EnterAuditedEvent()
    {
        // Preserve a real initialization fault before classifying an unknown model.
        NativeHost.Ensure();
        var ev=RunManager.Instance.EventSynchronizer.GetLocalEvent();
        if(NativeFullRun.Active&&ev is Amalgamator amalgamator){NativeAmalgamator.Enter(amalgamator);return;}
        if((NativeFullRun.Active||NativeEventFixtures.Active)&&ev is Reflections reflections){NativeReflections.Enter(reflections);return;}
        if((NativeFullRun.Active||NativeEventFixtures.Active)&&NativeSimpleEventEntry.Supports(ev)){NativeSimpleEventEntry.Enter(ev);return;}
        if(NativeFullRun.Active&&ev is SlipperyBridge bridge){NativeSlipperyBridge.Enter(bridge);return;}
        if (KaguyaNativeModLoop.Enabled && ev.GetType().Assembly.GetName().Name == KaguyaStage8Constants.ModId)
        {
            ev.OnRoomEnter();
            return;
        }
        if(!(ev is AbyssalBaths or SunkenStatue or DenseVegetation||(NativeEventFlow.Active||NativePreparation.Active)&&ev is WhisperingHollow or BattlewornDummy||NativeNaturalBoss.Active&&ev is AncientEventModel||NativeNaturalBoss.Act2&&ev is RanwidTheElder or DollRoom or InfestedAutomaton or StoneOfAllTime or ThisOrThat or Symbiote||NativeNaturalBoss.Third&&ev is FakeMerchant or HungryForMushrooms or PotionCourier)||ev.GetType().GetMethod(nameof(EventModel.OnRoomEnter))!.DeclaringType!=typeof(EventModel))throw new NotSupportedException("4B event model/entry not audited: "+ev.Id);
        // These sealed models inherit the no-op entry. Original AfterEventStarted
        // already ran in EventRoom.EnterInternal; no other model is inferred safe.
        ev.OnRoomEnter();
    }
}
