using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
namespace OfflineSearchHarness;

// Observe original command inputs, awaited results and writes. Never query a
// modifier a second time or replace native effects with displayed event values.
internal sealed class NativeEventResourceLedger : IDisposable {
 internal static NativeEventResourceLedger? Current;
 readonly Player player;
 readonly int goldBefore,hpBefore;
 readonly Stack<NativeShopLedger.Command> stack=[];
 int serial,goldDelta,hpDelta,damageCommands,goldCommands;
 decimal damageInput,goldInput;
 readonly List<object> results=[];
 internal NativeEventResourceLedger(Player p){if(Current!=null)throw new NativeDataException("Overlapping event resource ledger");player=p;goldBefore=p.Gold;hpBefore=p.Creature.CurrentHp;Current=this;Program.Observe("event-resource-start",new{subject=NativeIdentity.Ref(p.Creature)});}
 internal static void Install(){if(!NativeFullRun.Active)return;var method=typeof(CreatureCmd).GetMethods().Single(m=>m.Name=="Damage"&&m.GetParameters().Length==7&&m.GetParameters()[1].ParameterType==typeof(IEnumerable<Creature>));NativeHost.Register(method,nameof(DamageBefore),typeof(NativeEventResourceLedger),nameof(DamageAfter));}
 internal NativeShopLedger.Command StartGold(decimal amount,Player p){if(!ReferenceEquals(p,player))throw new NotSupportedException("Other-player event income");goldInput+=amount;goldCommands++;return Start("GainGold",amount);}
 NativeShopLedger.Command Start(string kind,decimal amount){var c=new NativeShopLedger.Command("event-resource-"+(++serial),kind,amount,"event");stack.Push(c);Program.Observe("event-command-start",new{id=c.Id,kind,amount,subject=NativeIdentity.Ref(player.Creature)});return c;}
 internal void End(NativeShopLedger.Command c){if(stack.Count==0||!ReferenceEquals(stack.Pop(),c))throw new NativeDataException("Event command nesting");Program.Observe("event-command-return",new{id=c.Id,kind=c.Kind,amount=c.Amount,subject=NativeIdentity.Ref(player.Creature)});}
 internal NativeShopLedger.Write? WriteStart(object owner,string field,int from,int to){if(!(ReferenceEquals(owner,player)||ReferenceEquals(owner,player.Creature)))return null;if(!stack.TryPeek(out var c))throw new NativeDataException("Event resource changed outside command");var w=new NativeShopLedger.Write(field,from,to,player.Creature,c.Id);Program.Observe("event-resource-write-start",new{id=c.Id,field,from,to,subject=NativeIdentity.Ref(player.Creature)});return w;}
 internal void WriteEnd(NativeShopLedger.Write w){if(w.Field=="gold")goldDelta+=w.To-w.From;else if(w.Field=="hp")hpDelta+=w.To-w.From;Program.Observe("event-resource-write-return",new{id=w.Command,field=w.Field,from=w.From,to=w.To,subject=NativeIdentity.Ref(player.Creature)});}
 internal void GoldModifier(decimal input,decimal modified,IEnumerable<AbstractModel> modifiers){if(!stack.TryPeek(out var c)||c.Kind!="GainGold")throw new NativeDataException("Event modifier outside gold command");Program.Observe("event-gold-modified",new{id=c.Id,input,modified,modifiers=modifiers.Select(x=>new{reference=NativeIdentity.Ref(x),model=x.Id.ToString()}).ToArray()});}
 static void DamageBefore(IEnumerable<Creature>? __1,decimal __2,out NativeShopLedger.Command? __state){__state=null;if(Current is not {} l)return;if(__1==null||!__1.SequenceEqual(new[]{l.player.Creature}))throw new NotSupportedException("Event damage receiver outside single-player ledger");l.damageInput+=__2;l.damageCommands++;__state=l.Start("Damage",__2);}
 static void DamageAfter(ref Task<IEnumerable<DamageResult>> __result,NativeShopLedger.Command? __state){if(__state!=null)__result=FinishDamage(__result,__state,Current!);}
 static async Task<IEnumerable<DamageResult>> FinishDamage(Task<IEnumerable<DamageResult>> task,NativeShopLedger.Command c,NativeEventResourceLedger l){var actual=await task;var rows=actual.ToArray();if(!ReferenceEquals(l,Current)||rows.Any(r=>!ReferenceEquals(r.Receiver,l.player.Creature)))throw new NativeDataException("Event original damage receiver/scope");l.results.AddRange(rows.Select(r=>(object)new{id=c.Id,receiver=NativeIdentity.Ref(r.Receiver),r.BlockedDamage,r.UnblockedDamage,r.WasTargetKilled,r.OverkillDamage}));Program.Observe("event-damage-result",new{id=c.Id,results=rows.Select(r=>NativeObservation.Value(r)).ToArray()});l.End(c);return rows;}
 internal object Complete(int expectedGold,int expectedDamage){if(stack.Count!=0||goldCommands!=1||damageCommands!=1||goldInput!=expectedGold||damageInput!=expectedDamage||player.Gold-goldBefore!=goldDelta||player.Creature.CurrentHp-hpBefore!=hpDelta)throw new NativeDataException("Event input/result/write accounting mismatch");var report=new{schema="native-event-resource/v1",goldInput,damageInput,goldDelta,hpDelta,goldCommands,damageCommands,results=results.ToArray(),scope="actual native modifiers and command effects; visible input is not net resource delta"};Program.Observe("event-resource-complete",report);return report;}
 public void Dispose(){if(ReferenceEquals(Current,this))Current=null;}
}
