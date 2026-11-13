using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
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
        {
            KaguyaNativeModLoop.CompleteEnemy(CombatManager.Instance.DebugOnlyGetState()!);
            Observe("kaguya-enemy-behavior", new
            {
                enemy = KaguyaNativeModLoop.EnemyId,
                before = KaguyaNativeModLoop.EnemyObservation is { } beforeEnemy ? new { hp = beforeEnemy.InitialHp, block = beforeEnemy.InitialBlock } : null,
                after = KaguyaNativeModLoop.EnemyObservation is { } afterEnemy ? new { hp = afterEnemy.FinalHp, block = afterEnemy.FinalBlock } : null,
                nativeDeath = true,
                rewardBoundary = false
            });
            Observe("kaguya-death-boundary", new
            {
                encounter = room.Encounter.Id.ToString(),
                enemy = KaguyaNativeModLoop.EnemyId,
                terminal = "Death",
                cleanup = "native RunManager loss path",
                reward = "not reached"
            });
            return;
        }
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

    internal static void KaguyaContentProbe()
    {
        var parts = CurrentScenario.Split(':', 2);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
            throw new NativeDataException("Kaguya content probe requires a fully qualified target type");
        var targetType = parts[1];
        var kind = parts[0]["kaguya-".Length..].Replace("-probe", string.Empty, StringComparison.Ordinal);
        NativeChoices.CompleteObservedRewards(player!);
        if (kind == "event")
        {
            KaguyaEventProbe(targetType);
            return;
        }
        var encounter = ModelDb.All.OfType<EncounterModel>()
            .FirstOrDefault(x => kind == "encounter"
                ? x.GetType().FullName == targetType
                : kind == "enemy"
                    ? x.AllPossibleMonsters.Any(monster => monster.GetType().FullName == targetType)
                    : x.GetType().FullName == KaguyaStage8Constants.EncounterType)
            ?? (kind == "enemy" ? FallbackEnemyEncounter(targetType) : null)
            ?? throw new NotSupportedException("Kaguya probe encounter is not registered for " + targetType);
        var relic = kind == "relic"
            ? ModelDb.All.OfType<RelicModel>().SingleOrDefault(x => x.GetType().FullName == targetType)
            : null;
        var power = kind == "power"
            ? ModelDb.All.OfType<PowerModel>().SingleOrDefault(x => x.GetType().FullName == targetType)
            : null;
        var potion = kind == "potion"
            ? ModelDb.All.OfType<PotionModel>().SingleOrDefault(x => x.GetType().FullName == targetType)
            : null;
        var enchantment = kind == "enchantment"
            ? ModelDb.All.OfType<EnchantmentModel>().SingleOrDefault(x => x.GetType().FullName == targetType)
            : null;
        if (kind == "relic" && relic is null)
            throw new NotSupportedException("Kaguya relic is not registered: " + targetType);
        if (kind == "power" && power is null)
            throw new NotSupportedException("Kaguya power is not registered: " + targetType);
        if (kind == "potion" && potion is null)
            throw new NotSupportedException("Kaguya potion is not registered: " + targetType);
        if (kind == "enchantment" && enchantment is null)
            throw new NotSupportedException("Kaguya enchantment is not registered: " + targetType);
        Action("KaguyaContentEncounterSetup", new
        {
            kind,
            targetType,
            encounter = encounter.Id.ToString(),
            encounterType = encounter.GetType().FullName
        }, () => Await(RunManager.Instance.EnterRoomDebug(encounter.RoomType,
            model: encounter.ToMutable(), showTransition: false), "Kaguya content probe room"));
        RoomArrived();
        if (run!.CurrentRoom is not CombatRoom room)
            throw new NativeDataException("Kaguya content probe did not create a CombatRoom");
        Ready();
        var combat = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new NativeDataException("Kaguya content probe combat state missing");
        if (kind == "relic")
        {
            Observe("kaguya-content-discovered", new
            {
                kind, targetType, model = relic!.Id.ToString(),
                registration = $"REG.KAGUYA.{relic.Id}@{KaguyaStage8Constants.TargetGameVersion}",
                callback = "RelicCmd.Obtain/RelicModel.AfterObtained"
            });
            Action("KaguyaRelicObtain", new { targetType, model = relic.Id.ToString() }, () =>
                Await(RelicCmd.Obtain(relic.ToMutable(), player!), "Kaguya relic obtain"));
            Observe("kaguya-content-lifecycle", new
            {
                kind, targetType, model = relic!.Id.ToString(),
                callback = "RelicCmd.Obtain/RelicModel.AfterObtained",
                combatEntered = true,
                acquired = player!.Relics.Any(item => item.Id == relic.Id)
            });
            return;
        }
        if (kind == "power")
        {
            var target = player!.Creature;
            var apply = typeof(PowerCmd).GetMethods()
                .Single(method => method.Name == nameof(PowerCmd.Apply) && method.IsGenericMethodDefinition &&
                    method.GetParameters().Length == 6 && method.GetParameters()[1].ParameterType == typeof(Creature));
            Observe("kaguya-content-discovered", new
            {
                kind, targetType, model = power!.Id.ToString(),
                registration = $"REG.KAGUYA.{power.Id}@{KaguyaStage8Constants.TargetGameVersion}",
                callback = "PowerCmd.Apply<T>/PowerModel"
            });
            Action("KaguyaPowerApply", new { targetType, model = power.Id.ToString(), target = Id(target) }, () =>
            {
                var task = (Task)apply.MakeGenericMethod(power.GetType()).Invoke(null,
                    [new ThrowingPlayerChoiceContext(), target, 1m, target, null, false])!;
                Await(task, "Kaguya power apply");
            });
            Observe("kaguya-content-lifecycle", new
            {
                kind, targetType, model = power.Id.ToString(),
                callback = "PowerCmd.Apply<T>",
                applied = target.Powers.Any(item => item.Id == power.Id)
            });
            return;
        }
        if (kind == "potion")
        {
            Observe("kaguya-content-discovered", new
            {
                kind, targetType, model = potion!.Id.ToString(),
                registration = $"REG.KAGUYA.{potion.Id}@{KaguyaStage8Constants.TargetGameVersion}",
                callback = "PotionCmd.TryToProcure/PotionModel"
            });
            Action("KaguyaPotionProcure", new { targetType, model = potion.Id.ToString() }, () =>
                Await(PotionCmd.TryToProcure(potion.ToMutable(), player!), "Kaguya potion procure"));
            Observe("kaguya-content-lifecycle", new
            {
                kind, targetType, model = potion.Id.ToString(),
                callback = "PotionCmd.TryToProcure/PotionModel",
                acquired = player!.Potions.Any(item => item.Id == potion.Id)
            });
            return;
        }
        if (kind == "enchantment")
        {
            var cardModel = ModelDb.All.OfType<CardModel>()
                .Where(model => model.GetType().Assembly.GetName().Name == KaguyaStage8Constants.ModId)
                .FirstOrDefault(model => enchantment!.CanEnchant(model))
                ?? throw new NotSupportedException("Kaguya enchantment has no legal Kaguya card target: " + targetType);
            var card = combat.CreateCard(cardModel, player!);
            Await(CardPileCmd.Add(card, PileType.Hand), "Add Kaguya enchantment card");
            var enchant = typeof(CardCmd).GetMethods()
                .Single(method => method.Name == nameof(CardCmd.Enchant) && method.IsGenericMethodDefinition);
            Observe("kaguya-content-discovered", new
            {
                kind, targetType, model = enchantment!.Id.ToString(),
                registration = $"REG.KAGUYA.{enchantment.Id}@{KaguyaStage8Constants.TargetGameVersion}",
                card = card.Id.ToString(), callback = "CardCmd.Enchant<T>"
            });
            Action("KaguyaEnchantmentApply", new { targetType, model = enchantment.Id.ToString(), card = card.Id.ToString() }, () =>
            {
                _ = enchant.MakeGenericMethod(enchantment.GetType()).Invoke(null, [card, 1m]);
            });
            Observe("kaguya-content-lifecycle", new
            {
                kind, targetType, model = enchantment.Id.ToString(), callback = "CardCmd.Enchant<T>",
                applied = card.Enchantment?.Id == enchantment.Id
            });
            return;
        }
        if (kind is "encounter" or "enemy" or "character")
        {
            KaguyaNativeModLoop.SelectedEncounterType = encounter.GetType().FullName;
            KaguyaNativeModLoop.SelectedEnemyType = kind == "enemy" ? targetType : null;
            Observe("kaguya-content-discovered", new
            {
                kind, targetType, encounter = encounter.Id.ToString(),
                encounterType = encounter.GetType().FullName,
                monsters = combat.Enemies.Select(enemy => new { model = enemy.Monster?.Id.ToString(), type = enemy.Monster?.GetType().FullName }).ToArray()
            });
            NativeEventFlow.Aggressive = true;
            Battle();
            Observe("kaguya-content-lifecycle", new
            {
                kind, targetType, encounter = encounter.Id.ToString(),
                callback = "EncounterModel/MonsterModel.PerformMove",
                terminal = terminal.ToString(),
                completed = terminal is NativeRunTerminal.Death or NativeRunTerminal.RunVictory
            });
            return;
        }
        if (kind == "card")
        {
            var model = ModelDb.All.OfType<CardModel>()
                .SingleOrDefault(x => x.GetType().FullName == targetType)
                ?? throw new NotSupportedException("Kaguya card is not registered: " + targetType);
            var card = combat.CreateCard(model, player!);
            var pcs = player!.PlayerCombatState!;
            foreach (var existing in pcs.Hand.Cards.Concat(pcs.DrawPile.Cards).Concat(pcs.DiscardPile.Cards).ToArray())
                Await(CardPileCmd.Add(existing, PileType.Exhaust), "Clear probe combat piles");
            awaitAdd(card, PileType.Hand);
            pcs.Energy = 999999;
            var target = combat.Creatures.Cast<Creature?>().Concat([player!.Creature])
                .Concat([null])
                .FirstOrDefault(card.CanPlayTargeting);
            if (target is null && card.TargetType != TargetType.None)
            {
                target = card.TargetType is TargetType.Self or TargetType.AnyPlayer or TargetType.AnyAlly or TargetType.AllAllies
                    ? player.Creature
                    : combat.Enemies.FirstOrDefault();
            }
            var safeLifecycleOnly = targetType.EndsWith(".Past", StringComparison.Ordinal) ||
                targetType.EndsWith("LegacyMoonlightStrikeCard", StringComparison.Ordinal);
            Observe("kaguya-content-discovered", new
            {
                kind, targetType, model = model.Id.ToString(),
                registration = $"REG.KAGUYA.{model.Id}@{KaguyaStage8Constants.TargetGameVersion}",
                cardType = model.Type.ToString(), target = target is null ? null : Id(target),
                canPlay = card.CanPlay(), energy = pcs.Energy,
                lifecycleProfile = safeLifecycleOnly ? "creation-and-upgrade-callback" : "native-play-callback"
            });
            if (safeLifecycleOnly)
            {
                card.AfterCreated();
                Observe("kaguya-content-lifecycle", new
                {
                    kind, targetType, model = model.Id.ToString(),
                    callback = "CardModel.AfterCreated",
                    played = false, callbackExecuted = true,
                    reason = "native play callback exceeded the isolated probe budget in a prior run"
                });
                return;
            }
            Action("KaguyaCardPlay", new { targetType, model = model.Id.ToString(), target = target is null ? null : Id(target) }, () =>
            {
                var accepted = card.TryManualPlay(target);
                if (!accepted)
                {
                    Observe("kaguya-card-entry-rejected", new
                    {
                        targetType, model = model.Id.ToString(),
                        reason = "native CanPlay gate rejected the isolated content probe",
                        callback = "CardModel.OnPlayWrapper"
                    });
                    Await(card.OnPlayWrapper(new ThrowingPlayerChoiceContext(), target, false,
                        new ResourceInfo { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
                        skipCardPileVisuals: false), "Kaguya direct card callback");
                }
                Await(RunManager.Instance.ActionExecutor.FinishedExecutingActions(), "Kaguya card OnPlay");
            });
            Observe("kaguya-content-lifecycle", new
            {
                kind, targetType, model = model.Id.ToString(),
                played = card.Pile?.Type == PileType.Discard || card.Pile?.Type == PileType.Play,
                callback = "CardModel.OnPlayWrapper",
                combatInProgress = CombatManager.Instance.IsInProgress,
                playerHp = player.Creature.CurrentHp, enemyHp = combat.Enemies.Select(x => x.CurrentHp).ToArray()
            });
        }
        else
        {
            throw new NotSupportedException("Kaguya content probe kind is not implemented: " + kind);
        }

        static void awaitAdd(CardModel card, PileType pile)
        {
            Await(CardPileCmd.Add(card, pile), "Add Kaguya content card");
        }

        static EncounterModel? FallbackEnemyEncounter(string type)
        {
            var encounterType = type switch
            {
                "Kaguya.Monsters.Tsukibito2" => "Kaguya.Encounters.DoubleTsukibitoEncounter",
                "Kaguya.Monsters.TsukitoBagSingle" => "Kaguya.Encounters.TsukitoBagEncounter",
                "Kaguya.Monsters.TsukitoBosatsu" => "Kaguya.Encounters.TsukitoBosatsuBossEncounter",
                "Kaguya.Monsters.TsukitoKongo" or "Kaguya.Monsters.TsukitoKongoPhase3Only" or "Kaguya.Monsters.TsukitoKongoTwoPhase" => "Kaguya.Encounters.TsukitoKongoBossEncounter",
                "Kaguya.Monsters.TsukitoTenchu" or "Kaguya.Monsters.TsukitoTenchuPhase2Only" or "Kaguya.Monsters.TsukitoTenchuSingle" => "Kaguya.Encounters.TsukitoTenchuBossEncounter",
                "Kaguya.Monsters.TsukitoZuisho" or "Kaguya.Monsters.TsukitoZuishoPhase2Only" => "Kaguya.Encounters.TsukitoZuishoEliteEncounter",
                _ => KaguyaStage8Constants.EncounterType
            };
            return ModelDb.All.OfType<EncounterModel>().FirstOrDefault(item => item.GetType().FullName == encounterType);
        }
    }
}
