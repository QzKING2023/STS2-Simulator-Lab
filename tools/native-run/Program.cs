using System.Security.Cryptography;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Timeline;
using SimulatorLab.Core;

namespace OfflineSearchHarness;
internal static partial class Program
{
    internal static readonly JsonSerializerOptions Json=new(){WriteIndented=true,IncludeFields=true,Converters={new JsonStringEnumConverter()}};
    static readonly Dictionary<object,string> Ids=new(ReferenceEqualityComparer.Instance);
    static readonly List<object> Events=[];
    static long sequence;
    static long eventSequence;
    static int dispatched,confirmed;
    static string last="boot";
    static RunState? run;
    internal static object? BoundaryRoom=>run?.CurrentRoom;
    static Player? player;
    static MainLoopContext loop=null!;
    static string output="";
    static string? replayPath;
    static JsonElement[] replay=[];
    static int replayIndex;
    static NativeRunTerminal terminal=NativeRunTerminal.Unknown;
    static string Id(object o)=>NativeIdentity.Get(o);
    internal static string StateStamp()=>Hash(State().GetRawText());
    private static RunState CreateRunAndWriteProfile(string seed)
    {
        var unlock = SaveManager.Instance.GenerateUnlockStateFromProgress();
        Write("profile.json",new{id="vanilla-all-epochs-zero-runs/v1",unlock=unlock.ToSerializable(),progress=SaveManager.Instance.Progress.ToSerializable()});
        NativeEvidence.Emit("profile-lock",new{sha256=Hash(File.ReadAllText(Path.Combine(output,"profile.json")))});
        var character = NativePerformancePilot.Select(scenario);
        var player = Player.CreateForNewRun(character, unlock, 1uL);
        var acts = ActModel.GetDefaultList().Select(a => a.ToMutable()).ToList();
        return RunState.CreateForNewRun([player], acts, [], GameMode.Standard, 0, seed);
    }
    static string scenario="slice-skip";
    static NativeScenario provenance=null!;
    static IDisposable? selectorScope;
    internal static bool CombatActionActive;
    internal static bool InThirdAct=>run?.CurrentActIndex==2;
    internal static bool InSecondAct=>run?.CurrentActIndex>0;
    internal static IReadOnlyList<object> WatcherEvents => Events.ToArray();
    internal static void ReleaseForTeardown()
    {
        ActionExecutor? actionExecutor = null;
        {
            // RunManager.CleanUp resets ActionQueueSet, which raises
            // ActionQueueChanged.  A newly-created run starts with its
            // ActionExecutor paused, so that reset would otherwise leave an
            // ExecuteActions task parked forever in WaitForUnpause and keep
            // the run's Player graph (including collectible mod types) alive.
            try
            {
                actionExecutor = RunManager.Instance.ActionExecutor;
                actionExecutor?.Unpause();
                actionExecutor?.Cancel();
            }
            catch { }
            try { RunManager.Instance.CleanUp(graceful: false); } catch { }
            // CleanUp resets ActionQueueSet and raises ActionQueueChanged,
            // which can start a fresh executor task after the first cancel.
            // Stop that post-reset task as well before releasing the graph.
            try { actionExecutor?.Unpause(); actionExecutor?.Cancel(); } catch { }
            try
            {
                var task = actionExecutor?.FinishedExecutingActions();
                if (task is { IsCompleted: false }) task.Wait(TimeSpan.FromSeconds(2));
            }
            catch { }
            try { actionExecutor?.Cancel(); } catch { }
            try { NativeFoundationModLoop.ReleaseRunGraphForTeardown(); } catch { }
        }
        SynchronizationContext.SetSynchronizationContext(null);
        if (NativeFoundationModLoop.Enabled)
        {
            try { NativeFoundationModLoop.ReleasePlayerForTeardown(player); } catch { }
        }
        run = null;
        player = null;
        try { loop?.ReleaseForTeardown(); } catch { }
        loop = null!;
        // The foundation model augmenter is a host-owned delegate. It does
        // not belong in the post-run state graph once ModelDb has been reset.
        GameBootstrap.ModelTypeAugmenter = null;
        Ids.Clear();
        Events.Clear();
        Route.Clear();
        NativeIdentity.ReleaseForTeardown();
        NativeChoices.ReleaseForTeardown();
        NativeLifecycle.ReleaseForTeardown();
        NativeHost.ReleaseForTeardown();
    }
    static object CardState(CardModel c)=>NativeNaturalBoss.CardRecords?(object)new{id=Id(c),model=c.Id.ToString(),upgrade=c.CurrentUpgradeLevel,deck=c.DeckVersion==null?null:Id(c.DeckVersion),native=c.ToSerializable()}:new{id=Id(c),model=c.Id.ToString(),upgrade=c.CurrentUpgradeLevel,deck=c.DeckVersion==null?null:Id(c.DeckVersion)};
    internal static object PermanentCardState(CardModel c)=>(NativeMerchantEconomy.Active||NativeRewardClosure.Active||NativeNaturalBoss.CardRecords)?(object)new{id=Id(c),model=c.Id.ToString(),upgrade=c.CurrentUpgradeLevel,native=c.ToSerializable()}:new{id=Id(c),model=c.Id.ToString(),upgrade=c.CurrentUpgradeLevel};
    static object EnemyState(Creature e)=>NativeMajor.Active?(object)new{id=Id(e),hp=e.CurrentHp,block=e.Block,model=e.Monster?.Id.ToString(),alive=e.IsAlive,primary=e.IsPrimaryEnemy,powers=e.Powers.Select(p=>new{id=Id(p),model=p.Id.ToString(),amount=p.Amount}).ToArray()}:new{id=Id(e),hp=e.CurrentHp,block=e.Block,model=e.Monster?.Id.ToString(),powers=e.Powers.Select(p=>new{id=Id(p),model=p.Id.ToString(),amount=p.Amount}).ToArray()};
    static int PolicyIndex(string kind,object[] candidates)
    {
        if(candidates.Length==0)throw new NotSupportedException("No legal root candidate");
        if(branchNext!=null){for(int i=0;i<candidates.Length;i++)if(NativeDecisionTicket.SameActionJson(branchNext.Action.GetRawText(),JsonSerializer.Serialize(new{kind,decision=candidates[i]},Json)))return i;throw new NativeActionRejectedException("Forced branch candidate disappeared");}
        if(replayPath==null)return NativePolicy.Choose(kind,candidates);
        if(replayIndex>=replay.Length)throw new NativeActionRejectedException("Missing root action");
        string action=replay[replayIndex].GetProperty("action").GetRawText();
        for(int i=0;i<candidates.Length;i++)if(NativeDecisionTicket.SameActionJson(action,JsonSerializer.Serialize(new{kind,decision=candidates[i]},Json)))return i;
        throw new NativeActionRejectedException("Frozen action is not a legal current candidate");
    }
    static string Hash(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    static void Write(string name,object value){using var timing=NativeTiming.Measure("compatibility-json-write");File.WriteAllText(Path.Combine(output,name),JsonSerializer.Serialize(value,Json));}
    internal static JsonElement State(){using var timing=NativeTiming.Measure("identity-and-state-projection");var state=JsonSerializer.SerializeToElement(new{
        act=run?.CurrentActIndex,floor=run?.ActFloor,node=run?.CurrentMapPoint?.coord.ToString(),room=run?.CurrentRoom?.GetType().Name,
        hp=player?.Creature.CurrentHp,maxHp=player?.Creature.MaxHp,gold=player?.Gold,block=player?.Creature.Block,
        runRng=run?.Rng.ToSerializable(),playerRng=player?.PlayerRng.ToSerializable(),eventRng=EventRng(),powers=player?.Creature.Powers.Select(p=>new{id=Id(p),model=p.Id.ToString(),amount=p.Amount}).ToArray(),
        deck=player?.Deck.Cards.Select(PermanentCardState).ToArray(),
        relics=player?.Relics.Select(r=>new{id=Id(r),model=r.Id.ToString()}).ToArray(),
        potions=player?.Potions.Select(p=>p==null?null:new{id=Id(p),model=p.Id.ToString()}).ToArray(),
        combat=player?.PlayerCombatState is {} pcs?new{turn=pcs.TurnNumber,round=CombatManager.Instance.DebugOnlyGetState()?.RoundNumber,side=CombatManager.Instance.DebugOnlyGetState()?.CurrentSide.ToString(),energy=pcs.Energy,phase=pcs.Phase.ToString(),
          hand=pcs.Hand.Cards.Select(CardState).ToArray(),
          draw=pcs.DrawPile.Cards.Select(CardState).ToArray(),discard=pcs.DiscardPile.Cards.Select(CardState).ToArray(),exhaust=pcs.ExhaustPile.Cards.Select(CardState).ToArray(),
          enemies=CombatManager.Instance.DebugOnlyGetState()?.Enemies.Select(EnemyState).ToArray()}:null
    },Json);
        if(!NativeLifecycle.Active)return state;
        var node=System.Text.Json.Nodes.JsonNode.Parse(state.GetRawText())!.AsObject();
        var eventSynchronizer = run is null ? null : RunManager.Instance.EventSynchronizer;
        var ev=run?.CurrentRoom is EventRoom ? eventSynchronizer?.Events.FirstOrDefault(e=>ReferenceEquals(e.Owner,player)) : null;
        node["lifecycle"]=JsonSerializer.SerializeToNode(new{native=NativeLifecycle.Snapshot(),mapGeneration=run is null ? 0 : RunManager.Instance.MapSelectionSynchronizer?.MapGenerationCount ?? 0,eventModel=ev==null?null:new{reference=NativeIdentity.Ref(ev),model=ev.Id.ToString(),rng=ev.Rng.ToSerializable(),ev.IsFinished}},Json);
        if(NativeRooms.Active||NativeRepeatTreasure.Active)node["rooms"]=JsonSerializer.SerializeToNode(RoomSnapshot(),Json);
        if(NativeMerchant.Active)node["merchant"]=JsonSerializer.SerializeToNode(NativeMerchant.Snapshot(),Json);
        if(NativeMerchantEconomy.Active||NativeRewardClosure.Active)node["mapHistory"]=JsonSerializer.SerializeToNode(NativeMerchantEconomy.History(run),Json);
        if(NativeMerchantExpansion.Active)node["transactionRelics"]=JsonSerializer.SerializeToNode(NativeMerchantExpansion.RelicStates(player),Json);
        if(NativeRewardProbe.Active&&player!=null)node["inventory"]=JsonSerializer.SerializeToNode(NativeRewards.Inventory(player),Json);
        if(NativeMajor.Active&&run?.CurrentRoom is CombatRoom majorRoom)node["majorCombat"]=JsonSerializer.SerializeToNode(new{room=NativeIdentity.Ref(majorRoom),encounter=majorRoom.Encounter.Id.ToString(),type=majorRoom.Encounter.RoomType.ToString(),shouldGiveRewards=majorRoom.Encounter.ShouldGiveRewards,inProgress=CombatManager.Instance.IsInProgress,intentAttackers=CombatManager.Instance.DebugOnlyGetState()?.Enemies.Count(x=>x.Monster?.IntendsToAttack==true)},Json);
        if(NativeNaturalBoss.Active)node["naturalMap"]=JsonSerializer.SerializeToNode(NaturalMapSnapshot(),Json);
        return JsonSerializer.SerializeToElement(node,Json);
    }
    internal static void Observe(string kind,object? detail=null){var state=State();NativeEvidence.Emit(kind,new{facts=detail,state});Events.Add(new{seq=++eventSequence,boundarySequence=sequence,kind,detail,state});if(!NativeMajor.Active)Write("events.json",Events);}
    static void Action(string kind,object decision,Action execute)
    {
        if(NativeLifecycle.Active&&NativeLifecycle.Closed)throw new NativeActionRejectedException("Native run terminal is closed; action dispatch refused");
        NativeHost.Ensure();NativeFullRun.Check(dispatched);
        if(NativeSolverRun.Active&&dispatched>=160)throw new TimeoutException("Solver run total action budget");
        if(NativeRooms.Active)RoomBudget();
        string before=State().GetRawText();
        var selected=JsonSerializer.SerializeToElement(new{kind,decision},Json);
        if(replayPath!=null){if(replayIndex>=replay.Length || !NativeDecisionTicket.SameActionJson(replay[replayIndex].GetProperty("action").GetRawText(),selected.GetRawText()))throw new NativeActionRejectedException("Frozen action mismatch at "+replayIndex);}
        var ticket=new NativeDecisionTicket(run!,sequence,Hash(before),selected.GetRawText());
        ticket.Consume(run!,sequence,Hash(State().GetRawText()),selected.GetRawText());
        NativeEvidence.Root="a"+(sequence+1);NativeEvidence.Context=(run?.CurrentActIndex+":"+run?.CurrentMapPoint?.coord+":"+Id(run?.CurrentRoom??(object)run!));
        NativeEvidence.Emit("root-request",new{action=selected,before=Hash(before),oracle=true});
        dispatched++; Observe("dispatch",selected);
        CombatActionActive=kind is "PlayCard" or "EndTurn" or "UsePotion";
        try{using var nativeTiming=NativeTiming.Measure("native-root-execution");execute(); NativeHost.Ensure();}finally{CombatActionActive=false;}
        var after=State();
        var entry=new{action=selected,before=Hash(before),after=Hash(after.GetRawText()),confirmed=confirmed+1,dispatched};
        if(replayPath!=null && (replay[replayIndex].GetProperty("before").GetString()!=entry.before || replay[replayIndex].GetProperty("after").GetString()!=entry.after))throw new InvalidOperationException("Frozen boundary mismatch at "+replayIndex);
        confirmed++; sequence++; last=kind+":"+sequence;
        replayIndex++; Route.Add(entry);Write("route.json",Route);Write("choices.json",NativeChoices.Transcript);Observe("confirmed",entry);BranchAfterConfirm(kind);
    }
    static readonly List<object> Route=[];
    static int completedNaturalBattles;
    static void Await(Task task,string name){using var timing=NativeTiming.Measure("native-await-and-pump");loop.RunUntilCompleted(task,NativeRooms.Active?RoomWaitBudget():TimeSpan.FromSeconds(45),name);}
    static void Ready()
    {
        if(NativeLifecycle.Active){var boundary=WaitBoundary();if(boundary!="Play")throw new InvalidOperationException("Expected Play, actual "+boundary);return;}
        var end=DateTime.UtcNow.AddSeconds(45);
        while(true){NativeHost.Ensure();var c=CombatManager.Instance.DebugOnlyGetState();
            if(c!=null&&CombatManager.Instance.IsInProgress&&c.CurrentSide==CombatSide.Player&&player!.PlayerCombatState?.Phase==PlayerTurnPhase.Play)return;
            if(player!.Creature.CurrentHp<=0)throw new InvalidOperationException("Player died before requested Play boundary");
            if(DateTime.UtcNow>end)throw new TimeoutException("Playable combat boundary");loop.Pump(TimeSpan.FromMilliseconds(10));}
    }
    static void Travel(bool preferCombat=false)
    {
        if(!MegaCrit.Sts2.Core.Hooks.Hook.ShouldProceedToNextMapPoint(run!)) throw new NotSupportedException("Native Hook forbids map travel");
        var current=run!.CurrentMapPoint!;
        var choices=current.Children.OrderBy(p=>p.coord.col).ThenBy(p=>p.coord.row).ToArray();
        if(preferCombat){var combat=choices.Where(p=>p.PointType is MegaCrit.Sts2.Core.Map.MapPointType.Monster or MegaCrit.Sts2.Core.Map.MapPointType.Elite or MegaCrit.Sts2.Core.Map.MapPointType.Boss).ToArray();if(combat.Length>0)choices=combat;}
        if(choices.Length==0)throw new NotSupportedException("No child node in slice");
        var next=choices[PolicyIndex("Travel",choices.Select(x=>(object)new{from=current.coord.ToString(),to=x.coord.ToString()}).ToArray())];
        Observe("map-candidates",choices.Select(p=>new{node=p.coord.ToString(),type=p.PointType.ToString()}).ToArray());
        Action("Travel",new{from=current.coord.ToString(),to=next.coord.ToString()},()=>Await(RunManager.Instance.EnterMapCoord(next.coord),"Native map travel"));
    }
    static void Opening()
    {
        if(run!.CurrentRoom is not EventRoom)throw new InvalidOperationException("Expected natural Neow opening");
        for(int n=0;n<12;n++){
            var ev=RunManager.Instance.EventSynchronizer.GetLocalEvent();
            if(ev.IsFinished){Observe("opening-finished",ev.Id.ToString());return;}
            var choices=ev.CurrentOptions.Select((o,i)=>new{index=i,o.TextKey,o.IsLocked,o.IsProceed}).ToArray();
            Observe("event-candidates",choices);
            var legal=choices.Where(o=>!o.IsLocked).ToArray();
            var choice=legal[PolicyIndex("EventOption",legal.Select(o=>(object)new{model=ev.Id.ToString(),o.index,o.TextKey}).ToArray())];
            void Execute(){
                RunManager.Instance.EventSynchronizer.ChooseLocalOption(choice.index);
                Await(RunManager.Instance.EventSynchronizer.AwaitPendingOptionTasks(),"Native event choice");
            }
            var decision=new{model=ev.Id.ToString(),choice.index,choice.TextKey};
            if(NativeRewardProbe.Active)CAction("EventOption",decision,Execute);else Action("EventOption",decision,Execute);
        }
        throw new NotSupportedException("Opening decision limit");
    }
    static void Battle()
    {
        Ready(); var manager=RunManager.Instance;var c=CombatManager.Instance.DebugOnlyGetState()!;
        if(NativeSolverJourney.Active){
            Observe("first-battle-opening",new{encounter=((CombatRoom)run!.CurrentRoom!).Encounter.Id.ToString()});
            SolverJourneyBattle();
            if(terminal==NativeRunTerminal.Death){Observe("death");TerminalStamp("native solver ProcessPendingLoss completed; trigger source only where observed");}
            else Observe("battle-victory-cleanup",new{historyEntries=CombatManager.Instance.History.Entries.Count()});
            return;
        }
        int victoriesBefore=NativeHost.BattleVictories;
        Observe("first-battle-opening",new{encounter=((CombatRoom)run!.CurrentRoom!).Encounter.Id.ToString()});
        int actionCount=0;
        while(CombatManager.Instance.IsInProgress){
            if(++actionCount>1000)throw new TimeoutException("Combat decision limit");
            var pcs=player!.PlayerCombatState!;
            if(pcs.Phase!=PlayerTurnPhase.Play || c.CurrentSide!=CombatSide.Player)throw new InvalidOperationException("Not a player decision boundary");
            if(NativeBranchSearch.Active)BranchBeforeDecision((from card in pcs.Hand.Cards from target in new Creature?[]{null}.Concat(c.Creatures.Cast<Creature?>()) where card.CanPlayTargeting(target) select (card,target)).ToArray());
            if(NativePreparation.Active)PreparedPotions();
            if(!CombatManager.Instance.IsInProgress)break;
            var candidates=(from card in pcs.Hand.Cards
                            from target in new Creature?[]{null}.Concat(c.Creatures.Cast<Creature?>())
                            where card.CanPlayTargeting(target)
                            select (card,target)).ToArray();
            if(NativeEventFlow.Aggressive||NativeMajor.Active||KaguyaNativeModLoop.Enabled)candidates=candidates.OrderByDescending(x=>x.card.Type==MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack).ThenByDescending(x=>x.card.Id.ToString()=="CARD.BASH").ToArray();
            if(NativeMajor.Active&&c.Enemies.Any(x=>x.Monster?.IntendsToAttack==true)&&player!.Creature.Block<10)candidates=candidates.OrderByDescending(x=>x.card.Id.ToString()=="CARD.DEFEND_IRONCLAD").ToArray();
            if(NativePreparation.Active)candidates=PreparedCandidates(candidates);
            Observe("combat-candidates",candidates.Select(x=>new{card=Id(x.card),model=x.card.Id.ToString(),target=x.target==null?null:Id(x.target)}).ToArray());
            bool deathPolicy=scenario.StartsWith("natural-death") && !(scenario=="natural-death-after-reward" && completedNaturalBattles==0);
            bool frozenEndTurn=branchNext?.Action.GetProperty("kind").GetString()=="EndTurn"||replayPath!=null&&replayIndex<replay.Length&&replay[replayIndex].GetProperty("action").GetProperty("kind").GetString()=="EndTurn";
            if(candidates.Length>0&&!deathPolicy&&!frozenEndTurn){var choice=candidates[PolicyIndex("PlayCard",candidates.Select(x=>(object)new{card=Id(x.card),model=x.card.Id.ToString(),target=x.target==null?null:Id(x.target)}).ToArray())];
                Action("PlayCard",new{card=Id(choice.card),model=choice.card.Id.ToString(),target=choice.target==null?null:Id(choice.target)},()=>{
                    if(branchNext!=null)NativeBranchSearch.PendingExhaust=branchNext.Exhaust;
                    PrepareTestSubjectChoice(choice.card);
                    if(!choice.card.TryManualPlay(choice.target))throw new InvalidOperationException("Native rejected card");
                    Await(manager.ActionExecutor.FinishedExecutingActions(),"Native card action");
                    if(scenario=="major-action-fault")throw new InvalidOperationException("labelled major fault after actual native card task, unconfirmed root");
                });
            }else{
                int turn=pcs.TurnNumber;
                Action("EndTurn",new{turn},()=>{
                    CombatManager.Instance.OnEndedTurnLocally();
                    manager.ActionQueueSynchronizer.RequestEnqueue(new EndPlayerTurnAction(player,turn));
                    Await(manager.ActionExecutor.FinishedExecutingActions(),"Native enemy phase");
                    if(scenario=="natural-death-fault")throw new InvalidOperationException("labelled fault after actual enemy turn; root unconfirmed");
                });
            }
            if(NativeLifecycle.Active&&WaitBoundary()=="Death"){Observe("death");TerminalStamp("native ProcessPendingLoss completed at a safe boundary; damage trigger source recorded only where available");return;}
            if((scenario=="natural-death-budget"||scenario=="major-action-budget")&&actionCount==1)throw new TimeoutException("labelled one confirmed enemy turn budget");
            if(!NativeLifecycle.Active&&player.Creature.CurrentHp<=0){terminal=NativeRunTerminal.Death;Observe("death");return;}
        }
        if(NativeHost.BattleVictories-victoriesBefore!=1)throw new InvalidOperationException("Combat stopped without actual native Victory callback");
        completedNaturalBattles++;
        Observe("battle-victory-cleanup",new{historyEntries=CombatManager.Instance.History.Entries.Count()});
    }
    static void Rewards()
    {
        var manager=RunManager.Instance;int offers=0;int goldBefore=player!.Gold;
        var previous=RewardsSet.testSelector;
        try{RewardsSet.testSelector=async set=>{
            offers++;
            Observe("reward-offer",set.Rewards.Select((r,i)=>new{index=i,kind=r.GetType().Name,r.IsPopulated,amount=r is GoldReward gold?(int?)gold.Amount:null}).ToArray());
            foreach(var reward in set.Rewards.Where(r=>r is GoldReward)){
                Action("TakeGold",new{setId=set.Id,index=set.Rewards.ToList().IndexOf(reward)},()=>Await(manager.RewardsSetSynchronizer.SelectLocalReward(reward),"Native gold collection"));
            }
            if(scenario=="slice-take"||WatcherNativeModLoop.Enabled||KaguyaNativeModLoop.Enabled||NativeEventFlow.Active&&!NativeEventFlow.Natural){
                var reward=set.Rewards.OfType<CardReward>().Single();
                if(!NativeChoices.Replaying)NativeChoices.Planned.Enqueue([NativeEventFlow.Active?Math.Max(0,reward.Cards.ToList().FindIndex(c=>c.Type==MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack)):1]);
                var original=player.Deck.Cards.ToArray();
                Action("TakeCard",new{setId=set.Id,index=set.Rewards.ToList().IndexOf(reward)},()=>{
                    Await(manager.RewardsSetSynchronizer.SelectLocalReward(reward),"Native card reward");
                    var acquired=player.Deck.Cards.Except(original,ReferenceEqualityComparer.Instance).Cast<CardModel>().Single();
                    if(!NativeEventFlow.Active)NativeChoices.RewardCompleted(acquired);
                });
                var gained=player.Deck.Cards.Except(original,ReferenceEqualityComparer.Instance).Cast<CardModel>().ToArray();
                if(gained.Length!=1)throw new InvalidOperationException("Native reward did not add one permanent instance");
                Observe("card-reward-acquired",new{card=NativeIdentity.Ref(gained[0]),model=gained[0].Id.ToString()});
            }
            if(!manager.RewardsSetSynchronizer.IsRewardsSetCompleted(set)){
                if(set.DisallowSkipping)throw new NotSupportedException("Required reward not supported");
                Action("SkipRewards",new{setId=set.Id},()=>manager.RewardsSetSynchronizer.SkipLocalRewardsSet());
            }
            if(!manager.RewardsSetSynchronizer.IsRewardsSetCompleted(set))throw new InvalidOperationException("Rewards did not complete");
            await Task.CompletedTask;
        };
        Await(RewardsCmd.OfferForRoomEnd(player,run!.CurrentRoom!),"Native rewards");
        }finally{RewardsSet.testSelector=previous;}
        if(offers!=1 || player.Gold<=goldBefore)throw new InvalidOperationException("Reward contract: one offer, positive native gold");
        Observe("rewards-completed",new{offers,goldBefore,goldAfter=player.Gold});
        Await(manager.ProceedFromTerminalRewardsScreen(),"Proceed to map");
    }
    static int Main(string[] args)
    {
        if(args.Length==2&&args[0]=="--visible-policy"){try{Console.WriteLine(SimulatorLab.Core.VisibleCombatPolicy.Decide(File.ReadAllText(args[1])));return 0;}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}}
        if(args.Length==3&&args[0]=="--check-pilot-plan"){try{NativeSolverCoordinator.Configure(args[1],args[2],"solver-character-pilot");return 0;}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}}
        if(args.Length==2&&args[0]=="--check-visible-execution"){try{var d=JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;NativeVisibleBattle.Check(d.GetProperty("rows").EnumerateArray().ToArray(),d.GetProperty("step"),d.GetProperty("probe"),d.GetProperty("n").GetInt32());return 0;}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}}
        if(args.Length==2&&args[0]=="--check-pilot-binding"){try{var data=JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;NativePerformancePilot.Review(data.GetProperty("header"),data.GetProperty("rows").EnumerateArray().ToArray());return 0;}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}}
        if(args.Length==3&&args[0]=="--check-run-plan"){try{NativeSolverCoordinator.Configure(args[1],args[2],"solver-native-darv-act2");Console.WriteLine(JsonSerializer.Serialize(NativeSolverCoordinator.Profile,Json));return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
        if(args.Length==2&&args[0]=="--check-solver-phase-action"){try{NativeSolverPhaseProtocol.Validate(JsonDocument.Parse(File.ReadAllText(args[1])).RootElement);return 0;}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}}
        if(args.Length==2&&args[0]=="--review-solver-phase"){try{Console.WriteLine(NativeSolverPhaseProtocol.Review(args[1]));return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
        if(args.Length==2&&args[0]=="--check-solver-phase-evidence"){try{var v=JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;var rows=v.GetProperty("rows").EnumerateArray().ToArray();string root=v.GetProperty("step").GetProperty("root").GetString()!;var dispatch=rows.Single(e=>e.GetProperty("kind").GetString()=="dispatch"&&e.GetProperty("root").GetString()==root);var confirmed=rows.Single(e=>e.GetProperty("kind").GetString()=="confirmed"&&e.GetProperty("root").GetString()==root);NativeBoundaryContract.Require(NativeBoundaryContract.Equal(dispatch,v.GetProperty("dispatch"))&&NativeBoundaryContract.Equal(confirmed,v.GetProperty("confirmed")),"Control payload actual dispatch/confirmation binding");NativeSolverPhaseProtocol.ReviewChildren(rows,v.GetProperty("step"),dispatch,confirmed,v.GetProperty("query"));return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
        if(args.Length==2&&args[0]=="--check-hand-discard-evidence"){try{var v=JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;var rows=v.GetProperty("rows").EnumerateArray().ToArray();string root=v.GetProperty("step").GetProperty("root").GetString()!;var dispatch=rows.Single(e=>e.GetProperty("kind").GetString()=="dispatch"&&e.GetProperty("root").GetString()==root);var confirmed=rows.Single(e=>e.GetProperty("kind").GetString()=="confirmed"&&e.GetProperty("root").GetString()==root);NativeBoundaryContract.Require(NativeBoundaryContract.Equal(dispatch,v.GetProperty("dispatch"))&&NativeBoundaryContract.Equal(confirmed,v.GetProperty("confirmed")),"Control payload actual dispatch/confirmation binding");NativeHandDiscardEvidence.Check(rows,v.GetProperty("step"),dispatch,confirmed,v.GetProperty("query"));return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
        if(args.Length==1&&args[0]=="--check-amalgamator-source"){try{Console.WriteLine(JsonSerializer.Serialize(NativeAmalgamator.Controls(),Json));return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
        if(args.Length==2&&args[0]=="--check-amalgamator-events"){try{Console.WriteLine(JsonSerializer.Serialize(NativeAmalgamator.ReviewEvents(JsonDocument.Parse(File.ReadAllText(args[1])).RootElement.EnumerateArray().ToArray()),Json));return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
        if(args.Length==2&&args[0]=="--review-amalgamator"){try{Console.WriteLine(JsonSerializer.Serialize(NativeAmalgamator.Review(args[1]),Json));return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
        if(args.Length==1&&args[0]=="--check-slippery-source"){try{Console.WriteLine(JsonSerializer.Serialize(NativeSlipperyBridge.Controls(),Json));return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
        if(args.Length==2&&args[0]=="--check-reflections-events"){try{Console.WriteLine(JsonSerializer.Serialize(NativeReflectionsEvidence.Review(JsonDocument.Parse(File.ReadAllText(args[1])).RootElement.EnumerateArray()),Json));return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
        if(args.Length==2&&args[0]=="--check-slippery-events"){try{Console.WriteLine(JsonSerializer.Serialize(NativeSlipperyEvidence.Review(JsonDocument.Parse(File.ReadAllText(args[1])).RootElement.EnumerateArray()),Json));return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
        if(args.Length==1&&args[0]=="--check-default-relic-source"){try{Console.WriteLine(JsonSerializer.Serialize(NativeRelicPickup.Controls(),Json));return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
        if(args.Length==2&&args[0]=="--check-default-relic-events"){try{Console.WriteLine(JsonSerializer.Serialize(NativeRelicPickupEvidence.Review(JsonDocument.Parse(File.ReadAllText(args[1])).RootElement.EnumerateArray()),Json));return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
        if(args.Length==1&&args[0]=="--check-soul-visual-audit"){try{Console.WriteLine(JsonSerializer.Serialize(NativeCreaturePresentation.Audit(),Json));return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
        if(args.Length==2&&args[0]=="--check-shop-ledger-events"){try{Console.WriteLine(JsonSerializer.Serialize(NativeShopLedgerEvidence.Review(JsonDocument.Parse(File.ReadAllText(args[1])).RootElement.EnumerateArray()),Json));return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
        if(args.Length==2&&args[0]=="--review-full-run"){try{Console.WriteLine(JsonSerializer.Serialize(NativeFullRunContract.Review(args[1]),Json));return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
        if(args.Length==2&&args[0]=="--check-full-run-events"){try{Console.WriteLine(JsonSerializer.Serialize(NativeFullRunContract.ReviewEvents(JsonDocument.Parse(File.ReadAllText(args[1])).RootElement.EnumerateArray()),Json));return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
        if(args.Length==1&&args[0]=="--check-full-run-budgets"){var c=new NativeFullRun.Limits("native-full-run/v1",1200,768,150,268435456);NativeFullRun.Validate(c);NativeFullRun.CheckCounts(c,1199,767,149,true);int rejected=0;foreach(var x in new[]{(1200,0,0d,false),(0,768,0d,true),(0,0,150d,false)})try{NativeFullRun.CheckCounts(c,x.Item1,x.Item2,x.Item3,x.Item4);}catch(TimeoutException){rejected++;}Console.WriteLine("global-budget-boundaries rejected="+rejected);return rejected==3?0:1;}
        if(args.Length==2&&args[0]=="--check-solver-action-binding"){try{var v=JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;NativeSolverActionContract.Bind(v.GetProperty("action"),v.GetProperty("query"),v.GetProperty("decision"),choices:true,potions:true);return 0;}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}}
        if(args.Length==2&&args[0]=="--review-solver-coordinator-prefix")return NativeEvidence.Review(args[1],solverCoordinatorPrefix:true);
        if(args.Length==3&&args[0]=="--check-coordinator-policy"){try{var q=JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;var c=JsonDocument.Parse(File.ReadAllText(args[2])).RootElement;NativeSolverCoordinator.BindSettings(q,c);NativeSolverCoordinator.Budget(q,c.TryGetProperty("SearchParallelism",out var dp)?dp.GetInt32():1,NativeSolverCoordinator.Limit(c));var rejection=NativeSolverCoordinator.Rejection(q,NativeSolverCoordinator.Guarded(c));if(rejection!=null)throw new NativeDataException(rejection);return 0;}catch(Exception ex){Console.Error.WriteLine(ex.GetType().Name+": "+ex.Message);return 1;}}
        if(args.Length==3&&args[0]=="--check-solver-coordinator"){try{var q=JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;NativeSolverCoordinator.Budget(q,int.TryParse(args[2],out int declaredDop)?declaredDop:1);var rejection=NativeSolverCoordinator.Rejection(q);if(rejection!=null)throw new NativeDataException(rejection);return 0;}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}}
        if(args.Length==2&&args[0]=="--review")return NativeEvidence.Review(args[1]);
        if(args.Length==2&&args[0]=="--review-solver-prefix")return NativeEvidence.Review(args[1],solverBudgetPrefix:true);
        if(args.Length==2&&args[0]=="--review-solver-room-prefix")return NativeEvidence.Review(args[1],solverRoomPrefix:true);
        if(args.Length==2&&args[0]=="--review-solver-rejected-prefix")return NativeEvidence.Review(args[1],solverRejectedPrefix:true);
        if(args.Length==3&&args[0]=="--check-solver-candidate"){
            try{NativeSolverCandidateContract.Review(JsonDocument.Parse(File.ReadAllText(args[1])).RootElement,JsonDocument.Parse(File.ReadAllText(args[2])).RootElement);return 0;}
            catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
        }
        if(args.Length==3&&args[0]=="--mutate")return NativeEvidence.Mutate(args[1],args[2]);
        if(args.Length<2)return 1;
        output=Path.GetFullPath(args[0]);
        string lab=(string?)AppContext.GetData("LabRoot")??Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../.."));
        string allowed=Path.Combine(lab,".build","runs")+Path.DirectorySeparatorChar;
        if(!output.StartsWith(allowed,StringComparison.OrdinalIgnoreCase))return 1;
        Directory.CreateDirectory(output);string seed=args[1];
        replayPath=args.Length>2&&!string.IsNullOrEmpty(args[2])?args[2]:null;
        scenario=args.Length>4?args[4]:"slice-skip";
        NativeLifecycle.Active=NativeLifecycle.CaptureScenario(scenario);NativeLifecycle.Scenario=scenario;NativePreparation.Configure(scenario);
        NativeEvidence.Mode=args.Length>5?args[5]:"detailed";
        Exception? branchConfigError=null;try{NativeSolverCoordinator.Configure(args.Length>7?args[7]:"",replayPath,scenario);if(!NativeSolverCoordinator.Active)NativeBranchSearch.Configure(args.Length>7?args[7]:"",replayPath,scenario);}catch(Exception ex){branchConfigError=ex;}
        provenance=NativeScenario.Describe(scenario,replayPath!=null,NativeEvidence.Mode,args.Length>3?args[3]:"",args.Length>6?args[6]:"",NativeBranchSearch.Active);
        var buildManifest=JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../build-manifest.json")))).RootElement;
        if(NativeFullRun.Active)NativeEvidence.Quota=NativeFullRun.Configuration!.TraceBytes;
        NativeEvidence.Open(output,new{defaultRelicProfile=NativeMajor.Active?NativeRelicPickup.Profile:null,shopLedgerProfile=NativeThirdShop.Active?"native-shop-ledger/v1":null,queryAccounting=NativeFullRun.Active?"started-query-events/v1":null,fullRunProfile=NativeFullRun.Configuration,coordinatorProfile=NativeSolverCoordinator.Profile,branchProfile=NativeBranchSearch.Profile,schema="native-header/v1",deathProfile=NativeMajor.Active?NativeDeathObservation.Profile:null,buildId=buildManifest.GetProperty("buildId").GetString(),seed,scenario,mode=NativeEvidence.Mode,provenance,information=NativeSolverProfile.Enabled?"Oracle":NativeMajor.Active?"oracle-current-intent-policy/oracle-trace":"player-visible-policy/oracle-trace",profile="vanilla-all-epochs-zero-runs/v1",budget=new{actionSeconds=45,actionCount=NativeRooms.Active?5000:scenario=="natural-death-budget"?1:1000,traceBytes=NativeEvidence.Quota},version="v0.111.0/41cef1ea",rngInventory=new{run=true,player=true,eventSynchronizer=true,eventModel=NativeLifecycle.Active?"current EventModel RNG captured; no-UI visual profile is not retail RNG equivalence":"no persistent RNG in Neow; other events not covered"},preparationProfile=NativePreparation.Active?NativePreparation.Profile:null,roomProfile=NativeRooms.Active?NativeRooms.Profile:null,integratedShopProfile=NativeThirdShop.Active?NativeThirdShop.Profile:null,merchantProfile=NativeMerchant.Active?new{id=NativeThirdShop.Active?NativeThirdShop.Profile:NativeMerchantExpansion.Active?NativeMerchantExpansion.Profile:NativeMerchantEconomy.Active?NativeMerchantEconomy.Profile:"4D1-merchant/v1",origin=NativeThirdShop.Active||NativeMerchantEconomy.Natural?"natural-run":"explicit-api-fixture",scope=NativeSolverJourney.ShopScenario(scenario)?"natural first-act composed default pickup relic/card/potion purchases and native exit; no removal/refill":NativeThirdShop.Active?"natural third-act default pickup relic/card/potion purchases and native exit; no removal/refill":NativeMerchantExpansion.Active?"native relic/base pickup/MawBank/potion refill/exit":"native inventory/card/potion/removal/exit",visualOnly="LoadRoomMerchantAssets",testModeLimits=NativeThirdShop.Active?"original potion price jitter restored; retail Exit stats absent, separate travel ledger":NativeMerchantEconomy.Active?NativeMerchantEconomy.Profile:"potion price jitter and Exit history absent"}:null,rewardProfile=NativeRewardProbe.Active?new{id=NativeRewardClosure.Active?"reward-closure/v2":"4C-native-collections/v1",depth=16,operationsPerRoot=128,setDecisions=32,policy="first legal take; full-slot discard then retry; fixture decisions explicit"}:null,eventProfile=NativeEventFlow.Active?new{id="4D2-native-events/v1",natural=NativeEventFlow.Natural,policy=NativeEventFlow.Natural?"legacy prefix; Hollow Gold; next Play":"attack-first; native first attack rewards; earned Hollow potions; declared Dummy V1 fixture",actualResume="native override Task completion required"}:null,stateScope=NativePerformancePilot.IsScenario(scenario)?"normal-start original-character first battle; Oracle continuation differential; generic summary omits class resources; not full run":NativeFullRun.Active?"normal-start multi-act controller; actual native terminal or explicit incomplete prefix; native save-card projection not covered":NativeDarv.Active?"certified prefix observation schema; original Darv and bounded next-act progression; native save-card projection not covered; not full run":NativeNaturalBoss.ThirdRoute?"normal-start third-act natural nodes to Boss/rewards or actual failure; not RunVictory":NativeNaturalBoss.Third?"normal-start third-act Ancient and first battle/rewards; not RunVictory":NativeNaturalBoss.IsElite2(scenario)?"natural second-act Elite with rewards or actual failure; not full run":NativeNaturalBoss.Act2?"two natural Acts to third map or actual failure; per-act and room lifecycle; no full run claim":NativeNaturalBoss.SecondBattle?"natural Boss, Tezcatara Soup and next-act first battle/rewards; native card serialization; no full run claim":NativeNaturalBoss.Active?"natural first-act Boss/rewards and next-act first Ancient decision; no full run claim":NativePreparation.Active?"finite legal preparation; target encounter API fixture or natural Elite; no full run claim":NativeEventFlow.Active?"4D2 original event slice/earned-prelude fixture; not full run":NativeMerchantExpansion.Active?"4D1C native transaction fixtures and natural relic slice; not full run":NativeMerchantEconomy.Active?"4D1B native merchant economics/history; registered natural path or API fixture; not full run":NativeMerchant.Active?"4D1 merchant fixtures; no natural map/relic/refill/full run claim":NativeRewardProbe.Active?"4C native rewards/treasure/potions; natural vertical slice and explicit API fixtures; not full run":NativeLifecycle.Active?"N1-T lifecycle fixtures / native death; not normal-start full victory":"N2 slice and declared API fixtures; not full run state"});
        NativeRunStop stop=NativeRunStop.EngineError;string? error=null;
        Func<RewardsSet,Task>? previousRewardSelector=null;bool rewardSelectorInstalled=false;
        try{
            if(branchConfigError!=null)throw new NativeActionRejectedException("Branch configuration rejected: "+branchConfigError.Message);
            if(replayPath!=null){replay=JsonDocument.Parse(File.ReadAllText(replayPath)).RootElement.EnumerateArray().Select(e=>e.Clone()).ToArray();if(NativeSolverCoordinator.Active&&!scenario.EndsWith("-replay")){if(replay.Length!=NativeSolverCoordinator.Configuration!.PrefixCount)throw new NativeActionRejectedException("Coordinator requires exact complete prefix route");}if(NativeBranchSearch.Active){if(replay.Length<NativeBranchSearch.Plan!.PrefixCount)throw new NativeActionRejectedException("Short branch prefix");replay=replay.Take(NativeBranchSearch.Plan.PrefixCount).ToArray();}}
            AssemblyResolver.VerifyGame();
            loop=new();SynchronizationContext.SetSynchronizationContext(loop);
            using(NativeTiming.Measure("host-initialization")){NativeHost.Install();if(NativeFoundationModLoop.Enabled)GameBootstrap.ModelTypeAugmenter=NativeFoundationModLoop.IncludeModels;else if(WatcherNativeModLoop.Enabled)GameBootstrap.ModelTypeAugmenter=WatcherNativeModLoop.IncludeModels;else if(KaguyaNativeModLoop.Enabled)GameBootstrap.ModelTypeAugmenter=KaguyaNativeModLoop.IncludeModels;else if(MinionNativeModLoop.Enabled)GameBootstrap.ModelTypeAugmenter=MinionNativeModLoop.IncludeModels;GameBootstrap.InitializeStaticState();if(NativeFoundationModLoop.Enabled){NativeFoundationModLoop.Initialize();Console.WriteLine("[foundation] finishing ModelDb");Console.Out.Flush();GameBootstrap.FinishModelDb();Console.WriteLine("[foundation] ModelDb finished");Console.Out.Flush();}else if(WatcherNativeModLoop.Enabled){WatcherNativeModLoop.Initialize();Console.WriteLine("[watcher] finishing ModelDb");Console.Out.Flush();GameBootstrap.FinishModelDb();Console.WriteLine("[watcher] ModelDb finished");Console.Out.Flush();}else if(KaguyaNativeModLoop.Enabled){KaguyaNativeModLoop.Initialize();Console.WriteLine("[kaguya] finishing ModelDb");Console.Out.Flush();GameBootstrap.FinishModelDb();Console.WriteLine("[kaguya] ModelDb finished");Console.Out.Flush();}else if(MinionNativeModLoop.Enabled){MinionNativeModLoop.Initialize();Console.WriteLine("[minion] finishing ModelDb");Console.Out.Flush();GameBootstrap.FinishModelDb();Console.WriteLine("[minion] ModelDb finished");Console.Out.Flush();}NativeSolverProfile.Initialize(output);NativeHost.Ensure();}
            NativeHost.Observe=kind=>Observe(kind);
            selectorScope=CardSelectCmd.UseSelector(NativeChoices.Instance);
            if(replayPath!=null){var cp=Path.Combine(Path.GetDirectoryName(replayPath)!,"choices.json");if(!File.Exists(cp))throw new NativeActionRejectedException("Missing frozen choice transcript");NativeChoices.LoadReplay(cp);NativeChoices.Replaying=true;
                if(NativeRewardProbe.Active){var rp=Path.Combine(Path.GetDirectoryName(replayPath)!,"reward-choices.json");if(!File.Exists(rp))throw new NativeActionRejectedException("Missing frozen reward transcript");NativeRewards.LoadReplay(rp);}}
            NativeObservation.EventFault=scenario=="event-fault";
            string control=args.Length>3?args[3]:"";
            if(control=="async-fault"){
                _=MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(Task.FromException(new InvalidOperationException("labelled async fault control")));
                loop.Pump(TimeSpan.FromMilliseconds(30));NativeHost.Ensure();
                throw new InvalidOperationException("Fault gate failed open");
            }
            if(control=="budget")throw new TimeoutException("labelled budget control");
            if(control=="strategy-stop")throw new NativeStrategyStoppedException("labelled strategy stop control");
            if(control=="runtime-failure")throw new NativeRuntimeFailureException("labelled runtime failure control");
            if(control=="environment-blocked")throw new NativeEnvironmentBlockedException("labelled environment blocked control");
            if(control=="crash")Environment.FailFast("labelled process crash control");
            if(control=="subchoice"){
                var method=typeof(CardSelectCmd).GetMethods().First(m=>m.Name=="FromSimpleGridForRewards");
                try{method.Invoke(null,method.GetParameters().Select(p=>p.HasDefaultValue?p.DefaultValue:null).ToArray());}
                catch(TargetInvocationException ex)when(ex.InnerException is NotSupportedException){throw ex.InnerException;}
                throw new InvalidOperationException("Unknown sub-choice executed");
            }
            // Lab profile: every epoch revealed, no fabricated wins/visits; same Progress for every unlock query.
            foreach(string epoch in EpochModel.AllEpochIds)SaveManager.Instance.Progress.ObtainEpochOverride(epoch,EpochState.Revealed);
            foreach(var epoch in SaveManager.Instance.Progress.Epochs)epoch.ObtainDate=0;
            AccessTools.PropertySetter(typeof(ProgressState),nameof(ProgressState.UniqueId))!.Invoke(SaveManager.Instance.Progress,["SIMULATORLAB_NATIVE_PROFILE_1"]);
            run=CreateRunAndWriteProfile(seed);
            player=run.Players.Single();
            var manager=RunManager.Instance;
            manager.SetUpNewSingleplayer(run,shouldSave:false);
            Await(manager.FinalizeStartingRelics(),"Starting relics");
            manager.Launch();terminal=NativeRunTerminal.Ongoing;
            Observe("new-run",new{seed,mode="Standard",character=player.Character.Id.ToString(),prediction="NotCompared",policy=NativePreparation.Active?NativePreparation.Policy:NativeMajor.Active?"oracle-current-intent/defend10/attackfirst/v2":NativeEventFlow.Active&&!NativeEventFlow.Natural?"visible-attackfirst/reward-firstattack/v1":NativeMerchantExpansion.Natural?"visible-nearest-shop/cheapest-supported-relic/v1":NativeMerchantEconomy.Natural?"visible-nearest-shop/cheapest-card/v1":scenario.StartsWith("natural-death")?"native-end-turn-only/v1":"native-first-legal/v1"});
            CoordinatorAtNewRun();
            if(NativeRewardProbe.Active){previousRewardSelector=RewardsSet.testSelector;RewardsSet.testSelector=NativeRewards.Collect;rewardSelectorInstalled=true;}
            Action("EnterAct",new{act=0},()=>Await(manager.EnterAct(0,false),"Natural first act"));
            Opening();
            if(control=="stale")new NativeDecisionTicket(run,sequence,"before","Travel").Consume(run,sequence+1,"before","Travel");
            if(NativeFoundationModLoop.IsScenario(scenario)){Travel();Ready();Battle();if(terminal==NativeRunTerminal.Death)stop=NativeRunStop.Completed;else{Rewards();Travel(preferCombat:true);Ready();Observe("foundation-cross-room-boundary");stop=NativeRunStop.SliceBoundary;}}
            else if(WatcherNativeModLoop.IsScenario(scenario)){Travel();Ready();Battle();if(terminal==NativeRunTerminal.Death)stop=NativeRunStop.Completed;else{Rewards();Travel();Ready();stop=NativeRunStop.SliceBoundary;}}
            else if(KaguyaNativeModLoop.IsScenario(scenario)){KaguyaEventProbe();stop=NativeRunStop.SliceBoundary;}
            else if(MinionNativeModLoop.IsScenario(scenario)){
                if(MinionNativeModLoop.IsRealCombatScenario(scenario)){
                    Travel();
                    RunMinionRealCombatSlice(seed);
                } else {
                    MinionNativeModLoop.RunLifecycleFixture(seed);
                }
                stop=NativeRunStop.SliceBoundary;
            }
            else if(NativeSolverRun.Active){SolverTwoBattles();stop=terminal==NativeRunTerminal.Death?NativeRunStop.Completed:NativeRunStop.SliceBoundary;}
            else if(NativeEventFixtures.Active){AcceptanceEventFixture();stop=NativeRunStop.SliceBoundary;}
            else if(scenario=="solver-power-death"){Travel();Ready();SolverPowerDeathFixture();stop=NativeRunStop.SliceBoundary;}
            else if(scenario=="solver-self-cost"){Travel();Ready();SolverSelfCostFixture();stop=NativeRunStop.SliceBoundary;}
            else if(scenario=="solver-tainted-lifecycle"){Travel();Ready();SolverTaintedFixture();stop=NativeRunStop.SliceBoundary;}
            else if(NativeVisibleBattle.IsScenario(scenario)){Travel();Ready();VisibleBattle();stop=terminal==NativeRunTerminal.Death?NativeRunStop.Completed:NativeRunStop.SliceBoundary;}
            else if(NativeSolverBattle.IsScenario(scenario)){Travel();Ready();SolverWholeBattle();stop=terminal==NativeRunTerminal.Death?NativeRunStop.Completed:NativeRunStop.SliceBoundary;}
            else if(scenario=="slice-solver-gate"){Travel();Ready();SolverGate();stop=NativeRunStop.SliceBoundary;}
            else if(NativeSolverOnline.IsScenario(scenario)){Travel();Ready();SolverOnlineFixture();stop=NativeRunStop.SliceBoundary;}
            else if(scenario=="solver-choice-potion"){Travel();Ready();SolverChoicePotionFixture();stop=NativeRunStop.SliceBoundary;}
            else if(scenario=="slice-solver-loop"){Travel();Ready();SolverLoop();stop=NativeRunStop.SliceBoundary;}
            else if(NativeMajor.Active){MajorProbe();stop=terminal is NativeRunTerminal.Death or NativeRunTerminal.RunVictory?NativeRunStop.Completed:NativeRunStop.SliceBoundary;}
            else if(NativeMerchantEconomy.Natural){NaturalMerchant();stop=terminal==NativeRunTerminal.Death?NativeRunStop.Completed:NativeRunStop.SliceBoundary;}
            else if(NativeMerchantExpansion.Active){ExpansionProbe();stop=NativeRunStop.SliceBoundary;}
            else if(NativeMerchant.Active){MerchantProbe();stop=NativeRunStop.SliceBoundary;}
            else if(NativeRewardProbe.Active){RewardsProbe();stop=terminal==NativeRunTerminal.Death?NativeRunStop.Completed:NativeRunStop.SliceBoundary;}
            else if(NativeRooms.Active){RoomsProbe();stop=terminal==NativeRunTerminal.Death?NativeRunStop.Completed:NativeRunStop.SliceBoundary;}
            else if(scenario.StartsWith("tail-")){TailProbe();stop=terminal==NativeRunTerminal.RunVictory?NativeRunStop.Completed:NativeRunStop.SliceBoundary;}
            else if(scenario=="natural-death-after-reward"){
                Travel();Battle();
                if(terminal==NativeRunTerminal.Death)stop=NativeRunStop.Completed;
                else { Rewards();Travel(preferCombat:true);Battle();if(terminal!=NativeRunTerminal.Death)throw new InvalidOperationException("Reward-to-death coordinator did not reach actual death");stop=NativeRunStop.Completed; }
            }
            else if(scenario.StartsWith("natural-death")){Travel();Battle();if(terminal!=NativeRunTerminal.Death)throw new InvalidOperationException("End-turn policy did not reach actual death");stop=NativeRunStop.Completed;}
            else if(!scenario.StartsWith("slice-")&&scenario!="event-fault"){
                if(scenario.StartsWith("deck-"))Fixture();else{Travel();Ready();Fixture();}
                stop=NativeRunStop.SliceBoundary;
            }else{
            Travel();Battle();
            if(terminal==NativeRunTerminal.Death)stop=NativeRunStop.Completed;
            else{Rewards();Travel();Ready();Observe("next-combat-first-play");stop=NativeRunStop.SliceBoundary;}
            }
            NativeBranchSearch.AssertFinished();NativeChoices.AssertConsumed();NativeObservation.Checkpoint();NativeHost.Ensure();
            if(replayPath!=null && replayIndex!=replay.Length)throw new InvalidOperationException("Unconsumed frozen actions");
        }catch(NativeBranchHorizon){try{NativeBranchSearch.AssertFinished();NativeChoices.AssertConsumed();NativeRewards.AssertConsumed();NativeObservation.Checkpoint();NativeHost.Ensure();stop=NativeRunStop.SliceBoundary;}catch(Exception ex){stop=NativeRunStop.DataError;error=ex.ToString();}}
        catch(NativeDataException ex){stop=NativeRunStop.DataError;error=ex.ToString();}
        catch(NotSupportedException ex){stop=NativeRunStop.Unsupported;error=ex.ToString();}
        catch(NativeActionRejectedException ex){stop=NativeRunStop.InvalidAction;error=ex.ToString();}
        catch(JsonException ex){stop=NativeRunStop.InvalidAction;error=ex.ToString();}
        catch(TimeoutException ex){stop=NativeRunStop.BudgetExceeded;error=ex.ToString();}
        catch(NativeStrategyStoppedException ex){stop=NativeRunStop.StrategyStopped;error=ex.ToString();}
        catch(NativeEnvironmentBlockedException ex){stop=NativeRunStop.EnvironmentBlocked;error=ex.ToString();}
        catch(NativeRuntimeFailureException ex){stop=NativeRunStop.RuntimeFailure;error=ex.ToString();}
        catch(Exception ex){stop=NativeRunStop.EngineError;error=ex.ToString();}
        selectorScope?.Dispose();
        if(rewardSelectorInstalled){RewardsSet.testSelector=previousRewardSelector;Write("reward-choices.json",NativeRewards.Transcript);}
        Write("choices.json",NativeChoices.Transcript);
        Write("public-requests.json",NativePolicy.PublicRequests);
        if(CardSelectCmd.Selector!=null){stop=NativeRunStop.EngineError;error="Selector scope leaked";}
        // Major profiles use the append-only canonical trace during execution.
        // Keep the legacy convenience export once, including truthful failed prefixes.
        if(NativeMajor.Active){try{if(scenario=="major-export-fault")throw new IOException("labelled compatibility export IO failure after actual native outcome");Write("events.json",Events);}catch(Exception ex){stop=NativeRunStop.DataError;error="Major compatibility export failed: "+ex;}}
        var seal=NativeEvidence.Finish(output,error==null);
        if(NativeEvidence.Failure!=null){stop=NativeRunStop.DataError;error=NativeEvidence.Failure.ToString();}
        Write("final-state.json",State());
        if(NativeFullRun.Active)FullRunReport();
        if(WatcherNativeModLoop.Enabled)WatcherNativeModLoop.WriteProbe(output,seed,replayPath!=null,terminal,stop,error);
        if(KaguyaNativeModLoop.Enabled)KaguyaNativeModLoop.WriteProbe(output,seed,replayPath!=null,terminal,stop,error);
        if(MinionNativeModLoop.Enabled)MinionNativeModLoop.WriteProbe(output,seed,replayPath!=null||scenario.EndsWith("-replay",StringComparison.Ordinal),terminal,stop,error);
        if(NativeFoundationModLoop.Enabled){NativeFoundationModLoop.WriteProbe(output,seed,replayPath!=null||scenario.EndsWith("-replay",StringComparison.Ordinal),terminal,stop,error);NativeFoundationModLoop.FinalizeTeardownAfterProbe(output);}
        if(dispatched>confirmed && error!=null)terminal=NativeRunTerminal.Unknown;
        var result=new NativeRunResultV1{Terminal=terminal,Stop=stop,Dispatched=dispatched,Confirmed=confirmed,DataComplete=error==null,LastBoundary=last,Error=error};
        Write("result.json",new{result,validation=result.Validate(),rawExit=result.ExitCode,scope=scenario,provenance,trace=seal,choicesSubmitted=NativeChoices.Submitted,guards=new{NativeHost.ProgressWrites,NativeHost.ReplayWrites,NativeHost.TelemetryCalls,faults=NativeHost.Faults.Select(e=>e.ToString()).ToArray(),isolation=NativeHost.IsolationEvidence},seams=NativeHost.Seams,bootstrap=GameBootstrap.Bypasses,loadedAssemblies=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetName().Name).Order().ToArray()});
        NativeTiming.Write(output);Console.WriteLine(JsonSerializer.Serialize(result,Json));return result.ExitCode;
    }
}
