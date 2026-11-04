using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SimulatorLab.Core;
namespace OfflineSearchHarness;
internal static partial class Program
{
    static void NaturalMerchant()
    {
        for(int n=0;n<12;n++){
            if(n>0&&!roomComplete)throw new NativeActionRejectedException("Unfinished natural room");
            int Distance(MapPoint p,HashSet<MapPoint> seen){if(!seen.Add(p))return 1000;if(p.PointType==MapPointType.Shop)return 0;return p.Children.Count==0?1000:1+p.Children.Min(x=>Distance(x,new(seen)));}
            var current=run!.CurrentMapPoint!;
            var children=current.Children.OrderBy(p=>Distance(p,[])).ThenBy(p=>p.PointType==MapPointType.Monster?0:1).ThenBy(p=>p.coord.col).ToArray();
            if(children.Length==0)throw new NotSupportedException("Natural merchant has no legal child");
            var next=children[replayPath!=null?PolicyIndex("Travel",children.Select(p=>(object)new{from=current.coord.ToString(),to=p.coord.ToString()}).ToArray()):0];
            Observe("map-candidates",children.Select(p=>new{node=p.coord.ToString(),type=p.PointType.ToString()}).ToArray());
            if(!Hook.ShouldProceedToNextMapPoint(run))throw new NativeActionRejectedException("Native map travel forbidden");
            Action("Travel",new{from=current.coord.ToString(),to=next.coord.ToString()},()=>{
                if(sessionRoom!=null)Observe("room-exit-start",new{session=Session,room=NativeIdentity.Ref(sessionRoom)});
                Await(RunManager.Instance.EnterMapCoord(next.coord),"Natural merchant approach");
                if(run.CurrentRoom is EventRoom)EnterAuditedEvent();
                if(sessionRoom!=null)Observe("room-exit-return",new{session=Session,room=NativeIdentity.Ref(sessionRoom)});
            });
            if(sessionRoom!=null)Observe("room-complete",new{session=Session,room=NativeIdentity.Ref(sessionRoom),reason="native travel"});
            RoomArrived();
            if(run.CurrentRoom is MerchantRoom merchant){
                MAction("MerchantAttach",new{room=NativeIdentity.Ref(merchant)},()=>{
                    MerchantRoom=merchant;NativeMerchant.Inventory=merchant.GetLocalInventory();
                    return new{nativeInventory=NativeIdentity.Ref(NativeMerchant.Inventory),initialRemovalCost=NativeMerchant.Inventory.CardRemovalEntry!.Cost};
                });
                var entry=NativeMerchantExpansion.Natural?NativeMerchant.Inventory!.RelicEntries.Where(e=>e.Model?.HasUponPickupEffect==false&&e.Cost<=player!.Gold).OrderBy(e=>e.Cost).Cast<MerchantEntry>().FirstOrDefault():NativeMerchant.Inventory!.CardEntries.Where(e=>e.IsStocked&&e.Cost<=player!.Gold).OrderBy(e=>e.Cost).Cast<MerchantEntry>().FirstOrDefault();
                if(entry==null)throw new NotSupportedException("No affordable supported item in natural merchant; no resource injection");
                MerchantBuy(entry);
                // An actual cancellable native selection, if affordable, not an assumed free removal.
                if(!NativeMerchantExpansion.Active&&player!.Gold>=NativeMerchant.Inventory.CardRemovalEntry!.Cost)MerchantRemove(true);
                RoomComplete("native merchant decision complete");
                var from=run.CurrentMapPoint!;
                var legal=from.Children.OrderBy(p=>p.PointType==MapPointType.Monster?0:1).ThenBy(p=>p.coord.col).ToArray();
                if(legal.Length==0)throw new NotSupportedException("Merchant has no legal exit node");
                var dest=legal[replayPath!=null?PolicyIndex("MerchantTravel",legal.Select(p=>(object)new{from=from.coord.ToString(),to=p.coord.ToString()}).ToArray()):0];
                Observe("map-candidates",legal.Select(p=>new{node=p.coord.ToString(),type=p.PointType.ToString()}).ToArray());
                if(!Hook.ShouldProceedToNextMapPoint(run))throw new NativeActionRejectedException("Merchant leave hook refused");
                MAction("MerchantTravel",new{from=from.coord.ToString(),to=dest.coord.ToString()},()=>{
                    Observe("room-exit-start",new{session=Session,room=NativeIdentity.Ref(sessionRoom)});
                    Await(RunManager.Instance.EnterMapCoord(dest.coord),"Actual merchant exit via natural travel");
                    Observe("room-exit-return",new{session=Session,room=NativeIdentity.Ref(sessionRoom)});
                    return new{closed=NativeMerchant.Closed,from=from.coord.ToString(),to=dest.coord.ToString()};
                });
                Observe("room-complete",new{session=Session,room=NativeIdentity.Ref(sessionRoom),reason="native merchant travel completed"});
                RoomArrived();
                if(run.CurrentRoom is not CombatRoom)throw new NotSupportedException("Natural merchant next decision is not combat");
                Ready();Observe("next-combat-first-play");
                var pcs=player!.PlayerCombatState!;var combat=MegaCrit.Sts2.Core.Combat.CombatManager.Instance.DebugOnlyGetState()!;
                var actions=(from card in pcs.Hand.Cards from target in new MegaCrit.Sts2.Core.Entities.Creatures.Creature?[]{null}.Concat(combat.Creatures.Cast<MegaCrit.Sts2.Core.Entities.Creatures.Creature?>()) where card.CanPlayTargeting(target) select (card,target)).ToArray();
                var play=actions[PolicyIndex("PlayCard",actions.Select(x=>(object)new{card=Id(x.card),model=x.card.Id.ToString(),target=x.target==null?null:Id(x.target)}).ToArray())];
                Action("PlayCard",new{card=Id(play.card),model=play.card.Id.ToString(),target=play.target==null?null:Id(play.target)},()=>{
                    if(!play.card.TryManualPlay(play.target))throw new NativeActionRejectedException("Next battle rejected natural play");
                    Await(RunManager.Instance.ActionExecutor.FinishedExecutingActions(),"Next battle actual first card");
                });
                Observe("n3d-contract",new{valid=true,scenario,fixture=false,operations=merchantOps,scope="natural merchant purchase, original exit and actual next battle card"});return;
            }
            if(run.CurrentRoom is CombatRoom){Ready();Battle();if(terminal==NativeRunTerminal.Death)return;roomVictories++;Rewards();RoomComplete("native combat and rewards complete");}
            else if(run.CurrentRoom is RestSiteRoom){SelectRoomOption(0);RoomComplete("native rest complete");}
            else if(run.CurrentRoom is EventRoom){var ev=RunManager.Instance.EventSynchronizer.GetLocalEvent();SelectRoomOption(ev is MegaCrit.Sts2.Core.Models.Events.DenseVegetation?0:1);RoomComplete("native event complete");}
            else throw new NotSupportedException("Natural merchant approach node unsupported: "+run.CurrentRoom!.GetType().Name);
        }
        throw new TimeoutException("Natural merchant twelve-node budget");
    }
}
