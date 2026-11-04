using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SimulatorLab.Core;
namespace OfflineSearchHarness;

internal static partial class Program
{
 // A registered natural continuation; never enters a debug room or edits resources.
 // First delivery closes the actual third Ancient -> first battle/rewards slice.
 static void ThirdActFirstBattle()
 {
  var manager=RunManager.Instance;
  if(run!.CurrentActIndex!=2||run.CurrentRoom is not MapRoom||run.CurrentMapPoint!=null)throw new NativeDataException("Third act requires actual fresh native map");
  if(!NativeFullRun.Active&&!(run.Act.Ancient.GetType()==typeof(Tanx)||NativeFullRun.Active&&run.Act.Ancient.GetType()==typeof(Nonupeipe))||!Hook.ShouldAllowAncient(run,player!,run.Act.Ancient))throw new NotSupportedException("Third-act initial contract requires audited unblocked native Ancient");
  var start=run.Map.StartingMapPoint;
  Observe("natural-start-candidate",new{act=run.CurrentActIndex,coord=start.coord.ToString(),type=start.PointType.ToString()});
  Action("NextActStart",new{act=run.CurrentActIndex,to=start.coord.ToString()},()=>{
   Await(manager.EnterMapCoord(start.coord),"Natural third-act original starting point");EnterAuditedEvent();
  });
  RoomArrived();Observe("natural-first-node",new{act=run.CurrentActIndex,coord=run.CurrentMapPoint!.coord.ToString(),room=NativeIdentity.Ref(run.CurrentRoom),goal="actual third-act native Ancient decision"});
  var ev=manager.EventSynchronizer.GetLocalEvent();
  // Use an actual offered simple relic. TriBoomerang's enchant selection remains
  // explicit unsupported here, rather than silently inventing an answer.
  if(NativeFullRun.Active&&ev.GetType()!=typeof(Nonupeipe)&&ev.GetType()!=typeof(Tanx))SelectPersistentAncient(ev);
   else if(ev.GetType()==typeof(Nonupeipe))SelectNonupeipe(ev);
  else{
  string[] preferred=["IRON_CLUB","CLAWS","CROSSBOW","MEAT_CLEAVER","SAI","SPIKED_GAUNTLETS","TANXS_WHISTLE","THROWING_AXE","WAR_HAMMER"];
  if(NativeNaturalBoss.ThirdRoute)preferred=["THROWING_AXE",..preferred.Where(x=>x!="THROWING_AXE")];
  int index=-1;
  foreach(var key in preferred){index=ev.CurrentOptions.ToList().FindIndex(o=>o.TextKey=="TANX.pages.INITIAL.options."+key&&!o.IsLocked);if(index>=0)break;}
  if(index<0)throw new NotSupportedException("Third-act Tanx has no registered offered simple relic");
  var option=ev.CurrentOptions[index];SelectRoomOption(index);
  Observe("natural-third-ancient-selected",new{model=ev.Id.ToString(),option=NativeIdentity.Ref(option),key=option.TextKey});RoomComplete("actual Tanx option task and native pickup effects complete");
  }
  var current=run.CurrentMapPoint!;var children=current.Children.OrderBy(p=>p.PointType==MegaCrit.Sts2.Core.Map.MapPointType.Monster?0:1).ThenBy(p=>p.coord.col).ToArray();
  if(children.Length==0)throw new NotSupportedException("Third-act start has no legal child");
  var decisions=children.Select(p=>(object)new{from=current.coord.ToString(),to=p.coord.ToString()}).ToArray();MajorTravel(children[PolicyIndex("Travel",decisions)]);
  if(run.CurrentRoom is not CombatRoom)throw new NotSupportedException("Third-act initial continuation requires real combat room");
  MajorBattleAndRewards();if(terminal==NativeRunTerminal.Death)return;
  Observe("natural-act-scope",new{act=run.CurrentActIndex,map=manager.MapSelectionSynchronizer.MapGenerationCount,boss=run.Act.BossEncounter.Id.ToString()});
  Observe("natural-third-first-complete",new{act=run.CurrentActIndex,goal="normal-start third-act Ancient and first native battle/rewards; not RunVictory"});
 }
 static void ThirdActNodes()
 {
  var costs=new Dictionary<MegaCrit.Sts2.Core.Map.MapPoint,int>();var visiting=new HashSet<MegaCrit.Sts2.Core.Map.MapPoint>();
  int Cost(MegaCrit.Sts2.Core.Map.MapPoint p){
   if(costs.TryGetValue(p,out int v))return v;if(!visiting.Add(p))throw new NativeDataException("Third map cycle");
   v=p.PointType==MegaCrit.Sts2.Core.Map.MapPointType.Boss?0:(p.PointType switch{MegaCrit.Sts2.Core.Map.MapPointType.Elite=>100,MegaCrit.Sts2.Core.Map.MapPointType.Shop=>player!.Gold>=150?-15:60,MegaCrit.Sts2.Core.Map.MapPointType.Monster=>12,MegaCrit.Sts2.Core.Map.MapPointType.RestSite or MegaCrit.Sts2.Core.Map.MapPointType.Treasure=>0,_=>2})+(p.Children.Count==0?10000:p.Children.Min(Cost));
   visiting.Remove(p);return costs[p]=v;
  }
  // A low-HP policy may seek the nearest visible Rest. Unknown nodes remain
  // unknown; neither hidden encounters nor future RNG are queried here.
  var restCosts=new Dictionary<MegaCrit.Sts2.Core.Map.MapPoint,int>();var shopCosts=new Dictionary<MegaCrit.Sts2.Core.Map.MapPoint,int>();
  int RestDistance(MegaCrit.Sts2.Core.Map.MapPoint p,HashSet<MegaCrit.Sts2.Core.Map.MapPoint> seen){
   if(restCosts.TryGetValue(p,out int cached))return cached;
   if(!seen.Add(p))throw new NativeDataException("Third rest map cycle");
   if(p.PointType==MegaCrit.Sts2.Core.Map.MapPointType.RestSite)return 0;
   int risk=p.PointType switch{MegaCrit.Sts2.Core.Map.MapPointType.Elite=>100,MegaCrit.Sts2.Core.Map.MapPointType.Shop=>60,_=>1};
   return restCosts[p]=risk+(p.Children.Count==0?1000:p.Children.Min(c=>RestDistance(c,new(seen))));
  }
  int ShopDistance(MegaCrit.Sts2.Core.Map.MapPoint p,HashSet<MegaCrit.Sts2.Core.Map.MapPoint> seen){
   if(shopCosts.TryGetValue(p,out int cached))return cached;
   if(!seen.Add(p))throw new NativeDataException("Third shop map cycle");
   if(p.PointType==MegaCrit.Sts2.Core.Map.MapPointType.Shop)return 0;
   return shopCosts[p]=(p.PointType==MegaCrit.Sts2.Core.Map.MapPointType.Elite?100:1)+(p.Children.Count==0?1000:p.Children.Min(c=>ShopDistance(c,new(seen))));
  }
  for(int n=0;n<24;n++){
   costs.Clear();var current=run!.CurrentMapPoint!;bool seekRest=player!.Creature.CurrentHp<player.Creature.MaxHp*0.7m;
   // A distance sentinel is not a reachable service. Once the remaining DAG
   // contains no Shop, its ranking must not override an immediately usable Rest.
   bool reachableShop=current.Children.Any(p=>ShopDistance(p,[])<1000);
   bool seekShop=reachableShop&&player.Gold>=150&&player.Creature.CurrentHp<player.Creature.MaxHp*0.5m;
   var options=current.Children.OrderBy(p=>seekShop?ShopDistance(p,[]):seekRest?RestDistance(p,[]):Cost(p)).ThenBy(Cost).ThenBy(p=>p.coord.col).ToArray();if(options.Length==0)throw new NotSupportedException("Third route ended before Boss");
   var decisions=options.Select(p=>(object)new{from=current.coord.ToString(),to=p.coord.ToString()}).ToArray();MajorTravel(options[PolicyIndex("Travel",decisions)]);
   if(run.CurrentRoom is CombatRoom battle){
    bool boss=battle.Encounter.RoomType==RoomType.Boss;MajorBattleAndRewards();if(terminal==NativeRunTerminal.Death)return;
    if(boss){FullActBossComplete(battle);Observe("natural-third-boss-complete",new{act=run.CurrentActIndex,encounter=battle.Encounter.Id.ToString(),goal="actual third Boss victory and native rewards; terminal continuation remains separate"});return;}
   }else if(run.CurrentRoom is MerchantRoom)ThirdShop();else PreparedNonCombatRoom();
  }
  throw new TimeoutException("Third natural act node budget");
 }
}
