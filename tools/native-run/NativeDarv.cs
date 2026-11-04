using System.Text.Json;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
namespace OfflineSearchHarness;
internal static class NativeDarv
{
 internal static bool IsActTwoProfile(string s)=>s is "solver-native-darv-act2" or "solver-native-darv-act2-replay";
 internal static bool IsScenario(string s)=>IsActTwoProfile(s)||s is "solver-native-darv" or "solver-native-darv-replay";
 internal static bool Active=>IsScenario(NativeLifecycle.Scenario);
 // Known persistent-only pickups; original effects remain native. No Boss ranking.
 internal static readonly string[] SimpleRelics=["RELIC.BLACK_STAR","RELIC.RUNIC_PYRAMID","RELIC.SOZU","RELIC.ECTOPLASM","RELIC.PHILOSOPHERS_STONE","RELIC.VELVET_CHOKER"];
 internal static void CheckPickup(JsonElement before,JsonElement after,string model){
  if(!SimpleRelics.Contains(model))throw new NotSupportedException("Unsupported Darv persistent pickup contract");
  NativeAncientChoice.CheckPersistentPickup(before,after,model);
 }

}
internal static partial class Program
{
 static void SelectDarvOption(EventModel ev){
  if(ev is not Darv)throw new NotSupportedException("Expected original generated Darv");
  var before=State();string bag=JsonSerializer.Serialize(player!.RelicGrabBag.ToSerializable());
  var options=ev.CurrentOptions.Select((o,i)=>new{index=i,reference=NativeIdentity.Ref(o),key=o.TextKey,o.IsLocked,relic=o.Relic?.Id.ToString(),pickup=o.Relic?.HasUponPickupEffect}).ToArray();
  var predictions=new List<object>();
  foreach(var option in ev.CurrentOptions){
   if(option.Relic is MegaCrit.Sts2.Core.Models.Relics.CallingBell){
    var relics=NativeForeseerSource.PredictRelicRewards(player,[RelicRarity.Common,RelicRarity.Uncommon,RelicRarity.Rare]);
    predictions.Add(new{option=NativeIdentity.Ref(option),source="RandomForeseer/0.13.19/OutOfCombatPredictionUtils.PredictRelicRewards",status="AvailableNotSelected",models=relics.Select(x=>x.Id.ToString()).ToArray(),scope="offered relic IDs only; excludes curse and nested pickup effects"});
   }else predictions.Add(new{option=NativeIdentity.Ref(option),source="RandomForeseer/0.13.19/RelicPickupPrediction",status="UnsupportedBySelectedSourceSubset",models=Array.Empty<string>(),scope="native execution remains separate"});
  }
  var after=State();bool pure=NativeBoundaryContract.Equal(before,after)&&bag==JsonSerializer.Serialize(player.RelicGrabBag.ToSerializable());
  Write("foreseer-darv-options.json",new{information="Oracle",root=NativeEvidence.Root,context=NativeEvidence.Context,before,after,options,predictions,bagBefore=bag,bagAfter=JsonSerializer.Serialize(player.RelicGrabBag.ToSerializable()),sourceUnchanged=pure});
  if(!pure)throw new NativeDataException("Foreseer option prediction mutated native state/RNG/grab bag");
  int index=Array.FindIndex(options,o=>!o.IsLocked&&o.pickup==false&&NativeDarv.SimpleRelics.Contains(o.relic));
  if(index<0)throw new NotSupportedException("Darv offers no declared persistent-only pickup; options and prediction coverage recorded");
  var selected=ev.CurrentOptions[index];var model=selected.Relic!.Id.ToString();
  SelectRoomOption(index);var result=State();NativeDarv.CheckPickup(before,result,model);
  Write("darv-selection.json",new{information="player-visible-options",root=NativeEvidence.Root,context=NativeEvidence.Context,before,after=result,index,key=selected.TextKey,option=NativeIdentity.Ref(selected),relic=model,policy="first native offered option with declared persistent-only pickup; no search scoring"});
  Observe("natural-ancient-selected",new{model=ev.Id.ToString(),option=NativeIdentity.Ref(selected),key=selected.TextKey,relic=model});RoomComplete("original Darv choice task and native persistent relic pickup completed");
 }
}
