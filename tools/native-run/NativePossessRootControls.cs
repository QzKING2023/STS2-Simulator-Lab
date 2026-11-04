using System.Reflection;
using System.Text.Json;
#if SOLVER_GATE
using CombatSolver;
using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
#endif
namespace OfflineSearchHarness;
internal static partial class Program
{
#if SOLVER_GATE
 static readonly HashSet<string> possessChecked=[];
 static readonly List<object> possessCases=[];
 static void PossessRootControls(CombatRootSnapshot root,CombatState native,int query){
  var proof=JsonSerializer.SerializeToElement(NativeSolverBranchRuntime.Capture());if(proof.ValueKind!=JsonValueKind.Object||proof.GetProperty("derivative").GetString()!="possessed-root.1")return;
  var dataField=typeof(PowerModel).GetField("_internalData",BindingFlags.Instance|BindingFlags.NonPublic)!;
  var powers=native.IterateHookListeners().OfType<PowerModel>().Where(p=>p is PossessSpeedPower or PossessStrengthPower).ToArray();
  Dictionary<(Creature,Creature),int> Native(string name){var result=new Dictionary<(Creature,Creature),int>();foreach(var power in powers.Where(p=>name=="Dexterity"?p is PossessSpeedPower:p is PossessStrengthPower)){var data=dataField.GetValue(power);if(data==null)continue;var values=(IReadOnlyDictionary<Creature,decimal>)data.GetType().GetField("stolen"+name)!.GetValue(data)!;foreach(var (target,amount) in values){if(amount==0)continue;result.Add((power.Owner,target),checked((int)amount));}}return result;}
  Dictionary<(Creature,Creature),int> Branch(SimulatedCombatState state,string name){var field=typeof(SimulatedCombatState).GetField("_stolen"+name,BindingFlags.Instance|BindingFlags.NonPublic)!;return field.GetValue(state) is IReadOnlyDictionary<(Creature,Creature),int> values?values.ToDictionary(p=>p.Key,p=>p.Value):[];}
  bool Equal(Dictionary<(Creature,Creature),int> x,Dictionary<(Creature,Creature),int> y)=>x.Count==y.Count&&x.All(p=>y.TryGetValue(p.Key,out int value)&&value==p.Value);
  foreach(string name in new[]{"Dexterity","Strength"}){
   var expected=Native(name);if(expected.Count==0||possessChecked.Contains(name))continue;
   string nativeBefore=StateStamp();int beforeDispatch=dispatched;var parent=root.ForkSimulator();var a=root.ForkSimulator();var b=root.ForkSimulator();var ps=(SimulatedCombatState)parent.State.CombatState;var sa=(SimulatedCombatState)a.State.CombatState;var sb=(SimulatedCombatState)b.State.CombatState;
   bool capture=Equal(expected,Branch(ps,name))&&Equal(expected,Branch(sa,name))&&Equal(expected,Branch(sb,name));var possessor=expected.Keys.First().Item1;var debt=expected.Where(x=>x.Key.Item1==possessor).ToArray();
   int Amount(SimulatedCombatState state,Creature target)=>name=="Dexterity"?state.GetAmount<DexterityPower>(target):state.GetAmount<StrengthPower>(target);
   var old=debt.ToDictionary(p=>p.Key.Item2,p=>Amount(sa,p.Key.Item2));sa.RefundPossessedStats(possessor);
   bool refunded=debt.All(p=>Amount(sa,p.Key.Item2)==old[p.Key.Item2]-p.Value)&&!Branch(sa,name).Keys.Any(k=>k.Item1==possessor);
   sa.RefundPossessedStats(possessor);bool once=debt.All(p=>Amount(sa,p.Key.Item2)==old[p.Key.Item2]-p.Value);
   bool isolated=Equal(expected,Branch(ps,name))&&Equal(expected,Branch(sb,name))&&Equal(expected,Native(name))&&debt.All(p=>Amount(ps,p.Key.Item2)==old[p.Key.Item2]&&Amount(sb,p.Key.Item2)==old[p.Key.Item2])&&nativeBefore==StateStamp()&&dispatched==beforeDispatch;
   possessCases.Add(new{query,stat=name,scope="branch-api fixture; native source debt observed, no native mutation",captured=capture,refunded,exactlyOnce=once,isolated,nativeExecutions=0,debt=debt.Select(p=>new{owner=p.Key.Item1.CombatId,target=p.Key.Item2.CombatId,amount=p.Value}).ToArray()});Write("possess-root-controls.json",possessCases);
   if(!capture||!refunded||!once||!isolated)throw new NativeDataException("Possessed root state/fork control failed");possessChecked.Add(name);
  }
 }
#endif
}
