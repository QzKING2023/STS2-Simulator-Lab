using System.Text.Json;

namespace OfflineSearchHarness;

// Internal registered evidence metadata. Origin never depends on the existence of a replay file.
internal sealed record NativeScenario(string Scenario,string ScenarioKind,string ExecutionKind,string Observation,string HostControl,string InputControl,bool Replay,bool Branch=false)
{
    internal static readonly HashSet<string> Fixtures=["solver-power-death","solver-self-cost","solver-tainted-lifecycle","solver-choice-potion","grid","multi","empty","single","manual-single","bundle","bundle-shared","armaments","headbutt","headbutt-empty","headbutt-single","deck-upgrade","deck-generic","sequential","hand","choose-card","baseline-first","provider-first"];
    static readonly HashSet<string> Faults=["invalid","missing","duplicate","too-few","too-many","stale","wrong-request","cross-room","reused","extra","unknown-field"];
    internal static NativeScenario Describe(string scenario,bool replay,string mode,string hostControl,string inputControl,bool branch=false)
    {
        if(mode is not ("off" or "minimal" or "detailed"))throw new ArgumentException("Unknown observation mode");
        if(hostControl is not ("" or "async-fault" or "stale" or "budget" or "subchoice"))throw new ArgumentException("Unknown host control");
        if(inputControl is not ("" or "root-extra" or "unknown-choice-field" or "missing-choice-file" or "invalid-route"))throw new ArgumentException("Unknown input control");
        if(inputControl=="invalid-route"&&!replay)throw new ArgumentException("Invalid-route control requires explicit replay input");
        var parts=scenario.Split(':');bool natural=scenario is "slice-skip" or "slice-take" or "natural-death" or "slice-solver-gate" or "slice-solver-loop";
        natural|=NativeVisibleBattle.IsScenario(scenario)||NativePerformancePilot.IsScenario(scenario)||NativeSolverRun.IsScenario(scenario)||scenario=="rooms-natural"||scenario=="merchant-natural"||scenario=="solver-battle-natural";
        natural|=scenario=="rewards-natural"||scenario=="merchant-natural-relic"||WatcherNativeModLoop.IsScenario(scenario)||KaguyaNativeModLoop.IsScenario(scenario)||MinionNativeModLoop.IsScenario(scenario);
        bool fixture=NativeEventFixtures.Scenarios.Contains(scenario)||scenario=="solver-battle-death"||NativeSolverOnline.IsScenario(scenario)||Fixtures.Contains(scenario)||scenario is "tail-next-act" or "tail-architect"||NativeRooms.IsFixture(scenario);
        bool fault=scenario is "event-fault" or "sink" or "quota" or "softlock" or "tail-architect-prefix-fault" or "tail-architect-win-fault" or "natural-death-budget" or "natural-death-fault" || parts.Length==2&&Fixtures.Contains(parts[0])&&Faults.Contains(parts[1]);
        fixture|=NativeMerchantExpansion.Fixtures.Contains(scenario);fault|=NativeMerchantExpansion.Faults.Contains(scenario);
        fault|=NativeRooms.IsFault(scenario)||NativeMerchantEconomy.Faults.Contains(scenario);
        fixture|=NativeMerchantEconomy.Fixtures.Contains(scenario);
        natural|=NativeEventFlow.NaturalScenario(scenario);fixture|=NativeEventFlow.FixtureScenario(scenario);fault|=NativeEventFlow.FaultScenario(scenario);
        natural|=NativeMajor.NaturalScenario(scenario);fixture|=NativeMajor.FixtureScenario(scenario);fault|=NativeMajor.ControlScenario(scenario);
        natural|=scenario=="closure-neow";fault|=NativeRewardClosure.Faults.Contains(scenario);fixture|=NativeRewardClosure.IsScenario(scenario)&&scenario!="closure-neow";
        fixture|=NativeRewardProbe.Fixtures.Contains(scenario)||NativeMerchant.Fixtures.Contains(scenario);fault|=NativeRewardProbe.Faults.Contains(scenario)||NativeMerchant.Faults.Contains(scenario);
        if(!natural&&!fixture&&!fault)throw new ArgumentException("Unregistered native scenario");
        if(branch&&(!replay||scenario!="major-natural-act3-route"))throw new ArgumentException("Branch scenario provenance");
        return new(scenario,fault||hostControl!=""||inputControl!=""?"fault-control":natural?"natural-run":"api-fixture",NativeSolverCoordinator.IsScenario(scenario)?(scenario.EndsWith("-replay")?"frozen-prefix-and-tail-replay":(NativePerformancePilot.IsScenario(scenario)||NativeSolverCoordinator.Configuration?.PrefixCount==0?"live":"prefix-replay/live-tail")):branch?"prefix-branch":mode!="detailed"?"observation-control":replay?"frozen-replay":"live",mode,hostControl,inputControl,replay,branch);
    }
    internal static NativeScenario Read(JsonElement e)=>e.Deserialize<NativeScenario>(Program.Json)??throw new NativeDataException("Missing provenance");
}
