using System.Text.Json;
using System.Text.Json.Nodes;
namespace OfflineSearchHarness;

// Finite projected transition frames, not a second implementation of death hooks.
// Original RemoveAllPowersAfterDeath supplies actual removed instances; the
// synchronous phase setter supplies its actual input. All other fields stay bound.
internal sealed class NativeDeathContract
{
 internal static readonly HashSet<string> Kinds=["player-fatal-damage","player-death-powers-start","player-death-powers-return","player-death-phase-start","player-death-phase-return","player-death-maxhp-start","player-death-maxhp-return"];
 bool enabled,maxAllowed,ownerRequired,detailed,fatal,removeStarted,removeReturned,phasePending,maxPending;
 int maxTarget;JsonElement maxInput;
 string root="",context="",subject="",phase="",player="",playerRoom="";
 readonly HashSet<string> creatures=[];
 JsonElement anchor,removalInput,phaseInput;
 static string T(JsonElement e,string p)=>e.GetProperty(p).GetString()!;
 static bool Eq(JsonElement a,JsonElement b)=>NativeBoundaryContract.Equal(a,b);
 static void R(bool v,string m){if(!v)throw new NativeDataException("Player death "+m);}
 internal void Accept(JsonElement p,JsonElement? state,JsonElement lastHistory,string historyRoot,JsonElement historyFacts)
 {
  string k=T(p,"kind");var d=p.GetProperty("detail");
  if(k=="header"){
   enabled=d.TryGetProperty("deathProfile",out var profile)&&profile.ValueKind!=JsonValueKind.Null;
   R(!enabled||profile.GetString() is NativeDeathObservation.Profile or NativeDeathObservation.OwnerProfile or NativeDeathObservation.LegacyProfile,"unknown collection profile");
   maxAllowed=enabled&&profile.GetString()==NativeDeathObservation.Profile;
   ownerRequired=enabled&&profile.GetString()!=NativeDeathObservation.LegacyProfile;detailed=T(d,"mode")=="detailed";
  }
  if(k=="identity"&&T(d,"type")=="Creature")creatures.Add(T(d,"id"));
  if(enabled&&k=="major-battle-start"){
   var facts=d.GetProperty("facts");
   if(ownerRequired||facts.TryGetProperty("player",out _)){
    var reference=facts.GetProperty("player");
    R(reference.ValueKind==JsonValueKind.Object&&reference.EnumerateObject().Count()==1,"actual player Creature reference shape");
    string actual=T(reference,"$ref");var s=state!.Value;
    R(creatures.Contains(actual)&&!s.GetProperty("combat").GetProperty("enemies").EnumerateArray().Any(x=>T(x,"id")==actual),"actual player registered Creature outside enemy roster");
    R(player==""||player==actual,"immutable actual local player across battles");player=actual;
    playerRoom=T(facts.GetProperty("room"),"$ref");
    R(playerRoom==T(s.GetProperty("majorCombat").GetProperty("room"),"$ref"),"player anchor current native battle room");
   }
  }
  if(k=="dispatch"){root=T(p,"root");context=T(p,"context");fatal=removeStarted=removeReturned=phasePending=maxPending=false;subject="";anchor=default;}
  if(Kinds.Contains(k)){
   if(k is "player-death-maxhp-start" or "player-death-maxhp-return")R(maxAllowed,"max HP observation requires v3 collector");
   R(enabled&&T(p,"root")==root&&T(p,"context")==context,"profile/current action owner");
   var s=state!.Value;R(s.GetProperty("hp").GetInt32()==0&&s.GetProperty("majorCombat").GetProperty("inProgress").GetBoolean(),"dead native player before loss");
   var f=d.GetProperty("facts");string id=T(f.GetProperty("subject"),"$ref");
   R(player!=""&&id==player&&creatures.Contains(id)&&playerRoom==T(s.GetProperty("majorCombat").GetProperty("room"),"$ref"),"subject bound to actual player Creature/current battle; legacy unbound death cannot be certified");
   string[] fields=k switch{"player-fatal-damage"=>["subject","epoch","index","result","triggerSource"],"player-death-powers-return"=>["subject","removed"],"player-death-phase-start" or "player-death-maxhp-start"=>["subject","from","to"],"player-death-phase-return" or "player-death-maxhp-return"=>["subject","to"],_=>["subject"]};
   R(f.EnumerateObject().Select(x=>x.Name).Order().SequenceEqual(fields.Order()),"closed native observation facts");
   if(k=="player-fatal-damage"){
    R(!fatal&&!removeStarted,"unique fatal history before cleanup");
    var result=f.GetProperty("result");R(T(result.GetProperty("Receiver"),"$ref")==id&&result.GetProperty("WasTargetKilled").GetBoolean()&&result.GetProperty("UnblockedDamage").GetInt32()>0,"actual fatal receiver/result");
    R(T(f,"triggerSource")=="not-recorded-by-native-damage-history","honest trigger provenance");
    R(f.GetProperty("epoch").GetInt32()>=0&&f.GetProperty("index").GetInt32()>0,"fatal native history cursor");
    if(detailed){
     R(historyRoot==root&&Eq(s,lastHistory)&&T(historyFacts,"type")=="DamageReceivedEntry"&&Eq(f.GetProperty("epoch"),historyFacts.GetProperty("epoch"))&&Eq(f.GetProperty("index"),historyFacts.GetProperty("index"))&&Eq(result,historyFacts.GetProperty("facts").GetProperty("Result")),"fatal complete history snapshot/cursor/result");
     var native=historyFacts.GetProperty("facts");R(T(native.GetProperty("Receiver"),"$ref")==player&&T(native.GetProperty("Actor"),"$ref")==player,"fatal native history receiver/actor actual player");
    }
    subject=id;fatal=true;anchor=s.Clone();
   }else{
    R(fatal&&id==subject,"same fatal player");
    if(k=="player-death-maxhp-start"){R(!maxPending&&!phasePending&&(!removeStarted||removeReturned)&&Eq(s,anchor)&&f.GetProperty("from").GetInt32()==s.GetProperty("maxHp").GetInt32(),"max HP setter complete prior frame");maxTarget=f.GetProperty("to").GetInt32();R(maxTarget>=0&&maxTarget!=s.GetProperty("maxHp").GetInt32(),"max HP actual changed target");maxInput=s.Clone();maxPending=true;}
    if(k=="player-death-maxhp-return"){R(maxPending&&f.GetProperty("to").GetInt32()==maxTarget,"max HP setter matching return");var expected=JsonNode.Parse(maxInput.GetRawText())!;expected["maxHp"]=maxTarget;R(Eq(s,JsonSerializer.SerializeToElement(expected)),"max HP setter complete result frame");anchor=s.Clone();maxPending=false;}
    if(k=="player-death-powers-start"){R(!removeStarted&&!removeReturned&&!phasePending&&!maxPending&&Eq(s,anchor),"powers entry immutable prior state");removeStarted=true;removalInput=s.Clone();}
    if(k=="player-death-powers-return"){
     R(removeStarted&&!removeReturned&&!phasePending&&!maxPending,"powers completion order/unique");
     var removed=f.GetProperty("removed").EnumerateArray().Select(x=>T(x,"$ref")).ToArray();var before=removalInput.GetProperty("powers").EnumerateArray().ToArray();
     R(removed.Distinct().Count()==removed.Length&&removed.All(id=>before.Any(x=>T(x,"id")==id)),"actual removed instances subset");
     var expected=JsonNode.Parse(removalInput.GetRawText())!;expected["powers"]=JsonSerializer.SerializeToNode(before.Where(x=>!removed.Contains(T(x,"id"))).ToArray());
     R(Eq(s,JsonSerializer.SerializeToElement(expected)),"powers complete native removal frame");removeReturned=true;anchor=s.Clone();
    }
    if(k=="player-death-phase-start"){
     string from=T(f,"from");phase=T(f,"to");R(removeReturned&&!phasePending&&!maxPending&&Eq(s,anchor)&&from==T(s.GetProperty("combat"),"phase")&&(from=="Start"&&phase=="AutoPostPlay"||from=="AutoPostPlay"&&phase=="End"),"dead turn phase entry/order/full state");phaseInput=s.Clone();phasePending=true;
    }
    if(k=="player-death-phase-return"){
     R(phasePending&&T(f,"to")==phase,"phase completion owner/order");var expected=JsonNode.Parse(phaseInput.GetRawText())!;expected["combat"]!["phase"]=phase;
     R(Eq(s,JsonSerializer.SerializeToElement(expected)),"phase complete native setter frame");anchor=s.Clone();phasePending=false;
    }
   }
  }
  if(enabled&&fatal&&k=="combat-history"){R(T(p,"root")==root&&T(p,"context")==context&&!phasePending&&!maxPending,"post-death history current action");anchor=state!.Value.Clone();}
  if(enabled&&k=="battle-loss-start")R(fatal&&removeReturned&&!phasePending&&!maxPending&&T(p,"root")==root&&T(p,"context")==context&&Eq(state!.Value,anchor),"loss input complete cleanup anchor");
  if(enabled&&k=="confirmed"){
   R(!phasePending&&!maxPending&&(!removeStarted||removeReturned),"confirmation before synchronous cleanup returned");
   if(fatal)R(removeReturned&&state!.Value.GetProperty("hp").GetInt32()==0&&!state.Value.GetProperty("majorCombat").GetProperty("inProgress").GetBoolean()&&state.Value.GetProperty("lifecycle").GetProperty("native").GetProperty("Losses").GetInt32()==1,"fatal confirmed only after actual terminal loss; prevention requires a separate observed contract");
  }
 }
 internal bool Enabled=>enabled;
}
