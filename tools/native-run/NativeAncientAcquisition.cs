using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
namespace OfflineSearchHarness;
// Capability registry for reviewed ORIGINAL pickup Tasks. Effects stay in the game.
// The transaction collector is shared: card additions and relic replacements.
internal static class NativeAncientAcquisition {
 internal static bool Active;
 internal static readonly List<JsonElement> Added=[];
 internal static readonly List<(string Old,RelicModel New)> Replacements=[];
 static int serial;
 internal static bool Supports(RelicModel relic){
  if(relic is not (PaelsHorn or TouchOfOrobas))return false;
  var method=AccessTools.DeclaredMethod(relic.GetType(),nameof(RelicModel.AfterObtained));var sm=method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
  if(method.Module!=typeof(RelicModel).Module||Harmony.GetPatchInfo(method) is {Owners.Count:>0}||sm==null||Harmony.GetPatchInfo(AccessTools.DeclaredMethod(sm,"MoveNext")) is {Owners.Count:>0})throw new NotSupportedException("Ancient pickup source/patch mismatch");return true;
 }
 internal static void Install(){if(NativeFullRun.Active)NativeHost.Register(AccessTools.DeclaredMethod(typeof(RelicCmd),nameof(RelicCmd.Replace)),nameof(ReplaceBefore),typeof(NativeAncientAcquisition),nameof(ReplaceAfter));}
 internal static void Begin(){if(Active)throw new NativeDataException("Nested Ancient acquisition");Added.Clear();Replacements.Clear();Active=true;}
 internal static string CardStart(CardModel card){string id="ancient-card-"+(++serial);Program.Observe("ancient-card-start",new{id,card=NativeIdentity.Ref(card),model=card.Id.ToString(),upgrade=card.CurrentUpgradeLevel});return id;}
 internal static void CardEnd(string id,CardModel selected,CardPileAddResult result){
  if(!Active||!result.success||result.cardAdded==null)throw new NotSupportedException("Ancient deck addition refused");var c=result.cardAdded;
  var fact=JsonSerializer.SerializeToElement(Program.PermanentCardState(c),Program.Json);Added.Add(fact);
  Program.Observe("ancient-card-return",new{id,selected=NativeIdentity.Ref(selected),actual=NativeIdentity.Ref(c),card=fact,native=c.ToSerializable(),result.success});
 }
 internal sealed record ReplaceScope(RelicModel Old,RelicModel New,string Id);
 static void ReplaceBefore(RelicModel __0,RelicModel __1,out ReplaceScope? __state){__state=Active?new(__0,__1,"ancient-relic-"+(++serial)):null;if(__state!=null)Program.Observe("ancient-replace-start",new{id=__state.Id,old=NativeIdentity.Ref(__0),replacement=NativeIdentity.Ref(__1),model=__1.Id.ToString()});}
 static void ReplaceAfter(ReplaceScope? __state,ref Task<RelicModel> __result){if(__state!=null)__result=Finish(__result,__state);}
 static async Task<RelicModel> Finish(Task<RelicModel> task,ReplaceScope s){var actual=await task;if(!ReferenceEquals(actual,s.New)||actual.Owner.Relics.Contains(s.Old)||!actual.Owner.Relics.Contains(actual))throw new NativeDataException("Original replacement result mismatch");Replacements.Add((NativeIdentity.Get(s.Old),actual));Program.Observe("ancient-replace-return",new{id=s.Id,old=NativeIdentity.Ref(s.Old),replacement=NativeIdentity.Ref(s.New),actual=NativeIdentity.Ref(actual),model=actual.Id.ToString()});return actual;}
 internal static void Complete(JsonElement before,JsonElement after,RelicModel offered){
  var expected=JsonNode.Parse(before.GetRawText())!;var relics=expected["relics"]!.AsArray();relics.Add(new JsonObject{["id"]=NativeIdentity.Get(offered),["model"]=offered.Id.ToString()});
  foreach(var r in Replacements){int index=Enumerable.Range(0,relics.Count).Single(i=>relics[i]!["id"]!.GetValue<string>()==r.Old);relics[index]=new JsonObject{["id"]=NativeIdentity.Get(r.New),["model"]=r.New.Id.ToString()};}
  foreach(var c in Added)expected["deck"]!.AsArray().Add(JsonNode.Parse(c.GetRawText()));
  expected["rooms"]!["eventOptions"]=new JsonArray();expected["lifecycle"]!["eventModel"]!["IsFinished"]=true;
  NativeBoundaryContract.Snapshot(after,JsonSerializer.SerializeToElement(expected),"Original Ancient transaction full frame/RNG");
 }
}
