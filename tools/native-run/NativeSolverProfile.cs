using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
namespace OfflineSearchHarness;

// Default builds keep their original blanket rejection. The separate solver
// profile accepts only its exact hashed compute inputs, never gameplay Mods.
internal static class NativeSolverProfile
{
#if SOLVER_GATE
    internal const bool Enabled=true;
    static JsonElement? inputLock;
    static JsonElement Lock=>inputLock??=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"solver-lock.json"))).RootElement;
    static JsonElement Row(string name)=>Lock.GetProperty("assemblies").EnumerateArray().Single(r=>r.GetProperty("name").GetString()==name);
    static void Validate(string path,string expected){if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))!=expected)throw new InvalidOperationException("Solver dependency hash mismatch: "+path);}
    internal static Assembly Resolve(AssemblyLoadContext context,AssemblyName name)
    {
        string simple=name.Name!;
        if(simple is "MyMod" or "BaseLib")throw new InvalidOperationException("Gameplay Mod forbidden: "+simple);
        var row=Row(simple);string path=Path.Combine(AppContext.BaseDirectory,row.GetProperty("file").GetString()!);
        Validate(path,row.GetProperty("sha256").GetString()!);
        if(AssemblyName.GetAssemblyName(path).Name!=simple)throw new InvalidOperationException("Solver dependency identity mismatch");
        return context.LoadFromAssemblyPath(path);
    }
    internal static void Ensure()
    {
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){
            string name=assembly.GetName().Name!;
            if(name is "MyMod" or "BaseLib")throw new InvalidOperationException("Gameplay Mod loaded in solver profile");
            if(name!="CombatSolver"&&!name.StartsWith("STS2-RitsuLib"))continue;
            var row=Row(name);string expected=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,row.GetProperty("file").GetString()!));
            if(Path.GetFullPath(assembly.Location)!=expected)throw new InvalidOperationException("Solver dependency outside pinned output");
            Validate(assembly.Location,row.GetProperty("sha256").GetString()!);
        }
    }
    internal static void Initialize(string output)=>Program.InitializeSolver(output);
#else
    internal const bool Enabled=false;
    internal static Assembly Resolve(AssemblyLoadContext context,AssemblyName name)=>throw new InvalidOperationException("Native-only host refuses Mod dependency: "+name.Name);
    internal static void Ensure(){if(NativeFoundationModLoop.Enabled||WatcherNativeModLoop.Enabled||KaguyaNativeModLoop.Enabled||MinionNativeModLoop.Enabled)return;if(AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name is "MyMod" or "CombatSolver" or "BaseLib"||a.GetName().Name!.StartsWith("STS2-RitsuLib")))throw new InvalidOperationException("Mod assembly loaded in native host");}
    internal static void Initialize(string output){}
#endif
}
