#if SOLVER_GATE
using CombatSolver;
using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
namespace OfflineSearchHarness;

// Observation only: original CardSelectCmd and the original OnPlay perform all effects.
internal sealed class NativeHandMutation
{
    readonly Player? owner;
    public bool complete {get;private set;}
    public object[] before {get;}
    public JsonElement choiceState {get;}
    public object[] after {get;private set;}=[];
    internal NativeHandMutation(CardModel[] candidates,int[] indices){owner=candidates.FirstOrDefault()?.Owner;before=Snapshot();choiceState=Program.State();}
    object[] Snapshot()=>owner?.PlayerCombatState==null?[]:
        new[]{PileType.Hand,PileType.Draw,PileType.Discard,PileType.Exhaust,PileType.Play}.SelectMany(p=>p.GetPile(owner).Cards.Select(c=>(object)new{
            id=NativeIdentity.Get(c),model=c.Id.ToString(),upgrade=c.CurrentUpgradeLevel,pile=p.ToString(),
            retain=c.Keywords.Contains(CardKeyword.Retain),clone=c.IsClone,key=CardChoiceSupport.ChoiceCardKey(c)})).ToArray();
    internal void Finish(){if(complete)throw new NativeDataException("Duplicate mutation completion");after=Snapshot();complete=true;}
}
#endif
