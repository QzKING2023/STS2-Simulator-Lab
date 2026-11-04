using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace OfflineSearchHarness;
internal static partial class Program
{
    static object? EventRng()=>run==null?null:(AccessTools.Field(RunManager.Instance.EventSynchronizer.GetType(),"_multiplayerOptionSelectionRng").GetValue(RunManager.Instance.EventSynchronizer) as Rng)?.ToSerializable();
    static void Plan(params int[] indices){if(!NativeChoices.Replaying)NativeChoices.Planned.Enqueue(indices);}
    static CardSelectorPrefs Prefs(int min=1,int? max=null,bool manual=false)=>new(new LocString("gameplay_ui","LAB_FIXTURE"),min,max??min){RequireManualConfirmation=manual};
    static void Contract(bool valid,string name,object? detail=null){Observe("fixture-assertion",new{name,valid,detail});if(!valid)throw new InvalidOperationException("Independent fixture contract: "+name);}
    static void Fixture()
    {
        string[] parts=scenario.Split(':');string name=parts[0];NativeChoices.Fault=parts.Length>1?parts[1]:"";
        var context=new ThrowingPlayerChoiceContext();var p=player!;
        string deckBefore=JsonSerializer.Serialize(p.Deck.Cards.Select(c=>c.ToSerializable()));var deckRefs=p.Deck.Cards.ToArray();
        Observe("fixture-setup-declared",new{name,entry="after natural Neow / first combat Play",changes="Native CreateCard/Add for combat fixtures; original permanent deck retained"});
        if(name.StartsWith("deck-")){
            var options=p.Deck.Cards.Where(c=>c.IsUpgradable).ToArray();Plan(1);
            Action("FixtureDeck",new{name},()=>{
                var selected=(AwaitCards(name=="deck-upgrade"?CardSelectCmd.FromDeckForUpgrade(p,Prefs()):CardSelectCmd.FromDeckGeneric(p,Prefs(),c=>c.IsUpgradable))).Single();
                Contract(ReferenceEquals(selected,options[1]),"nonzero permanent instance selected",new{selected=NativeIdentity.Ref(selected)});
                if(name=="deck-upgrade")CardCmd.Upgrade(selected);
            });
            Contract(p.Deck.Cards.SequenceEqual(deckRefs,ReferenceEqualityComparer.Instance),"permanent reference continuity");
            Contract(name!="deck-upgrade"||options[1].CurrentUpgradeLevel==1&&options[0].CurrentUpgradeLevel==0,"only specified permanent card upgraded");
        }else{
            var combat=CombatManager.Instance.DebugOnlyGetState()!;var pcs=p.PlayerCombatState!;
            void Move(CardModel c,PileType pile)=>Await(CardPileCmd.Add(c,pile),"Declared fixture card movement");
            CardModel Add(string type,PileType pile){var c=combat.CreateCard(ModelDb.All.OfType<CardModel>().Single(x=>x.GetType().Name==type),p);Move(c,pile);return c;}
            CardModel[] options=[];CardModel? effect=null;
            Action("FixtureSetup",new{name},()=>{
                foreach(var c in pcs.Hand.Cards.Concat(pcs.DrawPile.Cards).Concat(pcs.DiscardPile.Cards).ToArray())Move(c,PileType.Exhaust);
                if(name=="armaments")effect=Add("Armaments",PileType.Hand);
                if(name.StartsWith("headbutt"))effect=Add("Headbutt",PileType.Hand);
                int count=name is "empty" or "softlock" or "headbutt-empty"?0:name is "single" or "manual-single" or "headbutt-single"?1:3;
                options=Enumerable.Range(0,count).Select(_=>Add("StrikeIronclad",name.StartsWith("headbutt")?PileType.Discard:PileType.Hand)).ToArray();
            });
            int energy=pcs.Energy,block=p.Creature.Block;
            if(name is "armaments"||name.StartsWith("headbutt")){
                Plan(options.Length>1?1:0);
                if(options.Length<=1)NativeChoices.Planned.Clear();
                var target=name.StartsWith("headbutt")?combat.Enemies.First():p.Creature;int hp=target.CurrentHp;
                Action("FixturePlay",new{card=Id(effect!),name},()=>{
                    if(!effect!.TryManualPlay(name=="armaments"?null:target))throw new InvalidOperationException("Fixture native card play rejected");Await(RunManager.Instance.ActionExecutor.FinishedExecutingActions(),"Fixture native card action");
                });
                if(name=="armaments"){
                    Contract(p.Creature.Block-block==5&&pcs.Energy==energy-1,"Armaments independent 5 block / 1 energy");
                    Contract(options[1].CurrentUpgradeLevel==1&&options[0].CurrentUpgradeLevel==0,"Armaments exact duplicate upgraded");
                }else{
                    Contract(hp-target.CurrentHp==9,"Headbutt independent nine actual hp loss");
                    if(options.Length>0)Contract(ReferenceEquals(pcs.DrawPile.Cards.First(),options[options.Length>1?1:0]),"specified duplicate on draw top");
                }
            }else if(name=="sink"||name=="quota"){
                Action("FixtureTraceFailure",new{name},()=>{if(name=="sink")NativeEvidence.SinkFault=true;else NativeEvidence.Quota=1;Observe("sink-failure-trigger");});
            }else{
                int count=name=="multi"?2:1;
                if(name is not "empty" and not "single" and not "softlock" and not "baseline-first")Plan(name=="multi"?[1,0]:name is "manual-single" or "provider-first"?[0]:[1]);
                Action("FixtureSelect",new{name},()=>{
                    IEnumerable<CardModel> selected;
                    if(name=="baseline-first"){
                        selectorScope!.Dispose();
                        try{using(CardSelectCmd.UseSelector(new FirstNativeSelector()))selected=AwaitCards(CardSelectCmd.FromSimpleGrid(context,options,p,Prefs()));}
                        finally{selectorScope=CardSelectCmd.UseSelector(NativeChoices.Instance);}
                    }
                    else if(name.StartsWith("bundle")){
                        int reserve=NativeChoices.Reserved;
                        IReadOnlyList<IReadOnlyList<CardModel>> groups=name=="bundle-shared"?new IReadOnlyList<CardModel>[]{[options[0],options[1]],[options[0],options[2]]}:options.Select(c=>(IReadOnlyList<CardModel>)new[]{c}).ToArray();
                        selected=AwaitCards(CardSelectCmd.FromChooseABundleScreen(p,groups));
                        Contract(NativeChoices.Reserved-reserve==1,"Bundle native reserve exactly once");
                        if(name=="bundle-shared"){Contract(selected.SequenceEqual(groups[1],ReferenceEqualityComparer.Instance),"shared-first group identity retained");return;}
                    }
                    else if(name is "choose-card" or "softlock")selected=AwaitOne(CardSelectCmd.FromChooseACardScreen(context,options,p,false));
                    else if(name=="hand")selected=AwaitCards(CardSelectCmd.FromHand(context,p,Prefs(),null,options[0]));
                    else selected=AwaitCards(CardSelectCmd.FromSimpleGrid(context,options,p,Prefs(count,manual:name=="manual-single")));
                    var array=selected.ToArray();
                    Contract(array.Length==(name=="empty"?0:name=="multi"?2:1),"native cardinality");
                    if(options.Length>1)Contract(ReferenceEquals(array[0],options[name is "baseline-first" or "provider-first"?0:1]),"specified duplicate selected by reference");
                    if(name=="multi")Contract(ReferenceEquals(array[1],options[0]),"explicit multi order preserved");
                    if(name=="sequential"){
                        Plan(2);var again=AwaitCards(CardSelectCmd.FromSimpleGrid(context,options,p,Prefs())).Single();Contract(ReferenceEquals(again,options[2]),"second request consumed in order");
                    }
                });
                if(NativeChoices.Fault=="extra")Plan(0);
            }
            Contract(deckBefore==JsonSerializer.Serialize(p.Deck.Cards.Select(c=>c.ToSerializable()))&&p.Deck.Cards.SequenceEqual(deckRefs,ReferenceEqualityComparer.Instance),"full permanent deck content/upgrade/references unchanged");
        }
        Observe("fixture-contract",new{name,scenarioKind="api-fixture",valid=true});
    }
    static CardModel[] AwaitCards(Task<IEnumerable<CardModel>> task){Await(task,"Native API choice");return task.GetAwaiter().GetResult().ToArray();}
    static CardModel[] AwaitOne(Task<CardModel?> task){Await(task,"Native single choice");return task.GetAwaiter().GetResult() is {} c?[c]:[];}
    sealed class FirstNativeSelector : MegaCrit.Sts2.Core.TestSupport.ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options,int minSelect,int maxSelect)=>Task.FromResult(options.Take(minSelect));
        public MegaCrit.Sts2.Core.TestSupport.CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,IReadOnlyList<MegaCrit.Sts2.Core.Entities.CardRewardAlternatives.CardRewardAlternative> alternatives)=>throw new NotSupportedException("Baseline control is grid-only");
    }
}
