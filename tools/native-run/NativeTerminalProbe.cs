using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SimulatorLab.Core;

namespace OfflineSearchHarness;
internal static partial class Program
{
    static string WaitBoundary()
    {
        var deadline=DateTime.UtcNow.AddSeconds(45);
        while(true){
            NativeHost.Ensure();if(NativeRooms.Active)RoomBudget();
            if(NativeLifecycle.WinReturns>0&&NativeLifecycle.Won)return "RunVictory";
            if(NativeLifecycle.Losses>0&&!CombatManager.Instance.IsInProgress)return "Death";
            var combat=CombatManager.Instance.DebugOnlyGetState();
            if(combat!=null&&CombatManager.Instance.IsInProgress&&combat.CurrentSide==CombatSide.Player&&player!.PlayerCombatState?.Phase==PlayerTurnPhase.Play)return "Play";
            if(!CombatManager.Instance.IsInProgress&&run?.CurrentRoom is CombatRoom&&ReferenceEquals(NativeHost.LastVictoriousRoom,run.CurrentRoom))return "BattleVictory";
            if(run?.CurrentRoom is EventRoom)return "EventDecision";
            if(DateTime.UtcNow>deadline)throw new TimeoutException("Native lifecycle boundary");
            loop.Pump(TimeSpan.FromMilliseconds(10));
        }
    }
    static void TerminalStamp(string cause)
    {
        if(NativeLifecycle.WinReturns==1&&NativeLifecycle.RunEndReturns==1&&NativeLifecycle.Won)terminal=NativeRunTerminal.RunVictory;
        else if(NativeLifecycle.Losses==1&&player!.Creature.IsDead&&!CombatManager.Instance.IsInProgress)terminal=NativeRunTerminal.Death;
        else throw new InvalidOperationException("No completed native terminal authority");
        Observe("terminal-stamp",new{terminal=terminal.ToString(),cause,native=NativeLifecycle.Snapshot(),scope=provenance.ScenarioKind,retailLossFinalization=false});
        NativeLifecycle.Closed=true;int countBefore=dispatched;bool executed=false;
        bool refused=false;try{Action("EndTurn",new{turn=player!.PlayerCombatState?.TurnNumber},()=>executed=true);}catch(NativeActionRejectedException){refused=true;}
        Observe("terminal-contract",new{valid=refused&&!executed&&countBefore==dispatched&&dispatched==confirmed&&player!.Creature.IsDead,terminal=terminal.ToString(),staleRejected=refused,zeroAdditionalDispatch=countBefore==dispatched,executed});
        if(!refused||dispatched!=confirmed)throw new InvalidOperationException("Terminal contract");
    }
    static void TailProbe()
    {
        var manager=RunManager.Instance;int target=scenario=="tail-next-act"?0:run!.Acts.Count-1;
        Observe("fixture-setup-declared",new{name=scenario,fixtureKind="lifecycle",entry="fresh normal run objects after Neow",changes="EnterAct(target) creates a local act-ready precondition; prior Boss not executed; no resource/deck/HP/RNG edits",target});
        Action("FixtureTailSetup",new{target},()=>Await(manager.EnterAct(target,false),"Declared tail act setup"));
        int beforeAct=run!.CurrentActIndex,beforeMap=manager.MapSelectionSynchronizer.MapGenerationCount;
        var deck=player!.Deck.Cards.ToArray();int hp=player.Creature.CurrentHp,gold=player.Gold;
        Action("ActReady",new{act=beforeAct,map=beforeMap},()=>{
            NativeLifecycle.ActTask=null;manager.ActChangeSynchronizer.SetLocalPlayerReady();
            Await(manager.ActionExecutor.FinishedExecutingActions(),"Native ready vote");
            var deadline=DateTime.UtcNow.AddSeconds(45);
            while(NativeLifecycle.ActTask==null){NativeHost.Ensure();if(DateTime.UtcNow>deadline)throw new TimeoutException("Native act transition did not start");loop.Pump(TimeSpan.FromMilliseconds(10));}
            Await(NativeLifecycle.ActTask,"Actual native act transition");
        });
        if(scenario=="tail-next-act"){
            bool valid=run.CurrentActIndex==beforeAct+1&&manager.MapSelectionSynchronizer.MapGenerationCount>beforeMap&&player.Deck.Cards.SequenceEqual(deck,ReferenceEqualityComparer.Instance)&&player.Creature.CurrentHp==hp&&player.Gold==gold;
            var first=run.Map.StartingMapPoint;
            Observe("map-candidates",new{stage="next-act-first-decision",from=(string?)null,to=first.coord.ToString(),type=first.PointType.ToString(),mapGeneration=manager.MapSelectionSynchronizer.MapGenerationCount});
            // This slice stops at the request; it must not consume a future frozen root action.
            _=NativePolicy.Choose("ActOpeningTravel",[(object)new{to=first.coord.ToString(),type=first.PointType.ToString()}]);
            int started=NativeLifecycle.ActStarts;
            Action("OldActVoteControl",new{oldAct=beforeAct},()=>{
                manager.ActionQueueSynchronizer.RequestEnqueue(new VoteToMoveToNextActAction(player,beforeAct));Await(manager.ActionExecutor.FinishedExecutingActions(),"Native stale act vote");
            });
            bool stale=NativeLifecycle.ActStarts==started;
            Observe("tail-contract",new{valid=valid&&stale,kind="next-act",from=beforeAct,to=run.CurrentActIndex,beforeMap,afterMap=manager.MapSelectionSynchronizer.MapGenerationCount,staleIgnored=stale,firstDecision=first.coord.ToString(),room=run.CurrentRoom?.GetType().Name});
            if(!valid||!stale)throw new InvalidOperationException("Native next-act contract");
            return;
        }
        CompleteArchitect(fullRun:false);
    }
    static void CompleteArchitect(bool fullRun)
    {
        if(run!.CurrentRoom is not EventRoom room||room.CanonicalEvent is not TheArchitect)throw new InvalidOperationException("Native did not enter Architect");
        var ev=room.LocalMutableEvent;
        Action("EventRoomEnter",new{model=ev.Id.ToString()},()=>{ev.OnRoomEnter();Await(Task.WhenAll(NativeArchitectPresentation.DialogueTasks),"Actual native dialogue entry task");NativeHost.Ensure();});
        for(int n=0;n<32&&NativeLifecycle.WinReturns==0;n++){
            var options=ev.CurrentOptions.Select((o,i)=>new{index=i,o.IsLocked,o.IsProceed,o.TextKey}).Where(o=>!o.IsLocked).ToArray();
            if(options.Length==0)throw new NotSupportedException("Architect OnRoomEnter cleared all options; native UI-dependent dialogue continuation unavailable");
            Observe("tail-event-candidates",options);
            var option=options[PolicyIndex("ArchitectOption",options.Select(o=>(object)new{model=ev.Id.ToString(),o.index,o.TextKey}).ToArray())];
            Action("ArchitectOption",new{model=ev.Id.ToString(),option.index,option.TextKey},()=>{
                var actual=ev.CurrentOptions[option.index];
                Await(actual.Chosen(),"Native Architect option");NativeHost.Ensure();
            });
        }
        if(NativeLifecycle.WinReturns!=1)throw new NotSupportedException("Architect option budget ended without completed native WinRun");
        TerminalStamp("native Architect option -> WinRun completion, after forced player cleanup");
        Observe("tail-contract",new{valid=ev.IsFinished&&NativeLifecycle.ActReturns==(fullRun?3:1)&&NativeLifecycle.PlatformRefreshes==1,kind="architect",eventFinished=ev.IsFinished,normalStartFullRun=fullRun});
        if(!ev.IsFinished||NativeLifecycle.PlatformRefreshes!=1)throw new InvalidOperationException("Architect completion contract");
    }
}
