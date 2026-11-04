using System.Text.Json;
namespace OfflineSearchHarness;
internal static class NativeReflectionsEvidence {
 static string T(JsonElement e,string k)=>e.GetProperty(k).GetString()!;
 static JsonElement S(JsonElement e)=>e.GetProperty("detail").GetProperty("state");
 static JsonElement F(JsonElement e)=>e.GetProperty("detail").GetProperty("facts");
 static bool Eq(JsonElement a,JsonElement b)=>NativeBoundaryContract.Equal(a,b);
 static void R(bool b,string why)=>NativeBoundaryContract.Require(b,"Reflections: "+why);
 static JsonElement Cards(JsonElement state)=>JsonSerializer.SerializeToElement(state.GetProperty("deck").EnumerateArray().Select(c=>new{id=T(c,"id"),model=T(c,"model"),upgrade=c.GetProperty("upgrade").GetInt32()}),Program.Json);
 internal static object Review(IEnumerable<JsonElement> source){
  var rows=source.ToArray();int completed=0;
  foreach(var group in rows.GroupBy(r=>T(r,"root"))){var a=group.ToArray();var ds=a.Where(r=>T(r,"kind")=="dispatch").ToArray();
   if(ds.Length==0){R(!a.Any(r=>NativeReflections.Kinds.Contains(T(r,"kind"))),"observation requires dispatch");continue;}
   var d=ds.Single();var action=F(d);var decision=action.GetProperty("decision");
   bool relevant=T(action,"kind")=="RoomOption"&&decision.TryGetProperty("key",out var key)&&key.GetString()!.StartsWith("REFLECTIONS.",StringComparison.Ordinal);
   foreach(var p in a.Where(r=>NativeReflections.Kinds.Contains(T(r,"kind")))){NativeTraceContract.CheckState(S(p));R(T(p,"context")==T(d,"context")&&Array.IndexOf(a,p)>Array.IndexOf(a,d),"actual dispatched context/order");}
   if(!relevant)continue;
   var starts=a.Where(r=>T(r,"kind")=="reflections-start").ToArray();var ends=a.Where(r=>T(r,"kind")=="reflections-return").ToArray();var confirmed=a.Where(r=>T(r,"kind")=="confirmed").ToArray();
   if(confirmed.Length==0&&starts.Length==0)continue;R(starts.Length==1,"unique required start");var start=starts.Single();var f=F(start);int index=f.GetProperty("index").GetInt32();
   R(index==decision.GetProperty("index").GetInt32()&&Eq(f.GetProperty("option"),decision.GetProperty("option"))&&Eq(S(start),S(d)),"actual selected option and complete input state");
   var eventBefore=S(start).GetProperty("lifecycle").GetProperty("eventModel");R(T(eventBefore,"model")=="EVENT.REFLECTIONS"&&!eventBefore.GetProperty("IsFinished").GetBoolean()&&Eq(f.GetProperty("rng"),eventBefore.GetProperty("rng")),"original event source RNG");
   var opt=S(d).GetProperty("rooms").GetProperty("eventOptions")[index];R(T(opt,"TextKey")==T(decision,"key")&&Eq(opt.GetProperty("reference"),f.GetProperty("option"))&&!opt.GetProperty("IsLocked").GetBoolean(),"real legal option");
   R(Eq(f.GetProperty("cards"),Cards(S(start)))&&T(f,"information")=="Oracle","actual source deck and information");
   if(confirmed.Length==0&&ends.Length==0)continue;R(ends.Length==1,"unique required completion");var end=ends.Single();var ef=F(end);
   R(Array.IndexOf(a,start)<Array.IndexOf(a,end)&&ef.GetProperty("index").GetInt32()==index&&ef.GetProperty("finished").GetBoolean()&&Eq(ef.GetProperty("cards"),Cards(S(end))),"actual completion and full deck projection");
   var eventAfter=S(end).GetProperty("lifecycle").GetProperty("eventModel");R(Eq(eventBefore.GetProperty("reference"),eventAfter.GetProperty("reference"))&&T(eventAfter,"model")=="EVENT.REFLECTIONS"&&eventAfter.GetProperty("IsFinished").GetBoolean()&&Eq(ef.GetProperty("rng"),eventAfter.GetProperty("rng")),"original event completion identity/RNG");
   if(index==0)R(T(f,"prediction")=="RandomForeseer/0.13.19/single-event"&&T(ef,"comparison")=="ExactScalarCardsAndRng"&&Eq(f.GetProperty("forecast"),ef.GetProperty("cards"))&&Eq(f.GetProperty("forecastRng"),ef.GetProperty("rng")),"actual native scalar prediction comparison");
   else R(index==1&&T(f,"prediction")=="UnknownShatter"&&T(ef,"comparison")=="NotPredicted"&&f.GetProperty("forecast").ValueKind==JsonValueKind.Null,"unknown prediction is not certification");
   foreach(var p in a.Where(r=>T(r,"kind") is "room-option-return" or "confirmed"))R(Array.IndexOf(a,end)<Array.IndexOf(a,p)&&Eq(S(end),S(p)),"complete stationary output binding");completed++;
  }
  return new{completed};
 }
}
