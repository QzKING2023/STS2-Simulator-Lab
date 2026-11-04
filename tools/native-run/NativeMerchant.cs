using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SimulatorLab.Core;
using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Multiplayer.Game;

namespace OfflineSearchHarness;
internal static class NativeMerchant
{
    internal static readonly HashSet<string> Fixtures=["merchant-card","merchant-potion","merchant-full","merchant-poor","merchant-outstock","merchant-remove","merchant-cancel","merchant-repeat"];
    internal static readonly HashSet<string> Faults=["merchant-stale","merchant-wrong-inventory","merchant-task-fault"];
    internal static bool IsScenario(string s)=>Fixtures.Contains(s)||Faults.Contains(s);
    internal static bool Active=>IsScenario(NativeLifecycle.Scenario)||NativeMerchantEconomy.Active||NativeThirdShop.Active;
    internal static MerchantInventory? Inventory;
    internal static bool Closed;
    internal sealed class RemovalScope(MerchantCardRemovalEntry entry){internal readonly MerchantCardRemovalEntry Entry=entry;internal int Marks,Hooks;}
    internal static RemovalScope? CurrentRemoval;
    internal static void Install()
    {
        NativeHost.Register(AccessTools.Method(typeof(OneOffSynchronizer),"DoLocalMerchantCardRemoval"),nameof(RemovalBefore),typeof(NativeMerchant),nameof(RemovalAfter));
        NativeHost.Register(AccessTools.Method(typeof(Hook),"AfterItemPurchased"),nameof(PurchasedBefore),typeof(NativeMerchant),nameof(PurchasedAfter));
    }
    static void RemovalBefore(){}
    static void RemovalAfter(ref Task<bool> __result){if(Active&&CurrentRemoval is {} scope)__result=FinishRemoval(__result,scope);}
    static async Task<bool> FinishRemoval(Task<bool> original,RemovalScope scope)
    {
        bool success=await original;
        if(success){
            if(!ReferenceEquals(CurrentRemoval,scope)||scope.Entry.Used||scope.Marks!=0)throw new NativeDataException("Duplicate/stale native removal completion");
            // Same position as the pinned UI delegation: after OneOff's effects,
            // before MerchantCardRemovalEntry awaits AfterItemPurchased.
            scope.Entry.SetUsed();scope.Marks++;
            Program.Observe("n3d-removal-marked",new{entry=NativeIdentity.Ref(scope.Entry),source="OneOff-task-return/native-SetUsed"});
        }
        return success;
    }
    static void PurchasedBefore(MerchantEntry __2,int __3)
    {
        NativeMerchantExpansion.HookBefore(__2);
        NativeShopLedger.HookBefore(__2,__3);
        if(!Active||CurrentRemoval is not {} scope)return;
        if(!ReferenceEquals(__2,scope.Entry)||scope.Marks!=1||!scope.Entry.Used||++scope.Hooks!=1)throw new NativeDataException("Native purchase hook observed wrong removal completion order");
        Program.Observe("n3d-removal-hook",new{entry=NativeIdentity.Ref(scope.Entry),used=scope.Entry.Used,source="Hook.AfterItemPurchased-prefix"});
    }
    static void PurchasedAfter(MerchantEntry __2,ref Task __result){NativeMerchantExpansion.HookAfter(__2,ref __result);NativeShopLedger.HookAfter(__2,ref __result);}
    internal static object? Snapshot()=>Inventory==null?null:new{reference=NativeIdentity.Ref(Inventory),room=NativeIdentity.Ref(Program.MerchantRoom),closed=Closed,removals=Inventory.Player.ExtraFields.CardShopRemovalsUsed,entries=Inventory.AllEntries.Select((e,i)=>Entry(e,i)).ToArray()};
    internal static object Entry(MerchantEntry e,int i){object? content=e is MerchantCardEntry c?c.CreationResult?.Card:e is MerchantPotionEntry p?p.Model:e is MerchantRelicEntry r?r.Model:null;var value=System.Text.Json.JsonSerializer.SerializeToElement(new{index=i,reference=NativeIdentity.Ref(e),kind=e.GetType().Name,cost=e.Cost,stock=e.IsStocked,content=NativeIdentity.Ref(content),model=(content as AbstractModel)?.Id.ToString(),upgrade=content is CardModel card?(int?)card.CurrentUpgradeLevel:null,used=e is MerchantCardRemovalEntry removal?(bool?)removal.Used:null});if(!NativeMerchantEconomy.Active)return value;var node=System.Text.Json.Nodes.JsonNode.Parse(value.GetRawText())!;node["nativeCard"]=content is CardModel serializedCard?System.Text.Json.JsonSerializer.SerializeToNode(serializedCard.ToSerializable(),Program.Json):null;if(NativeMerchantExpansion.Active)node["source"]=content is AbstractModel model?System.Text.Json.JsonSerializer.SerializeToNode(NativeMerchantExpansion.Source(model),Program.Json):null;return node;}
}
internal static partial class Program
{
    internal static MerchantRoom? MerchantRoom;
    static int merchantOps;
    static void MAction(string kind,object decision,Func<object> execute)
    {
        string id="m"+(++merchantOps);
        Action(kind,decision,()=>{Observe("n3d-open",new{id,kind,decision});var result=execute();Observe("n3d-return",new{id,kind,result});});
        Observe("n3d-boundary",new{id,kind});
    }
    static void MerchantBuy(MerchantEntry entry,bool wrongInventory=false)
    {
        if(NativeMerchantExpansion.Active){ExpansionBuy(entry);return;}
        var inventory=NativeMerchant.Inventory!;
        string stamp=StateStamp();
        var choices=inventory.AllEntries.Where(e=>e is MerchantCardEntry or MerchantPotionEntry).ToArray();
        var decisions=choices.Select(e=>(object)new{inventory=NativeIdentity.Ref(inventory),entry=NativeIdentity.Ref(e)}).ToArray();
        NativePolicy.PublicRequests.Add(new{kind="MerchantBuy",options=choices.Select((e,i)=>new{handle="o"+i,e.Cost,e.IsStocked,kind=e.GetType().Name,model=e is MerchantCardEntry c?c.CreationResult?.Card.Id.ToString():((MerchantPotionEntry)e).Model?.Id.ToString()}).ToArray(),information="player-visible-current-inventory"});
        int index=replayPath!=null?PolicyIndex("MerchantBuy",decisions):Array.IndexOf(choices,entry);entry=choices[index];
        var supplied=wrongInventory?new MerchantInventory(player!):inventory;
        if(NativeMerchant.Closed||!ReferenceEquals(run!.CurrentRoom,MerchantRoom)||!ReferenceEquals(supplied,inventory)||!inventory.AllEntries.Contains(entry))throw new NativeActionRejectedException("Wrong/stale merchant inventory/entry");
        new NativeDecisionTicket(inventory,sequence,NativeMerchantEconomy.BaseScenario(scenario)=="merchant-stale"?"stale":stamp,"buy").Consume(inventory,sequence,StateStamp(),"buy");
        MAction("MerchantBuy",decisions[index],()=>{
            int gold=player!.Gold,cost=entry.Cost;var deck=player.Deck.Cards.ToArray();var potions=player.Potions.ToArray();
            object? offered=entry is MerchantCardEntry c?c.CreationResult?.Card:((MerchantPotionEntry)entry).Model;
            int completions=0;string? failure=null;
            void Completed(PurchaseStatus _,MerchantEntry e){if(!ReferenceEquals(e,entry))throw new NativeDataException("Wrong native purchase callback");completions++;}
            void Failed(PurchaseStatus reason)=>failure=reason.ToString();
            bool success;entry.PurchaseCompleted+=Completed;entry.PurchaseFailed+=Failed;
            try{var task=entry.OnTryPurchaseWrapper(inventory,ignoreCost:false);Await(task,"Native merchant purchase and AfterItemPurchased");success=task.GetAwaiter().GetResult();}
            finally{entry.PurchaseCompleted-=Completed;entry.PurchaseFailed-=Failed;}
            if(NativeMerchantEconomy.BaseScenario(scenario)=="merchant-task-fault")throw new InvalidOperationException("labelled merchant fault after actual purchase");
            if(success&&offered is CardModel card&&!player.Deck.Cards.Contains(card)||success&&offered is PotionModel potion&&!player.Potions.Contains(potion))throw new NotSupportedException("Merchant acquisition replacement requires explicit result contract");
            if(success&&entry.IsStocked)throw new NotSupportedException("Merchant refill needs next transaction profile");
            return new{success,cost,spent=gold-player.Gold,completions,failure,offered=NativeIdentity.Ref(offered),acquired=success?NativeIdentity.Ref(offered):null,newCards=player.Deck.Cards.Except(deck).Select(NativeIdentity.Ref).ToArray(),newPotions=player.Potions.Except(potions).Select(NativeIdentity.Ref).ToArray()};
        });
    }
    static void MerchantRemove(bool cancel)
    {
        var inventory=NativeMerchant.Inventory!;var entry=inventory.CardRemovalEntry!;
        if(NativeMerchant.Closed||!ReferenceEquals(run!.CurrentRoom,MerchantRoom))throw new NativeActionRejectedException("Stale removal inventory");
        if(!entry.Used){if(cancel)Plan();else Plan(1);}
        MAction("MerchantRemove",new{inventory=NativeIdentity.Ref(inventory),entry=NativeIdentity.Ref(entry),cancel},()=>{
            int gold=player!.Gold;var before=player.Deck.Cards.ToArray();
            if(NativeMerchant.CurrentRemoval!=null)throw new NativeDataException("Overlapping native merchant removal");
            var scope=new NativeMerchant.RemovalScope(entry);bool success;NativeMerchant.CurrentRemoval=scope;
            try{var task=entry.OnTryPurchaseWrapper(inventory,ignoreCost:false,cancelable:true);Await(task,"Native merchant removal choice and purchase hooks");success=task.GetAwaiter().GetResult();}
            finally{NativeMerchant.CurrentRemoval=null;}
            if(scope.Marks!=(success?1:0)||scope.Hooks!=(success?1:0))throw new NativeDataException("Missing native removal completion/hook observation");
            return new{success,cost=entry.Cost,spent=gold-player.Gold,removed=before.Except(player.Deck.Cards).Select(NativeIdentity.Ref).ToArray(),completion=success?"native-SetUsed-at-OneOff-boundary":"none",marks=scope.Marks,hooks=scope.Hooks};
        });
    }
    static void MerchantProbe()
    {
        string fixture=NativeMerchantEconomy.BaseScenario(scenario);
        Observe("fixture-setup-declared",new{name=fixture,fixtureKind="merchant",changes="Native GainGold(1000), EnterRoomDebug Shop; optional native fill/loss; no inventory edits"});
        MAction("MerchantSetup",new{room="Shop",gold=1000,full=fixture=="merchant-full",poor=fixture=="merchant-poor"},()=>{
            Await(PlayerCmd.GainGold(1000,player!),"Declared native fixture gold");
            Await(RunManager.Instance.EnterRoomDebug(RoomType.Shop,showTransition:false),"Original merchant room entry");
            MerchantRoom=run!.CurrentRoom as MerchantRoom??throw new NativeDataException("Native merchant missing");
            NativeMerchant.Inventory=MerchantRoom.GetLocalInventory();
            if(fixture=="merchant-full")while(player!.HasOpenPotionSlots)Await(PotionCmd.TryToProcure(ModelDb.Potion<BlockPotion>().ToMutable(),player),"Declared native full potion slots");
            if(fixture=="merchant-poor")Await(PlayerCmd.LoseGold(player!.Gold,player,MegaCrit.Sts2.Core.Entities.Gold.GoldLossType.Spent),"Declared native fixture empty purse");
            if(NativeMerchant.Inventory.CardEntries.Count()!=7||NativeMerchant.Inventory.PotionEntries.Count!=3||NativeMerchant.Inventory.RelicEntries.Count!=3||NativeMerchant.Inventory.CardRemovalEntry!.Cost!=75)throw new NativeDataException("Independent A0 normal merchant inventory/removal contract");
            return new{nativeInventory=NativeIdentity.Ref(NativeMerchant.Inventory),initialRemovalCost=75};
        });
        if(fixture is "merchant-remove" or "merchant-cancel" or "merchant-repeat"){
            MerchantRemove(fixture=="merchant-cancel");
            if(fixture=="merchant-repeat")MerchantRemove(false);
            if(fixture!="merchant-cancel"&&new MerchantCardRemovalEntry(player!).Cost!=100)throw new NativeDataException("Independent next-merchant A0 price100 contract");
        }else{
            MerchantEntry entry=fixture is "merchant-potion" or "merchant-full"?NativeMerchant.Inventory!.PotionEntries[0]:NativeMerchant.Inventory!.CharacterCardEntries[1];
            MerchantBuy(entry,fixture=="merchant-wrong-inventory");if(fixture=="merchant-outstock")MerchantBuy(entry);
        }
        MAction("MerchantExit",new{inventory=NativeIdentity.Ref(NativeMerchant.Inventory)},()=>{Await(MerchantRoom!.Exit(run),"Original merchant exit");NativeMerchant.Closed=true;return new{closed=true};});
        Observe("n3d-contract",new{valid=true,scenario,fixture=true,operations=merchantOps,scope="4D1 card/potion/removal; no refill/relic/natural map claim"});
    }
}
