using HarmonyLib;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
namespace OfflineSearchHarness;

// Explicit API fixture: two real rooms, take then skip, native manager owns Exit.
internal static class NativeRepeatTreasure
{
 internal static bool Active=>NativeLifecycle.Scenario=="reward-repeat-treasure";
 internal static void Install(){if(Active)NativeHost.Register(AccessTools.DeclaredMethod(typeof(TreasureRoom),"Exit"),nameof(Before),typeof(NativeRepeatTreasure),nameof(After));}
 static void Before(TreasureRoom __instance,out NativeRewards.Operation? __state){__state=Active?NativeRewards.Begin("treasure-exit",__instance):null;}
 static void After(ref Task __result,NativeRewards.Operation? __state){if(__state!=null)__result=Finish(__result,__state);}
 static async Task Finish(Task task,NativeRewards.Operation op){await task;NativeRewards.End(op,new{pickingClosed=RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics==null});}
}
internal static partial class Program
{
 static void RepeatTreasure()
 {
  CAction("RewardSetup",new{room="Treasure",fixture="first real chest"},()=>Await(RunManager.Instance.EnterRoomDebug(RoomType.Treasure,showTransition:false),"First native chest fixture"));
  TreasureC(false);
  CAction("RewardSetup",new{room="Treasure",fixture="second real chest; original transition exits first"},()=>Await(RunManager.Instance.EnterRoomDebug(RoomType.Treasure,showTransition:false),"Second native chest fixture"));
  TreasureC(true);
  CAction("RewardSetup",new{room="RestSite",fixture="original transition exits second; no rest effect"},()=>Await(RunManager.Instance.EnterRoomDebug(RoomType.RestSite,showTransition:false),"Exit second native chest fixture"));
 }
}
