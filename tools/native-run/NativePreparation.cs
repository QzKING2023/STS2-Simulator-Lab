using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
namespace OfflineSearchHarness;

// Finite internal composition, selected once before host installation. No public SDK.
internal static class NativePreparation
{
 internal static bool IsScenario(string s)=>NativeNaturalBoss.IsScenario(s)||s is "major-prepared-elite" or "major-prepared-boss" or "major-prepared-natural";
 internal static bool Active {get;private set;}
 internal static bool Boss=>NativeLifecycle.Scenario=="major-prepared-boss";
 internal static bool Natural=>NativeLifecycle.Scenario=="major-prepared-natural";
 internal static string Policy=>PolicyFor(locked??"");
 internal static string PolicyFor(string s)=>NativeSolverJourney.IsScenario(s)?"oracle-combatsolver-native-first-act/v1":s=="major-natural-act3-route"?"oracle-legal-natural-act3-route/v41-third-form-juggernaut":NativeNaturalBoss.IsThird(s)?"oracle-legal-natural-act3/v1-tanx":NativeNaturalBoss.IsElite2(s)?"oracle-legal-act2-nearest-elite/v4-facing":NativeNaturalBoss.IsAct2(s)?"oracle-legal-natural-act2/v7-facing":NativeNaturalBoss.IsSecondScenario(s)?"oracle-legal-natural-second/v1":NativeNaturalBoss.IsScenario(s)?"oracle-legal-natural-boss/v1":"oracle-legal-preparation/v5";
 static string? locked;
 internal static void Configure(string scenario){if(locked!=null)throw new InvalidOperationException("Capability configuration already locked");locked=scenario;Active=IsScenario(scenario);}
 internal static void Ensure(){if(locked!=NativeLifecycle.Scenario)throw new InvalidOperationException("Capability configuration changed");}
 internal static void Install(){if(Active)NativeHost.Register(HarmonyLib.AccessTools.DeclaredMethod(typeof(MegaCrit.Sts2.Core.Rooms.TreasureRoom),"Exit"),nameof(ChestBefore),typeof(NativePreparation),nameof(ChestAfter));}
 static void ChestBefore(MegaCrit.Sts2.Core.Rooms.TreasureRoom __instance)=>Program.Observe("prep-chest-exit-start",new{room=NativeIdentity.Ref(__instance)});
 static void ChestAfter(MegaCrit.Sts2.Core.Rooms.TreasureRoom __instance,ref Task __result)=>__result=ChestReturn(__result,__instance);
 static async Task ChestReturn(Task task,MegaCrit.Sts2.Core.Rooms.TreasureRoom room){await task;Program.Observe("prep-chest-exit-return",new{room=NativeIdentity.Ref(room),closed=MegaCrit.Sts2.Core.Runs.RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics==null});}
 internal static object Profile=>Describe(locked!);
 internal static object Describe(string scenario)=>new{id="finite-preparation/v1",scenario,policy=PolicyFor(scenario),nodes=24,features=new[]{"native-combat","native-rewards","rest-upgrade","audited-simple-events","Hollow-gold","treasure"},merchant=NativeSolverJourney.ShopScenario(scenario)?"composed native merchant/default pickup/price RNG/exit; no removal/refill":"explicit unsupported until composed",information=NativeSolverJourney.IsScenario(scenario)?"Oracle: CombatSolver draw/RNG/private state; native map and fixed reward choices":"oracle-current-native-intents; public map; no future RNG or draw order"};
 // Reader-only historical schema compatibility. Generation always uses Describe.
 internal static bool MatchesDescription(string scenario,System.Text.Json.JsonElement actual)
 {
  var expected=System.Text.Json.JsonSerializer.SerializeToElement(Describe(scenario));
  if(NativeBoundaryContract.Equal(expected,actual))return true;
  if(scenario=="major-natural-act3-route"){
   foreach(var policy in new[]{"oracle-legal-natural-act3-route/v40-juggernaut-block","oracle-legal-natural-act3-route/v39-boss-exhaust-unused-setup","oracle-legal-natural-act3-route/v38-boss-thin-and-reserve","oracle-legal-natural-act3-route/v37-boss-limit-anger-copies","oracle-legal-natural-act3-route/v36-boss-cure-for-unblocked-burst","oracle-legal-natural-act3-route/v35-boss-burst-before-small-block","oracle-legal-natural-act3-route/v34-boss-phase-two-burst","oracle-legal-natural-act3-route/v33-boss-reserve-cure","oracle-legal-natural-act3-route/v32-boss-hybrid-attacks","oracle-legal-natural-act3-route/v31-boss-no-recurring-selfharm","oracle-legal-natural-act3-route/v30-boss-exhaust-status","oracle-legal-natural-act3-route/v29-boss-phase-two-draw","oracle-legal-natural-act3-route/v28-boss-free-attacks","oracle-legal-natural-act3-route/v27-boss-use-visible-block","oracle-legal-natural-act3-route/v26-boss-emergency-energy","oracle-legal-natural-act3-route/v25-boss-autoplay","oracle-legal-natural-act3-route/v24-boss-phase","oracle-legal-natural-act3-route/v23-reachable-services","oracle-legal-natural-act3-route/v22-summoner-target","oracle-legal-natural-act3-route/v21-focused-calculation","oracle-legal-natural-act3-route/v20-primary-focus","oracle-legal-natural-act3-route/v19-third-aoe","oracle-legal-natural-act3-route/v18-compact-aoe","oracle-legal-natural-act3-route/v17-native-survival","oracle-legal-natural-act3-route/v16-native-supplies","oracle-legal-natural-act3-route/v15-native-shop","oracle-legal-natural-act3-route/v14-emergency-redraw","oracle-legal-natural-act3-route/v13-rest-distance","oracle-legal-natural-act3-route/v12-custom-event","oracle-legal-natural-act3-route/v1-tanx","oracle-legal-natural-act3-route/v2-axe","oracle-legal-natural-act3-route/v3-defence","oracle-legal-natural-act3-route/v4-fortifier","oracle-legal-natural-act3-route/v5-hybrid","oracle-legal-natural-act3-route/v6-known-map","oracle-legal-natural-act3-route/v7-deck","oracle-legal-natural-act3-route/v8-survival","oracle-legal-natural-act3-route/v9-unified","oracle-legal-natural-act3-route/v10-decline","oracle-legal-natural-act3-route/v11-emergency-hand"}){
    var historical=System.Text.Json.Nodes.JsonNode.Parse(expected.GetRawText())!;historical["policy"]=policy;
    if(NativeBoundaryContract.Equal(System.Text.Json.JsonSerializer.SerializeToElement(historical),actual))return true;
   }
   return false;
  }
  if(!NativeNaturalBoss.IsAct2(scenario)||NativeNaturalBoss.IsThird(scenario))return false;
  var prior=NativeNaturalBoss.IsElite2(scenario)?new[]{"oracle-legal-act2-nearest-elite/v1","oracle-legal-act2-nearest-elite/v2-deck","oracle-legal-act2-nearest-elite/v3-deck"}:new[]{"oracle-legal-natural-act2/v2","oracle-legal-natural-act2/v3-route","oracle-legal-natural-act2/v4-route-heal","oracle-legal-natural-act2/v5-deck","oracle-legal-natural-act2/v6-deck"};
  foreach(var policy in prior){
   var node=System.Text.Json.Nodes.JsonNode.Parse(expected.GetRawText())!;node["policy"]=policy;
   if(NativeBoundaryContract.Equal(System.Text.Json.JsonSerializer.SerializeToElement(node),actual))return true;
  }
  return false;
 }
 // This is policy ranking, never a substitute for native card legality or effects.
 internal static int CardRank(CardModel card)=>card.Id.ToString() switch{
  "CARD.DOMINATE"=>155,"CARD.STONE_ARMOR"=>151,"CARD.ARMAMENTS"=>138,"CARD.TRUE_GRIT"=>126,"CARD.CINDER"=>75,"CARD.INFERNO"=>110,
  "CARD.INFLAME"=>150,"CARD.METALLICIZE"=>145,"CARD.SHRUG_IT_OFF"=>140,"CARD.POMMEL_STRIKE"=>135,
  "CARD.FLAME_BARRIER"=>134,"CARD.UPPERCUT"=>132,"CARD.BATTLE_TRANCE"=>131,"CARD.DISARM"=>130,
  "CARD.IMPERVIOUS"=>129,"CARD.OFFERING"=>128,"CARD.SHOCKWAVE"=>127,
  "CARD.HEMOKINESIS"=>122,"CARD.IRON_WAVE"=>120,"CARD.CLEAVE"=>115,"CARD.CARNAGE"=>114,
  "CARD.THUNDERCLAP"=>112,"CARD.PERFECTED_STRIKE"=>108,"CARD.HEADBUTT"=>106,
  "CARD.ANGER"=>104,"CARD.INFERNAL_BLADE"=>40,"CARD.BLOODLETTING"=>30,
  _=>card.Type==CardType.Power?118:card.Type==CardType.Attack?100:80};
 internal static int RewardIndex(IReadOnlyList<CardModel> cards)=>Enumerable.Range(0,cards.Count).OrderByDescending(i=>CardRank(cards[i])).ThenBy(i=>i).First();
}

