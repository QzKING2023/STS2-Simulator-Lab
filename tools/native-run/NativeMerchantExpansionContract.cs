using System.Text.Json;
using System.Text.Json.Nodes;
using SimulatorLab.Core;
namespace OfflineSearchHarness;
// Finite transaction state machine. Only this profile admits real pickup/Hook/stock effects.
internal sealed class NativeMerchantExpansionContract
{
 internal static readonly HashSet<string> SnapshotKinds=["mc-effect-start","mc-effect-return","mc-stock-start","mc-stock-return","mc-hook-start","mc-hook-return","mc-pickup-start","mc-pickup-return","mc-callback","mc-failed"];
 readonly NativeMerchantEconomyContract naturalBusiness=new();
 static void R(bool b,string s){if(!b)throw new NativeDataException("Transaction "+s);}
 static string T(JsonElement e,string k)=>e.GetProperty(k).GetString()!;
 static string Ref(JsonElement e)=>e.ValueKind==JsonValueKind.Null?"":T(e,"$ref");
 static bool Eq(JsonElement a,JsonElement b)=>NativeDecisionTicket.SameActionJson(a.GetRawText(),b.GetRawText());
 static JsonElement[] A(JsonElement e,string k)=>e.GetProperty(k).EnumerateArray().ToArray();
 static JsonElement Entry(JsonElement s,string id)=>A(s.GetProperty("merchant"),"entries").Single(e=>Ref(e.GetProperty("reference"))==id);
 static void SameExcept(JsonElement a,JsonElement b,params string[] skip){R(a.EnumerateObject().Select(p=>p.Name).ToHashSet().SetEquals(b.EnumerateObject().Select(p=>p.Name)),"full field inventory");foreach(var p in a.EnumerateObject())if(!skip.Contains(p.Name))R(Eq(p.Value,b.GetProperty(p.Name)),"full state mismatch "+p.Name);}
 string root="",context="",kind="",op="",lastRoot="",lastContext="",scenario="";bool natural,setup,exited,done,pending,opened,returnedOnce;
 JsonElement dispatch,tail,decision,returned,before,effect,stock,hook,pickup,price,priceStartFacts,priceTail,priceLast,inventory,room;
 string stockMethod="";
 int phase,pickups,buys,prices;bool pricePending,failed,exitStarted,exitReturned;
 JsonElement exitStart,exitEnd,exitFacts;
 readonly HashSet<string> ops=[];
 readonly Dictionary<string,JsonElement> identities=[];
 readonly Dictionary<string,long> registeredAt=[];
 readonly Dictionary<string,int> generations=[];
 readonly HashSet<string> generatedObjects=[];
 long stockStarted,ordinal;
 JsonElement offeredSource;
 readonly Dictionary<string,JsonElement> priceRecords=[];
 internal void Accept(JsonElement p,JsonElement? snapshot)
 {
  ordinal++;string ev=T(p,"kind");var d=p.GetProperty("detail");var f=d.ValueKind==JsonValueKind.Object&&d.TryGetProperty("facts",out var v)?v:default;
  if(ev=="header"){scenario=T(d,"scenario");natural=scenario=="merchant-natural-relic";}
  if(natural)naturalBusiness.Accept(p,snapshot);
  if(!natural&&(ev.StartsWith("room-")||ev.StartsWith("merchant-stats-")))R(false,"unexpected natural room lifecycle in API fixture");
  if(ev=="identity"){identities.Add(T(d,"id"),d.Clone());registeredAt.Add(T(d,"id"),ordinal);}
  if(snapshot is {} observed)ValidateProjection(observed);
  if(ev=="root-request")R(!pending&&!done,"root before boundary/after contract");
  if(ev=="dispatch"){
   R(root=="","overlapping operation");root=T(p,"root");context=T(p,"context");kind=T(f,"kind");decision=f.GetProperty("decision").Clone();dispatch=snapshot!.Value.Clone();phase=pickups=0;opened=returnedOnce=failed=false;
   if(setup)R(Eq(tail,dispatch),"dispatch full confirmation binding");
  }
  if(ev=="n3d-open"){
   Owner(p);R(!opened&&!exited&&T(f,"kind")==kind&&Eq(f.GetProperty("decision"),decision)&&Eq(snapshot!.Value,dispatch)&&ops.Add(T(f,"id")),"open identity/owner/static/unique");opened=true;op=T(f,"id");
   if(kind=="MerchantBuy"){
    R(setup&&decision.EnumerateObject().Select(x=>x.Name).ToHashSet().SetEquals(["inventory","entry","offered","cost"])&&Eq(decision.GetProperty("inventory"),inventory),"buy inventory/version schema binding");before=Entry(dispatch,Ref(decision.GetProperty("entry"))).Clone();
    R(Eq(decision.GetProperty("offered"),before.GetProperty("content"))&&decision.GetProperty("cost").GetInt32()==before.GetProperty("cost").GetInt32(),"offered version/cost binding");
   }
  }
  if(ev=="merchant-price-start"){
   Owner(p);R(!pricePending&&opened&&(kind=="MerchantSetup"&&phase==0||kind=="MerchantBuy"&&phase==3)||natural&&!pricePending&&kind=="Travel","price lifecycle/unique");
   price=snapshot!.Value.Clone();
   var source=f.GetProperty("offeredSource");ValidateSource(source,"POTION");string generated=Ref(source.GetProperty("reference")),entryId=Ref(f.GetProperty("entry"));
   int generation=generations.GetValueOrDefault(entryId)+1;R(f.GetProperty("generation").GetInt32()==generation&&generatedObjects.Add(generated),"generated object unique generation");generations[entryId]=generation;
   if(phase==3){
    R(entryId==Ref(decision.GetProperty("entry"))&&registeredAt[generated]>stockStarted&&!ContainsIdentity(stock,generated),"refill generation fresh identity in actual stock lifecycle");
    var expected=JsonNode.Parse(stock.GetRawText())!;expected["playerRng"]!["rngs"]!["Shops"]=JsonNode.Parse(Advance(stock.GetProperty("playerRng").GetProperty("rngs").GetProperty("Shops"),2).GetRawText());
    var target=expected["merchant"]!["entries"]!.AsArray().Single(e=>e!["reference"]!["$ref"]!.GetValue<string>()==entryId)!;
    target["content"]=JsonNode.Parse(source.GetProperty("reference").GetRawText());target["model"]=T(source,"model");target["source"]=JsonNode.Parse(source.GetRawText());
    R(Eq(JsonSerializer.SerializeToElement(expected),price),"refill price start full generation state binding (native selection RNG before refill price)");
   }
   priceStartFacts=f.Clone();pricePending=true;
   if(prices>0&&kind=="MerchantSetup")R(Eq(priceTail,price),"generation full price chain");
   R(Eq(f.GetProperty("before"),price.GetProperty("playerRng").GetProperty("rngs").GetProperty("Shops")),"price before RNG binding");
  }
  if(ev=="merchant-price"){
   Owner(p);R(pricePending&&Eq(priceStartFacts.GetProperty("entry"),f.GetProperty("entry"))&&Eq(priceStartFacts.GetProperty("before"),f.GetProperty("before")),"price return unique/instance");
   R(Eq(priceStartFacts.GetProperty("offeredSource"),f.GetProperty("offeredSource"))&&priceStartFacts.GetProperty("generation").GetInt32()==f.GetProperty("generation").GetInt32(),"price source/generation unchanged");ValidateSource(f.GetProperty("offeredSource"),"POTION");R(T(f,"model")==T(f.GetProperty("offeredSource"),"model"),"price actual model source");
   var after=f.GetProperty("after");NativeMerchantEconomyContract.CheckRng(f.GetProperty("before"),after);
   R(after.GetProperty("counter").GetInt64()==f.GetProperty("before").GetProperty("counter").GetInt64()+1,"price RNG count");
   var expected=JsonNode.Parse(price.GetRawText())!;expected["playerRng"]!["rngs"]!["Shops"]=JsonNode.Parse(after.GetRawText());
   if(phase==3){var entry=expected["merchant"]!["entries"]!.AsArray().Single(e=>e!["reference"]!["$ref"]!.GetValue<string>()==Ref(f.GetProperty("entry")));entry!["cost"]=f.GetProperty("cost").GetInt32();}
   R(Eq(JsonSerializer.SerializeToElement(expected),snapshot!.Value),"price full state advance");
   int baseline=T(f,"rarity")=="Rare"?100:T(f,"rarity")=="Uncommon"?75:50;
   var rng=f.GetProperty("before");ulong output=unchecked(System.Numerics.BitOperations.RotateLeft(rng.GetProperty("s1").GetUInt64()*5,7)*9);double uniform=(output>>11)*(1.0/9007199254740992.0);float jitter=(float)(uniform*(double)(1.05f-.95f)+(double).95f);int raw=(int)MathF.Round(baseline*jitter);bool courier=A(snapshot.Value,"relics").Any(r=>T(r,"model")=="RELIC.THE_COURIER");R(f.GetProperty("cost").GetInt32()==(courier?(int)(raw*.8m):raw),"independent potion price/discount");
   priceRecords[Ref(f.GetProperty("entry"))]=f.Clone();priceTail=snapshot.Value.Clone();priceLast=f.Clone();prices++;pricePending=false;
  }
  if(SnapshotKinds.Contains(ev)){
   Owner(p);R(kind=="MerchantBuy"&&opened&&!returnedOnce,"child outside actual purchase");var s=snapshot!.Value;
   if(ev.StartsWith("mc-pickup")){R(phase==1&&Ref(f.GetProperty("relic"))==Ref(before.GetProperty("content")),"pickup owner/phase");if(ev=="mc-pickup-start"){R(pickups==0,"duplicate pickup start");pickup=s.Clone();pickups=1;}else{R(pickups==1&&Eq(pickup,s),"base pickup return full state/order");pickups=2;}return;}
   R(Ref(f.GetProperty("entry"))==Ref(decision.GetProperty("entry")),"child wrong entry");
   switch(ev){
    case "mc-effect-start":R(phase==0&&Eq(s,dispatch)&&Eq(f.GetProperty("offered"),before.GetProperty("content")),"effect dispatch binding");offeredSource=f.GetProperty("offeredSource").Clone();ValidateSource(offeredSource,T(before,"kind")=="MerchantRelicEntry"?"RELIC":"POTION");R(Eq(offeredSource,before.GetProperty("source"))&&Eq(offeredSource.GetProperty("reference"),before.GetProperty("content"))&&T(offeredSource,"model")==T(before,"model"),"effect actual source binding");R(!A(dispatch,"relics").Concat(A(dispatch,"potions")).Any(x=>T(x,"id")==Ref(before.GetProperty("content"))),"offered instance already owned");phase=1;break;
    case "mc-effect-return":R(phase==1&&f.GetProperty("success").GetBoolean()&&f.GetProperty("spent").GetInt32()==before.GetProperty("cost").GetInt32(),"effect result/order");ValidateEffect(s);effect=s.Clone();phase=2;break;
    case "mc-stock-start":R(phase==2&&Eq(effect,s),"stock start full binding");R(T(f,"method")== (A(dispatch,"relics").Any(r=>T(r,"model")=="RELIC.THE_COURIER")?"RestockAfterPurchase":"ClearAfterPurchase"),"native stock decision");stockMethod=T(f,"method");stock=s.Clone();stockStarted=ordinal;phase=3;break;
    case "mc-stock-return":R(phase==3&&!pricePending&&T(f,"method")==stockMethod,"stock return order/method");ValidateStock(stock,s);stock=s.Clone();phase=4;break;
    case "mc-hook-start":R(phase==4&&Eq(stock,s),"Hook start full stock binding");hook=s.Clone();phase=5;break;
    case "mc-hook-return":R(phase==5,"Hook return phase");ValidateHook(hook,s);hook=s.Clone();phase=6;break;
    case "mc-callback":R(phase==6&&Eq(hook,s),"callback after real Hook/static");phase=7;break;
    case "mc-failed":R(phase==0&&!failed&&Eq(s,dispatch)&&T(f,"failure")=="FailureGold"&&dispatch.GetProperty("gold").GetInt32()<before.GetProperty("cost").GetInt32(),"failed buy complete/static/reason");failed=true;phase=8;break;
   }
  }
  if(ev=="merchant-exit-start"){
   Owner(p);R(setup&&!exitStarted&&!exitReturned&&kind is "MerchantExit" or "MerchantTravel"&&Eq(f.GetProperty("inventory"),inventory)&&Eq(f.GetProperty("room"),room),"exit start required owner/identity/unique");
   if(!natural)R(Eq(dispatch,snapshot!.Value),"fixture exit dispatch full binding");exitStart=snapshot!.Value.Clone();exitFacts=f.Clone();exitStarted=true;
  }
  if(ev=="merchant-exit-return"){
   Owner(p);R(exitStarted&&!exitReturned&&Eq(exitFacts,f),"exit return identity/required order/unique");var expected=JsonNode.Parse(exitStart.GetRawText())!;expected["merchant"]!["closed"]=true;expected["mapHistory"]=JsonNode.Parse(snapshot!.Value.GetProperty("mapHistory").GetRawText());
   R(Eq(JsonSerializer.SerializeToElement(expected),snapshot.Value),"exit full non-history state binding");NativeMerchantEconomyContract.CheckExitHistory(exitStart,snapshot.Value);exitEnd=snapshot.Value.Clone();exitReturned=true;
  }
  if(ev=="n3d-return"){
   Owner(p);R(opened&&!returnedOnce&&T(f,"id")==op&&T(f,"kind")==kind,"return owner/required unique");returned=snapshot!.Value.Clone();var result=f.GetProperty("result");
   if(kind is "MerchantSetup" or "MerchantAttach"){
    R(!setup&&prices==3&&!pricePending,"completed initial native inventory");var m=returned.GetProperty("merchant");inventory=m.GetProperty("reference").Clone();room=m.GetProperty("room").Clone();R(T(identities[Ref(inventory)],"type")=="MerchantInventory"&&T(identities[Ref(room)],"type")=="MerchantRoom"&&Eq(result.GetProperty("nativeInventory"),inventory),"real inventory/room identity");
    var entries=A(m,"entries");R(T(returned,"room")=="MerchantRoom"&&entries.Length==14&&entries.Select(e=>Ref(e.GetProperty("reference"))).Distinct().Count()==14&&entries.Count(e=>T(e,"kind")=="MerchantCardEntry")==7&&entries.Count(e=>T(e,"kind")=="MerchantRelicEntry")==3&&entries.Count(e=>T(e,"kind")=="MerchantPotionEntry")==3,"original inventory cardinality/type/identity");
    foreach(var potion in entries.Where(e=>T(e,"kind")=="MerchantPotionEntry"))R(priceRecords.TryGetValue(Ref(potion.GetProperty("reference")),out var recorded)&&Eq(recorded.GetProperty("offeredSource").GetProperty("reference"),potion.GetProperty("content"))&&T(recorded,"model")==T(potion,"model")&&recorded.GetProperty("cost").GetInt32()==potion.GetProperty("cost").GetInt32(),"initial generated instance/price inventory binding");
    if(kind=="MerchantSetup"){
     var expected=JsonNode.Parse(priceTail.GetRawText())!;expected["merchant"]=JsonNode.Parse(returned.GetProperty("merchant").GetRawText());
     if(A(priceTail,"transactionRelics").Any(r=>T(r,"model")=="RELIC.MAW_BANK"&&!r.GetProperty("bought").GetBoolean())){
      expected["gold"]=priceTail.GetProperty("gold").GetInt32()+12;var player=expected["mapHistory"]!.AsArray().Last()!.AsArray().Last()!["player"]!;player["gold_gained"]=player["gold_gained"]!.GetValue<int>()+12;
     }
     if(scenario=="merchant-native-poor"){
      int gold=expected["gold"]!.GetValue<int>();expected["gold"]=0;var player=expected["mapHistory"]!.AsArray().Last()!.AsArray().Last()!["player"]!;player["gold_spent"]=player["gold_spent"]!.GetValue<int>()+gold;
     }
     R(Eq(JsonSerializer.SerializeToElement(expected),returned),"setup generated parent full binding including declared loss/native room Hook");
    }
    if(kind=="MerchantAttach"){R(Eq(decision.GetProperty("room"),room)&&Eq(dispatch.GetProperty("rooms").GetProperty("reference"),room),"Attach current room");SameExcept(dispatch,returned,"merchant");}setup=true;
   }else if(kind=="MerchantBuy"){
    R(result.EnumerateObject().Select(x=>x.Name).ToHashSet().SetEquals(["success","cost","spent","completed","failure","offered","acquired","restocked","replacement"]),"transaction result schema");bool ok=result.GetProperty("success").GetBoolean();R(phase==(ok?7:8)&&ok==!failed&&result.GetProperty("completed").GetInt32()==(ok?1:0),"mandatory transaction lifecycle");
    R(Eq(returned,ok?hook:dispatch)&&Eq(result.GetProperty("offered"),before.GetProperty("content"))&&result.GetProperty("cost").GetInt32()==before.GetProperty("cost").GetInt32()&&result.GetProperty("spent").GetInt32()==(ok?before.GetProperty("cost").GetInt32():0),"return full state/cost");
    R(ok?Eq(result.GetProperty("acquired"),before.GetProperty("content"))&&result.GetProperty("failure").ValueKind==JsonValueKind.Null:result.GetProperty("acquired").ValueKind==JsonValueKind.Null&&T(result,"failure")=="FailureGold","actual receipt/failure");
    var finalEntry=Entry(returned,Ref(decision.GetProperty("entry")));R(result.GetProperty("restocked").GetBoolean()==(ok&&finalEntry.GetProperty("stock").GetBoolean())&&Eq(result.GetProperty("replacement"),finalEntry.GetProperty("content")),"stock receipt/version binding");buys++;
   }else if(kind is "MerchantExit" or "MerchantTravel"){R(exitStarted&&exitReturned&&(!natural?Eq(exitEnd,returned):true),"completed original exit full return");if(natural)R(T(result,"from")==T(decision,"from")&&T(result,"to")==T(decision,"to")&&T(returned,"node")==T(decision,"to"),"Travel result action/destination binding");R(setup&&result.GetProperty("closed").GetBoolean()&&returned.GetProperty("merchant").GetProperty("closed").GetBoolean(),"real exit");exited=true;}
   else R(false,"unknown operation");returnedOnce=true;
  }
  if(ev=="confirmed"){
   Owner(p);if(opened){R(returnedOnce&&Eq(returned,snapshot!.Value),"confirmation before return/full state");pending=true;}
   tail=snapshot!.Value.Clone();lastRoot=root;lastContext=context;root="";
  }
  if(ev=="n3d-boundary"){R(root==""&&pending&&T(p,"root")==lastRoot&&T(p,"context")==lastContext&&T(f,"id")==op&&T(f,"kind")==kind&&Eq(tail,snapshot!.Value),"boundary owner/full state/unique");pending=false;}
  if(ev=="n3d-contract"){R(!done&&!pending&&root==""&&setup&&exited&&Eq(tail,snapshot!.Value)&&T(f,"scenario")==scenario&&f.GetProperty("operations").GetInt32()==ops.Count&&f.GetProperty("valid").GetBoolean()&&f.GetProperty("fixture").GetBoolean()==!natural,"final contract/static/origin");done=true;}
  if(setup&&snapshot is {} st&&st.GetProperty("merchant").ValueKind==JsonValueKind.Object)R(Eq(st.GetProperty("merchant").GetProperty("reference"),inventory)&&Eq(st.GetProperty("merchant").GetProperty("room"),room),"immutable room/inventory ownership");
 }
 void Owner(JsonElement p)=>R(root!=""&&T(p,"root")==root&&T(p,"context")==context,"operation root/context owner");
 void ValidateEffect(JsonElement s)
 {
  string field=T(before,"kind")=="MerchantRelicEntry"?"relics":"potions";string id=Ref(before.GetProperty("content"));var a=A(dispatch,field);var b=A(s,field);
  R(b.Length==a.Length+1&&a.Zip(b).All(z=>Eq(z.First,z.Second))&&T(b[^1],"id")==id&&T(b[^1],"model")==T(before,"model"),"real acquired exact instance");
  R(s.GetProperty("gold").GetInt32()==dispatch.GetProperty("gold").GetInt32()-before.GetProperty("cost").GetInt32(),"real payment");
  if(field=="relics"){R(pickups==2&&Eq(pickup,s),"mandatory actual base AfterObtained pair");var aa=A(dispatch,"transactionRelics");var bb=A(s,"transactionRelics");R(bb.Length==aa.Length+1&&aa.Zip(bb).All(z=>Eq(z.First,z.Second))&&Eq(bb[^1],offeredSource.GetProperty("relicState")),"relic state acquisition");}
  else R(pickups==0&&Eq(dispatch.GetProperty("transactionRelics"),s.GetProperty("transactionRelics")),"unexpected relic pickup/state");
  SameExcept(dispatch,s,"gold",field,"transactionRelics","mapHistory");
  // Original native purchase adds exactly one model to this field, no other history mutation.
  var history=JsonNode.Parse(dispatch.GetProperty("mapHistory").GetRawText())!;
  var player=history.AsArray().Last()!.AsArray().Last()!["player"]!;
  player["gold_spent"]=player["gold_spent"]!.GetValue<int>()+before.GetProperty("cost").GetInt32();
  var parts=T(before,"model").Split('.');var model=new JsonObject{["Category"]=parts[0],["Entry"]=parts[1]};player[field=="relics"?"bought_relics":"bought_potions"]!.AsArray().Add(new JsonObject{["Category"]=parts[0],["Entry"]=parts[1]});
  player[field=="relics"?"relic_choices":"potion_choices"]!.AsArray().Add(new JsonObject{["choice"]=model.DeepClone(),["was_picked"]=true});
  R(Eq(JsonSerializer.SerializeToElement(history),s.GetProperty("mapHistory")),"original acquisition history");
 }
 void ValidateStock(JsonElement a,JsonElement b)
 {
  string id=Ref(decision.GetProperty("entry"));var old=Entry(a,id);var now=Entry(b,id);bool refill=A(a,"relics").Any(r=>T(r,"model")=="RELIC.THE_COURIER");
  SameExcept(a,b,"merchant","playerRng");var expectedRng=JsonNode.Parse(a.GetProperty("playerRng").GetRawText())!;expectedRng["rngs"]!["Shops"]=JsonNode.Parse(b.GetProperty("playerRng").GetProperty("rngs").GetProperty("Shops").GetRawText());R(Eq(JsonSerializer.SerializeToElement(expectedRng),b.GetProperty("playerRng")),"stock changed non-Shop RNG");SameExcept(a.GetProperty("merchant"),b.GetProperty("merchant"),"entries");
  foreach(var e in A(a.GetProperty("merchant"),"entries"))if(Ref(e.GetProperty("reference"))!=id)R(Eq(e,Entry(b,Ref(e.GetProperty("reference")))),"unrelated inventory entry mutation");
  if(refill){R(Eq(priceTail,b),"stock return full price tail binding");R(T(old,"kind")=="MerchantPotionEntry"&&now.GetProperty("stock").GetBoolean()&&Ref(now.GetProperty("content"))!=""&&Ref(now.GetProperty("content"))!=Ref(old.GetProperty("content")),"actual replenished new potion identity");R(Eq(priceTail.GetProperty("playerRng"),b.GetProperty("playerRng"))&&prices==3+buys+1,"actual refill price RNG");var rngBefore=a.GetProperty("playerRng").GetProperty("rngs").GetProperty("Shops");R(b.GetProperty("playerRng").GetProperty("rngs").GetProperty("Shops").GetProperty("counter").GetInt64()==rngBefore.GetProperty("counter").GetInt64()+3,"native potion selection plus price draws");R(Ref(priceLast.GetProperty("entry"))==id&&T(priceLast,"model")==T(now,"model")&&priceLast.GetProperty("cost").GetInt32()==now.GetProperty("cost").GetInt32(),"generated replacement/price binding");SameExcept(old,now,"stock","content","model","cost","source");}
  else{R(!now.GetProperty("stock").GetBoolean()&&now.GetProperty("content").ValueKind==JsonValueKind.Null&&now.GetProperty("model").ValueKind==JsonValueKind.Null&&now.GetProperty("source").ValueKind==JsonValueKind.Null,"actual clear");R(Eq(a.GetProperty("playerRng"),b.GetProperty("playerRng")),"clear RNG unchanged");SameExcept(old,now,"stock","content","model","source");}
 }
 static void ValidateHook(JsonElement a,JsonElement b)
 {
  SameExcept(a,b,"transactionRelics");var old=A(a,"transactionRelics");var now=A(b,"transactionRelics");R(old.Length==now.Length,"Hook relic inventory cardinality");
  for(int i=0;i<old.Length;i++){
   if(T(old[i],"model")!="RELIC.MAW_BANK"||old[i].GetProperty("bought").GetBoolean()){R(Eq(old[i],now[i]),"unknown purchase Hook effect");continue;}
   var expected=JsonNode.Parse(old[i].GetRawText())!;expected["bought"]=true;expected["IsUsedUp"]=true;expected["status"]="Disabled";R(Eq(JsonSerializer.SerializeToElement(expected),now[i]),"independent real MawBank purchase effect");
  }
 }
 static bool ContainsIdentity(JsonElement e,string id)
 {
  if(e.ValueKind==JsonValueKind.Object)return e.EnumerateObject().Any(p=>(p.Name is "id" or "$ref"&&p.Value.ValueKind==JsonValueKind.String&&p.Value.GetString()==id)||ContainsIdentity(p.Value,id));
  return e.ValueKind==JsonValueKind.Array&&e.EnumerateArray().Any(v=>ContainsIdentity(v,id));
 }
 void ModelIdentity(string id,string model,string category)
 {
  R(identities.TryGetValue(id,out var registered)&&T(registered,"model")==model&&model.StartsWith(category+".",StringComparison.Ordinal),"registered identity/model/category binding");
 }
 void ValidateSource(JsonElement source,string category)
 {
  R(source.EnumerateObject().Select(p=>p.Name).ToHashSet().SetEquals(["reference","type","model","relicState"]),"source complete schema");
  string id=Ref(source.GetProperty("reference"));ModelIdentity(id,T(source,"model"),category);R(T(identities[id],"type")==T(source,"type"),"source registered concrete type");
  var state=source.GetProperty("relicState");
  if(category=="RELIC"){
   R(state.ValueKind==JsonValueKind.Object&&T(state,"id")==id&&T(state,"model")==T(source,"model"),"offered relic state identity");ValidateRelicShape(state);
  }else R(state.ValueKind==JsonValueKind.Null,"potion source no relic state");
 }
 static void ValidateRelicShape(JsonElement r)=>R(r.EnumerateObject().Select(p=>p.Name).ToHashSet().SetEquals(["id","model","status","IsUsedUp","bought"]),"complete relic source state schema");
 void ValidateProjection(JsonElement s)
 {
  foreach(var (field,category) in new[]{("relics","RELIC"),("potions","POTION")})foreach(var item in A(s,field))ModelIdentity(T(item,"id"),T(item,"model"),category);
  var relics=A(s,"relics");var states=A(s,"transactionRelics");R(relics.Length==states.Length,"complete relic state projection");
  for(int i=0;i<relics.Length;i++){ValidateRelicShape(states[i]);R(T(relics[i],"id")==T(states[i],"id")&&T(relics[i],"model")==T(states[i],"model"),"relic projections refer to same actual object");}
  if(s.GetProperty("merchant").ValueKind==JsonValueKind.Object){
   var contents=new HashSet<string>();
   foreach(var entry in A(s.GetProperty("merchant"),"entries"))if(entry.GetProperty("content").ValueKind==JsonValueKind.Object){
    string id=Ref(entry.GetProperty("content"));string category=T(entry,"kind")=="MerchantRelicEntry"?"RELIC":T(entry,"kind")=="MerchantPotionEntry"?"POTION":"CARD";
    ModelIdentity(id,T(entry,"model"),category);ValidateSource(entry.GetProperty("source"),category);R(Ref(entry.GetProperty("source").GetProperty("reference"))==id&&T(entry.GetProperty("source"),"model")==T(entry,"model"),"inventory source actual object binding");R(contents.Add(id),"unique stocked object identity");
    R(!A(s,"relics").Concat(A(s,"potions")).Any(x=>T(x,"id")==id)||phase is 1 or 2 or 3,"stock cannot alias an owned object outside acquisition interval");
   }
  }
 }
 static JsonElement Advance(JsonElement initial,int count)
 {
  ulong a=initial.GetProperty("s0").GetUInt64(),b=initial.GetProperty("s1").GetUInt64(),c=initial.GetProperty("s2").GetUInt64(),d=initial.GetProperty("s3").GetUInt64();
  for(int i=0;i<count;i++){ulong t=b<<17;c^=a;d^=b;b^=c;a^=d;c^=t;d=System.Numerics.BitOperations.RotateLeft(d,45);}
  return JsonSerializer.SerializeToElement(new{counter=initial.GetProperty("counter").GetInt64()+count,s0=a,s1=b,s2=c,s3=d});
 }
 internal void Finish(){R(done&&root==""&&!pending&&!pricePending&&setup&&exited&&exitStarted&&exitReturned&&buys==(scenario=="merchant-native-courier"?2:1),"complete required transactions");if(natural)naturalBusiness.Finish();}
}
