using System.Reflection;
using System.Diagnostics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Platform.Steam;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;

namespace OfflineSearchHarness;
internal static class NativeHost
{
    internal static int ProgressWrites, ReplayWrites, TelemetryCalls;
    internal static readonly List<Exception> Faults=[];
    internal static readonly List<object> Seams=[];
    internal static bool RunWon;
    internal static int BattleVictories;
    internal static object? LastVictoriousRoom;
    internal static Action<string>? Observe;
    static string? patchStamp;
    internal static object? IsolationEvidence;
    static readonly Stopwatch Clock=Stopwatch.StartNew();
    internal static void Install()
    {
        GameBootstrap.ApplyGodotBypasses();
        GameBootstrap.SkipGodotNodeStaticConstructors();
        Patch(typeof(RunManager),"WriteReplay",nameof(SkipReplay),[typeof(bool)]);
        Patch(typeof(SaveManager).Assembly.GetType("MegaCrit.Sts2.Core.Saves.Managers.ProgressSaveManager",true)!,"SaveProgress",nameof(SkipProgress),Type.EmptyTypes);
        Patch(typeof(PreloadManager),"LoadRoomEventAssets",nameof(Completed));
        Patch(typeof(PreloadManager),"LoadRoomTreasureAssets",nameof(Completed));
        Patch(typeof(PreloadManager),"LoadRoomMerchantAssets",nameof(Completed));
        // Neow eagerly builds relic hover tips for all candidates. Only the texture getter is presentation.
        PatchMethod(AccessTools.PropertyGetter(typeof(MegaCrit.Sts2.Core.Models.EnchantmentModel),"Icon")!,nameof(NullTexture));
        // Ancient option generation builds power hover tips. The pinned getter
        // only loads an icon texture; keep the original options and RNG path.
        if(NativeNaturalBoss.Active)PatchMethod(AccessTools.PropertyGetter(typeof(MegaCrit.Sts2.Core.Models.PowerModel),"Icon")!,nameof(NullTexture2D));
        Patch(typeof(AssetCache),"GetTexture2D",nameof(NullTexture2D));
        Patch(typeof(AssetCache),"GetCompressedTexture2D",nameof(NullTexture));
        Patch(typeof(MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet),"Clear",nameof(SkipHoverClear));
        PatchMethod(typeof(CombatManager).GetMethods(BindingFlags.Instance|BindingFlags.NonPublic)
            .Single(m=>m.Name=="EndCombatInternal" && m.GetParameters().Length==1),nameof(ObserveBattleVictory));
        GameBootstrap.Harmony.Patch(typeof(Godot.Time).TypeInitializer!,prefix:new HarmonyMethod(typeof(NativeHost),nameof(SkipTimeInitializer)));
        // Only static input-name constants; native reward alternatives and their effects remain intact.
        GameBootstrap.Harmony.Patch(typeof(MegaCrit.Sts2.Core.ControllerInput.MegaInput).TypeInitializer!,prefix:new HarmonyMethod(typeof(NativeHost),nameof(SkipTimeInitializer)));
        Patch(typeof(Godot.Time),"GetTicksMsec",nameof(TicksMsec));
        Patch(typeof(Godot.Time),"GetTicksUsec",nameof(TicksUsec));
        Patch(typeof(Godot.Time),"GetUnixTimeFromSystem",nameof(UnixTime));
        // Capture the original TaskHelper fault signal, before EventSynchronizer discards its faulted tasks.
        TaskHelper.UnobservedFault += ex=>Faults.Add(ex);
        var sentry=typeof(TaskHelper).Assembly.GetType("MegaCrit.Sts2.Core.Debug.SentryService",true)!;
        foreach(var method in sentry.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>m.Name.StartsWith("Capture") && m.ReturnType==typeof(void)))
            PatchMethod(method,nameof(SkipTelemetry));
        InstallVictoryCondition();
        NativePreparation.Install();
        NativeRepeatTreasure.Install();
        NativeBossPresentation.Install();
        NativeAmalgamator.Install();
        NativeCreaturePresentation.Install();
        if(NativeNaturalBoss.Third||NativeFullRun.Active)PatchMethod(HarmonyLib.AccessTools.DeclaredMethod(typeof(MegaCrit.Sts2.Core.MonsterMoves.Intents.StunIntent),"GetStaticHoverTip"),nameof(ThirdStunTip));
        NativeLifecycle.Install();
        NativeChoices.Install();
        NativeRewardClosure.Install();
        NativeRewardQuest.Install();
        NativeObservation.Install();
        NativeDeathObservation.Install();
        NativeEventFlow.Install();
        NativeMerchant.Install();
        NativeMerchantEconomy.Install();
        NativeMerchantExpansion.Install();
        NativeThirdShop.Install();
        NativeAncientAcquisition.Install();
        NativeTreasureLedger.Install();NativeEventResourceLedger.Install();
        NativeCrystalSphere.Install();
    }
    internal static void Register(MethodInfo method,string prefix,Type handler,string? postfix=null)
    {
        if(Harmony.GetPatchInfo(method) is {Owners.Count:>0})throw new InvalidOperationException("Unexpected patch on "+method);
        GameBootstrap.Harmony.Patch(method,prefix:new HarmonyMethod(handler,prefix),postfix:postfix==null?null:new HarmonyMethod(handler,postfix));
        Seams.Add(new{method=method.DeclaringType!.FullName+"."+method.Name,token=method.MetadataToken,mvid=method.Module.ModuleVersionId,handler=handler.Name+"."+prefix,postfix});
    }
    private static void Patch(Type type,string method,string replacement,Type[]? args=null)
        =>PatchMethod(args==null?AccessTools.Method(type,method)!:AccessTools.Method(type,method,args)!,replacement);
    private static void PatchMethod(MethodInfo target,string replacement,bool postfix=false)
    {
        if(target==null) throw new MissingMethodException(replacement);
        if(Harmony.GetPatchInfo(target) is { Owners.Count:>0 }) throw new InvalidOperationException("Unexpected patch on "+target);
        var handler=new HarmonyMethod(typeof(NativeHost).GetMethod(replacement,BindingFlags.Static|BindingFlags.NonPublic)!);
        GameBootstrap.Harmony.Patch(target,prefix:postfix?null:handler,postfix:postfix?handler:null);
        Seams.Add(new{method=target.DeclaringType!.FullName+"."+target.Name,token=target.MetadataToken,mvid=target.Module.ModuleVersionId,handler=replacement});
    }
    private static bool ThirdStunTip(ref MegaCrit.Sts2.Core.HoverTips.HoverTip __result)
    {
        if(!(NativeNaturalBoss.Third||NativeFullRun.Active)||!TestMode.IsOn||MegaCrit.Sts2.Core.Nodes.NGame.Instance!=null)throw new InvalidOperationException("Third Ancient visual tip outside isolated host");
        __result=new MegaCrit.Sts2.Core.HoverTips.HoverTip(new MegaCrit.Sts2.Core.Localization.LocString("intents","STUN.title"),new MegaCrit.Sts2.Core.Localization.LocString("intents","STUN.description"),(Godot.Texture2D)null!);
        NativeEvidence.Emit("third-tip-presentation",new{source="StunIntent.GetStaticHoverTip",textureMissing=true,testMode=true});return false;
    }
    private static bool SkipReplay(){ReplayWrites++;return false;}
    private static bool SkipProgress(){ProgressWrites++;return false;}
    private static bool SkipTelemetry(){TelemetryCalls++;return false;}
    private static bool Completed(ref Task __result){__result=Task.CompletedTask;return false;}
    private static bool NullTexture(ref Godot.CompressedTexture2D __result){__result=null!;return false;}
    private static bool NullTexture2D(ref Godot.Texture2D __result){__result=null!;return false;}
    private static bool SkipHoverClear()=>false;
    internal static void InstallVictoryCondition()=>Register(typeof(CombatManager).GetMethods(BindingFlags.Instance|BindingFlags.NonPublic).Single(m=>m.Name=="IsCombatEnding"&&m.GetParameters().Length==1),nameof(VictoryConditionBefore),typeof(NativeHost),nameof(VictoryConditionAfter));
    private static void VictoryConditionBefore() { }
    private static void VictoryConditionAfter(MegaCrit.Sts2.Core.Combat.CombatTurnState __0,bool __result)
    {
        if(!NativeMajor.Active||!Program.CombatActionActive||!__result||__0.PendingLoss!=null||!__0.IsInProgress)return;
        if(__0.State.Enemies.Any(e=>e!=null&&e.IsAlive&&e.IsPrimaryEnemy))return;
        var cursor=NativeObservation.Cursor;
        Program.Observe("major-victory-condition",new{source="CombatManager.IsCombatEnding",ending=__result,noPendingLoss=true,livingPrimaryEnemies=__0.State.Enemies.Count(e=>e!=null&&e.IsAlive&&e.IsPrimaryEnemy),epoch=cursor.Epoch,index=cursor.Index});
    }
    private static void ObserveBattleVictory(){Program.CaptureSolverTerminal("Victory");LastVictoriousRoom=Program.BoundaryRoom;BattleVictories++;Observe?.Invoke("battle-victory-before-cleanup");}
    private static bool SkipTimeInitializer()=>false;
    private static bool TicksMsec(ref ulong __result){__result=(ulong)Clock.ElapsedMilliseconds;return false;}
    private static bool TicksUsec(ref ulong __result){__result=(ulong)(Clock.ElapsedTicks*1_000_000L/Stopwatch.Frequency);return false;}
    private static bool UnixTime(ref double __result){__result=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()/1000d;return false;}
    private static void ObserveRunEnded(bool __0){RunWon=__0;}
    private static void RejectUnimplementedChoice(){throw new NotSupportedException("N1 native sub-choice provider not implemented; automatic TestMode selection refused");}
    internal static void Ensure()
    {
        NativePreparation.Ensure();
        string current=string.Join("\n",Harmony.GetAllPatchedMethods().Select(m=>m.Module.ModuleVersionId+":"+m.MetadataToken+":"+string.Join(",",Harmony.GetPatchInfo(m)!.Prefixes.Concat(Harmony.GetPatchInfo(m)!.Postfixes).Concat(Harmony.GetPatchInfo(m)!.Transpilers).Concat(Harmony.GetPatchInfo(m)!.Finalizers).Select(p=>p.owner+":"+p.PatchMethod.Module.ModuleVersionId+":"+p.PatchMethod.MetadataToken))).Order());
        if(patchStamp!=null && patchStamp!=current)throw new InvalidOperationException("Native host patch inventory changed");
        patchStamp=current;
        if(!TestMode.IsOn || SteamInitializer.Initialized || RunManager.Instance.ShouldSave || RunManager.Instance.DailyTime!=null)
            throw new InvalidOperationException("Offline host isolation invariant failed");
        if(AccessTools.Field(typeof(SaveManager),"_saveStore").GetValue(SaveManager.Instance) is not MemorySaveStore)
            throw new InvalidOperationException("Non-memory save backend");
        NativeSolverProfile.Ensure();
        if(Faults.Count>0){
            Exception? rejected=Faults.SelectMany(e=>e is AggregateException a?a.Flatten().InnerExceptions:new[]{e}.AsEnumerable()).FirstOrDefault(e=>e is SimulatorLab.Core.NativeActionRejectedException);
            if(rejected!=null)throw rejected;
            throw new AggregateException("Native asynchronous task failed",Faults);
        }
        if(MegaCrit.Sts2.Core.Nodes.NGame.Instance!=null || RunManager.Instance.NetService is {} net && net.GetType().Name!="NetSingleplayerGameService")
            throw new InvalidOperationException("Unexpected native UI/network host");
        NativeEvidence.Ensure();
        IsolationEvidence=new{testMode=TestMode.IsOn,steamInitialized=SteamInitializer.Initialized,shouldSave=RunManager.Instance.ShouldSave,daily=RunManager.Instance.DailyTime,
            backend="MemorySaveStore",networkService=RunManager.Instance.NetService?.GetType().Name,uiInstance=MegaCrit.Sts2.Core.Nodes.NGame.Instance!=null,patchInventory=patchStamp};
    }
}
