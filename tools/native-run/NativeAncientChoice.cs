using System.Text.Json;
using System.Text.Json.Nodes;
using SimulatorLab.Core;
namespace OfflineSearchHarness;
internal static class NativeAncientChoice
{
 internal static void CheckPersistentPickup(JsonElement before,JsonElement after,string model){
  void Need(bool ok,string why){if(!ok)throw new NativeDataException("Ancient native pickup: "+why);}
  var old=before.GetProperty("relics").EnumerateArray().ToArray();var now=after.GetProperty("relics").EnumerateArray().ToArray();
  Need(now.Length==old.Length+1&&old.Zip(now).All(x=>NativeBoundaryContract.Equal(x.First,x.Second))&&now.Last().GetProperty("model").GetString()==model&&!old.Any(x=>x.GetProperty("id").GetString()==now.Last().GetProperty("id").GetString()),"only actual new selected relic instance");
  Need(after.GetProperty("lifecycle").GetProperty("eventModel").GetProperty("IsFinished").GetBoolean(),"original event task completed");
  var expected=JsonNode.Parse(before.GetRawText())!;expected["relics"]=JsonNode.Parse(after.GetProperty("relics").GetRawText());
  expected["rooms"]!["eventOptions"]=new JsonArray();
  expected["lifecycle"]!["eventModel"]!["IsFinished"]=true;
  Need(NativeBoundaryContract.Equal(after,JsonSerializer.SerializeToElement(expected)),"persistent-only complete pickup frame including all RNG/deck/resources");
 }
}

internal static partial class Program
{
 static void SelectNonupeipe(MegaCrit.Sts2.Core.Models.EventModel ev){
  if(ev.GetType()!=typeof(MegaCrit.Sts2.Core.Models.Events.Nonupeipe)||ev.GetType().GetMethod("OnRoomEnter")!.DeclaringType!=typeof(MegaCrit.Sts2.Core.Models.EventModel))throw new NotSupportedException("Ancient entry outside audited inherited no-op source");
  var before=State();var options=ev.CurrentOptions.Select((o,i)=>new{index=i,option=NativeIdentity.Ref(o),key=o.TextKey,o.IsLocked,relic=NativeIdentity.Ref(o.Relic),model=o.Relic?.Id.ToString(),pickup=o.Relic?.HasUponPickupEffect}).ToArray();
  Write("third-ancient-options.json",new{information="Oracle adapter capability; offered choices visible",eventModel=ev.Id.ToString(),state=before,options,prediction=new{source="RandomForeseer0.13.19/RelicPickupPrediction",status="NotExecuted",scope="existing BeautifulBracelet preview reviewed; not selected by the no-pickup policy; no parallel predictor implemented"}});
  int index=Array.FindIndex(options,o=>!o.IsLocked&&o.pickup==false&&o.model!=null);
  if(index<0)throw new NotSupportedException("Nonupeipe has no offered persistent-only pickup; choices recorded");
  var offered=ev.CurrentOptions[index].Relic!;string model=offered.Id.ToString();
  SelectRoomOption(index);var after=State();NativeAncientChoice.CheckPersistentPickup(before,after,model);
  if(!player!.Relics.Any(r=>ReferenceEquals(r,offered)))throw new NativeDataException("Native Ancient did not obtain the actual offered instance");
  Write("third-ancient-pickup.json",new{root=NativeEvidence.Root,context=NativeEvidence.Context,eventModel=ev.Id.ToString(),index,key=options[index].key,offered=NativeIdentity.Ref(offered),model,before,after,policy="first offered unlocked relic with no UponPickup effect; audited native base RelicOption callback"});
  Observe("natural-third-ancient-selected",new{model=ev.Id.ToString(),option=options[index].option,key=options[index].key});RoomComplete("actual Nonupeipe option/native RelicCmd and event completion");
 }
}
