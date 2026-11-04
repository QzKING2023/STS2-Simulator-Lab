using System.Text.Json;

namespace OfflineSearchHarness;

// This port has no game object, private identity, draw order, seed, RNG or boundary stamp.
internal sealed record NativePublicAction(string Handle,string Kind,string? Model);
internal static class NativePolicy
{
    internal static readonly List<object> PublicRequests=[];
    internal static int Choose(string kind,object[] nativeCandidates)
    {
        var visible=nativeCandidates.Select((c,i)=>{
            var e=JsonSerializer.SerializeToElement(c);
            return new NativePublicAction("o"+i,kind,e.TryGetProperty("model",out var m)?m.GetString():null);
        }).ToArray();
        PublicRequests.Add(new{kind,candidates=visible,information="player-visible-candidate-handles"});
        return Baseline(visible);
    }
    static int Baseline(IReadOnlyList<NativePublicAction> options)=>options.Count>0?0:throw new InvalidOperationException("No candidate");
}
