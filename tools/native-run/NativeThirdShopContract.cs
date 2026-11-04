using System.Text.Json;
using System.Text.Json.Nodes;
namespace OfflineSearchHarness;

// A finite native-session contract, not a copy of card/relic effect execution.
internal sealed class NativeThirdShopContract
{
 internal static readonly HashSet<string> Kinds=["third-shop-attached","third-shop-buy-start","third-shop-buy-return","third-shop-complete","third-shop-exit-start","third-shop-exit-return"];
 readonly NativeShopLedgerContract ledger=new();bool ledgerEnabled,fullRun;
 bool applicable,enabled,attached,completed,buying,exitPending,closed;
 string scenario="",root="",context="",kind="",lastRoot="",lastContext="";
 JsonElement actualPlayer;
 JsonElement dispatch,action,confirmed,tail,inventory,room,buyStart,buyFacts,price,priceFacts,priceTail,exitState,exitFacts;
 int bought,prices;readonly Dictionary<string,JsonElement> priced=[];
 readonly Dictionary<string,(string type,string? model)> identities=[];
 readonly Dictionary<string,int> baseCosts=[];
 static string T(JsonElement e,string key)=>e.GetProperty(key).GetString()!;
 static string Ref(JsonElement e)=>e.ValueKind==JsonValueKind.Null?"":T(e,"$ref");
 static bool Eq(JsonElement a,JsonElement b)=>NativeBoundaryContract.Equal(a,b);
 static void R(bool v,string m){if(!v)throw new NativeDataException("Third shop "+m);}
 static JsonElement[] A(JsonElement e)=>e.EnumerateArray().ToArray();
 static void SameExcept(JsonElement a,JsonElement b,params string[] skip){
  R(a.ValueKind==JsonValueKind.Object&&b.ValueKind==JsonValueKind.Object&&a.EnumerateObject().Select(p=>p.Name).ToHashSet().SetEquals(b.EnumerateObject().Select(p=>p.Name)),"full object fields");
  foreach(var p in a.EnumerateObject())if(!skip.Contains(p.Name))R(Eq(p.Value,b.GetProperty(p.Name)),"unexpected state change "+p.Name);
 }
 static JsonElement Entry(JsonElement s,string id)=>A(s.GetProperty("merchant").GetProperty("entries")).Single(x=>Ref(x.GetProperty("reference"))==id);
 void Owner(JsonElement p)=>R(root!=""&&T(p,"root")==root&&T(p,"context")==context,"current action owner");
 Type ModelType(string id,string entryKind,string model)
 {
  R(identities.TryGetValue(id,out var registered)&&registered.model==model,"offered registered model");
  (Type category,string folder)=entryKind switch{
   "MerchantCardEntry"=>(typeof(MegaCrit.Sts2.Core.Models.CardModel),"Cards"),
   "MerchantRelicEntry"=>(typeof(MegaCrit.Sts2.Core.Models.RelicModel),"Relics"),
   "MerchantPotionEntry"=>(typeof(MegaCrit.Sts2.Core.Models.PotionModel),"Potions"),
   _=>throw new NativeDataException("Third shop unregistered model category")};
  var type=category.Assembly.GetType("MegaCrit.Sts2.Core.Models."+folder+"."+registered.type);
  // GetId(Type) only derives category/entry from CLR metadata. No model instance,
  // ModelDb.Init, canonical collection, UI, or game lifecycle is constructed.
  R(type!=null&&!type.IsAbstract&&category.IsAssignableFrom(type)&&MegaCrit.Sts2.Core.Models.ModelDb.GetId(type).ToString()==model,"registered concrete type/model/entry category");
  return type!;
 }
 void CheckEntry(JsonElement entry)
 {
  string kind=T(entry,"kind");R(identities.TryGetValue(Ref(entry.GetProperty("reference")),out var registered)&&registered.type==kind&&registered.model==null,"actual registered merchant entry type");
  if(kind=="MerchantCardRemovalEntry"){R(entry.GetProperty("content").ValueKind==JsonValueKind.Null&&entry.GetProperty("model").ValueKind==JsonValueKind.Null,"removal has no offered model");return;}
  _=ModelType(Ref(entry.GetProperty("content")),kind,T(entry,"model"));
 }
 JsonElement ExitInput()
 {
  R(T(dispatch,"room")=="MerchantRoom"&&Eq(dispatch,tail)&&Eq(dispatch.GetProperty("rooms").GetProperty("reference"),room)&&Eq(dispatch.GetProperty("merchant").GetProperty("reference"),inventory),"exit dispatch current completed inventory/full state");
  var expected=JsonNode.Parse(dispatch.GetRawText())!;
  expected["node"]=T(action.GetProperty("decision"),"to");expected["floor"]=dispatch.GetProperty("floor").GetInt32()+1;expected["room"]=null;expected["rooms"]=null;
  // Cost is a getter: after PopRoom it returns _cost, without merchant Hooks.
  // Original entry prices are anchored at attachment before any new discount.
  foreach(var entry in expected["merchant"]!["entries"]!.AsArray())entry!["cost"]=baseCosts[entry["reference"]!["$ref"]!.GetValue<string>()];
  return JsonSerializer.SerializeToElement(expected);
 }
 internal void Accept(JsonElement p,JsonElement? snapshot){
  string k=T(p,"kind");var d=p.GetProperty("detail");var f=d.ValueKind==JsonValueKind.Object&&d.TryGetProperty("facts",out var facts)?facts:default;
  if(k=="header"){if(d.TryGetProperty("fullRunProfile",out var runProfile)&&runProfile.ValueKind==JsonValueKind.Object){NativeFullRun.Validate(runProfile.Deserialize<NativeFullRun.Limits>(Program.Json));fullRun=true;}scenario=T(d,"scenario");applicable=NativeThirdShop.IsScenario(scenario);enabled=applicable&&d.TryGetProperty("integratedShopProfile",out var profile)&&profile.ValueKind==JsonValueKind.String&&profile.GetString()==NativeThirdShop.Profile;ledgerEnabled=enabled&&d.TryGetProperty("shopLedgerProfile",out var ledgerProfile)&&ledgerProfile.ValueKind==JsonValueKind.String&&ledgerProfile.GetString()=="native-shop-ledger/v1";R(!NativeSolverJourney.ShopScenario(scenario)||enabled,"required declared integrated shop profile");}
  if(!applicable){R(!Kinds.Contains(k),"integrated records outside registered scenario");return;}
  if(!enabled){R(!Kinds.Contains(k)&&k is not ("merchant-price" or "merchant-price-start")&&(k!="dispatch"||T(f,"kind")!="ThirdShop"),"undeclared integrated session");return;}
  if(k=="identity")identities.Add(T(d,"id"),(T(d,"type"),d.TryGetProperty("model",out var m)&&m.ValueKind==JsonValueKind.String?m.GetString():null));
  if(k=="major-battle-start"&&f.TryGetProperty("player",out var playerRef)){if(actualPlayer.ValueKind!=JsonValueKind.Undefined)R(Eq(actualPlayer,playerRef),"immutable native player");actualPlayer=playerRef.Clone();}
  if(k=="dispatch"){
   R(root=="","overlapping action");root=T(p,"root");context=T(p,"context");kind=T(f,"kind");action=f.Clone();dispatch=snapshot!.Value.Clone();
   if(kind=="ThirdShop")R(!attached&&prices==3&&T(dispatch,"room")=="MerchantRoom"&&(fullRun?NativeDarv.IsActTwoProfile(scenario)&&dispatch.GetProperty("act").GetInt32() is 0 or 1 or 2:NativeThirdShop.ValidAct(scenario,dispatch.GetProperty("act").GetInt32()))&&dispatch.GetProperty("naturalMap").GetProperty("act").GetInt32()==dispatch.GetProperty("act").GetInt32(),"attachment actual registered act/current map/three generated prices");
   if(kind=="Travel"){R(!attached||completed,"travel before shop complete");prices=0;priced.Clear();priceTail=default;}
  }
  if(k=="merchant-price-start"){
   R(f.EnumerateObject().Select(x=>x.Name).ToHashSet().SetEquals(["entry","before"]),"price start closed facts");
   Owner(p);R(kind=="Travel"&&prices<3&&price.ValueKind==JsonValueKind.Undefined&&T(snapshot!.Value,"room")=="MerchantRoom","native inventory price lifecycle");
   if(prices>0)R(Eq(priceTail,snapshot!.Value),"generated price full chain");
   price=snapshot!.Value.Clone();priceFacts=f.Clone();R(Eq(f.GetProperty("before"),price.GetProperty("playerRng").GetProperty("rngs").GetProperty("Shops")),"price actual RNG input");
  }
  if(k=="merchant-price"){
   R(f.EnumerateObject().Select(x=>x.Name).ToHashSet().SetEquals(["profile","entry","model","rarity","cost","before","after"])&&T(f,"profile")==NativeMerchantEconomy.Profile,"price return closed facts/profile");
   Owner(p);R(price.ValueKind==JsonValueKind.Object&&Eq(f.GetProperty("entry"),priceFacts.GetProperty("entry"))&&Eq(f.GetProperty("before"),priceFacts.GetProperty("before")),"price owner/unique/input");
   NativeMerchantEconomyContract.CheckRng(f.GetProperty("before"),f.GetProperty("after"));
   R(f.GetProperty("after").GetProperty("counter").GetInt64()==f.GetProperty("before").GetProperty("counter").GetInt64()+1,"native potion price one RNG sample");
   int baseline=T(f,"rarity") switch{"Common"=>50,"Uncommon"=>75,"Rare"=>100,_=>throw new NativeDataException("Third shop unknown potion rarity")};
   ulong random=unchecked(System.Numerics.BitOperations.RotateLeft(f.GetProperty("before").GetProperty("s1").GetUInt64()*5,7)*9);double uniform=(random>>11)*(1.0/9007199254740992.0);float jitter=(float)(uniform*(double)(1.05f-.95f)+(double).95f);int expectedCost=(int)MathF.Round(baseline*jitter);
   if(A(snapshot!.Value.GetProperty("relics")).Any(r=>T(r,"model")=="RELIC.MEMBERSHIP_CARD"))expectedCost=(int)(expectedCost*0.5m);
   R(f.GetProperty("cost").GetInt32()==expectedCost,"independent pinned potion price/jitter/discount");
   var expected=JsonNode.Parse(price.GetRawText())!;expected["playerRng"]!["rngs"]!["Shops"]=JsonNode.Parse(f.GetProperty("after").GetRawText());
   R(Eq(JsonSerializer.SerializeToElement(expected),snapshot!.Value),"price full state/RNG return");
   R(priced.TryAdd(Ref(f.GetProperty("entry")),f.Clone()),"unique generated price entry");prices++;priceTail=snapshot!.Value.Clone();price=default;
  }
  if(NativeShopLedgerContract.Kinds.Contains(k)){Owner(p);R(ledgerEnabled&&kind=="ThirdShop"&&buying&&!completed,"ledger current buying action");ledger.Accept(k,f,snapshot!.Value);}
  if(Kinds.Contains(k)){
   Owner(p);var s=snapshot!.Value;
   string[] fields=k switch{"third-shop-buy-start"=>ledgerEnabled?["index","entry","offered","cost","model","creature"]:["index","entry","offered","cost","model"],"third-shop-buy-return"=>ledgerEnabled?["index","entry","acquired","cost","spent","netSpent","callbacks","success"]:["index","entry","acquired","cost","spent","callbacks","success"],_=>["room","inventory"]};
   R(f.EnumerateObject().Select(x=>x.Name).ToHashSet().SetEquals(fields),"closed operation facts");
   if(k.StartsWith("third-shop-exit-")){
    R(kind=="Travel"&&attached&&completed&&!closed&&Eq(f.GetProperty("room"),room)&&Eq(f.GetProperty("inventory"),inventory),"actual exit room/inventory");
    if(k=="third-shop-exit-start"){R(!exitPending,"unique exit start");R(Eq(s,ExitInput()),"exit popped-room complete dispatch frame");exitPending=true;exitState=s.Clone();exitFacts=f.Clone();}
    else{R(exitPending&&Eq(f,exitFacts),"exit return required start");var expected=JsonNode.Parse(exitState.GetRawText())!;expected["merchant"]!["closed"]=true;R(Eq(JsonSerializer.SerializeToElement(expected),s),"native exit complete state");exitPending=false;closed=true;}
   }else if(k=="third-shop-attached"){
    R(kind=="ThirdShop"&&!attached&&Eq(f.GetProperty("room"),s.GetProperty("rooms").GetProperty("reference")),"attachment current room/unique");
    SameExcept(dispatch,s,"merchant");var m=s.GetProperty("merchant");R(!m.GetProperty("closed").GetBoolean()&&Eq(f.GetProperty("inventory"),m.GetProperty("reference"))&&Eq(f.GetProperty("room"),m.GetProperty("room")),"attachment actual inventory");
    var entries=A(m.GetProperty("entries"));R(entries.Length==14&&entries.Select(e=>Ref(e.GetProperty("reference"))).Distinct().Count()==14,"inventory cardinality and identity");
    // Old collection profiles do not record _cost. Certify that attachment
    // prices are raw before using them as the immutable exit price source.
    R(!A(s.GetProperty("relics")).Any(r=>ModelType(T(r,"id"),"MerchantRelicEntry",T(r,"model")).GetMethod("ModifyMerchantPrice")!.DeclaringType!=typeof(MegaCrit.Sts2.Core.Models.RelicModel).GetMethod("ModifyMerchantPrice")!.DeclaringType),"raw exit price provenance unavailable: pre-owned price modifier requires a separately collected base cost");
    baseCosts.Clear();foreach(var e in entries){CheckEntry(e);baseCosts.Add(Ref(e.GetProperty("reference")),e.GetProperty("cost").GetInt32());}
    foreach(var e in entries.Where(e=>T(e,"kind")=="MerchantPotionEntry")){var pf=priced[Ref(e.GetProperty("reference"))];R(T(pf,"model")==T(e,"model")&&pf.GetProperty("cost").GetInt32()==e.GetProperty("cost").GetInt32(),"actual generated model/price inventory binding");}
    R(identities[Ref(f.GetProperty("inventory"))].type=="MerchantInventory"&&identities[Ref(f.GetProperty("room"))].type=="MerchantRoom","actual inventory/room registered types");
    room=f.GetProperty("room").Clone();inventory=f.GetProperty("inventory").Clone();tail=s.Clone();attached=true;closed=false;completed=false;bought=0;
   }else if(k=="third-shop-buy-start"){
    R(kind=="ThirdShop"&&attached&&!completed&&!buying&&bought<6&&f.GetProperty("index").GetInt32()==bought&&Eq(s,tail),"purchase current parent/sequence/full input");
    var e=Entry(s,Ref(f.GetProperty("entry")));R(e.GetProperty("stock").GetBoolean()&&Eq(e.GetProperty("content"),f.GetProperty("offered"))&&T(e,"model")==T(f,"model")&&e.GetProperty("cost").GetInt32()==f.GetProperty("cost").GetInt32()&&e.GetProperty("cost").GetInt32()<=s.GetProperty("gold").GetInt32(),"offered actual stocked object and affordable price");
    CheckEntry(e);if(ledgerEnabled){R(actualPlayer.ValueKind==JsonValueKind.Object&&Eq(f.GetProperty("creature"),actualPlayer),"ledger actual player Creature");ledger.Begin(f,s);}buyStart=s.Clone();buyFacts=f.Clone();buying=true;
   }else if(k=="third-shop-buy-return"){
    R(kind=="ThirdShop"&&buying&&!completed&&f.GetProperty("index").GetInt32()==bought&&Eq(f.GetProperty("entry"),buyFacts.GetProperty("entry"))&&Eq(f.GetProperty("acquired"),buyFacts.GetProperty("offered"))&&f.GetProperty("success").GetBoolean()&&f.GetProperty("callbacks").GetInt32()==1,"actual purchase result/callback/instance");
    int cost=buyFacts.GetProperty("cost").GetInt32();if(ledgerEnabled){R(f.GetProperty("cost").GetInt32()==cost,"declared price");ledger.Complete(f,s,cost,buyStart.GetProperty("gold").GetInt32());}else R(f.GetProperty("cost").GetInt32()==cost&&f.GetProperty("spent").GetInt32()==cost&&buyStart.GetProperty("gold").GetInt32()-s.GetProperty("gold").GetInt32()==cost,"actual payment");
    var before=Entry(buyStart,Ref(buyFacts.GetProperty("entry")));var after=Entry(s,Ref(buyFacts.GetProperty("entry")));R(!after.GetProperty("stock").GetBoolean()&&after.GetProperty("content").ValueKind==JsonValueKind.Null&&after.GetProperty("model").ValueKind==JsonValueKind.Null,"native stock clear");
    bool discount=T(before,"model")=="RELIC.MEMBERSHIP_CARD"&&!A(buyStart.GetProperty("relics")).Any(r=>T(r,"model")=="RELIC.MEMBERSHIP_CARD");
    SameExcept(before,after,discount?["stock","content","model","upgrade","cost"]:["stock","content","model","upgrade"]);
    string field=T(before,"kind")=="MerchantCardEntry"?"deck":T(before,"kind")=="MerchantRelicEntry"?"relics":T(before,"kind")=="MerchantPotionEntry"?"potions":throw new NativeDataException("Third shop unsupported entry kind");
    var old=A(buyStart.GetProperty(field));var now=A(s.GetProperty(field));string id=Ref(f.GetProperty("acquired"));R(now.Length==old.Length+1&&!old.Any(x=>T(x,"id")==id)&&(field=="potions"?old.All(x=>now.Any(y=>Eq(x,y))):old.Zip(now).All(x=>Eq(x.First,x.Second))),"exact original inventory and new instance");
    var added=now.Single(x=>T(x,"id")==id);R(T(added,"model")==T(before,"model")&&(field!="deck"||Eq(added.GetProperty("upgrade"),before.GetProperty("upgrade"))),"new model/upgrade identity");
    _=ModelType(id,T(before,"kind"),T(added,"model"));
    if(field=="potions"){
     var expected=JsonNode.Parse(buyStart.GetProperty("inventory").GetRawText())!;var slot=expected["slots"]!.AsArray().First(x=>x!["potion"]==null)!;slot["potion"]=JsonNode.Parse(f.GetProperty("acquired").GetRawText());slot["model"]=T(added,"model");slot["queued"]=false;
     R(Eq(JsonSerializer.SerializeToElement(expected),s.GetProperty("inventory")),"actual native potion slot/capacity continuity");
    }
    SameExcept(buyStart,s,ledgerEnabled?(field=="potions"?["gold","hp","maxHp",field,"inventory","merchant"]:["gold","hp","maxHp",field,"merchant"]):(field=="potions"?["gold",field,"inventory","merchant"]:["gold",field,"merchant"]));SameExcept(buyStart.GetProperty("merchant"),s.GetProperty("merchant"),"entries");var aa=A(buyStart.GetProperty("merchant").GetProperty("entries"));var bb=A(s.GetProperty("merchant").GetProperty("entries"));R(aa.Length==bb.Length,"inventory remains same entries");foreach(var pair in aa.Zip(bb)){
     if(discount)R(pair.Second.GetProperty("cost").GetInt32()==(int)(pair.First.GetProperty("cost").GetInt32()*0.5m),"native MembershipCard derived price discount");
     if(!Eq(pair.First.GetProperty("reference"),buyFacts.GetProperty("entry"))){if(discount)SameExcept(pair.First,pair.Second,"cost");else R(Eq(pair.First,pair.Second),"unrelated stock unchanged");}
    }
    tail=s.Clone();buying=false;bought++;
   }else{
    R(k=="third-shop-complete"&&kind=="ThirdShop"&&attached&&!completed&&!buying&&Eq(s,tail)&&Eq(f.GetProperty("room"),room)&&Eq(f.GetProperty("inventory"),inventory),"required complete current session/full return");completed=true;
   }
  }
  if(k=="confirmed"){
   Owner(p);if(kind=="ThirdShop")R(attached&&completed&&!buying&&Eq(snapshot!.Value,tail),"shop root complete before confirmation");
   if(kind=="Travel"&&T(dispatch,"room")=="MerchantRoom")R(attached&&completed&&closed&&!exitPending,"required native exit before travel confirmation");
   if(kind=="Travel"&&prices>0)R(prices==3&&Eq(snapshot!.Value,priceTail),"generated price chain bound to Travel return");
   confirmed=snapshot!.Value.Clone();lastRoot=root;lastContext=context;root="";
   if(kind=="Travel"&&closed){attached=completed=false;}
  }
 }
 internal void Finish(){if(enabled)R(root==""&&!buying&&!exitPending&&price.ValueKind==JsonValueKind.Undefined&&(!attached||completed),"unfinished session suffix");}
}
