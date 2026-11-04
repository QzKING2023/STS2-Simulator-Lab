using System.Text.Json;
using CombatSolver;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace OfflineSearchHarness;

internal static partial class EleanorV040Probe
{
    private sealed record GenerationRow(string Instance, string Model, int Upgrade, bool LocalCreator,
        bool LocalOwner, bool HasDeckVersion);
    private sealed record RewardGenerationHistory(GenerationRow[] Native, GenerationRow[] Simulator, bool Matches,
        string Semantics = "New CardGeneratedEntry versus prediction generation entries; resolved snapshots required. Root.History is not used.");

    private static RewardGenerationHistory CaptureRewardGenerationHistory(CombatState combat,
        CombatPredictionSimulator sim, VanillaCombatRootAdapter adapter, int nativeBefore, int simBefore)
    {
        var player=LocalContext.GetMe(combat)!;
        var native=CombatManager.Instance.History.Entries.OfType<CardGeneratedEntry>().Skip(nativeBefore)
            .Select(e=>new GenerationRow(adapter.ObservedCardId(e.Card,false),e.Card.Id.Entry,e.Card.CurrentUpgradeLevel,
                ReferenceEquals(e.Creator,player),ReferenceEquals(e.Card.Owner,player),e.Card.DeckVersion!=null)).ToArray();
        var generated=sim.History.Entries.OfType<CombatPredictionCardGeneratedEntry>().Skip(simBefore).ToArray();
        var predicted=generated.Select(e=>new GenerationRow(adapter.ObservedCardId(e.Card.Original,true),e.Card.Id,e.Card.UpgradeLevel,
            ReferenceEquals(e.Creator,player),ReferenceEquals(e.Card.Owner,player),e.Card.Original.DeckVersion!=null)).ToArray();
        bool resolved=generated.All(e=>sim.History.Entries.OfType<CombatPredictionCardGenerationResolvedEntry>()
            .Any(r=>ReferenceEquals(r.OriginalEntry,e)&&ReferenceEquals(r.Card.Original,e.Card.Original)));
        return new(native,predicted,native.SequenceEqual(predicted)&&resolved);
    }

    private static JsonElement IdentityAction(CombatState combat, CardModel card, JsonElement original, int occurrence)
    {
        var plan=ParseAction(original);
        var pile=card.GetType().Name=="ReturnFlow"?PileType.Draw:PileType.Hand;
        var source=pile==PileType.Draw?card.Owner.PlayerCombatState!.DrawPile.Cards:card.Owner.PlayerCombatState!.Hand.Cards;
        var matching=source.Where(c=>c.GetType().Name=="ManaStrike"&&c.CurrentUpgradeLevel==0).ToArray();
        var chosen=matching[occurrence];
        var choices=plan.NestedChoices!.ToArray();
        var first=choices[0];
        choices[0]=first with {Cards=[new PlanCardToken(chosen.Id.Entry,0,CardChoiceSupport.ChoiceCardKey(chosen),occurrence,occurrence,"identity fixture")]};
        // Preserve the original source envelope. Only the explicit physical selection is changed.
        var payload=System.Text.Json.Nodes.JsonNode.Parse(original.GetRawText())!.AsObject();
        payload["nestedChoices"]=JsonSerializer.SerializeToNode(choices,ActionJson);
        return JsonSerializer.SerializeToElement(payload);
    }

    private static object VerifyRewardSiblingIdentity(CombatState combat, CardModel card, JsonElement a, JsonElement b)
    {
        var player=card.Owner;var root=CombatRootSnapshot.Capture(combat);var parent=root.ForkSimulator();
        _=GetState(parent,GetBranchOrb(parent,player),CaptureState(_stateType!,GetLiveManaState(player)));
        string ParentStamp()=>ContinuationStamp.CapturePredicted(player,parent,player.PlayerCombatState!.TurnNumber,root.Forecast,root.StartTurnNumber).StateText;
        string before=ParentStamp();int parentHistory=parent.History.Entries.Count();
        object Execute(JsonElement action)
        {
            var branch=parent.Fork();var shadow=(SimulatedCombatState)branch.State.CombatState;
            var predicted=branch.State.FindCard(card)!;
            var adapter=new VanillaCombatRootAdapter(combat,"reward-identity-sibling");
            shadow.BeginActionChoices(ParseAction(action).GetActionChoicesInExecutionOrder());
            try
            {
                using var scope=shadow.BeginCardExecutionScope(new HashSet<uint>());
                if(!branch.ManualPlay(predicted,null,out _)||branch.HasPendingChoice||!CombatBeamSolver.SettleReplayActionBoundary(branch,shadow))
                    throw new InvalidOperationException("Identity sibling did not settle.");
            }
            finally{shadow.EndActionChoices();}
            if(ParentStamp()!=before||parent.History.Entries.Count()!=parentHistory)throw new InvalidOperationException("Identity sibling mutated parent.");
            var generations=branch.History.Entries.OfType<CombatPredictionCardGeneratedEntry>().Select(e=>new{
                instance=adapter.ObservedCardId(e.Card.Original,true),e.Card.Id,e.Card.UpgradeLevel,kind=e.ResultKind.ToString(),localCreator=ReferenceEquals(e.Creator,player)}).ToArray();
            return new{root=adapter.CapturePredicted(combat,player,branch,player.PlayerCombatState!.TurnNumber,root.Forecast,root.StartTurnNumber),
                continuation=ContinuationStamp.CapturePredicted(player,branch,player.PlayerCombatState.TurnNumber,root.Forecast,root.StartTurnNumber).StateText,
                selections=shadow.AdapterSelections.ToArray(),generations,mana=GetState(branch,GetBranchOrb(branch,player),CaptureState(_stateType!,GetLiveManaState(player))).Capture()};
        }
        var first=JsonSerializer.SerializeToElement(Execute(a));var sibling=JsonSerializer.SerializeToElement(Execute(b));var repeated=JsonSerializer.SerializeToElement(Execute(a));
        bool equal=first.GetRawText()==repeated.GetRawText();bool distinct=first.GetRawText()!=sibling.GetRawText();
        if(!equal||!distinct||ParentStamp()!=before)throw new InvalidOperationException("Same-parent A/B/A identity contract failed.");
        return new{sameParent=true,a=first,b=sibling,aAgain=repeated,aRepeatedExact=equal,bDistinct=distinct,parentUnchanged=true,parentHistory};
    }
}
