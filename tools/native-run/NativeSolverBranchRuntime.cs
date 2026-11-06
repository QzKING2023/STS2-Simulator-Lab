using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
namespace OfflineSearchHarness;

// This certifies the selected dependency's fallback policy, not a static
// forecast's precision. Source/artifact provenance remains in the build lock.
internal static class NativeSolverBranchRuntime
{
 internal const string Policy="upstream-explicit-conditions/no-root-fallback/v1";
 internal const string Schema="branch-runtime-admission/v1";
 static object? proof;
 static bool captured;
 internal static object? Capture(){
  if(captured)return proof;captured=true;
#if SOLVER_GATE
  string root=AppContext.BaseDirectory;var l=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"solver-lock.json"))).RootElement;
  if(!l.TryGetProperty("derivative",out var d)||!d.TryGetProperty("policy",out var p)||p.GetString()!=Policy)return null;
  var row=l.GetProperty("assemblies").EnumerateArray().Single(x=>x.GetProperty("name").GetString()=="CombatSolver");
  string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,row.GetProperty("file").GetString()!))));
  string derivative=d.GetProperty("id").GetString()!;
  if(derivative=="combat-hook-ending.1"&&(d.GetProperty("baseDerivative").GetString()!="duration-ending-boundary.1"||d.GetProperty("baseSolverSha256").GetString()!="DBC8EE901EA02251826A04AEBA8F09E8037ACC63ED2BC2DF57F043F4EF624C32"||d.GetProperty("boundaryPolicy").GetString()!="PowerCmd.Apply/IsOverOrEnding"))throw new InvalidOperationException("Combat hook dependency provenance mismatch");
  if(derivative=="duration-ending-boundary.1"&&(d.GetProperty("baseDerivative").GetString()!="orb-terminal-boundary.1"||d.GetProperty("baseSolverSha256").GetString()!="6D0C4421D03451A1DFEED2809545B89B95048594D3985FCA775A4531499C299F"||d.GetProperty("boundaryPolicy").GetString()!="PowerCmd.Apply/IsOverOrEnding"))throw new InvalidOperationException("Duration ending dependency provenance mismatch");
  if(derivative=="orb-terminal-boundary.1"&&(d.GetProperty("baseDerivative").GetString()!="osty-root-identity.1"||d.GetProperty("baseSolverSha256").GetString()!="376D1E61BA2CF4640D9A36A73C56856B58244961D003ADECB21F0258F3B7D205"||d.GetProperty("boundaryPolicy").GetString()!="PowerCmd.Apply/IsOverOrEnding"))throw new InvalidOperationException("Orb terminal dependency provenance mismatch");
  if(derivative=="osty-root-identity.1"&&(d.GetProperty("baseDerivative").GetString()!="death-amount-sync.1"||d.GetProperty("baseSolverSha256").GetString()!="A5AEFA38D53A84B4F67BF71EDA2F3B1EEF7B0EE0FFCE3FC0C09CDE3413D2C35E"||d.GetProperty("boundaryPolicy").GetString()!="PowerCmd.Apply/IsOverOrEnding"))throw new InvalidOperationException("Pet root dependency provenance mismatch");
  if(derivative=="death-amount-sync.1"&&(d.GetProperty("baseDerivative").GetString()!="self-cost-order.1"||d.GetProperty("boundaryPolicy").GetString()!="PowerCmd.Apply/IsOverOrEnding"))throw new InvalidOperationException("Death amount dependency provenance mismatch");
  if(derivative=="self-cost-order.1"&&(d.GetProperty("baseDerivative").GetString()!="turn-history-side.1"||d.GetProperty("boundaryPolicy").GetString()!="PowerCmd.Apply/IsOverOrEnding"))throw new InvalidOperationException("Self-cost dependency provenance mismatch");
  if(derivative=="turn-history-side.1"&&(d.GetProperty("baseDerivative").GetString()!="tainted-root.1"||d.GetProperty("boundaryPolicy").GetString()!="PowerCmd.Apply/IsOverOrEnding"))throw new InvalidOperationException("Turn history dependency provenance mismatch");
  if(derivative=="ending-powers.2"&&(d.GetProperty("baseDerivative").GetString()!="strict-branch-runtime.1"||d.GetProperty("boundaryPolicy").GetString()!="PowerCmd.Apply/IsOverOrEnding"))throw new InvalidOperationException("Ending power dependency provenance mismatch");
  if(derivative=="tainted-root.1"&&(d.GetProperty("baseDerivative").GetString()!="illusion-admission.1"||d.GetProperty("boundaryPolicy").GetString()!="PowerCmd.Apply/IsOverOrEnding"))throw new InvalidOperationException("Tainted root dependency provenance mismatch");
  if(derivative=="illusion-admission.1"&&(d.GetProperty("baseDerivative").GetString()!="tainted-lifecycle.1"||d.GetProperty("boundaryPolicy").GetString()!="PowerCmd.Apply/IsOverOrEnding"))throw new InvalidOperationException("Illusion dependency provenance mismatch");
  if(derivative=="tainted-lifecycle.1"&&(d.GetProperty("baseDerivative").GetString()!="constant-intent.1"||d.GetProperty("boundaryPolicy").GetString()!="PowerCmd.Apply/IsOverOrEnding"))throw new InvalidOperationException("Tainted lifecycle dependency provenance mismatch");
  if(derivative=="constant-intent.1"&&(d.GetProperty("baseDerivative").GetString()!="possessed-root.1"||d.GetProperty("boundaryPolicy").GetString()!="PowerCmd.Apply/IsOverOrEnding"))throw new InvalidOperationException("Constant intent dependency provenance mismatch");
  if(derivative=="possessed-root.1"&&(d.GetProperty("baseDerivative").GetString()!="ending-powers.2"||d.GetProperty("boundaryPolicy").GetString()!="PowerCmd.Apply/IsOverOrEnding"))throw new InvalidOperationException("Possession root dependency provenance mismatch");
  if(hash!=row.GetProperty("sha256").GetString()||derivative is not ("strict-branch-runtime.1" or "ending-powers.2" or "possessed-root.1" or "constant-intent.1" or "tainted-lifecycle.1" or "illusion-admission.1" or "tainted-root.1" or "turn-history-side.1" or "self-cost-order.1" or "death-amount-sync.1" or "osty-root-identity.1" or "orb-terminal-boundary.1" or "duration-ending-boundary.1" or "combat-hook-ending.1"))throw new InvalidOperationException("Branch runtime dependency identity mismatch");
  var assembly=typeof(CombatSolver.CombatBeamSolver).Assembly;
  if(!assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Any(x=>x.Key=="LabDerivative"&&x.Value==derivative))throw new InvalidOperationException("Branch runtime compiled metadata mismatch");
  proof=new{schema=Schema,policy=Policy,derivative,assemblySha256=hash,scope="explicit branch resolution; static preview remains approximate"};
#endif
  return proof;
 }
 internal static bool AllowsConditionalPreview(JsonElement q){
  if(!q.TryGetProperty("branchRuntime",out var supplied)||supplied.ValueKind!=JsonValueKind.Object)return false;
  var actual=Capture();if(actual==null)return false;
  var expected=JsonSerializer.SerializeToElement(actual);
  if(!NativeBoundaryContract.Equal(supplied,expected))return false;
  var details=q.GetProperty("candidate").GetProperty("approximations");
  return details.ValueKind==JsonValueKind.Array&&details.GetArrayLength()>0&&details.EnumerateArray().All(x=>x.ValueKind==JsonValueKind.String&&x.GetString()!.EndsWith(":\u6761\u4ef6\u5206\u652f",StringComparison.Ordinal));
 }
}
