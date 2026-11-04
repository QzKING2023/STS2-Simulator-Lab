using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
namespace OfflineSearchHarness;

// Synchronous original calls only. No replacement, extra rules query, or Task
// completion inference. The complete projected state is observed on both sides.
internal static class NativeDeathObservation
{
 internal const string Profile="native-player-death/v3";
 internal const string OwnerProfile="native-player-death/v2";
 internal const string LegacyProfile="native-player-death/v1";
 static bool Own(Creature c)=>NativeMajor.Active&&NativeLifecycle.Active&&Program.CombatActionActive&&ReferenceEquals(c,Program.DeathSubject)&&c.IsDead;
 internal static void Install()
 {
  if(!NativeMajor.Active)return;
  NativeHost.Register(AccessTools.DeclaredMethod(typeof(Creature),nameof(Creature.RemoveAllPowersAfterDeath)),nameof(RemoveBefore),typeof(NativeDeathObservation),nameof(RemoveAfter));
  NativeHost.Register(AccessTools.PropertySetter(typeof(MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState),"Phase"),nameof(PhaseBefore),typeof(NativeDeathObservation),nameof(PhaseAfter));
  NativeHost.Register(AccessTools.PropertySetter(typeof(Creature),nameof(Creature.MaxHp)),nameof(MaxBefore),typeof(NativeDeathObservation),nameof(MaxAfter));
 }
 static void MaxBefore(Creature __instance,int __0,out (bool Death,NativeShopLedger.Write? Shop) __state){__state=(Own(__instance)&&__instance.MaxHp!=__0,NativeShopLedger.MaxBefore(__instance,__0));if(__state.Death)Program.Observe("player-death-maxhp-start",new{subject=NativeIdentity.Ref(__instance),from=__instance.MaxHp,to=__0});}
 static void MaxAfter(Creature __instance,(bool Death,NativeShopLedger.Write? Shop) __state){NativeShopLedger.MaxAfter(__instance,__state.Shop);if(__state.Death)Program.Observe("player-death-maxhp-return",new{subject=NativeIdentity.Ref(__instance),to=__instance.MaxHp});}
 internal static void Fatal(object entry,int epoch,int index)
 {
  if(entry.GetType().Name!="DamageReceivedEntry"||entry.GetType().GetProperty("Result")?.GetValue(entry) is not DamageResult result||!result.WasTargetKilled||!Own(result.Receiver))return;
  Program.Observe("player-fatal-damage",new{subject=NativeIdentity.Ref(result.Receiver),epoch,index,result=NativeObservation.Value(result),triggerSource="not-recorded-by-native-damage-history"});
 }
 static void RemoveBefore(Creature __instance,out bool __state)
 {__state=Own(__instance);if(__state)Program.Observe("player-death-powers-start",new{subject=NativeIdentity.Ref(__instance)});}
 static void RemoveAfter(Creature __instance,IEnumerable<PowerModel> __result,bool __state)
 {if(__state)Program.Observe("player-death-powers-return",new{subject=NativeIdentity.Ref(__instance),removed=__result.Select(NativeIdentity.Ref).ToArray()});}
 static void PhaseBefore(MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState __instance,PlayerTurnPhase __0,out bool __state)
 {
  __state=Program.DeathSubject is {} c&&Own(c)&&ReferenceEquals(c.Player?.PlayerCombatState,__instance)&&__instance.Phase!=__0;
  if(__state)Program.Observe("player-death-phase-start",new{subject=NativeIdentity.Ref(Program.DeathSubject),from=__instance.Phase.ToString(),to=__0.ToString()});
 }
 static void PhaseAfter(MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState __instance,PlayerTurnPhase __0,bool __state)
 {if(__state)Program.Observe("player-death-phase-return",new{subject=NativeIdentity.Ref(Program.DeathSubject),to=__0.ToString()});}
}
internal static partial class Program
{
 internal static Creature? DeathSubject=>player?.Creature;
 // Frozen policy decisions still use the original legality/ticket/effect path.
 // In particular an old potion decision is independent of today's live heuristic.
 static void ReplayPotionDecisions()
 {
  while(replayIndex<replay.Length&&replay[replayIndex].GetProperty("action").GetProperty("kind").GetString()=="UsePotion"){
   var decision=replay[replayIndex].GetProperty("action").GetProperty("decision");
   string id=decision.GetProperty("potion").GetProperty("$ref").GetString()!;
   var potion=player!.Potions.SingleOrDefault(p=>Id(p)==id)??throw new SimulatorLab.Core.NativeActionRejectedException("Frozen potion not held");
   var targetRef=decision.GetProperty("target");Creature? target=null;
   if(targetRef.ValueKind!=System.Text.Json.JsonValueKind.Null){string tid=targetRef.GetProperty("$ref").GetString()!;target=CombatManager.Instance.DebugOnlyGetState()!.Creatures.SingleOrDefault(c=>Id(c)==tid)??throw new SimulatorLab.Core.NativeActionRejectedException("Frozen potion target not in combat");}
   if(player.GetPotionSlotIndex(potion)!=decision.GetProperty("slot").GetInt32())throw new SimulatorLab.Core.NativeActionRejectedException("Frozen potion slot changed");
   UsePotionC(potion,target);
  }
 }
}
