#if SOLVER_GATE
using CombatSolver;
using HarmonyLib;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
namespace OfflineSearchHarness;
internal static partial class Program {
 static void ConstantIntentControls(){
  int variable=8;var fixedIntent=new SingleAttackIntent(8);var dynamicIntent=new SingleAttackIntent(()=>variable);
  var checks=new List<bool>{IntentForecaster.IsKnownConstantIntent(fixedIntent),IntentForecaster.IsKnownConstantIntent(new SingleAttackIntent(0)),!IntentForecaster.IsKnownConstantIntent(dynamicIntent)};
  variable=9;checks.Add(!IntentForecaster.IsKnownConstantIntent(dynamicIntent));
  var method=fixedIntent.DamageCalc!.Method;var h=new Harmony("Lab.ConstantIntent.NegativeControl");
  try{h.Patch(method,prefix:new HarmonyMethod(typeof(Program),nameof(ConstantIntentNoop)));checks.Add(!IntentForecaster.IsKnownConstantIntent(fixedIntent));}
  finally{h.Unpatch(method,HarmonyPatchType.All,h.Id);}
  checks.Add(IntentForecaster.IsKnownConstantIntent(fixedIntent));
  if(checks.Any(x=>!x))throw new InvalidOperationException("Constant intent provenance controls failed");
  Write("constant-intent-controls.json",new{passed=checks.Count,negative=3,positive=3,actualHarmonyInjection=true,businessDispatch=0,methodToken=method.MetadataToken,mvid=method.Module.ModuleVersionId,scope="integer constructor vs mutable delegate and unknown patch; no forecast/native equality claimed"});
 }
 static void ConstantIntentNoop(){}
}
#endif
