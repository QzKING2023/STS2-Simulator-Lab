using System.Text.Json.Nodes;
namespace OfflineSearchHarness;
internal static class NativeMerchantExpansionControls
{
 internal static void Apply(List<JsonObject> rows,string control)
 {
  static string K(JsonObject r)=>r["payload"]!["kind"]!.GetValue<string>();
  static JsonObject S(JsonObject r)=>r["payload"]!["detail"]!["state"]!.AsObject();
  static JsonObject F(JsonObject r)=>r["payload"]!["detail"]!["facts"]!.AsObject();
  JsonObject Buy()=>rows.First(r=>K(r)=="n3d-return"&&F(r)["kind"]!.GetValue<string>()=="MerchantBuy");
  if(control.StartsWith("binding:",StringComparison.Ordinal)){Binding(rows,control.Split(':'));return;}
  var parts=control.Split('|');
  if(parts.Length==2){var row=rows.First(r=>K(r)==parts[1]&&(parts[1] is not ("n3d-return" or "n3d-boundary")||F(r)["kind"]!.GetValue<string>()=="MerchantBuy"));
   switch(parts[0]){
    case "missing":rows.Remove(row);break;
    case "duplicate":rows.Insert(rows.IndexOf(row),row.DeepClone().AsObject());break;
    case "root":row["payload"]!["root"]="wrong-root";break;
    case "context":row["payload"]!["context"]="wrong-context";break;
    case "state-missing":row["payload"]!["detail"]!.AsObject().Remove("state");break;
    case "state-null":row["payload"]!["detail"]!["state"]=null;break;
    case "extension":S(row)["futureState"]=new JsonObject{["nested"]=3};break;
    case "before":rows.Remove(row);rows.Insert(rows.IndexOf(rows.First(r=>K(r)=="n3d-open"&&F(r)["kind"]!.GetValue<string>()=="MerchantBuy")),row);break;
    default:throw new ArgumentException(control);
   }return;
  }
  if(control=="equivalent-order"){foreach(var r in rows)if(r["payload"]?["detail"]?["state"] is JsonObject s)r["payload"]!["detail"]!["state"]=new JsonObject(s.Reverse().Select(x=>KeyValuePair.Create(x.Key,x.Value?.DeepClone())));return;}
  if(control.StartsWith("coedit-")){
   string[] kinds=control switch{
    "coedit-stock-hook"=>["mc-stock-return","mc-hook-start"],
    "coedit-effect-pickup"=>["mc-effect-return","mc-pickup-start","mc-pickup-return"],
    "coedit-callback-return"=>["mc-hook-return","mc-callback","n3d-return"],_=>throw new ArgumentException(control)};
   foreach(var r in rows.Where(r=>kinds.Contains(K(r))))S(r)["futureState"]=new JsonObject{["nested"]=3};return;
  }
  if(control.StartsWith("rng-before-start")){
   var r=rows.Where(r=>K(r)=="merchant-price-start").Skip(3).First();var rng=S(r)["playerRng"]!["rngs"]!["Shops"]!;
   if(control.EndsWith("counter"))rng["counter"]=rng["counter"]!.GetValue<long>()+1;else rng["s0"]=rng["s0"]!.GetValue<ulong>()^1UL;
   F(r)["before"]=rng.DeepClone();return;
  }
  if(control.StartsWith("exit-coedit-")||control.StartsWith("setup-coedit-")){
   bool begin=false;foreach(var r in rows){
    if(control.StartsWith("exit-")&&K(r)=="merchant-exit-return"||control.StartsWith("setup-")&&K(r)=="n3d-return"&&F(r)["kind"]!.GetValue<string>()=="MerchantSetup")begin=true;
    if(begin&&r["payload"]?["detail"]?["state"] is JsonObject st){if(control.EndsWith("gold"))st["gold"]=st["gold"]!.GetValue<int>()+7;else st["futureExitState"]=new JsonObject{["nested"]=1};}
   }return;
  }
  if(control=="exit-missing-all"){rows.RemoveAll(r=>K(r) is "merchant-exit-start" or "merchant-exit-return");return;}
  if(control=="exit-facts-wrong-room"){foreach(var r in rows.Where(r=>K(r) is "merchant-exit-start" or "merchant-exit-return"))F(r)["room"]=new JsonObject{["$ref"]=S(r)["relics"]![0]!["id"]!.GetValue<string>()};return;}
  if(control=="exit-history-coedit"){bool begin=false;foreach(var r in rows){if(K(r)=="merchant-exit-return")begin=true;if(begin&&r["payload"]?["detail"]?["state"] is JsonObject st)st["mapHistory"]!.AsArray().Last()!.AsArray().Last()!["player"]!["gold_spent"]=0;}return;}
  if(control=="equivalent-extension"){foreach(var r in rows)if(r["payload"]?["detail"]?["state"] is JsonObject st)st["futureState"]=new JsonObject{["nested"]=3};return;}
  if(control=="initial-inventory-price-coedit"){
   foreach(var r in rows)if(r["payload"]?["detail"]?["state"]?["merchant"] is JsonObject m)foreach(var e in m["entries"]!.AsArray().Where(e=>e!["kind"]!.GetValue<string>()=="MerchantPotionEntry"))e!["cost"]=e["cost"]!.GetValue<int>()+1;return;
  }
  var buy=Buy();
  if(control=="receipt-old")F(buy)["result"]!["acquired"]=new JsonObject{["$ref"]=S(buy)["relics"]![0]!["id"]!.GetValue<string>()};
  else if(control=="replacement-old")F(buy)["result"]!["replacement"]=F(buy)["result"]!["offered"]!.DeepClone();
  else if(control=="price-cost"){var r=rows.First(r=>K(r)=="merchant-price");F(r)["cost"]=0;}
  else if(control=="hook-rollback"){foreach(var r in rows.Where(r=>K(r)=="mc-hook-return"))S(r)["gold"]=0;}
  else throw new ArgumentException(control);
 }
 // Controls use actual object references and root IDs from the source, never seed-specific IDs.
 static void Binding(List<JsonObject> rows,string[] args)
 {
  string mode=args[1];int n=int.Parse(args[2]);string change=args[3];
  static string K(JsonObject r)=>r["payload"]!["kind"]!.GetValue<string>();
  static JsonObject S(JsonObject r)=>r["payload"]!["detail"]!["state"]!.AsObject();
  static JsonObject F(JsonObject r)=>r["payload"]!["detail"]!["facts"]!.AsObject();
  var buys=rows.Where(r=>K(r)=="mc-stock-start").ToArray();
  if(mode=="price"){
   string root=buys[n]["payload"]!["root"]!.GetValue<string>();
   var pair=rows.Where(r=>r["payload"]!["root"]!.GetValue<string>()==root&&K(r) is "merchant-price-start" or "merchant-price").ToArray();
   string entry=F(pair[0])["entry"]!["$ref"]!.GetValue<string>();
   foreach(var r in pair){
    if(change=="gold")S(r)["gold"]=S(r)["gold"]!.GetValue<int>()+7;
    else if(change=="extension")S(r)["onlyDuringPrice"]=new JsonObject{["nested"]=1};
    else if(change=="content"){
     var es=S(r)["merchant"]!["entries"]!.AsArray();var target=es.Single(e=>e!["reference"]!["$ref"]!.GetValue<string>()==entry)!;target["content"]=es.First(e=>e!["kind"]!.GetValue<string>()=="MerchantPotionEntry"&&e["reference"]!["$ref"]!.GetValue<string>()!=entry)!["content"]!.DeepClone();
    }else if(change=="other-entry"){var es=S(r)["merchant"]!["entries"]!.AsArray();es.First(e=>e!["reference"]!["$ref"]!.GetValue<string>()!=entry)!["cost"]=1;}
    else if(change=="generation")F(r)["generation"]=1;
    else throw new ArgumentException(change);
   }return;
  }
  if(mode=="source"){
   string kind=change.StartsWith("effect-")?"mc-effect-start":"merchant-price-start";var r=rows.Where(r=>K(r)==kind).Skip(kind=="merchant-price-start"?3+n:n).First();var f=F(r);
   string field=change.Replace("effect-","");
   if(field=="missing")f.Remove("offeredSource");
   else if(field=="null")f["offeredSource"]=null;
   else if(field=="generation")f["generation"]=0;
   else if(field=="type")f["offeredSource"]!["type"]="BurningBlood";
   else if(field=="model")f["offeredSource"]!["model"]="RELIC.BURNING_BLOOD";
   else if(field=="extra")f["offeredSource"]!["unknown"]=new JsonObject{["nested"]=1};
   else throw new ArgumentException(change);return;
  }
  if(mode=="relic"){
   var source=rows.First(r=>K(r)=="mc-effect-start");string id=F(source)["offered"]!["$ref"]!.GetValue<string>();
   foreach(var r in rows)if(r["payload"]?["detail"]?["state"]?["transactionRelics"] is JsonArray rs)foreach(var relic in rs.Where(x=>x!["id"]!.GetValue<string>()==id)){
    if(change=="model")relic!["model"]="RELIC.BURNING_BLOOD";
    else if(change=="status")relic!["status"]="Disabled";
    else if(change=="bought")relic!["bought"]=true;
    else if(change=="extra")relic!["unknown"]=new JsonObject{["nested"]=1};
    else throw new ArgumentException(change);
   }return;
  }
  var starts=rows.Where(r=>K(r)=="merchant-price-start").Skip(3).ToArray();var start=starts[n];string generated=F(start)["offeredSource"]!["reference"]!["$ref"]!.GetValue<string>();
  if(mode=="identity"){
   var id=rows.First(r=>K(r)=="identity"&&r["payload"]!["detail"]!["id"]!.GetValue<string>()==generated)["payload"]!["detail"]!;
   if(change=="type")id["type"]="BurningBlood";else if(change=="model")id["model"]="RELIC.BURNING_BLOOD";else throw new ArgumentException(change);return;
  }
  if(mode=="reuse"){
   var old=S(buys[n])["potions"]!.AsArray().First()!["id"]!.GetValue<string>();
   void Replace(JsonNode? v){if(v is JsonObject obj){foreach(var p in obj.ToArray())if(p.Key is "$ref" or "id"&&p.Value is JsonValue val&&val.TryGetValue<string>(out var s)&&s==generated)obj[p.Key]=old;else Replace(p.Value);}else if(v is JsonArray arr)foreach(var x in arr)Replace(x);}
   foreach(var r in rows.Skip(rows.IndexOf(start)))Replace(r["payload"]!["detail"]);return;
  }
  throw new ArgumentException(mode);
 }

}
