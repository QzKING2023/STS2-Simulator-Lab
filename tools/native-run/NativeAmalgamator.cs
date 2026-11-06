using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
namespace OfflineSearchHarness;

// Native deck choice/removal/creation remain unchanged. Only the two null UI
// receiver calls in each pinned async method are transported to a headless seam.
internal static class NativeAmalgamator
{
 static bool installed;
 static MethodInfo[] Bodies()=>new[]{"CombineStrikes","CombineDefends"}.Select(n=>AccessTools.DeclaredMethod(typeof(Amalgamator),n).GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType).Select(t=>AccessTools.DeclaredMethod(t,"MoveNext")).ToArray();
 static IEnumerable<MethodInfo> Methods()=>typeof(Amalgamator).GetMethods(BindingFlags.DeclaredOnly|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static).Concat(Bodies());
 internal static void Audit(){
  var body=Bodies();var expected=AccessTools.DeclaredMethod(typeof(NativeAmalgamator),nameof(Transpile));
  foreach(var m in Methods()){
   if(m.Module!=typeof(EventModel).Module)throw new NotSupportedException("Amalgamator source identity");
   var p=Harmony.GetPatchInfo(m);var all=p==null?[]:p.Prefixes.Concat(p.Postfixes).Concat(p.Transpilers).Concat(p.Finalizers).ToArray();
   if(installed&&body.Contains(m)){
    if(all.Length!=1||p!.Transpilers.Count!=1||all[0].PatchMethod!=expected||all[0].owner!=GameBootstrap.Harmony.Id)throw new NotSupportedException("Amalgamator presentation patch identity");
   }else if(all.Length!=0)throw new NotSupportedException("Unknown Amalgamator source patch");
  }
 }
 internal static void Install(){
  if(!NativeFullRun.Active)return;Audit();
  foreach(var m in Bodies()){
   GameBootstrap.Harmony.Patch(m,transpiler:new HarmonyMethod(typeof(NativeAmalgamator),nameof(Transpile)));
   NativeHost.Seams.Add(new{method=m.DeclaringType!.FullName+"."+m.Name,token=m.MetadataToken,mvid=m.Module.ModuleVersionId,ilSha256=Convert.ToHexString(SHA256.HashData(m.GetMethodBody()!.GetILAsByteArray()!)),handler="two null screen-shake calls only; original choice/removal/creation and delay retained"});
  }
  installed=true;Audit();
 }
 static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> input){
  var target=AccessTools.DeclaredMethod(typeof(NGame),nameof(NGame.ScreenShakeTrauma),[typeof(ShakeStrength)]);int count=0;
  foreach(var i in input){if(i.Calls(target)){i.opcode=OpCodes.Call;i.operand=AccessTools.DeclaredMethod(typeof(NativeAmalgamator),nameof(Shake));count++;}yield return i;}
  if(count!=2)throw new InvalidOperationException("Pinned Amalgamator visual IL anchor mismatch");
 }
 static void Shake(NGame? receiver,ShakeStrength strength){
  if(receiver!=null){receiver.ScreenShakeTrauma(strength);return;}
  if(!NativeFullRun.Active||!MegaCrit.Sts2.Core.TestSupport.TestMode.IsOn||NGame.Instance!=null)throw new InvalidOperationException("Amalgamator null visual outside isolated host");
  NativeEvidence.Emit("amalgamator-visual-guard",new{operation="ScreenShakeTrauma",strength=strength.ToString(),receiverMissing=true,businessReplaced=false});
 }
 internal static void Enter(Amalgamator e){Audit();if(!installed||e.GetType().GetMethod(nameof(EventModel.OnRoomEnter))!.DeclaringType!=typeof(EventModel))throw new NotSupportedException("Unreviewed Amalgamator entry");e.OnRoomEnter();}
 static void Noop(){}
 internal static object Controls(){
  Audit();int rejected=0;var h=new Harmony("lab.control.amalgamator");
  foreach(var m in new[]{AccessTools.DeclaredMethod(typeof(Amalgamator),"GenerateInitialOptions"),AccessTools.DeclaredMethod(typeof(Amalgamator),"CombineStrikes"),AccessTools.DeclaredMethod(typeof(Amalgamator),"CombineDefends")}.Concat(Bodies())){
   try{h.Patch(m,prefix:new HarmonyMethod(typeof(NativeAmalgamator),nameof(Noop)));try{Audit();throw new NativeDataException("Unknown event patch accepted");}catch(NotSupportedException){rejected++;}}finally{h.UnpatchAll(h.Id);}
  }
  Audit();return new{actualHarmonyRejected=rejected,businessExecutions=0};
 }
 internal static object Review(string directory){
  var rows=File.ReadLines(Path.Combine(directory,"trace.jsonl")).Select(l=>JsonDocument.Parse(l).RootElement.GetProperty("payload").Clone()).ToArray();return ReviewEvents(rows);
 }
 internal static object ReviewEvents(JsonElement[] rows){int count=0;
  foreach(var start in rows.Where(r=>r.GetProperty("kind").GetString()=="room-option-start"&&r.GetProperty("detail").GetProperty("facts").GetProperty("key").GetString()!.StartsWith("AMALGAMATOR."))){
   string root=start.GetProperty("root").GetString()!;var own=rows.Where(r=>r.GetProperty("root").GetString()==root).ToArray();
   var ret=own.Single(r=>r.GetProperty("kind").GetString()=="room-option-return");
   var before=start.GetProperty("detail").GetProperty("state").GetProperty("deck").EnumerateArray().ToArray();var after=ret.GetProperty("detail").GetProperty("state").GetProperty("deck").EnumerateArray().ToArray();
   string Id(JsonElement x)=>x.GetProperty("id").GetString()!;
   var req=own.Single(r=>r.GetProperty("kind").GetString()=="choice-requested");var response=own.Single(r=>r.GetProperty("kind").GetString()=="choice-returned");
   var removed=response.GetProperty("detail").GetProperty("objects").EnumerateArray().Select(x=>x.GetProperty("$ref").GetString()!).ToArray();
   var added=after.Where(x=>!before.Any(b=>Id(b)==Id(x))).ToArray();
   string key=start.GetProperty("detail").GetProperty("facts").GetProperty("key").GetString()!;
   string expected=key.EndsWith("COMBINE_DEFENDS")?"CARD.ULTIMATE_DEFEND":key.EndsWith("COMBINE_STRIKES")?"CARD.ULTIMATE_STRIKE":throw new NativeDataException("Unknown Amalgamator option");
   void R(bool ok,string why){if(!ok)throw new NativeDataException("Amalgamator: "+why);}
   // FromDeckForRemoval delegates to the already audited FromDeckGeneric
   // selector. The trace identifies the actual intercepted selector, not its wrapper.
   R(req.GetProperty("detail").GetProperty("source").GetString()=="FromDeckGeneric"&&req.GetProperty("detail").GetProperty("min").GetInt32()==2&&req.GetProperty("detail").GetProperty("max").GetInt32()==2&&removed.Length==2&&removed.Distinct().Count()==2,"two original removal selections");
   R(removed.All(id=>before.Any(c=>Id(c)==id)&&!after.Any(c=>Id(c)==id))&&added.Length==1&&added[0].GetProperty("model").GetString()==expected&&added[0].GetProperty("upgrade").GetInt32()==0,"actual removal and new card identity/model/upgrade");
   foreach(var old in before.Where(x=>!removed.Contains(Id(x))))R(NativeBoundaryContract.Equal(old,after.Single(x=>Id(x)==Id(old))),"unselected full card unchanged");
   R(after.Length==before.Length-1&&own.Count(r=>r.GetProperty("kind").GetString()=="amalgamator-visual-guard")==2&&ret.GetProperty("detail").GetProperty("facts").GetProperty("eventFinished").GetBoolean(),"completed native event and presentation calls");
   R(Array.IndexOf(own,start)<Array.IndexOf(own,req)&&Array.IndexOf(own,req)<Array.IndexOf(own,response)&&Array.IndexOf(own,response)<Array.IndexOf(own,ret),"original task order");count++;
  }
  return new{completedAmalgamator=count,scope="actual native two-card removal and generated card; no reward/prediction approximation"};
 }
}
