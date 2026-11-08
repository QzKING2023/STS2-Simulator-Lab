using System.Collections;
using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using SimulatorLab.Core.Mods;

namespace OfflineSearchHarness;

internal static partial class Program
{
    /// <summary>
    /// Executes one bounded real combat fixture after the natural first-combat
    /// Play boundary. All external Mod objects are reflected and reduced to
    /// scalar evidence before they reach the core adapter.
    /// </summary>
    internal static void RunMinionRealCombatSlice(string seed)
    {
        Ready();
        var combat = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("stage 10 requires an active combat");
        var owner = player ?? throw new InvalidOperationException("stage 10 player is missing");
        var context = new ThrowingPlayerChoiceContext();
        var events = new List<MinionCombatEvent>();

        var minionType = MinionNativeModLoop.Loaded["shadowversebydmod"].GetType(MinionStage10Constants.RealMinionType, true)!;
        var minionCmd = MinionNativeModLoop.Loaded["MinionLib"].GetType("MinionLib.Commands.MinionCmd", true)!;
        var addDefinition = minionCmd.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(x => x.Name == "AddMinion" && x.IsGenericMethodDefinition);
        var optionsType = MinionNativeModLoop.Loaded["MinionLib"].GetType("MinionLib.Minion.MinionSummonOptions", true)!;
        var addTask = (Task)addDefinition.MakeGenericMethod(minionType).Invoke(null,
            [context, owner, Activator.CreateInstance(optionsType)!])!;
        Await(addTask, "real Minion summon");
        var minion = owner.PlayerCombatState?.Pets.LastOrDefault(x => x.Monster?.GetType() == minionType)
            ?? throw new InvalidOperationException("real Shadowverse Minion was not registered in player pets");
        var minionModelId = minion.Monster?.Id.ToString() ?? minionType.FullName!;
        var minionCreatureId = "real-minion-1";
        var summonedHp = minion.CurrentHp;
        events.Add(new("minion-real-register", minionCreatureId, minionModelId, null, null, "stage10/native-minion-register"));
        events.Add(new("minion-real-summon", minionCreatureId, minionModelId, null, summonedHp, "stage10/native-minion-summon"));
        Observe("minion-real-register", new { type = minionType.FullName, model = minionModelId, petCount = owner.PlayerCombatState!.Pets.Count });
        Observe("minion-real-summon", new { type = minionType.FullName, model = minionModelId, hp = summonedHp, alive = minion.IsAlive });

        var componentCardType = MinionNativeModLoop.Loaded["shadowversebydmod"].GetType(MinionStage10Constants.RealComponentCardType, true)!;
        var cardTemplate = ModelDb.All.OfType<CardModel>().Single(x => x.GetType() == componentCardType);
        var componentCard = combat.CreateCard(cardTemplate, owner);
        Await(CardPileCmd.Add(componentCard, PileType.Hand), "real component card to hand");
        var component = ReadComponent(componentCard);
        var target = combat.Enemies.FirstOrDefault(x => x.IsAlive)
            ?? throw new InvalidOperationException("stage 10 combat has no live target");
        var targetBefore = target.CurrentHp;
        if (!componentCard.TryManualPlay(target)) throw new InvalidOperationException("real component card was rejected by native combat");
        Await(RunManager.Instance.ActionExecutor.FinishedExecutingActions(), "real component card action");
        var targetAfter = target.CurrentHp;
        var componentApplied = targetAfter < targetBefore && component.Amount > 0;
        events.Add(new("minion-component-real-result", componentCard.Id.ToString(), component.ComponentId,
            targetBefore, targetAfter, "stage10/native-component-result"));
        Observe("minion-component-real-result", new
        {
            cardType = componentCardType.FullName, cardId = componentCard.Id.ToString(),
            componentId = component.ComponentId, amount = component.Amount,
            target = target.Monster?.Id.ToString(), targetBefore, targetAfter,
            componentApplied
        });

        var damageBefore = minion.CurrentHp;
        var damageTask = CreatureCmd.Damage(context, minion, 1m, ValueProp.Unblockable | ValueProp.Unpowered, owner.Creature);
        Await(damageTask, "real Minion damage");
        var damageAfter = minion.CurrentHp;
        events.Add(new("minion-real-damaged", minionCreatureId, minionModelId, damageBefore, damageAfter, "stage10/native-minion-damage"));
        Observe("minion-real-damaged", new { creature = minionCreatureId, hpBefore = damageBefore, hpAfter = damageAfter, alive = minion.IsAlive });

        var deathBefore = minion.CurrentHp;
        var deathTask = CreatureCmd.Damage(context, minion, 999m, ValueProp.Unblockable | ValueProp.Unpowered, owner.Creature);
        Await(deathTask, "real Minion lethal damage");
        var deathAfter = minion.CurrentHp;
        var removedFromCombat = !combat.Creatures.Contains(minion);
        var removedFromPets = !(owner.PlayerCombatState?.Pets.Contains(minion) ?? false);
        events.Add(new("minion-real-death", minionCreatureId, minionModelId, deathBefore, deathAfter, "stage10/native-minion-death"));
        events.Add(new("minion-real-removal", minionCreatureId, minionModelId, deathAfter, deathAfter, "stage10/native-minion-removal"));
        Observe("minion-real-death", new { creature = minionCreatureId, hpBefore = deathBefore, hpAfter = deathAfter, alive = minion.IsAlive });
        Observe("minion-real-removal", new { creature = minionCreatureId, removedFromCombat, removedFromPets, combatCreatureCount = combat.Creatures.Count });

        foreach (var enemy in combat.Enemies.Where(x => x.IsAlive).ToArray())
            Await(CreatureCmd.Kill(enemy, force: true), "real combat cleanup kill");
        Await(CombatManager.Instance.CheckWinCondition(), "real combat win condition");
        Await(RunManager.Instance.ActionExecutor.FinishedExecutingActions(), "real combat cleanup action queue");
        var combatEnded = !CombatManager.Instance.IsInProgress;
        var cleanupConfirmed = combatEnded && removedFromCombat;
        events.Add(new("minion-combat-end-cleanup", "combat", null, null, null, "stage10/native-combat-end-cleanup"));
        Observe("minion-combat-end-cleanup", new
        {
            combatEnded, cleanupConfirmed, removedFromCombat, removedFromPets,
            historyEntries = CombatManager.Instance.History.Entries.Count()
        });
        MinionNativeModLoop.RecordRealCombat(new MinionRealCombatObservation(
            minionType.FullName!, minionModelId, minionCreatureId,
            summonedHp, damageAfter, deathAfter, minion.IsAlive,
            removedFromCombat, removedFromPets,
            componentCardType.FullName!, componentCard.Id.ToString(), component.ComponentId,
            component.Amount, targetBefore, targetAfter, componentApplied,
            combatEnded, cleanupConfirmed, "stage10/native-real-combat"), events);
    }

    private static (string ComponentId, int Amount) ReadComponent(CardModel card)
    {
        var property = card.GetType().GetProperty("Components", BindingFlags.Public | BindingFlags.Instance);
        if (property?.GetValue(card) is not IEnumerable values)
            throw new InvalidOperationException("real component card did not expose MinionLib components");
        var component = values.Cast<object>().SingleOrDefault()
            ?? throw new InvalidOperationException("real component card has no canonical component");
        var id = component.GetType().GetProperty("ComponentId")?.GetValue(component)?.ToString()
            ?? throw new InvalidOperationException("real component id is unavailable");
        var amount = component.GetType().GetProperty("Damage")?.GetValue(component) as int?
            ?? Convert.ToInt32(component.GetType().GetProperty("Damage")?.GetValue(component) ?? 0);
        return (id, amount);
    }
}
