using System.Text.Json;
using System.Text.Json.Nodes;
namespace OfflineSearchHarness;

// Pinned Tanx option source and simple-relic acquisition frame. Native effects
// execute first; unfamiliar effects fail this bounded contract explicitly.
internal static class NativeThirdAncientContract
{
 internal static readonly string[] Simple=["CLAWS","CROSSBOW","IRON_CLUB","MEAT_CLEAVER","SAI","SPIKED_GAUNTLETS","TANXS_WHISTLE","THROWING_AXE","WAR_HAMMER"];
 static string T(JsonElement e,string p)=>e.GetProperty(p).GetString()!;
 static void R(bool x,string why)=>NativeBoundaryContract.Require(x,"Third Ancient "+why);
 internal static string TypeForKey(string key)=>string.Concat(key.Split('.').Last().ToLowerInvariant().Split('_').Select(x=>char.ToUpperInvariant(x[0])+x[1..]));
 internal static void Open(JsonElement state)
 {
  var ev=state.GetProperty("lifecycle").GetProperty("eventModel");var options=state.GetProperty("rooms").GetProperty("eventOptions").EnumerateArray().ToArray();
  R(state.GetProperty("act").GetInt32()==2&&T(ev,"model")=="EVENT.TANX"&&!ev.GetProperty("IsFinished").GetBoolean()&&options.Length==3,"actual third-act Tanx with three options");
  var refs=new HashSet<string>();var keys=new HashSet<string>();
  for(int i=0;i<options.Length;i++){
   var o=options[i];string key=T(o,"TextKey");
   R(o.EnumerateObject().Select(p=>p.Name).Order().SequenceEqual(new[]{"index","reference","TextKey","IsLocked","IsProceed"}.Order())&&o.GetProperty("index").GetInt32()==i&&refs.Add(T(o.GetProperty("reference"),"$ref"))&&keys.Add(key)&&!o.GetProperty("IsLocked").GetBoolean()&&!o.GetProperty("IsProceed").GetBoolean(),"closed option identity/order");
   R(Simple.Append("TRI_BOOMERANG").Any(x=>key=="TANX.pages.INITIAL.options."+x),"actual pinned option pool");
  }
  var rng=ev.GetProperty("rng");R(rng.GetProperty("counter").GetInt64()>0,"native shuffled options have RNG witness");foreach(var field in new[]{"s0","s1","s2","s3"})_=rng.GetProperty(field).GetUInt64();
 }
 internal static void Selected(JsonElement before,JsonElement after,string key)
 {
  Open(before);string entry=Simple.SingleOrDefault(x=>key=="TANX.pages.INITIAL.options."+x)??throw new NotSupportedException("Unregistered Tanx pickup effect: "+key);
  var old=before.GetProperty("relics").EnumerateArray().Select(x=>T(x,"id")).ToHashSet();var added=after.GetProperty("relics").EnumerateArray().Where(x=>!old.Contains(T(x,"id"))).ToArray();
  R(added.Length==1&&added[0].EnumerateObject().Select(p=>p.Name).Order().SequenceEqual(new[]{"id","model"}.Order())&&T(added[0],"model")=="RELIC."+entry,"actual selected new relic instance/model");
  var expected=JsonNode.Parse(before.GetRawText())!;
  expected["relics"]!.AsArray().Add(JsonNode.Parse(added[0].GetRawText()));
  expected["rooms"]!["eventOptions"]=new JsonArray();expected["lifecycle"]!["eventModel"]!["IsFinished"]=true;
  R(NativeBoundaryContract.Equal(after,JsonSerializer.SerializeToElement(expected)),"simple relic pickup full native state frame");
 }
}
