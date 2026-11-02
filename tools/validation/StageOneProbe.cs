using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using CombatSolver;
using CombatSolver.Engine.Common;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using SimulatorLab.Core;

namespace OfflineSearchHarness;

internal static partial class EleanorV040Probe
{
    public static object RunStageOne(CombatState combat, MainLoopContext loop, HarnessOptions options)
    {
        string name = Environment.GetEnvironmentVariable("SIMULATORLAB_STAGE_ONE_CASE")!;
        string output = options.OutputDirectory;
        var player = LocalContext.GetMe(combat)!;
        var setup = new List<object>();
        int ended = 0, won = 0;
        CombatManager.Instance.CombatEnded += OnEnded;
        CombatManager.Instance.CombatWon += OnWon;
        void OnEnded(CombatRoom _) => ended++;
        void OnWon(CombatRoom _) => won++;
        object? replay = null;
        object? recording = null;
        object? control = null;
        var replays = new List<JsonElement>();
        ValidationStopException? typedFailure = null;
        try
        {
            if (!TestMode.IsOn) throw new InvalidOperationException("Death probe requires native TestMode: CreatureCmd.Kill skips OnEnded/metrics/history/UI only in this mode.");
            _ = CaptureOfflineWriteEvidence();
            if(name=="engine-error-control") throw new FormatException("stage-one-injected-before-native-dispatch");
            if(name is "invalid-card" or "invalid-target" or "invalid-turn")
            {
                var before=CombatRootSnapshot.Capture(combat);
                JsonElement action=name=="invalid-card"?Action("PlayCard","NOT_A_NATIVE_CARD"):
                    JsonSerializer.SerializeToElement(new{kind=name=="invalid-target"?"PlayCard":"EndTurn",turn=player.PlayerCombatState!.TurnNumber+(name=="invalid-turn"?1:0),cardId="STRIKE_IRONCLAD",cardOccurrence=0,targetIndex=999});
                replay=Replay([action]);
                var after=CombatRootSnapshot.Capture(combat);
                if(JsonSerializer.SerializeToElement(replay,ProtocolJson.Options).GetProperty("status").GetString()!="invalid-action"||before.LiveStamp!=after.LiveStamp||before.ContinuationStamp!=after.ContinuationStamp)
                    throw new InvalidOperationException("Invalid card was not rejected without native mutation.");
                control=new{kind="native-invalid-action",sourceUnchanged=true};
            }
            else if(name is "decision-budget" or "turn-budget")
            {
                string? reason;
                if(name=="decision-budget")
                {
                    var ticket=NativeCombatDecision.Query(combat,loop,options,Path.Combine(output,"query"));
                    replay=Replay(ticket.Consume(combat));
                    RequireLivingBoundary(replay);
                    reason=new DecisionBounds(MaxDecisions:1).Reached(ReplayVerification.Count(replays),player.PlayerCombatState!.TurnNumber,0);
                }
                else {replay=Replay([Action("EndTurn","")]);RequireLivingBoundary(replay);reason=new DecisionBounds(MaxTurns:1).Reached(0,player.PlayerCombatState!.TurnNumber,0);}
                if(reason is null)throw new InvalidOperationException("Controller budget did not stop.");
                control=new{kind="native-controller-budget",reason,queries=name=="decision-budget"?1:0,stoppedBeforeNextQuery=true};
            }
            else if (name is "ticket-stale" or "ticket-duplicate" or "budget-query")
            {
                var before=CombatRootSnapshot.Capture(combat);
                try
                {
                    var ticket=NativeCombatDecision.Query(combat,loop,options,Path.Combine(output,"query"));
                    if(name=="budget-query") throw new InvalidOperationException("Requested budget did not trigger.");
                    if(name=="ticket-stale") { replay=Replay([Action("PlayCard","GUIDING_POWER")]); RequireLivingBoundary(replay); }
                    else _=ticket.Consume(combat);
                    var rejectBefore=CombatRootSnapshot.Capture(combat);
                    try { _=ticket.Consume(combat); throw new InvalidOperationException("Ticket was not rejected."); }
                    catch(ValidationStopException ex) when(ex.Reason==ValidationStop.InvalidAction)
                    {
                        var after=CombatRootSnapshot.Capture(combat);
                        if(rejectBefore.LiveStamp!=after.LiveStamp||rejectBefore.ContinuationStamp!=after.ContinuationStamp)throw new InvalidOperationException("Rejection mutated source.");
                        control=new {kind="native-ticket-rejection",ex.Code,sourceUnchanged=true};
                    }
                }
                catch(ValidationStopException ex) when(name=="budget-query" && ex.Reason==ValidationStop.BudgetExceeded)
                {
                    var after=CombatRootSnapshot.Capture(combat);
                    if(before.LiveStamp!=after.LiveStamp||before.ContinuationStamp!=after.ContinuationStamp)throw new InvalidOperationException("Budget query mutated source.");
                    control=new{kind="actual-solver-budget",ex.Code,sourceUnchanged=true,executedActions=0};
                }
            }
            else if(name=="enemy-victory")
            {
                loop.RunUntilCompleted(InvokeNativePowerApply(typeof(ThornsPower),player.Creature,100m),TimeSpan.FromSeconds(15),"Native Thorns terminal fixture");
                replay=Replay([Action("EndTurn","")]);
                if(JsonSerializer.SerializeToElement(replay,ProtocolJson.Options).GetProperty("status").GetString()!="passed"||won!=1)throw new InvalidOperationException("Enemy-phase Victory did not match.");
            }
            else if (name is "enemy-death" or "upkeep-death")
            {
                recording = AddRecordingResetFixture(combat, loop);
                if (name == "upkeep-death")
                {
                    var guide = player.PlayerCombatState!.Hand.Cards.Single(c => c.Id.Entry == "GUIDING_POWER");
                    replay = Replay([Action("PlayCard",guide.Id.Entry)]);
                    if (JsonSerializer.SerializeToElement(replay, ProtocolJson.Options).GetProperty("status").GetString() != "boundary")
                        throw new InvalidOperationException("Actual Guide prefix failed.");
                    var mana = GetLiveManaState(player);
                    int amount = CaptureState(_stateType!,mana).Mana;
                    if (!(bool)_stateType!.GetMethod("TryPayMana")!.Invoke(mana,[amount])!) throw new InvalidOperationException("Native Mana drain failed.");
                    loop.RunUntilCompleted(InvokeNativeBlockGain(player.Creature,100m),TimeSpan.FromSeconds(15),"Explicit upkeep fixture block");
                    setup.Add(new {api="ManaState.TryPayMana / CreatureCmd.GainBlock",manaPaid=amount,block=100});
                }
                decimal damage = player.Creature.CurrentHp - 1;
                loop.RunUntilCompleted(InvokeNativeDamage(player.Creature,damage,(ValueProp)6),TimeSpan.FromSeconds(15),"Explicit nonlethal low HP fixture");
                setup.Add(new {api="CreatureCmd.Damage",damage,hp=player.Creature.CurrentHp,block=player.Creature.Block});
                if (player.Creature.CurrentHp != 1) throw new InvalidOperationException("Nonlethal fixture precondition failed.");
                replay = Replay([Action("EndTurn","")]);
                var result = JsonSerializer.SerializeToElement(replay, ProtocolJson.Options);
                if(result.GetProperty("status").GetString()!="death") throw new InvalidOperationException("Actual terminal Death differential did not pass.");
                if(ended!=1 || won!=0 || CombatManager.Instance.IsInProgress) throw new InvalidOperationException("Native pending-loss lifecycle mismatch.");
                var finalMana=CaptureState(_stateType!,GetLiveManaState(player));
                if(finalMana.Echo<=0)throw new InvalidOperationException("Death lifecycle erased the nonzero native Echo fixture unexpectedly.");
                if(name=="upkeep-death" && (player.PlayerCombatState!.TurnNumber!=2 || finalMana.Mana!=0 || finalMana.Stance!="Neutral"
                    || player.Creature.Powers.Any(p=>p.GetType().Name is "StrengthPower" or "DexterityPower" && p.Amount!=0)))
                    throw new InvalidOperationException("Upkeep-death causal boundary or own stat withdrawal mismatch.");
            }
            else throw new PredictionUnsupportedException($"Unknown stage-one case {name}.");
            return Write("passed",null);
        }
        catch(ValidationStopException ex) { typedFailure=ex; Write("failed",ex.ToString()); throw; }
        catch(Exception ex) { Write("failed",ex.ToString()); throw; }
        finally { CombatManager.Instance.CombatEnded -= OnEnded; CombatManager.Instance.CombatWon -= OnWon; }

