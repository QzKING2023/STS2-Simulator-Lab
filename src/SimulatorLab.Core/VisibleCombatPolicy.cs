using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimulatorLab.Core;

// No game/solver references, callbacks, IO, shared RNG or object identities.
// A trusted-policy data boundary, not an OS sandbox for arbitrary plugin code.
public sealed record VisibleCard(int Slot,string Model,int Upgrade);
public sealed record VisibleEnemy(int Slot,string Model,int Hp,int Block);
public sealed record VisibleAction(string Token,string Kind,int HandSlot,int EnemySlot);
public sealed record VisibleCombatObservation(string Schema,string Information,string Completeness,int Hp,int MaxHp,int Block,int Energy,int Turn,
    VisibleCard[] Hand,VisibleEnemy[] Enemies,int DrawCount,int DiscardCount,int ExhaustCount,VisibleAction[] LegalActions);

public static class VisibleCombatPolicy
{
    public static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public static string Token(string kind,int hand,int enemy)=>kind=="EndTurn"?"end":$"play:h{hand}:e{enemy}";
    public static VisibleCombatObservation Project(JsonElement state,IEnumerable<VisibleAction> actions)
    {
        var c=state.GetProperty("combat");
        int I(JsonElement e,string k)=>e.GetProperty(k).GetInt32();
        return new("visible-combat/v1","PlayerVisible","CommonCombatSubset",I(state,"hp"),I(state,"maxHp"),I(state,"block"),I(c,"energy"),I(c,"turn"),
            c.GetProperty("hand").EnumerateArray().Select((x,i)=>new VisibleCard(i,x.GetProperty("model").GetString()!,I(x,"upgrade"))).ToArray(),
            c.GetProperty("enemies").EnumerateArray().Select((x,i)=>new VisibleEnemy(i,x.GetProperty("model").GetString()!,I(x,"hp"),I(x,"block"))).ToArray(),
            c.GetProperty("draw").GetArrayLength(),c.GetProperty("discard").GetArrayLength(),c.GetProperty("exhaust").GetArrayLength(),
            actions.OrderBy(a=>a.Kind,StringComparer.Ordinal).ThenBy(a=>a.HandSlot).ThenBy(a=>a.EnemySlot).ToArray());
    }
    public static string Encode(VisibleCombatObservation o)=>JsonSerializer.Serialize(o,Json);
    public static string Decide(string wire)
    {
        var o=JsonSerializer.Deserialize<VisibleCombatObservation>(wire,Json)??throw new InvalidDataException("Missing visible observation");
        if(o.Schema!="visible-combat/v1"||o.Information!="PlayerVisible"||o.Completeness!="CommonCombatSubset"||o.Hand==null||o.Enemies==null||o.LegalActions==null||o.Hp<0||o.MaxHp<o.Hp||o.Energy<0||o.Turn<1)throw new InvalidDataException("Visible observation contract");
        if(o.Hand.Where((x,i)=>x==null||x.Slot!=i||string.IsNullOrWhiteSpace(x.Model)||x.Upgrade<0).Any()||o.Enemies.Where((x,i)=>x==null||x.Slot!=i||string.IsNullOrWhiteSpace(x.Model)).Any())throw new InvalidDataException("Visible slot contract");
        if(o.LegalActions.Length==0||o.LegalActions.Select(a=>a.Token).Distinct(StringComparer.Ordinal).Count()!=o.LegalActions.Length||o.LegalActions.Count(a=>a.Kind=="EndTurn")!=1)throw new InvalidDataException("Visible action set");
        foreach(var a in o.LegalActions){
            if(a.Kind is not ("EndTurn" or "PlayCard")||a.Token!=Token(a.Kind,a.HandSlot,a.EnemySlot)||a.Kind=="EndTurn"&&(a.HandSlot!=-1||a.EnemySlot!=-1)||a.Kind=="PlayCard"&&(a.HandSlot<0||a.HandSlot>=o.Hand.Length||a.EnemySlot< -1||a.EnemySlot>=o.Enemies.Length||a.EnemySlot>=0&&o.Enemies[a.EnemySlot].Hp<=0))throw new InvalidDataException("Visible action binding");
        }
        // Deliberately simple deterministic baseline. No human-quality claim.
        return o.LegalActions.Where(a=>a.Kind=="PlayCard").OrderBy(a=>a.HandSlot).ThenBy(a=>a.EnemySlot).FirstOrDefault()?.Token??"end";
    }
}
