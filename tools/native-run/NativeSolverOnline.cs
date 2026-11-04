using SimulatorLab.Core;
#if SOLVER_GATE
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;
#endif
namespace OfflineSearchHarness;
internal static class NativeSolverOnline
{
    internal static bool SmartScenario(string s)=>s is "solver-smart-block" or "solver-smart-energy";
    internal static bool IsScenario(string s)=>s is "solver-online-choice" or "solver-online-potion"||SmartScenario(s);
}
internal static partial class Program
{
#if !SOLVER_GATE
    static void SolverOnlineFixture()=>throw new NotSupportedException("Separate solver profile required");
#else
    static void SolverOnlineFixture()
    {
        var combat=CombatManager.Instance.DebugOnlyGetState()!;var pcs=player!.PlayerCombatState!;
        bool energy=scenario=="solver-smart-energy";
        Observe("fixture-setup-declared",new{name=scenario,changes=energy?"Upstream Smart high-loss derived input: remove combat piles/relics/powers; native enemy HP1/Strength20; Bloodletting HpLoss12 plus Strike; energy0; native EnergyPotion procurement. Explicit API fixture, not natural run.":"native hand card creation/upgrade or BlockPotion procurement; unchanged permanent deck; Smart solver policy; no forced action or score change"});
        Action("FixtureSetup",new{name=scenario},()=>{
            if(energy){
                Await(CardPileCmd.RemoveFromCombat([..pcs.Hand.Cards,..pcs.DrawPile.Cards,..pcs.DiscardPile.Cards,..pcs.ExhaustPile.Cards],skipVisuals:true),"Upstream fixture clear combat piles");
                foreach(var relic in player.Relics.ToArray())Await(RelicCmd.Remove(relic),"Upstream fixture remove relic");
                foreach(var power in combat.Creatures.SelectMany(c=>c.Powers).ToArray())Await(PowerCmd.Remove(power),"Upstream fixture remove power");
                Await(CreatureCmd.SetCurrentHp(combat.Enemies.Single(),1),"Upstream fixture enemy HP1");
                Await(PowerCmd.Apply<StrengthPower>(new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(),combat.Enemies.Single(),20,combat.Enemies.Single(),null),"Upstream fixture enemy Strength20");
                CardModel Add(string type){var c=combat.CreateCard(ModelDb.All.OfType<CardModel>().Single(x=>x.GetType().Name==type),player);Await(CardPileCmd.Add(c,PileType.Hand),"Upstream fixture native card");return c;}
                var blood=Add("Bloodletting");blood.DynamicVars["HpLoss"].BaseValue=12;_=Add("StrikeIronclad");pcs.LoseEnergy(pcs.Energy);
                Await(PotionCmd.TryToProcure(ModelDb.Potion<EnergyPotion>().ToMutable(),player),"Upstream-derived native EnergyPotion procurement");return;
            }
            foreach(var c in pcs.Hand.Cards.ToArray())Await(CardPileCmd.Add(c,PileType.Exhaust),"Online fixture hand to Exhaust");
            if(scenario=="solver-online-choice"){
                CardModel Add(string type){var c=combat.CreateCard(ModelDb.All.OfType<CardModel>().Single(x=>x.GetType().Name==type),player);Await(CardPileCmd.Add(c,PileType.Hand),"Online fixture native generated card");return c;}
                var grit=Add("TrueGrit");CardCmd.Upgrade(grit);_=Add("Wound");_=Add("Wound");
            }else Await(PotionCmd.TryToProcure(ModelDb.Potion<BlockPotion>().ToMutable(),player),"Online fixture native BlockPotion procurement");
        });
        bool choice=false,potion=false;int completed=0;
        for(int query=1;query<=9;query++){
            Ready();var ticket=SolverQuery(combat,query);
            if(scenario=="solver-online-choice"&&choice||scenario!="solver-online-choice"&&potion){
                Observe("solver-online-complete",new{nativeActionsExecuted=completed,queries=query,childChoice=choice,potion,wholeBattle=false,information="Oracle",policy="unmodified first solver action; solve-execute-compare-resolve",origin="api-fixture"});return;
            }
            if(completed>=8)throw new TimeoutException("Online solver capability not reached in eight actions");
            var executed=ExecuteSolverAction(ticket,combat,query,extended:true);completed++;choice|=executed.ChildChoice;potion|=executed.Potion;
        }
        throw new TimeoutException("Online solver query budget reached");
    }
#endif
}
