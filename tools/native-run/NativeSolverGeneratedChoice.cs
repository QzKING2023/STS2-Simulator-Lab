#if SOLVER_GATE
using System.Reflection;
using CombatSolver;
using CombatSolver.Engine.InCombat.Simulation;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using SimulatorLab.Core;
namespace OfflineSearchHarness;

// Reuse the upstream generated-potion capability and semantic native matcher.
// Native CardSelectCmd and potion OnUse own generation, RNG and card effects.
internal static class NativeSolverGeneratedChoice
{
 static readonly MethodInfo Resolve=AccessTools.Method(typeof(NativeChoiceSession),"ResolvePlannedCards");
 internal static void Validate(PlanAction a,PotionModel p,CombatPredictionSimulator simulator){
  if(a.Choice==null){if(PotionChoiceSupport.RequiresChoice(p))throw new NativeActionRejectedException("Missing required primary potion choice");return;}
  if(a.PotionId!=p.Id.Entry||!PotionChoiceSupport.RequiresChoice(p))throw new NativeActionRejectedException("Potion primary source mismatch");
  if(PotionChoiceSupport.GeneratesCardChoice(p)){Check(a.Choice,p.Id.Entry);return;}
  var spec=PotionChoiceSupport.GetSpec(simulator,p);var c=a.Choice;
  if(c.Effect!=spec.Effect||c.SourcePile!=spec.SourcePile||c.ContextId!=spec.ContextId||c.Timing!=PlanChoiceTiming.Action||c.SourceId!=p.Id.Entry||c.Cards.Count<Math.Min(spec.MinCount,spec.Options.Count)||c.Cards.Count>Math.Min(spec.MaxCount,spec.Options.Count))throw new NativeActionRejectedException("Upstream potion choice spec mismatch");
  foreach(var t in c.Cards){
   if(t.SourceOccurrence<0||t.OptionOccurrence<0||string.IsNullOrEmpty(t.StateKey))throw new NativeActionRejectedException("Potion pile token identity missing");
   var option=spec.Options.Where(x=>CardChoiceSupport.MatchesToken(x,t)).Skip(t.OptionOccurrence).FirstOrDefault();
   var original=spec.SourceCards.Where(x=>CardChoiceSupport.MatchesToken(x,t)).Skip(t.SourceOccurrence).FirstOrDefault();
   if(option==null||!ReferenceEquals(option,original))throw new NativeActionRejectedException("Potion pile token not an actual upstream candidate");
  }
 }
 internal static int[] SelectPile(PlanCardChoice c,CardModel[] cards,int min,int max,string leaf,string source){
  bool surface=(c.Effect is PlanChoiceEffect.MoveToHand or PlanChoiceEffect.MoveToHandFreeThisTurn)&&c.SourcePile is PileType.Draw or PileType.Discard&&leaf=="FromCombatPile" || (c.Effect is PlanChoiceEffect.Exhaust or PlanChoiceEffect.DiscardAndDraw or PlanChoiceEffect.SetFreeThisCombat)&&c.SourcePile==PileType.Hand&&leaf=="FromHand";
  if(!surface||c.SourceId!=source||c.ContextId!=""||c.Timing!=PlanChoiceTiming.Action)throw new NativeActionRejectedException("Potion pile native source/surface mismatch");
  return NativeSolverChoices.ResolveTokens(c,cards,min,max);
 }
 static void Check(PlanCardChoice c,string source){
  if(c.Effect!=PlanChoiceEffect.GenerateToHand||c.SourcePile!=PileType.None||c.Timing!=PlanChoiceTiming.Action||c.SourceId!=source||c.ContextId!="")throw new NativeActionRejectedException("Generated potion choice source/effect/timing");
  if(c.Cards.Count>1||c.Cards.Any(t=>t.SourceOccurrence<0||t.OptionOccurrence<0||string.IsNullOrEmpty(t.StateKey)))throw new NativeActionRejectedException("Generated potion choice identity/cardinality");
 }
 internal static int[] Select(PlanCardChoice c,CardModel[] cards,int min,int max,string leaf,string source){
  Check(c,source);
  if(leaf!="FromChooseACardScreen"||cards.Length==0||cards.Any(x=>!ReferenceEquals(x.Owner,cards[0].Owner))||min<0||max!=1)throw new NativeActionRejectedException("Generated potion native candidate boundary");
  var req=new NativeChoiceRequest(0,NativeChoiceSurfaceKind.ChooseCard,cards[0].Owner!,cards,null,min,max,min==0,false,false,source);
  IReadOnlyList<CardModel> selected;
  try{selected=(IReadOnlyList<CardModel>)Resolve.Invoke(null,[c,req,false])!;}
  catch(TargetInvocationException ex)when(ex.InnerException!=null){throw new NativeActionRejectedException("Upstream generated choice: "+ex.InnerException.Message);}
  for(int i=0;i<c.Cards.Count;i++){
   var t=c.Cards[i];var chosen=selected[i];int n=Array.IndexOf(cards,chosen);
   if(CardChoiceSupport.ChoiceCardKey(chosen)!=t.StateKey||cards.Take(n).Count(x=>CardChoiceSupport.MatchesToken(x,t))!=t.SourceOccurrence)throw new NativeActionRejectedException("Generated choice exact state/source occurrence");
  }
  return selected.Select(c=>Array.IndexOf(cards,c)).ToArray();
 }
}
internal static partial class Program
{
 static bool generatedChoiceControlsDone;
 internal static void GeneratedChoiceControls(PlanCardChoice c,CardModel[] cards,int min,int max,string leaf,string source){
  if(generatedChoiceControlsDone||c.Cards.Count!=1)return;
  string before=StateStamp();int dispatchedBefore=dispatched,rejected=0;var t=c.Cards[0];
  foreach(var bad in new[]{c with{SourceId="wrong"},c with{ContextId="old"},c with{Timing=PlanChoiceTiming.PlayerTurnStart},c with{SourcePile=PileType.Hand},c with{Cards=[t,t]},c with{Cards=[t with{CardId="INVALID"}]},c with{Cards=[t with{UpgradeLevel=999}]},c with{Cards=[t with{StateKey="wrong"}]},c with{Cards=[t with{SourceOccurrence=999}]},c with{Cards=[t with{OptionOccurrence=999}]}}){
   try{_=NativeSolverGeneratedChoice.Select(bad,cards,min,max,leaf,source);throw new NativeDataException("Generated choice control accepted");}catch(NativeActionRejectedException){rejected++;}
  }
  var a=NativeSolverGeneratedChoice.Select(c,cards,min,max,leaf,source);var b=NativeSolverGeneratedChoice.Select(c,cards,min,max,leaf,source);
  bool pure=before==StateStamp()&&dispatched==dispatchedBefore&&a.SequenceEqual(b);
  Write("solver-generated-choice-controls.json",new{negative=rejected,positive=2,sourceUnchanged=pure,zeroAdditionalDispatch=dispatched==dispatchedBefore,root=NativeEvidence.Root,context=NativeEvidence.Context,scope="actual native generated choice callback; root already dispatched"});
  if(!pure||rejected!=10)throw new NativeDataException("Generated choice control failure");generatedChoiceControlsDone=true;
 }
}
#endif
