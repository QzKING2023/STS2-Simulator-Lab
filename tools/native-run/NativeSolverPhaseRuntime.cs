#if SOLVER_GATE
using System.Reflection;
using System.Text.Json;
using CombatSolver;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using SimulatorLab.Core;
namespace OfflineSearchHarness;

internal static class NativeSolverPhaseRuntime
{
 static readonly MethodInfo Source=AccessTools.Method(typeof(KnowledgeDemonChoiceSupport),"SourceId");
 static readonly MethodInfo Resolve=AccessTools.Method(typeof(NativeChoiceSession),"ResolvePlannedCards");
 internal static int[] Select(PlanCardChoice plan,CardModel[] cards,int min,int max,string leaf,out object origin){
  var one=new PlanAction(PlanActionKind.EndTurn,1){TurnStartChoices=[plan]};
  NativeSolverPhaseProtocol.Validate(JsonSerializer.SerializeToElement(one,Program.Json));
  if(plan.Timing==PlanChoiceTiming.PlayerTurnStart)return NativePlayerPhaseChoice.Select(plan,cards,min,max,leaf,out origin);
  var combat=CombatManager.Instance.DebugOnlyGetState()??throw new NativeActionRejectedException("No enemy phase combat");
  var owners=combat.Enemies.Where(e=>e.Monster?.IsPerformingMove==true).ToArray();
  if(leaf!="FromChooseACardScreen"||combat.CurrentSide!=CombatSide.Enemy||owners.Length!=1||cards.Length==0||cards.Any(c=>!ReferenceEquals(c.Owner,cards[0].Owner)))throw new NativeActionRejectedException("Enemy choice actual phase/source/transport mismatch");
  var owner=owners[0];
  // Same pure native-state reader and SourceId contract used by the pinned
  // upstream support. No local move rules or curse effects are implemented.
  if(AccessTools.Field(owner.Monster!.GetType(),"_curseOfKnowledgeCounter")==null)throw new NotSupportedException("Enemy choice native source lacks upstream counter contract");
  int counter=MonsterValueReader.ReadInt(owner.Monster!,"_curseOfKnowledgeCounter");
  string actual=(string)Source.Invoke(null,[owner,counter])!;
  if(plan.SourceId!=actual||actual.Split(':')[0]!=owner.Monster.Id.Entry)throw new NativeActionRejectedException("Enemy choice actual source/counter mismatch");
  var request=new NativeChoiceRequest(0,NativeChoiceSurfaceKind.ChooseCard,cards[0].Owner!,cards,null,min,max,false,false,false,actual);
  IReadOnlyList<CardModel> selected;
  try{selected=(IReadOnlyList<CardModel>)Resolve.Invoke(null,[plan,request,false])!;}
  catch(TargetInvocationException ex)when(ex.InnerException!=null){throw new NativeActionRejectedException("Upstream native choice matcher: "+ex.InnerException.Message);}
  origin=new{source=NativeIdentity.Get(owner),model=owner.Monster!.Id.ToString(),combatId=owner.CombatId,counter,sourceId=actual,move=owner.Monster.NextMove.Id,performing=owner.Monster.IsPerformingMove,side=combat.CurrentSide.ToString()};
  return selected.Select(c=>Array.IndexOf(cards,c)).ToArray();
 }
}
internal static partial class Program
{
 static bool phaseControlsDone;
 internal static void PhaseChoiceControls(PlanCardChoice plan,CardModel[] cards,int min,int max,string leaf){
  if(plan.Timing==PlanChoiceTiming.PlayerTurnStart){PlayerPhaseControls(plan,cards,min,max,leaf);return;}
  if(phaseControlsDone)return;
  string before=StateStamp();int count=dispatched;var token=plan.Cards.Single();int rejected=0;
  foreach(var bad in new[]{plan with{SourceId="stale-source"},plan with{ContextId="stale-context"},plan with{Timing=PlanChoiceTiming.PlayerTurnStart},plan with{SourcePile=PileType.Hand},plan with{Cards=[token with{CardId="INVALID"}]},plan with{Cards=[token with{UpgradeLevel=1}]},plan with{Cards=[token with{SourceOccurrence=1}]},plan with{Cards=[token with{OptionOccurrence=999}]},plan with{Cards=[token,token]},plan with{Cards=[]}}){
   try{_=NativeSolverPhaseRuntime.Select(bad,cards,min,max,leaf,out _);throw new NativeDataException("Enemy phase negative accepted");}
   catch(Exception ex)when(ex is NativeActionRejectedException or NotSupportedException){rejected++;}
  }
  try{_=NativeSolverPhaseRuntime.Select(plan,cards,min,max,"FromHand",out _);throw new NativeDataException("Enemy phase wrong surface accepted");}catch(NativeActionRejectedException){rejected++;}
  var a=NativeSolverPhaseRuntime.Select(plan,cards,min,max,leaf,out var origin);var b=NativeSolverPhaseRuntime.Select(plan,cards,min,max,leaf,out _);
  bool pure=before==StateStamp()&&dispatched==count&&a.SequenceEqual(b);
  Write("solver-phase-controls.json",new{schema=NativeSolverPhaseProtocol.Schema,root=NativeEvidence.Root,context=NativeEvidence.Context,origin,negative=rejected,positive=2,sourceUnchanged=pure,zeroAdditionalDispatch=count==dispatched,scope="actual native enemy choice callback; EndTurn already dispatched; controls do not dispatch additional roots"});
  if(!pure||rejected!=11)throw new NativeDataException("Enemy phase controls changed source or failed");phaseControlsDone=true;
 }
 static bool playerPhaseControlsDone;
 static void PlayerPhaseControls(PlanCardChoice plan,CardModel[] cards,int min,int max,string leaf){
  if(playerPhaseControlsDone)return;string before=StateStamp();int count=dispatched,rejected=0;
  var bads=new List<PlanCardChoice>{plan with{SourceId="wrong"},plan with{ContextId="old"},plan with{SourcePile=PileType.Hand},plan with{Timing=PlanChoiceTiming.Action}};
  if(plan.Cards.Count>0){var token=plan.Cards[0];bads.Add(plan with{Cards=[token with{StateKey="old"}]});bads.Add(plan with{Cards=[token with{SourceOccurrence=999}]});}
  foreach(var bad in bads){try{_=NativeSolverPhaseRuntime.Select(bad,cards,min,max,leaf,out _);throw new NativeDataException("Player phase bad token accepted");}catch(Exception e)when(e is NativeActionRejectedException or NotSupportedException){rejected++;}}
  var a=NativeSolverPhaseRuntime.Select(plan,cards,min,max,leaf,out var origin);var b=NativeSolverPhaseRuntime.Select(plan,cards,min,max,leaf,out _);
  bool pure=before==StateStamp()&&count==dispatched&&a.SequenceEqual(b);if(!pure||rejected!=bads.Count)throw new NativeDataException("Player phase control failure");
  Write("solver-player-phase-controls.json",new{negative=rejected,positive=2,sourceUnchanged=pure,zeroAdditionalDispatch=true,origin});playerPhaseControlsDone=true;
 }
}
#endif
