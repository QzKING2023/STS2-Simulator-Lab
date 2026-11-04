using System.Text.Json;
namespace OfflineSearchHarness;
internal sealed class NativeBranchContract
{
 internal static readonly HashSet<string> Snapshots=["branch-switch","branch-frontier","branch-horizon"];
 internal bool Horizon;
 bool active,switched,frontier,bossComplete;string mode="",lastRoot="",lastContext="",currentKind="";int forced,roots,prefix,endTurns;
 JsonElement confirmed,profile,moves,lastChoice,selectedChoice;
 bool choiceOpen,choiceImplicit,choiceDecided,choiceResolved,exhaustConsumed;
 string currentRoot="",currentContext="";string? expectedExhaust;
 static void R(bool condition,string text){if(!condition)throw new NativeDataException("Branch "+text);}
 static bool Eq(JsonElement a,JsonElement b)=>NativeBoundaryContract.Equal(a,b);
 internal void Accept(JsonElement p,JsonElement? state){
  string k=p.GetProperty("kind").GetString()!,root=p.GetProperty("root").GetString()!,ctx=p.GetProperty("context").GetString()!;var d=p.GetProperty("detail");
  if(k=="header"){
   active=d.GetProperty("provenance").TryGetProperty("Branch",out var flag)&&flag.GetBoolean();
   if(active){R(d.GetProperty("scenario").GetString()=="major-natural-act3-route","scenario");profile=d.GetProperty("branchProfile");var plan=profile.GetProperty("plan");prefix=plan.GetProperty("PrefixCount").GetInt32();moves=plan.GetProperty("Moves").Clone();mode=plan.GetProperty("Mode").GetString()!;R(prefix>=595&&moves.GetArrayLength()<=3&&plan.GetProperty("HorizonTurns").GetInt32()==2&&mode is "probe" or "rollout" or "tail","fixed budget");}
  }
  if(!active){R(!Snapshots.Contains(k)&&k!="branch-choice","events outside branch profile");return;}
  R(!Horizon||k is "history-checkpoint" or "footer","action/effect after planned horizon");
  if(k=="natural-third-boss-complete")bossComplete=true;
  if(k=="dispatch"){
   R(!choiceOpen,"unfinished child before dispatch");
   currentRoot=root;currentContext=ctx;expectedExhaust=null;exhaustConsumed=false;
   currentKind=d.GetProperty("facts").GetProperty("kind").GetString()!;roots++;
   if(switched&&forced<moves.GetArrayLength()){
    var move=moves[forced];R(Eq(d.GetProperty("facts"),move.GetProperty("Action")),"forced current action");
    expectedExhaust=move.GetProperty("Exhaust").GetString();
   }
  }
  if(k=="confirmed"){
   R(!choiceOpen&&(expectedExhaust==null||exhaustConsumed),"missing forced child completion");
   if(switched&&!frontier)forced++;
   if(frontier&&currentKind=="EndTurn")endTurns++;
   confirmed=state!.Value.Clone();lastRoot=root;lastContext=ctx;
  }
  if(switched&&k=="choice-requested"&&d.GetProperty("source").GetString()=="FromHand"){
   R(!choiceOpen&&root==currentRoot&&ctx==currentContext&&d.GetProperty("root").GetString()==root&&d.GetProperty("context").GetString()==ctx,"actual child request owner");
   lastChoice=d.Clone();choiceOpen=true;choiceImplicit=d.GetProperty("implicitChoice").GetBoolean();choiceDecided=false;choiceResolved=false;
  }
  if(k=="branch-choice"){
   R(switched&&choiceOpen&&!choiceImplicit&&!choiceDecided&&!choiceResolved&&root==currentRoot&&ctx==currentContext&&d.GetProperty("source").GetString()=="FromHand"&&d.GetProperty("index").GetInt32()>=0,"actual choice owner/order");
   int index=d.GetProperty("index").GetInt32();
   R(index<lastChoice.GetProperty("oracleOptions").GetArrayLength()&&Eq(d.GetProperty("selected"),lastChoice.GetProperty("oracleOptions")[index].GetProperty("representative")),"actual request instance/index");
   selectedChoice=d.GetProperty("selected").Clone();choiceDecided=true;
  }
  if(choiceOpen&&k=="choice-resolved"){
   R(!choiceResolved&&(choiceImplicit||choiceDecided)&&root==currentRoot&&ctx==currentContext&&d.GetProperty("id").GetString()==lastChoice.GetProperty("id").GetString(),"missing/duplicate child decision or result owner");
   var objects=d.GetProperty("objects");
   if(!choiceImplicit)R(objects.GetArrayLength()==1&&Eq(objects[0],selectedChoice),"selected native result instance");
   if(expectedExhaust!=null){R(!exhaustConsumed&&objects.GetArrayLength()==1&&objects[0].GetProperty("$ref").GetString()==expectedExhaust,"forced Exhaust actual instance");exhaustConsumed=true;}
   if(choiceImplicit)selectedChoice=objects.Clone();
   choiceResolved=true;
  }
  if(choiceOpen&&k=="choice-returned"){
   R(choiceResolved&&root==currentRoot&&ctx==currentContext&&d.GetProperty("id").GetString()==lastChoice.GetProperty("id").GetString(),"child completion owner/order");
   R(choiceImplicit?Eq(d.GetProperty("objects"),selectedChoice):d.GetProperty("objects").GetArrayLength()==1&&Eq(d.GetProperty("objects")[0],selectedChoice),"child returned instance");choiceOpen=false;
  }
  if(!Snapshots.Contains(k))return;
  R(root==lastRoot&&ctx==lastContext&&Eq(state!.Value,confirmed),"static full confirmed state/owner");
  var s=state!.Value;R(s.GetProperty("hp").GetInt32()>0&&s.GetProperty("act").GetInt32()==2&&s.GetProperty("majorCombat").GetProperty("encounter").GetString()=="ENCOUNTER.TEST_SUBJECT_BOSS"&&s.GetProperty("majorCombat").GetProperty("inProgress").GetBoolean()&&s.GetProperty("combat").GetProperty("phase").GetString()=="Play"&&s.GetProperty("combat").GetProperty("side").GetString()=="Player","safe actual TestSubject Play");
  var f=d.GetProperty("facts");
  if(k=="branch-switch"){R(!switched&&roots==prefix&&f.GetProperty("prefix").GetInt32()==prefix&&Eq(f.GetProperty("config"),profile.GetProperty("configHash")),"exact switch boundary/config");switched=true;}
  if(k=="branch-frontier"){
   R(switched&&!frontier&&forced==moves.GetArrayLength()&&f.GetProperty("confirmed").GetInt32()==roots,"forced frontier unique/order");frontier=true;
   var opts=f.GetProperty("options");R(opts.GetArrayLength() is >=1 and <=4,"proposal count");
   foreach(var option in opts.EnumerateArray()){
    var a=option.GetProperty("Action");var decision=a.GetProperty("decision");string kind=a.GetProperty("kind").GetString()!;
    R(kind=="EndTurn"?decision.GetProperty("turn").GetInt32()==s.GetProperty("combat").GetProperty("turn").GetInt32():kind=="PlayCard"&&s.GetProperty("combat").GetProperty("hand").EnumerateArray().Any(c=>Eq(c.GetProperty("id"),decision.GetProperty("card"))&&Eq(c.GetProperty("model"),decision.GetProperty("model"))),"actual candidate hand/turn");
   }
  }
  if(k=="branch-horizon"){R(switched&&frontier&&!Horizon&&f.GetProperty("mode").GetString()==mode&&f.GetProperty("forced").GetInt32()==forced&&f.GetProperty("enemyTurns").GetInt32()==endTurns&&(mode=="probe"&&endTurns==0||mode=="rollout"&&endTurns==2),"planned stop facts/order");Horizon=true;}
 }
 internal void Finish(JsonElement report){
  if(!active)return;
  R(!choiceOpen&&switched&&forced==moves.GetArrayLength(),"unconsumed switch/forced prefix");
  var r=report.GetProperty("result");if(Horizon)R(r.GetProperty("Terminal").GetString()=="Ongoing"&&r.GetProperty("Stop").GetString()=="SliceBoundary","planned stop result");
  else R(r.GetProperty("Terminal").GetString()=="Death"||bossComplete,"missing actual terminal/Boss completion");
 }
}
