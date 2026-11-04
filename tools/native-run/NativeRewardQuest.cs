using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Cards;
namespace OfflineSearchHarness;
internal static class NativeRewardQuest
{
 internal static int Gold,Completed;
 internal static void Install(){if(NativeLifecycle.Scenario=="closure-spoils")NativeHost.Register(AccessTools.DeclaredMethod(typeof(SpoilsMap),nameof(SpoilsMap.OnQuestComplete)),nameof(Before),typeof(NativeRewardQuest),nameof(After));}
 static void Before(SpoilsMap __instance)=>Program.Observe("rc-quest-start",new{id=NativeRewards.CurrentOperation.Id,quest=NativeRewardClosure.Fact(__instance),act=__instance.SpoilsActIndex,coord=__instance.SpoilsCoord?.ToString()});
 static void After(SpoilsMap __instance,ref Task<int> __result)=>__result=Observe(__result,__instance);
 static async Task<int> Observe(Task<int> task,SpoilsMap quest){int gold=await task;Gold+=gold;Completed++;Program.Observe("rc-quest-return",new{id=NativeRewards.CurrentOperation.Id,quest=NativeRewardClosure.Fact(quest),gold});return gold;}
}
