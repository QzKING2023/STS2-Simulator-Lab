using System.Text.Json;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using SimulatorLab.Core;
namespace OfflineSearchHarness;

// Internal, bounded first-battle profile. No scoring, content or save overrides.
internal static class NativePerformancePilot
{
    internal static bool IsScenario(string s)=>s is "solver-character-pilot" or "solver-character-pilot-replay";
    internal static readonly string[] Characters=["Ironclad","Silent","Regent","Necrobinder","Defect"];
    internal static bool IsFull(NativeSolverCoordinator.Plan? p)=>p?.Pilot=="character-full/v1";
    internal static void Validate(NativeSolverCoordinator.Plan p,string scenario)
    {
        if(IsFull(p)){
            if(!NativeDarv.IsActTwoProfile(scenario)||!Characters.Contains(p.Character,StringComparer.Ordinal)||p.Run?.Schema!="native-full-run/v2"||p.PrefixCount!=0||p.MaxActions!=1000||p.Probe||p.SearchStudy||p.ParallelProbe||p.SearchParallelism!=2||p.ExecutionPolicy!="RunToTerminal"||p.ReturnedAggregateNodeLimit!=16384)
                throw new NativeActionRejectedException("Full character pilot requires registered original run limits and Oracle policy");
            return;
        }
        if(p.Pilot!=""||p.SearchStudy&&!IsScenario(scenario))throw new NativeActionRejectedException("Unknown pilot or study surface");
        if(!IsScenario(scenario)){
            if(p.Character!="Ironclad")throw new NativeActionRejectedException("Character selection is restricted to the registered pilot");
            return;
        }
        if(!Characters.Contains(p.Character,StringComparer.Ordinal)||p.Run!=null||p.PrefixCount!=0||p.Probe||p.MaxActions!=64||p.SearchParallelism!=1||p.ParallelProbe||p.ExecutionPolicy!="Guarded"||p.ReturnedAggregateNodeLimit!=4096)
            throw new NativeActionRejectedException("Character pilot requires its preregistered bounded Oracle profile");
    }
    internal static CharacterModel Select(string scenario)
    {
        if(!IsScenario(scenario)&&!IsFull(NativeSolverCoordinator.Configuration))return ModelDb.Get<Ironclad>();
        string requested=NativeSolverCoordinator.Configuration!.Character;
        var matches=ModelDb.All.OfType<CharacterModel>().Where(c=>c.GetType().Assembly==typeof(Ironclad).Assembly&&c.GetType().Name==requested).ToArray();
        if(matches.Length!=1)throw new NotSupportedException("Fixed native character catalog does not uniquely contain "+requested);
        return matches[0];
    }
    internal static void Review(JsonElement header,JsonElement[] rows)
    {
        string scenario=header.GetProperty("scenario").GetString()!;
        bool full=header.TryGetProperty("coordinatorProfile",out var c)&&c.ValueKind==JsonValueKind.Object&&c.TryGetProperty("Pilot",out var p)&&p.GetString()=="character-full/v1";
        if(!IsScenario(scenario)&&!full)return;
        var config=header.GetProperty("coordinatorProfile").Deserialize<NativeSolverCoordinator.Plan>()??throw new NativeDataException("Pilot profile missing");
        Validate(config,scenario);
        var created=rows.Single(r=>r.GetProperty("kind").GetString()=="new-run");
        string actual=created.GetProperty("detail").GetProperty("facts").GetProperty("character").GetString()!;
        if(actual!="CHARACTER."+config.Character.ToUpperInvariant()||header.GetProperty("information").GetString()!="Oracle")
            throw new NativeDataException("Pilot character/information binding mismatch");
    }
}
