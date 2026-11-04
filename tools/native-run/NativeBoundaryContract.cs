using System.Text.Json;
using System.Text.Json.Nodes;
namespace OfflineSearchHarness;

// Internal integrity primitives. Native tasks remain the authority for business
// intervals; these helpers bind their owners and immutable observation points.
internal static class NativeBoundaryContract
{
 internal static bool Equal(JsonElement a,JsonElement b)=>a.ValueKind!=JsonValueKind.Undefined&&b.ValueKind!=JsonValueKind.Undefined&&SimulatorLab.Core.NativeDecisionTicket.SameActionJson(a.GetRawText(),b.GetRawText());
 internal static void Require(bool value,string reason){if(!value)throw new NativeDataException(reason);}
 internal static void Owner(JsonElement p,string root,string context,bool phase,string reason)=>Require(phase&&p.GetProperty("root").GetString()==root&&p.GetProperty("context").GetString()==context,reason);
 internal static void Snapshot(JsonElement actual,JsonElement anchor,string reason,bool closing=false){
  if(!closing){Require(Equal(actual,anchor),reason);return;}
  var expected=JsonNode.Parse(anchor.GetRawText())!;expected["lifecycle"]!["native"]!["Closed"]=true;Require(Equal(actual,JsonSerializer.SerializeToElement(expected)),reason);
 }
 internal static void Room(JsonElement state,JsonElement reference,string type,string reason)=>Require(state.GetProperty("room").GetString()==type&&Equal(state.GetProperty("rooms").GetProperty("reference"),reference),reason);
}
internal sealed class NativeStageLedger
{
 readonly HashSet<(string Owner,string Stage)> stages=[];
 internal void Observe(string owner,string stage)=>NativeBoundaryContract.Require(owner!=""&&stages.Add((owner,stage)),"Required unique lifecycle stage: "+owner+"/"+stage);
 internal void Require(string owner,params string[] required)=>NativeBoundaryContract.Require(required.All(stage=>stages.Contains((owner,stage))),"Missing lifecycle stages: "+owner);
}
internal sealed class NativeInstanceLedger
{
 HashSet<string>? before;
 internal void Begin(IEnumerable<string> existing){NativeBoundaryContract.Require(before==null,"Instance scope already captured");before=existing.ToHashSet();}
 internal void Fresh(IEnumerable<string> introduced,Func<string,bool> registered,string reason){var existing=before??throw new NativeDataException("Missing instance scope");var ids=introduced.ToArray();NativeBoundaryContract.Require(existing.Count>0&&ids.Distinct().Count()==ids.Length&&ids.All(id=>!existing.Contains(id)&&registered(id)),reason);}
}
