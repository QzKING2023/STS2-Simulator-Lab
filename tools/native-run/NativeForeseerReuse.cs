// Selected RandomForeseer 0.13.19 source reuse. MIT, copyright (c) 2026 hotwords123.
// See THIRD_PARTY_NOTICES.md and the separately hashed original source snapshot.
// No Entry.Initialize, UI, telemetry, hooks or mutable multi-node context is loaded.
using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
namespace OfflineSearchHarness;

internal static partial class NativeForeseerSource
{
 // Exact PredictionExtensions.Clone(RelicGrabBag) body, same source version.
 internal static RelicGrabBag Clone(this RelicGrabBag grabBag)
 {
  return RelicGrabBag.FromSerializable(grabBag.ToSerializable());
 }
 // Exact method body from OutOfCombatPredictionUtils.PredictRelicRewards,
 // overload (Player,IEnumerable<RelicRarity>,Func<RelicModel,bool>?).
 internal static IReadOnlyList<RelicModel> PredictRelicRewards(Player player,IEnumerable<RelicRarity> rarities,Func<RelicModel,bool>? filter=null)
 {
  var grabBag = player.RelicGrabBag.Clone();
  return rarities
      .Select(rarity => grabBag.PullFromFront(rarity, filter ?? (_ => true), player.RunState) ?? RelicFactory.FallbackRelic)
      .ToList();
 }
 // NextActPrediction.ShowIfEligible/ShowIcons reads this already generated act.
 // Return scalar observations instead of creating Godot icons. This is Oracle data.
 internal static (string Ancient,string Boss)? NextAct(IRunState run)
 {
  if(run.CurrentActIndex+1>=run.Acts.Count)return null;
  var nextAct = run.Acts[run.CurrentActIndex + 1];
  if(nextAct._rooms._ancient==null||nextAct._rooms._boss==null)return null;
  return(nextAct._rooms._ancient.Id.ToString(),nextAct._rooms._boss.Id.ToString());
 }
}
internal static partial class Program
{
 static (string Ancient,string Boss)? foreseerNext;
 static void ForeseerNextActPreview(){
  if(!File.Exists(Path.Combine(AppContext.BaseDirectory,"foreseer-lock.json")))throw new NotSupportedException("Darv profile requires separately pinned Foreseer source lock");
  var before=State();foreseerNext=NativeForeseerSource.NextAct(run!);var after=State();
  bool pure=NativeBoundaryContract.Equal(before,after);
  Write("foreseer-next-act.json",new{source="RandomForeseer/0.13.19/NextActPrediction",information="Oracle",scope="already-generated content; not future multi-node simulation",root=NativeEvidence.Root,context=NativeEvidence.Context,before,after,sourceUnchanged=pure,status=foreseerNext==null?"UnsupportedUninitializedNextAct":"Available",ancient=foreseerNext?.Ancient,boss=foreseerNext?.Boss});
  if(!pure)throw new NativeDataException("Foreseer next-act read mutated native state/RNG");
 }
 static void ForeseerNextActVerify(){
  var actual=new{ancient=run!.Act.Ancient.Id.ToString(),boss=run.Act.BossEncounter.Id.ToString()};
  bool? exact=foreseerNext==null?null:foreseerNext.Value.Ancient==actual.ancient&&foreseerNext.Value.Boss==actual.boss;
  Write("foreseer-next-act-actual.json",new{root=NativeEvidence.Root,context=NativeEvidence.Context,actual,state=State(),exact,comparison=exact==null?"NotCompared":"Compared"});
  if(exact==false)throw new NativeDataException("Foreseer next-act/native content mismatch");
 }
}
