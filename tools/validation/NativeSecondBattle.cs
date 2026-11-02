using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CombatSolver.Engine.Common;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace OfflineSearchHarness;

internal static partial class NativeModRunLoop
{
    public static bool SecondBattleEnabled => Enabled && Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_SECOND_BATTLE")=="1";
    private static object? SecondBattle;
    private static CardModel? ExpectedSecondReward;
    private static readonly List<object> RewardPlayProvenance=[];

    public static void VerifySecondRewardCard(CardModel card)
    {
        if(!SecondBattleEnabled||Phase!="second-battle"||card.Id.Entry!="MANA_SHIELD")return;
        var native=card.Owner.PlayerCombatState!;
        if(ExpectedSecondReward==null||!ReferenceEquals(card.DeckVersion,ExpectedSecondReward)||!native.Hand.Cards.Contains(card))
            throw new PredictionUnsupportedException("ManaShield play is not the naturally drawn, actually acquired reward card.");
        RewardPlayProvenance.Add(new {turn=native.TurnNumber,card=card.Id.ToString(),deckVersion=card.DeckVersion!.Id.ToString(),inNativeHand=true,actualRewardReferenceMatched=true,
            energyBefore=native.Energy,manaBefore=JsonSerializer.SerializeToElement(ManaState(card.Owner),Json)});
    }

    private static object RunSecondBattle(CombatState combat,MainLoopContext loop,string output,CardModel? acquired,HarnessOptions options)
    {
        string directory=Path.Combine(output,"second-battle");Directory.CreateDirectory(directory);
        var player=LocalContext.GetMe(combat)!;
        ExpectedSecondReward=acquired;RewardPlayProvenance.Clear();
        var plans=new List<JsonElement[]>();
        var reports=new List<JsonElement>();
        string? fixedPath=Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_SECOND_PLAN");
        JsonElement[][]? fixedChunks=null;
        if(!string.IsNullOrEmpty(fixedPath))
        {
            string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixedPath)));
            if(!StringComparer.OrdinalIgnoreCase.Equals(hash,Environment.GetEnvironmentVariable("SIMULATORLAB_NATIVE_SECOND_PLAN_SHA")))
                throw new InvalidOperationException("Second-battle plan hash mismatch.");
            fixedChunks=JsonSerializer.Deserialize<JsonElement[][]>(File.ReadAllText(fixedPath))!;
        }
        bool shieldPlayed=false;int? shieldDrawTurn=null;
        string status="unsupported";string? reason="Bounded second-battle plan did not reach a terminal state.";
        var routeJson=new JsonSerializerOptions(Json){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};routeJson.Converters.Add(new JsonStringEnumConverter());
        for(int index=0;index<12;index++)
        {
            if(player.PlayerCombatState!.TurnNumber>12){reason="Second battle exceeded the declared 12-turn fixture bound.";break;}
            JsonElement[] actions;
            if(fixedChunks!=null)
            {
                if(index>=fixedChunks.Length){reason="Frozen second-battle chunks ended before Victory.";break;}
                actions=fixedChunks[index];
            }
            else if(acquired!=null&&!shieldPlayed)
            {
                // Coverage policy: defend and draw naturally until the actually acquired card is in hand.
                // No draw-order, HP, energy or mana setter is used.
                var native=player.PlayerCombatState;
                var shield=native.Hand.Cards.FirstOrDefault(c=>ReferenceEquals(c.DeckVersion,acquired));
                var prefix=new List<JsonElement>();
                if(shield!=null)
                    prefix.Add(Action("PlayCard",native.TurnNumber,shield.Id.Entry));
                else
                {
                    if(native.TurnNumber>5){reason="Acquired ManaShield not naturally drawn within the bounded coverage prefix.";break;}
                    foreach(var card in native.Hand.Cards.Where(c=>c.Id.Entry is "DEFEND_IRONCLAD" or "MANA_GATHER").Take(3))
                        prefix.Add(Action("PlayCard",native.TurnNumber,card.Id.Entry));
                    prefix.Add(Action("EndTurn",native.TurnNumber,""));
                }
                actions=prefix.ToArray();
            }
            else
            {
                var outcome=ModRuntime.RunSearchDetailed(combat,options,loop,null);
                actions=JsonSerializer.SerializeToElement(outcome.RouteActions,routeJson).EnumerateArray().Select(e=>e.Clone()).ToArray();
                if(actions.Length==0){reason="Native second-battle planner produced no executable actions.";break;}
            }
            string chunk=Path.Combine(directory,$"chunk-{index+1:00}");Directory.CreateDirectory(chunk);
            string path=Path.Combine(chunk,"route.json");File.WriteAllText(path,JsonSerializer.Serialize(actions,Json));
            File.WriteAllText(Path.Combine(chunk,"harness-result.json"),JsonSerializer.Serialize(new {
                eleanorMod=new {assemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Environment.GetEnvironmentVariable("SIMULATORLAB_ELEANOR_MOD_DLL")!)))},
                scenario=new {CharacterId="MyMod",EncounterId=((CombatRoom)combat.RunState.CurrentRoom!).Encounter.Id.Entry,Seed="LAB_V0111_ELEANOR_V040_FORM_01"}
            },Json));
            var replay=JsonSerializer.SerializeToElement(EleanorV040Probe.RunVictoryReplay(combat,loop,chunk,path,allowBoundary:true),Json);
            plans.Add(actions);reports.Add(replay);
            foreach(var step in replay.GetProperty("steps").EnumerateArray())
                if(step.GetProperty("cardId").GetString()=="MANA_SHIELD"&&step.GetProperty("nativeAccepted").GetBoolean())
                {
                    shieldPlayed=true;shieldDrawTurn ??= step.GetProperty("turn").GetInt32();
                    // The native action resolver picked this card from the actual hand, with a DeckVersion
                    // that is verified at handoff and a single ManaShield in the permanent deck.
                }
            string result=replay.GetProperty("status").GetString()!;
            if(result=="passed")
            {
                status=acquired==null||shieldPlayed?"passed":"failed";
                reason=status=="passed"?null:"Victory occurred without the required acquired ManaShield play.";
                break;
            }
            if(result!="boundary"){status=result;reason=replay.GetProperty("stopReason").GetString();break;}
        }
        string planPath=Path.Combine(directory,"chunk-plan.json");File.WriteAllText(planPath,JsonSerializer.Serialize(plans,Json));
        var report=new {status,reason,coveragePolicy="natural generated route; when taking ManaShield, defend/draw without attacks until it is actually in hand, then play it before the remaining native plan",
            naturalCardOrderUnchanged=true,acquiredCard=acquired?.Id.ToString(),shieldPlayed,shieldDrawTurn,rewardPlayProvenance=RewardPlayProvenance,
            planPath,planSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(planPath))),
            chunks=reports,trace=reports.Select(r=>new {opening=r.GetProperty("nativeOpeningRoot"),openingContinuation=r.GetProperty("nativeOpeningContinuation"),steps=r.GetProperty("steps")}).ToArray(),
            terminal=status=="passed"?"Victory":status,playerHp=player.Creature.CurrentHp};
        File.WriteAllText(Path.Combine(directory,"second-battle.json"),JsonSerializer.Serialize(report,Json));
        return report;

        static JsonElement Action(string kind,int turn,string cardId) => JsonSerializer.SerializeToElement(new {kind,turn,cardId,cardOccurrence=0,targetIndex=-1});
    }
}
