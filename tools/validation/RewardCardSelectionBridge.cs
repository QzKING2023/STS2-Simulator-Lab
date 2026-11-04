using System.Text.Json;
using System.Text.Json.Serialization;
using CombatSolver;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using SimulatorLab.Core;
using CombatSolver.Engine.Common;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Entities.Cards;
using System.Text.Json.Nodes;

namespace OfflineSearchHarness;

internal static partial class EleanorV040Probe
{
    private static readonly JsonSerializerOptions ActionJson = CreateActionJson();
    private static JsonSerializerOptions CreateActionJson()
    {var o=new JsonSerializerOptions{PropertyNameCaseInsensitive=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase};o.Converters.Add(new JsonStringEnumConverter());return o;}
    private static PlanAction ParseAction(JsonElement action)
    {
        try
        {
            var parsed=action.Deserialize<PlanAction>(ActionJson)??throw new JsonException("Missing action.");
            if(parsed.NestedChoicesBeforePrimary<0||parsed.NestedChoicesBeforePrimary>(parsed.NestedChoices?.Count??0))throw new JsonException("Invalid nested choice order.");
            if((parsed.TurnStartChoices?.Count??0)>0)
                throw new JsonException("TurnStartChoices are not supported by the native replay consumer.");
            if(parsed.Kind==PlanActionKind.EndTurn && parsed.GetActionChoicesInExecutionOrder().Count>0)
                throw new JsonException("EndTurn action choices are not supported by the native replay consumer.");
            return parsed;
        }
        catch(JsonException ex){throw new ValidationStopException(ValidationStop.InvalidAction,"action-json",ex.Message);}
    }

    // Coverage construction uses the same bounded candidate enumerator as search, replaying from
    // the unchanged parent for each newly discovered request. This is not online re-solving.
    internal static JsonElement BuildRewardAction(CombatState combat,CardModel nativeCard,int variant=0)
    {
        var player=nativeCard.Owner;var root=CombatRootSnapshot.Capture(combat);var parent=root.ForkSimulator();
        _=GetState(parent,GetBranchOrb(parent,player),CaptureState(_stateType!,GetLiveManaState(player)));
        string stamp=ContinuationStamp.CapturePredicted(player,parent,player.PlayerCombatState!.TurnNumber,root.Forecast,root.StartTurnNumber).StateText;
        var choices=new List<PlanCardChoice>();var names=SolverDisplayNames.Capture(combat);
        for(int depth=0;depth<8;depth++)
        {
            var child=parent.Fork();var shadow=(SimulatedCombatState)child.State.CombatState;
            var predicted=child.State.FindCard(nativeCard) ?? throw new InvalidOperationException("Coverage card is not in source root.");
            shadow.BeginActionChoices(choices);
            bool complete;
            using(shadow.BeginCardExecutionScope(new HashSet<uint>()))complete=child.ManualPlay(predicted,null,out _);
            shadow.EndActionChoices();
            if(ContinuationStamp.CapturePredicted(player,parent,player.PlayerCombatState.TurnNumber,root.Forecast,root.StartTurnNumber).StateText!=stamp)
                throw new InvalidOperationException("Coverage selection mutated parent.");
            if(complete&&!child.HasPendingChoice)
            {
                var matches=player.PlayerCombatState.Hand.Cards.Where(c=>c.Id==nativeCard.Id).ToArray();
                var plan=new PlanAction(PlanActionKind.PlayCard,player.PlayerCombatState.TurnNumber,nativeCard.Id.Entry,
                    Array.IndexOf(matches,nativeCard),NestedChoices:choices.ToArray(),CardStateKey:CardChoiceSupport.ChoiceCardKey(nativeCard),CardUpgradeLevel:nativeCard.CurrentUpgradeLevel);
                var payload=JsonNode.Parse(JsonSerializer.Serialize(plan,ActionJson))!.AsObject();
                payload["labChoiceSource"]=JsonSerializer.SerializeToNode(new{live=root.LiveStamp.StateText,continuation=root.ContinuationStamp.StateText,sequence=NativeCombatDecision.BoundarySequence});
                return JsonSerializer.SerializeToElement(payload);
            }
            var request=shadow.PendingTurnStartChoice ?? throw new InvalidOperationException("Missing pending selection request.");
            var candidates=CardChoiceSupport.BuildChoices(request.Spec!,names,16,16);
            if(candidates.Count==0)throw new InvalidOperationException("No legal selection candidate.");
            var selected=candidates[Math.Min(variant,candidates.Count-1)];
            choices.Add(selected with {SourceId=request.SourceId,ContextId=request.ContextId,Timing=request.Timing});
        }
        throw new PredictionUnsupportedException("Coverage nested selection limit reached.");
    }

    private sealed class RewardSelectionBridge(IEnumerable<AdapterSelectionEvidence> transcript) : ICardSelector
    {
        private readonly Queue<AdapterSelectionEvidence> _requests=new(transcript.Where(e=>!e.Implicit));
        public List<object> Calls {get;}=[];
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options,int minSelect,int maxSelect)
        {
            if(_requests.Count==0)throw new InvalidOperationException("Native requested an unplanned selection.");
            var request=_requests.Dequeue();var cards=options.ToArray();
            var keys=cards.Select(CardChoiceSupport.ChoiceCardKey).ToArray();
            if(minSelect!=1||maxSelect!=1||!keys.SequenceEqual(request.Options)||request.SelectedIndex<0||request.SelectedIndex>=cards.Length)
                throw new InvalidOperationException("Native selection options/order/state differ from simulated preflight.");
            Calls.Add(new{request.Source,request.Context,request.Pile,options=keys,selectedIndex=request.SelectedIndex});
            return Task.FromResult<IEnumerable<CardModel>>([cards[request.SelectedIndex]]);
        }
        public void AssertConsumed(){if(_requests.Count!=0)throw new InvalidOperationException("Native omitted a required explicit selection.");}
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,IReadOnlyList<CardRewardAlternative> alternatives)
            =>throw new NotSupportedException("Combat selector cannot select rewards.");
    }
}
