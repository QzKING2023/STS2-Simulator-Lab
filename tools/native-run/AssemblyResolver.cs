using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
namespace OfflineSearchHarness;
internal static class AssemblyResolver
{
    internal static void VerifyGame()
    {
        string file=Path.Combine((string)AppContext.GetData("Sts2DataDir")!,"sts2.dll");
        if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))!="0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9")throw new InvalidOperationException("Game hash changed");
    }
    [ModuleInitializer] internal static void Install() => AssemblyLoadContext.Default.Resolving += (context,name) =>
    {
        string simple=name.Name??"";
        if(simple is "MyMod" or "CombatSolver" or "BaseLib" || simple.StartsWith("STS2-RitsuLib") ||
            simple is "MinionLib" or "shadowversebydmod")
        {
            if (MinionNativeModLoop.Enabled)
                return MinionNativeModLoop.Resolve(context, name);
            return NativeSolverProfile.Resolve(context,name);
        }
        string path=Path.Combine((string)AppContext.GetData("Sts2DataDir")!, simple+".dll");
        return File.Exists(path)?context.LoadFromAssemblyPath(path):null;
    };
}
