using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
namespace OfflineSearchHarness;
internal static class NativeEventFixtures {
 internal static readonly HashSet<string> Scenarios=["event-reflections-touch","event-reflections-shatter","event-crystal-gold","event-crystal-debt"];
 internal static bool Active=>Scenarios.Contains(NativeLifecycle.Scenario);
}
internal static partial class Program {
 static void AcceptanceEventFixture(){
  bool reflection=scenario.StartsWith("event-reflections");int choice=scenario.EndsWith("touch")||scenario.EndsWith("gold")?0:1;
  Observe("fixture-setup-declared",new{name=scenario,fixtureKind="room",changes="Original EnterRoomDebug after normal Neow; Reflections upgrades first two cards via CardCmd; Crystal fixture grants 100 gold via PlayerCmd. Forced event bypasses natural IsAllowed selection, not a natural run."});
  Action("RoomFixtureSetup",new{scenario,choice},()=>{
   if(reflection){foreach(var c in player!.Deck.Cards.Take(2))CardCmd.Upgrade(c);}
   else Await(PlayerCmd.GainGold(100,player!),"Declared Crystal fixture gold");
   Await(RunManager.Instance.EnterRoomDebug(RoomType.Event,model:reflection?ModelDb.Event<Reflections>():ModelDb.Event<CrystalSphere>(),showTransition:false),"Declared native event fixture");EnterAuditedEvent();
  });RoomArrived();
  var original=player!.Deck.Cards.ToArray();int gold=player.Gold;
  SelectRoomOption(choice);
  var ev=RunManager.Instance.EventSynchronizer.GetLocalEvent();if(!ev.IsFinished)throw new NativeDataException("Fixture native event not finished");
  if(reflection&&choice==1){
   if(player.Deck.Cards.Count!=original.Length*2+1||!original.Select((c,i)=>ReferenceEquals(c,player.Deck.Cards[i])).All(x=>x)||player.Deck.Cards.Select(NativeIdentity.Get).Distinct().Count()!=player.Deck.Cards.Count)throw new NativeDataException("Shatter original/copy unique identity contract");
   for(int i=0;i<original.Length;i++){var c=player.Deck.Cards[i+original.Length];if(c.Id!=original[i].Id||c.CurrentUpgradeLevel!=original[i].CurrentUpgradeLevel||ReferenceEquals(c,original[i]))throw new NativeDataException("Shatter native cloned card contract");}
   if(player.Deck.Cards.Last().Id.ToString()!="CARD.BAD_LUCK")throw new NativeDataException("Shatter curse contract");
  }
  if(!reflection){NativeCrystalSphere.EnsureCompleted();if(NativeCrystalSphere.Clicks!=(choice==0?3:6))throw new NativeDataException("Crystal original click count");}
  Observe("room-assertion",new{session=Session,scenario,valid=true,beforeGold=gold,afterGold=player.Gold,changes="native event completion; exact Reflections scalar forecast or unique clone identities; Crystal original finite clicks"});
  RoomComplete("original event Task completed");LeaveRoom();Observe("room-slice-complete",new{fixture=true,rooms=roomNumber,victories=0,goal=true});
 }
}
