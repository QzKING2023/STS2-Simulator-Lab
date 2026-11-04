using System.Globalization;
using System.Text.Json;
using CombatSolver;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using SimulatorLab.Core;

namespace OfflineSearchHarness;

/// <summary>
/// Maps both the live v0.111.0 game root and CombatSolver's predicted root into the Lab's
/// versioned CombatRootV1. Card identity is tied to the native CardModel reference captured at
/// the opening root. Independently created native/shadow cards use separate, stable ordered
/// first-observation ordinals in each world; they do not share object references.
/// The full reviewed continuation serialization is kept as a strict state
/// extension so dynamic/private vanilla state remains part of the comparison.
/// </summary>
internal sealed class VanillaCombatRootAdapter
{
    private readonly record struct CardView(CardModel Identity, CardModel State);

    private readonly CombatCaseHeaderV1 _header;
    private readonly Dictionary<CardModel, string> _cardIds = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Creature, string> _creatureIds = new(ReferenceEqualityComparer.Instance);
    private int _nextCardId;
    private readonly Dictionary<CardModel, string> _nativeGeneratedIds = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<CardModel, string> _predictedGeneratedIds = new(ReferenceEqualityComparer.Instance);

    public VanillaCombatRootAdapter(CombatState initial, string scenarioId)
    {
        Player player = LocalContext.GetMe(initial)
            ?? throw new InvalidOperationException("Cannot initialize the Lab root adapter without the local player.");
        _header = new CombatCaseHeaderV1
        {
            CharacterModelId = player.Character.Id.Entry,
            EncounterModelId = initial.Encounter?.Id.Entry ?? "UNKNOWN_ENCOUNTER",
            RunMode = "single-player",
            Ascension = initial.RunState.AscensionLevel,
            ActIndex = initial.RunState.CurrentActIndex,
            InformationMode = InformationMode.OracleReplay,
        };
        ScenarioId = scenarioId;

        foreach (Creature creature in initial.Creatures)
            _ = CreatureId(creature);
        foreach (CardModel card in player.PlayerCombatState!.AllCards)
            _cardIds.Add(card, $"card:{_nextCardId++:D3}:{card.Id.Entry}");
    }

    public string ScenarioId { get; }
    public string CombatId { get; private set; } = "";

    internal string ObservedCardId(CardModel card, bool predicted) => CardId(card, predicted);

    public PredictedCard ResolvePredictedHandCard(CardModel nativeCard, SimPlayerCombatState predicted)
    {
        // Called only after the complete ordered roots/continuations matched. Resolve an
        // independently generated object by its already-observed identity, never by model alone.
        if (!_cardIds.TryGetValue(nativeCard, out string? id) && !_nativeGeneratedIds.TryGetValue(nativeCard, out id))
            throw new PredictionUnsupportedException("Native action card has no observed root identity.");
        var matches = predicted.Hand.Cards.Where(card =>
            (_cardIds.TryGetValue(card.Original, out string? initial) && initial == id)
            || (_predictedGeneratedIds.TryGetValue(card.Original, out string? generated) && generated == id)).ToArray();
        if (matches.Length != 1 || matches[0].Preview.Id != nativeCard.Id
            || !ReferenceEquals(matches[0].Preview.Owner, nativeCard.Owner))
            throw new PredictionUnsupportedException("Generated action card has no unique matching observed hand identity.");
        return matches[0];
    }

    public CombatRootV1 CaptureLive(CombatState state, bool terminalVictory = false, bool terminalDefeat = false)
    {
        Player player = LocalContext.GetMe(state)
            ?? throw new InvalidOperationException("Cannot capture the live root without the local player.");
        PlayerCombatState playerState = player.PlayerCombatState
            ?? throw new InvalidOperationException("Cannot capture the live root without PlayerCombatState.");
        string stateText = ContinuationStamp.CaptureLive(state).StateText;
        CombatPhase phase = ResolvePhase(state.CurrentSide, playerState.Phase,
            player.Creature.CurrentHp <= 0, terminalVictory, terminalDefeat);
        Dictionary<PileKind, IReadOnlyList<CardView>> cardPiles = new()
        {
            [PileKind.Draw] = playerState.DrawPile.Cards.Select(card => new CardView(card, card)).ToArray(),
            [PileKind.Hand] = playerState.Hand.Cards.Select(card => new CardView(card, card)).ToArray(),
            [PileKind.Discard] = playerState.DiscardPile.Cards.Select(card => new CardView(card, card)).ToArray(),
            [PileKind.Exhaust] = playerState.ExhaustPile.Cards.Select(card => new CardView(card, card)).ToArray(),
            [PileKind.Play] = playerState.PlayPile.Cards.Select(card => new CardView(card, card)).ToArray(),
        };
        return BuildRoot(
            ScenarioId,
            state.CurrentSide,
            phase,
            playerState.TurnNumber,
            player,
            state.Creatures,
            creature => creature.CurrentHp,
            creature => creature.MaxHp,
            creature => creature.Block,
            creature => creature.Powers,
            creature => creature.Monster?.NextMove?.Id,
            player.Relics,
            playerState.Energy,
            playerState.Stars,
            playerState.OrbQueue.Orbs.Select(orb => orb.Id.Entry).ToArray(),
            playerState.OrbQueue.Capacity,
            cardPiles,
            rng: CaptureLiveRng(state.RunState.Rng),
            stateText);
    }

