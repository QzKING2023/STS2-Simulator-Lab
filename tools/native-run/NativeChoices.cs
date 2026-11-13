using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Rewards;
using SimulatorLab.Core;

namespace OfflineSearchHarness;

internal sealed record NativeChoiceAnswer(string Request,string Context,string Stamp,string[] Selected);
internal sealed class NativeChoices : ICardSelector
{
    internal sealed class Frame(MethodBase method,object[] args,Frame? parent)
    {
        internal MethodBase Method=method;internal object[] Args=args;internal Frame? Parent=parent;
        internal bool Explicit;
        internal uint? NativeId;
        internal string? RequestId;
        internal string[] Selected=[];
        internal int Min=1,Max=1;
        internal string Source=>Method.Name;
    }
    static readonly AsyncLocal<Frame?> Current=new();
    static readonly JsonSerializerOptions AnswerJson=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    internal static readonly NativeChoices Instance=new();
    internal static readonly List<object> Transcript=[];
    internal static readonly Queue<int[]> Planned=new();
    internal static Func<string,CardModel[],int,int,string,bool,int[]>? SolverProvider;
    internal static string Fault="";
    internal static int Submitted;
    internal static int Reserved;
    static readonly List<(string Id,CardModel Card)> pendingRewards=[];
    static readonly HashSet<string> Consumed=[];
    internal static Action<string,object?> Emit=NativeEvidence.Emit;
    internal static void ReleaseForTeardown()
    {
        Transcript.Clear();
        Planned.Clear();
        pendingRewards.Clear();
        Consumed.Clear();
        RootChoiceCounts.Clear();
        SolverProvider = null;
        Current.Value = null;
    }
    static JsonElement[] replay=[];static int replayIndex,requestNumber;
    internal static void LoadReplay(string path){replay=JsonDocument.Parse(File.ReadAllText(path)).RootElement.EnumerateArray().Where(x=>!NativeBranchSearch.Active||int.Parse(x.GetProperty("request").GetProperty("root").GetString()!.AsSpan(1))<=NativeBranchSearch.Plan!.PrefixCount).Select(x=>x.Clone()).ToArray();}
    internal static void AssertConsumed(){if(Planned.Count>0||replayIndex!=replay.Length||pendingRewards.Count>0)throw new NativeActionRejectedException("Unconsumed extra choice answers or unfinished reward");}
    internal static bool Replaying;
    static string Stamp()=>Program.StateStamp();
    static readonly Dictionary<string,int> RootChoiceCounts=[];
    static CardModel[] Resolve(CardModel[] cards,int min,int max,string source,bool implicitChoice=false,CardModel[]? implicitResult=null,IReadOnlyList<IReadOnlyList<CardModel>>? bundles=null)
    {
        using var measurement=NativeTiming.Measure("choice-resolution");
        int depth=0;for(var f=Current.Value;f!=null;f=f.Parent)depth++;
        RootChoiceCounts[NativeEvidence.Root]=RootChoiceCounts.GetValueOrDefault(NativeEvidence.Root)+1;
        if(depth>16||RootChoiceCounts[NativeEvidence.Root]>128)throw new TimeoutException("Native nested-choice budget exhausted");
        // Public handles are per request, never oracle instance IDs or draw-position IDs.
        foreach(var c in cards)NativeIdentity.Get(c);
        string id="q"+(++requestNumber),context=NativeEvidence.Context,stamp=Stamp();
        var options=cards.Select((c,i)=>new{handle="o"+i,model=c.Id.ToString(),upgrade=c.CurrentUpgradeLevel,group=bundles==null?null:bundles[i].Select(x=>new{model=x.Id.ToString(),upgrade=x.CurrentUpgradeLevel}).ToArray()}).ToArray();
        var oracleOptions=cards.Select((c,i)=>new{handle="o"+i,representative=NativeIdentity.Ref(c),objects=(bundles==null?new[]{c}:bundles[i]).Select(NativeIdentity.Ref).ToArray()}).ToArray();
        var frame=Current.Value;if(frame!=null)frame.RequestId=id;
        var request=new{id,context,root=NativeEvidence.Root,parent=frame?.Parent?.RequestId??NativeEvidence.Root,source,min,max,implicitChoice,transport=source=="Bundle"?"native-testmode-bundle-seam":"native-selector",nativeChoiceId=frame?.NativeId,options,oracleOptions,completion=source=="CardReward"?"reward-acquired":"method-return"};
        Emit("choice-requested",request);
        var solverIndices=SolverProvider?.Invoke(id,cards,min,max,source,implicitChoice);
        string[] selected;
        if(Replaying){
            if(replayIndex>=replay.Length)throw new NativeActionRejectedException("Missing choice answer");
            var frozen=replay[replayIndex++];
            if(!NativeDecisionTicket.SameActionJson(frozen.GetProperty("request").GetRawText(),JsonSerializer.Serialize(request,Program.Json)))throw new NativeActionRejectedException("Choice request changed");
            var answer=frozen.GetProperty("answer").Deserialize<NativeChoiceAnswer>(AnswerJson)??throw new NativeActionRejectedException("Missing answer");
            selected=Validate(answer,id,context,stamp,options.Select(o=>o.handle).ToArray(),min,max);
        }else if(implicitChoice)selected=(implicitResult??[]).Select(c=>"o"+Array.IndexOf(cards,c)).ToArray();
        else{
            var indices=solverIndices??NativeBranchSearch.Select(cards,source)??(Planned.Count>0?Planned.Dequeue():Enumerable.Range(0,min).ToArray());
            var answer=new NativeChoiceAnswer(id,context,stamp,indices.Select(i=>"o"+i).ToArray());
            answer=Fault switch{
                "missing"=>throw new NativeActionRejectedException("Missing choice answer control"),
                "stale"=>answer with{Stamp="stale"},"wrong-request"=>answer with{Request="unknown"},"cross-room"=>answer with{Context="other-room"},
                "duplicate"=>answer with{Selected=["o0","o0"]},"too-few"=>answer with{Selected=[]},"too-many"=>answer with{Selected=options.Select(o=>o.handle).Append("o0").ToArray()},
                "invalid"=>answer with{Selected=["not-a-candidate"]},_=>answer};
            if(Fault=="unknown-field"){
                try{_=JsonSerializer.Deserialize<NativeChoiceAnswer>(JsonSerializer.Serialize(answer).TrimEnd('}')+",\"extra\":true}",AnswerJson);}
                catch(JsonException ex){throw new NativeActionRejectedException(ex.Message);}
                throw new InvalidOperationException("Unknown field accepted");
            }
            selected=Validate(answer,id,context,stamp,options.Select(o=>o.handle).ToArray(),min,max);
        }
        if(solverIndices!=null&&!selected.SequenceEqual(solverIndices.Select(i=>"o"+i)))throw new NativeActionRejectedException("Solver child differs from implicit/replay answer");
        var chosen=selected.Select(h=>cards[int.Parse(h.AsSpan(1))]).ToArray();
        if(frame!=null)frame.Selected=selected;
        if(implicitChoice && !chosen.SequenceEqual(implicitResult??[],ReferenceEqualityComparer.Instance))throw new InvalidOperationException("Implicit choice mismatch");
        var response=new NativeChoiceAnswer(id,context,stamp,selected);
        Transcript.Add(new{request,answer=response});
        Emit("choice-resolved",new{id,stamp,selected,implicitChoice,objects=chosen.Select(NativeIdentity.Ref).ToArray()});
        if(source=="CardReward")pendingRewards.Add((id,chosen.Single()));
        if(!implicitChoice)Submitted++;
        if(Fault=="reused")_=Validate(response,id,context,stamp,options.Select(o=>o.handle).ToArray(),min,max);
        return chosen;
    }
    internal static void RewardCompleted(CardModel acquired)
    {
        var matches=pendingRewards.Where(p=>ReferenceEquals(p.Card,acquired)).ToArray();
        if(matches.Length!=1)throw new NativeDataException("Native reward identity mismatch");
        var pending=matches[0];Emit("choice-reward-completed",new{id=pending.Id,objects=new[]{NativeIdentity.Ref(acquired)}});pendingRewards.Remove(pending);
    }
    internal static void CompleteObservedRewards(Player player)
    {
        foreach(var pending in pendingRewards.ToArray())
        {
            if(!player.Deck.Cards.Any(card=>ReferenceEquals(card,pending.Card)))
                throw new NativeDataException("Native reward returned without acquired permanent instance");
            Emit("choice-reward-completed",new{id=pending.Id,objects=new[]{NativeIdentity.Ref(pending.Card)},source="kaguya-content-probe-opening"});
            pendingRewards.Remove(pending);
        }
    }
    static string[] Validate(NativeChoiceAnswer a,string id,string context,string stamp,string[] handles,int min,int max)
    {
        if(Consumed.Contains(id)||a.Request!=id||a.Context!=context||a.Stamp!=stamp||a.Selected==null||a.Selected.Length<min||a.Selected.Length>max||a.Selected.Distinct().Count()!=a.Selected.Length||a.Selected.Any(x=>!handles.Contains(x)))
            throw new NativeActionRejectedException("Illegal/stale choice response");
        Consumed.Add(id);
        return a.Selected;
    }
    public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options,int minSelect,int maxSelect)
    {
        var f=Current.Value??throw new NotSupportedException("Unscoped native selection");f.Explicit=true;
        // The native UI honours Cancelable; ICardSelector receives only min/max.
        // Preserve that actual prefs contract instead of rejecting an empty cancel.
        if(f.Args.OfType<CardSelectorPrefs>().Any(p=>p.Cancelable))minSelect=0;
        if(f.Method.Name=="FromChooseACardScreen")minSelect=(bool)f.Args[3]?0:1;
        return Task.FromResult<IEnumerable<CardModel>>(Resolve(options.ToArray(),minSelect,maxSelect,f.Source));
    }
    public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,IReadOnlyList<CardRewardAlternative> alternatives)
    {
        if(NativeRewardProbe.Active&&NativeRewards.InSession)return NativeRewards.CardChoice(options,alternatives);
        var selected=Resolve(options.Select(c=>c.Card).ToArray(),1,1,"CardReward");
        return new CardRewardSelection{card=selected.Single()};
    }
    internal static void Install()
    {
        NativeHost.Register(AccessTools.Method(typeof(CardReward),"OnSelect"),nameof(CardRewardBefore),typeof(NativeChoices),nameof(CardRewardAfter));
        NativeHost.Register(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Runs.RunManager).GetProperty("PlayerChoiceSynchronizer")!.PropertyType,"ReserveChoiceId"),nameof(ReserveBefore),typeof(NativeChoices),nameof(ReserveAfter));
        NativeHost.Register(AccessTools.Method(typeof(CardSelectCmd),"ReportSoftlock"),nameof(Softlock),typeof(NativeChoices));
        var allowed=new HashSet<string>{"FromChooseACardScreen","FromSimpleGrid","FromCombatPile","FromHand","FromHandForDiscard","FromHandForUpgrade","FromDeckForUpgrade","FromDeckGeneric","FromDeckForRemoval","FromDeckForTransformation","FromDeckForEnchantment","FromChooseABundleScreen"};
        foreach(var m in typeof(CardSelectCmd).GetMethods(BindingFlags.Public|BindingFlags.Static).Where(m=>m.Name.StartsWith("From"))){
            if(!allowed.Contains(m.Name)){NativeHost.Register(m,nameof(Reject),typeof(NativeChoices));continue;}
            // Wrappers have no separate decision; their leaf method owns the logical request.
            if(m.Name=="FromDeckForEnchantment"&&m.GetParameters()[0].ParameterType==typeof(Player))continue;
            if(m.Name is "FromHandForDiscard" or "FromDeckForRemoval"||m.Name=="FromCombatPile"&&m.GetParameters().Length==4)continue;
            string post=m.ReturnType==typeof(Task<CardModel>)?nameof(AfterSingle):nameof(AfterMany);
            NativeHost.Register(m,nameof(Before),typeof(NativeChoices),post);
        }
        var bundle=typeof(CardSelectCmd).GetMethod("FromChooseABundleScreen")!;
        var state=bundle.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
        var move=AccessTools.Method(state,"MoveNext");
        if(Harmony.GetPatchInfo(move)!=null)throw new InvalidOperationException("Unexpected bundle state machine patch");
        GameBootstrap.Harmony.Patch(move,transpiler:new HarmonyMethod(typeof(NativeChoices),nameof(BundleTranspiler)));
        NativeHost.Seams.Add(new{method=move.ToString(),token=move.MetadataToken,mvid=move.Module.ModuleVersionId,ilSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(move.GetMethodBody()!.GetILAsByteArray()!)),handler="BundleTranspiler"});
    }
    sealed record CardScope(CardModel[] Candidates,CardReward? Previous,bool NewMenu);
    static void CardRewardBefore(CardReward __instance,out CardScope __state){bool active=NativeRewardProbe.Active&&NativeRewards.InSession;__state=new(NativeRooms.Active||NativeRewardProbe.Active?__instance.Cards.ToArray():[],active?NativeRewards.EnterCard(__instance):null,active);}
    static void CardRewardAfter(CardReward __instance,CardScope __state,ref Task<bool> __result){if(__state.NewMenu){NativeRewards.LeaveCard(__state.Previous);__result=NativeRewards.CardAfter(__result,__instance);}else if(NativeRooms.Active||NativeRewardProbe.Active)__result=CompleteNativeReward(__result,__state.Candidates);}
    static async Task<bool> CompleteNativeReward(Task<bool> task,CardModel[] originalCandidates)
    {
        bool result=await task;
        // Original OnSelect removes acquired cards from its candidate list.
        foreach(var pending in pendingRewards.Where(p=>originalCandidates.Any(c=>ReferenceEquals(c,p.Card))).ToArray()){
            if(!pending.Card.Owner!.Deck.Cards.Any(c=>ReferenceEquals(c,pending.Card)))throw new NativeDataException("Original card reward returned without acquired permanent instance");
            RewardCompleted(pending.Card);
        }
        return result;
    }
    static void ReserveBefore(){}
    static void Softlock(){NativeHost.Faults.Add(new InvalidOperationException("Native selection reported softlock"));NativeEvidence.Emit("selection-fault",new{source="CardSelectCmd.ReportSoftlock"});}
    static void ReserveAfter(uint __result){NativeRewardClosure.Reserved(__result);Reserved++;if(Current.Value is {} f)f.NativeId=__result;NativeEvidence.Emit("native-choice-reserved",new{nativeChoiceId=__result});}
    static void Reject()=>throw new NotSupportedException("Native choice method not supported");
    static void Before(MethodBase __originalMethod,object[] __args,out Frame __state)
    {
        __state=new Frame(__originalMethod,__args,Current.Value);Current.Value=__state;
        var prefs=__args.OfType<CardSelectorPrefs>().ToArray();
        if(prefs.Length>0){__state.Min=prefs[0].MinSelect;__state.Max=prefs[0].MaxSelect;}
    }
    static void AfterSingle(ref Task<CardModel?> __result,Frame __state){Current.Value=__state.Parent;__result=FinishSingle(__result,__state);}
    static async Task<CardModel?> FinishSingle(Task<CardModel?> task,Frame f){var result=await task;if(!f.Explicit)Implicit(f,result==null?[]:[result]);NativeEvidence.Emit("choice-returned",new{id=f.RequestId,objects=result==null?Array.Empty<object?>():[NativeIdentity.Ref(result)]});return result;}
    static void AfterMany(ref Task<IEnumerable<CardModel>> __result,Frame __state){Current.Value=__state.Parent;__result=FinishMany(__result,__state);}
    static async Task<IEnumerable<CardModel>> FinishMany(Task<IEnumerable<CardModel>> task,Frame f){var result=(await task).ToArray();if(!f.Explicit)Implicit(f,result);NativeEvidence.Emit("choice-returned",new{id=f.RequestId,objects=result.Select(NativeIdentity.Ref).ToArray()});return result;}
    static void Implicit(Frame f,CardModel[] result)
    {
        var candidate=f.Args.OfType<IReadOnlyList<CardModel>>().FirstOrDefault();
        // The original method has already filtered and selected. Do not re-run its predicates.
        var previous=Current.Value;Current.Value=f;
        try{_=Resolve((candidate??result).ToArray(),0,Math.Max(f.Max,result.Length),f.Source,true,result);}
        finally{Current.Value=previous;}
    }
    static IEnumerable<CodeInstruction> BundleTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var code=instructions.ToList();int changed=0;
        for(int i=1;i<code.Count;i++)if(code[i].opcode==OpCodes.Callvirt&&code[i].operand is MethodInfo m&&m.Name=="get_Item"&&m.DeclaringType==typeof(IReadOnlyList<IReadOnlyList<CardModel>>)&&code[i-1].opcode==OpCodes.Ldc_I4_0){
            code[i].opcode=OpCodes.Call;code[i].operand=AccessTools.Method(typeof(NativeChoices),nameof(SelectBundle));changed++;
        }
        if(changed!=1)throw new InvalidOperationException("Pinned Bundle IL anchor mismatch: "+changed);
        return code;
    }
    static IReadOnlyList<CardModel> SelectBundle(IReadOnlyList<IReadOnlyList<CardModel>> bundles,int ignored)
    {
        var f=Current.Value??throw new InvalidOperationException("Bundle context missing");f.Explicit=true;
        // The response retains the native group index, including groups sharing the same first object.
        if(bundles.Any(b=>b.Count==0))throw new NotSupportedException("Empty bundle option");
        _=Resolve(bundles.Select(b=>b[0]).ToArray(),1,1,"Bundle",bundles:bundles);
        return bundles[int.Parse(f.Selected.Single().AsSpan(1))];
    }
}
