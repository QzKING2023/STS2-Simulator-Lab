using System.Reflection;
using CombatSolver;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;
using CombatSolver.Engine.InCombat.Simulation;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;

namespace OfflineSearchHarness;

/// <summary>
/// Lab-owned, card-specific adapters for the existing Eleanor ManaGather/ManaStrike rules.
/// Registration binds to the exact unpatched OnPlay method and the loaded Mod's manifest identity.
/// </summary>
internal static class EleanorCardMirrors
{
    private const string ExistingPatchOwner = "simulatorlab.eleanor.registration-guard-probe";
    private static readonly Harmony ExistingPatchProbe = new(ExistingPatchOwner);
    private static readonly List<EleanorRegistrationGuardEvidence> RegistrationGuards = [];
    private static Type? _manaStateType;
    private static int _openingMana;
    private static int _openingGauge;
    private static string _openingStance = "Neutral";
    private static bool _openingCompleteOrb;
    private static int _openingStartDrawBonus;
    private static Type? _manaToShieldPowerType;

    public static IReadOnlyList<EleanorRegistrationGuardEvidence> GuardEvidence => RegistrationGuards;

    public static void Register(Assembly mod, string modManifestJson, Type manaStateType, int mana, int gauge,
        string stance, bool completeOrb, int startDrawBonus)
    {
        _manaStateType = manaStateType;
        _openingMana = mana;
        _openingGauge = gauge;
        _openingStance = stance;
        _openingCompleteOrb = completeOrb;
        _openingStartDrawBonus = startDrawBonus;
        EleanorTurnStartMirrors.Register(mod, modManifestJson, manaStateType);
        Type manaGather = mod.GetType("MyMod.MyModCode.Cards.ManaGather", throwOnError: true)!;
        Type manaStrike = mod.GetType("MyMod.MyModCode.Cards.ManaStrike", throwOnError: true)!;
        Type manaBurst = mod.GetType("MyMod.MyModCode.Cards.ManaBurst", throwOnError: true)!;
        Type manaToShield = mod.GetType("MyMod.MyModCode.Cards.ManaToShield", throwOnError: true)!;
        _manaToShieldPowerType = mod.GetType("MyMod.MyModCode.Powers.ManaToShieldPower", throwOnError: true)!;
        RegisterCard(mod, modManifestJson, manaGather, manaStrike);
        RegisterCard(mod, modManifestJson, manaStrike, manaGather);
        RegisterCard(mod, modManifestJson, manaBurst, manaGather);
        RegisterCard(mod, modManifestJson, manaToShield, manaBurst);
    }

    public static EleanorManaPredictionState GetState(CardOnPlayMirrorContext context)
    {
        RelicModel orb = context.PreviewCard.Owner.Relics
            .SingleOrDefault(relic => relic.GetType().Name is "CrackedCrystalOrb" or "CompleteCrystalOrb")
            ?? throw new PredictionUnsupportedException("Eleanor ManaRuntime state has no CrackedCrystalOrb owner key.");
        return GetState(context.Simulator, orb);
    }

    public static EleanorManaPredictionState GetState(CombatPredictionSimulator simulator, RelicModel orb)
    {
        Type stateType = _manaStateType
            ?? throw new InvalidOperationException("Eleanor card mirrors were not registered before root capture.");
        return simulator.StateStore.Get<RelicModel, EleanorManaPredictionState>(orb,
            _ => new(stateType, _openingMana, _openingGauge, _openingStance, _openingCompleteOrb, _openingStartDrawBonus));
    }

    public static bool TryRead(CombatPredictionSimulator simulator, RelicModel orb,
        out EleanorManaSnapshot snapshot)
    {
        if (simulator.StateStore.TryGetReadOnly<EleanorManaPredictionState>(orb, out EleanorManaPredictionState? state))
        {
            snapshot = state!.Capture();
            return true;
        }
        snapshot = default;
        return false;
    }