    public CombatRootV1 CapturePredicted(
        CombatState liveState,
        Player player,
        CombatPredictionSimulator simulator,
        int turn,
        IntentForecast forecast,
        int startTurnNumber,
        bool terminalVictory = false,
        bool terminalDefeat = false)
    {
        SimPlayerCombatState playerState = simulator.State.GetPlayerCombatState(player);
        SimulatedCombatState simulatedCombat = (SimulatedCombatState)simulator.State.CombatState;
        string stateText = ContinuationStamp.CapturePredicted(
            player, simulator, turn, forecast, startTurnNumber).StateText;
        CombatPhase phase = ResolvePhase(
            simulatedCombat.CurrentSide,
            playerState.Phase,
            simulator.State.GetCreature(player.Creature).CurrentHp <= 0,
            terminalVictory || simulator.TerminalStamp?.Outcome == CombatTerminalOutcome.Victory,
            terminalDefeat || simulator.TerminalStamp?.Outcome == CombatTerminalOutcome.Defeat);

        Dictionary<PileKind, IReadOnlyList<CardView>> cardPiles = new()
        {
            [PileKind.Draw] = playerState.DrawPile.Cards.Select(card => new CardView(card.Original, card.Preview)).ToArray(),
            [PileKind.Hand] = playerState.Hand.Cards.Select(card => new CardView(card.Original, card.Preview)).ToArray(),
            [PileKind.Discard] = playerState.DiscardPile.Cards.Select(card => new CardView(card.Original, card.Preview)).ToArray(),
            [PileKind.Exhaust] = playerState.ExhaustPile.Cards.Select(card => new CardView(card.Original, card.Preview)).ToArray(),
            [PileKind.Play] = playerState.PlayPile.Cards.Select(card => new CardView(card.Original, card.Preview)).ToArray(),
        };
        return BuildRoot(
            ScenarioId,
            simulatedCombat.CurrentSide,
            phase,
            turn,
            player,
            simulator.State.Creatures,
            creature => simulator.State.GetCreature(creature).CurrentHp,
            creature => simulator.State.GetCreature(creature).MaxHp,
            creature => simulator.State.GetCreature(creature).Block,
            creature => simulatedCombat.EffectivePowers().Where(power => ReferenceEquals(power.Owner, creature)),
            creature => creature.Monster == null ? null : simulatedCombat.GetPredictedMoveId(creature),
            simulatedCombat.RelicsOf(player),
            playerState.Energy,
            playerState.Stars,
            playerState.OrbQueue.Orbs.Select(orb => orb.Id.Entry).ToArray(),
            playerState.OrbQueue.Capacity,
            cardPiles,
            rng: CapturePredictedRng(simulator.Rng),
            stateText, predicted: true);
    }

