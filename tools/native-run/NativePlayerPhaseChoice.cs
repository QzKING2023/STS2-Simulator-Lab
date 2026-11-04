#if SOLVER_GATE
using System.Reflection;
using CombatSolver;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using SimulatorLab.Core;
namespace OfflineSearchHarness;
// Observe the original shuffle hook; no shuffle, draw or power effect is replaced.
internal static class NativePlayerPhaseChoice {
 static readonly MethodInfo Original=AccessTools.DeclaredMethod(typeof(StratagemPower),nameof(StratagemPower.AfterShuffle));
 static readonly MethodInfo MoveNext=AccessTools.DeclaredMethod(Original.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>()!.StateMachineType,"MoveNext");
 internal static int[] Select(PlanCardChoice plan,CardModel[] cards,int min,int max,string leaf,out object origin){
  var active=cards.FirstOrDefault()?.Owner?.Creature.Powers.OfType<StratagemPower>().SingleOrDefault();
  if(active==null||Harmony.GetPatchInfo(Original) is {Owners.Count:>0}||Harmony.GetPatchInfo(MoveNext) is {Owners.Count:>0}||!new System.Diagnostics.StackTrace().GetFrames().Any(f=>f.GetMethod()==MoveNext)||leaf!="FromCombatPile"||active.Owner.CombatState?.CurrentSide!=CombatSide.Player||active.Id.Entry!=plan.SourceId||cards.Any(c=>!ReferenceEquals(c.Owner,active.Owner.Player)))throw new NativeActionRejectedException("Player phase source/hook/owner/transport mismatch");
  var expected=NativeSolverChoices.ResolveTokens(plan,cards,min,max);
  var request=new NativeChoiceRequest(0,NativeChoiceSurfaceKind.CombatPile,active.Owner.Player!,cards,null,min,max,false,false,false,plan.SourceId);
  var resolver=AccessTools.Method(typeof(NativeChoiceSession),"ResolvePlannedCards");
  IReadOnlyList<CardModel> selected;
  try{selected=(IReadOnlyList<CardModel>)resolver.Invoke(null,[plan,request,false])!;}catch(TargetInvocationException ex){throw new NativeActionRejectedException("Upstream player choice mismatch: "+ex.InnerException?.Message);}
  var result=selected.Select(c=>Array.IndexOf(cards,c)).ToArray();if(!result.SequenceEqual(expected))throw new NativeDataException("Upstream/pile token resolution disagreement");origin=new{hook="AfterShuffle",source=NativeIdentity.Get(active),model=active.Id.ToString(),owner=NativeIdentity.Get(active.Owner),amount=active.Amount,side=active.Owner.CombatState!.CurrentSide.ToString(),sourceId=active.Id.Entry,methodToken=MoveNext.MetadataToken,mvid=MoveNext.Module.ModuleVersionId,stackVerified=true};return result;
 }
}
#endif
