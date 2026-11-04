using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Commands;
using SimulatorLab.Core;
namespace OfflineSearchHarness;
// Finite event flow. All rewards and room changes execute the original game Tasks.
internal static class NativeEventFlow
{
 internal static bool NaturalScenario(string s)=>s=="event-natural";
 internal static bool FixtureScenario(string s)=>s=="event-combat";
 internal static bool FaultScenario(string s)=>s is "event-resume-fault" or "event-resume-budget";
 internal static bool IsScenario(string s)=>NaturalScenario(s)||FixtureScenario(s)||FaultScenario(s);
 internal static bool Active=>IsScenario(NativeLifecycle.Scenario);
 internal static bool Natural=>NaturalScenario(NativeLifecycle.Scenario);
 internal static bool Unified,HollowDone,Aggressive;
 internal static Task? ResumeTask;
 static int resumeNumber;
 internal static void Install(){if(Active)NativeHost.Register(AccessTools.DeclaredMethod(typeof(BattlewornDummy),nameof(EventModel.Resume)),nameof(ResumeBefore),typeof(NativeEventFlow),nameof(ResumeAfter));}
 sealed record ResumeScope(string Id,EventModel Event,AbstractRoom Exited);
 static void ResumeBefore(BattlewornDummy __instance,AbstractRoom room,out ResumeScope __state){__state=new("resume"+(++resumeNumber),__instance,room);Program.Observe("ef-resume-start",new{id=__state.Id,eventModel=NativeIdentity.Ref(__instance),exited=NativeIdentity.Ref(room)});}
 static void ResumeAfter(ref Task __result,ResumeScope __state)=>__result=ResumeTask=Track(__result,__state);
 static async Task Track(Task task,ResumeScope s){
  try{await task;
   if(NativeLifecycle.Scenario=="event-resume-budget")await Task.Delay(60000); // labelled held completion; process exits at its explicit bounded wait
   if(NativeLifecycle.Scenario=="event-resume-fault")throw new InvalidOperationException("labelled fault after actual native EventModel.Resume");
   Program.Observe("ef-resume-return",new{id=s.Id,eventModel=NativeIdentity.Ref(s.Event),exited=NativeIdentity.Ref(s.Exited)});
  }catch(Exception ex){Program.Observe("ef-resume-fault",new{id=s.Id,eventModel=NativeIdentity.Ref(s.Event),exited=NativeIdentity.Ref(s.Exited),error=ex.Message});throw;}
 }
}
internal static partial class Program
{
 static Func<RewardsSet,Task>? eventOldSelector;
 static void EnableEventRewards(){
  if(NativeEventFlow.Unified)throw new NativeDataException("Duplicate unified event scope");
  if(replayPath!=null){var rp=Path.Combine(Path.GetDirectoryName(replayPath)!,"reward-choices.json");if(!File.Exists(rp))throw new NativeActionRejectedException("Missing frozen event reward transcript");NativeRewards.LoadReplay(rp);}
  var before=State();NativeEventFlow.Unified=true;eventOldSelector=RewardsSet.testSelector;RewardsSet.testSelector=NativeRewards.Collect;
  Observe("ef-enable",new{before,scope="inventory projection and native unified rewards; existing state unchanged"});
 }
 static void EventFlowProbe(){
  try{
   if(NativeEventFlow.Natural){NaturalRooms();if(!NativeEventFlow.HollowDone)throw new NotSupportedException("Natural slice did not reach Hollow");}
   else{
    Observe("fixture-setup-declared",new{name=scenario,fixtureKind="native-event-combat",changes="Normal-start actual rooms and first visible attack card rewards, then declared native BattlewornDummy room; no direct resource/HP/deck edits"});
    NativeEventFlow.Aggressive=true;NaturalRooms();if(!NativeEventFlow.HollowDone)throw new NotSupportedException("Fixed natural prelude did not reach Hollow");
    CAction("EventSetup",new{model="EVENT.BATTLEWORN_DUMMY"},()=>{Await(RunManager.Instance.EnterRoomDebug(RoomType.Event,model:ModelDb.Event<BattlewornDummy>(),showTransition:false),"Declared native event fixture");EnterAuditedEvent();});
    RoomArrived();var parent=run!.CurrentRoom!;var ev=RunManager.Instance.EventSynchronizer.GetLocalEvent();
    Observe("ef-parent",new{parent=NativeIdentity.Ref(parent),eventModel=NativeIdentity.Ref(ev),model=ev.Id.ToString()});
    SelectRoomOption(0);Ready();NativeEventFlow.Aggressive=true;
    var brew=player!.Potions.FirstOrDefault(p=>p.Id.ToString()=="POTION.GAMBLERS_BREW");
    if(brew!=null){if(!NativeChoices.Replaying)Plan(Enumerable.Range(0,player.PlayerCombatState!.Hand.Cards.Count).ToArray());UsePotionC(brew,player.Creature);}
    var earned=player!.Potions.FirstOrDefault(p=>p.Id.ToString()=="POTION.EXPLOSIVE_AMPOULE");
    if(earned!=null){var combat=MegaCrit.Sts2.Core.Combat.CombatManager.Instance.DebugOnlyGetState()!;var target=new MegaCrit.Sts2.Core.Entities.Creatures.Creature?[]{null}.Concat(combat.Creatures.Cast<MegaCrit.Sts2.Core.Entities.Creatures.Creature?>()).First(t=>earned.IsValidTarget(t));UsePotionC(earned,target);}
    var child=(CombatRoom)run.CurrentRoom!;
    Observe("ef-subcombat",new{parent=NativeIdentity.Ref(parent),child=NativeIdentity.Ref(child),eventModel=NativeIdentity.Ref(ev),encounter=child.Encounter.Id.ToString(),child.ShouldResumeParentEventAfterCombat,child.ParentEventId});
    Battle();if(terminal==NativeRunTerminal.Death)return;
    Observe("ef-battle-complete",new{child=NativeIdentity.Ref(child),victories=NativeHost.BattleVictories,timedOut=((MegaCrit.Sts2.Core.Models.Encounters.BattlewornDummyEventEncounter)child.Encounter).RanOutOfTime});
    CAction("EventRewards",new{source="native event combat room end"},()=>Await(RewardsCmd.OfferForRoomEnd(player!,child),"Original event combat rewards"));
    CAction("EventProceed",new{parent=NativeIdentity.Ref(parent),child=NativeIdentity.Ref(child)},()=>{
     Observe("ef-proceed-start",new{parent=NativeIdentity.Ref(parent),child=NativeIdentity.Ref(child)});
     Await(RunManager.Instance.ProceedFromTerminalRewardsScreen(),"Original terminal reward proceed");
     Observe("ef-room-resume-return",new{parent=NativeIdentity.Ref(parent),child=NativeIdentity.Ref(child)});
     var actual=NativeEventFlow.ResumeTask??throw new NativeDataException("No actual EventModel.Resume task");
     if(scenario=="event-resume-budget")loop.RunUntilCompleted(actual,TimeSpan.FromMilliseconds(50),"Labelled bounded actual Resume task wait");else Await(actual,"Actual EventModel.Resume completion");
     NativeHost.Ensure();
     if(!ReferenceEquals(run.CurrentRoom,parent)||!ev.IsFinished)throw new NativeDataException("Native event not actually restored/finished");
     if(((MegaCrit.Sts2.Core.Models.Encounters.BattlewornDummyEventEncounter)child.Encounter).RanOutOfTime)throw new NotSupportedException("Native dummy timed out; no reward/resume success claim");
     Observe("ef-proceed-return",new{parent=NativeIdentity.Ref(parent),child=NativeIdentity.Ref(child)});
    });
    RoomComplete("actual EventModel.Resume finished");
    var current=run.CurrentMapPoint!;var children=current.Children.OrderBy(x=>x.coord.col).ThenBy(x=>x.coord.row).ToArray();
    if(children.Length==0||!MegaCrit.Sts2.Core.Hooks.Hook.ShouldProceedToNextMapPoint(run))throw new NativeActionRejectedException("Native event exit has no legal next node");
    var choices=children.Select(x=>(object)new{from=current.coord.ToString(),to=x.coord.ToString()}).ToArray();var next=children[PolicyIndex("Travel",choices)];
    Action("Travel",new{from=current.coord.ToString(),to=next.coord.ToString()},()=>{
     Observe("room-exit-start",new{session=Session,room=NativeIdentity.Ref(parent)});
     Await(RunManager.Instance.EnterMapCoord(next.coord),"Original map travel after event Resume");
     if(run.CurrentRoom is not CombatRoom)throw new NotSupportedException("Fixed event continuation reached non-combat");Ready();
     Observe("room-exit-return",new{session=Session,room=NativeIdentity.Ref(parent)});
    });
    Observe("room-complete",new{session=Session,room=NativeIdentity.Ref(parent),reason="native map travel exited restored event"});RoomArrived();
    Observe("ef-complete",new{goal="actual event combat reward resume and legal next Play",parent=NativeIdentity.Ref(parent)});
   }
   NativeRewards.AssertConsumed();Observe("n3c-contract",new{name=scenario,complete=true});
  }finally{if(NativeEventFlow.Unified){RewardsSet.testSelector=eventOldSelector;Write("reward-choices.json",NativeRewards.Transcript);}}
 }
}
