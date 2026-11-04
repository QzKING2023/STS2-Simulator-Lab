using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
namespace OfflineSearchHarness;

// Internal versioned heuristic over the current hand/deck and public intents.
// It chooses legal candidates only; native code remains responsible for effects.
internal static class NativeAct2Policy
{
 internal static bool Active=>NativeNaturalBoss.Act2&&Program.InSecondAct;
 internal static int Rank(CardModel card)
 {
  int value=card.Id.ToString() switch{
   "CARD.DARK_EMBRACE" or "CARD.FEEL_NO_PAIN"=>60,
   "CARD.STAMPEDE"=>90,
   _=>NativePreparation.CardRank(card)};
  int copies=card.Owner.Deck.Cards.Count(c=>c.Id==card.Id);
  if(card.Id.ToString() is not ("CARD.INFLAME" or "CARD.SHRUG_IT_OFF" or "CARD.POMMEL_STRIKE"))value-=60*copies;
  return value;
 }
 internal static int RewardIndex(IReadOnlyList<CardModel> cards,IReadOnlyList<CardRewardAlternative> alternatives)
 {
  int index=Enumerable.Range(0,cards.Count).OrderByDescending(i=>Rank(cards[i])).ThenBy(i=>i).First();
  if(cards[index].Owner.Deck.Cards.Count>=17&&Rank(cards[index])<110){
   for(int i=0;i<alternatives.Count;i++)if(alternatives[i].OptionId=="Skip")return cards.Count+i;
  }
  return index;
 }
 internal static int UpgradeRank(CardModel card)=>card.Id.ToString() switch{
  "CARD.INFLAME"=>220,"CARD.ARMAMENTS"=>210,"CARD.STAMPEDE"=>190,
  "CARD.OFFERING"=>180,"CARD.BASH"=>175,_=>NativePreparation.CardRank(card)};
}
