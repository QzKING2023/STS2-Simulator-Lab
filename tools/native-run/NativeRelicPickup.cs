using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
namespace OfflineSearchHarness;

// Capability gate over the pinned original implementation, not a relic-name list.
// Combat effects are separately admitted by the existing solver root audit.
internal static class NativeRelicPickup
{
 internal const string Profile="original-default-relic/v1";
 internal sealed record Pickup(string Operation,RelicReward Reward,RelicModel Offered,JsonElement Before,JsonElement Source);
 static readonly Lazy<Dictionary<string,Type>> types=new(()=>typeof(RelicModel).Assembly.GetTypes().Where(t=>!t.IsAbstract&&t.Namespace=="MegaCrit.Sts2.Core.Models.Relics"&&typeof(RelicModel).IsAssignableFrom(t)).ToDictionary(t=>ModelDb.GetId(t).ToString()));
 internal static Type? Resolve(string model)=>types.Value.GetValueOrDefault(model);
 internal static JsonElement Source(Type type,string model){
  if(type.Assembly!=typeof(RelicModel).Assembly||type.IsAbstract||!typeof(RelicModel).IsAssignableFrom(type)||Resolve(model)!=type)throw new NotSupportedException("Relic original assembly/type/model mismatch");
  var obtained=AccessTools.Method(type,"AfterObtained");var pickup=AccessTools.PropertyGetter(type,"HasUponPickupEffect");
  if(obtained==null||obtained.DeclaringType!=typeof(RelicModel)||pickup==null||pickup.Module!=typeof(RelicModel).Module)throw new NotSupportedException("Relic has a non-default acquisition implementation");
  obtained=Declared(obtained);pickup=Declared(pickup);
  foreach(var method in new[]{obtained,pickup})if(Harmony.GetPatchInfo(method) is {Owners.Count:>0})throw new NotSupportedException("Relic acquisition source has an unknown patch");
  object Method(MethodInfo m)=>new{type=m.DeclaringType!.FullName,name=m.Name,token=m.MetadataToken,mvid=m.Module.ModuleVersionId};
  return JsonSerializer.SerializeToElement(new{type=type.FullName,model,obtained=Method(obtained),pickupGetter=Method(pickup)},Program.Json);
 }
 internal static Pickup Begin(RelicReward reward,string operation){
  var before=Program.State();var offered=reward.Relic??throw new NotSupportedException("Missing original reward relic");
  var source=Source(offered.GetType(),offered.Id.ToString());
  if(offered.HasUponPickupEffect)throw new NotSupportedException("Relic advertises an immediate pickup effect");
  NativeBoundaryContract.Snapshot(before,Program.State(),"Relic admission cannot mutate state/RNG");
  var p=new Pickup(operation,reward,offered,before,source);
  Program.Observe("default-relic-admitted",new{id=operation,reward=NativeIdentity.Ref(reward),offered=NativeIdentity.Ref(offered),model=offered.Id.ToString(),source});return p;
 }
 internal static void Complete(Pickup p,bool success){
  if(!success||!ReferenceEquals(p.Reward.ClaimedRelic,p.Offered)||!p.Reward.Player.Relics.Any(r=>ReferenceEquals(r,p.Offered)))throw new NotSupportedException("Original relic declined/replaced; outside persistent pickup contract");
  var after=Program.State();
  try{Frame(p.Before,after,NativeIdentity.Get(p.Offered),p.Offered.Id.ToString());}
  catch(NativeDataException ex){throw new NotSupportedException("Native pickup changed state outside default persistent frame",ex);}
  Program.Observe("default-relic-completed",new{id=p.Operation,reward=NativeIdentity.Ref(p.Reward),offered=NativeIdentity.Ref(p.Offered),model=p.Offered.Id.ToString(),source=p.Source,acquired=NativeIdentity.Ref(p.Offered)});
 }
 internal static void Frame(JsonElement before,JsonElement after,string id,string model){
  var old=before.GetProperty("relics").EnumerateArray().ToArray();
  NativeBoundaryContract.Require(!old.Any(x=>x.GetProperty("id").GetString()==id),"Relic actual fresh instance");
  var expected=JsonNode.Parse(before.GetRawText())!;expected["relics"]!.AsArray().Add(new JsonObject{["id"]=id,["model"]=model});
  NativeBoundaryContract.Snapshot(after,JsonSerializer.SerializeToElement(expected),"Default relic full original pickup frame/RNG");
 }
 static MethodInfo Declared(MethodInfo method)=>method.DeclaringType!.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Single(m=>m.Module==method.Module&&m.MetadataToken==method.MetadataToken);
 static void Noop(){}
 internal static object Controls(){
  int passed=0;
  foreach(var model in new[]{"RELIC.MUMMIFIED_HAND","RELIC.MEAL_TICKET","RELIC.ORICHALCUM"}){_=Source(Resolve(model)!,model);passed++;}
  foreach(var pair in new[]{(typeof(string),"RELIC.MUMMIFIED_HAND"),(Resolve("RELIC.MUMMIFIED_HAND")!,"RELIC.ORICHALCUM")}){
   bool rejected=false;try{_=Source(pair.Item1,pair.Item2);}catch(NotSupportedException){rejected=true;}if(!rejected)throw new NativeDataException("Relic source control accepted");passed++;
  }
  var probe=new Harmony("lab.control.default-relic");bool denied=false;var type=Resolve("RELIC.MUMMIFIED_HAND")!;
  try{probe.Patch(Declared(AccessTools.Method(type,"AfterObtained")),prefix:new HarmonyMethod(typeof(NativeRelicPickup),nameof(Noop)));try{_=Source(type,"RELIC.MUMMIFIED_HAND");}catch(NotSupportedException){denied=true;}}
  finally{probe.UnpatchAll(probe.Id);}
  if(!denied)throw new NativeDataException("Relic real unknown patch accepted");passed++;
  return new{passed,actualHarmonyRejected=denied,businessExecutions=0,scope="metadata/source admission only; mutable pickup separately observed"};
 }
}
