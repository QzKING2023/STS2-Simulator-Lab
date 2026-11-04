using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SimulatorLab.Core;
namespace OfflineSearchHarness;

// Internal, finite Elite/Boss slice. Native methods own all effects and rewards.
internal static class NativeMajor
{
 internal static bool NaturalScenario(string s)=>NativeNaturalBoss.IsScenario(s)||s is "major-natural" or "major-prepared-natural";
 internal static bool FixtureScenario(string s)=>s is "major-elite" or "major-boss" or "major-prepared-elite" or "major-prepared-boss";
 internal static bool ControlScenario(string s)=>s is "major-action-fault" or "major-action-budget" or "major-export-fault";
 internal static bool IsScenario(string s)=>NaturalScenario(s)||FixtureScenario(s)||ControlScenario(s);
 internal static bool Active=>IsScenario(NativeLifecycle.Scenario);
}
internal static partial class Program
{
 static void MajorTravel(MapPoint next)
 {
  var current=run!.CurrentMapPoint!;
  if(!current.Children.Contains(next)||!Hook.ShouldProceedToNextMapPoint(run))throw new NativeActionRejectedException("Major illegal current map edge/guard");
  Observe("map-candidates",current.Children.OrderBy(x=>x.coord.col).Select(x=>new{from=current.coord.ToString(),to=x.coord.ToString(),type=x.PointType.ToString()}).ToArray());
  Action("Travel",new{from=current.coord.ToString(),to=next.coord.ToString()},()=>{
   RecordCustomMerchantDecline();
    var previous=run.CurrentRoom;string session=Session;
    Observe("room-exit-start",new{session,room=NativeIdentity.Ref(previous),operation="EnterMapCoord",from=current.coord.ToString(),to=next.coord.ToString()});
   Await(RunManager.Instance.EnterMapCoord(next.coord),"Major actual map travel");
    // Project in the existing canonical order before facts request a new room ID.
    // Otherwise observing a destination first renumbers its EventModel and room.
    _=State();
    Observe("room-exit-return",new{session,room=NativeIdentity.Ref(previous),operation="EnterMapCoord",from=current.coord.ToString(),to=next.coord.ToString(),arrived=NativeIdentity.Ref(run.CurrentRoom)});
   if(run.CurrentRoom is EventRoom)EnterAuditedEvent();
   if(run.CurrentRoom is CombatRoom)Ready();
    Observe("map-transition-ready",new{session,room=NativeIdentity.Ref(previous),arrived=NativeIdentity.Ref(run.CurrentRoom),from=current.coord.ToString(),to=next.coord.ToString()});
  });
   Observe("room-complete",new{session=Session,room=NativeIdentity.Ref(sessionRoom),operation="EnterMapCoord"});
  RoomArrived();
 }
 static void MajorBattleAndRewards()
 {
  var room=run!.CurrentRoom as CombatRoom??throw new NativeDataException("Major not in native combat");
  Observe("major-battle-start",new{room=NativeIdentity.Ref(room),player=NativeIdentity.Ref(player!.Creature),encounter=room.Encounter.Id.ToString(),type=room.Encounter.RoomType.ToString(),room.Encounter.ShouldGiveRewards});
  Battle();
  if(terminal==NativeRunTerminal.Death)return;
  Observe("major-battle-result",new{room=NativeIdentity.Ref(room),terminal="BattleVictory"});
  Observe("major-rewards-start",new{room=NativeIdentity.Ref(room),room.Encounter.ShouldGiveRewards});
  if(room.Encounter.ShouldGiveRewards)CAction("RewardOffer",new{room=NativeIdentity.Ref(room)},()=>Await(RewardsCmd.OfferForRoomEnd(player!,room),"Major native room-end rewards"));
  Observe("major-rewards-return",new{room=NativeIdentity.Ref(room),offered=room.Encounter.ShouldGiveRewards});
  RoomComplete("actual major battle and native rewards completed");
 }
 static void MajorProbe()
 {
  var manager=RunManager.Instance;var act=run!.Acts[run.CurrentActIndex];
  var elites=act.AllEliteEncounters.OrderBy(x=>x.Id.ToString(),StringComparer.Ordinal).ToArray();
  var bosses=act.AllBossEncounters.OrderBy(x=>x.Id.ToString(),StringComparer.Ordinal).ToArray();
  if(elites.Length==0||bosses.Length==0)throw new NotSupportedException("Default act has no declared major encounter pool");
  Observe("major-plan",new{act=act.Id.ToString(),elites=elites.Select(x=>x.Id.ToString()).ToArray(),bosses=bosses.Select(x=>x.Id.ToString()).ToArray(),target=NativeNaturalBoss.Active?"natural-generated-boss":(scenario=="major-boss"||NativePreparation.Boss)?bosses[0].Id.ToString():elites[0].Id.ToString(),selection=NativeNaturalBoss.Active?"native-map-generated-boss":"ordinal-first-native-pool",policy=NativePreparation.Active?NativePreparation.Policy:"oracle-current-intent/defend10/attackfirst/v2",natural=NativeMajor.NaturalScenario(scenario)});
  if(NativeNaturalBoss.Active){NaturalBoss();return;}
  if(NativePreparation.Active){PreparedMajor();return;}
  if(NativeMajor.FixtureScenario(scenario)||NativeMajor.ControlScenario(scenario)){
   var first=run.CurrentMapPoint!.Children.OrderBy(x=>x.PointType==MapPointType.Monster?0:1).ThenBy(x=>x.coord.col).First();
   MajorTravel(first);if(run.CurrentRoom is not CombatRoom)throw new NotSupportedException("Major registered prelude did not reach combat");
   MajorBattleAndRewards();if(terminal==NativeRunTerminal.Death)return;
   var selected=scenario=="major-boss"?bosses[0]:elites[0];
   Observe("fixture-setup-declared",new{name=scenario,fixtureKind="native-major-encounter",changes="Original EnterRoomDebug after actual normal-start Monster and rewards; no resource/HP/deck edits",encounter=selected.Id.ToString()});
   Action("MajorFixture",new{encounter=selected.Id.ToString(),type=selected.RoomType.ToString()},()=>{
    Await(manager.EnterRoomDebug(selected.RoomType,model:selected.ToMutable(),showTransition:false),"Declared major encounter fixture");Ready();
   });
   RoomArrived();MajorBattleAndRewards();if(terminal==NativeRunTerminal.Death)return;
   if(scenario=="major-boss"){
    Action("ActReady",new{act=run.CurrentActIndex,map=manager.MapSelectionSynchronizer.MapGenerationCount},()=>{
     Observe("major-ready-start",new{room=NativeIdentity.Ref(run.CurrentRoom)});NativeLifecycle.ActTask=null;manager.ActChangeSynchronizer.SetLocalPlayerReady();
     Await(manager.ActionExecutor.FinishedExecutingActions(),"Major native ready vote");
     var deadline=DateTime.UtcNow.AddSeconds(45);
     while(NativeLifecycle.ActTask==null){NativeHost.Ensure();if(DateTime.UtcNow>deadline)throw new TimeoutException("Major ready did not start actual act transition");loop.Pump(TimeSpan.FromMilliseconds(10));}
     Await(NativeLifecycle.ActTask,"Major actual EnterNextAct Task");Observe("major-ready-return",new{act=run.CurrentActIndex,map=manager.MapSelectionSynchronizer.MapGenerationCount});
    });
   }
  }else{
   // Public generated map only. Unknown rooms remain explicit Unsupported stops.
   int Distance(MapPoint p,HashSet<MapPoint> visited){if(!visited.Add(p))return 1000;if(p.PointType==MapPointType.Elite)return 0;return p.Children.Count==0?1000:1+p.Children.Min(x=>Distance(x,new(visited)));}
   bool met=false;
   for(int n=0;n<12;n++){
    var options=run.CurrentMapPoint!.Children.OrderBy(x=>Distance(x,[])).ThenBy(x=>x.coord.col).ToArray();
    if(options.Length==0)throw new NotSupportedException("Major natural path ended before Elite");
    var decisions=options.Select(x=>(object)new{from=run.CurrentMapPoint.coord.ToString(),to=x.coord.ToString()}).ToArray();
    var next=options[PolicyIndex("Travel",decisions)];MajorTravel(next);
    if(run.CurrentRoom is CombatRoom cr){met=cr.Encounter.RoomType==RoomType.Elite;MajorBattleAndRewards();if(terminal==NativeRunTerminal.Death)return;if(met)break;}
    else if(run.CurrentRoom is RestSiteRoom rest){SelectRoomOption(rest.Options.ToList().FindIndex(x=>x.OptionId=="HEAL"));RoomComplete("native heal");}
    else if(run.CurrentRoom is EventRoom){var ev=manager.EventSynchronizer.GetLocalEvent();SelectRoomOption(ev is MegaCrit.Sts2.Core.Models.Events.DenseVegetation?0:1);RoomComplete("audited event completed");}
    else throw new NotSupportedException("Major natural room outside finite slice: "+run.CurrentRoom!.GetType().Name);
   }
   if(!met)throw new TimeoutException("Major natural node budget before Elite");
  }
  NativeRewards.AssertConsumed();Observe("major-complete",new{scenario,natural=NativeMajor.NaturalScenario(scenario),goal="actual major battle rewards; Boss fixture includes actual act-ready"});
  Observe("n3c-contract",new{name=scenario,complete=true});
 }
}
