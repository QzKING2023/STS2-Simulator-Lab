using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Hooks;
namespace OfflineSearchHarness;
internal static class NativeTreasureLedger {
 internal static Player? Player;
 internal static bool Active=>Player!=null;
 static readonly Stack<NativeShopLedger.Command> stack=[];
 static int serial,delta,writes,commands;
 static decimal inputs;
 internal static void Install(){if(NativeFullRun.Active)NativeHost.Register(AccessTools.DeclaredMethod(typeof(Hook),nameof(Hook.ModifyGoldGained)),nameof(ModifierBefore),typeof(NativeTreasureLedger),nameof(ModifierAfter));}
 internal static void Begin(Player player){if(Active||stack.Count>0)throw new NativeDataException("Overlapping treasure ledger");Player=player;serial=delta=writes=commands=0;inputs=0;}
 internal static NativeShopLedger.Command Start(decimal amount,Player p){if(!ReferenceEquals(Player,p))throw new NotSupportedException("Treasure other-player income");var c=new NativeShopLedger.Command("treasure-gold-"+(++serial),"GainGold",amount,"treasure");stack.Push(c);inputs+=amount;commands++;Program.Observe("treasure-gold-start",new{id=c.Id,amount,subject=NativeIdentity.Ref(p.Creature)});return c;}
 internal static void End(NativeShopLedger.Command c){if(stack.Count==0||!ReferenceEquals(stack.Pop(),c))throw new NativeDataException("Treasure gold command nesting");Program.Observe("treasure-gold-return",new{id=c.Id,amount=c.Amount,subject=NativeIdentity.Ref(Player!.Creature)});}
 internal static NativeShopLedger.Write? WriteStart(object subject,string field,int from,int to){if(!ReferenceEquals(subject,Player)||field!="gold")return null;if(!stack.TryPeek(out var c))throw new NativeDataException("Treasure gold write without original command");var w=new NativeShopLedger.Write(field,from,to,Player!.Creature,c.Id);Program.Observe("treasure-gold-write-start",new{id=c.Id,from,to,subject=NativeIdentity.Ref(w.Subject)});return w;}
 internal static void WriteEnd(NativeShopLedger.Write w){delta+=w.To-w.From;writes++;Program.Observe("treasure-gold-write-return",new{id=w.Command,from=w.From,to=w.To,subject=NativeIdentity.Ref(w.Subject)});}
 static void ModifierBefore(){}
 static void ModifierAfter(decimal __2,Player __3,IEnumerable<AbstractModel> __4,decimal __result){if(NativeEventResourceLedger.Current is {} ev){ev.GoldModifier(__2,__result,__4);return;}if(Active&&ReferenceEquals(Player,__3)){if(!stack.TryPeek(out var c))throw new NativeDataException("Treasure modifier outside command");Program.Observe("treasure-gold-modified",new{id=c.Id,input=__2,modified=__result,modifiers=__4.Select(x=>new{reference=NativeIdentity.Ref(x),model=x.Id.ToString()}).ToArray()});}}
 internal static object Complete(int nominal,int before){if(stack.Count!=0||commands==0||inputs!=nominal||Player!.Gold-before!=delta)throw new NativeDataException("Treasure original input/command/write ledger mismatch");return new{gold=nominal,accounting="native-modified-ledger/v1",actualGoldDelta=delta,commands,writes};}
 internal static void Clear(){Player=null;stack.Clear();}
}
