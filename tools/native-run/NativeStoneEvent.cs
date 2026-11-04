using System.Reflection;
using System.Text.Json;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
namespace OfflineSearchHarness;
internal static partial class Program
{
 // Reuses the normal event synchronizer/task bridge. Only the audited native
 // Lift trade is admitted; Push and its deck-enchantment selection stay explicit
 // unsupported. No event effect or prediction is reimplemented here.
 sealed record StoneLiftInput(StoneOfAllTime Event,PotionModel Potion,int MaxHp,JsonElement State,bool PotionLock);
 static StoneLiftInput? StoneLiftBefore(EventModel? ev,int selected)
 {
  if(ev is not StoneOfAllTime stone)return null;
  if(selected!=0||stone.CurrentOptions[selected].TextKey!="STONE_OF_ALL_TIME.pages.INITIAL.options.LIFT")throw new NotSupportedException("StoneOfAllTime only audited native Lift trade");
  var chosen=(PotionModel?)typeof(StoneOfAllTime).GetField("_drinkAndLiftPotion",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(stone);
  if(chosen==null||!player!.Potions.Contains(chosen)||player.CanUseOrRemovePotions)throw new NativeDataException("Stone Lift actual selected potion/lock precondition");
  return new(stone,chosen,player.Creature.MaxHp,State(),!player.CanUseOrRemovePotions);
 }
 static void StoneLiftAfter(StoneLiftInput? input)
 {
  if(input==null)return;
  var after=State();bool removed=!player!.Potions.Contains(input.Potion),unlocked=player.CanUseOrRemovePotions;
  Write("stone-lift.json",new{root=NativeEvidence.Root,context=NativeEvidence.Context,eventModel=input.Event.Id.ToString(),eventIdentity=Id(input.Event),option="STONE_OF_ALL_TIME.pages.INITIAL.options.LIFT",potion=Id(input.Potion),model=input.Potion.Id.ToString(),before=input.State,after,removed,lockedBefore=input.PotionLock,unlockedAfter=unlocked,finished=input.Event.IsFinished,forecast="not covered by selected RandomForeseer subset; native execution only"});
  if(!removed||!unlocked||!input.Event.IsFinished||player.Creature.MaxHp!=input.MaxHp+10)throw new NotSupportedException("Stone Lift result outside audited fixed-version trade contract");
 }
}
