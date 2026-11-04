using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.TreasureRelicPicking;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
namespace OfflineSearchHarness;
internal static partial class Program
{
    static void TreasureC(bool skip)
    {
        var manager=RunManager.Instance;var room=(TreasureRoom)run!.CurrentRoom!;var sync=manager.TreasureRoomRelicSynchronizer;
        var candidates=sync.CurrentRelics?.ToArray()??throw new NativeDataException("Native treasure did not begin picking on Enter");
        CAction("TreasureOpen",new{room=NativeIdentity.Ref(room)},()=>{
            var normal=NativeRewards.Begin("treasure-normal",room);int before=player!.Gold;
            if(NativeFullRun.Active)NativeTreasureLedger.Begin(player);
            var task=room.DoNormalRewards();try{Await(task,"Native normal chest rewards");}catch{NativeTreasureLedger.Clear();throw;}
            int gold=task.GetAwaiter().GetResult();
            // This fixture has no gold modifier or Spoils Map quest. Modified accounting is a separate future profile.
            if(NativeFullRun.Active){try{NativeRewards.End(normal,NativeTreasureLedger.Complete(gold,before));}finally{NativeTreasureLedger.Clear();}}
            else if(player.Gold-before!=gold)throw new NotSupportedException("Treasure modified gold accounting outside declared unmodified profile");
            else if(scenario=="closure-spoils")NativeRewards.End(normal,new{gold,accounting="native-spoils-quest",actualGoldDelta=player.Gold-before,questGold=NativeRewardQuest.Gold,baseGold=gold-NativeRewardQuest.Gold,quests=NativeRewardQuest.Completed});
            else NativeRewards.End(normal,new{gold,accounting="native-unmodified",actualGoldDelta=player.Gold-before});
            var extra=NativeRewards.Begin("treasure-extra",room);Await(room.DoExtraRewardsIfNeeded(),"Native extra chest rewards");NativeRewards.End(extra,new{completed=true});
        });
        Task? obtain=null;int notifications=0;
        async Task Obtain(List<RelicPickingResult> results){
            foreach(var result in results.Where(x=>ReferenceEquals(x.player,player))){
                var op=NativeRewards.Begin("treasure-obtain",result.relic);
                var created=result.relic.ToMutable();var gained=await RelicCmd.Obtain(created,player!);
                if(!player!.Relics.Contains(gained))throw new NativeDataException("Treasure notification without native acquisition");
                if(!ReferenceEquals(created,gained))throw new NotSupportedException("Treasure relic replacement outside same-instance profile");
                NativeRewards.End(op,new{canonical=NativeIdentity.Ref(result.relic),created=NativeIdentity.Ref(created),acquired=NativeIdentity.Ref(gained),model=gained.Id.ToString(),identityMode="same-instance"});
            }
        }
        void Award(List<RelicPickingResult> results){if(++notifications!=1)throw new NativeDataException("Duplicate treasure award notification");obtain=Obtain(results);}
        sync.RelicsAwarded+=Award;
        try{
            CAction("TreasurePick",new{room=NativeIdentity.Ref(room)},()=>{
                var opts=candidates.Select((x,i)=>new NativeRewards.Option("o"+i,"Relic",NativeIdentity.Ref(x),x.Id.ToString())).Append(new("o"+candidates.Length,"SkipTreasure",null)).ToArray();
                var op=NativeRewards.Begin("treasure-choice",room,opts,skip?candidates.Length:0);int choice=Array.FindIndex(opts,x=>x.Handle==op.Handle);
                if(choice==candidates.Length)sync.SkipRelicLocally();else sync.PickRelicLocally(choice);
                Await(manager.ActionExecutor.FinishedExecutingActions(),"Native treasure vote");if(obtain!=null)Await(obtain,"Native treasure relic acquisition");
                if(!skip&&notifications!=1)throw new NativeDataException("Treasure native distribution incomplete");
                NativeRewards.End(op,new{skip,notifications});
            });
        }finally{sync.RelicsAwarded-=Award;}
        if(NativePreparation.Active||NativeRepeatTreasure.Active)return; // Original manager transition owns the single native Exit.
        CAction("TreasureExit",new{room=NativeIdentity.Ref(room)},()=>{var op=NativeRewards.Begin("treasure-exit",room);Await(room.Exit(run),"Original treasure exit");NativeRewards.End(op,new{pickingClosed=sync.CurrentRelics==null});});
        if(sync.CurrentRelics!=null)throw new NativeDataException("Treasure session remained open after native Exit");
    }
}
