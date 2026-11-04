using System.Text.Json;
using CombatSolver;
using CombatSolver.Engine.Common;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using System.Text.Json.Nodes;

namespace OfflineSearchHarness;

internal static partial class EleanorV040Probe
{
    public static object RunRewardBoundary(CombatState combat,MainLoopContext loop,string output,HarnessOptions options)
    {
        string caseId=Environment.GetEnvironmentVariable("SIMULATORLAB_REWARD_CASE")!;
        var parts=caseId.Split('-');string name=parts[0];bool upgraded=parts[1]=="1";int requested=int.Parse(parts[2]);string layout=parts[3];
        var player=LocalContext.GetMe(combat)!;var ps=player.PlayerCombatState!;
        VanillaGameActionProbe.InstallGodotClockShim();
        var setup=new List<object>();
        void Move(CardModel c,PileType p){loop.RunUntilCompleted(CardPileCmd.Add(c,p),TimeSpan.FromSeconds(15),"Reward API fixture movement");setup.Add(new{card=c.Id.ToString(),pile=p.ToString()});}
        foreach(var c in ps.Hand.Cards.Concat(ps.DrawPile.Cards).Concat(ps.DiscardPile.Cards).ToArray())Move(c,PileType.Exhaust);
        CardModel Add(string type,PileType p,bool up=false)
        {
            var canonical=ModelDb.All.OfType<CardModel>().Single(c=>c.GetType().Name==type);
            var c=combat.CreateCard(canonical,player);Move(c,p);if(up)CardCmd.Upgrade(c);return c;
        }
        var card=Add(name,PileType.Hand,upgraded);
        if(layout is not "empty" and not "none")
        {
            if(name=="ReturnFlow")
            {
                if(layout!="discard") {Add("ManaStrike",PileType.Draw);if(layout!="single")Add("ManaGather",PileType.Draw);}
                if(layout!="draw"&&layout!="single"){Add("ManaShield",PileType.Discard);Add("ManaStrike",PileType.Discard);}
                if(layout is "duplicates" or "identity")Add("ManaStrike",PileType.Draw);
                if(layout=="identity")Add("ManaStrike",PileType.Draw).EnergyCost.AddThisCombat(1);
            }
            else if(name=="ManaConversionPrinciple")
            {Add("GuidingPower",PileType.Hand);if(layout!="eternal"){Add("ManaStrike",PileType.Hand);if(layout!="single")Add("ManaStrike",PileType.Hand,layout!="identity");}if(layout=="identity")Add("ManaEssence",PileType.Hand,upgraded);}
            else {Add("ManaStrike",layout=="shuffle"?PileType.Discard:PileType.Draw);}
        }
        if(layout=="full")while(ps.Hand.Cards.Count<10)Add("ManaGather",PileType.Hand);
        if(layout=="shield")
        {
            var power=card.GetType().Assembly.GetType("MyMod.MyModCode.Powers.ManaToShieldPower",true)!;
            loop.RunUntilCompleted(InvokeNativePowerApply(power,player.Creature,3),TimeSpan.FromSeconds(15),"Native ManaToShield fixture");
        }
        if(layout=="nodraw")
            loop.RunUntilCompleted(InvokeNativePowerApply(typeof(MegaCrit.Sts2.Core.Models.Powers.NoDrawPower),player.Creature,1),TimeSpan.FromSeconds(15),"Native no-draw fixture");
        if(layout=="complete")
        {
            foreach(var relic in player.Relics.ToArray())loop.RunUntilCompleted(RelicCmd.Remove(relic),TimeSpan.FromSeconds(15),"Native relic fixture removal");
            var orb=ModelDb.All.OfType<RelicModel>().Single(r=>r.GetType().Name=="CompleteCrystalOrb").ToMutable();
            loop.RunUntilCompleted(RelicCmd.Obtain(orb,player),TimeSpan.FromSeconds(15),"Native complete-orb fixture obtain");
            _stateType!.GetMethod("SetCompleteOrb")!.Invoke(GetLiveManaState(player),[true]);
        }
        if(layout is "mentor" or "reverse" or "complete")
        {
            var runtimeType=card.GetType().Assembly.GetType("MyMod.MyModCode.Combat.ManaRuntime",true)!;
            var runtime=runtimeType.GetMethod("For")!.Invoke(null,[player]);var change=runtimeType.GetMethod("ChangeForm")!;
            loop.RunUntilCompleted((Task)change.Invoke(runtime,[new ThrowingPlayerChoiceContext(),Enum.Parse(change.GetParameters()[1].ParameterType,layout=="reverse"?"Reverse":"Mentor")])!,TimeSpan.FromSeconds(15),"Native form fixture");
        }
        object mana=GetLiveManaState(player);int initial=CaptureState(_stateType!,mana).Mana;
        if(requested>initial)_stateType!.GetMethod("GainMana")!.Invoke(mana,[requested-initial]);
        else if(!(bool)_stateType!.GetMethod("TryPayMana")!.Invoke(mana,[initial-requested])!)throw new InvalidOperationException("Fixture mana payment failed.");
        int energy=ps.Energy;int deck=player.Deck.Cards.Count;int handBefore=ps.Hand.Cards.Count;int blockBefore=player.Creature.Block;
        var permanentDeck=player.Deck.Cards.ToArray();
        var permanentKeys=permanentDeck.Select(CardChoiceSupport.ChoiceCardKey).ToArray();
        string permanentSerialized=JsonSerializer.Serialize(permanentDeck.Select(c=>c.ToSerializable()).ToArray());
        bool DeckUnchanged()=>player.Deck.Cards.SequenceEqual(permanentDeck,ReferenceEqualityComparer.Instance)
            && player.Deck.Cards.Select(CardChoiceSupport.ChoiceCardKey).SequenceEqual(permanentKeys)
            && permanentSerialized==JsonSerializer.Serialize(player.Deck.Cards.Select(c=>c.ToSerializable()).ToArray());
        var oldEssence=ps.Hand.Cards.SingleOrDefault(c=>c.GetType().Name=="ManaEssence"&& !ReferenceEquals(c,card));
        CardModel? selectedIdentity=null;
        object? selectedScope=null;
        object? identityEvidence=null;
        int generationStart=CombatManager.Instance.History.Entries.OfType<MegaCrit.Sts2.Core.Combat.History.Entries.CardGeneratedEntry>().Count();
        var action=BuildRewardAction(combat,card,layout=="variant"?1:0);
        var actionAgain=BuildRewardAction(combat,card,layout=="variant"?1:0);
        if(action.GetRawText()!=actionAgain.GetRawText())throw new InvalidOperationException("Selection construction is not reproducible.");
        if(layout=="identity")
        {
            var a=IdentityAction(combat,card,action,0);var b=IdentityAction(combat,card,action,1);
            identityEvidence=VerifyRewardSiblingIdentity(combat,card,a,b);
            action=b;
            selectedIdentity=(name=="ReturnFlow"?ps.DrawPile.Cards:ps.Hand.Cards).Where(c=>c.GetType().Name=="ManaStrike"&&c.CurrentUpgradeLevel==0).Skip(1).First();
            selectedScope=selectedIdentity.CardScope;
        }
        object? solverEvidence=null;
        if(layout=="solver")
        {
            var before=CombatRootSnapshot.Capture(combat);
            var outcome=ModRuntime.RunSearchDetailed(combat,options,loop,null);
            var after=CombatRootSnapshot.Capture(combat);
            bool found=JsonSerializer.SerializeToElement(outcome.RouteActions,ActionJson).EnumerateArray().Select(ParseAction)
                .Any(a=>a.CardId==card.Id.Entry&&a.GetActionChoicesInExecutionOrder().Count>0);
            solverEvidence=new{foundChoices=found,route=outcome.RouteActions,outcome.Result.TotalExpandedNodes,boundary=outcome.Result.BoundaryReason.ToString(),outcome.TimeBoundaryObserved};
            if(!found||before.LiveStamp!=after.LiveStamp||before.ContinuationStamp!=after.ContinuationStamp)throw new InvalidOperationException("Bounded search failed to discover card choices or mutated native source.");
        }
        object Replay(JsonElement a,string sub)
        {
            string dir=Path.Combine(output,sub);Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,"route.json"),JsonSerializer.Serialize(new[]{a}));
            File.WriteAllText(Path.Combine(dir,"harness-result.json"),JsonSerializer.Serialize(new{eleanorMod=new{assemblySha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_DLL")!)))},scenario=new{CharacterId="MyMod",EncounterId=((CombatRoom)combat.RunState.CurrentRoom!).Encounter.Id.Entry,Seed=NativeModRunLoop.SourceSeed}}));
            return RunVictoryReplay(combat,loop,dir,Path.Combine(dir,"route.json"),allowBoundary:true);
        }
        var replays=new List<JsonElement>();var controls=new List<object>();
        var stale=JsonNode.Parse(action.GetRawText())!.AsObject();stale["labChoiceSource"]!["sequence"]=-1;
        var staleBefore=CombatRootSnapshot.Capture(combat);
        var staleReport=JsonSerializer.SerializeToElement(Replay(JsonSerializer.SerializeToElement(stale),"invalid-stale"));
        var staleAfter=CombatRootSnapshot.Capture(combat);
        bool staleRejected=staleReport.GetProperty("status").GetString()=="invalid-action"&&staleBefore.LiveStamp==staleAfter.LiveStamp&&staleBefore.ContinuationStamp==staleAfter.ContinuationStamp;
        controls.Add(new{kind="stale-choice-boundary",passed=staleRejected});if(!staleRejected)throw new InvalidOperationException("Stale choice source was accepted.");
        bool badOfferRejected=false;
        try{_=new RewardSelectionBridge([new("control","control",PileType.Hand,["wrong-option-key"],0,false)]).GetSelectedCards([card],1,1);}
        catch(InvalidOperationException){badOfferRejected=true;}
        controls.Add(new{kind="selector-offer-contract-control",passed=badOfferRejected,actualNativeFault=false});
        if(!badOfferRejected)throw new InvalidOperationException("Selector option mismatch was accepted.");
        var plan=ParseAction(action);
        if(plan.NestedChoices?.Count>0)
        {
            var first=plan.NestedChoices[0];
            var bads=new[]{plan with{NestedChoices=[]},plan with{NestedChoices=[first with{Cards=[first.Cards[0] with{StateKey="stale-state"}]}]},
                plan with{NestedChoices=plan.NestedChoices.Concat(new[]{first}).ToArray()},
                plan with{NestedChoices=[first with{SourceId="wrong-source"}]},plan with{NestedChoices=[first with{ContextId="wrong-context"}]},
                plan with{NestedChoices=[first with{SourcePile=PileType.Exhaust}]},
                plan with{NestedChoices=[first with{Cards=[first.Cards[0] with{SourceOccurrence=999}]}]}};
            int i=0;foreach(var bad in bads)
            {
                var before=CombatRootSnapshot.Capture(combat);var rejected=JsonSerializer.SerializeToElement(Replay(JsonSerializer.SerializeToElement(bad,ActionJson),"invalid-"+(++i)));
                var after=CombatRootSnapshot.Capture(combat);
                bool okay=rejected.GetProperty("status").GetString()=="invalid-action"&&before.LiveStamp==after.LiveStamp&&before.ContinuationStamp==after.ContinuationStamp;
                controls.Add(new{kind=i,passed=okay,status=rejected.GetProperty("status").GetString()});if(!okay)throw new InvalidOperationException("Invalid choice mutated native state or was accepted.");
            }
        }
        replays.Add(JsonSerializer.SerializeToElement(Replay(action,"play")));
        var afterMana=CaptureState(_stateType!,mana);
        int mentor=layout=="mentor"?1:layout=="complete"?2:0;
        int expectedMana=requested-(name=="ReturnFlow"&&requested>=1?1:0)+(name=="ManaEssence"?(upgraded?2:1):0)+mentor;
        bool exact=replays.All(r=>r.GetProperty("status").GetString()=="boundary")&&afterMana.Mana==expectedMana&&ps.Energy==energy-(name=="ManaEssence"?0:1)&&DeckUnchanged();
        if(name=="ReturnFlow")
        {
            int moved=requested==0||layout=="empty"?0:layout is "single" or "draw" or "discard"?1:2;
            exact &= ps.Hand.Cards.Count==Math.Min(10,handBefore-1+moved);
            exact &= card.GetKeywordsWithSources(KeywordSources.Local).Contains(CardKeyword.Retain)==upgraded;
            if(layout=="shield")exact &=player.Creature.Block-blockBefore==(requested>=1?3:0);
        }
        if(name=="ManaConversionPrinciple"&&layout!="empty")
        {
            var essence=ps.Hand.Cards.Single(c=>c.GetType().Name=="ManaEssence"&&!ReferenceEquals(c,oldEssence));
            exact &= essence.CurrentUpgradeLevel==(upgraded?1:0);
            if(layout=="identity")
            {
                var created=CombatManager.Instance.History.Entries.OfType<MegaCrit.Sts2.Core.Combat.History.Entries.CardGeneratedEntry>().Skip(generationStart).ToArray();
                bool provenance=created.Length==1&&ReferenceEquals(created[0].Card,essence)&&ReferenceEquals(created[0].Creator,player)
                    && !ReferenceEquals(essence,oldEssence)&&ps.Hand.Cards.Contains(oldEssence!)&&!ps.AllCards.Contains(selectedIdentity!)
                    && selectedIdentity!.HasBeenRemovedFromState&&ReferenceEquals(essence.Owner,player)&&essence.DeckVersion==null
                    && ReferenceEquals(essence.CardScope,selectedScope);
                exact &=provenance;
                identityEvidence=new{siblingProof=identityEvidence,physicalTargetOccurrence=1,generatedEntryIsNewEssence=provenance,
                    oldEssenceState=CardChoiceSupport.ChoiceCardKey(oldEssence!),newEssenceState=CardChoiceSupport.ChoiceCardKey(essence),targetRemoved=selectedIdentity!.HasBeenRemovedFromState,
                    replacementUsesSelectedScope=ReferenceEquals(essence.CardScope,selectedScope)};
            }
            replays.Add(JsonSerializer.SerializeToElement(Replay(BuildRewardAction(combat,essence),"essence")));
            exact &= ps.ExhaustPile.Cards.Contains(essence)&&CaptureState(_stateType!,mana).Mana==requested+(upgraded?2:1)+2*mentor;
            if(layout=="identity")exact &=ps.Hand.Cards.Contains(oldEssence!)&&!ps.ExhaustPile.Cards.Contains(oldEssence!);
        }
        if(layout=="identity"&&name=="ReturnFlow")exact &=ps.Hand.Cards.Contains(selectedIdentity!)&&ps.DrawPile.Cards.Count(c=>c.GetType().Name=="ManaStrike")==2;
        if(name=="ManaEssence")exact &=ps.ExhaustPile.Cards.Contains(card);
        exact &= replays.All(r=>r.GetProperty("status").GetString()=="boundary")&&DeckUnchanged();
        var report=new{schema="reward-card-boundary/v2",status=exact?"passed":"failed",caseId,fixture="Native CreateCard/Add/Upgrade/ManaState API setup; no reward-acquisition claim",setup,action,solverEvidence,identityEvidence,controls,replays,requested,expectedMana,afterMana,energyBefore=energy,energyAfter=ps.Energy,permanentDeckUnchanged=DeckUnchanged(),permanentDeckBefore=permanentKeys,permanentDeckAfter=player.Deck.Cards.Select(CardChoiceSupport.ChoiceCardKey).ToArray(),permanentDeckSerializedBefore=permanentSerialized,permanentDeckSerializedAfter=JsonSerializer.Serialize(player.Deck.Cards.Select(c=>c.ToSerializable()).ToArray()),permanentDeckReferencesUnchanged=player.Deck.Cards.SequenceEqual(permanentDeck,ReferenceEqualityComparer.Instance),guards=CaptureOfflineWriteEvidence()};
        File.WriteAllText(Path.Combine(output,"reward-boundary.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        if(!exact)throw new InvalidOperationException("Reward card boundary contract/differential failed.");return report;
    }
}
