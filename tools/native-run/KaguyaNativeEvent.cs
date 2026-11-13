using System.Text.Json;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SimulatorLab.Core;
using SimulatorLab.Core.Mods;

namespace OfflineSearchHarness;

internal static partial class Program
{
    internal static void KaguyaEventProbe(string? requestedType = null)
    {
        var manager = RunManager.Instance;
        var eventModel = ModelDb.All.OfType<EventModel>()
            .Where(x => x.GetType().FullName == (requestedType ?? KaguyaStage8Constants.EventType))
            .SingleOrDefault() ?? throw new NotSupportedException("Kaguya Taketori event is not registered");
        KaguyaNativeModLoop.SelectedEventType = eventModel.GetType().FullName;
        KaguyaNativeModLoop.GoldBefore = player!.Gold;
        Action("KaguyaEventSetup", new
        {
            eventType = eventModel.GetType().FullName,
            source = "ModelDb.All exact registered Kaguya event"
        }, () => Await(manager.EnterRoomDebug(RoomType.Event, model: eventModel, showTransition: false),
            "Kaguya native event room"));
        RoomArrived();
        if (run!.CurrentRoom is not EventRoom room)
            throw new NativeDataException("Kaguya debug room did not create an EventRoom");
        EnterAuditedEvent();
        var ev = manager.EventSynchronizer.GetLocalEvent();
        var options = ev.CurrentOptions.ToArray();
        if (options.Length == 0)
            throw new NotSupportedException("Kaguya event exposed no options");
        var optionIds = options.Select(x => x.TextKey).ToArray();
        Observe("kaguya-event-entered", new
        {
            eventId = ev.Id.ToString(),
            eventType = ev.GetType().FullName,
            options = optionIds,
            finished = ev.IsFinished
        });
        var before = State();
        var selectedOptions = new List<string>();
        var after = before;
        for (var step = 0; !ev.IsFinished && step < 32; step++)
        {
            var current = ev.CurrentOptions.ToArray();
            var index = requestedType == "Kaguya.Events.RainbowTelephonePole"
                ? Array.FindIndex(current, option => !option.IsLocked)
                : Array.FindIndex(current, option => !option.IsLocked && option.IsProceed);
            if (index < 0)
                index = Array.FindLastIndex(current, option => !option.IsLocked);
            if (index < 0)
                throw new NotSupportedException($"Kaguya event {ev.Id} exposed only locked options at step {step}");
            var selected = current[index].TextKey;
            selectedOptions.Add(selected);
            SelectRoomOption(index);
            after = State();
            Observe("kaguya-event-option", new
            {
                eventId = ev.Id.ToString(), option = selected, optionIndex = index,
                step, finished = ev.IsFinished
            });
        }
        if (!ev.IsFinished)
            throw new NotSupportedException("Kaguya event did not finish within thirty-two native options");
        var selectedOption = selectedOptions.FirstOrDefault() ?? optionIds[0];
        var cardId = NewCardId(before, after);
        KaguyaNativeModLoop.EventObservation = new(
            ev.Id.ToString(), ev.GetType().Name, selectedOptions, selectedOption,
            "native/kaguya/event");
        KaguyaNativeModLoop.RewardObservation = new(
            requestedType is null ? "REWARD.KAGUYA_TAKETORI_OPTION" : "REWARD.KAGUYA_EVENT_PROBE", "event-result", player.Gold - KaguyaNativeModLoop.GoldBefore,
            cardId, "native/kaguya/event-result");
        Observe("kaguya-event-result", new
        {
            eventId = ev.Id.ToString(),
            option = selectedOption,
            card = cardId,
            goldBefore = KaguyaNativeModLoop.GoldBefore,
            goldAfter = player.Gold,
            hpBefore = before.GetProperty("hp").GetInt32(),
            hpAfter = after.GetProperty("hp").GetInt32()
        });
        Observe("kaguya-resource-ledger", new
        {
            goldBefore = KaguyaNativeModLoop.GoldBefore,
            goldAfter = player.Gold,
            delta = player.Gold - KaguyaNativeModLoop.GoldBefore,
            hpBefore = before.GetProperty("hp").GetInt32(),
            hpAfter = after.GetProperty("hp").GetInt32(),
            card = cardId,
            reward = KaguyaNativeModLoop.RewardObservation
        });
        if (!ev.IsFinished)
            throw new NotSupportedException("Kaguya event option did not finish the native event");
        RoomComplete("native Kaguya event result and cleanup boundary");
        Action("KaguyaTeardown", new { room = NativeIdentity.Ref(room), eventId = ev.Id.ToString() },
            () => Await(room.Exit(run), "Kaguya native event Exit"));
        Observe("kaguya-cleanup", new { room = NativeIdentity.Ref(room), currentRoom = run.CurrentRoom?.GetType().Name });
        Observe("kaguya-teardown", new { eventId = ev.Id.ToString(), registryIsolation = true, newRunRequired = true });
    }

    private static string? NewCardId(JsonElement before, JsonElement after)
    {
        if (!before.TryGetProperty("deck", out var beforeDeck) || !after.TryGetProperty("deck", out var afterDeck)) return null;
        var oldIds = beforeDeck.EnumerateArray().Select(x => x.TryGetProperty("model", out var m) ? m.GetString() : null)
            .Where(x => x is not null).ToList();
        return afterDeck.EnumerateArray()
            .Select(x => x.TryGetProperty("model", out var m) ? m.GetString() : null)
            .Where(x => x is not null)
            .FirstOrDefault(x => !oldIds.Remove(x!));
    }
}
