using System.Collections;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace OfflineSearchHarness;
internal static class NativeObservation
{
    static int historyIndex,historyEpoch;
    internal static (int Epoch,int Index) Cursor=>(historyEpoch,historyIndex);
    static readonly HashSet<string> skip=["Description","HumanReadableString","History"];
    internal static void Install()
    {
        NativeHost.Register(AccessTools.Method(typeof(CombatHistory),"Clear"),nameof(BeforeClear),typeof(NativeObservation));
        CombatManager.Instance.History.Changed+=HistoryChanged;
        NativeHost.Register(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Events.EventOption),"Chosen"),nameof(EventBefore),typeof(NativeObservation),nameof(EventAfter));
    }
    internal static bool EventFault;
    static int taskNumber;
    static void EventBefore(out string __state){__state="t"+(++taskNumber);NativeEvidence.Emit("task-start",new{id=__state,source="EventOption.Chosen"});}
    static void EventAfter(ref Task __result,string __state)=>__result=TrackEvent(__result,__state);
    static async Task TrackEvent(Task task,string id)
    {
        try{await task;if(EventFault)throw new InvalidOperationException("labelled event task fault");NativeEvidence.Emit("task-completed",new{id,source="EventOption.Chosen"});}
        catch(Exception ex){NativeEvidence.Emit("task-fault",new{id,source="EventOption.Chosen",error=ex.Message});throw;}
    }
    internal static void Checkpoint()
    {
        int entries=CombatManager.Instance.History.Entries.Count();
        if(entries!=historyIndex)throw new NativeDataException("Native history observation count mismatch");
        NativeEvidence.Emit("history-checkpoint",new{epoch=historyEpoch,entries,observed=historyIndex});
    }
    static void BeforeClear()
    {
        int entries=CombatManager.Instance.History.Entries.Count();
        if(entries!=historyIndex)throw new NativeDataException("Native history clear count mismatch");
        NativeEvidence.Emit("history-clearing",new{epoch=historyEpoch,entries,observed=historyIndex});historyIndex=0;historyEpoch++;
    }
    internal static object? Value(object? value,int depth=0)
    {
        if(value==null)return null;
        if(value is CardModel c)return new{reference=NativeIdentity.Ref(c),model=c.Id.ToString(),upgrade=c.CurrentUpgradeLevel,deck=NativeIdentity.Ref(c.DeckVersion)};
        if(value is AbstractModel or Creature)return NativeIdentity.Ref(value);
        if(value is Enum)return value.ToString();
        if(value is string or bool or int or decimal or uint or long or ulong or float or double)return value;
        if(value is IEnumerable list)return list.Cast<object?>().Select(x=>Value(x,depth+1)).ToArray();
        // Only immutable event records / specific native value objects, never arbitrary game getters.
        if(depth<3&&(value is DamageResult||value.GetType().Name is "CardPlay" or "MoveState")){
            var result=new Dictionary<string,object?>();
            foreach(var p in value.GetType().GetProperties(BindingFlags.Instance|BindingFlags.Public).Where(p=>p.GetIndexParameters().Length==0&&!skip.Contains(p.Name)))result[p.Name]=Value(p.GetValue(value),depth+1);
            return result;
        }
        return new{unavailable=value.GetType().Name,reason="outside declared typed projection"};
    }
    static void HistoryChanged()
    {
        try{
            var entries=CombatManager.Instance.History.Entries.ToArray();
            if(entries.Length<historyIndex)throw new NativeDataException("Unobserved history reset");
            foreach(var entry in entries.Skip(historyIndex)){
                var facts=new Dictionary<string,object?>();
                foreach(var p in entry.GetType().GetProperties(BindingFlags.Instance|BindingFlags.Public).Where(p=>p.GetIndexParameters().Length==0&&!skip.Contains(p.Name)))facts[p.Name]=Value(p.GetValue(entry));
                _=Program.StateStamp(); // Mandatory identity projection, also in off/minimal mode.
                // Always register identities even when detailed event output is off.
                historyIndex++;
                if(NativeEvidence.Mode=="detailed")Program.Observe("combat-history",new{epoch=historyEpoch,index=historyIndex,type=entry.GetType().Name,stage="native-history-before-after-hook",round=CombatManager.Instance.DebugOnlyGetState()?.RoundNumber,side=CombatManager.Instance.DebugOnlyGetState()?.CurrentSide.ToString(),facts});
                NativeDeathObservation.Fatal(entry,historyEpoch,historyIndex);
            }
            historyIndex=entries.Length;
        }catch(Exception ex){NativeEvidence.Failure=ex;}
    }
}
