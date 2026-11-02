using System.Text.Json;

namespace SimulatorLab.Core;

public static class GameBuildPin
{
    public const string Version = "v0.111.0";
    public const string Commit = "41cef1ea";
    public const long MainAssemblyHash = 222455745;
}

public enum InformationMode { OracleReplay, PlayerVisible }
public enum CombatPhase { Setup, PlayerTurnStart, PlayerAction, EnemyTurn, Victory, Defeat }
public enum PileKind { Draw, Hand, Discard, Exhaust, Play }
public enum CombatRngStream { Shuffle, CardGeneration, PotionGeneration, CardSelection, EnergyCosts, Targets, OrbGeneration, MonsterAi, Niche }
public enum CombatActionKind { PlayCard, UsePotion, SelectCards, EndTurn }
[Flags]
public enum GapImpact { None = 0, Informational = 1, ActionLegality = 2, CombatOutcome = 4, ObservableState = 8, Scoring = 16 }

public sealed record ModVersionV1(string ModId, string Version);

public sealed record CombatCaseHeaderV1
{
    public string Schema { get; init; } = "combat-case/v1";
    public string GameVersion { get; init; } = GameBuildPin.Version;
    public string GameCommit { get; init; } = GameBuildPin.Commit;
    public long MainAssemblyHash { get; init; } = GameBuildPin.MainAssemblyHash;
    public string CharacterModelId { get; init; } = "";
    public string EncounterModelId { get; init; } = "";
    public string RunMode { get; init; } = "single-player";
    public int Ascension { get; init; }
    public int ActIndex { get; init; }
    public InformationMode InformationMode { get; init; } = InformationMode.OracleReplay;
    public List<ModVersionV1> Mods { get; init; } = [];
}

public sealed record CombatantResourcesV1
{
    public int Energy { get; init; }
    public int Stars { get; init; }
    public List<string> Orbs { get; init; } = [];
    public SortedDictionary<string, int> Counters { get; init; } = new(StringComparer.Ordinal);
}

public sealed record PowerStateV1(string ModelId, int Amount, string? InstanceId = null);
public sealed record RelicStateV1(string RelicId, bool IsMelted, SortedDictionary<string, decimal> DynamicVars);

public sealed record CombatantStateV1
{
    public string CombatantId { get; init; } = "";
    public string ModelId { get; init; } = "";
    public bool IsPlayer { get; init; }
    public bool IsAlive { get; init; } = true;
    public int CurrentHp { get; init; }
    public int MaxHp { get; init; }
    public int Block { get; init; }
    public string? IntentId { get; init; }
    public int? IntentDamage { get; init; }
    public int? IntentHitCount { get; init; }
    public CombatantResourcesV1? Resources { get; init; }
    public List<PowerStateV1> Powers { get; init; } = [];
}

public sealed record CardInstanceStateV1
{
    public string InstanceId { get; init; } = "";
    public string ModelId { get; init; } = "";
    public int UpgradeLevel { get; init; }
    public int EnergyCost { get; init; }
    public string TargetType { get; init; } = "";
    public SortedDictionary<string, int> DynamicVars { get; init; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, int> Modifiers { get; init; } = new(StringComparer.Ordinal);
}

public sealed record CardPileStateV1
{
    public PileKind Kind { get; init; }
    // Order is part of exact state. Never sort this list when hashing or forking.
    public List<string> CardInstanceIds { get; init; } = [];
}

public sealed record RandomStreamCheckpointV1(CombatRngStream Stream, string Algorithm, string StateHex, long DrawCount);
public sealed record CombatEventV1(long Sequence, string Kind, string SourceId, string? TargetId, SortedDictionary<string, long> Values);

public sealed record ModelExtensionStateV1
{
    public string ModId { get; init; } = "";
    public string TypeId { get; init; } = "";
    public int SchemaVersion { get; init; }
    public JsonElement Payload { get; init; }
    public bool HasForkAdapter { get; init; }
}

public sealed record PredictionGapV1
{
    public string EffectId { get; init; } = "";
    public string SourceModelId { get; init; } = "";
    public string Reason { get; init; } = "";
    public GapImpact Impact { get; init; }
    public bool IsModeled { get; init; }
}

public sealed record CombatRootV1
{
    public string Schema { get; init; } = "combat-root/v1";
    public CombatCaseHeaderV1 Header { get; init; } = new();
    public string CombatId { get; init; } = "";
    public CombatPhase Phase { get; init; }
    public int TurnNumber { get; init; }
    public string CurrentActorId { get; init; } = "";
    public bool IsStableDecisionBoundary { get; init; }
    public int ActionsInFlight { get; init; }
    public string? PendingChoiceId { get; init; }
    public List<CombatantStateV1> Combatants { get; init; } = [];
    public List<CardInstanceStateV1> Cards { get; init; } = [];
    public List<CardPileStateV1> Piles { get; init; } = [];
    public List<RandomStreamCheckpointV1> RandomStreams { get; init; } = [];
    public List<CombatEventV1> History { get; init; } = [];
    public List<RelicStateV1> Relics { get; init; } = [];
    public List<ModelExtensionStateV1> Extensions { get; init; } = [];
    public List<PredictionGapV1> Gaps { get; init; } = [];

