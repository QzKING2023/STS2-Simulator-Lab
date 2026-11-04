using System.Reflection.Emit;
using System.Security.Cryptography;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
namespace OfflineSearchHarness;

// Same receiver-only technique as NativeBossPresentation. Retain original Died
// unsubscription and callback; only the absent room's visual-node lookup is null.
internal static class NativeCreaturePresentation
{
 static System.Reflection.MethodInfo Target=>AccessTools.DeclaredMethod(typeof(SoulNexus),"AfterDeath");
 internal static void Install(){if(NativeFullRun.Active)InstallTarget();}
 static void InstallTarget(){
  var method=Target;
  if(Harmony.GetPatchInfo(method) is {Owners.Count:>0})throw new InvalidOperationException("Unknown SoulNexus death presentation patch");
  GameBootstrap.Harmony.Patch(method,transpiler:new HarmonyMethod(typeof(NativeCreaturePresentation),nameof(Transpile)));
  NativeHost.Seams.Add(new{method=method.ToString(),token=method.MetadataToken,mvid=method.Module.ModuleVersionId,ilSha256=Convert.ToHexString(SHA256.HashData(method.GetMethodBody()!.GetILAsByteArray()!)),handler="original death callback retained; absent visual room lookup only"});
 }
 static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions){
  var target=AccessTools.DeclaredMethod(typeof(NCombatRoom),nameof(NCombatRoom.GetCreatureNode),[typeof(Creature)]);int count=0;
  foreach(var i in instructions){if(i.Calls(target)){i.opcode=OpCodes.Call;i.operand=AccessTools.DeclaredMethod(typeof(NativeCreaturePresentation),nameof(Node));count++;}yield return i;}
  if(count!=1)throw new InvalidOperationException("Pinned death visual lookup anchor mismatch: "+count);
 }
 static NCreature? Node(NCombatRoom? room,Creature creature){
  if(room!=null)return room.GetCreatureNode(creature);
  if(!NativeFullRun.Active||!MegaCrit.Sts2.Core.TestSupport.TestMode.IsOn||MegaCrit.Sts2.Core.Nodes.NGame.Instance!=null)throw new InvalidOperationException("Missing visual room outside isolated full run");
  NativeEvidence.Emit("creature-visual-guard",new{source="SoulNexus.AfterDeath",subject=NativeIdentity.Ref(creature),roomMissing=true,operation="visual lookup only; original unsubscribe retained"});return null;
 }
 static void Noop(){}
 internal static object Audit(){
  var probe=new Harmony("lab.control.soul-visual");bool rejected=false;
  try{probe.Patch(Target,prefix:new HarmonyMethod(typeof(NativeCreaturePresentation),nameof(Noop)));try{InstallTarget();}catch(InvalidOperationException ex)when(ex.Message=="Unknown SoulNexus death presentation patch"){rejected=true;}}
  finally{probe.UnpatchAll(probe.Id);}
  if(!rejected)throw new InvalidOperationException("Unknown real Harmony patch accepted");
  return new{rejected,actualHarmony=true,businessExecutions=0};
 }
}
