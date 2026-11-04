using System.Text.Json;
namespace OfflineSearchHarness;
internal static class NativeAncientEvidence
{
 static string S(JsonElement e,string k)=>e.GetProperty(k).GetString()!;
 internal static void Check(JsonElement options,JsonElement pickup,JsonElement[] rows){
  void R(bool b,string why)=>NativeBoundaryContract.Require(b,"Ancient evidence: "+why);
  string root=S(pickup,"root"),context=S(pickup,"context");
  var d=rows.Single(x=>S(x,"kind")=="dispatch"&&S(x,"root")==root);var c=rows.Single(x=>S(x,"kind")=="confirmed"&&S(x,"root")==root);
  var observed=rows.Single(x=>S(x,"kind")=="natural-third-ancient-selected"&&S(x,"root")==root);
  R(new[]{d,c,observed}.All(x=>S(x,"context")==context)&&Array.IndexOf(rows,d)<Array.IndexOf(rows,c)&&Array.IndexOf(rows,c)<Array.IndexOf(rows,observed),"ordered current action/context");
  var before=d.GetProperty("detail").GetProperty("state");var after=c.GetProperty("detail").GetProperty("state");
  foreach(var x in new[]{options.GetProperty("state"),pickup.GetProperty("before")})NativeBoundaryContract.Snapshot(x,before,"Ancient input full dispatch");
  NativeBoundaryContract.Snapshot(pickup.GetProperty("after"),after,"Ancient output full confirmed");NativeBoundaryContract.Snapshot(observed.GetProperty("detail").GetProperty("state"),after,"Ancient observation full confirmed");
  R(before.GetProperty("act").GetInt32()==2&&S(before.GetProperty("lifecycle").GetProperty("eventModel"),"model")=="EVENT.NONUPEIPE"&&S(options,"eventModel")=="EVENT.NONUPEIPE"&&S(pickup,"eventModel")=="EVENT.NONUPEIPE","actual third Ancient");
  var action=d.GetProperty("detail").GetProperty("facts");R(S(action,"kind")=="RoomOption","native option action");var decision=action.GetProperty("decision");
  var offered=options.GetProperty("options").EnumerateArray().ToArray();var native=before.GetProperty("rooms").GetProperty("eventOptions").EnumerateArray().ToArray();R(offered.Length==3&&native.Length==3,"three actual offers");
  var ids=rows.Where(x=>S(x,"kind")=="identity").ToDictionary(x=>S(x.GetProperty("detail"),"id"),x=>x.GetProperty("detail"));
  string[] pool=["BLESSED_ANTLER","BRILLIANT_SCARF","DELICATE_FROND","DIAMOND_DIADEM","FUR_COAT","GLITTER","JEWELRY_BOX","LOOMING_FRUIT","SIGNET_RING","BEAUTIFUL_BRACELET"];
  for(int i=0;i<offered.Length;i++){
   var o=offered[i];var n=native[i];string model=S(o,"model"),id=S(o.GetProperty("relic"),"$ref");
   R(o.GetProperty("index").GetInt32()==i&&n.GetProperty("index").GetInt32()==i&&NativeBoundaryContract.Equal(o.GetProperty("option"),n.GetProperty("reference"))&&S(o,"key")==S(n,"TextKey")&&o.GetProperty("IsLocked").GetBoolean()==n.GetProperty("IsLocked").GetBoolean(),"ordered native option identity");
   R(pool.Any(p=>model=="RELIC."+p&&S(o,"key")=="NONUPEIPE.pages.INITIAL.options."+p)&&ids.TryGetValue(id,out var identity)&&S(identity,"model")==model&&S(identity,"type")==NativeThirdAncientContract.TypeForKey(model),"offered registered actual relic/model");
  }
  int index=pickup.GetProperty("index").GetInt32();R(index>=0&&index<3,"selection index");var selected=offered[index];
  R(index==Array.FindIndex(offered,x=>!x.GetProperty("IsLocked").GetBoolean()&&!x.GetProperty("pickup").GetBoolean())&&S(pickup,"key")==S(selected,"key")&&S(pickup,"model")==S(selected,"model")&&NativeBoundaryContract.Equal(pickup.GetProperty("offered"),selected.GetProperty("relic"))&&S(decision,"key")==S(selected,"key")&&NativeBoundaryContract.Equal(decision.GetProperty("option"),selected.GetProperty("option")),"actual policy selection and dispatch");
  var facts=observed.GetProperty("detail").GetProperty("facts");R(S(facts,"model")=="EVENT.NONUPEIPE"&&S(facts,"key")==S(selected,"key")&&NativeBoundaryContract.Equal(facts.GetProperty("option"),selected.GetProperty("option")),"completion selection owner");
  NativeAncientChoice.CheckPersistentPickup(before,after,S(selected,"model"));R(S(after.GetProperty("relics").EnumerateArray().Last(),"id")==S(selected.GetProperty("relic"),"$ref"),"actually acquired offered instance");
 }
 internal static void Review(string directory,JsonElement[] rows){
  bool completed=rows.Any(x=>S(x,"kind")=="natural-third-ancient-selected"&&S(x.GetProperty("detail").GetProperty("facts"),"model")=="EVENT.NONUPEIPE");
  if(!completed)return;
  Check(JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"third-ancient-options.json"))).RootElement,JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"third-ancient-pickup.json"))).RootElement,rows);
 }
}
