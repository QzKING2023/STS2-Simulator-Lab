using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Events;
namespace OfflineSearchHarness;
internal static partial class Program {
 static bool DefaultAncientRelic(RelicModel? relic){
  if(relic==null)return false;
  try{_=NativeRelicPickup.Source(relic.GetType(),relic.Id.ToString());return true;}catch(NotSupportedException){return NativeAncientAcquisition.Supports(relic);}
 }
 static bool StandardAncientCallback(EventOption option){
  var callback=(Func<Task>?)typeof(EventOption).GetProperty("OnChosen",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(option);
  if(callback==null||callback.GetInvocationList().Length!=1)return false;
  var method=callback.Method;var type=method.DeclaringType;
  if(type?.DeclaringType!=typeof(AncientEventModel)||!method.Name.Contains("<RelicOption>")||method.Module!=typeof(AncientEventModel).Module||Harmony.GetPatchInfo(method) is {Owners.Count:>0})return false;
  var sm=method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
  return sm!=null&&Harmony.GetPatchInfo(AccessTools.DeclaredMethod(sm,"MoveNext")) is not {Owners.Count:>0};
 }
 static void SelectPersistentAncient(EventModel ev){
  if(ev is not AncientEventModel||ev.GetType().GetMethod("OnRoomEnter")!.DeclaringType!=typeof(EventModel))throw new NotSupportedException("Generic Ancient requires original inherited entry");
  var before=State();var options=ev.CurrentOptions.Select((o,i)=>new{index=i,option=NativeIdentity.Ref(o),o.TextKey,o.IsLocked,relic=NativeIdentity.Ref(o.Relic),model=o.Relic?.Id.ToString(),pickup=o.Relic?.HasUponPickupEffect,standardCallback=StandardAncientCallback(o)&&DefaultAncientRelic(o.Relic)}).ToArray();
  int selected=Array.FindIndex(options,x=>!x.IsLocked&&x.pickup==false&&x.standardCallback);
  Write("persistent-ancient-options-act"+run!.CurrentActIndex+".json",new{model=ev.Id.ToString(),state=before,options,prediction="unknown; native execution only",policy="first actual unlocked no-pickup relic with audited base callback"});
  if(selected<0)throw new NotSupportedException("Ancient has no supported actual persistent pickup; offers recorded");
  var offered=ev.CurrentOptions[selected].Relic!;string model=offered.Id.ToString();
  bool transaction=NativeAncientAcquisition.Supports(offered);if(transaction)NativeAncientAcquisition.Begin();
  try{SelectRoomOption(selected);}finally{NativeAncientAcquisition.Active=false;}
  var after=State();if(transaction)NativeAncientAcquisition.Complete(before,after,offered);else NativeAncientChoice.CheckPersistentPickup(before,after,model);
  if(!player!.Relics.Any(r=>ReferenceEquals(r,offered)))throw new InvalidOperationException("Ancient acquired wrong instance");
  Observe("persistent-ancient-complete",new{eventModel=ev.Id.ToString(),selected,options,before,after,offered=NativeIdentity.Ref(offered),model,transaction});RoomComplete("original base Ancient callback and actual pickup transaction");
 }
}
