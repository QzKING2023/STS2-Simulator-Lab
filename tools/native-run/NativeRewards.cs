using System.Text.Json;
using System.Text.Json.Serialization;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using SimulatorLab.Core;

namespace OfflineSearchHarness;

// Internal decisions over real native objects. A selection never applies a reward itself.
internal static class NativeRewards
{
    // Narrow native pickup contracts. Later combat hooks still execute in game code.
    internal static Type? MajorRelicType(string model)=>model switch{
        "RELIC.MEAL_TICKET"=>typeof(MegaCrit.Sts2.Core.Models.Relics.MealTicket),
        "RELIC.ORICHALCUM"=>typeof(MegaCrit.Sts2.Core.Models.Relics.Orichalcum),
        _=>null};
    internal sealed record Option(string Handle,string Kind,object? Reference,string? Model=null,int? Amount=null,int? Upgrade=null);
    internal sealed record Answer(string Request,string Context,string Stamp,string Handle);
    internal sealed class Operation(string id,string parent,string kind,Option[]? options)
    {
        internal readonly string Id=id,Parent=parent,Kind=kind,Root=NativeEvidence.Root,Context=NativeEvidence.Context;
        internal readonly Option[]? Options=options;
        internal string? Handle;
        internal object? Selected;
    }
    static readonly List<Operation> stack=[];
    internal static bool InSession=>stack.Count>0;
    internal static Operation CurrentOperation=>stack.Last();
    static readonly HashSet<string> consumed=[];
    static readonly JsonSerializerOptions strict=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    internal static readonly List<object> Transcript=[];
    internal static readonly Queue<int> Planned=[];
    internal static readonly HashSet<CardModel> AcquiredCards=new(ReferenceEqualityComparer.Instance);
    static JsonElement[] replay=[];static int replayIndex,number;
    internal static string Fault="";
    static int rootCount;static string countedRoot="";
    static readonly AsyncLocal<CardReward?> currentCard=new();
    static readonly Dictionary<CardReward,(Operation Op,CardModel? Card,object[] SourceCards,object[] SourceAlternatives)> pendingCards=[];
    // A completion is published only after the actual native OnSelect task and its menu result.
    // Keying by the real reward object keeps nested offers from overwriting another item's result.
    internal static readonly Dictionary<CardReward,CardModel?> completedCards=[];
    internal static object CardFact(CardModel card)=>NativeNaturalBoss.CardRecords?(object)new{reference=NativeIdentity.Ref(card),model=card.Id.ToString(),upgrade=card.CurrentUpgradeLevel,native=card.ToSerializable(),runFloor=card.Owner.RunState.TotalFloor}:new{reference=NativeIdentity.Ref(card),model=card.Id.ToString(),upgrade=card.CurrentUpgradeLevel};
    internal static CardReward? EnterCard(CardReward card){var previous=currentCard.Value;currentCard.Value=card;NativeRewardClosure.Enter(card);return previous;}
    internal static void LeaveCard(CardReward? previous){currentCard.Value=previous;NativeRewardClosure.Leave(previous);}
    internal static void LoadReplay(string path)=>replay=JsonDocument.Parse(File.ReadAllText(path)).RootElement.EnumerateArray().Where(x=>!NativeBranchSearch.Active||int.Parse(x.GetProperty("request").GetProperty("root").GetString()!.AsSpan(1))<=NativeBranchSearch.Plan!.PrefixCount).Select(x=>x.Clone()).ToArray();
    internal static void AssertConsumed(){if(NativeRewardClosure.Pending||stack.Count!=0||pendingCards.Count!=0||completedCards.Count!=0||Planned.Count!=0||replayIndex!=replay.Length)throw new NativeActionRejectedException("Unconsumed reward decisions/operations");}
    internal static object Inventory(MegaCrit.Sts2.Core.Entities.Players.Player player)=>new{capacity=player.MaxPotionCount,canUseOrRemove=player.CanUseOrRemovePotions,slots=player.PotionSlots.Select((p,i)=>new{index=i,potion=NativeIdentity.Ref(p),model=p?.Id.ToString(),queued=p?.IsQueued}).ToArray()};
    internal static Operation Begin(string kind,object? subject=null,Option[]? options=null,int preferred=0)
    {
        if(stack.Count>=16)throw new TimeoutException("Reward operation depth budget");
        if(countedRoot!=NativeEvidence.Root){countedRoot=NativeEvidence.Root;rootCount=0;}
        if(++rootCount>128)throw new TimeoutException("Reward operation count budget");
        string id="r"+(++number),stamp=Program.StateStamp();
        var op=new Operation(id,stack.Count==0?NativeEvidence.Root:stack[^1].Id,kind,options);
        Program.Observe("n3c-open",new{id,parent=op.Parent,kind,subject=NativeIdentity.Ref(subject),options,stamp});stack.Add(op);
        if(options!=null){
            if(options.Length==0)throw new NativeActionRejectedException("Empty reward decision candidates");
            var request=new{id,parent=op.Parent,root=op.Root,context=op.Context,kind,options};
            NativePolicy.PublicRequests.Add(new{kind,options=options.Select(x=>new{x.Handle,x.Kind,x.Model,x.Amount,x.Upgrade}).ToArray(),information="player-visible-native-reward-options"});
            Answer answer;
            if(NativeChoices.Replaying){
                if(replayIndex>=replay.Length)throw new NativeActionRejectedException("Missing reward choice answer");
                var frozen=replay[replayIndex++];
                if(!NativeDecisionTicket.SameActionJson(frozen.GetProperty("request").GetRawText(),JsonSerializer.Serialize(request,Program.Json)))throw new NativeActionRejectedException("Reward request changed");
                answer=frozen.GetProperty("answer").Deserialize<Answer>(strict)??throw new NativeActionRejectedException("Null reward answer");
            }else{
                int index=Planned.Count>0?Planned.Dequeue():NativeSolverRun.Choose(options,preferred);
                answer=new(id,op.Context,stamp,index>=0&&index<options.Length?options[index].Handle:"invalid");
                answer=Fault switch{"missing"=>throw new NativeActionRejectedException("Missing reward answer control"),"stale"=>answer with{Stamp="stale"},"cross-set"=>answer with{Request="previous-set"},"invalid"=>answer with{Handle="invalid"},_=>answer};
                if(Fault=="extra")_=JsonSerializer.Deserialize<Answer>(JsonSerializer.Serialize(answer).TrimEnd('}')+",\"extra\":true}",strict);
            }
            if(answer.Request!=id||answer.Context!=op.Context||answer.Stamp!=Program.StateStamp()||!consumed.Add(id)||!options.Any(x=>x.Handle==answer.Handle))throw new NativeActionRejectedException("Invalid/stale/reused reward answer");
            var ticket=new NativeDecisionTicket(op,number,stamp,JsonSerializer.Serialize(answer));ticket.Consume(op,number,Program.StateStamp(),JsonSerializer.Serialize(answer));
            if(Fault=="duplicate")ticket.Consume(op,number,Program.StateStamp(),JsonSerializer.Serialize(answer));
            op.Handle=answer.Handle;op.Selected=options.Single(x=>x.Handle==answer.Handle).Reference;
            Transcript.Add(new{request,answer});
            Program.Observe("n3c-answer",new{id,handle=op.Handle,selected=op.Selected});
        }
        Program.Observe("n3c-start",new{id});return op;
    }
    internal static void End(Operation op,object? result=null)
    {
        if(stack.Count==0||!ReferenceEquals(stack[^1],op)||op.Root!=NativeEvidence.Root||op.Context!=NativeEvidence.Context)throw new NativeDataException("Reward operation owner/order changed");
        Program.Observe("n3c-return",new{id=op.Id,result});stack.RemoveAt(stack.Count-1);
    }
    internal static Operation? LastRoot;
    internal static void Boundary(Operation root){if(stack.Count!=0)throw new NativeDataException("Reward children incomplete");LastRoot=root;Program.Observe("n3c-boundary",new{id=root.Id});}
    static CardModel SpecialCard(SpecialCardReward reward)=>(CardModel)(HarmonyLib.AccessTools.Field(typeof(SpecialCardReward),"_card").GetValue(reward)??throw new NativeDataException("Missing native special reward card"));
    static object SetOption(Reward x,int index)=>x is SpecialCardReward special?(object)new{index,reference=NativeIdentity.Ref(x),kind=x.GetType().Name,amount=(int?)null,model=SpecialCard(special).Id.ToString(),content=NativeIdentity.Ref(SpecialCard(special)),card=CardFact(SpecialCard(special))}:new{index,reference=NativeIdentity.Ref(x),kind=x.GetType().Name,amount=x is GoldReward g?(int?)g.Amount:null,model=x is PotionReward p?p.Potion?.Id.ToString():x is RelicReward r?r.Relic?.Id.ToString():null,content=NativeIdentity.Ref(x is PotionReward pp?(object?)pp.Potion:x is RelicReward rr?rr.Relic:null)};
    internal static async Task Collect(RewardsSet set)
    {
        var parent=Begin("reward-set",set);
        Program.Observe("n3c-set",new{id=parent.Id,set=set.Id,reference=NativeIdentity.Ref(set),disallowSkip=set.DisallowSkipping,options=set.Rewards.Select(SetOption).ToArray()});
        if(set.Rewards.Any(x=>x is not (GoldReward or CardReward or PotionReward or RelicReward or SpecialCardReward)))throw new NotSupportedException("4C native reward type outside declared collection profile");
        var manager=RunManager.Instance;Reward? failed=null;int decisions=0;bool skipped=false;
        while(!manager.RewardsSetSynchronizer.IsRewardsSetCompleted(set)){
            if(++decisions>32)throw new TimeoutException("Reward set decision budget");
            var remaining=set.Rewards.Where(x=>!x.SuccessfullySelected).ToArray();
            var potions=set.Player.HasOpenPotionSlots||!set.Player.CanUseOrRemovePotions?[]:set.Player.Potions.ToArray();
            var native=new List<object?>();var opts=new List<Option>();
            foreach(var reward in remaining){native.Add(reward);opts.Add(new("o"+opts.Count,reward.GetType().Name,NativeIdentity.Ref(reward),reward is PotionReward p?p.Potion?.Id.ToString():reward is RelicReward r?r.Relic?.Id.ToString():reward is SpecialCardReward sr?SpecialCard(sr).Id.ToString():null,reward is GoldReward g?g.Amount:null));}
            foreach(var potion in potions){native.Add(potion);opts.Add(new("o"+opts.Count,"DiscardPotion",NativeIdentity.Ref(potion),potion.Id.ToString()));}
            if(!set.DisallowSkipping){native.Add(null);opts.Add(new("o"+opts.Count,"SkipRewards",null));}
            int prefer=failed!=null?(potions.Length>0?remaining.Length:native.Count-1):0;
            var take=Begin("reward-item",set,opts.ToArray(),prefer);int index=opts.FindIndex(x=>x.Handle==take.Handle);var selected=native[index];
            bool success;
            if(selected==null){if(set.DisallowSkipping)throw new NativeActionRejectedException("Native rewards prohibit skip");manager.RewardsSetSynchronizer.SkipLocalRewardsSet();skipped=true;success=true;}
            else if(selected is PotionModel discard){if(!set.Player.CanUseOrRemovePotions)throw new NativeActionRejectedException("Potion discard disabled");await PotionCmd.Discard(discard);failed=null;success=true;}
            else{
                var reward=(Reward)selected;
                if(reward is not (GoldReward or CardReward or PotionReward or RelicReward or SpecialCardReward))throw new NotSupportedException("Native reward type "+reward.GetType().Name);
                NativeRewardClosure.SelectingPotion.Value=reward as PotionReward;
                NativeRelicPickup.Pickup? pickup=NativeMajor.Active&&reward is RelicReward majorRelic?NativeRelicPickup.Begin(majorRelic,take.Id):null;
                try{success=await manager.RewardsSetSynchronizer.SelectLocalReward(reward);}finally{NativeRewardClosure.SelectingPotion.Value=null;}
                if(pickup!=null)NativeRelicPickup.Complete(pickup,success);
                if(NativeRewardProbe.Scenario=="reward-task-fault")throw new InvalidOperationException("labelled fault after actual native reward selection");
                if(!NativeRewardClosure.ProcureActive&&success&&reward is PotionReward p&&!set.Player.Potions.Contains(p.Potion!))throw new NotSupportedException("Native potion procurement hook changed/declined identity");
                if(success&&reward is RelicReward r&&!set.Player.Relics.Contains(r.ClaimedRelic!))throw new NativeDataException("Native relic completion missing inventory instance");
                failed=success?null:reward;
            }
            object? acquired=selected is PotionReward pr?(NativeRewardClosure.ProcureActive?NativeRewardClosure.ActualPotion(pr):pr.ClaimedPotion):selected is RelicReward rr?rr.ClaimedRelic:selected is SpecialCardReward sc?SpecialCard(sc):null;
            if(selected is CardReward card){
                if(!completedCards.Remove(card,out var actual))throw new NativeDataException("Missing actual completed card acquisition");
                acquired=actual;
            }
            if(selected is SpecialCardReward special){
                var actual=SpecialCard(special);if(!set.Player.Deck.Cards.Contains(actual))throw new NotSupportedException("Special card native addition did not retain offered instance");
                End(take,new{success,skipped,selected=NativeIdentity.Ref(selected),acquired=NativeIdentity.Ref(actual),acquiredCard=CardFact(actual)});continue;
            }
            if(NativeRewardClosure.Active)End(take,new{success,skipped,selected=NativeIdentity.Ref(selected),acquired=NativeIdentity.Ref(acquired),claimedPotion=NativeIdentity.Ref((selected as PotionReward)?.ClaimedPotion)});else End(take,new{success,skipped,selected=NativeIdentity.Ref(selected),acquired=NativeIdentity.Ref(acquired)});
        }
        Program.Observe("n3c-set-completed",new{id=parent.Id,set=set.Id,skipped,allTaken=set.AllRewardsSuccessfullySelected});End(parent,new{complete=true});
    }
    internal static CardRewardSelection CardChoice(IReadOnlyList<CardCreationResult> cards,IReadOnlyList<CardRewardAlternative> alternatives)
    {
        var reward=currentCard.Value??throw new NativeDataException("Unscoped native card reward");
        if(NativeRewardClosure.Active)return NativeRewardClosure.Choose(reward,cards,alternatives);
        if(pendingCards.ContainsKey(reward))throw new NotSupportedException("Additional native card selection before prior acquisition boundary");
        var opts=cards.Select((x,i)=>new Option("o"+i,"Card",NativeIdentity.Ref(x.Card),x.Card.Id.ToString(),Upgrade:x.Card.CurrentUpgradeLevel)).Concat(alternatives.Select((a,i)=>new Option("o"+(cards.Count+i),"Alternative",NativeIdentity.Ref(a),a.OptionId))).ToArray();
        var op=Begin("card-menu",reward,opts,NativeAct2Policy.Active?NativeAct2Policy.RewardIndex(cards.Select(x=>x.Card).ToArray(),alternatives):NativePreparation.Active?NativePreparation.RewardIndex(cards.Select(x=>x.Card).ToArray()):0);int index=Array.FindIndex(opts,x=>x.Handle==op.Handle);
        CardModel? card=index<cards.Count?cards[index].Card:null;pendingCards.Add(reward,(op,card,cards.Select(x=>CardFact(x.Card)).ToArray(),alternatives.Select(a=>(object)new{reference=NativeIdentity.Ref(a),option=a.OptionId,afterSelected=a.AfterSelected.ToString()}).ToArray()));
        return new(){card=card,alternative=index>=cards.Count&&index<cards.Count+alternatives.Count?alternatives[index-cards.Count]:null};
    }
    internal static async Task<bool> CardAfter(Task<bool> task,CardReward reward)
    {
        bool result=await task;
        if(NativeRewardClosure.Active)return NativeRewardClosure.Complete(reward,result);
        if(!pendingCards.Remove(reward,out var pending))throw new NativeDataException("Missing card menu operation");
        if(pending.Card!=null&&!reward.Player.Deck.Cards.Contains(pending.Card))throw new NotSupportedException("Native card replacement/declined acquisition requires explicit identity adapter");
        if(pending.Card!=null)AcquiredCards.Add(pending.Card);
        End(pending.Op,new{success=result,acquired=NativeIdentity.Ref(pending.Card),sourceCards=pending.SourceCards,sourceAlternatives=pending.SourceAlternatives,acquiredCard=pending.Card==null?null:CardFact(pending.Card),identityMode="same-instance"});
        if(!completedCards.TryAdd(reward,pending.Card))throw new NativeDataException("Duplicate native card completion");return result;
    }
}
