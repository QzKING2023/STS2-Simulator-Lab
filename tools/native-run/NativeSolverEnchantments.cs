using System.Globalization;
using System.Text.Json;
using System.Reflection;
using SimulatorLab.Core;
#if SOLVER_GATE
using CombatSolver;
using HarmonyLib;
using CombatSolver.Engine.Common.Mirrors;
using CombatSolver.Engine.InCombat.Mirrors.Enchantments.OnPlay;
using MegaCrit.Sts2.Core.Models;
#endif
namespace OfflineSearchHarness;

// Transport capability only. Actual effects stay in the pinned upstream mirror
// and the original native card play. This never registers or alters a handler.
internal static class NativeSolverEnchantments
{
 internal const string Protocol="upstream-card-enchantment/v1";
#if SOLVER_GATE
 static MethodMirrorRegistryDescriptor Descriptor=>((IMethodMirrorRegistryDescriptorProvider)typeof(EnchantmentOnPlayMirrors).GetField("Registry",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!).DescribeMirrorSupport();
 internal static bool Registered(Type type)=>type.Assembly==typeof(CardModel).Assembly&&Descriptor.Registrations.Any(r=>r.ReceiverType==type&&r.Kind==MethodMirrorRegistrationKind.Handled);
 // Reuse upstream MethodMirrorRegistry.NotOverridden for the original empty
 // base OnPlay. Other enchantment methods still pass the root audit and shadow diff.
 internal static string Support(Type type){
  if(type.Assembly!=typeof(CardModel).Assembly||type.IsAbstract||!typeof(EnchantmentModel).IsAssignableFrom(type))return "Unsupported";
  var basis=Descriptor.BaseMethod;
  var actual=type.GetMethod(basis.Name,BindingFlags.Instance|BindingFlags.Public,null,basis.GetParameters().Select(p=>p.ParameterType).ToArray(),null);
  if(actual==null||Harmony.GetPatchInfo(actual) is {Owners.Count:>0}||Harmony.GetPatchInfo(basis) is {Owners.Count:>0})return "Unsupported";
  if(actual.DeclaringType==basis.DeclaringType&&actual.Module==basis.Module&&actual.MetadataToken==basis.MetadataToken)return "InheritedBase";
  return Registered(type)?"Handled":"Unsupported";
 }
 internal static object? Capture(CardModel card)=>card.Enchantment is {} e?new{id=e.Id.Entry,type=e.GetType().FullName,assembly=e.GetType().Assembly.FullName,amount=e.Amount,status=(int)e.Status,stamp=EnchantmentStateSupport.Describe(e),registered=Registered(e.GetType()),onPlaySupport=Support(e.GetType())}:null;
 internal static void ValidateNative(CardModel card,string expected){
  var e=card.Enchantment;
  if((e?.Id.Entry??"")!=expected)throw new NativeActionRejectedException("Solver/native enchantment identity mismatch");
  if(e!=null&&Support(e.GetType())=="Unsupported")throw new NotSupportedException("Native enchantment has no exact upstream OnPlay capability");
 }
#endif
 internal static bool Enabled(JsonElement q)=>q.TryGetProperty("binding",out var map)&&map.TryGetProperty("cardIdentity",out var v)&&v.GetString()==Protocol;
 internal static void ValidateMap(JsonElement action,JsonElement entry){
  void Need(bool ok,string why){if(!ok)throw new NativeDataException("Solver enchantment binding: "+why);}
  string id=action.GetProperty("CardEnchantmentId").GetString()!;var e=entry.GetProperty("enchantment");
  if(e.ValueKind==JsonValueKind.Null){Need(id=="","missing native enchantment");return;}
  Need(e.ValueKind==JsonValueKind.Object&&id!=""&&e.GetProperty("id").GetString()==id,"actual native enchantment ID");
#if SOLVER_GATE
  string type=e.GetProperty("type").GetString()!;
  var receiver=typeof(CardModel).Assembly.GetType(type,false);
  Need(receiver!=null&&e.GetProperty("assembly").GetString()==typeof(CardModel).Assembly.FullName,"exact pinned native receiver");
  if(e.TryGetProperty("onPlaySupport",out var support))Need(support.GetString()==Support(receiver!)&&support.GetString()!="Unsupported"&&e.GetProperty("registered").GetBoolean()==Registered(receiver!),"exact inherited or handled upstream capability");
  else Need(Registered(receiver!)&&e.GetProperty("registered").GetBoolean(),"legacy explicit handler capability");
#else
  throw new NotSupportedException("Separate solver build required for enchantment mapping");
#endif
  string stamp=e.GetProperty("stamp").GetString()!;
  string prefix=id+":"+e.GetProperty("amount").GetDecimal().ToString(CultureInfo.InvariantCulture)+":"+e.GetProperty("status").GetInt32();
  Need((stamp==prefix||stamp.StartsWith(prefix+":",StringComparison.Ordinal))&&entry.GetProperty("key").GetString()!.Contains("|"+stamp+"|",StringComparison.Ordinal)&&action.GetProperty("CardStateKey").GetString()==entry.GetProperty("key").GetString(),"complete upstream amount/status/private state key");
 }
}
