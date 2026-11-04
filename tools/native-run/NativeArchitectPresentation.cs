using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Events;

namespace OfflineSearchHarness;
// The dialogue continuation lives after a visual-speaker guard. In a host without Nodes,
// bypass ONLY that visual early-return; preserve the original dialogue and victory body.
internal static class NativeArchitectPresentation
{
    internal static int MissingSpeakerContinuations,SpeechBubblesSkipped;
    internal static readonly List<Task> DialogueTasks=[];
    static int tasks;
    internal static void Install()
    {
        var original=AccessTools.Method(typeof(TheArchitect),"PlayCurrentLine");
        NativeHost.Register(original,nameof(DialogueBefore),typeof(NativeArchitectPresentation),nameof(DialogueAfter));
        var move=AccessTools.Method(original.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType,"MoveNext");
        if(Harmony.GetPatchInfo(move)!=null)throw new InvalidOperationException("Unknown Architect presentation patch");
        GameBootstrap.Harmony.Patch(move,transpiler:new HarmonyMethod(typeof(NativeArchitectPresentation),nameof(Transpile)));
        NativeHost.Seams.Add(new{method=move.ToString(),token=move.MetadataToken,mvid=move.Module.ModuleVersionId,ilSha256=Convert.ToHexString(SHA256.HashData(move.GetMethodBody()!.GetILAsByteArray()!)),handler="Architect visual speaker guard only"});
        NativeHost.Register(AccessTools.Method(typeof(TheArchitect),"ShowSpeechBubble"),nameof(SkipBubble),typeof(NativeArchitectPresentation));
    }
    static void DialogueBefore(out string __state)
    {
        __state=NativeLifecycle.Active?"dialogue"+(++tasks):"";
        if(__state!="")NativeEvidence.Emit("task-start",new{id=__state,source="TheArchitect.PlayCurrentLine"});
    }
    static void DialogueAfter(ref Task __result,string __state)
    {
        if(__state!=""){__result=TrackDialogue(__result,__state);DialogueTasks.Add(__result);}
    }
    static async Task TrackDialogue(Task task,string id)
    {
        try{await task;NativeEvidence.Emit("task-completed",new{id,source="TheArchitect.PlayCurrentLine"});}
        catch(Exception ex){NativeEvidence.Emit("task-fault",new{id,source="TheArchitect.PlayCurrentLine",error=ex.Message});throw;}
    }
    static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
    {
        var code=instructions.ToList();int changed=0;
        for(int i=0;i+4<code.Count;i++)if(code[i].Calls(AccessTools.Method(typeof(TheArchitect),"GetSpeaker"))&&code[i+1].opcode==OpCodes.Stfld&&code[i+3].opcode==OpCodes.Ldfld&&Equals(code[i+1].operand,code[i+3].operand)&&(code[i+4].opcode==OpCodes.Brtrue_S||code[i+4].opcode==OpCodes.Brtrue)){
            code.Insert(i+4,new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(NativeArchitectPresentation),nameof(HasSpeaker))));changed++;
        }
        if(changed!=1)throw new InvalidOperationException("Architect pinned visual guard anchor mismatch: "+changed);
        return code;
    }
    static bool HasSpeaker(Creature? speaker)
    {
        if(speaker!=null)return true;
        if(!NativeLifecycle.Active||MegaCrit.Sts2.Core.Nodes.NGame.Instance!=null)return false;
        MissingSpeakerContinuations++;NativeEvidence.Emit("architect-presentation",new{operation="continue original dialogue after absent visual speaker",rngChanged=false});return true;
    }
    static bool SkipBubble()
    {
        if(!NativeLifecycle.Active||MegaCrit.Sts2.Core.Nodes.NGame.Instance!=null)return true;
        SpeechBubblesSkipped++;return false;
    }
}
