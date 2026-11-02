using System.Text.Json;
using CombatSolver.Engine.Common;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace OfflineSearchHarness;

internal static partial class EleanorV040Probe
{
    public static object RunManaShieldBoundary(CombatState combat,MainLoopContext loop,string output)
    {
        int requested=int.Parse(Environment.GetEnvironmentVariable("SIMULATORLAB_SHIELD_MANA")!);
        bool upgraded=Environment.GetEnvironmentVariable("SIMULATORLAB_SHIELD_UPGRADED")=="1";
        if(requested is <0 or >2)throw new InvalidOperationException("Shield matrix only admits Mana 0/1/2.");
        VanillaGameActionProbe.InstallGodotClockShim();
        var player=LocalContext.GetMe(combat)!;
        if(player.Creature.Powers.Count!=0||player.Creature.Block!=0)throw new PredictionUnsupportedException("Shield fixture requires a neutral power-free opening.");
        var canonical=ModelDb.All.OfType<CardModel>().Single(c=>c.GetType().FullName=="MyMod.MyModCode.Cards.ManaShield");
        var card=combat.CreateCard(canonical,player);
        loop.RunUntilCompleted(CardPileCmd.Add(card,PileType.Hand),TimeSpan.FromSeconds(15),"Native API fixture card addition");
        if(upgraded)CardCmd.Upgrade(card);
        object mana=GetLiveManaState(player);
        int initial=CaptureState(_stateType!,mana).Mana;
        bool paid=(bool)_stateType!.GetMethod("TryPayMana")!.Invoke(mana,[initial-requested])!;
        if(!paid||CaptureState(_stateType,mana).Mana!=requested||card.CurrentUpgradeLevel!=(upgraded?1:0))
            throw new InvalidOperationException("Native upgrade/resource fixture setup failed.");
        int energyBefore=player.PlayerCombatState!.Energy;
        int blockBefore=player.Creature.Block;
        if (Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_DLL")!))) != SimulatorLab.Core.EleanorV041Contracts.AssemblySha256)
            throw new InvalidOperationException("Independent ManaShield contract is bound to the v0.4.1 input.");
        decimal expectedBlock=SimulatorLab.Core.EleanorV041Contracts.ManaShield(upgraded,requested).Block;
        File.WriteAllText(Path.Combine(output,"route.json"),JsonSerializer.Serialize(new[]{new{kind="PlayCard",turn=player.PlayerCombatState.TurnNumber,cardId="MANA_SHIELD",cardOccurrence=0,targetIndex=-1}}));
        File.WriteAllText(Path.Combine(output,"harness-result.json"),JsonSerializer.Serialize(new{eleanorMod=new{assemblySha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_DLL")!)))},scenario=new{CharacterId="MyMod",EncounterId=((CombatRoom)combat.RunState.CurrentRoom!).Encounter.Id.Entry,Seed=NativeModRunLoop.SourceSeed}}));
        var replay=JsonSerializer.SerializeToElement(RunVictoryReplay(combat,loop,output,Path.Combine(output,"route.json"),allowBoundary:true));
        var after=CaptureState(_stateType,mana);
        bool exact=replay.GetProperty("status").GetString()=="boundary"&&after.Mana==requested-(requested>=1?1:0)
            &&player.PlayerCombatState.Energy==energyBefore-1&&player.Creature.Block-blockBefore==expectedBlock;
        var report=new{schema="mana-shield-boundary/v1",status=exact?"passed":"failed",fixture="actual CombatState.CreateCard/CardPileCmd.Add/CardCmd.Upgrade and ManaState.TryPayMana; no reward-acquisition claim",upgraded,requestedMana=requested,
            setup=new{initialMana=initial,drained=initial-requested,card=card.ToSerializable(),upgradeLevel=card.CurrentUpgradeLevel},energyBefore,energyAfter=player.PlayerCombatState.Energy,blockBefore,blockAfter=player.Creature.Block,expectedBlock,manaAfter=after,replay,guards=CaptureOfflineWriteEvidence()};
        File.WriteAllText(Path.Combine(output,"shield-boundary.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        if(!exact)throw new InvalidOperationException("ManaShield boundary contract or native/simulator replay failed.");
        return report;
    }
}
