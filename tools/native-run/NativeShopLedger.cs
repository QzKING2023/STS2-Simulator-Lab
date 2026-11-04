using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Merchant;

namespace OfflineSearchHarness;

// Observe original commands and synchronous writes. No effects, resource edits,
// modifier queries or replacement Tasks are executed in place of game logic.
internal static class NativeShopLedger
{
 internal sealed class Command(string id,string kind,decimal amount,string reason){internal readonly string Id=id,Kind=kind,Reason=reason;internal readonly decimal Amount=amount;}
 internal sealed class Write(string field,int from,int to,object subject,string? command){internal readonly string Field=field;internal readonly int From=from,To=to;internal readonly object Subject=subject;internal readonly string? Command=command;}
 internal static MerchantEntry? Entry;
 static readonly Stack<Command> commands=[];
 static int serial,paid,hooks;
 internal static void Install(){
  if(!NativeThirdShop.Active)return;
  NativeHost.Register(AccessTools.PropertySetter(typeof(Player),nameof(Player.Gold)),nameof(GoldBefore),typeof(NativeShopLedger),nameof(GoldAfter));
  NativeHost.Register(AccessTools.PropertySetter(typeof(Creature),nameof(Creature.CurrentHp)),nameof(HpBefore),typeof(NativeShopLedger),nameof(HpAfter));
  // MaxHp is already audited by NativeDeathObservation; share that observer.
  NativeHost.Register(AccessTools.DeclaredMethod(typeof(PlayerCmd),nameof(PlayerCmd.GainGold)),nameof(GainBefore),typeof(NativeShopLedger),nameof(CommandAfter));
  NativeHost.Register(AccessTools.DeclaredMethod(typeof(PlayerCmd),nameof(PlayerCmd.LoseGold)),nameof(LoseBefore),typeof(NativeShopLedger),nameof(CommandAfter));
 }
 internal static void Begin(MerchantEntry entry){if(Entry!=null||commands.Count!=0)throw new NativeDataException("Overlapping shop ledger");Entry=entry;serial=paid=hooks=0;}
 internal static int Complete(int cost){if(commands.Count!=0||hooks!=1||paid!=cost)throw new NativeDataException("Missing original shop payment/hook completion");return paid;}
 internal static void Clear(){Entry=null;commands.Clear();}
 static bool Own(object subject)=>Entry!=null&&(ReferenceEquals(subject,NativeMerchant.Inventory!.Player)||ReferenceEquals(subject,NativeMerchant.Inventory.Player.Creature));
 static void GoldBefore(Player __instance,int __0,out Write? __state)=>__state=StartWrite(__instance,"gold",__instance.Gold,__0);
 static void GoldAfter(Player __instance,Write? __state)=>EndWrite(__state,__instance.Gold);
 static void HpBefore(Creature __instance,int __0,out Write? __state)=>__state=StartWrite(__instance,"hp",__instance.CurrentHp,__0);
 static void HpAfter(Creature __instance,Write? __state)=>EndWrite(__state,__instance.CurrentHp);
 internal static Write? MaxBefore(Creature c,int to)=>StartWrite(c,"maxHp",c.MaxHp,to);
 internal static void MaxAfter(Creature c,Write? state)=>EndWrite(state,c.MaxHp);
 static Write? StartWrite(object subject,string field,int from,int to){
  if(NativeEventResourceLedger.Current is {} eventLedger)return eventLedger.WriteStart(subject,field,from,to);
  if(NativeTreasureLedger.Active)return NativeTreasureLedger.WriteStart(subject,field,from,to);
  if(!Own(subject))return null;
  var w=new Write(field,from,to,subject is Player owner?owner.Creature:subject,commands.TryPeek(out var c)?c.Id:null);
  Program.Observe("third-shop-resource-start",new{entry=NativeIdentity.Ref(Entry),field,from,to,subject=NativeIdentity.Ref(w.Subject),command=w.Command});return w;
 }
 static void EndWrite(Write? w,int actual){if(w==null)return;if(actual!=w.To)throw new NativeDataException("Original shop setter result changed");if(w.Command?.StartsWith("event-resource-")==true){NativeEventResourceLedger.Current!.WriteEnd(w);return;}if(w.Command?.StartsWith("treasure-gold-")==true){NativeTreasureLedger.WriteEnd(w);return;}Program.Observe("third-shop-resource-return",new{entry=NativeIdentity.Ref(Entry),field=w.Field,from=w.From,to=actual,subject=NativeIdentity.Ref(w.Subject),command=w.Command});}
 static Command? StartCommand(Player p,string kind,decimal amount,string reason){
  if(!Own(p))return null;var c=new Command("g"+(++serial),kind,amount,reason);commands.Push(c);
  Program.Observe("third-shop-gold-start",new{entry=NativeIdentity.Ref(Entry),id=c.Id,kind,amount,reason,subject=NativeIdentity.Ref(p.Creature)});return c;
 }
 static void GainBefore(decimal __0,Player __1,out Command? __state)=>__state=NativeEventResourceLedger.Current is {} ev?ev.StartGold(__0,__1):NativeTreasureLedger.Active?NativeTreasureLedger.Start(__0,__1):StartCommand(__1,"GainGold",__0,"income");
 static void LoseBefore(decimal __0,Player __1,GoldLossType __2,out Command? __state)=>__state=StartCommand(__1,"LoseGold",__0,__2.ToString());
 static void CommandAfter(Command? __state,ref Task __result){if(__state!=null)__result=FinishCommand(__result,__state);}
 static async Task FinishCommand(Task task,Command c){await task;if(c.Reason=="event"){NativeEventResourceLedger.Current!.End(c);return;}if(c.Reason=="treasure"){NativeTreasureLedger.End(c);return;}if(!commands.TryPop(out var actual)||!ReferenceEquals(actual,c))throw new NativeDataException("Shop command nesting changed");if(c.Kind=="LoseGold"&&c.Reason=="Spent")paid+=checked((int)c.Amount);Program.Observe("third-shop-gold-return",new{entry=NativeIdentity.Ref(Entry),id=c.Id,kind=c.Kind,amount=c.Amount,reason=c.Reason,subject=NativeIdentity.Ref(NativeMerchant.Inventory!.Player.Creature)});}
 internal static void HookBefore(MerchantEntry entry,int goldSpent){if(Entry==null)return;if(!ReferenceEquals(entry,Entry)||commands.Count!=0||++hooks!=1||paid!=goldSpent)throw new NativeDataException("Original shop gross payment/hook mismatch");Program.Observe("third-shop-hook-start",new{entry=NativeIdentity.Ref(entry),goldSpent});}
 internal static void HookAfter(MerchantEntry entry,ref Task task){if(Entry!=null)task=FinishHook(task,entry);}
 static async Task FinishHook(Task task,MerchantEntry entry){await task;if(!ReferenceEquals(entry,Entry)||commands.Count!=0)throw new NativeDataException("Shop hook scope changed");Program.Observe("third-shop-hook-return",new{entry=NativeIdentity.Ref(entry),goldSpent=paid});}
}