    public CombatRootV1 Fork()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(this, ProtocolJson.Options);
        return JsonSerializer.Deserialize<CombatRootV1>(bytes, ProtocolJson.Options)
            ?? throw new InvalidOperationException("Could not fork combat root.");
    }
}

public sealed record CombatActionV1
{
    public CombatActionKind Kind { get; init; }
    public string? CardInstanceId { get; init; }
    public int? CardHandIndex { get; init; }
    public string? PotionInstanceId { get; init; }
    public string? TargetCombatantId { get; init; }
    public List<string> SelectedCardInstanceIds { get; init; } = [];
    public string? ChoiceId { get; init; }
}

public sealed record CombatTraceStepV1(string BeforeStateDigest, CombatActionV1 Action, string AfterStateDigest, List<PredictionGapV1> Gaps);

public sealed record CombatTraceV1
{
    public string Schema { get; init; } = "combat-trace/v1";
    public CombatCaseHeaderV1 Header { get; init; } = new();
    public string RootStateDigest { get; init; } = "";
    public List<CombatTraceStepV1> Steps { get; init; } = [];
}

public sealed record CombatSnapshotV1
{
    public string CombatId { get; init; } = "";
    public int TurnNumber { get; init; }
    public CombatPhase Phase { get; init; }
    public bool IsTerminal { get; init; }
    public string StateDigest { get; init; } = "";
    public List<CombatantStateV1> Combatants { get; init; } = [];
    public List<CardPileStateV1> Piles { get; init; } = [];
    public List<PredictionGapV1> Gaps { get; init; } = [];
}

public sealed record CaptureValidationV1(bool IsExactReady, List<string> Errors);

public static class CombatCaptureValidator
{
    private const GapImpact BlockingImpact = GapImpact.ActionLegality | GapImpact.CombatOutcome | GapImpact.ObservableState;