        JsonElement Action(string kind,string card) => JsonSerializer.SerializeToElement(new{kind,turn=player.PlayerCombatState!.TurnNumber,cardId=card,cardOccurrence=0,targetIndex=-1});
        object Replay(JsonElement[] actions)
        {
            string dir=Path.Combine(output,"stage-one-action-"+Directory.GetDirectories(output,"stage-one-action-*").Length);
            Directory.CreateDirectory(dir);
            string plan=Path.Combine(dir,"route.json"); File.WriteAllText(plan,JsonSerializer.Serialize(actions));
            File.WriteAllText(Path.Combine(dir,"harness-result.json"),JsonSerializer.Serialize(new{eleanorMod=new{assemblySha256=EleanorV041Contracts.AssemblySha256},scenario=new{CharacterId="MyMod",EncounterId=((CombatRoom)combat.RunState.CurrentRoom!).Encounter.Id.Entry,Seed=NativeModRunLoop.SourceSeed}}));
            var result=JsonSerializer.SerializeToElement(RunVictoryReplay(combat,loop,dir,plan,allowBoundary:true),ProtocolJson.Options);
            replays.Add(result); return result;
        }
        void RequireLivingBoundary(object result)
        {
            ReplayVerification.RequireBoundary(JsonSerializer.SerializeToElement(result,ProtocolJson.Options),1);
            if(!CombatManager.Instance.IsInProgress || player.Creature.IsDead || player.PlayerCombatState!.Phase!=PlayerTurnPhase.Play)
                throw new ValidationStopException(ValidationStop.InvalidAction,"not-living-play-boundary","Controller fixture left the native living Play boundary.");
        }
        object Write(string status,string? error)
        {
            object runtime=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="MyMod")
                .GetType("MyMod.MyModCode.Combat.ManaRuntime",true)!.GetMethod("For")!.Invoke(null,[player])!;
            string leafStatus=replays.Count>0?replays[^1].GetProperty("status").GetString()!:"boundary";
            string resultStatus=error!=null?ValidationOutcomeV1.StatusForStop(typedFailure?.Reason??ValidationStop.EngineError)
                :name.Contains("budget")?"budget-exceeded":name.StartsWith("ticket")||name.StartsWith("invalid-")?"invalid-action":leafStatus;
            var outcome=ReplayVerification.Aggregate(name,replays,resultStatus,error) with {
                ScenarioKind=name.EndsWith("control")?"injected-fault":"native-api-fixture",
                ReasonCode=typedFailure?.Code??(error!=null?"stage-one-injected-or-unexpected-engine-error":name),
                VerificationPassed=status=="passed", Coverage=error==null?CoverageOutcome.Complete:resultStatus=="unsupported"?CoverageOutcome.Unsupported:CoverageOutcome.NotRun };
            if(outcome.Validate().Count!=0)throw new InvalidOperationException("Invalid stage-one result envelope: "+string.Join(";",outcome.Validate()));
            var report=new {schema="stage-one-native-case/v1",name,status,error,outcome,confirmedActionsScope="all replay actions in this directed case; setup APIs excluded",scenarioKind=outcome.ScenarioKind,setup,recording,control,replay,replays,
                loadedAssemblies=AppDomain.CurrentDomain.GetAssemblies().Where(a=>!a.IsDynamic && !string.IsNullOrEmpty(a.Location)
                    && (a.GetName().Name is "sts2" or "0Harmony" or "GodotSharp" or "MyMod" or "BaseLib" or "CombatSolver" or "SimulatorLab.Core" || a.GetName().Name!.StartsWith("STS2-RitsuLib")))
                    .Select(a=>new {name=a.GetName().Name,path=a.Location,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location)))}).ToArray(),
                lifecycle=new{ended,won,inProgress=CombatManager.Instance.IsInProgress,hp=player.Creature.CurrentHp,
                    mana=CaptureState(_stateType!,GetLiveManaState(player)),listening=(bool)runtime.GetType().GetField("_listening",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(runtime)!,
                    semantics="Pinned ProcessPendingLoss emits CombatEnded without AfterCombatEnd; native TestMode skips run-history/metrics/UI. Residual role state is observed, never reset by Lab."},guards=CaptureOfflineWriteEvidence()};
            File.WriteAllText(Path.Combine(output,"stage-one-case.json"),JsonSerializer.Serialize(report,ProtocolJson.Options));return report;
        }
    }
}
