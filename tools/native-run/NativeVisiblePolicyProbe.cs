using System.Text.Json;
using System.Text.Json.Nodes;
using SimulatorLab.Core;
#if SOLVER_GATE
using CombatSolver;
using MegaCrit.Sts2.Core.Combat;
#endif
namespace OfflineSearchHarness;
internal static partial class Program
{
#if SOLVER_GATE
 static (string Wire,string Decision) VisiblePolicyProbe(CombatState combat,int query,bool deployed=false)
 {
  string live=ContinuationStamp.CaptureLive(combat).StateText;var before=State();int count=dispatched;
  var legal=new List<VisibleAction>{new("end","EndTurn",-1,-1)};var hand=player!.PlayerCombatState!.Hand.Cards;
  for(int h=0;h<hand.Count;h++){
   if(hand[h].CanPlayTargeting(null))legal.Add(new(VisibleCombatPolicy.Token("PlayCard",h,-1),"PlayCard",h,-1));
   for(int e=0;e<combat.Enemies.Count;e++)if(combat.Enemies[e].IsAlive&&hand[h].CanPlayTargeting(combat.Enemies[e]))legal.Add(new(VisibleCombatPolicy.Token("PlayCard",h,e),"PlayCard",h,e));
  }
  string wire=VisibleCombatPolicy.Encode(VisibleCombatPolicy.Project(before,legal));string decision=VisibleCombatPolicy.Decide(wire);
  var tests=new List<object>();
  foreach(string mutation in new[]{"rng","draw-order","private-identities","future-and-seed","public-hp"}){
   var node=JsonNode.Parse(before.GetRawText())!.AsObject();
   if(mutation=="rng"){node["runRng"]=null;node["playerRng"]=new JsonObject{["different"]=true};node["eventRng"]=null;}
   if(mutation=="draw-order"){var a=node["combat"]!["draw"]!.AsArray();node["combat"]!["draw"]=new JsonArray(a.Reverse().Select(x=>x?.DeepClone()).ToArray());}
   if(mutation=="private-identities")foreach(string pile in new[]{"hand","draw","discard","exhaust"})foreach(var c in node["combat"]![pile]!.AsArray()){c!["id"]="different";c["deck"]="hidden";c["privateState"]="secret";}
   if(mutation=="future-and-seed"){node["seed"]="other";node["futureWinner"]="Death";node["root"]="hidden-hash";node["rooms"]=null;node["inventory"]=null;node["naturalMap"]=null;}
   if(mutation=="public-hp")node["hp"]=Math.Max(0,before.GetProperty("hp").GetInt32()-1);
   string changed=VisibleCombatPolicy.Encode(VisibleCombatPolicy.Project(JsonSerializer.SerializeToElement(node),legal));
   bool pass=mutation=="public-hp"?changed!=wire:changed==wire&&VisibleCombatPolicy.Decide(changed)==decision;
   tests.Add(new{name=mutation,passed=pass});if(!pass)throw new NativeDataException("Visible projection noninterference failed");
  }
  int rejected=0;foreach(string extra in new[]{"seed","root","drawOrder","solverResult","logPath"}){
   var node=JsonNode.Parse(wire)!;node[extra]="secret";
   try{_=VisibleCombatPolicy.Decide(node.ToJsonString());throw new NativeDataException("Visible policy accepted unknown input");}catch(JsonException){rejected++;}
  }
  bool pure=live==ContinuationStamp.CaptureLive(combat).StateText&&NativeBoundaryContract.Equal(before,State())&&count==dispatched;
  Write("visible-boundary-"+query+".json",new{schema="visible-boundary-probe/v1",sourceInformation="Oracle diagnostic only",policyInformation="PlayerVisible",root=NativeEvidence.Root,context=NativeEvidence.Context,wire,decision,legal,tests,deployed,unknownFieldsRejected=rejected,sourceUnchanged=pure,zeroDispatch=count==dispatched,scope="trusted pure policy receives only serialized common visible subset; deployment status in deployed field; no adversarial OS sandbox or full-run fairness claim",limitation="hidden-state substitutions test the projection with a fixed native public legality vector; not recomputed legality in alternative native worlds"});
  if(!pure||rejected!=5)throw new NativeDataException("Visible probe changed native state/RNG");
  return (wire,decision);
 }
#endif
}
