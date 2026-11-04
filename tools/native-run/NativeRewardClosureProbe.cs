using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;
namespace OfflineSearchHarness;
internal static partial class Program
{
 static void ClosureProbe()
 {
  if(scenario=="closure-neow"){
   TravelC();CardContinuityC();return;
  }
  Observe("fixture-setup-declared",new{name=scenario,fixtureKind="native-reward-closure",changes="Native Obtain/Procure/custom RewardsSet only; no reward, card, slot or RNG field edits"});
  int goldBefore=player!.Gold,deckBefore=player.Deck.Cards.Count;
  if(scenario=="closure-spoils"){
   CAction("RewardSetup",new{fixture="native quest Add then EnterAct(1), generated quest coordinate entry; not normal route"},()=>{var quest=run!.CreateCard<SpoilsMap>(player);Await(CardPileCmd.Add(quest,PileType.Deck),"Native SpoilsMap deck setup");Await(RunManager.Instance.EnterAct(1,false),"Declared native act1 map generation");if(quest.SpoilsCoord is not {} coord)throw new NotSupportedException("Native SpoilsMap did not generate target");Await(RunManager.Instance.EnterMapCoord(coord),"Declared fixture jump to native generated quest coordinate");});TreasureC(false);
  }else if(scenario=="closure-treasure"){
   CAction("RewardSetup",new{room="Treasure"},()=>Await(MegaCrit.Sts2.Core.Runs.RunManager.Instance.EnterRoomDebug(RoomType.Treasure,showTransition:false),"Declared treasure closure fixture"));TreasureC(false);
  }else if(scenario.StartsWith("closure-sozu")){
   CAction("RewardSetup",new{sozu=true,full=scenario.EndsWith("full")},()=>{
    if(scenario.EndsWith("full"))while(player.HasOpenPotionSlots)Await(PotionCmd.TryToProcure(ModelDb.Potion<BlockPotion>().ToMutable(),player),"Native fill before Sozu");
    Await(RelicCmd.Obtain(ModelDb.Relic<Sozu>().ToMutable(),player),"Native Sozu fixture");
   });
   OfferC([new PotionReward(ModelDb.Potion<FirePotion>().ToMutable(),player)]);
  }else{
   CAction("RewardSetup",new{relic=scenario.StartsWith("closure-reroll")?"Driftwood":scenario.StartsWith("closure-lens")?"FresnelLens":"none"},()=>{
    if(scenario.StartsWith("closure-reroll"))Await(RelicCmd.Obtain(ModelDb.Relic<Driftwood>().ToMutable(),player),"Native Driftwood fixture");
    if(scenario.StartsWith("closure-lens"))Await(RelicCmd.Obtain(ModelDb.Relic<FresnelLens>().ToMutable(),player),"Native FresnelLens fixture");
   });
   if(scenario=="closure-lens-add"){
    var selected=run!.CreateCard<DefendIronclad>(player);
    CAction("RewardSetup",new{source="direct-native-Add-API",selected=NativeIdentity.Ref(selected)},()=>{var op=NativeRewards.Begin("card-add",selected);NativeRewardClosure.DirectSelected=selected;NativeRewardClosure.DirectOperation=op;try{Await(CardPileCmd.Add(selected,PileType.Deck),"Native API Add with real FresnelLens replacement");NativeRewards.End(op,new{selected=NativeIdentity.Ref(selected),actual=NativeIdentity.Ref(NativeRewardClosure.DirectActual)});}finally{NativeRewardClosure.DirectOperation=null;NativeRewardClosure.DirectSelected=null;}});
   }else{
   CardReward card=scenario=="closure-lens-options"?new CardReward([run!.CreateCard<StrikeIronclad>(player),run.CreateCard<DefendIronclad>(player)],CardCreationSource.Encounter,player,CardCreationOptions.ForRoom(player,RoomType.Monster)):new CardReward(CardCreationOptions.ForRoom(player,RoomType.Monster),3,player);
   OfferC([card]);
   }
  }
  Observe("n3c-assertion",new{name=scenario,goldBefore,deckBefore});
 }
}
