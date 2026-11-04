using System.Text.Json;
namespace OfflineSearchHarness;
internal static class NativeSlipperyEvidence
{
 static string T(JsonElement e,string k)=>e.GetProperty(k).GetString()!;
 static JsonElement S(JsonElement e)=>e.GetProperty("detail").GetProperty("state");
 static JsonElement F(JsonElement e)=>e.GetProperty("detail").GetProperty("facts");
 static bool Same(JsonElement a,JsonElement b)=>NativeBoundaryContract.Equal(a,b);
 static void Need(bool b,string why)=>NativeBoundaryContract.Require(b,"Slippery: "+why);
 static string CardId(JsonElement c)=>T(c.GetProperty("reference"),"$ref");
 static void Card(JsonElement card,JsonElement state){var item=state.GetProperty("deck").EnumerateArray().Single(x=>T(x,"id")==CardId(card));Need(T(card,"model")==T(item,"model")&&card.GetProperty("upgrade").GetInt32()==item.GetProperty("upgrade").GetInt32(),"actual deck card instance/model/upgrade");}
 internal static void Effect(int index,JsonElement before,JsonElement after,JsonElement eb,JsonElement ea,JsonElement forecast,JsonElement forecastRng){
  Need(index is 0 or 1&&T(eb,"model")=="EVENT.SLIPPERY_BRIDGE"&&T(ea,"model")==T(eb,"model")&&Same(eb.GetProperty("reference"),ea.GetProperty("reference"))&&!eb.GetProperty("finished").GetBoolean(),"original event identity and unfinished input");
  Card(eb.GetProperty("current"),before);if(forecast.ValueKind!=JsonValueKind.Null)Card(forecast,before);
  int holds=eb.GetProperty("holds").GetInt32();Need(holds>=0,"native hold counter");
  if(index==1){
   Need(!ea.GetProperty("finished").GetBoolean()&&ea.GetProperty("holds").GetInt32()==holds+1&&Same(forecast,ea.GetProperty("current"))&&Same(forecastRng,ea.GetProperty("rng")),"native HoldOn exact RF candidate/RNG");
   var skipped=eb.GetProperty("skipped").EnumerateArray().Select(x=>x.GetString()!).Append(CardId(eb.GetProperty("current"))).Distinct().Order();
   Need(skipped.SequenceEqual(ea.GetProperty("skipped").EnumerateArray().Select(x=>x.GetString()!).Order()),"original skipped instances");
   Need(after.GetProperty("hp").GetInt32()==before.GetProperty("hp").GetInt32()-3-holds&&after.GetProperty("hp").GetInt32()>0&&Same(before.GetProperty("deck"),after.GetProperty("deck")),"surviving native HoldOn damage/no removal");
  }else{
   Need(ea.GetProperty("finished").GetBoolean()&&ea.GetProperty("holds").GetInt32()==holds&&Same(eb.GetProperty("current"),ea.GetProperty("current"))&&Same(eb.GetProperty("rng"),ea.GetProperty("rng"))&&Same(eb.GetProperty("skipped"),ea.GetProperty("skipped")),"native Overcome finished without reroll");
   var expected=before.GetProperty("deck").EnumerateArray().Where(x=>T(x,"id")!=CardId(eb.GetProperty("current"))).ToArray();
   Need(Same(JsonSerializer.SerializeToElement(expected),after.GetProperty("deck")),"exact native removal of selected instance");
  }
 }
 internal static object Review(IEnumerable<JsonElement> rows){
  JsonElement action=default,dispatch=default,start=default,returned=default;string root="",context="";bool relevant=false;int completed=0,entries=0;
  void Owner(JsonElement r)=>Need(T(r,"root")==root&&T(r,"context")==context,"current dispatched root/context");
  foreach(var r in rows){var kind=T(r,"kind");
   if(kind=="dispatch"){
    action=F(r);dispatch=S(r);root=T(r,"root");context=T(r,"context");start=returned=default;
    relevant=T(action,"kind")=="RoomOption"&&action.GetProperty("decision").TryGetProperty("key",out var k)&&k.GetString()!.StartsWith("SLIPPERY_BRIDGE.",StringComparison.Ordinal);
   }else if(kind=="slippery-entry"){
    Owner(r);Need(T(action,"kind")=="Travel"&&Same(F(r).GetProperty("eventBefore"),F(r).GetProperty("eventAfter"))&&T(F(r).GetProperty("eventAfter"),"model")=="EVENT.SLIPPERY_BRIDGE","original no-UI entry");entries++;
   }else if(kind=="slippery-start"){
    Owner(r);Need(relevant&&start.ValueKind==JsonValueKind.Undefined&&Same(S(r),dispatch),"required unique start at dispatch state");var f=F(r);var d=action.GetProperty("decision");int i=f.GetProperty("index").GetInt32();
    Need(i==d.GetProperty("index").GetInt32()&&T(f,"key")==T(d,"key")&&Same(f.GetProperty("option"),d.GetProperty("option")),"actual selected option");
    var opt=dispatch.GetProperty("rooms").GetProperty("eventOptions")[i];Need(T(opt,"TextKey")==T(f,"key")&&Same(opt.GetProperty("reference"),f.GetProperty("option"))&&!opt.GetProperty("IsLocked").GetBoolean(),"real offered option");
    Need(T(f,"information")=="Oracle"&&T(f,"source")=="RandomForeseer/0.13.19/SlipperyBridgePrediction"&&f.GetProperty("previewCount").GetInt32()==1&&f.GetProperty("repeatSame").GetBoolean()&&Same(f.GetProperty("eventBefore"),f.GetProperty("eventAfterPreview")),"source scope and prediction purity");start=r;
   }else if(kind=="slippery-return"){
    Owner(r);Need(relevant&&start.ValueKind==JsonValueKind.Object&&returned.ValueKind==JsonValueKind.Undefined,"required unique completion");var a=F(start);var f=F(r);int i=a.GetProperty("index").GetInt32();
    Need(f.GetProperty("index").GetInt32()==i&&Same(f.GetProperty("option"),a.GetProperty("option"))&&T(f,"forecastComparison")== (i==1?"Exact":"NotComparedCounterfactual"),"completion actual choice and comparison");
    Effect(i,S(start),S(r),a.GetProperty("eventBefore"),f.GetProperty("eventAfter"),a.GetProperty("forecast"),a.GetProperty("forecastRng"));returned=r;completed++;
   }else if(relevant&&kind is "room-option-return" or "confirmed"){
    Owner(r);Need(returned.ValueKind==JsonValueKind.Object&&Same(S(r),S(returned)),"completion required and complete stationary return state");
   }
  }
  return new{entries,completed};
 }
}
