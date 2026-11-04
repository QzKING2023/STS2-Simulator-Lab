using System.Text.Json;
using System.Text.Json.Nodes;
namespace OfflineSearchHarness;

// Data schema and one fixed-version independent assertion, not an effect mirror.
internal static class NativeCardRecordContract
{
 internal static void Card(JsonElement card){
  NativeBoundaryContract.Require(card.TryGetProperty("native",out var native)&&native.ValueKind==JsonValueKind.Object,"Missing required native card serialization");
  var id=native.GetProperty("id");NativeBoundaryContract.Require(id.GetProperty("Category").GetString()+"."+id.GetProperty("Entry").GetString()==card.GetProperty("model").GetString(),"Native card serialized model linkage");
  // SerializableCard intentionally omits the default upgrade value (zero).
  int upgrade=native.TryGetProperty("current_upgrade_level",out var value)?value.GetInt32():0;
  NativeBoundaryContract.Require(upgrade==card.GetProperty("upgrade").GetInt32(),"Native card serialized upgrade linkage");
 }
 internal static void State(JsonElement state,Func<string,bool> registered){
  var deck=state.GetProperty("deck").EnumerateArray().ToArray();var permanent=new HashSet<string>();
  foreach(var c in deck){Card(c);NativeBoundaryContract.Require(permanent.Add(c.GetProperty("id").GetString()!)&&registered(c.GetProperty("id").GetString()!),"Permanent card registered unique instance");}
  var combat=state.GetProperty("combat");if(combat.ValueKind==JsonValueKind.Null)return;
  var copies=new HashSet<string>();foreach(var pile in new[]{"hand","draw","discard","exhaust"})foreach(var c in combat.GetProperty(pile).EnumerateArray()){
   Card(c);NativeBoundaryContract.Require(copies.Add(c.GetProperty("id").GetString()!)&&registered(c.GetProperty("id").GetString()!),"Combat card registered unique pile instance");
   if(c.GetProperty("deck").ValueKind!=JsonValueKind.Null)NativeBoundaryContract.Require(permanent.Contains(c.GetProperty("deck").GetString()!),"Combat card permanent source linkage");
  }
 }
 internal static void Soup(JsonElement before,JsonElement after){
  var cards=after.GetProperty("deck").EnumerateArray().ToDictionary(c=>c.GetProperty("id").GetString()!);
  foreach(var c in before.GetProperty("deck").EnumerateArray()){
   if(c.GetProperty("model").GetString()!="CARD.STRIKE_IRONCLAD"||c.GetProperty("native").TryGetProperty("enchantment",out _))continue;
   var expected=JsonNode.Parse(c.GetRawText())!;
   expected["native"]!["enchantment"]=new JsonObject{["id"]=new JsonObject{["Category"]="ENCHANTMENT",["Entry"]="TEZCATARAS_EMBER"},["amount"]=1};
   NativeBoundaryContract.Snapshot(cards[c.GetProperty("id").GetString()!],JsonSerializer.SerializeToElement(expected),"Soup fixed plain Ironclad Strike enchantment/instance frame");
  }
 }
}
