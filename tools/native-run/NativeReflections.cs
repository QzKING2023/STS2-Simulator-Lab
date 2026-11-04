// Selected RandomForeseer 0.13.19 algorithm reuse, MIT (c) 2026 hotwords123.
// Scalar projection of ReflectionsPrediction/PredictedCard/PredictionUtils.
// Native event commands own all effects; Shatter has no upstream prediction.
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;
namespace OfflineSearchHarness;
internal static class NativeReflections {
 internal static readonly HashSet<string> Kinds=["reflections-entry","reflections-start","reflections-return"];
 internal static void Audit(){
  foreach(var m in typeof(Reflections).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly)){
   if(m.Module!=typeof(EventModel).Module||Harmony.GetPatchInfo(m) is {Owners.Count:>0})throw new NotSupportedException("Unknown Reflections source patch");
   var sm=m.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>()?.StateMachineType;
   if(sm!=null&&Harmony.GetPatchInfo(AccessTools.DeclaredMethod(sm,"MoveNext")) is {Owners.Count:>0})throw new NotSupportedException("Unknown Reflections async patch");
  }
 }
 internal static JsonElement Rng(Reflections e)=>JsonSerializer.SerializeToElement(e.Rng.ToSerializable(),Program.Json);
 internal static void Enter(Reflections e){
  Audit();if(NEventRoom.Instance!=null)throw new NotSupportedException("Reflections entry requires absent event UI");
  var before=Program.State();var rng=Rng(e);e.OnRoomEnter();
  NativeBoundaryContract.Snapshot(before,Program.State(),"Original Reflections null-UI entry");NativeBoundaryContract.Snapshot(rng,Rng(e),"Reflections entry RNG");
  Program.Observe("reflections-entry",new{model=e.Id.ToString(),presentation="original null-conditional path",rng});
 }
 sealed class Predicted(CardModel original){
  CardModel? preview;
  internal CardModel Original=>original;
  internal CardModel Preview=>preview??original;
  internal CardModel MutablePreview=>preview??=(CardModel)original.MutableClone();
 }
 internal static (JsonElement Cards,JsonElement Rng) Predict(Reflections e){
  using var locked=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"foreseer-lock.json")));
  foreach(string name in new[]{"OutOfCombat/Events/ReflectionsPrediction.cs","Common/PredictedCard.cs","Common/PredictionUtils.cs"})
   if(!locked.RootElement.GetProperty("inputs").EnumerateArray().Any(x=>x.GetProperty("path").GetString()=="RandomForeseerCode/"+name))throw new NotSupportedException("Missing pinned Reflections source");
  var rng=e.Rng.Clone();var deckState=e.Owner!.Deck.Cards.Select(c=>new Predicted(c)).ToList();
  var upgraded=deckState.Where(c=>c.Preview.IsUpgraded).ToList();
  for(int i=0;i<2&&upgraded.Count>0;i++){var c=rng.NextItem(upgraded);if(c==null)break;upgraded.Remove(c);c.MutablePreview.DowngradeInternal();}
  var final=deckState.ToDictionary(c=>c.Original,c=>c.Preview.CurrentUpgradeLevel);
  var upgradable=deckState.Where(c=>c.Preview.IsUpgradable).ToList();
  for(int i=0;i<4&&upgradable.Count>0;i++){
   var c=rng.NextItem(upgradable);if(c==null)break;upgradable.Remove(c);
   // Upstream CreateUpgradedCard: clone, UpgradeInternal, FinalizeUpgradeInternal.
   var preview=(CardModel)c.Preview.MutableClone();if(preview.IsUpgradable){preview.UpgradeInternal();preview.FinalizeUpgradeInternal();}
   final[c.Original]=preview.CurrentUpgradeLevel;
  }
  return(JsonSerializer.SerializeToElement(e.Owner.Deck.Cards.Select(c=>new{id=NativeIdentity.Get(c),model=c.Id.ToString(),upgrade=final[c]}).ToArray(),Program.Json),JsonSerializer.SerializeToElement(rng.ToSerializable(),Program.Json));
 }
 internal static JsonElement Cards(Reflections e)=>JsonSerializer.SerializeToElement(e.Owner!.Deck.Cards.Select(c=>new{id=NativeIdentity.Get(c),model=c.Id.ToString(),upgrade=c.CurrentUpgradeLevel}).ToArray(),Program.Json);
}
internal static partial class Program {
 sealed record ReflectionInput(Reflections Event,int Index,JsonElement Before,JsonElement Rng,JsonElement Cards,JsonElement Forecast,JsonElement ForecastRng);
 static ReflectionInput? ReflectionBefore(EventModel? ev,int index){
  if(ev is not Reflections e)return null;NativeReflections.Audit();
  if(index is <0 or >1||e.CurrentOptions.Count!=2||e.CurrentOptions[index].TextKey!=(index==0?"REFLECTIONS.pages.INITIAL.options.TOUCH_A_MIRROR":"REFLECTIONS.pages.INITIAL.options.SHATTER"))throw new NotSupportedException("Reflections option contract");
  var before=State();var rng=NativeReflections.Rng(e);var cards=NativeReflections.Cards(e);JsonElement forecast=default,fr=default;
  if(index==0){var p=NativeReflections.Predict(e);var again=NativeReflections.Predict(e);forecast=p.Cards;fr=p.Rng;NativeBoundaryContract.Snapshot(p.Cards,again.Cards,"RF Reflections repeat cards");NativeBoundaryContract.Snapshot(p.Rng,again.Rng,"RF Reflections repeat RNG");}
  NativeBoundaryContract.Snapshot(before,State(),"RF Reflections parent state purity");NativeBoundaryContract.Snapshot(rng,NativeReflections.Rng(e),"RF Reflections parent RNG purity");
  Observe("reflections-start",new{index,option=NativeIdentity.Ref(e.CurrentOptions[index]),cards,rng,forecast=index==0?(object)forecast:null,forecastRng=index==0?(object)fr:null,prediction=index==0?"RandomForeseer/0.13.19/single-event":"UnknownShatter",information="Oracle"});
  return new(e,index,before,rng,cards,forecast,fr);
 }
 static void ReflectionAfter(ReflectionInput? p){
  if(p==null)return;var cards=NativeReflections.Cards(p.Event);var rng=NativeReflections.Rng(p.Event);
  if(!p.Event.IsFinished)throw new NativeDataException("Original Reflections task unfinished");
  if(p.Index==0){NativeBoundaryContract.Snapshot(p.Forecast,cards,"RF Reflections/native upgrades");NativeBoundaryContract.Snapshot(p.ForecastRng,rng,"RF Reflections/native RNG");}
  Observe("reflections-return",new{index=p.Index,cards,rng,finished=p.Event.IsFinished,comparison=p.Index==0?"ExactScalarCardsAndRng":"NotPredicted"});
 }
}
