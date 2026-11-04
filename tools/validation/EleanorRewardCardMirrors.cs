using System.Reflection;
using CombatSolver;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;
using CombatSolver.Engine.InCombat.Simulation;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using System.Runtime.CompilerServices;

namespace OfflineSearchHarness;

internal static partial class EleanorV040Probe
{
    internal const string RewardSchema="eleanor-v041/reward-card-selection/v1";
    private static void RegisterRewardCards(Assembly mod)
    {
        foreach(var name in new[]{"ReturnFlow","ManaConversionPrinciple","ManaEssence"})
        {
            var type=mod.GetType("MyMod.MyModCode.Cards."+name,true)!;
            var target=AccessTools.Method(type,"OnPlay",[typeof(PlayerChoiceContext),typeof(CardPlay)]);
            var handler=typeof(EleanorV040Probe).GetMethod(nameof(DispatchRewardCard),BindingFlags.Static|BindingFlags.NonPublic)!.MakeGenericMethod(type)
                .CreateDelegate(typeof(Action<,>).MakeGenericType(type,typeof(CardOnPlayMirrorContext)));
            var register=typeof(AdaptedCardOnPlayMirrors).GetMethod("RegisterUnpatched")!.MakeGenericMethod(type);
            var other=AccessTools.Method(mod.GetType("MyMod.MyModCode.Cards.ManaStrike",true)!,"OnPlay",[typeof(PlayerChoiceContext),typeof(CardPlay)]);
            bool wrongTarget=Rejects(register,[RewardSchema,other,mod,_manifest,handler]);
            bool wrongAssembly=Rejects(register,[RewardSchema,target,typeof(EleanorV040Probe).Assembly,_manifest,handler]);
            bool wrongManifest=Rejects(register,[RewardSchema,target,mod,"{\"id\":\"wrong\",\"name\":\"wrong\",\"version\":\"0\",\"affects_gameplay\":true}",handler]);
            PatchProbe.Patch(target,prefix:new HarmonyMethod(typeof(EleanorV040Probe).GetMethod(nameof(GuardPrefix),BindingFlags.Static|BindingFlags.NonPublic)!));
            bool patched;try{patched=Rejects(register,[RewardSchema,target,mod,_manifest,handler]);}finally{PatchProbe.UnpatchAll(PatchOwner);}
            if(!(wrongTarget&&wrongAssembly&&wrongManifest&&patched))throw new InvalidOperationException("Reward source audit control failed.");
            register.Invoke(null,[RewardSchema,target,mod,_manifest,handler]);
            RegistrationEvidence.Add(new{cardType=type.FullName,wrongTarget,wrongAssembly,wrongManifest,existingPatchRejected=patched});
        }
    }

    private static void VerifyRewardPatchAudit(Assembly mod)
    {
        // Capture seals the registry. Run these controls only after every adapter is registered.
        foreach(var name in new[]{"ReturnFlow","ManaConversionPrinciple","ManaEssence"})
        {
            var type=mod.GetType("MyMod.MyModCode.Cards."+name,true)!;
            var target=AccessTools.Method(type,"OnPlay",[typeof(PlayerChoiceContext),typeof(CardPlay)]);
            var before=AdaptedCardOnPlayMirrors.CaptureLiveStamp();
            bool latePatchRejected=false;
            PatchProbe.Patch(target,prefix:new HarmonyMethod(typeof(EleanorV040Probe).GetMethod(nameof(GuardPrefix),BindingFlags.Static|BindingFlags.NonPublic)!));
            try
            {
                bool changed=AdaptedCardOnPlayMirrors.CaptureLiveStamp()!=before;
                try{_=AdaptedCardOnPlayMirrors.Select(type,target,Harmony.GetPatchInfo(target));}
                catch(PredictionUnsupportedException){latePatchRejected=changed;}
            }
            finally{PatchProbe.UnpatchAll(PatchOwner);}
            var moveNext=target.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType.GetMethod("MoveNext",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            bool? stateMachinePatchRejected=null;
            if(moveNext!=null)
            {
                stateMachinePatchRejected=false;
                PatchProbe.Patch(moveNext,prefix:new HarmonyMethod(typeof(EleanorV040Probe).GetMethod(nameof(GuardPrefix),BindingFlags.Static|BindingFlags.NonPublic)!));
                try{try{_=AdaptedCardOnPlayMirrors.CaptureLiveStamp();}catch(PredictionUnsupportedException){stateMachinePatchRejected=true;}}finally{PatchProbe.UnpatchAll(PatchOwner);}
            }
            if(!latePatchRejected||stateMachinePatchRejected==false)throw new InvalidOperationException("Reward late patch audit failed.");
            if(AdaptedCardOnPlayMirrors.CaptureLiveStamp()!=before)throw new InvalidOperationException("Patch control failed to restore registration stamp.");
            RegistrationEvidence.Add(new{cardType=type.FullName,actualLatePatchChangesStampAndRejects=latePatchRejected,actualStateMachinePatchRejected=stateMachinePatchRejected});
        }
    }

    private static void DispatchRewardCard<TCard>(TCard card,CardOnPlayMirrorContext context) where TCard:CardModel
    {
        var sim=context.Simulator;
        sim.RejectExecutionContinuation();
        var player=context.PreviewCard.Owner;
        var state=GetState(context,GetBranchOrb(sim,player));
        var tx=state.BeginCard(context.CardPlay.PlayIndex==0?context.CardPlay.Resources.EnergySpent:0);
        var combat=(SimulatedCombatState)sim.State.CombatState;
        var owner=sim.State.GetPlayerCombatState(player);
        if(card.GetType().Name=="ReturnFlow")
        {
            bool paid=state.TryPayMana(1);
            if(paid)
            {
                int shield=combat.EffectivePowers().OfType<PowerModel>().Where(p=>p.GetType().Name=="ManaToShieldPower"&&p.Owner==player.Creature).Sum(p=>p.Amount);
                if(shield>0)sim.GainBlock(player.Creature,shield,(MegaCrit.Sts2.Core.ValueProps.ValueProp)4,context.Card,context.CardPlay);
                foreach(var pile in new[]{PileType.Draw,PileType.Discard})
                {
                    var source=owner.GetCardPile(pile)!.Cards;
                    var options=pile==PileType.Draw?source.OrderBy(c=>c.Preview.Rarity).ThenBy(c=>c.Preview.Id.Entry,StringComparer.Ordinal).ToArray():source.ToArray();
                    if(!combat.TryResolveAdapterCardSelection(context,RewardSchema,"return/"+pile,pile,options,out var selected))return;
                    foreach(var selectedCard in selected)sim.AddToPile(selectedCard,PileType.Hand,CardPilePosition.Bottom);
                }
            }
        }
        else if(card.GetType().Name=="ManaConversionPrinciple")
        {
            var options=owner.Hand.Cards.Where(c=>!ReferenceEquals(c,context.Card)&&c.Preview.IsTransformable).ToArray();
            if(!combat.TryResolveAdapterCardSelection(context,RewardSchema,"transform/essence",PileType.Hand,options,out var selected))return;
            if(selected.Count>0)
            {
                var canonical=ModelDb.All.OfType<CardModel>().Single(c=>c.GetType().FullName=="MyMod.MyModCode.Cards.ManaEssence");
                if(!CardChoiceSupport.TransformCards(sim,selected,canonical,card.IsUpgraded))return;
            }
        }
        else
        {state.GainMana(card.DynamicVars["Mana"].IntValue);sim.Draw(player,1);if(sim.HasPendingChoice)return;}
        state.FinishCard(tx);
    }
}
