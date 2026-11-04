using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace OfflineSearchHarness;

// Opt-in, pinned native business branches. Global TestMode and platform/save guards stay on.
internal static class NativeMerchantEconomy
{
    internal static readonly HashSet<string> Fixtures=["merchant-economy-card","merchant-economy-potion","merchant-economy-remove","merchant-economy-cancel"];
    internal static readonly HashSet<string> Faults=["merchant-economy-stale","merchant-economy-wrong-inventory","merchant-economy-task-fault"];
    internal static bool IsScenario(string s)=>Fixtures.Contains(s)||Faults.Contains(s)||s=="merchant-natural"||NativeMerchantExpansion.IsScenario(s);
    internal static bool Active=>IsScenario(NativeLifecycle.Scenario);
    internal static bool Natural=>NativeLifecycle.Scenario=="merchant-natural"||NativeMerchantExpansion.Natural;
    internal static string BaseScenario(string s)=>s.Replace("merchant-economy-","merchant-");
    internal const string Profile="native-merchant-economy/v1";
    internal static int Prices,ExitStarts,ExitReturns;
    internal static void Install()
    {
        if(!Active)return;
        AssemblyResolver.VerifyGame();
        Patch(AccessTools.Method(typeof(MerchantPotionEntry),nameof(MerchantPotionEntry.CalcCost)),"get_IsOff");
        Patch(AccessTools.Method(typeof(MerchantRoom),nameof(MerchantRoom.Exit)),"get_IsOn");
        Patch(AccessTools.Method(typeof(RunManager),"UpdatePlayerStatsInMapPointHistory"),"get_IsOn");
    }
    // Prefix/postfix are applied together with the transpiler to avoid accepting pre-existing patches.
    static void Patch(MethodInfo method,string getter)
    {
        if(Harmony.GetPatchInfo(method) is {Owners.Count:>0})throw new InvalidOperationException("Unknown merchant business patch");
        if(method.Module.Assembly!=typeof(RunState).Assembly)throw new InvalidOperationException("Wrong merchant assembly");
        expected[method]=getter;
        // Register observes the same target below, so install all three pieces atomically here.
        string prefix=method.Name=="CalcCost"?nameof(PriceBefore):method.Name=="Exit"?nameof(ExitBefore):nameof(StatsBefore);
        string postfix=method.Name=="CalcCost"?nameof(PriceAfter):method.Name=="Exit"?nameof(ExitAfter):nameof(StatsAfter);
        GameBootstrap.Harmony.Patch(method,prefix:new HarmonyMethod(typeof(NativeMerchantEconomy),prefix),postfix:new HarmonyMethod(typeof(NativeMerchantEconomy),postfix),transpiler:new HarmonyMethod(typeof(NativeMerchantEconomy),nameof(BusinessGuard)));
        NativeHost.Seams.Add(new{method=method.DeclaringType!.FullName+"."+method.Name,token=method.MetadataToken,mvid=method.Module.ModuleVersionId,il=Convert.ToHexString(SHA256.HashData(method.GetMethodBody()!.GetILAsByteArray()!)),profile=Profile,guard=getter,expectedMatches=1,behavior="original native body; local TestMode branch only"});
    }
    static readonly Dictionary<MethodBase,string> expected=[];
    internal static void InstallPriceOnly()=>Patch(AccessTools.Method(typeof(MerchantPotionEntry),nameof(MerchantPotionEntry.CalcCost)),"get_IsOff");
    static IEnumerable<CodeInstruction> BusinessGuard(IEnumerable<CodeInstruction> source,MethodBase __originalMethod)
    {
        string getter=expected[__originalMethod];int matches=0;
        foreach(var instruction in source){
            if(instruction.Calls(AccessTools.PropertyGetter(typeof(TestMode),getter=="get_IsOn"?"IsOn":"IsOff"))){
                instruction.opcode=getter=="get_IsOn"?OpCodes.Ldc_I4_0:OpCodes.Ldc_I4_1;instruction.operand=null;matches++;
            }
            yield return instruction;
        }
        if(matches!=1)throw new InvalidOperationException("Pinned merchant TestMode guard mismatch");
    }
    static void PriceBefore(MerchantPotionEntry __instance,out JsonElement __state)
    {
        __state=JsonSerializer.SerializeToElement(__instance._player.PlayerRng.Shops.ToSerializable(),Program.Json);
        Program.Observe("merchant-price-start",NativeMerchantExpansion.Active?NativeMerchantExpansion.PriceSource(__instance,__state,true):new{entry=NativeIdentity.Ref(__instance),before=__state});
    }
    static void PriceAfter(MerchantPotionEntry __instance,JsonElement __state)
    {
        Prices++;
        var facts=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(new{profile=Profile,entry=NativeIdentity.Ref(__instance),model=__instance.Model!.Id.ToString(),rarity=__instance.Model.Rarity.ToString(),cost=__instance.Cost,before=__state,after=__instance._player.PlayerRng.Shops.ToSerializable()},Program.Json))!;
        if(NativeMerchantExpansion.Active){var source=JsonSerializer.SerializeToElement(NativeMerchantExpansion.PriceSource(__instance,__state,false));facts["offeredSource"]=System.Text.Json.Nodes.JsonNode.Parse(source.GetProperty("offeredSource").GetRawText());facts["generation"]=source.GetProperty("generation").GetInt32();}
        Program.Observe("merchant-price",facts);
    }
    static void ExitBefore(MerchantRoom __instance)
    {
        if(!ReferenceEquals(__instance,Program.MerchantRoom))throw new InvalidOperationException("Wrong merchant exit session");
        ExitStarts++;Program.Observe("merchant-exit-start",new{inventory=NativeIdentity.Ref(NativeMerchant.Inventory),room=NativeIdentity.Ref(__instance)});
    }
    static void ExitAfter(ref Task __result)=>__result=FinishExit(__result);
    static async Task FinishExit(Task original)
    {
        await original;NativeMerchant.Closed=true;ExitReturns++;
        Program.Observe("merchant-exit-return",new{inventory=NativeIdentity.Ref(NativeMerchant.Inventory),room=NativeIdentity.Ref(Program.MerchantRoom)});
    }
    static void StatsBefore()=>Program.Observe("merchant-stats-start",new{profile=Profile});
    static void StatsAfter()=>Program.Observe("merchant-stats-return",new{profile=Profile});
    internal static object History(IRunState? run)
        =>run==null?Array.Empty<object>():run.MapPointHistory.Select((act,a)=>act.Select((h,i)=>new{act=a,index=i,reference=NativeIdentity.Ref(h),type=h.MapPointType.ToString(),player=h.GetEntry(run.Players.Single().NetId),rooms=h.Rooms}).ToArray()).ToArray();
}
