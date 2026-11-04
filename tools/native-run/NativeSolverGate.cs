using System.Reflection;
using SimulatorLab.Core;
#if SOLVER_GATE
using CombatSolver;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;
#endif
namespace OfflineSearchHarness;
internal static partial class Program
{
#if !SOLVER_GATE
    static void SolverGate()=>throw new NotSupportedException("Separate solver build profile required");
#else
    internal static void InitializeSolver(string directory)
    {
        // No Entry.Initialize, settings Load/Save, UI, worker launcher or online services.
        typeof(Entry).GetProperty("Logger",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,new CombatSolverLog(Path.Combine(directory,"solver-logs")));
        SolverController.DisplayServerNameProvider=()=>"headless";
        SolverSettings.ApplyForTesting(new SolverSettingsData{
            PerformanceMigrationVersion=SolverSettings.CurrentPerformanceMigrationVersion,
            PerformancePreset=SolverPerformancePreset.Custom,SearchBeamWidth=8,SearchMaxExpandedNodes=1000,
            SearchTimeLimitSeconds=5,SearchMaxDegreeOfParallelism=1,SearchMaxCardBranchesPerNode=8,SearchMaxPileChoiceBranchesPerAction=8,SearchMaxHandChoiceBranchesPerAction=8,
            EnableNoGcRegion=false,UseBeamWidthPortfolio=false,OnlineStatisticsEnabled=false,SearchCompletionNotificationsEnabled=false,
            StopFullAutoOnDeathTurn=NativeSolverCoordinator.StopOnDeath,StopFullAutoOnWorseRecalculation=NativeSolverCoordinator.StopOnDeath
        });
        Write("solver-profile.json",new{profile="solver-gate/v2",nativeOnly=false,entryInitializeCalled=false,settingsLoadSaveCalled=false,extraSearchPatches=false,information="Oracle",solverVersion="0.50.1",candidatePolicy=NativeSolverCoordinator.Active?"frozen-beam-prefix/coordinator-one-step/v1":NativeSolverJourney.Candidates?NativeSolverCandidateContract.Policy:"strict-boundary/v1",settings=SolverSettings.Capture()});
        SolverSettingsAudit();
        ConstantIntentControls();
        // This first gate installs no extra search patches. Root auditing remains active.
    }
    static CombatRootSnapshot SolverCapture(CombatState combat,string role,int query)
    {
        var before=State();var continuation=ContinuationStamp.CaptureLive(combat);
        var root=CombatRootSnapshot.Capture(combat);
        if(!NativeDecisionTicket.SameActionJson(before.GetRawText(),State().GetRawText())||continuation.StateText!=ContinuationStamp.CaptureLive(combat).StateText)throw new NativeDataException("Solver capture mutated native state/RNG");
        Observe("solver-root-pure",new{role,query,continuation=Hash(continuation.StateText),rootCards=root.CapturedCardCount,information="Oracle"});return root;
    }
    sealed record SolverTicket(PlanAction Action,string NativeState,string Continuation,CombatState Combat,long Sequence,string SearchBoundary="None",bool IncompleteSearch=false,bool Coordinator=false,bool StaticPreviewApproximate=false)
    {
        bool used;
        internal bool Used=>used;
        internal void Consume(CombatState current){if(used||!ReferenceEquals(Combat,current)||Sequence!=sequence||NativeState!=State().GetRawText()||Continuation!=ContinuationStamp.CaptureLive(current).StateText)throw new NativeActionRejectedException("Stale/consumed solver ticket");used=true;}
    }
    static SolverTicket SolverQuery(CombatState combat,int query)
    {
        CoordinatorBeforeQuery();NativeFullRun.Check(dispatched,query:true);
        if(NativeFullRun.Active)NativeEvidence.Emit("run-query-admitted",new{query,ordinal=NativeFullRun.Queries});
        if(CoordinatorTail&&sequence-NativeSolverCoordinator.Configuration!.PrefixCount>=NativeSolverCoordinator.Configuration.MaxActions)throw new TimeoutException("Coordinator online tail action budget reached");
        var root=SolverCapture(combat,"query",query);string before=State().GetRawText();
        SolverBranchControls(root,combat,query);
        var policy=SolverController.CaptureSearchPolicy(SolverSettings.Capture(),combat,false,null);
        bool coordinated=NativeSolverOnline.SmartScenario(scenario)||CoordinatorTail;
        policy=coordinated?policy with{
            FixedBudget=true,BudgetOverrideMilliseconds=10000,MaxDegreeOfParallelism=CoordinatorTail?NativeSolverCoordinator.Configuration!.SearchParallelism:1,VerifyIncrementalSearch=false,
            UseBeamWidthPortfolio=false,UseNoveltyPortfolio=false,StopAtAcceptableBattleHpLoss=false,IgnoreLongTermRewards=true,
            RelicTargets=[],PotionPolicy=SolverPotionPolicy.Smart,PotionStrategy=new(SolverPotionPolicy.Smart,[]),PredictPotionReward=false,
            Profile=SolverSearchProfile.Default with{BeamWidth=8,MaxExpandedNodes=256,SoftTimeBudgetMilliseconds=10000,StopPortfolioAtHpTarget=false}
        }:policy with{Profile=policy.Profile with{SoftTimeBudgetMilliseconds=5000}};
        int cancelSeconds=coordinated?15:10;
        var names=SolverDisplayNames.Capture(combat);var damage=BattleDamageTracker.Observe(combat);
        ParallelSettingsProbe(root,combat,policy,names,damage);
        using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(cancelSeconds));
        var task=Task.Run(()=>coordinated?CombatSearchCoordinator.Solve(root,names,damage,policy,cancel.Token,null):new CombatBeamSolver(root,names,damage,policy,cancel.Token,searchProfile:policy.Profile).Solve());
        try{Await(task,"bounded solver query");}catch(OperationCanceledException){throw new TimeoutException("Solver cooperative query cancellation budget reached");}catch{cancel.Cancel();throw;}var result=task.GetAwaiter().GetResult();
        if(!NativeDecisionTicket.SameActionJson(before,State().GetRawText())||root.ContinuationStamp.StateText!=ContinuationStamp.CaptureLive(combat).StateText)throw new NativeDataException("Search mutated native source or RNG");
        var queryData=System.Text.Json.JsonSerializer.SerializeToElement(new{phaseChoices=NativeFullRun.Active?NativeSolverPhaseProtocol.Capture():null,binding=SolverBinding(combat,root.ContinuationStamp.StateText),branchRuntime=NativeSolverBranchRuntime.Capture(),query,nativeState=State(),root=root.ContinuationStamp.StateText,boundary=result.BoundaryReason.ToString(),result.TotalExpandedNodes,result.ExpandedNodes,actions=result.BestNode.Actions,policy.Profile,engine=coordinated?"CombatSearchCoordinator":"CombatBeamSolver",searchablePotions=root.SearchablePotions.Select(p=>new{p.Slot,p.PotionId}).ToArray(),potionPolicy=policy.PotionPolicy,policy.PotionStrategy,policy.FixedBudget,policy.BudgetOverrideMilliseconds,policy.StopAtAcceptableBattleHpLoss,policy.IgnoreLongTermRewards,policy.PredictPotionReward,result.ExplicitPotionCount,result.PotionHpSaved,result.PotionHpRequired,searchElapsedMilliseconds=result.TotalSearchElapsed.TotalMilliseconds,candidate=new{policy=CoordinatorTail?NativeSolverCoordinator.Policy:NativeSolverJourney.Candidates?NativeSolverCandidateContract.Policy:"strict-boundary/v1",resultScope=result.ResultScope.ToString(),snapshotBoundary=result.Snapshot.BoundaryReason.ToString(),hasRisk=result.Snapshot.HasRisk,playerDead=result.Snapshot.PlayerDead,unmirrored=result.UnmirroredDetails(),forecastUnsupported=result.Forecast.HasUnsupportedIntent,forecastExact=result.Forecast.IsExactForModeledDamage,approximations=result.Forecast.ApproximationDetails},prediction=new{result.StartTurnNumber,result.DeathTurn,result.OnlyDeathRoutesFound,result.CombatEndedTurn,outcome=result.Snapshot.PlayerDead?"Defeat":result.Snapshot.AllEnemiesDead?"Victory":"Ongoing",result.Snapshot.PlayerHp,result.Snapshot.ProjectedPlayerHp},work=new{result.ParallelExpansionWaves,result.ParallelExpansionWorkItems,result.MaxParallelExpansionConcurrency,result.SingleSessionSearch,attributions=result.SearchWorkAttributions,portfolio=result.PortfolioTelemetry},executionPolicy=NativeSolverCoordinator.Configuration?.ExecutionPolicy??"Guarded",stopOnPredictedDeath=NativeSolverCoordinator.StopOnDeath,budget=new{beam=policy.Profile.BeamWidth,nodes=policy.Profile.MaxExpandedNodes,softMilliseconds=policy.Profile.SoftTimeBudgetMilliseconds,cancelSeconds,dop=policy.MaxDegreeOfParallelism,returnedAggregateNodeLimit=CoordinatorTail?NativeSolverCoordinator.ReturnLimit:coordinated?4096:policy.Profile.MaxExpandedNodes},sourceUnchanged=true},Json);
        Write("solver-query-"+query+".json",queryData);
        Observe("solver-query-complete",new{query,root=Hash(root.ContinuationStamp.StateText),boundary=result.BoundaryReason.ToString()});
        if(CoordinatorTail){
            string? reason=NativeSolverCoordinator.Rejection(queryData,NativeSolverCoordinator.StopOnDeath);
            Write("solver-admission-"+query+".json",new{query,policy=NativeSolverCoordinator.Policy,decision=reason==null?"eligible-for-shadow":"rejected",reason,nativeTerminal="Ongoing",branchRuntime=NativeSolverBranchRuntime.Capture(),staticPreviewApproximate=!result.Forecast.IsExactForModeledDamage||result.Forecast.ApproximationDetails.Count>0,certifiedScope="actual shadow/native executed prefix; static forecast precision not promoted"});
            if(result.BoundaryReason is SearchBoundaryReason.TimeLimit)throw new TimeoutException("Coordinator TimeLimit; no adoption");
            NativeSolverCoordinator.Budget(queryData,NativeSolverCoordinator.Configuration!.SearchParallelism,NativeSolverCoordinator.ReturnLimit);
            if(reason!=null)throw new NotSupportedException("Coordinator candidate not adopted: "+reason);
            if(NativeSolverCoordinator.Configuration!.Probe)throw new NotSupportedException("Coordinator query probe completed without native dispatch");
            return new(result.BestNode.Actions[0],before,root.ContinuationStamp.StateText,combat,sequence,result.BoundaryReason.ToString(),result.BoundaryReason!=SearchBoundaryReason.None,true,!result.Forecast.IsExactForModeledDamage||result.Forecast.ApproximationDetails.Count>0);
        }
        if(NativeSolverJourney.Candidates){
            NativeSolverBudgetContract.Review(queryData,System.Text.Json.JsonSerializer.SerializeToElement(new{settings=SolverSettings.Capture()},Json));
            string? rejection=NativeSolverCandidateContract.Rejection(queryData);
            if(rejection!=null){if(result.BoundaryReason is SearchBoundaryReason.NodeLimit or SearchBoundaryReason.TimeLimit)throw new TimeoutException("Bounded candidate not adopted: "+rejection);throw new NotSupportedException("Candidate not adopted: "+rejection);}
            return new(result.BestNode.Actions[0],before,root.ContinuationStamp.StateText,combat,sequence,result.BoundaryReason.ToString(),result.BoundaryReason!=SearchBoundaryReason.None);
        }
        if(result.BoundaryReason is SearchBoundaryReason.TimeLimit or SearchBoundaryReason.NodeLimit||coordinated&&result.TotalExpandedNodes>4096)throw new TimeoutException("Solver finite search budget reached; candidate not adopted");
        if(result.BestNode.Actions.Count==0)throw new NotSupportedException("Solver returned no candidate");
        return new(result.BestNode.Actions[0],before,root.ContinuationStamp.StateText,combat,sequence);
    }
    static void ValidateGateFields(PlanAction action,bool choices=false,bool enchantments=false)
    {
        NativeSolverActionContract.Fields(System.Text.Json.JsonSerializer.SerializeToElement(action,Json),choices:choices,enchantments:enchantments);
        if(action.Kind!=PlanActionKind.PlayCard)throw new NotSupportedException("PlayCard required");
    }
    static MegaCrit.Sts2.Core.Models.CardModel ResolveSolverCard(PlanAction action,bool choices=false,bool enchantments=false)
    {
        ValidateGateFields(action,choices,enchantments);
        var hand=player!.PlayerCombatState!.Hand.Cards.ToArray();
        var card=hand.Where(c=>c.Id.Entry==action.CardId).Skip(action.CardOccurrence).FirstOrDefault();
        if(card==null||CardChoiceSupport.ChoiceCardKey(card)!=action.CardStateKey||card.CurrentUpgradeLevel!=action.CardUpgradeLevel||!ReferenceEquals(card,hand.Where(c=>c.Id.Entry==action.CardId&&CardChoiceSupport.ChoiceCardKey(c)==action.CardStateKey).Skip(action.CardStateOccurrence).FirstOrDefault()))throw new NativeActionRejectedException("Solver card state/instance not present");
        NativeSolverEnchantments.ValidateNative(card,action.CardEnchantmentId);
        return card;
    }
    static void SolverGate()
    {
        var combat=CombatManager.Instance.DebugOnlyGetState()!;
        var ticket=SolverQuery(combat,1);var action=ticket.Action;ValidateGateFields(action);
        var controlResults=new List<string>();int dispatchBeforeControls=dispatched;
        var child=new PlanCardChoice(PlanChoiceEffect.Exhaust,MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand,[]);
        foreach(var bad in new[]{action with{Kind=PlanActionKind.EndTurn},action with{Choice=child},action with{NestedChoices=[child]},action with{TurnStartChoices=[child]},action with{NestedChoicesBeforePrimary=1},action with{ReplayCount=1},action with{EndsPlayerTurn=true},action with{PotionSlot=0},action with{CardEnchantmentId="unknown"},action with{CardStateKey="stale"},action with{CardOccurrence=999},action with{CardStateOccurrence=999},action with{CardUpgradeLevel=999}}){
            bool rejected=false;try{_=ResolveSolverCard(bad);}catch(Exception ex)when(ex is NativeActionRejectedException or NotSupportedException){rejected=true;}
            if(!rejected)throw new NativeDataException("Solver mapping negative control accepted");controlResults.Add("rejected");
        }
        if(dispatched!=dispatchBeforeControls)throw new NativeDataException("Solver negative control dispatched a native action");
        Write("solver-controls.json",new{negativeMappings=controlResults.Count,allRejected=true,zeroAdditionalDispatch=true});
        var root=SolverCapture(combat,"shadow",1);var shadow=root.ForkSimulator();var parent=root.ForkSimulator();
        string Stamp(CombatPredictionSimulator sim)=>ContinuationStamp.CapturePredicted(player!,sim,player!.PlayerCombatState!.TurnNumber,root.Forecast,root.StartTurnNumber).StateText;
        string parentBefore=Stamp(parent);
        var card=ResolveSolverCard(action);
        var target=action.TargetCombatId is uint id?combat.Creatures.Single(c=>c.CombatId==id):action.TargetIndex>=0?combat.Enemies[action.TargetIndex]:null;
        if(action.Turn!=player!.PlayerCombatState!.TurnNumber||!card.CanPlayTargeting(target))throw new NativeActionRejectedException("Solver action not natively legal/current");
        var candidate=shadow.State.FindCard(card)??throw new NativeActionRejectedException("No shadow card for native identity");
        if(!shadow.ManualPlay(candidate,target,out _)||shadow.HasPendingChoice)throw new NotSupportedException("Prediction first action unresolved");
        string expected=Stamp(shadow);
        if(parentBefore!=Stamp(parent)||root.ContinuationStamp.StateText!=ContinuationStamp.CaptureLive(combat).StateText)throw new NativeDataException("Prediction branch contaminated parent or native source");
        var nativeBefore=State();var staleTicket=ticket with{};
        if(staleTicket.Used)throw new NativeDataException("Stale control must start unconsumed");
        ticket.Consume(combat);
        bool consumedRejected=false;try{ticket.Consume(combat);}catch(NativeActionRejectedException){consumedRejected=true;}
        if(!consumedRejected)throw new NativeDataException("Solver ticket reusable");
        Action("PlayCard",new{card=Id(card),model=card.Id.ToString(),target=target==null?null:Id(target)},()=>{
            if(!card.TryManualPlay(target))throw new NativeActionRejectedException("Native solver action rejected");Await(MegaCrit.Sts2.Core.Runs.RunManager.Instance.ActionExecutor.FinishedExecutingActions(),"native solver action");
        });
        string actual=ContinuationStamp.CaptureLive(combat).StateText;
        Write("solver-step.json",new{action,card=Id(card),target=target==null?null:Id(target),nativeBefore,nativeAfter=State(),expected,actual,exact=expected==actual,parentUnchanged=true,consumedRejected});
        if(expected!=actual)throw new NativeDataException("Solver/native first-action differential mismatch");
        int beforeStaleDispatch=dispatched;
        bool staleRejected=false;try{staleTicket.Consume(combat);}catch(NativeActionRejectedException){staleRejected=true;}
        if(!staleRejected)throw new NativeDataException("Solver stale boundary accepted");
        Write("solver-ticket-checks.json",new{freshBefore=true,unconsumedAtRejection=!staleTicket.Used,staleRejected,consumedRejected,zeroAdditionalDispatch=dispatched==beforeStaleDispatch,oldSequence=staleTicket.Sequence,currentSequence=sequence,stateChanged=staleTicket.NativeState!=State().GetRawText()});
        _=SolverQuery(combat,2);Observe("solver-gate-complete",new{nativeActionsExecuted=1,queries=2,prediction="diff-certified-one-action",staleRejected,wholeBattle=false});
    }
#endif
}
