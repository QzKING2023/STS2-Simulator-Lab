using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.TestSupport;
namespace OfflineSearchHarness;

// Scoped observation of original native acquisition Tasks. No business result is substituted.
internal static class NativeRewardClosure
{
 internal static readonly HashSet<string> Scenarios=["closure-reroll-take","closure-reroll-skip","closure-direct","closure-lens-options","closure-lens-add","closure-sozu-empty","closure-sozu-full","closure-neow","closure-treasure","closure-spoils"];
 internal static readonly HashSet<string> Faults=["closure-reroll-invalid","closure-reroll-stale","closure-reroll-missing","closure-reroll-extra","closure-reroll-duplicate","closure-reroll-disabled","closure-add-fault"];
 internal static bool IsScenario(string s)=>Scenarios.Contains(s)||Faults.Contains(s);
 internal static bool Active=>IsScenario(NativeLifecycle.Scenario);
 internal static bool ProcureActive=>Active||NativeFullRun.Active;
 internal static bool Pending=>menus.Count>0;
 sealed record Menu(NativeRewards.Operation Op,CardModel? Selected,object[] Cards,object[] Alternatives,int Generation);
 static readonly Dictionary<CardReward,Menu> menus=[];
 static readonly Dictionary<CardReward,int> generations=[];
 static readonly Dictionary<CardReward,CardModel?> acquired=[];
 static readonly Dictionary<PotionReward,PotionModel?> potions=[];
 internal static readonly AsyncLocal<PotionReward?> SelectingPotion=new();
 static readonly AsyncLocal<CardReward?> reward=new();
 static readonly AsyncLocal<uint?> nativeChoice=new();
 internal static void Reserved(uint id){if(Active&&reward.Value!=null)nativeChoice.Value=id;}
 internal static CardReward? Enter(CardReward r){var old=reward.Value;reward.Value=r;return old;}
 internal static void Leave(CardReward? old)=>reward.Value=old;
 internal static object Fact(CardModel c)=>new{reference=NativeIdentity.Ref(c),model=c.Id.ToString(),upgrade=c.CurrentUpgradeLevel,native=c.ToSerializable()};
 internal static PotionModel? ActualPotion(PotionReward r)=>potions.GetValueOrDefault(r);
 internal static void Install()
 {
  if(!Active){
   if(NativeFullRun.Active){var addOriginal=typeof(CardPileCmd).GetMethods().Single(m=>m.Name=="Add"&&m.GetParameters()[0].ParameterType==typeof(CardModel)&&m.GetParameters()[1].ParameterType==typeof(PileType));NativeHost.Register(addOriginal,nameof(AddBefore),typeof(NativeRewardClosure),nameof(AddAfter));}
   if(ProcureActive){var method=typeof(PotionCmd).GetMethods().Single(m=>m.Name=="TryToProcure"&&!m.IsGenericMethod);NativeHost.Register(method,nameof(ProcureBefore),typeof(NativeRewardClosure),nameof(ProcureAfter));}
   return;
  }
  NativeHost.Register(AccessTools.DeclaredMethod(typeof(CardReward),nameof(CardReward.Reroll)),nameof(RerollBefore),typeof(NativeRewardClosure),nameof(RerollAfter));
  var add=typeof(CardPileCmd).GetMethods().Single(m=>m.Name=="Add"&&m.GetParameters()[0].ParameterType==typeof(CardModel)&&m.GetParameters()[1].ParameterType==typeof(PileType));
  NativeHost.Register(add,nameof(AddBefore),typeof(NativeRewardClosure),nameof(AddAfter));
  var procure=typeof(PotionCmd).GetMethods().Single(m=>m.Name=="TryToProcure"&&!m.IsGenericMethod);
  NativeHost.Register(procure,nameof(ProcureBefore),typeof(NativeRewardClosure),nameof(ProcureAfter));
 }
 internal static CardRewardSelection Choose(CardReward r,IReadOnlyList<CardCreationResult> cards,IReadOnlyList<CardRewardAlternative> alternatives)
 {
  if(menus.ContainsKey(r))throw new NotSupportedException("Native menu requested before prior business boundary");
  // Native test selector retains its original alternative list after reroll; UI availability is authoritative.
  alternatives=alternatives.Where(a=>a.OptionId!="REROLL"||r.CanReroll).ToArray();
  var opts=cards.Select((c,i)=>new NativeRewards.Option("o"+i,"Card",NativeIdentity.Ref(c.Card),c.Card.Id.ToString(),Upgrade:c.Card.CurrentUpgradeLevel)).Concat(alternatives.Select((a,i)=>new NativeRewards.Option("o"+(cards.Count+i),"Alternative",NativeIdentity.Ref(a),a.OptionId))).ToArray();
  int generation=generations.GetValueOrDefault(r)+1;generations[r]=generation;
  int preferred=0;
  if(NativeLifecycle.Scenario.StartsWith("closure-reroll")&&generation==1)preferred=cards.Count+alternatives.ToList().FindIndex(a=>a.OptionId=="REROLL");
  else if(NativeLifecycle.Scenario=="closure-reroll-skip")preferred=cards.Count+alternatives.ToList().FindIndex(a=>a.OptionId=="Skip");
  else if(cards.Count>1)preferred=1;
  if(generation==2&&NativeLifecycle.Scenario=="closure-reroll-disabled")preferred=opts.Length;
  if(generation==2&&Faults.Contains(NativeLifecycle.Scenario)&&NativeLifecycle.Scenario.StartsWith("closure-reroll"))NativeRewards.Fault=NativeLifecycle.Scenario[15..];
  var op=NativeRewards.Begin("card-menu",r,opts,preferred);int index=Array.FindIndex(opts,x=>x.Handle==op.Handle);
  CardModel? selected=index<cards.Count?cards[index].Card:null;
  if(selected==null&&alternatives[index-cards.Count].OptionId is not ("Skip" or "REROLL"))throw new NotSupportedException("Native alternate effect outside closure profile");
  menus.Add(r,new(op,selected,cards.Select(c=>Fact(c.Card)).ToArray(),alternatives.Select(a=>(object)new{reference=NativeIdentity.Ref(a),option=a.OptionId,afterSelected=a.AfterSelected.ToString()}).ToArray(),generation));
  Program.Observe("rc-menu",new{id=op.Id,reward=NativeIdentity.Ref(r),generation,nativeChoiceId=nativeChoice.Value??throw new NativeDataException("Missing native reserved reward choice"),r.CanReroll,alternatives=menus[r].Alternatives,cards=cards.Select(c=>new{card=Fact(c.Card),modifiers=c.ModifyingRelics?.Select(NativeIdentity.Ref).ToArray()}).ToArray()});
  return new(){card=selected,alternative=index>=cards.Count?alternatives[index-cards.Count]:null};
 }
 static void FinishMenu(CardReward r,bool complete,bool reroll)
 {
  if(!menus.Remove(r,out var m))throw new NativeDataException("Missing scoped card menu");
  var actual=acquired.GetValueOrDefault(r);
  NativeRewards.End(m.Op,new{success=complete,acquired=NativeIdentity.Ref(actual),sourceCards=m.Cards,sourceAlternatives=m.Alternatives,acquiredCard=actual==null?null:Fact(actual),identityMode=reroll?"reroll":actual==null?"none":ReferenceEquals(actual,m.Selected)?"same-instance":"replacement",generation=m.Generation});
 }
 internal static bool Complete(CardReward r,bool result)
 {
  FinishMenu(r,result,false);var actual=acquired.GetValueOrDefault(r);
  if(actual!=null)NativeRewards.AcquiredCards.Add(actual);
  if(!NativeRewards.completedCards.TryAdd(r,actual))throw new NativeDataException("Duplicate native closure completion");acquired.Remove(r);return result;
 }
 static void RerollBefore(CardReward __instance){if(menus.TryGetValue(__instance,out var m))Program.Observe("rc-reroll-start",new{id=m.Op.Id,reward=NativeIdentity.Ref(__instance),generation=m.Generation,cards=__instance.Cards.Select(Fact).ToArray(),__instance.CanReroll});}
 static void RerollAfter(CardReward __instance){if(menus.TryGetValue(__instance,out var m)){Program.Observe("rc-reroll-return",new{id=m.Op.Id,reward=NativeIdentity.Ref(__instance),generation=m.Generation,cards=__instance.Cards.Select(Fact).ToArray(),__instance.CanReroll});FinishMenu(__instance,false,true);}}
 internal static CardModel? DirectSelected;
 internal static NativeRewards.Operation? DirectOperation;
 internal static CardModel? DirectActual;
 sealed record AddScope(CardReward? Reward,CardModel Selected,int Generation,string Id,System.Text.Json.JsonElement SelectedFact);
 static void AddBefore(CardModel card,PileType newPileType,out AddScope? __state)
 {
  __state=null;var r=reward.Value;
  if(newPileType!=PileType.Deck)return;
  if(NativeAncientAcquisition.Active){__state=new(null,card,1,NativeAncientAcquisition.CardStart(card),System.Text.Json.JsonSerializer.SerializeToElement(Fact(card),Program.Json));return;}
   if(!Active)return; // Full-run observer is scoped only to the Ancient transaction.
  if(DirectOperation is {} direct){if(!ReferenceEquals(card,DirectSelected))throw new NativeDataException("Wrong direct API object");__state=new(null,card,1,direct.Id,System.Text.Json.JsonSerializer.SerializeToElement(Fact(card),Program.Json));Program.Observe("rc-add-start",new{id=direct.Id,reward=(object?)null,generation=1,selected=__state.SelectedFact});return;}
  if(r==null)return;
  if(!menus.TryGetValue(r,out var m)||!ReferenceEquals(m.Selected,card))throw new NativeDataException("Card add outside selected native menu");
  __state=new(r,card,m.Generation,m.Op.Id,System.Text.Json.JsonSerializer.SerializeToElement(Fact(card),Program.Json));Program.Observe("rc-add-start",new{id=m.Op.Id,reward=NativeIdentity.Ref(r),generation=m.Generation,selected=Fact(card)});
 }
 static void AddAfter(ref Task<CardPileAddResult> __result,AddScope? __state){if(__state!=null)__result=AddTask(__result,__state);}
 static async Task<CardPileAddResult> AddTask(Task<CardPileAddResult> task,AddScope s)
 {
  var result=await task;
  if(s.Id.StartsWith("ancient-card-",StringComparison.Ordinal)){NativeAncientAcquisition.CardEnd(s.Id,s.Selected,result);return result;}
  if(s.Reward!=null&&acquired.ContainsKey(s.Reward))throw new NotSupportedException("Additional card acquisition requires multi-take profile");
  var actual=result.success?result.cardAdded:null;if(s.Reward!=null)acquired.Add(s.Reward,actual);else DirectActual=actual;
  Program.Observe("rc-add-return",new{id=s.Id,reward=NativeIdentity.Ref(s.Reward),generation=s.Generation,selected=s.SelectedFact,result.success,actual=actual==null?null:Fact(actual),modifiers=result.modifyingModels?.Select(NativeIdentity.Ref).ToArray()??[]});if(NativeLifecycle.Scenario=="closure-add-fault")throw new InvalidOperationException("labelled fault after real native Add and observation");return result;
 }
 sealed record PotionScope(PotionReward Reward,PotionModel Potion,string Id);
 static void ProcureBefore(PotionModel potion,out PotionScope? __state)
 {
  __state=SelectingPotion.Value is {} r?new(r,potion,NativeRewards.CurrentOperation.Id):null;
  if(__state!=null)Program.Observe("rc-procure-start",new{id=__state.Id,reward=NativeIdentity.Ref(__state.Reward),potion=NativeIdentity.Ref(potion),model=potion.Id.ToString()});
 }
 static void ProcureAfter(ref Task<PotionProcureResult> __result,PotionScope? __state){if(__state!=null)__result=ProcureTask(__result,__state);}
 static async Task<PotionProcureResult> ProcureTask(Task<PotionProcureResult> task,PotionScope s)
 {
  var result=await task;potions[s.Reward]=result.success?result.potion:null;
  Program.Observe("rc-procure-return",new{id=s.Id,reward=NativeIdentity.Ref(s.Reward),potion=NativeIdentity.Ref(s.Potion),result.success,failure=result.failureReason.ToString(),actual=NativeIdentity.Ref(result.success?result.potion:null)});return result;
 }
}
