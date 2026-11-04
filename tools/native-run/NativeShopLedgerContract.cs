using System.Text.Json;
using System.Text.Json.Nodes;
namespace OfflineSearchHarness;

// Trust original audited setters for actual writes, not arbitrary item-specific
// effect formulas. Reconcile all writes with original command/task boundaries.
internal sealed class NativeShopLedgerContract
{
 internal static readonly HashSet<string> Kinds=["third-shop-resource-start","third-shop-resource-return","third-shop-gold-start","third-shop-gold-return","third-shop-hook-start","third-shop-hook-return"];
 readonly Dictionary<string,int> resources=[];
 readonly Stack<(JsonElement facts,JsonElement state,int gold)> commands=[];
 JsonElement entry,player,creature,writeFacts,writeState,hookState,hookFacts;
 int paid,spends,hook,serial;bool opened;
 static bool Eq(JsonElement a,JsonElement b)=>NativeBoundaryContract.Equal(a,b);
 static string T(JsonElement e,string k)=>e.GetProperty(k).GetString()!;
 static void R(bool ok,string m){if(!ok)throw new NativeDataException("Shop ledger "+m);}
 internal void Begin(JsonElement f,JsonElement s){R(!opened,"overlapping transaction");opened=true;entry=f.GetProperty("entry").Clone();creature=f.GetProperty("creature").Clone();player=creature;foreach(var k in new[]{"gold","hp","maxHp"})resources[k]=s.GetProperty(k).GetInt32();paid=spends=hook=serial=0;writeFacts=hookState=default;}
 void Current(JsonElement s){foreach(var (k,v) in resources)R(s.GetProperty(k).GetInt32()==v,"unobserved resource change "+k);}
 internal void Accept(string kind,JsonElement f,JsonElement s){
  string[] fields=kind.Contains("resource")?["entry","field","from","to","subject","command"]:kind.Contains("gold")?["entry","id","kind","amount","reason","subject"]:["entry","goldSpent"];R(f.EnumerateObject().Select(p=>p.Name).ToHashSet().SetEquals(fields),"closed ledger facts");
  R(opened&&Eq(f.GetProperty("entry"),entry),"current transaction entry");
  if(kind=="third-shop-resource-start"){
   Current(s);R(writeFacts.ValueKind==JsonValueKind.Undefined,"overlapping setter");string field=T(f,"field");R(resources.ContainsKey(field)&&Eq(f.GetProperty("subject"),field=="gold"?player:creature),"actual owner/resource");R(f.GetProperty("from").GetInt32()==resources[field],"setter input");
   if(commands.TryPeek(out var cmd))R(T(f,"command")==T(cmd.facts,"id"),"setter command owner");else R(f.GetProperty("command").ValueKind==JsonValueKind.Null,"unowned command ref");
   if(field=="gold")R(commands.Count>0,"gold write requires original command");writeFacts=f.Clone();writeState=s.Clone();
  }else if(kind=="third-shop-resource-return"){
   R(writeFacts.ValueKind==JsonValueKind.Object&&Eq(f,writeFacts),"setter required matching return");string field=T(f,"field");var expected=JsonNode.Parse(writeState.GetRawText())!;expected[field]=f.GetProperty("to").GetInt32();R(Eq(JsonSerializer.SerializeToElement(expected),s),"setter complete state frame");resources[field]=f.GetProperty("to").GetInt32();writeFacts=default;
  }else if(kind=="third-shop-gold-start"){
   Current(s);R(writeFacts.ValueKind==JsonValueKind.Undefined&&Eq(f.GetProperty("subject"),player)&&T(f,"kind") is "GainGold" or "LoseGold","original gold command subject/type");R(T(f,"id")=="g"+(++serial),"unique sequential command");commands.Push((f.Clone(),s.Clone(),resources["gold"]));
  }else if(kind=="third-shop-gold-return"){
   Current(s);R(writeFacts.ValueKind==JsonValueKind.Undefined&&commands.TryPeek(out _),"gold return requires command");var c=commands.Pop();R(Eq(c.facts,f),"gold task owner/arguments");var expected=JsonNode.Parse(c.state.GetRawText())!;foreach(var (k,v) in resources)expected[k]=v;R(Eq(JsonSerializer.SerializeToElement(expected),s),"gold task complete state frame");
   if(T(f,"kind")=="LoseGold"){
    R(T(f,"reason")=="Spent"&&f.GetProperty("amount").GetDecimal()>=0&&f.GetProperty("amount").GetDecimal()==decimal.Truncate(f.GetProperty("amount").GetDecimal()),"registered integral purchase debit");int amount=checked((int)f.GetProperty("amount").GetDecimal());R(resources["gold"]==c.gold-amount,"original actual debit");paid+=amount;spends++;
   }
  }else if(kind=="third-shop-hook-start"){
   Current(s);R(commands.Count==0&&writeFacts.ValueKind==JsonValueKind.Undefined&&hook==0&&spends==1&&f.GetProperty("goldSpent").GetInt32()==paid,"original AfterItemPurchased payment/start");hook=1;hookFacts=f.Clone();hookState=s.Clone();
  }else if(kind=="third-shop-hook-return"){
   Current(s);R(hook==1&&commands.Count==0&&writeFacts.ValueKind==JsonValueKind.Undefined&&Eq(f,hookFacts),"original purchase hook completion");var expected=JsonNode.Parse(hookState.GetRawText())!;foreach(var(k,v)in resources)expected[k]=v;R(Eq(JsonSerializer.SerializeToElement(expected),s),"hook observed resource/full state frame");hook=2;hookState=s.Clone();
  }else throw new NativeDataException("Unknown shop ledger event");
 }
 internal void Complete(JsonElement f,JsonElement s,int cost,int beforeGold){
  Current(s);R(opened&&commands.Count==0&&writeFacts.ValueKind==JsonValueKind.Undefined&&hook==2&&spends==1&&paid==cost&&f.GetProperty("spent").GetInt32()==paid&&f.GetProperty("netSpent").GetInt32()==beforeGold-resources["gold"]&&Eq(s,hookState),"payment/net balance and completed hook bound to return");opened=false;
 }
}
