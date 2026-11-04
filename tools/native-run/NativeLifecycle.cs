using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Platform.Steam;
using MegaCrit.Sts2.Core.Runs;

namespace OfflineSearchHarness;

// Observation wraps original Tasks; it never replaces native completion or game effects.
internal static class NativeLifecycle
{
    internal static bool Active;
    internal static string Scenario="";
    internal static int Losses,RunEndStarts,RunEndReturns,WinStarts,WinReturns,ActStarts,ActReturns,PlatformRefreshes;
    internal static Task? ActTask;
    internal static bool Won;
    internal static bool Closed;
    internal static bool IsScenario(string scenario)=>scenario.StartsWith("tail-")||scenario.StartsWith("natural-death");
    internal static bool CaptureScenario(string scenario)=>NativeSolverBattle.IsScenario(scenario)||NativeMajor.IsScenario(scenario)||IsScenario(scenario)||NativeRooms.IsScenario(scenario)||NativeRewardProbe.IsScenario(scenario)||NativeMerchant.IsScenario(scenario)||NativeMerchantEconomy.IsScenario(scenario);
    internal static void Install()
    {
        NativeHost.Register(AccessTools.Method(typeof(RunManager),"OnEnded",[typeof(bool)]),nameof(EndBefore),typeof(NativeLifecycle),nameof(EndAfter));
        NativeHost.Register(AccessTools.Method(typeof(RunManager),"WinRun"),nameof(WinBefore),typeof(NativeLifecycle),nameof(WinAfter));
        NativeHost.Register(AccessTools.Method(typeof(RunManager),"EnterNextAct"),nameof(ActBefore),typeof(NativeLifecycle),nameof(ActAfter));
        NativeHost.Register(AccessTools.Method(typeof(CombatManager),"ProcessPendingLoss"),nameof(LossBefore),typeof(NativeLifecycle),nameof(LossAfter));
        // This original method immediately returns when Steam is uninitialized. Preserve it, assert its guard.
        NativeHost.Register(AccessTools.Method(typeof(SteamStatsManager),"RefreshGlobalStats"),nameof(PlatformBefore),typeof(NativeLifecycle));
        NativeArchitectPresentation.Install();
    }
    static void EndBefore(bool __0)
    {
        if(!Active)return;
        RunEndStarts++;Program.Observe("run-end-start",new{victory=__0});
        if(Scenario=="tail-architect-prefix-fault")throw new InvalidOperationException("labelled OnEnded prefix fault control; original body not executed");
    }
    static void EndAfter(bool __0)
    {
        if(!Active)return;
        RunEndReturns++;Won=__0;Program.Observe("run-end-return",new{victory=__0});
    }
    static void WinBefore(){if(Active){WinStarts++;Program.Observe("run-win-start");}}
    static void WinAfter(ref Task __result){if(Active)__result=Track(__result,"run-win",()=>{WinReturns++;});}
    static void ActBefore(){if(Active){ActStarts++;Program.Observe("act-transition-start");}}
    static void ActAfter(ref Task __result){if(Active)__result=ActTask=Track(__result,"act-transition",()=>{ActReturns++;});}
    static async Task Track(Task task,string source,Action finished)
    {
        try{
            await task;
            if(source=="run-win"&&Scenario=="tail-architect-win-fault")throw new InvalidOperationException("labelled post-native WinRun task fault control");
            finished();Program.Observe(source+"-completed");
        }catch(Exception ex){NativeEvidence.Emit("lifecycle-fault",new{source,error=ex.Message});throw;}
    }
    static void LossBefore(){if(Active){Program.CaptureSolverTerminal("Defeat");Program.Observe("battle-loss-start");}}
    static void LossAfter(){if(Active){if(CombatManager.Instance.IsInProgress)throw new InvalidOperationException("Loss callback not terminal");Losses++;Program.Observe("battle-loss-confirmed");}}
    static void PlatformBefore()
    {
        if(SteamInitializer.Initialized)throw new InvalidOperationException("Steam refresh guard rejected initialized platform");
        if(Active){PlatformRefreshes++;NativeEvidence.Emit("platform-offline-guard",new{source="SteamStatsManager.RefreshGlobalStats",initialized=false,behavior="original immediate return"});}
    }
    internal static object Snapshot()=>new{Losses,RunEndStarts,RunEndReturns,WinStarts,WinReturns,ActStarts,ActReturns,PlatformRefreshes,Won,Closed};
}
