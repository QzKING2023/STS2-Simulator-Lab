using SimulatorLab.Core;
#if SOLVER_GATE
using CombatSolver;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Runs;
using System.Reflection;
#endif
namespace OfflineSearchHarness;
internal static partial class Program
{
#if !SOLVER_GATE
    static void SolverLoop()=>throw new NotSupportedException("Separate solver build required");
#else
    static void ValidateLoopFields(PlanAction action)
    {
        NativeSolverActionContract.Fields(System.Text.Json.JsonSerializer.SerializeToElement(action,Json));
    }
    static Creature? ResolveSolverTarget(PlanAction action,CombatState combat)
    {
        if(action.TargetIndex < -1)throw new NativeActionRejectedException("Invalid solver target index");
        Creature? byId=null,byIndex=null;
        if(action.TargetCombatId is uint id)byId=combat.Creatures.SingleOrDefault(c=>c.CombatId==id)??throw new NativeActionRejectedException("Solver target identity absent");
        if(action.TargetIndex>=0){if(action.TargetIndex>=combat.Enemies.Count)throw new NativeActionRejectedException("Solver target index absent");byIndex=combat.Enemies[action.TargetIndex];}
        if(byId!=null&&byIndex!=null&&!ReferenceEquals(byId,byIndex))throw new NativeActionRejectedException("Solver target ID/index disagree");
        var target=byId??byIndex;
        if(target!=null&&!target.IsAlive)throw new NativeActionRejectedException("Solver target dead");
        return target;
    }
    static void SolverLoopControls(CombatState combat,PlanAction action)
    {
        var end=new PlanAction(PlanActionKind.EndTurn,action.Turn);
        ValidateLoopFields(end);
        var child=new PlanCardChoice(PlanChoiceEffect.Exhaust,PileType.Hand,[]);
        int beforeDispatch=dispatched;string before=State().GetRawText();int rejected=0;
        foreach(Action test in new Action[]{
            ()=>ValidateLoopFields(end with{CardId="STRIKE_IRONCLAD"}),
            ()=>ValidateLoopFields(end with{TurnStartChoices=[child]}),
            ()=>ValidateLoopFields(end with{NestedChoices=[child]}),
            ()=>ValidateLoopFields(end with{Choice=child}),
            ()=>ValidateLoopFields(end with{ReplayCount=1}),
            ()=>ValidateLoopFields(end with{Kind=(PlanActionKind)999}),
            ()=>ValidateLoopFields(end with{TargetIndex=0}),
            ()=>ResolveSolverTarget(action with{TargetIndex=-2},combat),
            ()=>ResolveSolverTarget(action with{TargetCombatId=uint.MaxValue},combat),
            ()=>ResolveSolverTarget(action with{TargetIndex=999},combat),
            ()=>ResolveSolverCard(action with{CardStateKey="stale"})
        }){
            try{test();throw new NativeDataException("Loop negative mapping accepted");}catch(Exception ex)when(ex is NativeActionRejectedException or NotSupportedException){rejected++;}
        }
        if(dispatched!=beforeDispatch||before!=State().GetRawText())throw new NativeDataException("Loop controls mutated or dispatched");
        Write("solver-loop-controls.json",new{negativeMappings=rejected,allRejected=true,zeroAdditionalDispatch=true,sourceUnchanged=true,endTurnFieldsAccepted=true});
    }
    sealed record SolverExecuted(PlanAction Action,bool TargetedAttack,bool ChildChoice,bool Potion,string? Terminal=null);
    static SolverExecuted ExecuteSolverAction(SolverTicket ticket,CombatState combat,int query,bool extended=false,bool allowTerminal=false)
    {
            var action=ticket.Action;NativeSolverActionContract.Fields(System.Text.Json.JsonSerializer.SerializeToElement(action,Json),choices:extended,potions:extended,enchantments:extended,phaseChoices:extended&&NativeFullRun.Active);
            if(extended&&(action.NestedChoices?.Count>0||action.NestedChoicesBeforePrimary!=0))throw new NotSupportedException("Online nested choices not certified");
            if(action.Turn!=player!.PlayerCombatState!.TurnNumber)throw new NativeActionRejectedException("Solver turn stale");
            var root=SolverCapture(combat,"shadow",query);var shadow=root.ForkSimulator();var parent=root.ForkSimulator();
            SolverHookControls(root,combat,query);EndingPowerControls(root,combat,query);PossessRootControls(root,combat,query);
            string Stamp(CombatPredictionSimulator sim)=>ContinuationStamp.CapturePredicted(player!,sim,((SimulatedCombatState)sim.State.CombatState).GetPlayerTurnNumber(player!),root.Forecast,root.StartTurnNumber).StateText;
            string parentBefore=Stamp(parent);MegaCrit.Sts2.Core.Models.CardModel? card=null;MegaCrit.Sts2.Core.Models.PotionModel? potion=null;Creature? target=null;
            if(action.Kind==PlanActionKind.PlayCard){
                card=ResolveSolverCard(action,extended,extended);target=ResolveSolverTarget(action,combat);
                if(card.Enchantment!=null)EnchantmentIdentityControls(action,card,combat);
                if(!card.CanPlayTargeting(target))throw new NativeActionRejectedException("Solver play illegal");
                var candidate=shadow.State.FindCard(card)??throw new NativeActionRejectedException("Shadow instance absent");
                var simulated=(SimulatedCombatState)shadow.State.CombatState;
                // Match the upstream replay action envelope: card callbacks and
                // the deferred death pass share a branch-local processed set.
                var processedDeaths=new HashSet<uint>();
                using var executionScope=simulated.BeginCardExecutionScope(processedDeaths);
                if(extended)simulated.BeginActionChoices(action.GetActionChoicesInExecutionOrder());
                bool played=shadow.ManualPlay(candidate,target,out _);
                if(played&&!shadow.HasPendingChoice){
                    using(shadow.BeginExecutionDispatch())
                        played=CorePowerSupport.ApplyEnemyDeathPowers(shadow,simulated,simulated.KnownEnemies,processedDeaths);
                    if(played)played=CombatBeamSolver.SettleReplayActionBoundary(shadow,simulated);
                }
                if(extended)simulated.EndActionChoices();
                if(!played||shadow.HasPendingChoice)throw new NotSupportedException("Solver play requires unresolved choice");
            }else if(action.Kind==PlanActionKind.UsePotion){
                potion=player!.GetPotionAtSlotIndex(action.PotionSlot)??throw new NativeActionRejectedException("Native solver potion slot empty");target=ResolveSolverTarget(action,combat);
                // Upstream potion native transport validates self potions against
                // the player, while EnqueueManualUse still receives null.
                var validationTarget=target??(potion.IsValidTarget(player.Creature)?player.Creature:null);
                if(potion.Id.Entry!=action.PotionId||!potion.IsValidTarget(validationTarget)||!potion.PassesCustomUsabilityCheck)throw new NativeActionRejectedException("Native solver potion stale/illegal");
                var simulated=(SimulatedCombatState)shadow.State.CombatState;var forkPotion=simulated.GetPotionAtSlot(player,action.PotionSlot)??throw new NativeActionRejectedException("Shadow potion slot empty");
                if(forkPotion.Id.Entry!=action.PotionId||!PotionOnUseSupport.CanSearch(forkPotion))throw new NotSupportedException("Solver potion capability absent");
                // Same separation as upstream CombatBeamSolver.Expansion.Replay:
                // potion primary selection is applied by Complete; only hook
                // selections enter the action cursor. Never enqueue it twice.
                NativeSolverGeneratedChoice.Validate(action,forkPotion,shadow);
                int historyStart=shadow.History.Entries.Count;simulated.BeginActionChoices(action.NestedChoices);
                bool used;
                try{used=PotionExecutionSupport.Prepare(shadow,simulated,forkPotion,action.PotionSlot,target)&&PotionExecutionSupport.Complete(shadow,simulated,forkPotion,target,action.Choice,historyStart,new HashSet<uint>())&&CombatBeamSolver.SettleReplayActionBoundary(shadow,simulated);}
                finally{simulated.EndActionChoices();}
                if(!used||shadow.HasPendingChoice)throw new NotSupportedException("Solver potion unresolved");
            }else{
                var driver=new CombatBeamSolver(root,SolverDisplayNames.Capture(combat),BattleDamageTracker.Observe(combat),SolverController.CaptureSearchPolicy(SolverSettings.Capture(),combat,false,null));
                var advance=typeof(CombatBeamSolver).GetMethods(BindingFlags.Instance|BindingFlags.NonPublic).Single(m=>m.Name=="AdvanceRound"&&m.GetParameters().Length==7);
                object?[] args=[shadow,(SimulatedCombatState)shadow.State.CombatState,action.Turn-root.StartTurnNumber,new HashSet<uint>(),0,action.TurnStartChoices,null];
                object? boundary;
                try{boundary=advance.Invoke(driver,args);}catch(TargetInvocationException ex)when(ex.InnerException!=null){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();throw;}
                var simulated=(SimulatedCombatState)shadow.State.CombatState;
                if(boundary?.ToString()!="None"||(shadow.IsInProgress&&!CombatBeamSolver.SettleReplayActionBoundary(shadow,simulated))||shadow.HasPendingChoice)throw new NotSupportedException("Solver EndTurn unresolved: "+boundary);
                _=simulated.ConsumePlayerTurnEndRequest();
            }
            if(allowTerminal)shadow.CheckWinCondition(((SimulatedCombatState)shadow.State.CombatState).GetPlayerTurnNumber(player!));
            // Upstream HasRisk is a raw history flag. Use the same compensation
            // classification as CombatBeamSolver.GetCoverageSummary.
            var shadowGaps=NativeSolverJourney.Candidates?PredictionCoverage.Collect(shadow).ToArray():Array.Empty<PredictionGap>();
            if(NativeSolverJourney.Candidates){
                Write("solver-shadow-"+query+".json",new{query,rawRisk=shadow.HasRisk,gaps=shadowGaps,uncompensated=shadowGaps.Any(g=>!g.Compensated)});
                if(shadowGaps.Any(g=>!g.Compensated))throw new NotSupportedException("Candidate first-action shadow has uncompensated risk; no native dispatch");
            }
            var predictedTerminal=shadow.TerminalStamp;
            if(ticket.Coordinator){
                bool lethal=predictedTerminal?.Outcome==CombatTerminalOutcome.Defeat||shadow.State.GetCreature(player!.Creature).IsDead;
                Write("solver-current-shadow-"+query+".json",new{query,root=NativeEvidence.Root,context=NativeEvidence.Context,nativeBefore=State(),action,outcome=predictedTerminal?.Outcome.ToString()??"Ongoing",terminal=predictedTerminal,lethal,executionPolicy=NativeSolverCoordinator.Configuration!.ExecutionPolicy,stopOnPredictedDeath=NativeSolverCoordinator.StopOnDeath,parentUnchanged=parentBefore==Stamp(parent),continuation=Stamp(shadow)});
                if(action.Kind==PlanActionKind.EndTurn){
                    var liveRisk=LiveEndTurnRiskEvaluator.Evaluate(combat,action.TurnStartChoices);
                    Write("solver-endturn-risk-"+query+".json",new{query,risk=liveRisk,sourceUnchanged=root.ContinuationStamp.StateText==ContinuationStamp.CaptureLive(combat).StateText,nativeState=State()});
                    if(root.ContinuationStamp.StateText!=ContinuationStamp.CaptureLive(combat).StateText)throw new NativeDataException("Live EndTurn risk changed source/RNG");
                    lethal|=liveRisk.PlayerDead;
                }
                if(lethal&&NativeSolverCoordinator.StopOnDeath)throw new NotSupportedException(action.Kind==PlanActionKind.EndTurn?"Coordinator pre-EndTurn lethal protection":"Coordinator current-action lethal protection");
            }
            if(!shadow.IsInProgress&&(!allowTerminal||predictedTerminal==null))throw new NotSupportedException("Predicted terminal needs separate native terminal contract");
            string expected=Stamp(shadow);
            if(parentBefore!=Stamp(parent)||root.ContinuationStamp.StateText!=ContinuationStamp.CaptureLive(combat).StateText)throw new NativeDataException("Solver step contaminated parent/native");
            var before=State();var stale=ticket with{};ticket.Consume(combat);bool consumedRejected=false;
            try{ticket.Consume(combat);}catch(NativeActionRejectedException){consumedRejected=true;}
            if(!consumedRejected||stale.Used)throw new NativeDataException("Solver ticket control precondition");
            if(allowTerminal)ArmSolverTerminal(combat,query);
            object[] children=[];
            if(card!=null){
                Action("PlayCard",new{card=Id(card),model=card.Id.ToString(),target=target==null?null:Id(target)},()=>{
                    using var scope=extended?new NativeSolverChoices(action):null;
                    if(!card.TryManualPlay(target))throw new NativeActionRejectedException("Native solver play rejected");Await(RunManager.Instance.ActionExecutor.FinishedExecutingActions(),"solver native PlayCard");scope?.AssertConsumed();children=scope?.Records.ToArray()??[];
                });
            }else if(potion!=null){
                void Use(){
                    var operation=NativeSolverJourney.Active?NativeRewards.Begin("potion-use",potion):null;
                    using var scope=new NativeSolverChoices(action);potion.EnqueueManualUse(target);Await(RunManager.Instance.ActionExecutor.FinishedExecutingActions(),"solver native UsePotion");scope.AssertConsumed();children=scope.Records.ToArray();
                    if(operation!=null)NativeRewards.End(operation,new{consumed=NativeIdentity.Ref(potion),target=NativeIdentity.Ref(target)});
                }
                var decision=new{potion=Id(potion),slot=action.PotionSlot,target=target==null?null:Id(target)};
                if(NativeSolverJourney.Active)CAction("UsePotion",decision,Use);else Action("UsePotion",decision,Use);
            }else{
                Action("EndTurn",new{turn=action.Turn},()=>{
                    using var scope=extended?new NativeSolverChoices(action):null;
                    CombatManager.Instance.OnEndedTurnLocally();RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new EndPlayerTurnAction(player!,action.Turn));
                    Await(RunManager.Instance.ActionExecutor.FinishedExecutingActions(),"solver native enemy and next-player turn");
                    scope?.AssertConsumed();children=scope?.Records.ToArray()??[];
                });
            }
            var after=State();string actual=allowTerminal&&SolverNativeTerminal!=null?SolverNativeTerminal.Continuation:ContinuationStamp.CaptureLive(combat).StateText;
            if(allowTerminal)CheckSolverTerminal(predictedTerminal,query);
            int dispatchBeforeStale=dispatched;bool staleRejected=false;try{stale.Consume(combat);}catch(NativeActionRejectedException){staleRejected=true;}
            Write((extended?"solver-online-step-":"solver-loop-step-")+query+".json",new{query,action,children,coordinator=ticket.Coordinator,staticPreviewApproximate=ticket.StaticPreviewApproximate,predictionScope="current executed step; future static preview may be approximate",shadowPredictionGaps=shadowGaps,searchBoundary=ticket.SearchBoundary,incompleteSearch=ticket.IncompleteSearch,root=NativeEvidence.Root,context=NativeEvidence.Context,card=card==null?null:Id(card),target=target==null?null:Id(target),nativeBefore=before,nativeAfter=after,expected,actual,exact=expected==actual,parentUnchanged=parentBefore==Stamp(parent),consumedRejected,staleRejected,unconsumedAtRejection=!stale.Used,zeroAdditionalDispatch=dispatchBeforeStale==dispatched,oldSequence=stale.Sequence,currentSequence=sequence});
            if(expected!=actual)throw new NativeDataException("Solver/native loop differential mismatch at "+query);
            if(!staleRejected||stale.Used||dispatchBeforeStale!=dispatched)throw new NativeDataException("Unconsumed stale loop ticket accepted");
            return new(action,card?.Type==CardType.Attack&&target!=null,children.Length>0,potion!=null,predictedTerminal?.Outcome.ToString());
    }
    static void SolverLoop()
    {
        bool attacked=false,ended=false;int completed=0;
        for(int query=1;query<=9;query++){
            Ready();var combat=CombatManager.Instance.DebugOnlyGetState()!;
            var ticket=SolverQuery(combat,query);
            if(query==1)SolverLoopControls(combat,ticket.Action);
            if(attacked&&ended){Observe("solver-loop-complete",new{nativeActionsExecuted=completed,queries=query,targetedAttack=true,endTurn=true,wholeBattle=false,information="Oracle"});return;}
            if(completed>=8)throw new TimeoutException("Solver action-loop budget reached");
            var executed=ExecuteSolverAction(ticket,combat,query);
            completed++;attacked|=executed.TargetedAttack;ended|=executed.Action.Kind==PlanActionKind.EndTurn;
        }
        throw new TimeoutException("Solver query-loop budget reached");
    }
#endif
}
