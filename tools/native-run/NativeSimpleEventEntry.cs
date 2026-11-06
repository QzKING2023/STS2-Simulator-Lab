using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
namespace OfflineSearchHarness;
// Fixed game source reviewed: these entries inherit the original no-op. All options/effects remain native.
internal static class NativeSimpleEventEntry {
 // FieldOfManSizedHoles inherits OnRoomEnter. Both options use original deck
 // selectors/commands; NCardEnchantVfx.Create already returns null in TestMode.
 // No event effect or enchantment mirror is installed here.
 internal static bool Supports(EventModel e)=>e is SpiritGrafter or RoomFullOfCheese or ByrdonisNest or TheLegendsWereTrue or MorphicGrove or BrainLeech or Wellspring or TheLanternKey or Bugslayer or SapphireSeed or WarHistorianRepy or CrystalSphere or ColorfulPhilosophers or ColossalFlower or SelfHelpBook or TheFutureOfPotions or FieldOfManSizedHoles;
 internal static void Enter(EventModel e){
  if(!Supports(e)||e.GetType().GetMethod(nameof(EventModel.OnRoomEnter))!.DeclaringType!=typeof(EventModel))throw new NotSupportedException("Unreviewed simple event entry");
  foreach(var m in e.GetType().GetMethods(BindingFlags.DeclaredOnly|BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)){
   if(m.Module!=typeof(EventModel).Module||Harmony.GetPatchInfo(m) is {Owners.Count:>0})throw new NotSupportedException("Unknown simple event source patch");
   var sm=m.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
   if(sm!=null&&Harmony.GetPatchInfo(AccessTools.DeclaredMethod(sm,"MoveNext")) is {Owners.Count:>0})throw new NotSupportedException("Unknown simple event async patch");
  }
  e.OnRoomEnter();
 }
}