internal static partial class Program
{
 static void PreparedMajor()
 {
  var manager=MegaCrit.Sts2.Core.Runs.RunManager.Instance;var act=run!.Acts[run.CurrentActIndex];int battles=0;
  for(int n=0;n<24;n++){
   var children=run.CurrentMapPoint!.Children.ToArray();
   if(children.Length==0)throw new NotSupportedException("Prepared route has no legal child");
   // Fixture preparation follows real legal edges until the mandatory chest/Boss
   // boundary. The jump itself is explicitly declared, never counted as natural.
   if(!NativePreparation.Natural&&children.All(x=>x.PointType==MegaCrit.Sts2.Core.Map.MapPointType.Boss||!NativePreparation.Boss&&x.PointType==MegaCrit.Sts2.Core.Map.MapPointType.Treasure))break;
   int Distance(MegaCrit.Sts2.Core.Map.MapPoint p,HashSet<MegaCrit.Sts2.Core.Map.MapPoint> seen){if(!seen.Add(p))return 1000;if(p.PointType==MegaCrit.Sts2.Core.Map.MapPointType.Elite)return 0;return p.Children.Count==0?1000:1+p.Children.Min(x=>Distance(x,new(seen)));}
   int RouteCost(MegaCrit.Sts2.Core.Map.MapPoint p,HashSet<MegaCrit.Sts2.Core.Map.MapPoint> seen){
    if(!seen.Add(p))return 10000;if(p.PointType==MegaCrit.Sts2.Core.Map.MapPointType.Boss)return 0;
    int cost=p.PointType switch{MegaCrit.Sts2.Core.Map.MapPointType.Elite=>100,MegaCrit.Sts2.Core.Map.MapPointType.Shop=>60,MegaCrit.Sts2.Core.Map.MapPointType.Monster=>4,MegaCrit.Sts2.Core.Map.MapPointType.RestSite=>0,MegaCrit.Sts2.Core.Map.MapPointType.Treasure=>0,_=>12};
    return cost+(p.Children.Count==0?10000:p.Children.Min(x=>RouteCost(x,new(seen))));
   }
   int Rank(MegaCrit.Sts2.Core.Map.MapPoint p)=>NativePreparation.Natural?Distance(p,[]):RouteCost(p,[]);
   var ordered=children.OrderBy(Rank).ThenBy(x=>x.coord.col).ToArray();var decisions=ordered.Select(x=>(object)new{from=run.CurrentMapPoint.coord.ToString(),to=x.coord.ToString()}).ToArray();
   MajorTravel(ordered[PolicyIndex("Travel",decisions)]);
   if(run.CurrentRoom is MegaCrit.Sts2.Core.Rooms.CombatRoom cr){
    MajorBattleAndRewards();battles++;if(terminal==SimulatorLab.Core.NativeRunTerminal.Death)return;
    if(NativePreparation.Natural&&cr.Encounter.RoomType==MegaCrit.Sts2.Core.Rooms.RoomType.Elite){PreparedComplete();return;}
   }else if(run.CurrentRoom is MegaCrit.Sts2.Core.Rooms.RestSiteRoom rest){
    bool smith=player!.Creature.CurrentHp>=player.Creature.MaxHp*0.65m&&player.Deck.Cards.Any(c=>c.IsUpgradable);
    int index=rest.Options.ToList().FindIndex(x=>x.OptionId==(smith?"SMITH":"HEAL")&&x.IsEnabled);
    if(index<0)throw new NotSupportedException("Prepared rest option unavailable");
    if(smith&&!NativeChoices.Replaying){var cards=player.Deck.Cards.Where(c=>c.IsUpgradable).ToArray();var chosen=cards.OrderByDescending(c=>c.Id.ToString()=="CARD.BASH"?200:NativePreparation.CardRank(c)).First();Plan(Array.IndexOf(cards,chosen));}
    SelectRoomOption(index);RoomComplete("actual native preparation rest/upgrade");
   }else if(run.CurrentRoom is MegaCrit.Sts2.Core.Rooms.EventRoom){
    var ev=manager.EventSynchronizer.GetLocalEvent();
    if(ev is MegaCrit.Sts2.Core.Models.Events.BattlewornDummy)throw new NotSupportedException("Prepared nested event needs composed event task contract");
    for(int q=0;!ev.IsFinished&&q<12;q++)SelectRoomOption(ev is MegaCrit.Sts2.Core.Models.Events.DenseVegetation or MegaCrit.Sts2.Core.Models.Events.WhisperingHollow?0:1);
    RoomComplete("actual native preparation event");
   }else if(run.CurrentRoom is MegaCrit.Sts2.Core.Rooms.TreasureRoom){TreasureC(false);RoomComplete("native treasure rewards; original next travel owns Exit");}
   else throw new NotSupportedException("Prepared room requires next capability composition: "+run.CurrentRoom!.GetType().Name);
  }
  if(NativePreparation.Natural)throw new TimeoutException("Prepared natural target not reached within node budget");
  if(battles<1)throw new NativeDataException("Prepared fixture missing actual earned combat preparation");
  var selected=NativePreparation.Boss?act.AllBossEncounters.OrderBy(x=>x.Id.ToString(),StringComparer.Ordinal).First():act.AllEliteEncounters.OrderBy(x=>x.Id.ToString(),StringComparer.Ordinal).First();
  Observe("fixture-setup-declared",new{name=scenario,fixtureKind="native-major-encounter",changes="Original EnterRoomDebug after actual legal preparation path/rewards/rest; no resource/HP/deck edits",encounter=selected.Id.ToString()});
  Action("MajorFixture",new{encounter=selected.Id.ToString(),type=selected.RoomType.ToString()},()=>{Await(manager.EnterRoomDebug(selected.RoomType,model:selected.ToMutable(),showTransition:false),"Prepared fixed original target");Ready();});
  RoomArrived();MajorBattleAndRewards();if(terminal==SimulatorLab.Core.NativeRunTerminal.Death)return;
  if(NativePreparation.Boss){
   Action("ActReady",new{act=run.CurrentActIndex,map=manager.MapSelectionSynchronizer.MapGenerationCount},()=>{
    Observe("major-ready-start",new{room=NativeIdentity.Ref(run.CurrentRoom)});NativeLifecycle.ActTask=null;manager.ActChangeSynchronizer.SetLocalPlayerReady();
    Await(manager.ActionExecutor.FinishedExecutingActions(),"Prepared actual ready vote");
    var deadline=DateTime.UtcNow.AddSeconds(45);
    while(NativeLifecycle.ActTask==null){NativeHost.Ensure();if(DateTime.UtcNow>deadline)throw new TimeoutException("Prepared ready task missing");loop.Pump(TimeSpan.FromMilliseconds(10));}
    Await(NativeLifecycle.ActTask,"Prepared actual EnterNextAct");Observe("major-ready-return",new{act=run.CurrentActIndex,map=manager.MapSelectionSynchronizer.MapGenerationCount});
   });
  }
  PreparedComplete();
 }
 static void PreparedComplete(){NativeRewards.AssertConsumed();Observe("major-complete",new{scenario,natural=NativePreparation.Natural,goal="actual prepared target victory/rewards; Boss actual next act"});Observe("n3c-contract",new{name=scenario,complete=true});}
 static (CardModel card,MegaCrit.Sts2.Core.Entities.Creatures.Creature? target)[] PreparedCandidates((CardModel card,MegaCrit.Sts2.Core.Entities.Creatures.Creature? target)[] candidates)
 {
  string before=StateStamp();var combat=CombatManager.Instance.DebugOnlyGetState()!;
  int incoming=combat.Enemies.Sum(e=>e.Monster?.NextMove.Intents.OfType<MegaCrit.Sts2.Core.MonsterMoves.Intents.AttackIntent>().Sum(i=>i.GetTotalDamage([player!.Creature],e))??0);
  if(StateStamp()!=before)throw new NativeDataException("Intent policy query changed native state/RNG");
  int deficit=Math.Max(0,incoming-player!.Creature.Block);
  // Frozen decisions are checked against all native-legal actions, independently of current heuristic exclusions.
  if(replayPath!=null||branchNext!=null)return candidates;
  int Score(CardModel card,MegaCrit.Sts2.Core.Entities.Creatures.Creature? target){
   if(ThirdPolicyActive)return ThirdPolicyRank(card,target,combat,incoming,deficit);
   string id=card.Id.ToString();
   if(card.Type==CardType.Attack&&target!=null&&combat.Enemies.Contains(target)&&card.DynamicVars.TryGetValue("Damage",out var killDamage)){
    decimal estimate=killDamage.BaseValue+(player!.Creature.Powers.FirstOrDefault(p=>p.Id.ToString()=="POWER.STRENGTH_POWER")?.Amount??0);
    if(target.Powers.Any(p=>p.Id.ToString()=="POWER.VULNERABLE_POWER"))estimate*=1.5m;
    if(estimate>=target.CurrentHp+target.Block)return 1000+(int)estimate;
   }
   if(NativeAct2Policy.Active&&id is "CARD.DARK_EMBRACE" or "CARD.FEEL_NO_PAIN")return 160;
   if(card.Type==CardType.Power)return 600;
   if(card.DynamicVars.ContainsKey("Block"))return deficit>0?400+(int)(10*card.DynamicVars.Block.BaseValue/Math.Max(1,card.EnergyCost.GetWithModifiers(CostModifiers.All))):-100;
   if(id=="CARD.DOMINATE")return 520;
   if(id is "CARD.OFFERING" or "CARD.BATTLE_TRANCE")return 550;
   if(id=="CARD.DISARM"&&incoming>0)return 510;
   if(id is "CARD.DEFEND_IRONCLAD" or "CARD.SHRUG_IT_OFF" or "CARD.FLAME_BARRIER" or "CARD.IMPERVIOUS" or "CARD.TRUE_GRIT")return deficit>0?400+Math.Min(deficit,12):-100;
   if(id=="CARD.BASH")return target!=null&&!target.Powers.Any(p=>p.Id.ToString()=="POWER.VULNERABLE_POWER")&&player!.PlayerCombatState!.Energy>=3?510:280;
   return card.Type==CardType.Attack?200+NativePreparation.CardRank(card)+(card.DynamicVars.TryGetValue("Damage",out var damage)?(int)(10*damage.BaseValue/Math.Max(1,card.EnergyCost.GetWithModifiers(CostModifiers.All))):0):150;
  }
  bool surrounded=NativeAct2Policy.Active&&player!.Creature.Powers.Any(p=>p.Id.ToString()=="POWER.SURROUNDED_POWER");
  var ordered=candidates.Where(x=>Score(x.card,x.target)>=0).OrderByDescending(x=>Score(x.card,x.target)).ThenBy(x=>x.target==null?0:combat.Enemies.Contains(x.target)?surrounded?(x.target.Powers.Any(p=>p.Id.ToString()=="POWER.BACK_ATTACK_LEFT_POWER")?0:1000):x.target.CurrentHp+x.target.Block:10000).ToArray();
  if(StateStamp()!=before)throw new NativeDataException("Card policy query changed native state/RNG");return ordered;
 }
 static void PreparedPotions()
 {
  if(replayPath!=null){ReplayPotionDecisions();return;}
   if(NativeBranchSearch.Switched&&NativeBranchSearch.Forced<NativeBranchSearch.Plan!.Moves.Length)return;
  var combat=CombatManager.Instance.DebugOnlyGetState()!;
  if(ThirdPolicyActive&&ThirdDefencePotion(combat))return;
  if(((MegaCrit.Sts2.Core.Rooms.CombatRoom)run!.CurrentRoom!).Encounter.RoomType==MegaCrit.Sts2.Core.Rooms.RoomType.Monster){
   // Versioned policy only: preserve first-act baseline; spend a real Fire Potion
   // on a currently visible low-HP second-act enemy. Actual effect stays native.
   if(NativeNaturalBoss.Act2&&run.CurrentActIndex>0){
    var fire=player!.Potions.FirstOrDefault(p=>p.Id.ToString()=="POTION.FIRE_POTION");
    var enemy=combat.Enemies.Where(e=>e.CurrentHp+e.Block<=20).OrderBy(e=>e.CurrentHp+e.Block).FirstOrDefault();
    if(fire!=null&&enemy!=null&&fire.IsValidTarget(enemy)&&fire.PassesCustomUsabilityCheck)UsePotionC(fire,enemy);
   }
   return;
  }
  var potion=player!.Potions.FirstOrDefault(p=>p.Id.ToString() is "POTION.FIRE_POTION" or "POTION.BLOCK_POTION" or "POTION.STRENGTH_POTION" or "POTION.DEXTERITY_POTION" or "POTION.EXPLOSIVE_POTION" || NativeAct2Policy.Active&&p.Id.ToString()=="POTION.EXPLOSIVE_AMPOULE");
  if(potion==null)return;
  var targets=new MegaCrit.Sts2.Core.Entities.Creatures.Creature?[]{null}.Concat(combat.Creatures.Cast<MegaCrit.Sts2.Core.Entities.Creatures.Creature?>()).Where(t=>potion.IsValidTarget(t)).OrderBy(t=>t!=null&&combat.Enemies.Contains(t)?t.CurrentHp:10000).ToArray();
  if(targets.Length>0&&potion.PassesCustomUsabilityCheck)UsePotionC(potion,targets[0]);
 }
}
