using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
namespace OfflineSearchHarness;

internal static partial class Program
{
 // Current native intents/hand only. Scores are a bounded heuristic, not damage
 // execution or a claim of exact effect prediction. No future draw/RNG access.
 static bool ThirdPolicyActive=>NativeNaturalBoss.ThirdRoute&&run!.CurrentActIndex==2;
 static int ThirdPolicyRank(CardModel card,Creature? target,CombatState combat,int incoming,int deficit)
 {
  var self=player!.Creature;string id=card.Id.ToString();
  // Survival guard precedes the kill estimate: native Hemokinesis pays HP first.
  if(id=="CARD.HEMOKINESIS"&&self.CurrentHp<=card.DynamicVars.HpLoss.BaseValue)return -100;
  int cost=Math.Max(1,card.EnergyCost.GetWithModifiers(CostModifiers.All));
  if(combat.Enemies.Any(e=>e.Monster is MegaCrit.Sts2.Core.Models.Monsters.TestSubject))return TestSubjectRank(card,target,combat,incoming,deficit,cost);
  if(card.Type==CardType.Attack&&target!=null&&combat.Enemies.Contains(target)&&card.DynamicVars.TryGetValue("Damage",out var damage)){
   decimal estimate=damage.BaseValue+(self.Powers.FirstOrDefault(p=>p.Id.ToString()=="POWER.STRENGTH_POWER")?.Amount??0);
   if(self.Powers.Any(p=>p.Id.ToString()=="POWER.WEAK_POWER"))estimate*=0.75m;
   if(target.Powers.Any(p=>p.Id.ToString()=="POWER.VULNERABLE_POWER"))estimate*=1.5m;
   if(estimate>=target.CurrentHp+target.Block){
    bool minion=target.Powers.Any(p=>p.Id.ToString()=="POWER.MINION_POWER");
    bool lastPrimary=!minion&&combat.Enemies.Where(e=>e!=target).All(e=>e.Powers.Any(p=>p.Id.ToString()=="POWER.MINION_POWER"));
    if(lastPrimary||target.Monster?.IntendsToAttack==true)return (lastPrimary?4000:2600)+(int)estimate;
   }
  }
  if(card.DynamicVars.TryGetValue("Block",out var block)){
   decimal estimate=block.BaseValue+(self.Powers.FirstOrDefault(p=>p.Id.ToString()=="POWER.DEXTERITY_POWER")?.Amount??0);
   if(self.Powers.Any(p=>p.Id.ToString()=="POWER.FRAIL_POWER"))estimate*=0.75m;
   if(deficit>0)return (deficit>=self.CurrentHp?2300:800)+10*(int)Math.Min(deficit,Math.Max(0,estimate))/cost+(id=="CARD.ARMAMENTS"?120:0);
   if(card.Type!=CardType.Attack)return id=="CARD.SHRUG_IT_OFF"?500:id=="CARD.ARMAMENTS"?350:-100;
  }
  if(id=="CARD.DARK_EMBRACE"||id=="CARD.FEEL_NO_PAIN")return 120;
  if(id=="CARD.INFLAME")return 750;
  if(card.Type==CardType.Power)return 650;
  if(id=="CARD.DISARM"&&incoming>0)return 950;
  if(id=="CARD.OFFERING")return self.CurrentHp>8?740:-100;
  if(id=="CARD.BATTLE_TRANCE")return 740;
  if(id=="CARD.BASH")return target!=null&&!target.Powers.Any(p=>p.Id.ToString()=="POWER.VULNERABLE_POWER")?560:300;
  if(id=="CARD.HEMOKINESIS"&&self.CurrentHp<=4)return -100;
  return card.Type==CardType.Attack?400+NativePreparation.CardRank(card)+(card.DynamicVars.TryGetValue("Damage",out var value)?10*(int)value.BaseValue/cost:0)+(target?.Monster?.IntendsToAttack==true?100:0):200;
 }
 // Finite boss policy over current native hand, visible intents and powers.
 // It ranks legal actions; no action effects or future draw order are simulated.
 static int TestSubjectRank(CardModel card,Creature? target,CombatState combat,int incoming,int deficit,int cost)
 {
  var self=player!.Creature;string id=card.Id.ToString();var boss=combat.Enemies.First(e=>e.Monster is MegaCrit.Sts2.Core.Models.Monsters.TestSubject);
  bool reviving=boss.IsDead&&boss.Powers.Any(p=>p.Id.ToString()=="POWER.ADAPTABLE_POWER");
  bool intangible=boss.Powers.Any(p=>p.Id.ToString()=="POWER.INTANGIBLE_POWER");
  decimal hpLoss=card.DynamicVars.TryGetValue("HpLoss",out var loss)?loss.BaseValue:0;
  if(hpLoss>0&&(self.CurrentHp<=hpLoss+3||intangible||reviving))return -100;
  if(id=="CARD.OFFERING"&&self.CurrentHp<=10)return -100;
  if(id=="CARD.ANGER"&&(intangible||!boss.Powers.Any(p=>p.Id.ToString()=="POWER.NEMESIS_POWER")))return -100;
  if(id=="CARD.HEMOKINESIS"&&!intangible&&!reviving&&self.CurrentHp>10&&boss.Powers.Any(p=>p.Id.ToString()=="POWER.PAINFUL_STABS_POWER"))return 3400;
  if(id=="CARD.STAMPEDE"){
   decimal juggernaut=self.Powers.FirstOrDefault(p=>p.Id.ToString()=="POWER.JUGGERNAUT_POWER")?.Amount??0;
   bool visibleBlockKnockout=!intangible&&boss.CurrentHp>0&&boss.CurrentHp<=juggernaut*2&&player.PlayerCombatState!.Energy>=3&&player.PlayerCombatState.Hand.Cards.Any(c=>c.DynamicVars.ContainsKey("Block")&&c.EnergyCost.GetWithModifiers(CostModifiers.All)<=1);
   if(self.CurrentHp>10&&(reviving||visibleBlockKnockout))return 2400;
   return incoming>self.Block+10||self.CurrentHp<20?-100:700;
  }
  if(id=="CARD.INFERNO")return -100;
  if(id=="CARD.JUGGERNAUT")return self.CurrentHp>Math.Max(0,incoming-self.Block)+5?4000:500;
  if(id=="CARD.INFLAME"&&boss.Powers.Any(p=>p.Id.ToString()=="POWER.PAINFUL_STABS_POWER")&&(self.Powers.FirstOrDefault(p=>p.Id.ToString()=="POWER.STRENGTH_POWER")?.Amount??0)>=3)return 400;
  if(id=="CARD.INFLAME")return self.CurrentHp>Math.Max(0,incoming-self.Block)+5||reviving?3500:700;
  if(id=="CARD.DARK_EMBRACE"||id=="CARD.FEEL_NO_PAIN")return 100;
  if(!intangible&&!reviving&&card.Type==CardType.Attack&&card.EnergyCost.GetWithModifiers(CostModifiers.All)==0)return 3200;
  if(id=="CARD.FLAME_BARRIER"&&incoming>0)return 2600+incoming;
  if(id=="CARD.IMPERVIOUS"&&reviving)return -100;
  if(card.DynamicVars.TryGetValue("Block",out var block)){
   if(deficit>0)return 1500+(int)(100*Math.Min(deficit,block.BaseValue)/cost)+(id=="CARD.SHRUG_IT_OFF"?60:0);
   int score=card.Type==CardType.Attack?(intangible?300:1200):id=="CARD.SHRUG_IT_OFF"?900:id=="CARD.ARMAMENTS"?300:-100;
   return NativeJuggernautRanking.Adjust(score,deficit,card.Type!=CardType.Attack,block.BaseValue,
    self.Powers.FirstOrDefault(p=>p.Id.ToString()=="POWER.JUGGERNAUT_POWER")?.Amount??0,intangible,reviving,
    boss.IsAlive&&boss.Powers.Any(p=>p.Id.ToString()=="POWER.NEMESIS_POWER"));
  }
  if(id=="CARD.BATTLE_TRANCE"||id=="CARD.POMMEL_STRIKE")return 1100;
  if(reviving&&card.Type==CardType.Attack)return -100;
  if(id=="CARD.BASH")return intangible?100:target!=null&&!target.Powers.Any(p=>p.Id.ToString()=="POWER.VULNERABLE_POWER")?1000:400;
  return card.Type==CardType.Attack?(intangible?300:650)+NativePreparation.CardRank(card):500;
 }
 static void PrepareTestSubjectChoice(CardModel card)
 {
  if(NativeBranchSearch.Switched||!ThirdPolicyActive||NativeChoices.Replaying||card.Id.ToString()!="CARD.TRUE_GRIT"||card.CurrentUpgradeLevel==0)return;
  var combat=CombatManager.Instance.DebugOnlyGetState()!;
  if(!combat.Enemies.Any(e=>e.Monster is MegaCrit.Sts2.Core.Models.Monsters.TestSubject))return;
  var offered=player!.PlayerCombatState!.Hand.Cards.Where(c=>!ReferenceEquals(c,card)).ToArray();
  if(offered.Length<=1)return;
  int Rank(CardModel c)=>c.Id.ToString() is "CARD.INFERNO" or "CARD.DARK_EMBRACE" or "CARD.STAMPEDE"?-10:c.Type==CardType.Status?0:c.Type==CardType.Curse?1:c.Id.ToString()=="CARD.ANGER"?20:c.DynamicVars.ContainsKey("Block")?200:100;
  if(NativeChoices.Planned.Count!=0)throw new NativeDataException("Unconsumed boss choice plan");
  Plan(Enumerable.Range(0,offered.Length).OrderBy(i=>Rank(offered[i])).ThenBy(i=>i).First());
 }
 static bool ThirdDefencePotion(CombatState combat)
 {
  string before=StateStamp();var self=player!.Creature;
  var blood=player.Potions.FirstOrDefault(p=>p.Id.ToString()=="POTION.BLOOD_POTION");
  bool heal=blood!=null&&self.CurrentHp<self.MaxHp*0.6m&&blood.IsValidTarget(self)&&blood.PassesCustomUsabilityCheck;
  var iron=player.Potions.FirstOrDefault(p=>p.Id.ToString()=="POTION.HEART_OF_IRON");
  bool plate=iron!=null&&iron.IsValidTarget(self)&&iron.PassesCustomUsabilityCheck;
  var blast=player.Potions.FirstOrDefault(p=>p.Id.ToString()=="POTION.EXPLOSIVE_AMPOULE");
  var blastTarget=blast==null?null:new Creature?[]{null,self}.Concat(combat.Enemies).FirstOrDefault(t=>blast.IsValidTarget(t));
  bool explode=blast!=null&&combat.Enemies.Count(e=>e.IsAlive)>=2&&blast.IsValidTarget(blastTarget)&&blast.PassesCustomUsabilityCheck;
  var swift=player.Potions.FirstOrDefault(p=>p.Id.ToString()=="POTION.SWIFT_POTION");
  bool draw=swift!=null&&player.PlayerCombatState!.Energy>=2&&player.PlayerCombatState.Hand.Cards.Count<=5&&swift.IsValidTarget(self)&&swift.PassesCustomUsabilityCheck;
  var vulnerable=player.Potions.FirstOrDefault(p=>p.Id.ToString()=="POTION.VULNERABLE_POTION");
  var primary=combat.Enemies.Where(e=>e.IsAlive&&!e.Powers.Any(p=>p.Id.ToString()=="POWER.MINION_POWER")&&!e.Powers.Any(p=>p.Id.ToString() is "POWER.VULNERABLE_POWER" or "POWER.ARTIFACT_POWER")).OrderByDescending(e=>e.CurrentHp).FirstOrDefault();
  bool weaken=vulnerable!=null&&primary!=null&&primary.CurrentHp>=30&&vulnerable.IsValidTarget(primary)&&vulnerable.PassesCustomUsabilityCheck;
  var potion=player.Potions.FirstOrDefault(p=>p.Id.ToString()=="POTION.FORTIFIER");
  int incoming=combat.Enemies.Sum(e=>e.Monster?.NextMove.Intents.OfType<MegaCrit.Sts2.Core.MonsterMoves.Intents.AttackIntent>().Sum(i=>i.GetTotalDamage([self],e))??0);
  var brew=player.Potions.FirstOrDefault(p=>p.Id.ToString()=="POTION.GAMBLERS_BREW");
  var pcs=player.PlayerCombatState!;var hand=pcs.Hand.Cards.ToArray();
  int[] redraw=Enumerable.Range(0,hand.Length).Where(i=>!hand[i].DynamicVars.ContainsKey("Block")).ToArray();
  bool usableBlock=hand.Any(c=>c.DynamicVars.ContainsKey("Block")&&c.CanPlayTargeting(null));
  bool refresh=brew!=null&&pcs.Energy>0&&incoming-self.Block>=self.CurrentHp&&!usableBlock&&redraw.Length>0&&brew.IsValidTarget(self)&&brew.PassesCustomUsabilityCheck;
  bool use=potion!=null&&self.Block>0&&incoming>self.Block&&(self.Block>=10&&incoming-self.Block>6||incoming-self.Block>=self.CurrentHp)&&potion.IsValidTarget(self)&&potion.PassesCustomUsabilityCheck;
  var cure=player.Potions.FirstOrDefault(p=>p.Id.ToString()=="POTION.CURE_ALL");
  var subject=combat.Enemies.FirstOrDefault(e=>e.Monster is MegaCrit.Sts2.Core.Models.Monsters.TestSubject);
  bool emergencyCure=subject!=null&&cure!=null&&(!usableBlock&&(pcs.Energy<=2&&incoming-self.Block>=self.CurrentHp||subject.Powers.Any(p=>p.Id.ToString()=="POWER.NEMESIS_POWER")&&incoming-self.Block>self.MaxHp*0.25m&&hand.Length<=6))&&cure.IsValidTarget(self)&&cure.PassesCustomUsabilityCheck;
  if(StateStamp()!=before)throw new NativeDataException("Third potion policy query changed native state/RNG");
  if(emergencyCure){UsePotionC(cure!,self);return true;}
  if(heal){UsePotionC(blood!,self);return true;}
  if(plate){UsePotionC(iron!,self);return true;}
  if(explode){UsePotionC(blast!,blastTarget);return true;}
  if(draw){UsePotionC(swift!,self);return true;}
  if(weaken){UsePotionC(vulnerable!,primary);return true;}
  if(refresh){Plan(redraw);UsePotionC(brew!,self);return true;}
  if(!use)return false;UsePotionC(potion!,self);return true;
 }
}

// A ranking correction over observed inputs, not an effect simulator. Keep all
// existing positive scores and the preceding survival/defence gates intact.
// Nemesis marks the actual third form; earlier forms retain their old score.
internal static class NativeJuggernautRanking
{
 internal static int Adjust(int score,int deficit,bool pureBlock,decimal block,decimal juggernaut,bool intangible,bool reviving,bool thirdForm)
  =>thirdForm&&score<0&&deficit==0&&pureBlock&&block>0&&juggernaut>0&&!intangible&&!reviving?800:score;
}
