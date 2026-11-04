using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SimulatorLab.Core;
namespace OfflineSearchHarness;

// A separate, natural-map profile. No debug entry or resource fixture is used.
internal static class NativeNaturalBoss
{
 internal static bool ThirdRoute=>NativeLifecycle.Scenario=="major-natural-act3-route";
 internal static bool Third=>IsThird(NativeLifecycle.Scenario)||NativeFullRun.Active&&Program.InThirdAct;
 internal static bool IsThird(string s)=>s is "major-natural-act3" or "major-natural-act3-route";
 internal static bool IsScenario(string s)=>NativeSolverJourney.IsScenario(s)||IsThird(s)||s is "major-natural-boss" or "major-natural-second" or "major-natural-act2" or "major-natural-act2-elite";
 internal static bool IsSecondScenario(string s)=>IsThird(s)||s is "major-natural-second" or "major-natural-act2" or "major-natural-act2-elite";
 internal static bool IsAct2(string s)=>NativeDarv.IsActTwoProfile(s)||IsThird(s)||s is "major-natural-act2" or "major-natural-act2-elite";
 internal static bool IsElite2(string s)=>s=="major-natural-act2-elite";
 internal static bool Act2=>IsAct2(NativeLifecycle.Scenario);
 internal static bool SecondBattle=>NativeDarv.Active||IsSecondScenario(NativeLifecycle.Scenario);
 // Darv keeps the certified prefix's observation schema. The older dedicated
 // serialization scenarios keep their additional native save-card projection.
 internal static bool CardRecords=>IsSecondScenario(NativeLifecycle.Scenario);
 internal static bool Active=>IsScenario(NativeLifecycle.Scenario);
}
internal static partial class Program
{
 static object? NaturalMapSnapshot()
 {
  if(!NativeNaturalBoss.Active||run?.Map==null||RunManager.Instance.MapSelectionSynchronizer.MapGenerationCount==0)return null;
  var map=run.Map;
  var points=map.GetAllMapPoints().Append(map.StartingMapPoint).Append(map.BossMapPoint).Distinct().OrderBy(x=>x.coord.row).ThenBy(x=>x.coord.col).ToArray();
  return new{reference=NativeIdentity.Ref(map),mapType=map.GetType().Name,act=run.CurrentActIndex,generation=RunManager.Instance.MapSelectionSynchronizer.MapGenerationCount,start=map.StartingMapPoint.coord.ToString(),boss=map.BossMapPoint.coord.ToString(),bossModel=run.Act.BossEncounter.Id.ToString(),ancientModel=run.Act.Ancient.Id.ToString(),points=points.Select(x=>new{coord=x.coord.ToString(),type=x.PointType.ToString(),children=x.Children.OrderBy(c=>c.coord.row).ThenBy(c=>c.coord.col).Select(c=>c.coord.ToString()).ToArray()}).ToArray()};
 }
 static void NaturalBoss()
 {
  FullActEnter();
  // Public DAG only. Memoization bounds ranking work independently of path count.
  var costs=new Dictionary<MapPoint,int>();var visiting=new HashSet<MapPoint>();
  int Cost(MapPoint p){
   if(costs.TryGetValue(p,out var cost))return cost;
   if(!visiting.Add(p))throw new NativeDataException("Generated map cycle");
   cost=p.PointType==MapPointType.Boss?0:(p.PointType switch{MapPointType.Elite=>100,MapPointType.Shop=>60,MapPointType.Monster=>4,MapPointType.RestSite or MapPointType.Treasure=>0,_=>12})+(p.Children.Count==0?10000:p.Children.Min(Cost));
   visiting.Remove(p);costs[p]=cost;return cost;
  }
  for(int n=0;n<24;n++){
   var current=run!.CurrentMapPoint??throw new NotSupportedException("Natural current map point unavailable");
   var ordered=current.Children.OrderBy(Cost).ThenBy(x=>x.coord.col).ToArray();
   if(ordered.Length==0)throw new NotSupportedException("Natural route ended before Boss");
   var decisions=ordered.Select(x=>(object)new{from=current.coord.ToString(),to=x.coord.ToString()}).ToArray();
   MajorTravel(ordered[PolicyIndex("Travel",decisions)]);
   if(run.CurrentRoom is CombatRoom combat){
    bool boss=combat.Encounter.RoomType==RoomType.Boss;
    MajorBattleAndRewards();if(terminal==NativeRunTerminal.Death)return;
    if(boss){
     FullActBossComplete(combat);
     if(NativeDarv.Active)ForeseerNextActPreview();
     ActualBossReady();FullActEnter();
     if(!NativeFullRun.Active&&!(NativeDarv.Active?run.Act.Ancient is MegaCrit.Sts2.Core.Models.Events.Darv:run.Act.Ancient is MegaCrit.Sts2.Core.Models.Events.Tezcatara)||!MegaCrit.Sts2.Core.Hooks.Hook.ShouldAllowAncient(run,player!,run.Act.Ancient))throw new NotSupportedException(NativeDarv.Active?"Declared Darv continuation requires actual unblocked native Darv":"Next-act first-choice contract currently requires unblocked native Tezcatara");
     if(NativeDarv.Active)ForeseerNextActVerify();
     var start=run.Map.StartingMapPoint;
     Observe("natural-start-candidate",new{act=run.CurrentActIndex,coord=start.coord.ToString(),type=start.PointType.ToString()});
     Action("NextActStart",new{act=run.CurrentActIndex,to=start.coord.ToString()},()=>{
      if(run.CurrentMapPoint!=null||run.CurrentRoom is not MapRoom)throw new NativeActionRejectedException("Next act start requires actual fresh native map");
      Await(RunManager.Instance.EnterMapCoord(start.coord),"Natural next-act original starting map point");
      if(run.CurrentRoom is EventRoom)EnterAuditedEvent();else if(run.CurrentRoom is CombatRoom)Ready();
     });
     RoomArrived();Observe("natural-first-node",new{act=run.CurrentActIndex,coord=run.CurrentMapPoint!.coord.ToString(),room=NativeIdentity.Ref(run.CurrentRoom),goal="actual next-act first native decision; choice not yet taken"});
     if(NativeNaturalBoss.SecondBattle){ContinueSecondBattle();if(terminal==NativeRunTerminal.Death)return;}
     if(NativeNaturalBoss.Act2){ContinueActTwo();if(terminal==NativeRunTerminal.Death)return;}
     if(NativeFullRun.Active){FullRunFinalAct();if(terminal==NativeRunTerminal.Death)return;}
      else if(NativeNaturalBoss.Third){ThirdActFirstBattle();if(terminal==NativeRunTerminal.Death)return;if(NativeNaturalBoss.ThirdRoute){ThirdActNodes();if(terminal==NativeRunTerminal.Death)return;}}
     NativeRewards.AssertConsumed();Observe("major-complete",new{scenario,natural=true,goal=NativeFullRun.Active?"normal-start native terminal or explicit incomplete failure; full-run-progress records scope":NativeNaturalBoss.Third?"natural third-act Ancient and first battle/rewards; not full run":NativeNaturalBoss.IsElite2(scenario)?"natural second-act Elite and rewards; not full run":NativeNaturalBoss.Act2?"two natural Boss victories/rewards and third act MapRoom; not RunVictory":NativeNaturalBoss.SecondBattle?"natural Boss, Ancient choice and next-act first battle/rewards":"natural first-act Boss rewards, next-act task and first native decision"});
     Observe("n3c-contract",new{name=scenario,complete=true});return;
    }
   }else {PreparedNonCombatRoom();if(terminal==NativeRunTerminal.Death)return;}
  }
  throw new TimeoutException("Natural Boss node budget");
 }
 static void ContinueSecondBattle()
 {
  var ev=RunManager.Instance.EventSynchronizer.GetLocalEvent();
  if(NativeFullRun.Active&&ev is not MegaCrit.Sts2.Core.Models.Events.Darv){SelectPersistentAncient(ev);}else if(NativeDarv.Active){SelectDarvOption(ev);}else{
  int index=ev.CurrentOptions.ToList().FindIndex(x=>x.TextKey=="TEZCATARA.pages.INITIAL.options.NUTRITIOUS_SOUP"&&!x.IsLocked);
  if(index<0)throw new NotSupportedException("Natural continuation currently requires offered NutritiousSoup; other Ancient choices not silently substituted");
  var option=ev.CurrentOptions[index];SelectRoomOption(index);
  Observe("natural-ancient-selected",new{model=ev.Id.ToString(),option=NativeIdentity.Ref(option),key=option.TextKey});RoomComplete("original Ancient option task and native relic/enchantment effects complete");}
  var current=run!.CurrentMapPoint!;var children=current.Children.OrderBy(x=>x.PointType==MapPointType.Monster?0:1).ThenBy(x=>x.coord.col).ToArray();
  if(children.Length==0)throw new NotSupportedException("Native next-act start has no legal child");
  var decisions=children.Select(x=>(object)new{from=current.coord.ToString(),to=x.coord.ToString()}).ToArray();MajorTravel(children[PolicyIndex("Travel",decisions)]);
  if(run.CurrentRoom is not CombatRoom)throw new NotSupportedException("Natural next-act continuation reached noncombat room; explicit composition required");
  MajorBattleAndRewards();
 }
 static void ContinueActTwo()
 {
  Observe("natural-act-scope",new{act=run!.CurrentActIndex,map=RunManager.Instance.MapSelectionSynchronizer.MapGenerationCount,boss=run.Act.BossEncounter.Id.ToString()});
  var costs=new Dictionary<MapPoint,int>();var visiting=new HashSet<MapPoint>();
  int Cost(MapPoint p){if(costs.TryGetValue(p,out var v))return v;if(!visiting.Add(p))throw new NativeDataException("Generated second map cycle");bool elite=NativeNaturalBoss.IsElite2(scenario);v=(elite?p.PointType==MapPointType.Elite:p.PointType==MapPointType.Boss)?0:(p.PointType switch{MapPointType.Elite=>100,MapPointType.Shop=>60,MapPointType.Monster=>12,MapPointType.RestSite or MapPointType.Treasure=>0,_=>2})+(p.Children.Count==0?10000:p.Children.Min(Cost));visiting.Remove(p);costs[p]=v;return v;}
  for(int n=0;n<24;n++){
   var current=run!.CurrentMapPoint!;var options=current.Children.OrderBy(Cost).ThenBy(x=>x.coord.col).ToArray();
   if(options.Length==0)throw new NotSupportedException("Second act route ended before Boss");
   var decisions=options.Select(x=>(object)new{from=current.coord.ToString(),to=x.coord.ToString()}).ToArray();MajorTravel(options[PolicyIndex("Travel",decisions)]);
   if(run.CurrentRoom is CombatRoom combat){
    bool boss=combat.Encounter.RoomType==RoomType.Boss;MajorBattleAndRewards();if(terminal==NativeRunTerminal.Death)return;
    if(NativeNaturalBoss.IsElite2(scenario)&&combat.Encounter.RoomType==RoomType.Elite){Observe("natural-elite-complete",new{act=run.CurrentActIndex,encounter=combat.Encounter.Id.ToString(),goal="actual natural second-act Elite and original rewards; not full run"});return;}
    if(boss){FullActBossComplete(combat);ActualBossReady();FullActEnter();Observe("natural-act2-complete",new{act=run.CurrentActIndex,map=RunManager.Instance.MapSelectionSynchronizer.MapGenerationCount,goal="two actual natural Boss victories/rewards and third act MapRoom; not RunVictory"});return;}
   }else {PreparedNonCombatRoom();if(terminal==NativeRunTerminal.Death)return;}
  }
  throw new TimeoutException("Second natural act node budget");
 }
 static void ActualBossReady()
 {
  var manager=RunManager.Instance;
  Action("ActReady",new{act=run!.CurrentActIndex,map=manager.MapSelectionSynchronizer.MapGenerationCount},()=>{
   Observe("major-ready-start",new{room=NativeIdentity.Ref(run.CurrentRoom)});NativeLifecycle.ActTask=null;manager.ActChangeSynchronizer.SetLocalPlayerReady();
   Await(manager.ActionExecutor.FinishedExecutingActions(),"Natural actual ready vote");var deadline=DateTime.UtcNow.AddSeconds(45);
   while(NativeLifecycle.ActTask==null){NativeHost.Ensure();if(DateTime.UtcNow>deadline)throw new TimeoutException("Natural ready task missing");loop.Pump(TimeSpan.FromMilliseconds(10));}
   Await(NativeLifecycle.ActTask,"Natural actual EnterNextAct");Observe("major-ready-return",new{act=run.CurrentActIndex,map=manager.MapSelectionSynchronizer.MapGenerationCount});
  });
 }
 static void PreparedNonCombatRoom()
 {
  var manager=RunManager.Instance;
  if(run!.CurrentRoom is RestSiteRoom rest){
   bool smith=player!.Creature.CurrentHp>=player.Creature.MaxHp*0.65m&&player.Deck.Cards.Any(c=>c.IsUpgradable)&&!(NativeNaturalBoss.Act2&&run.CurrentActIndex>0&&player.Creature.CurrentHp<player.Creature.MaxHp);
   int index=rest.Options.ToList().FindIndex(x=>x.OptionId==(smith?"SMITH":"HEAL")&&x.IsEnabled);
   if(index<0)throw new NotSupportedException("Natural rest option unavailable");
    if(smith&&!NativeChoices.Replaying){var cards=player.Deck.Cards.Where(c=>c.IsUpgradable).ToArray();var chosen=cards.OrderByDescending(c=>NativeAct2Policy.Active?NativeAct2Policy.UpgradeRank(c):c.Id.ToString()=="CARD.BASH"?200:NativePreparation.CardRank(c)).First();Plan(Array.IndexOf(cards,chosen));}
   SelectRoomOption(index);RoomComplete("actual native natural rest/upgrade");
  }else if(run.CurrentRoom is EventRoom){
   var ev=manager.EventSynchronizer.GetLocalEvent();
   if(ev is MegaCrit.Sts2.Core.Models.Events.BattlewornDummy)throw new NotSupportedException("Natural nested event requires composed event task contract");
   for(int q=0;!ev.IsFinished&&q<12;q++){
    var parent=run.CurrentRoom;
    SelectRoomOption(ev.CurrentOptions.Count==1?0:ev is MegaCrit.Sts2.Core.Models.Events.SlipperyBridge bridge?NativeSlipperyBridge.Select(bridge):ev is MegaCrit.Sts2.Core.Models.Events.ThisOrThat or MegaCrit.Sts2.Core.Models.Events.StoneOfAllTime or MegaCrit.Sts2.Core.Models.Events.DenseVegetation or MegaCrit.Sts2.Core.Models.Events.WhisperingHollow or MegaCrit.Sts2.Core.Models.Events.DollRoom or MegaCrit.Sts2.Core.Models.Events.HungryForMushrooms?0:1);
    if(!ReferenceEquals(parent,run.CurrentRoom)){
     var child=run.CurrentRoom as CombatRoom??throw new NotSupportedException("Event changed to unsupported child room");
     if(child.ShouldResumeParentEventAfterCombat)throw new NotSupportedException("Natural event child requires explicit Resume task integration");
     Observe("full-event-child",new{parent=NativeIdentity.Ref(parent),eventModel=NativeIdentity.Ref(ev),child=NativeIdentity.Ref(child),child.ParentEventId,child.ShouldResumeParentEventAfterCombat,encounter=child.Encounter.Id.ToString()});
     Ready();RoomArrived();MajorBattleAndRewards();return;
    }
   }
   RoomComplete("actual native natural event");
   }else if(run.CurrentRoom is MerchantRoom&&NativeSolverJourney.ShopScenario(scenario)){ThirdShop();}
   else if(run.CurrentRoom is TreasureRoom){TreasureC(false);RoomComplete("native treasure rewards; next travel owns original Exit");}
  else throw new NotSupportedException("Natural room requires capability composition: "+run.CurrentRoom!.GetType().Name);
 }
}
