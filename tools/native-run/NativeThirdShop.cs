using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
namespace OfflineSearchHarness;

// Internal composition of native merchant transactions; no stock/resource fixtures.
internal static class NativeThirdShop
{
 internal static bool IsScenario(string s)=>s=="major-natural-act3-route"||NativeSolverJourney.ShopScenario(s);
 internal static bool Active=>IsScenario(NativeLifecycle.Scenario);
 internal static bool ValidAct(string s,int act)=>NativeFullRun.Active?act is 0 or 1 or 2:NativeDarv.IsActTwoProfile(s)?act is 0 or 1:s=="major-natural-act3-route"?act==2:NativeSolverJourney.ShopScenario(s)&&act==0;
 internal const string Profile="natural-third-shop/v1";
 internal static void Install(){
  if(!Active)return;
  NativeMerchantEconomy.InstallPriceOnly();
  NativeShopLedger.Install();
  NativeHost.Register(AccessTools.DeclaredMethod(typeof(MerchantRoom),"Exit"),nameof(ExitBefore),typeof(NativeThirdShop),nameof(ExitAfter));
 }
 static void ExitBefore(MerchantRoom __instance){
  if(!ReferenceEquals(__instance,Program.MerchantRoom)||NativeMerchant.Closed)throw new InvalidOperationException("Third shop exit wrong inventory/room");
  Program.Observe("third-shop-exit-start",new{room=NativeIdentity.Ref(__instance),inventory=NativeIdentity.Ref(NativeMerchant.Inventory)});
 }
 static void ExitAfter(ref Task __result)=>__result=ExitTask(__result);
 static async Task ExitTask(Task task){await task;NativeMerchant.Closed=true;Program.Observe("third-shop-exit-return",new{room=NativeIdentity.Ref(Program.MerchantRoom),inventory=NativeIdentity.Ref(NativeMerchant.Inventory)});}
}

internal static partial class Program
{
 static void ThirdShop()
 {
  if(!NativeThirdShop.Active||!NativeThirdShop.ValidAct(scenario,run!.CurrentActIndex)||run.CurrentRoom is not MerchantRoom shop)throw new InvalidOperationException("Integrated shop outside registered actual natural room/act");
  Action("ThirdShop",new{room=NativeIdentity.Ref(shop),profile=NativeThirdShop.Profile},()=>{
   MerchantRoom=shop;NativeMerchant.Inventory=shop.GetLocalInventory();NativeMerchant.Closed=false;
   var inventory=NativeMerchant.Inventory;
   Observe("third-shop-attached",new{room=NativeIdentity.Ref(shop),inventory=NativeIdentity.Ref(inventory)});
   // No alternative effect execution: only offered default pickups are selected.
   // Transactions with new selection/refill semantics stop explicitly.
   if(player!.Relics.Any(r=>r is TheCourier))throw new NotSupportedException("Integrated shop Courier refill needs its own session contract");
   for(int n=0;n<6;n++){
    string before=StateStamp();
    var relic=inventory.RelicEntries.Where(e=>e.IsStocked&&e.Cost<=player.Gold&&e.Model is not (TheCourier or MawBank)&&e.Model?.HasUponPickupEffect==false&&AccessTools.Method(e.Model.GetType(),"AfterObtained").DeclaringType==typeof(RelicModel)).OrderBy(e=>e.Cost).FirstOrDefault();
    var card=inventory.CardEntries.Where(e=>e.IsStocked&&e.Cost<=player.Gold&&e.CreationResult!=null&&NativePreparation.CardRank(e.CreationResult.Card)>=125&&player.Deck.Cards.Count(c=>c.Id==e.CreationResult.Card.Id)<2).OrderByDescending(e=>NativePreparation.CardRank(e.CreationResult!.Card)).FirstOrDefault();
    var potion=player.HasOpenPotionSlots&&!player.Relics.Any(r=>r.Id.ToString()=="RELIC.SOZU")?inventory.PotionEntries.FirstOrDefault(e=>e.IsStocked&&e.Cost<=player.Gold&&e.Model?.Id.ToString() is "POTION.EXPLOSIVE_AMPOULE" or "POTION.FIRE_POTION" or "POTION.BLOCK_POTION" or "POTION.SWIFT_POTION"):null;
    MerchantEntry? entry=relic??(MerchantEntry?)potion??card;
    if(StateStamp()!=before)throw new NativeDataException("Third shop policy changed state/RNG");
    if(entry==null)break;
    object offered=entry is MerchantRelicEntry r?r.Model!:entry is MerchantPotionEntry pot?pot.Model!:((MerchantCardEntry)entry).CreationResult!.Card;
    int gold=player.Gold,cost=entry.Cost,callbacks=0;string? failure=null;
    var deck=player.Deck.Cards.ToArray();var relics=player.Relics.ToArray();var potions=player.Potions.ToArray();
    Observe("third-shop-buy-start",new{index=n,entry=NativeIdentity.Ref(entry),offered=NativeIdentity.Ref(offered),cost,model=((AbstractModel)offered).Id.ToString(),creature=NativeIdentity.Ref(player.Creature)});
    void Completed(PurchaseStatus status,MerchantEntry actual){if(status!=PurchaseStatus.Success||!ReferenceEquals(actual,entry))throw new NativeDataException("Third shop native callback mismatch");callbacks++;}
    void Failed(PurchaseStatus status)=>failure=status.ToString();
    entry.PurchaseCompleted+=Completed;entry.PurchaseFailed+=Failed;
    bool success;int paid;NativeShopLedger.Begin(entry);
    try{var task=entry.OnTryPurchaseWrapper(inventory,ignoreCost:false);Await(task,"Natural third shop original purchase and hooks");success=task.GetAwaiter().GetResult();paid=NativeShopLedger.Complete(cost);}
    finally{entry.PurchaseCompleted-=Completed;entry.PurchaseFailed-=Failed;NativeShopLedger.Clear();}
    if(!success||callbacks!=1||failure!=null||paid!=cost||entry.IsStocked)throw new NativeDataException("Third shop actual purchase/stock/payment mismatch");
    var acquired=player.Deck.Cards.Except(deck).Cast<object>().Concat(player.Relics.Except(relics)).Concat(player.Potions.Except(potions)).ToArray();
    if(acquired.Length!=1||!ReferenceEquals(acquired[0],offered))throw new NotSupportedException("Third shop native replacement/decline requires explicit result contract");
    Observe("third-shop-buy-return",new{index=n,entry=NativeIdentity.Ref(entry),acquired=NativeIdentity.Ref(offered),cost,spent=paid,netSpent=gold-player.Gold,callbacks,success});
   }
   Observe("third-shop-complete",new{room=NativeIdentity.Ref(shop),inventory=NativeIdentity.Ref(inventory)});
  });
  RoomComplete("actual natural shop purchases complete; next Travel owns native Exit");
 }
}
