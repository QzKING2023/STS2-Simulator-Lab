using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using SimulatorLab.Core;
#if SOLVER_GATE
using CombatSolver;
#endif
namespace OfflineSearchHarness;
#if SOLVER_GATE
// Reuses the pinned PlanCardChoice token contract and the existing native
// selector transport. Effects are dispatched by the original game, not this bridge.
internal sealed class NativeSolverChoices : IDisposable
{
    readonly IReadOnlyList<PlanCardChoice> choices;
    readonly string root=NativeEvidence.Root,context=NativeEvidence.Context,source;
    int index;
    readonly bool phase,generated,potionPile;
    internal readonly List<object> Records=[];
    readonly List<NativeHandMutation> mutations=[];
    // The Lab waits through the NEXT player start. Upstream UI deployment's
    // CaptureDeploymentEndTurnChoices intentionally stops before that phase.
    internal NativeSolverChoices(PlanAction action){source=action.Kind==PlanActionKind.UsePotion?action.PotionId:action.CardId;phase=action.Kind==PlanActionKind.EndTurn;generated=action.Kind==PlanActionKind.UsePotion&&action.Choice?.Effect==PlanChoiceEffect.GenerateToHand;potionPile=action.Kind==PlanActionKind.UsePotion&&action.Choice!=null&&!generated;choices=phase?action.TurnStartChoices??[]:action.GetActionChoicesInExecutionOrder();if(NativeChoices.SolverProvider!=null)throw new NativeActionRejectedException("Nested solver choice scope");NativeChoices.SolverProvider=Select;}
    internal static int[] Resolve(PlanCardChoice choice,CardModel[] cards,int min,int max,string source)
    {
        // The native CardSelectCmd surface is not a one-to-one mapping to the
        // logical source pile.  Cleanse, for example, consumes a Draw-pile
        // card through FromCombatPile.  Keep the logical effect/source token
        // strict, but defer the physical surface check to Select where the
        // actual leaf method is available.
        if(!(choice.Effect is PlanChoiceEffect.Exhaust or PlanChoiceEffect.Upgrade or PlanChoiceEffect.Discard or PlanChoiceEffect.Duplicate or PlanChoiceEffect.ApplyRetain or PlanChoiceEffect.ApplyEthereal or PlanChoiceEffect.MoveToHand or PlanChoiceEffect.MoveToDrawTop)||choice.Timing!=PlanChoiceTiming.Action||choice.ContextId!=""||choice.SourceId!=""&&choice.SourceId!=source)throw new NotSupportedException("Unsupported solver choice provenance/effect/timing");
        return ResolveTokens(choice,cards,min,max);
    }
    internal static int[] ResolveTokens(PlanCardChoice choice,CardModel[] cards,int min,int max)
    {
        if(choice.Cards.Count<min||choice.Cards.Count>max)throw new NativeActionRejectedException("Solver child cardinality");
        if(cards.Length==0&&choice.Cards.Count==0&&min==0)return [];
        var pile=choice.SourcePile.GetPile(cards.FirstOrDefault()?.Owner??throw new NativeActionRejectedException("Empty solver choice candidates")).Cards;
        var result=new List<int>();
        foreach(var token in choice.Cards){
            if(token.SourceOccurrence<0||token.OptionOccurrence<0||string.IsNullOrEmpty(token.StateKey))throw new NativeActionRejectedException("Missing solver child identity");
            var card=cards.Where(c=>CardChoiceSupport.MatchesToken(c,token)).Skip(token.OptionOccurrence).FirstOrDefault();
            if(card==null||result.Contains(Array.IndexOf(cards,card))||!pile.Any(c=>ReferenceEquals(c,card))||pile.TakeWhile(c=>!ReferenceEquals(c,card)).Count(c=>CardChoiceSupport.MatchesToken(c,token))!=token.SourceOccurrence||CardChoiceSupport.ChoiceCardKey(card)!=token.StateKey)throw new NativeActionRejectedException("Stale/illegal solver child token");
            result.Add(Array.IndexOf(cards,card));
        }
        return result.ToArray();
    }
    int[] Select(string request,CardModel[] cards,int min,int max,string leaf,bool implicitChoice)
    {
        if(NativeEvidence.Root!=root||NativeEvidence.Context!=context||(!phase&&!generated&&leaf is not ("FromHand" or "FromHandForUpgrade" or "FromCombatPile")))throw new NativeActionRejectedException("Unplanned/stale solver child request");
        if(index>=choices.Count){if(implicitChoice&&cards.Length==0&&min==0)return [];throw new NativeActionRejectedException("Unplanned solver child request");}
        var choice=choices[index];
        // Original FromCombatPile returns before presenting a choice when combat
        // is ending. The upstream branch also leaves the pile untouched; retain
        // the planned token as cancelled evidence, never move the card locally.
        if(!phase&&!generated&&!potionPile&&implicitChoice&&leaf=="FromCombatPile"&&cards.Length==0&&min==0&&MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsEnding){
            var combat=MegaCrit.Sts2.Core.Combat.CombatManager.Instance.DebugOnlyGetState()??throw new NativeActionRejectedException("Missing ending combat");
            var pile=choice.SourcePile.GetPile(combat.Players.Single()).Cards.ToArray();_=Resolve(choice,pile,0,Math.Max(max,choice.Cards.Count),source);
            Program.Observe("solver-terminal-choice",new{request,choice,source,leaf,ending=true});
            Records.Add(new{request,root,context,source,leaf,choice,indices=Array.Empty<int>(),selected=Array.Empty<string>(),candidates=Array.Empty<object>(),suppressedAtTerminal=true});index++;return [];
        }
        if(!phase&&!generated&&!potionPile&&((choice.Effect is PlanChoiceEffect.Exhaust or PlanChoiceEffect.Discard or PlanChoiceEffect.Duplicate or PlanChoiceEffect.ApplyRetain or PlanChoiceEffect.ApplyEthereal)&&leaf is not ("FromHand" or "FromCombatPile")||(choice.Effect==PlanChoiceEffect.Upgrade&&(leaf is not ("FromHandForUpgrade" or "FromCombatPile" or "FromDeckForUpgrade")||cards.Any(c=>!c.IsUpgradable)))||(choice.Effect is PlanChoiceEffect.MoveToHand or PlanChoiceEffect.MoveToDrawTop&&leaf!=(choice.SourcePile==PileType.Hand?"FromHand":"FromCombatPile"))))throw new NativeActionRejectedException("Solver choice native surface/effect mismatch");
        object? origin=null;var indices=phase?NativeSolverPhaseRuntime.Select(choice,cards,min,max,leaf,out origin):generated?NativeSolverGeneratedChoice.Select(choice,cards,min,max,leaf,source):potionPile?NativeSolverGeneratedChoice.SelectPile(choice,cards,min,max,leaf,source):Resolve(choice,cards,min,max,source);
        if(!phase&&!generated&&!potionPile)Controls(choice,cards,min,max,source);
        if(generated)Program.GeneratedChoiceControls(choice,cards,min,max,leaf,source);
        if(phase){Program.PhaseChoiceControls(choice,cards,min,max,leaf);Program.Observe("solver-phase-choice",new{request,choice,phase=origin});}
        NativeHandMutation? mutation=null;
        if(!phase&&!generated&&!potionPile&&choice.Effect is PlanChoiceEffect.Duplicate or PlanChoiceEffect.ApplyRetain or PlanChoiceEffect.MoveToDrawTop){
            mutation=new(cards,indices);mutations.Add(mutation);
        }
        Records.Add(new{request,root,context,source,leaf,choice,indices,phase=origin,mutation,candidates=cards.Select(c=>new{id=NativeIdentity.Get(c),model=c.Id.ToString(),upgrade=c.CurrentUpgradeLevel,key=CardChoiceSupport.ChoiceCardKey(c)}).ToArray(),selected=indices.Select(i=>NativeIdentity.Get(cards[i])).ToArray()});index++;return indices;
    }
    static readonly HashSet<PlanChoiceEffect> checkedEffects=[];
    static void Controls(PlanCardChoice c,CardModel[] cards,int min,int max,string source){
        if(c.Cards.Count==0||!checkedEffects.Add(c.Effect))return;
        var before=Program.StateStamp();int rejected=0;var token=c.Cards[0];
        foreach(var bad in new[]{c with{SourceId="wrong"},c with{ContextId="old"},c with{Timing=PlanChoiceTiming.PlayerTurnStart},c with{Cards=[token with{StateKey="wrong"}]},c with{Cards=[token with{OptionOccurrence=999}]},c with{Cards=[token with{SourceOccurrence=999}]},c with{Cards=[token with{CardId="INVALID"}]}}){
            try{_=Resolve(bad,cards,min,max,source);throw new NativeDataException("Hand/pile negative choice accepted");}catch(Exception ex)when(ex is NativeActionRejectedException or NotSupportedException){rejected++;}
        }
        var a=Resolve(c,cards,min,max,source);var b=Resolve(c,cards,min,max,source);
        if(c.Effect is PlanChoiceEffect.Discard or PlanChoiceEffect.Duplicate or PlanChoiceEffect.ApplyRetain)foreach(var bad in new[]{c with{SourcePile=PileType.Exhaust},c with{Cards=[token,token]}}){
            try{_=Resolve(bad,cards,min,max,source);throw new NativeDataException("Discard negative choice accepted");}catch(Exception ex)when(ex is NativeActionRejectedException or NotSupportedException){rejected++;}
        }
        if(rejected!=(c.Effect is PlanChoiceEffect.Discard or PlanChoiceEffect.Duplicate or PlanChoiceEffect.ApplyRetain?9:7)||!a.SequenceEqual(b)||before!=Program.StateStamp())throw new NativeDataException("Hand/pile choice control mutated native state");
        Program.RecordPileChoiceControl(c.Effect.ToString(),source,rejected);

    }
    internal void AssertConsumed(){if(index!=choices.Count)throw new NativeActionRejectedException("Unconsumed extra solver child choices");foreach(var m in mutations)m.Finish();}
    public void Dispose(){NativeChoices.SolverProvider=null;}
}
internal static partial class Program {
 internal static void RecordPileChoiceControl(string effect,string source,int rejected)=>Write("solver-pile-choice-controls-"+effect+".json",new{effect,source,negative=rejected,positive=2,root=NativeEvidence.Root,context=NativeEvidence.Context,sourceUnchanged=true,zeroAdditionalDispatch=true,scope="pure matcher controls inside an already dispatched root"});
}
#endif
