using System.Text.Json;
using System.Text.Json.Serialization;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using SimulatorLab.Core;
namespace OfflineSearchHarness;

internal sealed record BranchMove(JsonElement Action,string? Exhaust=null);
internal sealed record BranchPlan(string Schema,int PrefixCount,string PrefixSha,string ChoiceSha,string RewardSha,BranchMove[] Moves,string Mode,int HorizonTurns=2);
internal sealed class NativeBranchHorizon:Exception;
internal static class NativeBranchSearch
{
 internal static BranchPlan? Plan;
 internal static bool Active=>Plan!=null;
 internal static bool Switched;
 internal static int StartTurn,EndTurns,Forced;
 internal static string ConfigHash="";
 internal static string? PendingExhaust;
 internal static int Choices;
 internal static object? Profile=>Plan==null?null:new{schema="native-branch/v1",configHash=ConfigHash,plan=Plan,information="oracle-native-rollout",execution="exact-prefix/live-tail"};
 internal static string Sha(string path)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
 internal static void Configure(string path,string? replay,string scenario){
  if(path=="")return;
  if(replay==null||scenario!="major-natural-act3-route")throw new NativeActionRejectedException("Branch requires declared natural third route and exact replay prefix");
  Plan=JsonSerializer.Deserialize<BranchPlan>(File.ReadAllText(path),new JsonSerializerOptions{UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow})??throw new NativeActionRejectedException("Missing branch plan");
  ConfigHash=Sha(path);
  if(Plan.Schema!="native-branch/v1"||Plan.PrefixCount<595||Plan.Moves.Length>3||Plan.Mode is not ("probe" or "rollout" or "tail")||Plan.HorizonTurns!=2)throw new NativeActionRejectedException("Branch plan budget/schema invalid");
  string parent=Path.GetDirectoryName(replay)!;
  if(Sha(replay)!=Plan.PrefixSha||Sha(Path.Combine(parent,"choices.json"))!=Plan.ChoiceSha||Sha(Path.Combine(parent,"reward-choices.json"))!=Plan.RewardSha)throw new NativeActionRejectedException("Branch input identity mismatch");
  foreach(var move in Plan.Moves){
   var a=move.Action;string kind=a.GetProperty("kind").GetString()!;
   if(!a.EnumerateObject().Select(x=>x.Name).Order().SequenceEqual(new[]{"decision","kind"})||kind is not ("PlayCard" or "EndTurn"))throw new NativeActionRejectedException("Branch unsupported action fields/kind");
   var expected=kind=="PlayCard"?new[]{"card","model","target"}:new[]{"turn"};
   if(!a.GetProperty("decision").EnumerateObject().Select(x=>x.Name).Order().SequenceEqual(expected.Order())||kind=="EndTurn"&&move.Exhaust!=null)throw new NativeActionRejectedException("Branch unsupported decision fields");
  }
 }
 internal static void AssertFinished(){if(Active&&(!Switched||Forced!=Plan!.Moves.Length||PendingExhaust!=null))throw new NativeDataException("Branch prefix/forced moves incomplete");}
 internal static int[]? Select(CardModel[] cards,string source){
  if(!Switched||source!="FromHand")return null;
  int index;
  if(PendingExhaust!=null){index=Array.FindIndex(cards,c=>NativeIdentity.Get(c)==PendingExhaust);if(index<0)throw new NativeActionRejectedException("Forced exhaust instance absent from actual request");PendingExhaust=null;}
  else{
   int Rank(CardModel c)=>c.Id.ToString() is "CARD.INFERNO" or "CARD.DARK_EMBRACE" or "CARD.STAMPEDE"?-10:c.Type==CardType.Status?0:c.Type==CardType.Curse?1:c.Id.ToString()=="CARD.ANGER"?20:c.DynamicVars.ContainsKey("Block")?200:100;
   index=Enumerable.Range(0,cards.Length).OrderBy(i=>Rank(cards[i])).ThenBy(i=>i).First();
  }
  if(++Choices>128)throw new TimeoutException("Branch child-choice budget");
  NativeEvidence.Emit("branch-choice",new{source,selected=NativeIdentity.Ref(cards[index]),index});return [index];
 }
}
internal static partial class Program
{
 static BranchMove? branchNext;
 static void BranchBeforeDecision((CardModel card,Creature? target)[] raw){
  if(!NativeBranchSearch.Active)return;
  var plan=NativeBranchSearch.Plan!;
  if(!NativeBranchSearch.Switched){
   if(sequence<plan.PrefixCount)return;
   if(sequence!=plan.PrefixCount||replayIndex!=replay.Length)throw new NativeDataException("Branch switch prefix boundary mismatch");
   NativeChoices.AssertConsumed();NativeRewards.AssertConsumed();
   var s=State();var co=s.GetProperty("combat");
   if(s.GetProperty("act").GetInt32()!=2||s.GetProperty("majorCombat").GetProperty("encounter").GetString()!="ENCOUNTER.TEST_SUBJECT_BOSS"||co.GetProperty("phase").GetString()!="Play"||co.GetProperty("side").GetString()!="Player")throw new NativeDataException("Branch actual safe TestSubject boundary required");
   NativeBranchSearch.StartTurn=co.GetProperty("turn").GetInt32();replayPath=null;NativeChoices.Replaying=false;NativeBranchSearch.Switched=true;
   Observe("branch-switch",new{prefix=plan.PrefixCount,config=NativeBranchSearch.ConfigHash});
  }
  if(sequence-plan.PrefixCount>=96||player!.PlayerCombatState!.TurnNumber-NativeBranchSearch.StartTurn>=16)throw new TimeoutException("Branch actual tail action/turn budget");
  branchNext=NativeBranchSearch.Forced<plan.Moves.Length?plan.Moves[NativeBranchSearch.Forced]:null;
  if(branchNext!=null){
   var a=branchNext.Action;string kind=a.GetProperty("kind").GetString()!;
   bool legal=kind=="EndTurn"?a.GetProperty("decision").GetProperty("turn").GetInt32()==player!.PlayerCombatState!.TurnNumber:raw.Any(x=>NativeDecisionTicket.SameActionJson(a.GetRawText(),JsonSerializer.Serialize(new{kind="PlayCard",decision=new{card=Id(x.card),model=x.card.Id.ToString(),target=x.target==null?null:Id(x.target)}},Json)));
   if(!legal)throw new NativeActionRejectedException("Forced branch action not native-legal now");return;
  }
  if(!branchFrontier){
   NativeBranchSearch.EndTurns=0;var options=BranchOptions(raw);var state=State();
   Write("branch-node.json",new{confirmed=sequence,state,options,config=NativeBranchSearch.ConfigHash});
   Observe("branch-frontier",new{confirmed=sequence,options});branchFrontier=true;
  }
  if(plan.Mode=="probe"||plan.Mode=="rollout"&&NativeBranchSearch.EndTurns>=plan.HorizonTurns){
   NativeBranchSearch.AssertFinished();Observe("branch-horizon",new{mode=plan.Mode,forced=NativeBranchSearch.Forced,enemyTurns=NativeBranchSearch.EndTurns});throw new NativeBranchHorizon();
  }
 }
 static bool branchFrontier;
 static BranchMove[] BranchOptions((CardModel card,Creature? target)[] raw){
  string before=StateStamp();var timer=System.Diagnostics.Stopwatch.StartNew();var result=new List<BranchMove>();
  void Add(BranchMove m){if(result.Count<4&&!result.Any(x=>NativeDecisionTicket.SameActionJson(JsonSerializer.Serialize(x,Json),JsonSerializer.Serialize(m,Json))))result.Add(m);}
  BranchMove Move(CardModel card,Creature? target,string? exhaust=null)=>new(JsonSerializer.SerializeToElement(new{kind="PlayCard",decision=new{card=Id(card),model=card.Id.ToString(),target=target==null?null:Id(target)}},Json),exhaust);
  Add(new(JsonSerializer.SerializeToElement(new{kind="EndTurn",decision=new{turn=player!.PlayerCombatState!.TurnNumber}},Json)));
  if(raw.Length>128)throw new TimeoutException("Branch proposal candidate budget");
  var ordered=PreparedCandidates(raw);if(ordered.Length>0)Add(Move(ordered[0].card,ordered[0].target));
  var grit=raw.FirstOrDefault(x=>x.card.Id.ToString()=="CARD.TRUE_GRIT"&&x.card.CurrentUpgradeLevel>0);
  if(grit.card!=null){
   var offered=player!.PlayerCombatState!.Hand.Cards.Where(x=>!ReferenceEquals(x,grit.card)).ToArray();
   foreach(var c in offered.OrderBy(x=>x.Id.ToString() is "CARD.STAMPEDE" or "CARD.INFERNO" or "CARD.DARK_EMBRACE"?0:x.Type==CardType.Status?1:2).Take(2))Add(Move(grit.card,grit.target,Id(c)));
  }
  var block=raw.Where(x=>x.card.DynamicVars.ContainsKey("Block")).OrderByDescending(x=>x.card.DynamicVars.Block.BaseValue).FirstOrDefault();if(block.card!=null)Add(Move(block.card,block.target));
  foreach(var x in ordered)Add(Move(x.card,x.target));
  if(timer.ElapsedMilliseconds>250)throw new TimeoutException("Branch proposal wall budget");
  if(StateStamp()!=before)throw new NativeDataException("Branch proposal mutated native state/RNG");
  return result.ToArray();
 }
 static void BranchAfterConfirm(string kind){
  if(!NativeBranchSearch.Switched)return;
  if(branchNext!=null){if(NativeBranchSearch.PendingExhaust!=null)throw new NativeDataException("Forced exhaust was not consumed");NativeBranchSearch.Forced++;branchNext=null;}
  if(kind=="EndTurn")NativeBranchSearch.EndTurns++;
 }
}
