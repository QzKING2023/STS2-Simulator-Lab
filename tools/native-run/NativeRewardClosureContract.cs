using System.Text.Json;
using System.Text.Json.Nodes;
namespace OfflineSearchHarness;
// Additional native business evidence. The enclosing 4C reader still validates its full operation tree.
internal sealed class NativeRewardClosureContract
{
 internal static readonly HashSet<string> SnapshotKinds=["rc-menu","rc-reroll-start","rc-reroll-return","rc-add-start","rc-add-return","rc-procure-start","rc-procure-return","rc-quest-start","rc-quest-return"];
 static void R([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool b,string m){if(!b)throw new NativeDataException("Closure "+m);}
 static string T(JsonElement e,string k)=>e.GetProperty(k).GetString()!;
 static string Ref(JsonElement e)=>e.ValueKind==JsonValueKind.Null?"":T(e,"$ref");
 static bool Eq(JsonElement a,JsonElement b)=>SimulatorLab.Core.NativeDecisionTicket.SameActionJson(a.GetRawText(),b.GetRawText());
 static void Except(JsonElement a,JsonElement b,params string[] fields){R(a.EnumerateObject().Select(p=>p.Name).ToHashSet().SetEquals(b.EnumerateObject().Select(p=>p.Name)),"complete state inventory");foreach(var p in a.EnumerateObject())if(!fields.Contains(p.Name))R(Eq(p.Value,b.GetProperty(p.Name)),"full state "+p.Name);}
 sealed class Menu(JsonElement open,JsonElement options){internal JsonElement Open=open.Clone(),Options=options.Clone(),Source,Selected,Last,Add,Returned;internal string Reward="",Root="",Context="",Mode="";internal int Generation;internal bool Declared,AddStarted,AddReturned,RerollStarted,RerollReturned,Closed;}
 static void CheckDraws(JsonElement before,JsonElement after){long n=after.GetProperty("counter").GetInt64()-before.GetProperty("counter").GetInt64();R(n>0&&n<=4096,"reward RNG draw budget");ulong a=before.GetProperty("s0").GetUInt64(),b=before.GetProperty("s1").GetUInt64(),c=before.GetProperty("s2").GetUInt64(),d=before.GetProperty("s3").GetUInt64();for(long i=0;i<n;i++){ulong t=b<<17;c^=a;d^=b;b^=c;a^=d;c^=t;d=System.Numerics.BitOperations.RotateLeft(d,45);}R(Eq(JsonSerializer.SerializeToElement(new{counter=after.GetProperty("counter").GetInt64(),s0=a,s1=b,s2=c,s3=d}),after),"independent reward RNG state progression");}
 static JsonNode History(JsonElement state)=>JsonNode.Parse(state.GetProperty("mapHistory").GetRawText())!;
 static JsonObject HistoryPlayer(JsonNode h)=>h.AsArray().Last()!.AsArray().Last()!["player"]!.AsObject();
 static void HistoryEqual(JsonNode expected,JsonElement state)=>R(Eq(JsonSerializer.SerializeToElement(expected),state.GetProperty("mapHistory")),"native history complete delta");
 static void Choice(JsonObject player,JsonElement card,bool picked)=>player["card_choices"]!.AsArray().Add(new JsonObject{["card"]=JsonNode.Parse(card.GetRawText()),["was_picked"]=picked});
 readonly Dictionary<string,(string Type,string? Model)> registeredModels=[];
 void ModelBinding(string reference,string model,string category,Type baseType)
 {
  R(registeredModels.TryGetValue(reference,out var registered)&&registered.Model==model,"registered instance/model binding");
  string prefix="MegaCrit.Sts2.Core.Models."+category+".";
  var actualType=baseType.Assembly.GetType(prefix+registered.Type);
  R(model.StartsWith(category[..^1].ToUpperInvariant()+".",StringComparison.Ordinal)&&actualType!=null&&baseType.IsAssignableFrom(actualType)&&model.Split('.').Last().Replace("_","").Equals(registered.Type,StringComparison.OrdinalIgnoreCase),"registered native type/model category binding");
 }
 void CardContent(JsonElement card,string reference)
 {
  ModelBinding(reference,T(card,"model"),"Cards",typeof(MegaCrit.Sts2.Core.Models.CardModel));
  var native=card.GetProperty("native");var id=native.GetProperty("id");
  R(T(card,"model")==T(id,"Category")+"."+T(id,"Entry"),"native card model binding");
  int upgrade=native.TryGetProperty("current_upgrade_level",out var u)?u.GetInt32():0;
  R(upgrade>=0&&card.GetProperty("upgrade").GetInt32()==upgrade,"native card upgrade projection binding");
 }
 void CardFact(JsonElement card)=>CardContent(card,Ref(card.GetProperty("reference")));
 readonly Dictionary<string,Menu> menus=[];readonly Dictionary<string,int> generations=[];
 readonly Dictionary<string,JsonElement> potionStarts=[];
 readonly Dictionary<string,JsonElement> operationOpens=[],operationSelections=[],potionLast=[],rewardContents=[],nextMenus=[];
 readonly Stack<string> operationStack=[];
 readonly Dictionary<string,Menu> previous=[];readonly HashSet<string> potionReturned=[];
 string scenario="";int adds,rerolls,procures,quests;bool done;
 JsonElement questStart,questFacts,questEnd;string questOp="";
 uint? lastNativeChoice;string lastChoiceRoot="",lastChoiceContext="";readonly HashSet<uint> usedNativeChoices=[];
 internal void Accept(JsonElement p,JsonElement? snapshot)
 {
  string ev=T(p,"kind"),root=T(p,"root"),context=T(p,"context");var d=p.GetProperty("detail");var f=d.TryGetProperty("facts",out var ff)?ff:default;
  if(ev=="header")scenario=T(d,"scenario");
  if(ev=="identity")registeredModels.Add(T(d,"id"),(T(d,"type"),d.GetProperty("model").GetString()));
  if(snapshot is {} projected){
   foreach(var item in projected.GetProperty("relics").EnumerateArray())ModelBinding(T(item,"id"),T(item,"model"),"Relics",typeof(MegaCrit.Sts2.Core.Models.RelicModel));
   foreach(var item in projected.GetProperty("potions").EnumerateArray())ModelBinding(T(item,"id"),T(item,"model"),"Potions",typeof(MegaCrit.Sts2.Core.Models.PotionModel));
  }
  if(snapshot is {} deckProjected){var ids=new HashSet<string>();foreach(var card in deckProjected.GetProperty("deck").EnumerateArray()){R(ids.Add(T(card,"id")),"unique permanent instance");CardContent(card,T(card,"id"));}}
  if(ev=="native-choice-reserved"){lastNativeChoice=d.GetProperty("nativeChoiceId").GetUInt32();lastChoiceRoot=root;lastChoiceContext=context;}
  if(ev=="n3c-open"){operationOpens.Add(T(f,"id"),JsonSerializer.SerializeToElement(new{facts=f,state=snapshot!.Value,root,context}));operationStack.Push(T(f,"id"));}
  if(ev=="n3c-answer")operationSelections.Add(T(f,"id"),f.GetProperty("selected").Clone());
  if(ev=="n3c-set")foreach(var item in f.GetProperty("options").EnumerateArray())if(T(item,"kind")=="PotionReward")rewardContents.Add(Ref(item.GetProperty("reference")),item.Clone());
  if(ev=="n3c-open"&&T(f,"kind")=="card-add"){R(scenario=="closure-lens-add"&&!snapshot!.Value.GetProperty("deck").EnumerateArray().Any(c=>T(c,"id")==Ref(f.GetProperty("subject"))),"declared fresh direct API scope");menus.Add(T(f,"id"),new Menu(snapshot!.Value,f.GetProperty("options")){Reward="",Root=root,Context=context,Declared=true,Generation=1,Selected=f.GetProperty("subject").Clone(),Last=snapshot!.Value.Clone(),Mode="direct-api"});}
  if(ev=="n3c-open"&&T(f,"kind")=="card-menu"){var m=new Menu(snapshot!.Value,f.GetProperty("options")){Reward=Ref(f.GetProperty("subject")),Root=root,Context=context};R(menus.TryAdd(T(f,"id"),m),"menu unique");if(previous.TryGetValue(m.Reward,out var prior)){R(prior.Closed&&prior.Mode=="reroll"&&Eq(prior.Last,snapshot!.Value),"next menu complete static business binding");}}
  if(ev=="n3c-answer"&&menus.TryGetValue(T(f,"id"),out var answered))answered.Selected=f.GetProperty("selected").Clone();
  if(ev.StartsWith("rc-")&&!ev.StartsWith("rc-procure")&&!ev.StartsWith("rc-quest")){
   R(menus.TryGetValue(T(f,"id"),out var found),"business without menu");var m=found!;R(!m.Closed&&m.Root==root&&m.Context==context&&m.Reward==Ref(f.GetProperty("reward")),"business owner");var s=snapshot!.Value;
   if(ev=="rc-menu"){
    R(!m.Declared&&Eq(m.Open,s),"menu declaration/static");R(lastNativeChoice==f.GetProperty("nativeChoiceId").GetUInt32()&&usedNativeChoices.Add(lastNativeChoice.Value)&&lastChoiceRoot==root&&lastChoiceContext==context,"native choice reserved/menu binding unique");int gen=generations.GetValueOrDefault(m.Reward)+1;R(f.GetProperty("generation").GetInt32()==gen,"menu generation");generations[m.Reward]=gen;m.Generation=gen;R(f.GetProperty("CanReroll").GetBoolean()==(scenario.StartsWith("closure-reroll")&&gen==1),"independent actual reroll availability");
    var alternatives=f.GetProperty("alternatives").EnumerateArray().ToArray();var alternateOptions=m.Options.EnumerateArray().Where(o=>T(o,"Kind")=="Alternative").ToArray();R(alternatives.Length==alternateOptions.Length,"complete alternative source");for(int i=0;i<alternatives.Length;i++){var a=alternatives[i];R(Eq(a.GetProperty("reference"),alternateOptions[i].GetProperty("Reference"))&&T(a,"option")==T(alternateOptions[i],"Model")&&!(T(a,"option")=="REROLL"&&!f.GetProperty("CanReroll").GetBoolean())&&T(a,"afterSelected")== (T(a,"option")=="Skip"?"EndSelectionAndDoNotCompleteReward":T(a,"option")=="REROLL"?"DoNothing":"unsupported"),"native alternative source/action");}
    m.Declared=true;m.Last=s.Clone();m.Source=f.Clone();if(nextMenus.TryGetValue(m.Reward,out var generated)){R(Eq(generated,JsonSerializer.SerializeToElement(f.GetProperty("cards").EnumerateArray().Select(c=>c.GetProperty("card")).ToArray())),"reroll actual generated next menu binding");nextMenus.Remove(m.Reward);}previous[m.Reward]=m;
    var cards=f.GetProperty("cards").EnumerateArray().ToArray();foreach(var card in cards)CardFact(card.GetProperty("card"));var opts=m.Options.EnumerateArray().Where(x=>T(x,"Kind")=="Card").ToArray();R(cards.Length==opts.Length,"source cardinality");for(int i=0;i<cards.Length;i++)R(Eq(cards[i].GetProperty("card").GetProperty("reference"),opts[i].GetProperty("Reference"))&&T(cards[i].GetProperty("card"),"model")==T(opts[i],"Model")&&cards[i].GetProperty("card").GetProperty("upgrade").GetInt32()==opts[i].GetProperty("Upgrade").GetInt32(),"source identity/model");
   }else{
    R(m.Declared&&f.GetProperty("generation").GetInt32()==m.Generation,"business generation");
    if(ev=="rc-add-start"){R(!m.AddStarted&&!m.RerollStarted&&Eq(m.Last,s)&&Eq(f.GetProperty("selected").GetProperty("reference"),m.Selected),"add selected/static/order");if(m.Mode!="direct-api"){var source=m.Source.GetProperty("cards").EnumerateArray().Single(c=>Eq(c.GetProperty("card").GetProperty("reference"),m.Selected)).GetProperty("card");R(Eq(source,f.GetProperty("selected")),"selected full source state");}
     CardFact(f.GetProperty("selected"));R(!s.GetProperty("deck").EnumerateArray().Any(c=>T(c,"id")==Ref(m.Selected)),"fresh selected instance outside permanent deck");m.AddStarted=true;m.Add=f.GetProperty("selected").Clone();m.Last=s.Clone();}
    if(ev=="rc-add-return"){
     R(m.AddStarted&&!m.AddReturned&&Eq(m.Add,f.GetProperty("selected")),"add return unique/source");bool ok=f.GetProperty("success").GetBoolean();var old=m.Last.GetProperty("deck").EnumerateArray().ToArray();var deck=s.GetProperty("deck").EnumerateArray().ToArray();
     if(ok){var actual=f.GetProperty("actual");CardFact(actual);R(!old.Any(c=>T(c,"id")==Ref(actual.GetProperty("reference"))),"actual newly acquired instance");R(deck.Length==old.Length+1&&old.Zip(deck).All(z=>Eq(z.First,z.Second))&&T(deck[^1],"id")==Ref(actual.GetProperty("reference"))&&T(deck[^1],"model")==T(actual,"model")&&deck[^1].GetProperty("upgrade").GetInt32()==actual.GetProperty("upgrade").GetInt32()&&T(actual,"model")==T(m.Add,"model")&&actual.GetProperty("upgrade").GetInt32()==m.Add.GetProperty("upgrade").GetInt32()&&Eq(deck[^1].GetProperty("native"),actual.GetProperty("native")),"actual permanent instance/full content");}
     else R(Eq(m.Last.GetProperty("deck"),s.GetProperty("deck"))&&f.GetProperty("actual").ValueKind==JsonValueKind.Null,"declined no acquisition");
     var history=History(m.Last);if(ok)HistoryPlayer(history)["cards_gained"]!.AsArray().Add(JsonNode.Parse(m.Add.GetProperty("native").GetRawText()));HistoryEqual(history,s);
     if(ok){var a=f.GetProperty("actual");var expected=JsonNode.Parse(m.Add.GetProperty("native").GetRawText())!;expected["floor_added_to_deck"]=s.GetProperty("floor").GetInt32();
      var mods=f.GetProperty("modifiers").EnumerateArray().ToArray();
      if(scenario=="closure-lens-add"){R(m.Mode=="direct-api"&&!Eq(a.GetProperty("reference"),m.Selected)&&mods.Length==1&&s.GetProperty("relics").EnumerateArray().Any(r=>T(r,"id")==Ref(mods[0])&&T(r,"model")=="RELIC.FRESNEL_LENS"),"real FresnelLens replacement/source");expected["enchantment"]=new JsonObject{["id"]=new JsonObject{["Category"]="ENCHANTMENT",["Entry"]="NIMBLE"},["amount"]=2};}
      else R(Eq(a.GetProperty("reference"),m.Selected)&&mods.Length==0,"default same-instance acquisition");
      R(Eq(JsonSerializer.SerializeToElement(expected),a.GetProperty("native")),"independent actual card content/upgrade/enchantment");
     }
     Except(m.Last,s,"deck","mapHistory");m.AddReturned=true;m.Last=s.Clone();m.Returned=f.Clone();adds++;
    }
    if(ev=="rc-reroll-start"){R(!m.RerollStarted&&!m.AddStarted&&Eq(m.Last,s),"reroll start/static");R(m.Options.EnumerateArray().Any(x=>T(x,"Kind")=="Alternative"&&T(x,"Model")=="REROLL"&&Eq(x.GetProperty("Reference"),m.Selected)),"reroll selected native alternative");R(f.GetProperty("CanReroll").GetBoolean()&&m.Source.GetProperty("CanReroll").GetBoolean()&&Eq(f.GetProperty("cards"),JsonSerializer.SerializeToElement(m.Source.GetProperty("cards").EnumerateArray().Select(c=>c.GetProperty("card")).ToArray())),"reroll before source complete binding");m.RerollStarted=true;m.Last=s.Clone();}
    if(ev=="rc-reroll-return"){R(m.RerollStarted&&!m.RerollReturned&&!f.GetProperty("CanReroll").GetBoolean(),"reroll return/order");var history=History(m.Last);foreach(var c in m.Source.GetProperty("cards").EnumerateArray())Choice(HistoryPlayer(history),c.GetProperty("card").GetProperty("native"),false);HistoryEqual(history,s);
     Except(m.Last,s,"playerRng","mapHistory");var oldRng=m.Last.GetProperty("playerRng").GetProperty("rngs");var newRng=s.GetProperty("playerRng").GetProperty("rngs");Except(oldRng,newRng,"Rewards");CheckDraws(oldRng.GetProperty("Rewards"),newRng.GetProperty("Rewards"));
     nextMenus[m.Reward]=f.GetProperty("cards").Clone();m.RerollReturned=true;m.Last=s.Clone();rerolls++;}
   }
  }
  if(ev=="n3c-return"&&menus.TryGetValue(T(f,"id"),out var end)){
   R(end.Declared&&!end.Closed,"menu result required unique");var result=f.GetProperty("result");if(end.Mode=="direct-api"){R(end.AddReturned&&Eq(end.Last,snapshot!.Value)&&Eq(result.GetProperty("selected"),end.Selected)&&Eq(result.GetProperty("actual"),end.Returned.GetProperty("actual").GetProperty("reference")),"direct API result/full state");end.Closed=true;R(operationStack.Pop()==T(f,"id"),"direct API operation return order");return;}R(result.EnumerateObject().Select(p=>p.Name).ToHashSet().SetEquals(["success","acquired","sourceCards","sourceAlternatives","acquiredCard","identityMode","generation"]),"complete menu result schema");R(Eq(result.GetProperty("sourceCards"),JsonSerializer.SerializeToElement(end.Source.GetProperty("cards").EnumerateArray().Select(c=>c.GetProperty("card")).ToArray()))&&Eq(result.GetProperty("sourceAlternatives"),end.Source.GetProperty("alternatives")),"menu canonical source/result inventory");string mode=T(result,"identityMode");R(result.GetProperty("generation").GetInt32()==end.Generation,"menu result generation");
   if(mode=="reroll")R(end.RerollReturned&&!end.AddStarted&&!result.GetProperty("success").GetBoolean()&&result.GetProperty("acquired").ValueKind==JsonValueKind.Null&&result.GetProperty("acquiredCard").ValueKind==JsonValueKind.Null&&Eq(end.Last,snapshot!.Value),"reroll completion");
   else if(end.AddStarted){R(end.AddReturned&&Eq(result.GetProperty("acquired"),end.Returned.GetProperty("actual").GetProperty("reference")),"menu actual result binding");R(result.GetProperty("success").GetBoolean()&&Eq(result.GetProperty("acquiredCard"),end.Returned.GetProperty("actual"))&&mode==(Eq(end.Selected,end.Returned.GetProperty("actual").GetProperty("reference"))?"same-instance":"replacement"),"menu actual full content/mode/result");if(scenario=="closure-lens-options"){var selectedSource=end.Source.GetProperty("cards").EnumerateArray().Single(c=>Eq(c.GetProperty("card").GetProperty("reference"),end.Selected));var enchant=selectedSource.GetProperty("card").GetProperty("native").GetProperty("enchantment");R(T(enchant.GetProperty("id"),"Entry")=="NIMBLE"&&enchant.GetProperty("amount").GetInt32()==2&&selectedSource.GetProperty("modifiers").EnumerateArray().Any(mod=>snapshot!.Value.GetProperty("relics").EnumerateArray().Any(r=>T(r,"id")==Ref(mod)&&T(r,"model")=="RELIC.FRESNEL_LENS")),"actual candidate-generation FresnelLens source");}
    var history=History(end.Last);var hp=HistoryPlayer(history);var actual=end.Returned.GetProperty("actual");Choice(hp,actual.GetProperty("native"),true);
    foreach(var c in end.Source.GetProperty("cards").EnumerateArray()){var original=c.GetProperty("card");if(Eq(original.GetProperty("reference"),actual.GetProperty("reference")))continue;var native=JsonNode.Parse(original.GetProperty("native").GetRawText())!;if(Eq(original.GetProperty("reference"),end.Selected))native["floor_added_to_deck"]=snapshot!.Value.GetProperty("floor").GetInt32();Choice(hp,JsonSerializer.SerializeToElement(native),false);}HistoryEqual(history,snapshot!.Value);
    Except(end.Last,snapshot!.Value,"mapHistory");}
   else{R(mode=="none"&&result.GetProperty("acquiredCard").ValueKind==JsonValueKind.Null&&Eq(end.Last,snapshot!.Value)&&result.GetProperty("acquired").ValueKind==JsonValueKind.Null&&!result.GetProperty("success").GetBoolean()&&end.Options.EnumerateArray().Any(o=>T(o,"Kind")=="Alternative"&&T(o,"Model")=="Skip"&&Eq(o.GetProperty("Reference"),end.Selected)),"native Skip alternative without acquisition/static");}
   end.Mode=mode;end.Closed=true;
  }
  if(ev=="rc-procure-start"){string id=T(f,"id"),r=Ref(f.GetProperty("reward"));var op=operationOpens[id];R(operationStack.Peek()==id&&T(op.GetProperty("facts"),"kind")=="reward-item"&&Eq(operationSelections[id],f.GetProperty("reward"))&&Eq(op.GetProperty("state"),snapshot!.Value)&&T(op,"root")==root&&T(op,"context")==context,"procure current operation full state binding");R(Eq(rewardContents[r].GetProperty("content"),f.GetProperty("potion"))&&T(rewardContents[r],"model")==T(f,"model"),"procure actual offered source");R(potionStarts.TryAdd(id,JsonSerializer.SerializeToElement(new{facts=f,state=snapshot!.Value,root,context})),"procure unique start");}
  if(ev=="rc-procure-return"){
   string r=T(f,"id");R(potionStarts.ContainsKey(r),"required procure start");var start=potionStarts[r];R(operationStack.Peek()==r&&T(start,"root")==root&&T(start,"context")==context&&Eq(start.GetProperty("facts").GetProperty("reward"),f.GetProperty("reward"))&&potionReturned.Add(r),"procure required unique return");R(Eq(start.GetProperty("facts").GetProperty("potion"),f.GetProperty("potion")),"procure source");
   var before=start.GetProperty("state");bool ok=f.GetProperty("success").GetBoolean();var s=snapshot!.Value;
   // This finite profile declares only Sozu refusal. Other success/failure hooks need their own actual-inventory contract.
   R(scenario is "closure-sozu-empty" or "closure-sozu-full","declared procure profile");
   R(before.GetProperty("relics").EnumerateArray().Any(x=>T(x,"model")=="RELIC.SOZU"),"applicable native Sozu owner gate");
   R(!ok&&T(f,"failure")=="NotAllowed"&&f.GetProperty("actual").ValueKind==JsonValueKind.Null&&Eq(before,s),"Sozu NotAllowed no actual acquisition/full static state");
   potionLast[r]=JsonSerializer.SerializeToElement(new{facts=f,state=s});procures++;
  }
  if(ev=="n3c-return"){
   string id=T(f,"id");R(operationStack.Pop()==id,"closure operation return order");var op=operationOpens[id].GetProperty("facts");
   if(T(op,"kind")=="reward-item"){var result=f.GetProperty("result");R(result.EnumerateObject().Select(p=>p.Name).ToHashSet().SetEquals(["success","skipped","selected","acquired","claimedPotion"]),"complete collection result schema");if(!rewardContents.ContainsKey(Ref(operationSelections[id])))R(result.GetProperty("claimedPotion").ValueKind==JsonValueKind.Null,"non-potion cannot claim potion");}
   if(T(op,"kind")=="reward-item"&&operationSelections[id].ValueKind==JsonValueKind.Object&&rewardContents.ContainsKey(Ref(operationSelections[id]))){R(potionLast.TryGetValue(id,out var last),"required native procure result");var facts=last.GetProperty("facts");var result=f.GetProperty("result");R(Eq(last.GetProperty("state"),snapshot!.Value)&&Eq(result.GetProperty("acquired"),facts.GetProperty("actual"))&&Eq(result.GetProperty("claimedPotion"),facts.GetProperty("potion"))&&!result.GetProperty("skipped").GetBoolean()&&result.GetProperty("success").GetBoolean()==(facts.GetProperty("success").GetBoolean()||T(facts,"failure")=="NotAllowed"),"procure outer completion/actual/full state");}
  }
  if(ev=="rc-quest-start"){
   R(scenario=="closure-spoils"&&quests==0&&questOp==""&&operationStack.Peek()==T(f,"id"),"quest current normal operation/order");questOp=T(f,"id");var op=operationOpens[questOp];R(T(op.GetProperty("facts"),"kind")=="treasure-normal"&&T(op,"root")==root&&T(op,"context")==context,"quest parent identity");questStart=snapshot!.Value.Clone();questFacts=f.Clone();var original=questStart.GetProperty("deck").EnumerateArray().Single(c=>T(c,"id")==Ref(f.GetProperty("quest").GetProperty("reference")));R(Eq(original.GetProperty("native"),f.GetProperty("quest").GetProperty("native"))&&T(original,"model")==T(f.GetProperty("quest"),"model")&&original.GetProperty("upgrade").GetInt32()==f.GetProperty("quest").GetProperty("upgrade").GetInt32(),"quest actual permanent source content");
   R(T(f,"coord")==T(questStart,"node")&&f.GetProperty("act").GetInt32()==questStart.GetProperty("act").GetInt32()&&T(f.GetProperty("quest"),"model")=="CARD.SPOILS_MAP","quest actual generated target");
   var initial=op.GetProperty("state");Except(initial,questStart,"gold","mapHistory","playerRng");int baseGold=questStart.GetProperty("gold").GetInt32()-initial.GetProperty("gold").GetInt32();var rngBefore=initial.GetProperty("playerRng").GetProperty("rngs").GetProperty("Rewards");ulong out64=unchecked(System.Numerics.BitOperations.RotateLeft(rngBefore.GetProperty("s1").GetUInt64()*5,7)*9);int expectedBase=42+(int)(((ulong)(uint)(out64>>32)*11)>>32);R(baseGold==expectedBase,"independent native chest base gold range");var a=initial.GetProperty("playerRng").GetProperty("rngs");var b=questStart.GetProperty("playerRng").GetProperty("rngs");Except(a,b,"Rewards");NativeMerchantEconomyContract.CheckRng(a.GetProperty("Rewards"),b.GetProperty("Rewards"));var history=History(initial);var hp=HistoryPlayer(history);hp["gold_gained"]=hp["gold_gained"]!.GetValue<int>()+baseGold;HistoryEqual(history,questStart);
  }
  if(ev=="rc-quest-return"){
   R(T(operationOpens[questOp],"root")==root&&T(operationOpens[questOp],"context")==context&&questOp==T(f,"id")&&operationStack.Peek()==questOp&&quests==0&&Eq(f.GetProperty("quest"),questFacts.GetProperty("quest"))&&f.GetProperty("gold").GetInt32()==600,"quest required result/source/600");var state=snapshot!.Value;string id=Ref(f.GetProperty("quest").GetProperty("reference"));var old=questStart.GetProperty("deck").EnumerateArray().ToArray();R(old.Any(c=>T(c,"id")==id)&&Eq(JsonSerializer.SerializeToElement(old.Where(c=>T(c,"id")!=id)),state.GetProperty("deck"))&&state.GetProperty("gold").GetInt32()==questStart.GetProperty("gold").GetInt32()+600,"quest actual removal/gold");Except(questStart,state,"gold","deck","mapHistory");var history=History(questStart);var hp=HistoryPlayer(history);hp["gold_gained"]=hp["gold_gained"]!.GetValue<int>()+600;hp["cards_removed"]!.AsArray().Add(JsonNode.Parse(f.GetProperty("quest").GetProperty("native").GetRawText()));hp["completed_quests"]!.AsArray().Add(JsonNode.Parse(f.GetProperty("quest").GetProperty("native").GetProperty("id").GetRawText()));HistoryEqual(history,state);questEnd=state.Clone();quests++;
  }
  if(ev=="n3c-return"&&T(operationOpens[T(f,"id")].GetProperty("facts"),"kind")=="treasure-normal"&&scenario=="closure-spoils"){
   var result=f.GetProperty("result");R(Eq(questEnd,snapshot!.Value)&&result.GetProperty("baseGold").GetInt32()==questStart.GetProperty("gold").GetInt32()-operationOpens[questOp].GetProperty("state").GetProperty("gold").GetInt32()&&quests==1&&result.GetProperty("quests").GetInt32()==1&&result.GetProperty("questGold").GetInt32()==600&&result.GetProperty("gold").GetInt32()==result.GetProperty("baseGold").GetInt32()+600,"treasure source ledger exact quest contribution");
  }
  if(ev=="n3c-contract"){
   R(!done&&operationStack.Count==0&&nextMenus.Count==0&&menus.Values.All(m=>m.Closed)&&potionStarts.Count==potionReturned.Count,"complete business operations");
   if(scenario.StartsWith("closure-reroll"))R(rerolls==1&&adds==(scenario.EndsWith("skip")?0:1),"independent reroll/acquisition count");
   if(scenario.StartsWith("closure-sozu"))R(procures==1&&snapshot!.Value.GetProperty("relics").EnumerateArray().Any(r=>T(r,"model")=="RELIC.SOZU"),"independent Sozu native gate");
   if(scenario is "closure-direct" or "closure-lens-options" or "closure-lens-add")R(adds==1&&rerolls==0,"independent direct acquisition count");if(scenario=="closure-spoils")R(quests==1,"required real quest completion");if(scenario=="closure-neow")R(adds==2&&rerolls==0,"Neow actual two acquisitions");done=true;
  }
 }
 internal void Finish()=>R(done&&menus.Values.All(m=>m.Closed),"unfinished closure contract");
}
