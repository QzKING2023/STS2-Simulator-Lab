using System.Text.Json;
namespace OfflineSearchHarness;
internal static class NativeRelicPickupEvidence
{
 internal static readonly HashSet<string> Kinds=["default-relic-admitted","default-relic-completed"];
 sealed class Op(JsonElement row){internal readonly JsonElement Open=row;internal JsonElement Answer,Start,Admission,Completion;internal bool Returned;}
 static string T(JsonElement e,string key)=>e.GetProperty(key).GetString()!;
 static JsonElement F(JsonElement r)=>r.GetProperty("detail").GetProperty("facts");
 static JsonElement S(JsonElement r)=>r.GetProperty("detail").GetProperty("state");
 static string Ref(JsonElement e)=>T(e,"$ref");
 static bool Eq(JsonElement a,JsonElement b)=>NativeBoundaryContract.Equal(a,b);
 static void R(bool b,string why)=>NativeBoundaryContract.Require(b,"Default relic: "+why);
 internal static object Review(IEnumerable<JsonElement> rows){
  bool enabled=false;int completed=0;string root="",context="";var ops=new Dictionary<string,Op>();var offered=new Dictionary<string,(string root,string context,JsonElement option)>();var identities=new Dictionary<string,JsonElement>();
  foreach(var row in rows){
   string kind=T(row,"kind");var detail=row.GetProperty("detail");
   if(kind=="header"){
    if(detail.TryGetProperty("defaultRelicProfile",out var profile)&&profile.ValueKind!=JsonValueKind.Null){R(profile.ValueKind==JsonValueKind.String&&profile.GetString()==NativeRelicPickup.Profile,"known profile");enabled=true;}
   }
   if(!enabled){R(!Kinds.Contains(kind),"undeclared profile");continue;}
   if(kind=="identity")identities.Add(T(detail,"id"),detail);
   if(kind=="dispatch"){root=T(row,"root");context=T(row,"context");}
   if(kind=="n3c-set")foreach(var option in F(row).GetProperty("options").EnumerateArray())if(T(option,"kind")=="RelicReward")offered[Ref(option.GetProperty("reference"))]=(T(row,"root"),T(row,"context"),option);
   if(kind=="n3c-open"&&T(F(row),"kind")=="reward-item")ops.Add(T(F(row),"id"),new Op(row));
   if(kind=="n3c-answer"&&ops.TryGetValue(T(F(row),"id"),out var answer)){R(answer.Answer.ValueKind==JsonValueKind.Undefined,"unique answer");answer.Answer=row;}
   if(kind=="n3c-start"&&ops.TryGetValue(T(F(row),"id"),out var start)){R(start.Start.ValueKind==JsonValueKind.Undefined,"unique item start");start.Start=row;}
   if(Kinds.Contains(kind)){
    var f=F(row);string[] fields=kind=="default-relic-admitted"?["id","reward","offered","model","source"]:["id","reward","offered","model","source","acquired"];R(f.EnumerateObject().Select(p=>p.Name).ToHashSet().SetEquals(fields),"closed pickup facts");R(T(row,"root")==root&&T(row,"context")==context,"current action");R(ops.TryGetValue(T(f,"id"),out _),"required reward item");var op=ops[T(f,"id")];
    R(op.Start.ValueKind==JsonValueKind.Object&&op.Answer.ValueKind==JsonValueKind.Object&&!op.Returned&&T(op.Open,"root")==root&&T(op.Open,"context")==context,"started current item");
    var option=F(op.Open).GetProperty("options").EnumerateArray().Single(x=>T(x,"Handle")==T(F(op.Answer),"handle"));
    R(T(option,"Kind")=="RelicReward"&&Eq(option.GetProperty("Reference"),f.GetProperty("reward"))&&Eq(F(op.Answer).GetProperty("selected"),f.GetProperty("reward")),"selected original reward");
    var actual=offered[Ref(f.GetProperty("reward"))];R(actual.root==root&&actual.context==context&&Eq(actual.option.GetProperty("content"),f.GetProperty("offered"))&&T(actual.option,"model")==T(f,"model"),"actual set offered instance/model");
    var type=NativeRelicPickup.Resolve(T(f,"model"))??throw new NativeDataException("Unknown original relic type");var identity=identities[Ref(f.GetProperty("offered"))];R(T(identity,"type")==type.Name&&T(identity,"model")==T(f,"model")&&Eq(f.GetProperty("source"),NativeRelicPickup.Source(type,T(f,"model"))),"original concrete source proof");
    if(kind=="default-relic-admitted"){
     R(op.Admission.ValueKind==JsonValueKind.Undefined&&Eq(S(row),S(op.Start))&&Eq(S(op.Start),S(op.Open)),"unique admission unchanged item start");op.Admission=row;
    }else{
     R(op.Admission.ValueKind==JsonValueKind.Object&&op.Completion.ValueKind==JsonValueKind.Undefined&&Eq(f.GetProperty("acquired"),f.GetProperty("offered")),"unique original acquisition completion");
     NativeRelicPickup.Frame(S(op.Admission),S(row),Ref(f.GetProperty("acquired")),T(f,"model"));op.Completion=row;completed++;
    }
   }
   if(kind=="n3c-return"&&ops.TryGetValue(T(F(row),"id"),out var returned)&&returned.Answer.ValueKind==JsonValueKind.Object){
    var option=F(returned.Open).GetProperty("options").EnumerateArray().Single(x=>T(x,"Handle")==T(F(returned.Answer),"handle"));
    if(T(option,"Kind")=="RelicReward"){
     R(!returned.Returned&&returned.Completion.ValueKind==JsonValueKind.Object&&T(row,"root")==T(returned.Open,"root")&&T(row,"context")==T(returned.Open,"context"),"required completion before item return");
     var result=F(row).GetProperty("result");R(result.GetProperty("success").GetBoolean()&&!result.GetProperty("skipped").GetBoolean()&&Eq(result.GetProperty("selected"),F(returned.Completion).GetProperty("reward"))&&Eq(result.GetProperty("acquired"),F(returned.Completion).GetProperty("acquired"))&&Eq(S(row),S(returned.Completion)),"actual returned instance/full state");returned.Returned=true;
    }
   }
   if(kind=="confirmed")foreach(var op in ops.Values.Where(x=>T(x.Open,"root")==T(row,"root")&&x.Answer.ValueKind==JsonValueKind.Object)){
    var option=F(op.Open).GetProperty("options").EnumerateArray().Single(x=>T(x,"Handle")==T(F(op.Answer),"handle"));if(T(option,"Kind")=="RelicReward")R(op.Returned,"confirmed root requires original relic completion");
   }
  }
  return new{enabled,completed};
 }
}
