using System.Reflection.Emit;
using System.Security.Cryptography;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Audio;
namespace OfflineSearchHarness;

// Preserve each exact native callback, including IsOverOrEnding query.
// Its one audio call already has a TestMode no-op body, but callvirt null fails first.
internal static class NativeBossPresentation
{
 internal static int Skips;
 internal static void Install()
 {
  if(!NativeNaturalBoss.Act2)return;
  var methods=new[]{AccessTools.DeclaredMethod(typeof(Rocket),nameof(Rocket.BeforeDeath)),AccessTools.DeclaredMethod(typeof(Crusher),nameof(Crusher.BeforeDeath))};
  foreach(var method in methods)if(Harmony.GetPatchInfo(method) is {Owners.Count:>0})throw new InvalidOperationException("Unknown "+method.DeclaringType!.Name+" death patch");
  foreach(var method in methods){
   GameBootstrap.Harmony.Patch(method,transpiler:new HarmonyMethod(typeof(NativeBossPresentation),nameof(Transpile)));
   NativeHost.Seams.Add(new{method=method.ToString(),token=method.MetadataToken,mvid=method.Module.ModuleVersionId,ilSha256=Convert.ToHexString(SHA256.HashData(method.GetMethodBody()!.GetILAsByteArray()!)),handler=method.DeclaringType!.Name+" null audio receiver only; original callback retained"});
  }
 }
 static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions,System.Reflection.MethodBase __originalMethod)
 {
  var original=AccessTools.DeclaredMethod(typeof(NAudioManager),nameof(NAudioManager.PlayOneShot),[typeof(string),typeof(float)]);int count=0;
  string handler=__originalMethod.DeclaringType==typeof(Rocket)?nameof(RocketAudio):__originalMethod.DeclaringType==typeof(Crusher)?nameof(CrusherAudio):throw new InvalidOperationException("Unknown death callback");
  foreach(var i in instructions){if(i.Calls(original)){i.opcode=OpCodes.Call;i.operand=AccessTools.DeclaredMethod(typeof(NativeBossPresentation),handler);count++;}yield return i;}
  if(count!=1)throw new InvalidOperationException("Pinned death audio anchor mismatch: "+count);
 }
 static void RocketAudio(NAudioManager? receiver,string path,float volume)=>Audio(receiver,path,volume,"Rocket.BeforeDeath");
 static void CrusherAudio(NAudioManager? receiver,string path,float volume)=>Audio(receiver,path,volume,"Crusher.BeforeDeath");
 static void Audio(NAudioManager? receiver,string path,float volume,string source)
 {
  if(receiver!=null){receiver.PlayOneShot(path,volume);return;}
  if(!NativeNaturalBoss.Act2||MegaCrit.Sts2.Core.Nodes.NGame.Instance!=null||!MegaCrit.Sts2.Core.TestSupport.TestMode.IsOn)throw new InvalidOperationException("Unexpected missing audio receiver");
  Skips++;NativeEvidence.Emit("boss-audio-guard",new{source,receiverMissing=true,testMode=true,operation="original TestMode PlayOneShot no-op"});
 }
}
