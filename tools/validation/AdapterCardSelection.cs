using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;
using CombatSolver.Engine.InCombat.Simulation;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace CombatSolver;

// A selection description is evidence only. Effects remain in the strictly registered OnPlay.
internal sealed record AdapterSelectionEvidence(string Source, string Context, PileType Pile,
    string[] Options, int SelectedIndex, bool Implicit);

internal sealed partial class SimulatedCombatState
{
    internal List<AdapterSelectionEvidence> AdapterSelections { get; } = [];

    internal bool TryResolveAdapterCardSelection(CardOnPlayMirrorContext context, string schema,
        string contract, PileType pile, IReadOnlyList<PredictedCard> options,
        out IReadOnlyList<PredictedCard> selected)
    {
        var type=context.OriginalCard.GetType();
        var target=AdaptedCardOnPlayMirrors.ResolveOnPlay(type)
            ?? throw new InvalidPlannedChoiceBranchException("Selection source has no OnPlay.");
        var registration=AdaptedCardOnPlayMirrors.Select(type,target,Harmony.GetPatchInfo(target));
        if(registration is null || registration.RequiresPatchComposition || registration.Schema!=schema
            || registration.Target!=target || string.IsNullOrEmpty(registration.SourceIdentity)
            || !ReferenceEquals(context.Simulator.State.CombatState,this))
            throw new InvalidPlannedChoiceBranchException("Selection source does not match strict registration.");
        context.Simulator.RejectExecutionContinuation(); // Whole-action replay owns opaque role transactions.
        var source=context.Simulator.State.GetPlayerCombatState(context.PreviewCard.Owner).GetCardPile(pile)!.Cards;
        if(options.Any(c=>!source.Contains(c)||c.Preview.Owner!=context.PreviewCard.Owner))
            throw new InvalidPlannedChoiceBranchException("Selection candidate belongs to another pile/owner.");
        selected=[];
        if(options.Count<=1)
        {
            selected=options;
            AdapterSelections.Add(new(context.PreviewCard.Id.Entry,contract,pile,
                options.Select(c=>CardChoiceSupport.ChoiceCardKey(c.Preview)).ToArray(),options.Count-1,true));
            return true;
        }
        var spec=new CardChoiceSpec(PlanChoiceEffect.ModDefined,pile,1,1,options,source,0d,ContextId:contract);
        var request=new TurnStartChoiceRequest(context.PreviewCard.Id.Entry,spec.Effect,pile,1,spec,contract,ActiveActionChoiceTiming);
        if(_activeActionChoices is null || !_activeActionChoices.TryTake(request,out var choice))
        {SetPendingTurnStartChoice(request);return false;}
        if(choice!.Cards.Count!=1)throw new InvalidPlannedChoiceBranchException("Selection requires exactly one card.");
        var token=choice.Cards[0];
        var fromOptions=options.Where(c=>CardChoiceSupport.MatchesToken(c,token)).Skip(token.OptionOccurrence).FirstOrDefault();
        var fromSource=source.Where(c=>CardChoiceSupport.MatchesToken(c,token)).Skip(token.SourceOccurrence).FirstOrDefault();
        if(token.OptionOccurrence<0||token.SourceOccurrence<0||fromOptions is null||!ReferenceEquals(fromOptions,fromSource)
            || CardChoiceSupport.ChoiceCardKey(fromOptions.Preview)!=token.StateKey)
            throw new InvalidPlannedChoiceBranchException("Stale or invalid selection state/physical occurrence.");
        selected=[fromOptions];
        AdapterSelections.Add(new(context.PreviewCard.Id.Entry,contract,pile,
            options.Select(c=>CardChoiceSupport.ChoiceCardKey(c.Preview)).ToArray(),Array.IndexOf(options.ToArray(),fromOptions),false));
        ClearPendingTurnStartChoice();return true;
    }
}
