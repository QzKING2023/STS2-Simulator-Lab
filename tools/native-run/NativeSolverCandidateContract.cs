using System.Text.Json;
using SimulatorLab.Core;
namespace OfflineSearchHarness;

// Adoption is only permission to validate one step in a shadow, never proof of victory.
internal static class NativeSolverCandidateContract
{
 internal const string Policy="node-first-action/v1";
 internal static string? Rejection(JsonElement q)
 {
  var c=q.GetProperty("candidate");var actions=q.GetProperty("actions");
  if(c.GetProperty("policy").GetString()!=Policy) return "unknown-candidate-policy";
  if(q.GetProperty("boundary").GetString() is not ("None" or "NodeLimit")) return "unsupported-search-boundary";
  if(c.GetProperty("resultScope").GetString()!="SearchCompletion")return "unsupported-result-scope";
  if(c.GetProperty("snapshotBoundary").GetString()!="None")return "unsupported-snapshot-boundary";
  if(c.GetProperty("hasRisk").GetBoolean()||c.GetProperty("unmirrored").GetArrayLength()!=0||c.GetProperty("forecastUnsupported").GetBoolean())return "unmodeled-risk";
  if(!c.GetProperty("forecastExact").GetBoolean()||c.GetProperty("approximations").GetArrayLength()!=0)return "approximate-forecast";
  if(c.GetProperty("playerDead").GetBoolean())return "candidate-predicts-death";
  if(actions.ValueKind!=JsonValueKind.Array||actions.GetArrayLength()==0)return "no-candidate";
  if(actions[0].GetProperty("Turn").GetInt32()!=q.GetProperty("nativeState").GetProperty("combat").GetProperty("turn").GetInt32())return "candidate-not-current-turn";
  return null;
 }
 internal static void Review(JsonElement q,JsonElement profile)
 {
  NativeSolverBudgetContract.Review(q,profile);
  var reason=Rejection(q);
  if(reason!=null)throw new NativeDataException("Solver candidate evidence: "+reason);
  NativeSolverActionContract.Fields(q.GetProperty("actions")[0],choices:true,potions:true,enchantments:NativeSolverEnchantments.Enabled(q));
 }
}
