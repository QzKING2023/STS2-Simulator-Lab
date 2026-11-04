using SimulatorLab.Core;
#if SOLVER_GATE
using CombatSolver;
using CombatSolver.Engine.Common;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Combat;
#endif
namespace OfflineSearchHarness;
internal static partial class Program
{
#if SOLVER_GATE
 static bool enchantmentControlsDone;
 static void EnchantmentIdentityControls(PlanAction action,CardModel card,CombatState combat){
  if(enchantmentControlsDone)return;
  string source=State().GetRawText(),continuation=ContinuationStamp.CaptureLive(combat).StateText;int count=dispatched,rejected=0;
  var stamp=EnchantmentStateSupport.Describe(card.Enchantment!);
  foreach(var bad in new[]{action with{CardEnchantmentId=""},action with{CardEnchantmentId="UNKNOWN"},action with{CardStateKey=action.CardStateKey.Replace("|"+stamp+"|","|"+stamp+":stale|",StringComparison.Ordinal)},action with{CardStateOccurrence=999},action with{CardOccurrence=999},action with{CardUpgradeLevel=action.CardUpgradeLevel+1}}){
   try{_=ResolveSolverCard(bad,true,true);throw new NativeDataException("Enchantment identity negative accepted");}catch(Exception ex)when(ex is NativeActionRejectedException or NotSupportedException){rejected++;}
  }
  _=ResolveSolverCard(action,true,true);
  bool pure=source==State().GetRawText()&&continuation==ContinuationStamp.CaptureLive(combat).StateText;
  Write("solver-enchantment-controls.json",new{root=NativeEvidence.Root,context=NativeEvidence.Context,card=Id(card),enchantment=NativeSolverEnchantments.Capture(card),negativeMappings=rejected,positiveNativeIdentity=true,zeroAdditionalDispatch=dispatched==count,sourceUnchanged=pure});
  if(rejected!=6||count!=dispatched||!pure)throw new NativeDataException("Enchantment identity controls changed native source or dispatched");
  enchantmentControlsDone=true;
 }
#endif
}
