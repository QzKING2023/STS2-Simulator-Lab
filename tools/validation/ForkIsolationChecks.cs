using CombatSolver;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;

namespace OfflineSearchHarness;

internal static class ForkIsolationChecks
{
    public static void Run(CombatState liveCombat)
    {
        var captured = CombatRootSnapshot.Capture(liveCombat);
        var parent = captured.ForkSimulator();
        var baselineShuffle = parent.Rng.ShuffleState;
        var left = parent.Fork();
        var right = parent.Fork();

        if (!left.Rng.ShuffleState.Equals(baselineShuffle) || !right.Rng.ShuffleState.Equals(baselineShuffle))
            throw new InvalidOperationException("Child RNG state differs from the captured parent.");

        _ = left.Rng.Shuffle.NextInt(100);
        if (!right.Rng.ShuffleState.Equals(baselineShuffle) || !parent.Rng.ShuffleState.Equals(baselineShuffle))
            throw new InvalidOperationException("Advancing one child changed a sibling or parent RNG stream.");

        var leftPlayer = left.State.CombatState.Players[0];
        var rightPlayer = right.State.CombatState.Players[0];
        var leftHand = left.State.GetPlayerCombatState(leftPlayer).Hand;
        var rightHand = right.State.GetPlayerCombatState(rightPlayer).Hand;
        int originalHandCount = rightHand.Cards.Count;

        var leftCard = leftHand.Cards[0];
        var cardVariable = leftCard.Preview.DynamicVars.Keys.First();
        decimal siblingVariableBefore = rightHand.Cards[0].Preview.DynamicVars[cardVariable].BaseValue;
        leftCard.MutablePreview.DynamicVars[cardVariable].UpgradeValueBy(1);
        if (rightHand.Cards[0].Preview.DynamicVars[cardVariable].BaseValue != siblingVariableBefore)
            throw new InvalidOperationException("Changing one child's mutable card preview changed its sibling.");

        var leftPlayerCombatState = left.State.GetCreature(leftPlayer.Creature);
        var rightPlayerCombatState = right.State.GetCreature(rightPlayer.Creature);
        int siblingBlockBefore = rightPlayerCombatState.Block;
        leftPlayerCombatState.GainBlock(3);
        if (leftPlayerCombatState.Block != siblingBlockBefore + 3 || rightPlayerCombatState.Block != siblingBlockBefore)
            throw new InvalidOperationException("Changing one child's block state changed its sibling.");

        leftHand.Clear();
        if (leftHand.Cards.Count != 0 || rightHand.Cards.Count != originalHandCount || originalHandCount == 0)
            throw new InvalidOperationException("Clearing one child hand changed the sibling or produced an invalid fixture.");

        left.History.RecordRisk(Enum.GetValues<PredictionRiskReason>().First());
        if (!left.History.HasRisk || right.History.HasRisk || parent.History.HasRisk)
            throw new InvalidOperationException("Appending history to one child changed a sibling or parent history.");

        left.LoseCombat();
        if (!left.CheckWinCondition(1) || left.TerminalStamp is null)
            throw new InvalidOperationException("Mutated branch did not reach its simulated terminal state.");
        if (!right.IsInProgress || right.TerminalStamp is not null || !parent.IsInProgress || parent.TerminalStamp is not null)
            throw new InvalidOperationException("Terminal state leaked from the mutated child to its parent or sibling.");

        Console.WriteLine("FORK_ISOLATION Passed block=True card_preview=True hand_pile=True sibling_rng=True parent_rng=True history=True terminal_state=True scope=offline_generated_combat");
    }
}
