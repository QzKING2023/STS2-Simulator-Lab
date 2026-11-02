namespace SimulatorLab.Core;

/// <summary>
/// The reviewed M1 host probe exposes only a token-guarded manual card-play call.
/// This DTO mirrors that narrow call boundary; it does not execute it.
/// </summary>
public sealed record HostProbeManualPlayCallV1(
    int HandIndex,
    int EnemyIndex,
    string ExpectedHostToken,
    string SourceRootDigest);

public sealed record HostProbeBridgeResultV1(
    bool IsSupported,
    HostProbeManualPlayCallV1? Call,
    List<string> Errors);

public static class HostProbeActionBridge
{
    /// <summary>
    /// Converts a supported Lab play-card action into the existing M1.HostProbe
    /// PlayHandCard(index, enemy, expectedToken) arguments. The caller must take
    /// expectedHostToken from that same live host snapshot. The game-side token
    /// guard remains authoritative and rejects stale calls.
    /// </summary>
    public static HostProbeBridgeResultV1 TryCreateManualPlayCall(
        CombatRootV1 root,
        CombatActionV1 action,
        string expectedHostToken)
    {
        List<string> errors = [];
        var rootCheck = CombatCaptureValidator.ValidateExactReady(root);
        if (!rootCheck.IsExactReady)
            errors.AddRange(rootCheck.Errors.Select(x => $"Root: {x}"));
        if (root.Phase != CombatPhase.PlayerAction)
            errors.Add("HostProbe manual play is supported only at a player-action boundary.");
        if (action.Kind != CombatActionKind.PlayCard)
            errors.Add($"HostProbe manual play does not support action kind {action.Kind}.");
        if (string.IsNullOrWhiteSpace(expectedHostToken))
            errors.Add("A non-empty token from the current HostProbe snapshot is required.");

        var cardId = action.CardInstanceId;
        var hand = root.Piles.FirstOrDefault(x => x.Kind == PileKind.Hand);
        var handIndex = cardId is null || hand is null
            ? -1
            : hand.CardInstanceIds.FindIndex(x => StringComparer.Ordinal.Equals(x, cardId));
        if (handIndex < 0)
            errors.Add("The selected card instance is not present in the captured hand order.");

        var card = cardId is null ? null : root.Cards.FirstOrDefault(x => StringComparer.Ordinal.Equals(x.InstanceId, cardId));
        if (card is not null && card.TargetType == "AnyEnemy")
        {
            if (action.TargetCombatantId is null)
                errors.Add("A target is required for an AnyEnemy card.");
        }
        else if (card is not null && card.TargetType is "Self" or "None")
        {
            if (action.TargetCombatantId is not null)
                errors.Add("A target was supplied for a targetless or self-targeting card.");
        }
        else if (card is not null)
        {
            errors.Add($"HostProbe bridge does not recognize target type {card.TargetType}.");
        }

        int enemyIndex = -1;
        if (action.TargetCombatantId is not null)
        {
            // The root adapter must preserve the host's enemy list order in the
            // Combatants list. The host probe indexes Creature.CombatState.Enemies.
            var enemies = root.Combatants.Where(x => !x.IsPlayer).ToList();
            enemyIndex = enemies.FindIndex(x => StringComparer.Ordinal.Equals(x.CombatantId, action.TargetCombatantId));
            if (enemyIndex < 0)
                errors.Add("The target is not present in the captured enemy order.");
            else if (!enemies[enemyIndex].IsAlive)
                errors.Add("The target enemy is not alive in the captured root.");
            if (card?.TargetType != "AnyEnemy")
                errors.Add("Only AnyEnemy cards may carry an enemy target through this bridge.");
        }

        if (errors.Count != 0)
            return new(false, null, errors);

        return new(true, new HostProbeManualPlayCallV1(
            handIndex,
            enemyIndex,
            expectedHostToken,
            StateDigest.Compute(root)), []);
    }
}
