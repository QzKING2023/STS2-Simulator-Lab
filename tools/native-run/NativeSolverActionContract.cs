using System.Text.Json;
using System.Text.RegularExpressions;
using SimulatorLab.Core;
namespace OfflineSearchHarness;

// A finite deployment protocol, shared by the runtime and evidence reader.
internal static class NativeSolverActionContract
{
    static void Need(bool ok,string why){if(!ok)throw new NativeDataException("Solver action binding: "+why);}
    static string S(JsonElement e,string n)=>e.GetProperty(n).GetString()??"";
    static int I(JsonElement e,string n)=>e.GetProperty(n).GetInt32();
    static bool Empty(JsonElement e,string n)=>e.GetProperty(n).ValueKind==JsonValueKind.Null||e.GetProperty(n).ValueKind==JsonValueKind.Array&&e.GetProperty(n).GetArrayLength()==0;
    internal static void BindReport(JsonElement step,JsonElement decision)
    {
        string kind=S(step.GetProperty("action"),"Kind");
        var card=step.GetProperty("card");var target=step.GetProperty("target");
        Need(card.ValueKind is JsonValueKind.Null or JsonValueKind.String && target.ValueKind is JsonValueKind.Null or JsonValueKind.String,"report nullable identity types");
        if(kind=="PlayCard")Need(card.ValueKind==JsonValueKind.String&&card.GetString()==S(decision,"card"),"report card/actual dispatch instance");
        else Need(card.ValueKind==JsonValueKind.Null,"report non-card action has null card");
        if(kind=="EndTurn")Need(target.ValueKind==JsonValueKind.Null,"report EndTurn has null target");
        else Need(NativeDecisionTicket.SameActionJson(target.GetRawText(),decision.GetProperty("target").GetRawText()),"report target/actual dispatch instance");
    }
    internal static void Fields(JsonElement a,bool choices=false,bool potions=false,bool enchantments=false,bool phaseChoices=false)
    {
        string kind=S(a,"Kind");
        bool card=kind=="PlayCard",potion=kind=="UsePotion"&&potions,end=kind=="EndTurn";
        if(!card&&!potion&&!end)throw new NotSupportedException("Unsupported solver action kind");
        if((!choices&&(a.GetProperty("Choice").ValueKind!=JsonValueKind.Null||!Empty(a,"NestedChoices")))||(!phaseChoices&&!Empty(a,"TurnStartChoices"))||I(a,"ReplayCount")!=0||a.GetProperty("EndsPlayerTurn").GetBoolean()||(!enchantments&&S(a,"CardEnchantmentId")!="")||(!choices&&I(a,"NestedChoicesBeforePrimary")!=0))throw new NotSupportedException("Unsupported solver execution field");
        if(!Empty(a,"TurnStartChoices"))NativeSolverPhaseProtocol.Validate(a);
        if(!card&&S(a,"CardEnchantmentId")!="")throw new NotSupportedException("Non-card action carries enchantment fields");
        if(!potion&&(I(a,"PotionSlot")!=-1||S(a,"PotionId")!=""))throw new NotSupportedException("Unexpected solver potion fields");
        if(!card&&(S(a,"CardId")!=""||S(a,"CardStateKey")!=""||I(a,"CardOccurrence")!=0||I(a,"CardStateOccurrence")!=0||I(a,"CardUpgradeLevel")!=0))throw new NotSupportedException("Unexpected solver card fields");
        if(end&&(I(a,"TargetIndex")!=-1||a.GetProperty("TargetCombatId").ValueKind!=JsonValueKind.Null||a.GetProperty("Choice").ValueKind!=JsonValueKind.Null||!Empty(a,"NestedChoices")))throw new NotSupportedException("Unexpected solver EndTurn fields");
        if(card&&(S(a,"CardId")==""||S(a,"CardStateKey")==""||I(a,"CardOccurrence")<0||I(a,"CardStateOccurrence")<0))throw new NativeActionRejectedException("Missing solver card identity");
        if(potion&&(I(a,"PotionSlot")<0||S(a,"PotionId")==""))throw new NativeActionRejectedException("Missing solver potion identity");
        if(I(a,"TargetIndex") < -1)throw new NativeActionRejectedException("Invalid solver target index");
    }
    // Only the three unmodified starter cards in the immutable v2 slice have this
    // legacy projection. Other old roots fail closed; new captures supply a map.
    static string LegacyKey(JsonElement card,string root)
    {
        string id=S(card,"model").Replace("CARD.","");int up=I(card,"upgrade");
        Need(up==0&&id is "STRIKE_IRONCLAD" or "DEFEND_IRONCLAD" or "BASH","legacy starter-only projection");
        string vars=id=="DEFEND_IRONCLAD"?"Block=5":id=="BASH"?"Damage=8;VulnerablePower=2":"Damage=6";
        string energy=id=="BASH"?"2":"1";
        string stamp=id+"+0/False:"+energy+"/False:-1/0/False/False/False/True/False/-:0:0/-:0["+vars.Replace(';',',')+",]/private=-/keywords=[]/baselib=-";
        Need(root.Contains(stamp+",",StringComparison.Ordinal),"legacy card must match pinned continuation");
        return id+"+0|energy=False:"+energy+"|stars=False:-1|replay=0|exhaust=False|sly=False|retain=False|deck=True|keywords=|vars="+vars+"|-|:0|baselib=-";
    }
    internal static void Bind(JsonElement action,JsonElement query,JsonElement decision,bool choices=false,bool potions=false)
    {
        bool enchantments=NativeSolverEnchantments.Enabled(query);Fields(action,choices,potions,enchantments,NativeSolverPhaseProtocol.Enabled(query));var state=query.GetProperty("nativeState");var combat=state.GetProperty("combat");
        Need(I(action,"Turn")==I(combat,"turn"),"action turn/current boundary");
        bool mapped=query.TryGetProperty("binding",out var map);
        if(mapped){Need(S(map,"continuation")==NativeEvidence.Hash(S(query,"root"))&&S(map,"state")==NativeEvidence.Hash(JsonSerializer.Serialize(state)),"native map boundary");}
        string kind=S(action,"Kind");
        if(kind=="EndTurn"){Need(I(decision,"turn")==I(action,"Turn"),"native EndTurn turn");return;}
        if(kind=="PlayCard"){
            var hand=combat.GetProperty("hand").EnumerateArray().ToArray();
            if(enchantments)Need(map.GetProperty("hand").EnumerateArray().Select(c=>S(c,"id")).SequenceEqual(hand.Select(c=>S(c,"id")))&&hand.Select(c=>S(c,"id")).Distinct().Count()==hand.Length,"complete ordered native hand identity map");
            var models=hand.Where(c=>S(c,"model")=="CARD."+S(action,"CardId")).ToArray();
            Need(I(action,"CardOccurrence")<models.Length,"card occurrence absent");var selected=models[I(action,"CardOccurrence")];
            string Key(JsonElement c)=>mapped?S(map.GetProperty("hand").EnumerateArray().Single(x=>S(x,"id")==S(c,"id")),"key"):LegacyKey(c,S(query,"root"));
            Need(S(selected,"id")==S(decision,"card")&&S(selected,"model")==S(decision,"model")&&I(selected,"upgrade")==I(action,"CardUpgradeLevel")&&Key(selected)==S(action,"CardStateKey"),"native selected card/model/state key");
            if(enchantments)NativeSolverEnchantments.ValidateMap(action,map.GetProperty("hand").EnumerateArray().Single(x=>S(x,"id")==S(selected,"id")));
            var keyed=models.Where(c=>Key(c)==S(action,"CardStateKey")).ToArray();Need(I(action,"CardStateOccurrence")<keyed.Length&&S(keyed[I(action,"CardStateOccurrence")],"id")==S(selected,"id"),"state occurrence native instance");
        }else{
            Need(mapped,"potion requires native map");
            var entries=map.GetProperty("potions").EnumerateArray().ToArray();
            Need(entries.Select(x=>I(x,"slot")).Distinct().Count()==entries.Length&&entries.Select(x=>S(x,"id")).Distinct().Count()==entries.Length,"unique native potion map");
            var p=entries.Single(x=>I(x,"slot")==I(action,"PotionSlot"));
            var held=state.GetProperty("potions").EnumerateArray().Single(x=>S(x,"id")==S(p,"id"));
            Need(I(action,"PotionSlot")==I(decision,"slot")&&S(p,"model")==S(held,"model")&&S(p,"model")=="POTION."+S(action,"PotionId")&&S(p,"id")==S(decision,"potion"),"native potion instance/slot/model");
        }
        var enemies=combat.GetProperty("enemies").EnumerateArray().ToArray();string? target=null;int index=I(action,"TargetIndex");
        if(index>=0){Need(index<enemies.Length,"target index absent");target=S(enemies[index],"id");Need(I(enemies[index],"hp")>0,"target dead");}
        if(action.GetProperty("TargetCombatId").ValueKind!=JsonValueKind.Null){
            uint combatId=action.GetProperty("TargetCombatId").GetUInt32();string? byId=null;
            if(mapped)byId=map.GetProperty("creatures").EnumerateArray().Where(x=>x.GetProperty("combatId").GetUInt32()==combatId).Select(x=>S(x,"id")).SingleOrDefault();
            else foreach(Match m in Regex.Matches(S(query,"root"),@";E(\d+)=(\d+)/"))if(uint.Parse(m.Groups[2].Value)==combatId){int i=int.Parse(m.Groups[1].Value);Need(i<enemies.Length,"legacy target mapping");byId=S(enemies[i],"id");}
            Need(byId!=null&&(target==null||target==byId),"target ID/index disagree or absent");target=byId;
        }
        Need((decision.GetProperty("target").ValueKind==JsonValueKind.Null?null:S(decision,"target"))==target,"actual native target");
    }
}
