using System.Text.Json;
using SimulatorLab.Core;
namespace OfflineSearchHarness;

// Finite single-beam contract. Coordinator aggregate budgets are a separate protocol.
internal static class NativeSolverBudgetContract
{
 internal static void Review(JsonElement q,JsonElement profile)
 {
  void Need(bool ok,string why){if(!ok)throw new NativeDataException("Solver budget evidence: "+why);}
  var p=q.GetProperty("Profile");var b=q.GetProperty("budget");var configured=profile.GetProperty("settings").GetProperty("Profile");
  long nodes=p.GetProperty("MaxExpandedNodes").GetInt64(),expanded=q.GetProperty("ExpandedNodes").GetInt64(),total=q.GetProperty("TotalExpandedNodes").GetInt64();
  Need(q.GetProperty("engine").GetString()=="CombatBeamSolver","single-beam engine required");
  Need(nodes>0&&expanded>=0&&total==expanded&&expanded<=nodes,"actual nonnegative single-beam work within cap");
  Need(nodes==b.GetProperty("nodes").GetInt64()&&nodes==b.GetProperty("returnedAggregateNodeLimit").GetInt64()&&nodes==configured.GetProperty("MaxExpandedNodes").GetInt64(),"effective node cap/profile/configuration");
  foreach(var (a,z) in new[]{("BeamWidth","beam"),("SoftTimeBudgetMilliseconds","softMilliseconds")})
   Need(p.GetProperty(a).GetInt64()>0&&p.GetProperty(a).GetInt64()==b.GetProperty(z).GetInt64()&&p.GetProperty(a).GetInt64()==configured.GetProperty(a).GetInt64(),"effective "+a);
  Need(b.GetProperty("dop").GetInt32()==1&&profile.GetProperty("settings").GetProperty("SearchMaxDegreeOfParallelism").GetInt32()==1,"single worker configuration");
  string boundary=q.GetProperty("boundary").GetString()!;
  if(boundary=="NodeLimit")Need(expanded==nodes,"NodeLimit requires actual expanded work at effective cap");
  if(boundary=="TimeLimit"){
   // Old files without measured search elapsed time cannot certify this boundary.
   double elapsed=q.GetProperty("searchElapsedMilliseconds").GetDouble();
   Need(double.IsFinite(elapsed)&&elapsed>=b.GetProperty("softMilliseconds").GetInt64(),"TimeLimit requires measured elapsed search budget");
  }
 }
}
