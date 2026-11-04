using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
namespace OfflineSearchHarness;

internal static partial class Program
{
 // Declining transactions is a policy decision. The original subsequent travel
 // owns EventRoom.Exit; no transaction or fake completion is dispatched here.
 static void RecordCustomMerchantDecline()
 {
  if(!NativeNaturalBoss.Third||run!.CurrentRoom is not EventRoom||RunManager.Instance.EventSynchronizer.GetLocalEvent() is not FakeMerchant ev)return;
  if(!ev.IsFinished||ev.StartedFight||ev.CurrentOptions.Count!=0||!Hook.ShouldProceedToNextMapPoint(run))throw new NotSupportedException("FakeMerchant decline requires native finished/proceed state without a fight");
  string stamp=StateStamp();
  var offered=ev.Inventory.RelicEntries.Select(e=>{var model=e.Model??throw new NativeDataException("Custom merchant stock has no actual model");return new{reference=NativeIdentity.Ref(model),model=model.Id.ToString(),stocked=e.IsStocked};}).ToArray();
  if(StateStamp()!=stamp)throw new NativeDataException("Custom merchant inspection changed native state/RNG");
  Observe("natural-event-declined",new{room=NativeIdentity.Ref(run.CurrentRoom),model=NativeIdentity.Ref(ev),policy="leave-without-transactions",offered,purchases=0,pricesEvaluated=false,nativeProceedAllowed=true});
 }
}
