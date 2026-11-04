// Scalar reuse of RandomForeseer 0.13.19 SlipperyBridgePrediction, MIT (c) 2026 hotwords123.
// Original snapshot and license are locked separately. Native event tasks own all effects.
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;
namespace OfflineSearchHarness;
internal static partial class NativeForeseerSource
{
 internal static (CardModel? Card,JsonElement Rng) SlipperyNext(SlipperyBridge slipperyBridge)
 {
  var player=slipperyBridge.Owner!;
  var rng=slipperyBridge.Rng.Clone();
  var current=slipperyBridge._randomCardToLose;
  var skipped=slipperyBridge._skippedRemovals?.ToHashSet()??[];
  // Exact upstream candidate/update body, previewCount=1; UI projection removed.
  if(current!=null)skipped.Add(current);
  var candidates=current==null?player.Deck.Cards.Where(card=>card.Rarity!=CardRarity.Basic).ToList():player.Deck.Cards.Where(card=>card.GetType()!=current.GetType()).ToList();
  candidates.RemoveAll(card=>!card.IsRemovable||skipped.Contains(card));
  if(candidates.Count==0)candidates=player.Deck.Cards.Where(card=>card.IsRemovable).ToList();
  current=rng.NextItem(candidates);
  return(current,JsonSerializer.SerializeToElement(rng.ToSerializable(),Program.Json));
 }
}
internal static class NativeSlipperyBridge
{
 internal static readonly HashSet<string> Kinds=["slippery-entry","slippery-start","slippery-return"];
 internal static void Audit(){
  foreach(string name in new[]{"OnRoomEnter","GenerateInitialOptions","GetNewRandomCard","Overcome","HoldOn"}){
   var method=AccessTools.DeclaredMethod(typeof(SlipperyBridge),name)??throw new NotSupportedException("Missing original SlipperyBridge method");
   if(method.Module!=typeof(EventModel).Module||Harmony.GetPatchInfo(method) is {Owners.Count:>0})throw new NotSupportedException("Unknown SlipperyBridge source patch");
   var stateMachine=method.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>()?.StateMachineType;
   if(stateMachine!=null&&Harmony.GetPatchInfo(AccessTools.DeclaredMethod(stateMachine,"MoveNext")) is {Owners.Count:>0})throw new NotSupportedException("Unknown SlipperyBridge async source patch");
  }
 }
 internal static JsonElement Card(CardModel? c)=>JsonSerializer.SerializeToElement(c==null?null:new{reference=NativeIdentity.Ref(c),model=c.Id.ToString(),upgrade=c.CurrentUpgradeLevel},Program.Json);
 internal static JsonElement Event(SlipperyBridge e)=>JsonSerializer.SerializeToElement(new{reference=NativeIdentity.Ref(e),model=e.Id.ToString(),holds=e._numberOfHoldOns,current=Card(e._randomCardToLose),skipped=e._skippedRemovals?.Select(c=>NativeIdentity.Get(c)).Order().ToArray()??[],rng=JsonSerializer.SerializeToElement(e.Rng.ToSerializable(),Program.Json),finished=e.IsFinished},Program.Json);
 internal static void Enter(SlipperyBridge e){
  Audit();if(NEventRoom.Instance!=null)throw new NotSupportedException("SlipperyBridge headless entry requires absent event UI");
  var before=Program.State();var eventBefore=Event(e);e.OnRoomEnter();
  NativeBoundaryContract.Snapshot(before,Program.State(),"Original SlipperyBridge null-UI entry state");NativeBoundaryContract.Snapshot(eventBefore,Event(e),"Original SlipperyBridge entry RNG");
  Program.Observe("slippery-entry",new{eventBefore,eventAfter=Event(e),presentation="original null-conditional no-UI path"});
 }
 internal static int Select(SlipperyBridge e)=>e._numberOfHoldOns==0&&e.Owner!.Creature.CurrentHp>3?1:0;
 static void Noop(){}
 internal static object Controls(){Audit();var method=AccessTools.DeclaredMethod(typeof(SlipperyBridge),"OnRoomEnter");var h=new Harmony("lab.control.slippery");bool rejected=false;try{h.Patch(method,prefix:new HarmonyMethod(typeof(NativeSlipperyBridge),nameof(Noop)));try{Audit();}catch(NotSupportedException){rejected=true;}}finally{h.UnpatchAll(h.Id);}if(!rejected)throw new NativeDataException("Slippery unknown source patch accepted");return new{actualHarmonyRejected=rejected,businessExecutions=0};}
}
internal static partial class Program
{
 sealed record SlipperyInput(SlipperyBridge Event,int Index,object Option,JsonElement Before,JsonElement EventBefore,JsonElement Forecast,JsonElement ForecastRng);
 static SlipperyInput? SlipperyBefore(EventModel? ev,int index){
  if(ev is not SlipperyBridge e)return null;
  NativeSlipperyBridge.Audit();
  if(e.CurrentOptions.Count!=2||index<0||index>1||e.CurrentOptions[0].TextKey!="SLIPPERY_BRIDGE.pages.INITIAL.options.OVERCOME"||!e.CurrentOptions[1].TextKey.Contains("HOLD_ON",StringComparison.Ordinal)||e._randomCardToLose==null||!player!.Deck.Cards.Contains(e._randomCardToLose)||!e._randomCardToLose.IsRemovable)throw new NotSupportedException("SlipperyBridge actual option/instance unsupported");
  if(index==1&&player.Creature.CurrentHp<=3+e._numberOfHoldOns)throw new NotSupportedException("SlipperyBridge lethal HoldOn outside current event contract");
  using var locked=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"foreseer-lock.json")));
  if(!locked.RootElement.GetProperty("inputs").EnumerateArray().Any(x=>x.GetProperty("path").GetString()=="RandomForeseerCode/OutOfCombat/Events/SlipperyBridgePrediction.cs"))throw new NotSupportedException("Missing pinned SlipperyBridge source");
  var before=State();var eb=NativeSlipperyBridge.Event(e);var next=NativeForeseerSource.SlipperyNext(e);var repeat=NativeForeseerSource.SlipperyNext(e);
  NativeBoundaryContract.Snapshot(before,State(),"Slippery RF state purity");NativeBoundaryContract.Snapshot(eb,NativeSlipperyBridge.Event(e),"Slippery RF event RNG purity");
  if(!ReferenceEquals(next.Card,repeat.Card)||!NativeBoundaryContract.Equal(next.Rng,repeat.Rng))throw new NativeDataException("Slippery RF repeated prediction mismatch");
  var forecast=NativeSlipperyBridge.Card(next.Card);var option=NativeIdentity.Ref(e.CurrentOptions[index])!;
  Observe("slippery-start",new{index,key=e.CurrentOptions[index].TextKey,option,eventBefore=eb,eventAfterPreview=NativeSlipperyBridge.Event(e),forecast,forecastRng=next.Rng,information="Oracle",source="RandomForeseer/0.13.19/SlipperyBridgePrediction",previewCount=1,repeatSame=true});
  return new(e,index,option,before,eb,forecast,next.Rng);
 }
 static void SlipperyAfter(SlipperyInput? input){
  if(input==null)return;var after=State();var ea=NativeSlipperyBridge.Event(input.Event);
  NativeSlipperyEvidence.Effect(input.Index,input.Before,after,input.EventBefore,ea,input.Forecast,input.ForecastRng);
  Observe("slippery-return",new{index=input.Index,option=input.Option,eventAfter=ea,forecastComparison=input.Index==1?"Exact":"NotComparedCounterfactual"});
 }
}
