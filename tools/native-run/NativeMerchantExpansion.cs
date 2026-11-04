using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;
using SimulatorLab.Core;
namespace OfflineSearchHarness;
// Explicit native transaction profile. Wrappers observe original Tasks and stock methods.
internal static class NativeMerchantExpansion
{
 internal const string Profile="native-merchant-transactions/v2";
 internal static readonly HashSet<string> Fixtures=["merchant-native-relic","merchant-native-hook","merchant-native-courier","merchant-native-poor"];
 internal static readonly HashSet<string> Faults=["merchant-native-stale","merchant-native-fault"];
 internal static bool IsScenario(string s)=>Fixtures.Contains(s)||Faults.Contains(s)||s=="merchant-natural-relic";
 internal static bool Active=>IsScenario(NativeLifecycle.Scenario);
 internal static bool Natural=>NativeLifecycle.Scenario=="merchant-natural-relic";
 internal static MerchantEntry? Current;
 internal static object? Content(MerchantEntry e)=>e is MerchantRelicEntry r?r.Model:e is MerchantPotionEntry p?p.Model:e is MerchantCardEntry c?c.CreationResult?.Card:null;
 internal static object RelicState(RelicModel r)=>new{id=NativeIdentity.Get(r),model=r.Id.ToString(),status=r.Status.ToString(),r.IsUsedUp,bought=r is MawBank m?(bool?)m.HasItemBeenBought:null};
 internal static object RelicStates(MegaCrit.Sts2.Core.Entities.Players.Player? p)=>p?.Relics.Select(RelicState).ToArray()??[];
 internal static object Source(AbstractModel model)=>new{reference=NativeIdentity.Ref(model),type=model.GetType().Name,model=model.Id.ToString(),relicState=model is RelicModel relic?RelicState(relic):null};
 static readonly Dictionary<MerchantPotionEntry,int> generations=[];
 internal static object PriceSource(MerchantPotionEntry entry,System.Text.Json.JsonElement before,bool start)
 {
  // Register the same mandatory snapshot identities before reading extra source metadata.
  // These getters only observe the real generated object; no selection or RNG call.
  _=Program.StateStamp();
  if(start)generations[entry]=generations.GetValueOrDefault(entry)+1;
  return new{entry=NativeIdentity.Ref(entry),before,generation=generations[entry],offeredSource=Source(entry.Model!)};
 }
 internal static void Install()
 {
  if(!Active)return;
  foreach(var type in new[]{typeof(MerchantRelicEntry),typeof(MerchantPotionEntry)}){
   NativeHost.Register(AccessTools.DeclaredMethod(type,"OnTryPurchase"),nameof(EffectBefore),typeof(NativeMerchantExpansion),nameof(EffectAfter));
   foreach(string method in new[]{"ClearAfterPurchase","RestockAfterPurchase"})NativeHost.Register(AccessTools.DeclaredMethod(type,method),nameof(StockBefore),typeof(NativeMerchantExpansion),nameof(StockAfter));
  }
  NativeHost.Register(AccessTools.DeclaredMethod(typeof(RelicModel),"AfterObtained"),nameof(PickupBefore),typeof(NativeMerchantExpansion),nameof(PickupAfter));
 }
 static object Facts()=>new{entry=NativeIdentity.Ref(Current),offered=NativeIdentity.Ref(Current==null?null:Content(Current)),offeredSource=Source((AbstractModel)Content(Current!)!)};
 static void EffectBefore(MerchantEntry __instance){if(ReferenceEquals(__instance,Current))Program.Observe("mc-effect-start",Facts());}
 static void EffectAfter(MerchantEntry __instance,ref Task<(bool,int)> __result){if(ReferenceEquals(__instance,Current))__result=EffectTask(__result);}
 static async Task<(bool,int)> EffectTask(Task<(bool,int)> original){var result=await original;Program.Observe("mc-effect-return",new{entry=NativeIdentity.Ref(Current),success=result.Item1,spent=result.Item2});return result;}
 static void StockBefore(MerchantEntry __instance,MethodBase __originalMethod){if(ReferenceEquals(__instance,Current))Program.Observe("mc-stock-start",new{entry=NativeIdentity.Ref(Current),method=__originalMethod.Name});}
 static void StockAfter(MerchantEntry __instance,MethodBase __originalMethod){if(ReferenceEquals(__instance,Current))Program.Observe("mc-stock-return",new{entry=NativeIdentity.Ref(Current),method=__originalMethod.Name});}
 static void PickupBefore(RelicModel __instance){if(Current is MerchantRelicEntry r&&ReferenceEquals(r.Model,__instance))Program.Observe("mc-pickup-start",new{relic=NativeIdentity.Ref(__instance)});}
 static void PickupAfter(RelicModel __instance,ref Task __result){if(Current is MerchantRelicEntry r&&ReferenceEquals(r.Model,__instance))__result=PickupTask(__result,__instance);}
 static async Task PickupTask(Task original,RelicModel relic){await original;Program.Observe("mc-pickup-return",new{relic=NativeIdentity.Ref(relic)});}
 internal static void HookBefore(MerchantEntry entry){if(ReferenceEquals(entry,Current))Program.Observe("mc-hook-start",new{entry=NativeIdentity.Ref(entry)});}
 internal static void HookAfter(MerchantEntry entry,ref Task result){if(ReferenceEquals(entry,Current))result=HookTask(result,entry);}
 static async Task HookTask(Task original,MerchantEntry entry){await original;Program.Observe("mc-hook-return",new{entry=NativeIdentity.Ref(entry)});}
}
internal static partial class Program
{
 static void ExpansionBuy(MerchantEntry requested)
 {
  var inventory=NativeMerchant.Inventory!;
  var entries=inventory.AllEntries.Where(e=>e is MerchantRelicEntry or MerchantPotionEntry).ToArray();
  var decisions=entries.Select(e=>(object)new{inventory=NativeIdentity.Ref(inventory),entry=NativeIdentity.Ref(e),offered=NativeIdentity.Ref(NativeMerchantExpansion.Content(e)),cost=e.Cost}).ToArray();
  int index=replayPath!=null?PolicyIndex("MerchantBuy",decisions):Array.IndexOf(entries,requested);var entry=entries[index];
  if(entry is MerchantRelicEntry relic&&relic.Model?.HasUponPickupEffect==true)throw new NotSupportedException("Relic pickup effect requires explicit transaction contract: "+relic.Model.Id);
  if(entry is MerchantRelicEntry rr&&rr.Model!=null&&AccessTools.Method(rr.Model.GetType(),"AfterObtained").DeclaringType!=typeof(RelicModel))throw new NotSupportedException("Non-default relic AfterObtained needs explicit profile");
  foreach(var listener in player!.Relics)if(AccessTools.Method(listener.GetType(),"AfterItemPurchased").DeclaringType!=AccessTools.Method(typeof(RelicModel),"AfterItemPurchased").DeclaringType&&listener is not MawBank)throw new NotSupportedException("Unknown native purchase listener: "+listener.Id);
  NativePolicy.PublicRequests.Add(new{kind="MerchantBuy",information="player-visible-current-inventory",options=entries.Select((e,i)=>new{handle="o"+i,e.Cost,e.IsStocked,model=(NativeMerchantExpansion.Content(e) as AbstractModel)?.Id.ToString()}).ToArray()});
  string stamp=StateStamp();var ticket=new NativeDecisionTicket(inventory,sequence,stamp,"buy");
  
  ticket.Consume(inventory,sequence,StateStamp(),"buy");
  if(NativeMerchant.Closed||!ReferenceEquals(run!.CurrentRoom,MerchantRoom))throw new NativeActionRejectedException("Closed/wrong merchant room");
  MAction("MerchantBuy",decisions[index],()=>{
   var offered=NativeMerchantExpansion.Content(entry);int cost=entry.Cost,gold=player!.Gold,completed=0;string? failure=null;
   void Complete(PurchaseStatus status,MerchantEntry actual){if(!ReferenceEquals(actual,entry)||status!=PurchaseStatus.Success)throw new NativeDataException("Wrong actual purchase callback");completed++;Observe("mc-callback",new{entry=NativeIdentity.Ref(entry)});}
   void Fail(PurchaseStatus status){failure=status.ToString();Observe("mc-failed",new{entry=NativeIdentity.Ref(entry),failure});}
   entry.PurchaseCompleted+=Complete;entry.PurchaseFailed+=Fail;NativeMerchantExpansion.Current=entry;
   bool success;
   try{var task=entry.OnTryPurchaseWrapper(inventory,ignoreCost:false);Await(task,"Native relic/potion purchase including stock and Hook");success=task.GetAwaiter().GetResult();}
   finally{NativeMerchantExpansion.Current=null;entry.PurchaseCompleted-=Complete;entry.PurchaseFailed-=Fail;}
   if(scenario=="merchant-native-fault")throw new InvalidOperationException("labelled transaction fault after actual purchase");
   if(success&&!(offered is RelicModel r&&player.Relics.Contains(r)||offered is PotionModel p&&player.Potions.Contains(p)))throw new NotSupportedException("Native transaction acquisition replaced/declined offered instance");
   return new{success,cost,spent=gold-player.Gold,completed,failure,offered=NativeIdentity.Ref(offered),acquired=success?NativeIdentity.Ref(offered):null,restocked=success&&entry.IsStocked,replacement=NativeIdentity.Ref(NativeMerchantExpansion.Content(entry))};
  });
 }
 static void ExpansionProbe()
 {
  Observe("fixture-setup-declared",new{name=scenario,fixtureKind="merchant-transaction",changes="Native GainGold(1000), optional Obtain(MawBank/TheCourier), EnterRoomDebug Shop; no inventory edits"});
  MAction("MerchantSetup",new{room="Shop",gold=1000,profile=NativeMerchantExpansion.Profile},()=>{
   Await(PlayerCmd.GainGold(1000,player!),"Declared fixture gold");
   if(scenario=="merchant-native-hook")Await(RelicCmd.Obtain(ModelDb.Relic<MawBank>().ToMutable(),player!),"Declared native MawBank obtain");
   if(scenario is "merchant-native-courier" or "merchant-native-stale")Await(RelicCmd.Obtain(ModelDb.Relic<TheCourier>().ToMutable(),player!),"Declared native Courier obtain");
   Await(RunManager.Instance.EnterRoomDebug(RoomType.Shop,showTransition:false),"Original shop entry");
   MerchantRoom=(MerchantRoom)run!.CurrentRoom!;NativeMerchant.Inventory=MerchantRoom.GetLocalInventory();
   if(scenario=="merchant-native-poor")Await(PlayerCmd.LoseGold(player!.Gold,player,MegaCrit.Sts2.Core.Entities.Gold.GoldLossType.Spent),"Declared empty purse");
   return new{nativeInventory=NativeIdentity.Ref(NativeMerchant.Inventory)};
  });
  var inv=NativeMerchant.Inventory!;
  var item=scenario is "merchant-native-courier" or "merchant-native-stale"?(MerchantEntry)inv.PotionEntries[0]:inv.RelicEntries.First(e=>e.Model?.HasUponPickupEffect==false);
  var oldTicket=new NativeDecisionTicket(inv,sequence,StateStamp(),"buy");
  ExpansionBuy(item);
  if(scenario=="merchant-native-stale")oldTicket.Consume(inv,sequence,StateStamp(),"buy");
  if(scenario=="merchant-native-courier")ExpansionBuy(item);
  MAction("MerchantExit",new{inventory=NativeIdentity.Ref(inv)},()=>{Await(MerchantRoom!.Exit(run),"Original transaction shop exit");NativeMerchant.Closed=true;return new{closed=true};});
  Observe("n3d-contract",new{valid=true,scenario,fixture=true,operations=merchantOps,scope=NativeMerchantExpansion.Profile});
 }
}
