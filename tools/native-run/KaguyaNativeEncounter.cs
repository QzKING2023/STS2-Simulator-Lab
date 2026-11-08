using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SimulatorLab.Core;
using SimulatorLab.Core.Mods;

namespace OfflineSearchHarness;

internal static partial class Program
{
    internal static void KaguyaEncounterProbe()
    {
        var manager = RunManager.Instance;
        var encounter = ModelDb.All.OfType<EncounterModel>()
            .Where(x => x.GetType().FullName == KaguyaStage8Constants.EncounterType)
            .SingleOrDefault() ?? throw new NotSupportedException("Kaguya IroP encounter is not registered");
        var selected = new { Encounter = encounter, Monsters = encounter.AllPossibleMonsters.ToArray(), TotalHp = encounter.AllPossibleMonsters.Sum(m => m.MinInitialHp) };
        KaguyaNativeModLoop.SelectedEncounterType = encounter.GetType().FullName;
        KaguyaNativeModLoop.SelectedEnemyType = (selected?.Monsters.FirstOrDefault() ?? encounter.AllPossibleMonsters.First()).GetType().FullName;
        KaguyaNativeModLoop.EncounterId = encounter.Id.ToString();
        KaguyaNativeModLoop.GoldBefore = player!.Gold;
        Observe("kaguya-plan", new
        {
            mod = KaguyaStage8Constants.ModId,
            encounterType = encounter.GetType().FullName,
            encounter = encounter.Id.ToString(),
            roomType = encounter.RoomType.ToString(),
            enemyType = KaguyaNativeModLoop.SelectedEnemyType,
            candidates = new[] { new { type = selected.Encounter.GetType().FullName, id = selected.Encounter.Id.ToString(), selected.TotalHp, enemies = selected.Monsters.Select(m => new { type = m.GetType().FullName, hp = m.MinInitialHp }).ToArray() } },
            targetGameVersion = KaguyaStage8Constants.TargetGameVersion,
            dependency = KaguyaStage8Constants.BaseLibId + "@" + KaguyaStage8Constants.BaseLibVersion
        });
        Action("KaguyaEncounterSetup", new
        {
            encounter = encounter.Id.ToString(),
            encounterType = encounter.GetType().FullName,
            source = "ModelDb.All exact registered Kaguya encounter"
        }, () => Await(manager.EnterRoomDebug(encounter.RoomType, model: encounter.ToMutable(), showTransition: false),
            "Kaguya native encounter room"));
        RoomArrived();
        if (run!.CurrentRoom is not CombatRoom room)
            throw new NativeDataException("Kaguya debug room did not create a CombatRoom");
        Ready();
        var state = CombatManager.Instance.DebugOnlyGetState() ?? throw new NativeDataException("Kaguya combat state missing");
        KaguyaNativeModLoop.CaptureEnemy(state);
        Observe("kaguya-encounter-entered", new
        {
            encounter = room.Encounter.Id.ToString(),
            encounterType = room.Encounter.GetType().FullName,
            enemy = KaguyaNativeModLoop.EnemyId,
            enemyType = KaguyaStage8Constants.EnemyType,
            initial = KaguyaNativeModLoop.EnemyObservation
        });
        Observe("kaguya-enemy-intent", new
        {
            enemy = KaguyaNativeModLoop.EnemyId,
            intent = KaguyaNativeModLoop.EnemyObservation?.IntentIds,
            intendsToAttack = KaguyaNativeModLoop.EnemyObservation?.IntendsToAttack,
            evidence = "native/MonsterModel.NextMove"
        });
        NativeEventFlow.Aggressive = true;
        Battle();
        if (terminal == NativeRunTerminal.Death)
            throw new NotSupportedException("Kaguya encounter probe died before reward boundary");
        KaguyaNativeModLoop.CompleteEnemy(CombatManager.Instance.DebugOnlyGetState()!);
        Observe("kaguya-enemy-behavior", new
        {
            enemy = KaguyaNativeModLoop.EnemyId,
            before = KaguyaNativeModLoop.EnemyObservation is { } before ? new { hp = before.InitialHp, block = before.InitialBlock } : null,
            after = KaguyaNativeModLoop.EnemyObservation is { } after ? new { hp = after.FinalHp, block = after.FinalBlock } : null,
            nativeVictory = true
        });
        Observe("kaguya-battle-victory", new { encounter = room.Encounter.Id.ToString(), terminal = "battle-complete" });
        Rewards();
        var rows = WatcherEvents.Select(x => JsonSerializer.SerializeToElement(x, Json)).ToArray();
        KaguyaNativeModLoop.CaptureReward(rows, KaguyaNativeModLoop.GoldBefore, player.Gold);
        Observe("kaguya-resource-ledger", new
        {
            goldBefore = KaguyaNativeModLoop.GoldBefore,
            goldAfter = player.Gold,
            delta = player.Gold - KaguyaNativeModLoop.GoldBefore,
            reward = KaguyaNativeModLoop.RewardObservation
        });
        RoomComplete("native Kaguya encounter reward and cleanup boundary");
        Action("KaguyaTeardown", new { room = NativeIdentity.Ref(room), encounter = room.Encounter.Id.ToString() },
            () => Await(room.Exit(run), "Kaguya native room Exit"));
        Observe("kaguya-cleanup", new { room = NativeIdentity.Ref(room), currentRoom = run.CurrentRoom?.GetType().Name });
        Observe("kaguya-teardown", new { encounter = KaguyaNativeModLoop.EncounterId, registryIsolation = true, newRunRequired = true });
    }
}
