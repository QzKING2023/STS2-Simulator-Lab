using System.Diagnostics;
using System.Text.Json;

namespace OfflineSearchHarness;
internal static class NativeTiming
{
    static readonly Dictionary<string,long> ticks=[];
    static readonly Dictionary<string,int> calls=[];
    internal static IDisposable Measure(string key)=>new Measurement(key);
    sealed class Measurement(string key) : IDisposable
    {
        readonly long start=Stopwatch.GetTimestamp();
        public void Dispose(){ticks[key]=ticks.GetValueOrDefault(key)+Stopwatch.GetTimestamp()-start;calls[key]=calls.GetValueOrDefault(key)+1;}
    }
    internal static void Write(string directory)=>File.WriteAllText(Path.Combine(directory,"timing.json"),JsonSerializer.Serialize(new{scope="overlapping wall durations; do not sum nested phases",peakWorkingSetBeforeExit=Process.GetCurrentProcess().PeakWorkingSet64,milliseconds=ticks.ToDictionary(t=>t.Key,t=>t.Value*1000d/Stopwatch.Frequency),calls},Program.Json));
}