    private static void RegisterCard(Assembly mod, string modManifestJson, Type cardType, Type wrongCardType)
    {
        MethodInfo target = AccessTools.Method(cardType, "OnPlay",
                [typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext), typeof(CardPlay)])
            ?? throw new MissingMethodException(cardType.FullName, "OnPlay");
        Type contextType = typeof(CardOnPlayMirrorContext);
        Type handlerType = typeof(Action<,>).MakeGenericType(cardType, contextType);
        MethodInfo handlerMethod = typeof(EleanorCardMirrors).GetMethod(nameof(Dispatch), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(cardType);
        Delegate handler = handlerMethod.CreateDelegate(handlerType);
        MethodInfo register = typeof(AdaptedCardOnPlayMirrors).GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(method => method.Name == "RegisterUnpatched" && method.IsGenericMethodDefinition)
            .MakeGenericMethod(cardType);
        MethodInfo wrongTarget = AccessTools.Method(wrongCardType, "OnPlay",
                [typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext), typeof(CardPlay)])
            ?? throw new MissingMethodException(wrongCardType.FullName, "OnPlay");
        string wrongManifest = "{\"id\":\"not-the-loaded-assembly\",\"name\":\"guard probe\",\"version\":\"0\",\"affects_gameplay\":true}";
        bool wrongTargetRejected = RejectsRegistration(register,
            ["eleanor-guard/wrong-target", wrongTarget, mod, modManifestJson, handler]);
        bool wrongAssemblyRejected = RejectsRegistration(register,
            ["eleanor-guard/wrong-assembly", target, typeof(EleanorCardMirrors).Assembly, modManifestJson, handler]);
        bool wrongManifestRejected = RejectsRegistration(register,
            ["eleanor-guard/wrong-manifest", target, mod, wrongManifest, handler]);
        ExistingPatchProbe.Patch(target, prefix: new HarmonyMethod(
            typeof(EleanorCardMirrors).GetMethod(nameof(GuardPrefix), BindingFlags.Static | BindingFlags.NonPublic)!));
        bool existingPatchRejected;
        try
        {
            existingPatchRejected = RejectsRegistration(register,
                ["eleanor-guard/existing-patch", target, mod, modManifestJson, handler]);
        }
        finally
        {
            ExistingPatchProbe.UnpatchAll(ExistingPatchOwner);
        }
        if (!wrongTargetRejected || !wrongAssemblyRejected || !wrongManifestRejected || !existingPatchRejected)
            throw new InvalidOperationException($"No-patch registration guards failed for {cardType.FullName}.");
        RegistrationGuards.Add(new(cardType.FullName!, wrongTargetRejected, wrongAssemblyRejected,
            wrongManifestRejected, existingPatchRejected));
        InvokeRegistration(register, ["eleanor-mana-card/v1", target, mod, modManifestJson, handler]);
    }

    private static bool RejectsRegistration(MethodInfo register, object?[] arguments)
    {
        try
        {
            InvokeRegistration(register, arguments);
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private static void InvokeRegistration(MethodInfo register, object?[] arguments)
    {
        try
        {
            register.Invoke(null, arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static void GuardPrefix() { }

    private static void Dispatch<TCard>(TCard card, CardOnPlayMirrorContext context) where TCard : CardModel
    {
        EleanorManaPredictionState state = GetState(context);
        int energySpent = context.CardPlay.PlayIndex == 0 ? context.CardPlay.Resources.EnergySpent : 0;
        object transaction = state.BeginCard(energySpent);

        switch (card.GetType().Name)
        {
            case "ManaGather":
                // Matches the Mod's OnPlay order: ordinary card block, then ManaRuntime.GainMana.
                context.GainBlock(card.Owner.Creature);
                int requested = context.PreviewCard.DynamicVars["Mana"].IntValue;
                int gained = state.GainMana(requested);
                state.AddManaGained(transaction, gained);
                state.FinishCard(transaction);
                return;
            case "ManaStrike":
                // The live card debits mana before constructing one 2-hit attack. There is no
                // stance/gauge settlement between the two hits; Finish runs after both complete.
                bool paid = state.TryPayMana(1);
                if (paid) state.AddManaPaid(transaction, 1);
                if (paid && context.CombatState is SimulatedCombatState simulatedCombat
                    && simulatedCombat.EffectivePowers().FirstOrDefault(power =>
                        power.GetType() == _manaToShieldPowerType && ReferenceEquals(power.Owner, card.Owner.Creature)) is { Amount: > 0 } shield)
                    context.GainBlock(card.Owner.Creature, shield.Amount, MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered);
                context.AttackSingle(paid ? 2 : 1);
                state.FinishCard(transaction);
                return;
            case "ManaBurst":
                int burstMana = context.PreviewCard.DynamicVars["Mana"].IntValue;
                int burstGained = state.GainMana(burstMana);
                state.AddManaGained(transaction, burstGained);
                state.FinishCard(transaction);
                return;
            case "ManaToShield":
                SimulatedCombatState combat = context.CombatState as SimulatedCombatState
                    ?? throw new PredictionUnsupportedException("ManaToShield requires simulated combat state.");
                Type powerType = _manaToShieldPowerType
                    ?? throw new InvalidOperationException("ManaToShieldPower type was not bound to the loaded Mod assembly.");
                decimal powerAmount = context.PreviewCard.DynamicVars.Block.BaseValue;
                if (powerAmount != decimal.Truncate(powerAmount) || powerAmount < int.MinValue || powerAmount > int.MaxValue)
                    throw new PredictionUnsupportedException($"ManaToShieldPower amount {powerAmount} is not an exact integer.");
                combat.ApplyPower(powerType, card.Owner.Creature, decimal.ToInt32(powerAmount), card.Owner.Creature);
                return;
            default:
                throw new PredictionUnsupportedException($"No Eleanor card mirror exists for {card.GetType().FullName}.");
        }
    }

}

internal sealed record EleanorRegistrationGuardEvidence(string CardType, bool WrongTargetRejected,
    bool WrongAssemblyRejected, bool WrongManifestRejected, bool ExistingPatchRejected);

internal readonly record struct EleanorManaSnapshot(int Mana, int Gauge, string Stance,
    bool CompleteOrb, int StartDrawBonus);

internal readonly record struct EleanorStartTurnOutcome(int EnergyPaid, int ManaPaid, int HpLoss,
    int OrbEnergy, int OrbMana, EleanorManaSnapshot After);

/// <summary>Mod state stored under the actual CrackedCrystalOrb model and deep-forked per simulator branch.</summary>
internal sealed class EleanorManaPredictionState : IPredictionStateForkable, IPredictionForkBoundary
{
    private readonly Type _stateType;
    private object _manaState;

    public EleanorManaPredictionState(Type stateType, int mana, int gauge, string stance, bool completeOrb, int startDrawBonus)
    {
        _stateType = stateType;
        _manaState = Create(stateType, new(mana, gauge, stance, completeOrb, startDrawBonus));
    }

    private EleanorManaPredictionState(Type stateType, object manaState)
    {
        _stateType = stateType;
        _manaState = manaState;
    }

    public object Fork(PredictionForkContext context)
        => new EleanorManaPredictionState(_stateType, Clone(_stateType, Capture()));

    public void AssertForkable() { }

    public object BeginCard(int energySpent) => _stateType.GetMethod("BeginCard")!.Invoke(_manaState, [energySpent])!;
    public int GainMana(int amount) => (int)_stateType.GetMethod("GainMana")!.Invoke(_manaState, [amount])!;
    public bool TryPayMana(int amount) => (bool)_stateType.GetMethod("TryPayMana")!.Invoke(_manaState, [amount])!;
    public void AddManaGained(object tx, int amount) => Set(tx, "ManaGained", amount);
    public void AddManaPaid(object tx, int amount) => Set(tx, "ManaPaid", amount);
    public void FinishCard(object tx) => _stateType.GetMethod("FinishCard")!.Invoke(_manaState, [tx]);

    public EleanorStartTurnOutcome StartTurn(int availableEnergy, bool completeOrb)
    {
        _stateType.GetMethod("SetCompleteOrb", [typeof(bool)])!.Invoke(_manaState, [completeOrb]);
        object upkeep = _stateType.GetMethod("PayUpkeep", [typeof(int)])!.Invoke(_manaState, [availableEnergy])!;
        int energyPaid = ReadRecordInt(upkeep, "EnergyPaid");
        int manaPaid = ReadRecordInt(upkeep, "ManaPaid");
        int hpLoss = ReadRecordInt(upkeep, "HpLoss");
        object orb = _stateType.GetMethod("GrantOrbStart", Type.EmptyTypes)!.Invoke(_manaState, null)!;
        int orbEnergy = ReadRecordInt(orb, "Energy");
        int orbMana = ReadRecordInt(orb, "Mana");
        return new(energyPaid, manaPaid, hpLoss, orbEnergy, orbMana, Capture());
    }

    public EleanorManaSnapshot Capture()
        => new(ReadInt("Mana"), ReadInt("Gauge"), _stateType.GetProperty("Stance")!.GetValue(_manaState)!.ToString()!,
            (bool)_stateType.GetProperty("CompleteOrb")!.GetValue(_manaState)!, ReadInt("StartDrawBonus"));

    private int ReadInt(string name) => Convert.ToInt32(_stateType.GetProperty(name)!.GetValue(_manaState));

    private static int ReadRecordInt(object record, string name)
        => Convert.ToInt32(record.GetType().GetProperty(name)?.GetValue(record)
            ?? throw new MissingMemberException(record.GetType().FullName, name));

    private static object Create(Type type, EleanorManaSnapshot snapshot)
    {
        object state = Activator.CreateInstance(type)!;
        Set(state, "Mana", snapshot.Mana);
        Set(state, "Gauge", snapshot.Gauge);
        PropertyInfo stance = type.GetProperty("Stance")!;
        Set(state, "Stance", Enum.Parse(stance.PropertyType, snapshot.Stance));
        Set(state, "CompleteOrb", snapshot.CompleteOrb);
        Set(state, "StartDrawBonus", snapshot.StartDrawBonus);
        return state;
    }

    private static object Clone(Type type, EleanorManaSnapshot snapshot) => Create(type, snapshot);

    private static void Set(object target, string name, object? value)
    {
        PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(target.GetType().FullName, name);
        (property.GetSetMethod(true) ?? throw new MissingMethodException(target.GetType().FullName, "set_" + name)).Invoke(target, [value]);
    }
}