    private CombatRootV1 BuildRoot(
        string combatId,
        CombatSide side,
        CombatPhase phase,
        int turn,
        Player player,
        IReadOnlyList<Creature> creatures,
        Func<Creature, int> currentHp,
        Func<Creature, int> maxHp,
        Func<Creature, int> block,
        Func<Creature, IEnumerable<PowerModel>> powers,
        Func<Creature, string?> intent,
        IEnumerable<RelicModel> relics,
        int energy,
        int stars,
        IReadOnlyList<string> orbs,
        int orbCapacity,
        IReadOnlyDictionary<PileKind, IReadOnlyList<CardView>> cardPiles,
        List<RandomStreamCheckpointV1> rng,
        string stateText, bool predicted = false)
    {
        CombatId = string.IsNullOrEmpty(CombatId) ? combatId : CombatId;
        if (!StringComparer.Ordinal.Equals(CombatId, combatId))
            throw new InvalidOperationException($"Combat ID changed during a trace: {CombatId} -> {combatId}.");

        PileKind[] pileKinds = [PileKind.Draw, PileKind.Hand, PileKind.Discard, PileKind.Exhaust, PileKind.Play];
        List<CardPileStateV1> piles = pileKinds.Select(kind => new CardPileStateV1
        {
            Kind = kind,
            CardInstanceIds = cardPiles[kind].Select(card => CardId(card.Identity, predicted)).ToList(),
        }).ToList();
        Dictionary<string, CardModel> cardsById = new(StringComparer.Ordinal);
        foreach (CardView card in cardPiles.Values.SelectMany(cards => cards))
            cardsById.TryAdd(CardId(card.Identity, predicted), card.State);
        List<CardInstanceStateV1> cards = cardsById.Select(pair => CardState(pair.Key, pair.Value)).ToList();

        string playerId = CreatureId(player.Creature);
        List<CombatantStateV1> combatants = creatures.Select(creature => new CombatantStateV1
        {
            CombatantId = CreatureId(creature),
            ModelId = creature.Player?.Character.Id.Entry ?? creature.Monster?.Id.Entry ?? creature.Name,
            IsPlayer = ReferenceEquals(creature, player.Creature),
            IsAlive = currentHp(creature) > 0,
            CurrentHp = currentHp(creature),
            MaxHp = maxHp(creature),
            Block = block(creature),
            IntentId = creature.Monster == null ? null : intent(creature),
            Resources = ReferenceEquals(creature, player.Creature)
                ? new CombatantResourcesV1
                {
                    Energy = energy,
                    Stars = stars,
                    Orbs = orbs.ToList(),
                    Counters = new SortedDictionary<string, int>(StringComparer.Ordinal)
                    {
                        ["orbCapacity"] = orbCapacity,
                    },
                }
                : null,
            Powers = powers(creature)
                .Where(power => power.Amount != 0)
                .Select(power => new PowerStateV1(power.Id.Entry, power.Amount))
                .ToList(),
        }).ToList();

        string currentActorId = phase is CombatPhase.Victory or CombatPhase.Defeat
            ? playerId
            : side == CombatSide.Player
                ? playerId
                : creatures.FirstOrDefault(creature => creature.Monster != null && currentHp(creature) > 0) is { } actor
                    ? CreatureId(actor)
                    : playerId;

        List<RelicStateV1> relicStates = relics.Select(relic => new RelicStateV1(
            relic.Id.Entry,
            relic.IsMelted,
            CaptureDynamicVars(relic.DynamicVars))).ToList();

        JsonElement continuationPayload = JsonSerializer.SerializeToElement(
            new { serializer = "CombatSolver.ContinuationStamp/v0.50.1", stateText },
            ProtocolJson.Options);
        ModelExtensionStateV1 continuationExtension = new()
        {
            ModId = "sts2",
            TypeId = "combat-solver-continuation/v1",
            SchemaVersion = 1,
            Payload = continuationPayload,
            HasForkAdapter = true,
        };

        return new CombatRootV1
        {
            Header = _header,
            CombatId = CombatId,
            Phase = phase,
            TurnNumber = turn,
            CurrentActorId = currentActorId,
            IsStableDecisionBoundary = true,
            ActionsInFlight = 0,
            Combatants = combatants,
            Cards = cards,
            Piles = piles,
            RandomStreams = rng,
            Relics = relicStates,
            Extensions = [continuationExtension],
        };
    }

    private CardInstanceStateV1 CardState(string instanceId, CardModel card)
        => new()
        {
            InstanceId = instanceId,
            ModelId = card.Id.Entry,
            UpgradeLevel = card.CurrentUpgradeLevel,
            EnergyCost = Math.Max(0, card.EnergyCost.GetWithModifiers(CostModifiers.Local)),
            TargetType = card.TargetType.ToString(),
            DynamicVars = CaptureDynamicVars(card.DynamicVars)
                .Where(pair => decimal.Truncate(pair.Value) == pair.Value
                    && pair.Value is >= int.MinValue and <= int.MaxValue)
                .Aggregate(new SortedDictionary<string, int>(StringComparer.Ordinal),
                    (result, pair) =>
                    {
                        result[pair.Key] = (int)pair.Value;
                        return result;
                    }),
        };

