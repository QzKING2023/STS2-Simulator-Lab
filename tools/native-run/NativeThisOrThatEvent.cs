// Scalar source reuse: RandomForeseer 0.13.19, MIT, (c) 2026 hotwords123.
// The UI/pickup tip path is not initialized. See THIRD_PARTY_NOTICES.md.
using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;
namespace OfflineSearchHarness;

internal static partial class NativeForeseerSource
{
 // Exact bodies of PredictionExtensions.Clone(Rng) and the two
 // PredictRelicRewards(Player,int[,Rng]) overloads used by ThisOrThatPrediction.
 internal static Rng Clone(this Rng rng)
 {
  return new Rng(rng.ToSerializable());
 }
 internal static IReadOnlyList<RelicModel> PredictRelicRewards(Player player,int count)
 {
  return PredictRelicRewards(player,count,player.PlayerRng.Rewards.Clone());
 }
 internal static IReadOnlyList<RelicModel> PredictRelicRewards(Player player,int count,Rng rng)
 {
  var grabBag=player.RelicGrabBag.Clone();
  var relics=new List<RelicModel>();
  for(var i=0;i<count;i++){
   var rarity=RelicFactory.RollRarity(rng);
   relics.Add(grabBag.PullFromFront(rarity,player.RunState)??RelicFactory.FallbackRelic);
  }
  return relics;
 }
}

internal static partial class Program
{
 sealed record ThisOrThatInput(ThisOrThat Event,int Gold,int HpLoss,JsonElement State,string Bag,string Forecast);
 static void ValidateThisOrThatBranch(ThisOrThat model,int selected){
  if(selected!=0||model.CurrentOptions.Count!=2||model.CurrentOptions[0].TextKey!="THIS_OR_THAT.pages.INITIAL.options.PLAIN"||model.CurrentOptions[1].TextKey!="THIS_OR_THAT.pages.INITIAL.options.ORNATE")throw new NotSupportedException("ThisOrThat only audited native Plain branch");
 }
 static ThisOrThatInput? ThisOrThatBefore(EventModel? ev,int selected)
 {
  if(ev is not ThisOrThat model)return null;
  const string plain="THIS_OR_THAT.pages.INITIAL.options.PLAIN",ornate="THIS_OR_THAT.pages.INITIAL.options.ORNATE";
  ValidateThisOrThatBranch(model,selected);
  int gold=model.DynamicVars.Gold.IntValue,hp=model.DynamicVars.HpLoss.IntValue;
  if(hp!=6||gold<41||gold>69||player!.Creature.CurrentHp<=hp)throw new NotSupportedException("ThisOrThat Plain input outside audited surviving A0 scope");
  var rf=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"foreseer-lock.json"))).RootElement;
  if(!rf.GetProperty("inputs").EnumerateArray().Any(r=>r.GetProperty("path").GetString()=="RandomForeseerCode/OutOfCombat/Events/ThisOrThatPrediction.cs"))throw new NotSupportedException("ThisOrThat requires declared RF event source subset");
  var before=State();string bag=JsonSerializer.Serialize(player.RelicGrabBag.ToSerializable());int dispatchBefore=dispatched,rejected=0;
  foreach(int bad in new[]{-1,1}){try{ValidateThisOrThatBranch(model,bad);throw new NativeDataException("ThisOrThat bad/unsupported branch accepted");}catch(NotSupportedException){rejected++;}}
  var forecast=NativeForeseerSource.PredictRelicRewards(player,1).Single().Id.ToString();
  var repeat=NativeForeseerSource.PredictRelicRewards(player,1).Single().Id.ToString();
  bool pure=NativeBoundaryContract.Equal(before,State())&&bag==JsonSerializer.Serialize(player.RelicGrabBag.ToSerializable());
  Write("foreseer-this-or-that.json",new{root=NativeEvidence.Root,context=NativeEvidence.Context,information="Oracle",source="RandomForeseer0.13.19/ThisOrThatPrediction->PredictRelicRewards(Player,1)",selected=plain,forecastOption=ornate,status="CounterfactualNotExecuted",relicModel=forecast,repeatSame=forecast==repeat,before,after=State(),bagBefore=bag,bagAfter=JsonSerializer.Serialize(player.RelicGrabBag.ToSerializable()),sourceUnchanged=pure,negativeBranchChoices=rejected,zeroAdditionalDispatch=dispatchBefore==dispatched,pickupPrediction="not invoked; no UI or live-player pickup simulation",plainForecast="RF supplies no Plain result forecast; displayed native variables are observed"});
  if(!pure||forecast!=repeat||rejected!=2||dispatchBefore!=dispatched)throw new NativeDataException("ThisOrThat RF preview changed source/RNG/bag, dispatched or was not repeatable");
  return new(model,gold,hp,before,bag,forecast);
 }
 static void ThisOrThatAfter(ThisOrThatInput? input,NativeEventResourceLedger? ledger)
 {
  if(input==null)return;
  var after=State();var resources=(ledger??throw new NativeDataException("Missing original event ledger")).Complete(input.Gold,input.HpLoss);
  Write("this-or-that-plain.json",new{root=NativeEvidence.Root,context=NativeEvidence.Context,eventModel=input.Event.Id.ToString(),eventIdentity=Id(input.Event),option="THIS_OR_THAT.pages.INITIAL.options.PLAIN",visibleGold=input.Gold,visibleHpLoss=input.HpLoss,resources,before=input.State,after,bagBefore=input.Bag,bagAfter=JsonSerializer.Serialize(player!.RelicGrabBag.ToSerializable()),finished=input.Event.IsFinished,ornateForecast=input.Forecast,forecastComparison="NotCompared: Ornate not executed; Plain uses actual native effects"});
  if(!input.Event.IsFinished||player!.Creature.CurrentHp<=0)throw new NotSupportedException("ThisOrThat original event did not finish alive");
 }
}
