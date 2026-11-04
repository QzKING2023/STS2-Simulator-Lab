using System.Text.Json;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using SimulatorLab.Core;
namespace OfflineSearchHarness;

internal static partial class Program
{
 sealed record SymbioteInput(Symbiote Event,CardModel[] Deck,JsonElement Before);
 static SymbioteInput? SymbioteBefore(EventModel? ev,int selected){
  if(ev is not Symbiote sym)return null;
  if(selected!=1||sym.CurrentOptions.Count!=2||sym.CurrentOptions[1].TextKey!="SYMBIOTE.pages.INITIAL.options.KILL_WITH_FIRE"||sym.DynamicVars.Cards.IntValue!=1)throw new NotSupportedException("Symbiote only audited original one-card KillWithFire branch");
  if(!player!.Deck.Cards.Any(c=>c.IsTransformable&&c.Type!=MegaCrit.Sts2.Core.Entities.Cards.CardType.Quest))throw new NotSupportedException("Symbiote empty transform candidates not covered");
  return new(sym,player.Deck.Cards.ToArray(),State());
 }
 static void SymbioteAfter(SymbioteInput? input){
  if(input==null)return;
  var deck=player!.Deck.Cards.ToArray();var removed=input.Deck.Except(deck,ReferenceEqualityComparer.Instance).Cast<CardModel>().ToArray();var added=deck.Except(input.Deck,ReferenceEqualityComparer.Instance).Cast<CardModel>().ToArray();
  if(!input.Event.IsFinished||deck.Length!=input.Deck.Length||removed.Length!=1||added.Length!=1)throw new NativeDataException("Symbiote original transform did not replace exactly one permanent instance");
  Write("symbiote-transform.json",new{root=NativeEvidence.Root,context=NativeEvidence.Context,information="Oracle",model=input.Event.Id.ToString(),option="SYMBIOTE.pages.INITIAL.options.KILL_WITH_FIRE",before=input.Before,after=State(),removed=removed.Select(c=>new{id=Id(c),model=c.Id.ToString(),native=c.ToSerializable()}).ToArray(),added=added.Select(c=>new{id=Id(c),model=c.Id.ToString(),native=c.ToSerializable()}).ToArray(),prediction=new{source="RandomForeseer0.13.19/SymbiotePrediction->PredictDistinctDeckTransformResults",status="NotExecuted",reason="existing source reviewed; transform prediction helpers not in pinned source subset; no parallel predictor implemented"},execution="original EventOption/FromDeckForTransformation/CardCmd.TransformToRandom; native RNG, hooks, history and replacement"});
 }
}