    private SortedDictionary<string, decimal> CaptureDynamicVars(
        IEnumerable<KeyValuePair<string, MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar>> dynamicVars)
    {
        SortedDictionary<string, decimal> result = new(StringComparer.Ordinal);
        foreach (var pair in dynamicVars)
        {
            if (pair.Value is MegaCrit.Sts2.Core.Localization.DynamicVars.StringVar)
                continue;
            result[pair.Key] = Convert.ToDecimal(pair.Value.BaseValue, CultureInfo.InvariantCulture);
        }
        return result;
    }

    private string CardId(CardModel card, bool predicted)
    {
        if (_cardIds.TryGetValue(card, out string? id))
            return id;
        var generated = predicted ? _predictedGeneratedIds : _nativeGeneratedIds;
        if (generated.TryGetValue(card, out id)) return id;
        id = $"card:{_nextCardId + generated.Count:D3}:{card.Id.Entry}";
        generated.Add(card, id);
        return id;
    }

    private string CreatureId(Creature creature)
    {
        if (_creatureIds.TryGetValue(creature, out string? id))
            return id;
        id = ReferenceEquals(creature.Player?.Creature, creature)
            ? "player:local"
            : creature.CombatId is { } combatId
                ? $"creature:{combatId}"
                : $"creature:unassigned:{creature.Monster?.Id.Entry ?? creature.Name}";
        if (_creatureIds.Values.Contains(id, StringComparer.Ordinal))
            id += $":{_creatureIds.Count}";
        _creatureIds.Add(creature, id);
        return id;
    }

    private static CombatPhase ResolvePhase(
        CombatSide side,
        PlayerTurnPhase playerPhase,
        bool playerDead,
        bool terminalVictory,
        bool terminalDefeat)
    {
        if (terminalVictory)
            return CombatPhase.Victory;
        if (terminalDefeat || playerDead)
            return CombatPhase.Defeat;
        if (side == CombatSide.Enemy)
            return CombatPhase.EnemyTurn;
        return playerPhase == PlayerTurnPhase.Play ? CombatPhase.PlayerAction : CombatPhase.PlayerTurnStart;
    }

    private static List<RandomStreamCheckpointV1> CaptureLiveRng(RunRngSet rng) =>
    [
        Rng(CombatRngStream.Shuffle, rng.Shuffle.CaptureState()),
        Rng(CombatRngStream.CardGeneration, rng.CombatCardGeneration.CaptureState()),
        Rng(CombatRngStream.PotionGeneration, rng.CombatPotionGeneration.CaptureState()),
        Rng(CombatRngStream.CardSelection, rng.CombatCardSelection.CaptureState()),
        Rng(CombatRngStream.EnergyCosts, rng.CombatEnergyCosts.CaptureState()),
        Rng(CombatRngStream.Targets, rng.CombatTargets.CaptureState()),
        Rng(CombatRngStream.OrbGeneration, rng.CombatOrbGeneration.CaptureState()),
        Rng(CombatRngStream.MonsterAi, rng.MonsterAi.CaptureState()),
        Rng(CombatRngStream.Niche, rng.Niche.CaptureState()),
    ];

    private static List<RandomStreamCheckpointV1> CapturePredictedRng(CombatPredictionRngSet rng) =>
    [
        Rng(CombatRngStream.Shuffle, rng.ShuffleState),
        Rng(CombatRngStream.CardGeneration, rng.CombatCardGenerationState),
        Rng(CombatRngStream.PotionGeneration, rng.CombatPotionGenerationState),
        Rng(CombatRngStream.CardSelection, rng.CombatCardSelectionState),
        Rng(CombatRngStream.EnergyCosts, rng.CombatEnergyCostsState),
        Rng(CombatRngStream.Targets, rng.CombatTargetsState),
        Rng(CombatRngStream.OrbGeneration, rng.CombatOrbGenerationState),
        Rng(CombatRngStream.MonsterAi, rng.MonsterAiState),
        Rng(CombatRngStream.Niche, rng.NicheState),
    ];

    private static RandomStreamCheckpointV1 Rng(CombatRngStream stream, PredictionRngState state)
        => new(stream, "MegaRandom/v0.111.0", StateHex(state), state.Counter);

    private static string StateHex(PredictionRngState state)
        => string.Concat(
            state.State0.ToString("x16", CultureInfo.InvariantCulture),
            state.State1.ToString("x16", CultureInfo.InvariantCulture),
            state.State2.ToString("x16", CultureInfo.InvariantCulture),
            state.State3.ToString("x16", CultureInfo.InvariantCulture));
}