    public static CaptureValidationV1 ValidateExactReady(CombatRootV1 root)
    {
        List<string> errors = [];
        if (root.Schema != "combat-root/v1") errors.Add("Unsupported root schema.");
        if (root.Header.Schema != "combat-case/v1") errors.Add("Unsupported case schema.");
        if (root.Header.GameVersion != GameBuildPin.Version || root.Header.GameCommit != GameBuildPin.Commit || root.Header.MainAssemblyHash != GameBuildPin.MainAssemblyHash)
            errors.Add($"Game build mismatch; expected {GameBuildPin.Version}/{GameBuildPin.Commit}/{GameBuildPin.MainAssemblyHash}.");
        if (string.IsNullOrWhiteSpace(root.Header.CharacterModelId) || string.IsNullOrWhiteSpace(root.Header.EncounterModelId))
            errors.Add("Character or encounter model ID is missing.");
        if (root.Header.InformationMode != InformationMode.OracleReplay) errors.Add("Exact replay requires OracleReplay mode.");
        if (!root.IsStableDecisionBoundary || root.ActionsInFlight != 0 || root.PendingChoiceId != null)
            errors.Add("Root is not a stable decision boundary (in-flight action or pending choice).");
        if (root.TurnNumber < 1) errors.Add("Turn number must be positive.");
        if (root.CombatId.Length == 0) errors.Add("Combat ID is missing.");

        var combatantIds = root.Combatants.Select(x => x.CombatantId).ToArray();
        if (combatantIds.Any(string.IsNullOrWhiteSpace) || combatantIds.Distinct(StringComparer.Ordinal).Count() != combatantIds.Length)
            errors.Add("Combatant IDs are missing or duplicated.");
        if (root.Combatants.Count(x => x.IsPlayer) != 1) errors.Add("Exactly one player combatant is required for this milestone.");
        if (root.Combatants.Any(x => x.ModelId.Length == 0 || x.CurrentHp < 0 || x.MaxHp < x.CurrentHp || x.Block < 0))
            errors.Add("Combatant model or health/block values are invalid.");
        if (root.Phase is not (CombatPhase.Victory or CombatPhase.Defeat) && !combatantIds.Contains(root.CurrentActorId, StringComparer.Ordinal))
            errors.Add("Current actor does not match a captured combatant.");

        var cardIds = root.Cards.Select(x => x.InstanceId).ToArray();
        if (cardIds.Any(string.IsNullOrWhiteSpace) || cardIds.Distinct(StringComparer.Ordinal).Count() != cardIds.Length)
            errors.Add("Card instance IDs are missing or duplicated.");
        if (root.Cards.Any(x => x.ModelId.Length == 0 || x.UpgradeLevel < 0 || x.EnergyCost < 0))
            errors.Add("Card model or cost values are invalid.");
        var pileIds = root.Piles.SelectMany(x => x.CardInstanceIds).ToArray();
        if (pileIds.Distinct(StringComparer.Ordinal).Count() != pileIds.Length)
            errors.Add("A card instance appears in more than one combat pile.");
        if (!cardIds.Order(StringComparer.Ordinal).SequenceEqual(pileIds.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            errors.Add("Combat piles do not account for every captured card instance.");
        if (root.Piles.Select(x => x.Kind).Distinct().Count() != root.Piles.Count)
            errors.Add("Combat pile kinds are duplicated.");

        var rngStreams = root.RandomStreams.Select(x => x.Stream).ToArray();
        var requiredStreams = Enum.GetValues<CombatRngStream>();
        if (rngStreams.Distinct().Count() != rngStreams.Length || !requiredStreams.Order().SequenceEqual(rngStreams.Order()))
            errors.Add("Exact replay requires one checkpoint for each of the nine combat RNG streams.");
        if (root.RandomStreams.Any(x => x.Algorithm.Length == 0 || x.StateHex.Length == 0 || x.DrawCount < 0))
            errors.Add("Random stream checkpoint is incomplete.");

        if (root.Extensions.GroupBy(x => (x.ModId, x.TypeId)).Any(x => x.Count() > 1))
            errors.Add("A Mod state extension is duplicated.");
        if (root.Extensions.Any(x => x.ModId.Length == 0 || x.TypeId.Length == 0 || x.SchemaVersion < 1 || x.Payload.ValueKind == JsonValueKind.Undefined || !x.HasForkAdapter))
            errors.Add("A Mod state extension is missing a schema, payload, or fork adapter.");
        foreach (var gap in root.Gaps.Where(x => !x.IsModeled && (x.Impact & BlockingImpact) != 0))
            errors.Add($"Unsupported gameplay effect {gap.EffectId} from {gap.SourceModelId}: {gap.Reason}");

        return new(errors.Count == 0, errors);
    }
}

public sealed record VisibleCardV1(string ModelId, int UpgradeLevel, int EnergyCost, string TargetType, SortedDictionary<string, int> DynamicVars);
public sealed record VisiblePowerV1(string ModelId, int Amount);
public sealed record VisibleCombatantV1(string ModelId, bool IsPlayer, bool IsAlive, int CurrentHp, int MaxHp, int Block, string? IntentId, int? IntentDamage, int? IntentHitCount, int? Energy, int? Stars, List<string> Orbs, List<VisiblePowerV1> Powers);

public sealed record CombatObservationV1
{
    public string Schema { get; init; } = "combat-observation/v1";
    public string GameVersion { get; init; } = GameBuildPin.Version;
    public string GameCommit { get; init; } = GameBuildPin.Commit;
    public string CharacterModelId { get; init; } = "";
    public string EncounterModelId { get; init; } = "";
    public int TurnNumber { get; init; }
    public CombatPhase Phase { get; init; }
    public int DrawPileCount { get; init; }
    public List<VisibleCardV1> Hand { get; init; } = [];
    public List<VisibleCardV1> Discard { get; init; } = [];
    public List<VisibleCombatantV1> Combatants { get; init; } = [];
}

public static class CombatObservationProjector
{
    public static CombatObservationV1 Project(CombatRootV1 root)
    {
        var cards = root.Cards.ToDictionary(x => x.InstanceId, StringComparer.Ordinal);
        var piles = root.Piles.ToDictionary(x => x.Kind);
        List<VisibleCardV1> VisiblePile(PileKind kind) => piles.TryGetValue(kind, out var pile)
            ? pile.CardInstanceIds.Select(id => cards[id]).Select(x => new VisibleCardV1(x.ModelId, x.UpgradeLevel, x.EnergyCost, x.TargetType, new(x.DynamicVars, StringComparer.Ordinal))).ToList()
            : [];
        var combatants = root.Combatants.Select(x => new VisibleCombatantV1(
            x.ModelId, x.IsPlayer, x.IsAlive, x.CurrentHp, x.MaxHp, x.Block, x.IntentId, x.IntentDamage, x.IntentHitCount,
            x.Resources?.Energy, x.Resources?.Stars, x.Resources?.Orbs.ToList() ?? [],
            x.Powers.Select(p => new VisiblePowerV1(p.ModelId, p.Amount)).ToList())).ToList();
        return new CombatObservationV1
        {
            GameVersion = root.Header.GameVersion,
            GameCommit = root.Header.GameCommit,
            CharacterModelId = root.Header.CharacterModelId,
            EncounterModelId = root.Header.EncounterModelId,
            TurnNumber = root.TurnNumber,
            Phase = root.Phase,
            DrawPileCount = piles.GetValueOrDefault(PileKind.Draw)?.CardInstanceIds.Count ?? 0,
            Hand = VisiblePile(PileKind.Hand),
            Discard = VisiblePile(PileKind.Discard),
            Combatants = combatants,
        };
    }
}

public sealed record ObservationComparisonV1(bool IsMatch, List<string> MismatchedFields);

/// <summary>
/// Compares player-visible projections field-by-field while retaining list order.
/// Host-specific capture code can map its live snapshot to CombatObservationV1 and
/// use this for replay checks without comparing hidden RNG or draw identities.
/// </summary>
public static class CombatObservationComparer
{
    public static ObservationComparisonV1 Compare(CombatObservationV1 expected, CombatObservationV1 actual)
    {
        List<string> mismatches = [];
        CompareField("schema", expected.Schema, actual.Schema);
        CompareField("gameVersion", expected.GameVersion, actual.GameVersion);
        CompareField("gameCommit", expected.GameCommit, actual.GameCommit);
        CompareField("characterModelId", expected.CharacterModelId, actual.CharacterModelId);
        CompareField("encounterModelId", expected.EncounterModelId, actual.EncounterModelId);
        CompareField("turnNumber", expected.TurnNumber, actual.TurnNumber);
        CompareField("phase", expected.Phase, actual.Phase);
        CompareField("drawPileCount", expected.DrawPileCount, actual.DrawPileCount);
        CompareField("hand", expected.Hand, actual.Hand);
        CompareField("discard", expected.Discard, actual.Discard);
        CompareField("combatants", expected.Combatants, actual.Combatants);
        return new(mismatches.Count == 0, mismatches);

        void CompareField<T>(string name, T left, T right)
        {
            if (!StringComparer.Ordinal.Equals(StateDigest.Compute(left), StateDigest.Compute(right)))
                mismatches.Add(name);
        }
    }
}

public interface ICombatSimulator<TLiveCombat> where TLiveCombat : class
{
    CombatRootV1 Capture(TLiveCombat liveCombat, CombatCaseHeaderV1 header);
    IReadOnlyList<CombatActionV1> EnumerateLegalActions(CombatRootV1 root);
    CombatRootV1 Apply(CombatRootV1 root, CombatActionV1 action);
    CombatRootV1 AdvanceToDecisionBoundary(CombatRootV1 root);
    CombatRootV1 Fork(CombatRootV1 root);
    CombatSnapshotV1 ReadSnapshot(CombatRootV1 root);
}

public static class CombatSnapshots
{
    public static CombatSnapshotV1 Read(CombatRootV1 root)
    {
        var detached = root.Fork();
        return new CombatSnapshotV1
        {
            CombatId = detached.CombatId,
            TurnNumber = detached.TurnNumber,
            Phase = detached.Phase,
            IsTerminal = detached.Phase is CombatPhase.Victory or CombatPhase.Defeat,
            StateDigest = StateDigest.Compute(detached),
            Combatants = detached.Combatants,
            Piles = detached.Piles,
            Gaps = detached.Gaps,
        };
    }
}

public interface IPlayerVisiblePolicy
{
    CombatActionV1 SelectAction(CombatObservationV1 observation);
}
