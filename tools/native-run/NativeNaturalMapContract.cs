using System.Text.Json;
namespace OfflineSearchHarness;

// One immutable native map per generation; route authority is not self-reported
// by the chosen action. Each registered continuation has explicit per-act scope and endpoints.
internal sealed class NativeNaturalMapContract
{
 NativeInstanceLedger firstInstances=new();
 string scenario="";JsonElement anchor;string initialRoot="",initialContext="";readonly Dictionary<string,(string Type,string? Model)> identities=[];
 static string T(JsonElement e,string p)=>e.GetProperty(p).GetString()!;
 static bool Eq(JsonElement a,JsonElement b)=>NativeBoundaryContract.Equal(a,b);
 static void R(bool v,string m){if(!v)throw new NativeDataException("Natural map "+m);}
 static JsonElement Point(JsonElement map,string coord)=>map.GetProperty("points").EnumerateArray().Single(x=>T(x,"coord")==coord);
 static void Shape(JsonElement m){
  R(m.ValueKind==JsonValueKind.Object,"required object");
  R(m.EnumerateObject().Select(x=>x.Name).Order().SequenceEqual(new[]{"act","boss","bossModel","ancientModel","generation","mapType","points","reference","start"}.Order()),"closed graph schema");
  var points=m.GetProperty("points");R(points.ValueKind==JsonValueKind.Array&&points.GetArrayLength()>0&&points.GetArrayLength()<256,"bounded native graph");
  var coords=points.EnumerateArray().Select(x=>T(x,"coord")).ToHashSet();R(coords.Count==points.GetArrayLength(),"unique coordinates");
  foreach(var p in points.EnumerateArray()){
   R(p.EnumerateObject().Select(x=>x.Name).Order().SequenceEqual(new[]{"coord","type","children"}.Order()),"closed point schema");
   R(Enum.TryParse<MegaCrit.Sts2.Core.Map.MapPointType>(T(p,"type"),out var type)&&Enum.IsDefined(type)&&type.ToString()==T(p,"type"),"known point type");
   var children=p.GetProperty("children").EnumerateArray().Select(x=>x.GetString()!).ToArray();R(children.Distinct().Count()==children.Length&&children.All(coords.Contains),"known unique children");
  }
  R(T(Point(m,T(m,"start")),"type")=="Ancient"&&T(Point(m,T(m,"boss")),"type")=="Boss","actual start/Boss nodes");
  var seen=new HashSet<string>();var active=new HashSet<string>();
  void Visit(string c){R(active.Add(c),"acyclic graph");if(seen.Add(c))foreach(var x in Point(m,c).GetProperty("children").EnumerateArray())Visit(x.GetString()!);active.Remove(c);}
  Visit(T(m,"start"));R(seen.Contains(T(m,"boss")),"Boss reachable from start");
 }
 internal void Accept(JsonElement p,JsonElement? state)
 {
  var kind=T(p,"kind");var d=p.GetProperty("detail");if(kind=="header")scenario=T(d,"scenario");
  if(kind=="identity")identities.Add(T(d,"id"),(T(d,"type"),d.GetProperty("model").GetString()));
  if(kind=="dispatch"&&T(d.GetProperty("facts"),"kind")=="EnterAct"){R(initialRoot=="","unique initial map action");initialRoot=T(p,"root");initialContext=T(p,"context");}
  if(kind=="dispatch"&&T(d.GetProperty("facts"),"kind")=="NextActStart"){firstInstances=new();firstInstances.Begin(identities.Keys);}
  if(state is not {} s)return;
  var m=s.GetProperty("naturalMap");
  if(m.ValueKind==JsonValueKind.Null){R(anchor.ValueKind==JsonValueKind.Undefined&&kind is "new-run" or "dispatch","null only before initial map");return;}
  if(anchor.ValueKind==JsonValueKind.Undefined||!Eq(anchor,m))Shape(m);
  R(Eq(m.GetProperty("act"),s.GetProperty("act"))&&Eq(m.GetProperty("generation"),s.GetProperty("lifecycle").GetProperty("mapGeneration")),"actual act/generation");
  string mapId=T(m.GetProperty("reference"),"$ref");R(identities.TryGetValue(mapId,out var mapType)&&mapType.Type==T(m,"mapType"),"registered native map identity");
  if(anchor.ValueKind==JsonValueKind.Undefined){R(initialRoot!=""&&T(p,"root")==initialRoot&&T(p,"context")==initialContext&&s.GetProperty("act").GetInt32()==0,"initial map from original EnterAct action");anchor=m.Clone();}
  else if(!Eq(anchor,m)){
   R(kind=="act-transition-completed"&&m.GetProperty("act").GetInt32()==anchor.GetProperty("act").GetInt32()+1&&m.GetProperty("generation").GetInt32()==anchor.GetProperty("generation").GetInt32()+1&&mapId!=T(anchor.GetProperty("reference"),"$ref"),"map changes only at actual act task completion");anchor=m.Clone();
  }
  var node=s.GetProperty("node");
  if(node.ValueKind!=JsonValueKind.Null){
   var point=Point(m,node.GetString()!);
   if(kind=="major-battle-start"){
    string actual=T(s.GetProperty("majorCombat"),"type"),declared=T(point,"type");
    R(declared=="Unknown"||actual==declared,"battle type from arrived native map point");
    if(declared=="Boss")R(T(s.GetProperty("majorCombat"),"encounter")==T(m,"bossModel"),"actual generated Boss model");
   }
  }
  if(kind=="map-candidates"){
   R(node.ValueKind==JsonValueKind.String,"map choices require current point");var point=Point(m,node.GetString()!);
   var choices=d.GetProperty("facts").EnumerateArray().ToArray();var children=point.GetProperty("children").EnumerateArray().Select(x=>x.GetString()!).ToHashSet();
   R(choices.Length==children.Count&&choices.Select(x=>T(x,"to")).ToHashSet().SetEquals(children)&&choices.All(x=>T(x,"from")==node.GetString()&&T(x,"type")==T(Point(m,T(x,"to")),"type")),"complete current native edges/types");
  }
  if(kind=="natural-first-node")R(node.ValueKind==JsonValueKind.String&&node.GetString()==T(m,"start")&&T(s,"room")=="EventRoom"&&s.GetProperty("rooms").GetProperty("eventOptions").GetArrayLength()>0,"next start actionable Ancient options");
  if(kind=="confirmed"&&T(d.GetProperty("facts").GetProperty("action"),"kind")=="NextActStart"){
   var rooms=s.GetProperty("rooms");var ev=s.GetProperty("lifecycle").GetProperty("eventModel");
   R(rooms.EnumerateObject().Select(x=>x.Name).Order().SequenceEqual(new[]{"reference","map","stack","rest","eventOptions"}.Order()),"closed first room projection");
   R(identities[T(rooms.GetProperty("reference"),"$ref")].Type=="EventRoom"&&identities[T(ev.GetProperty("reference"),"$ref")].Model==T(m,"ancientModel"),"first room/model registered identities");
   var opts=rooms.GetProperty("eventOptions").EnumerateArray().ToArray();var refs=new HashSet<string>();
   var fresh=opts.Select(o=>T(o.GetProperty("reference"),"$ref")).Append(T(rooms.GetProperty("reference"),"$ref")).Append(T(ev.GetProperty("reference"),"$ref")).ToArray();
   firstInstances.Fresh(fresh,identities.ContainsKey,"Natural map first node actual fresh native instance ledger");
   if(NativeNaturalBoss.IsThird(scenario)&&s.GetProperty("act").GetInt32()==2){R(opts.All(o=>identities[T(o.GetProperty("reference"),"$ref")].Type=="EventOption"),"third native option declared types");NativeThirdAncientContract.Open(s);return;}
   if(NativeDarv.IsScenario(scenario)){
    R(T(ev,"model")=="EVENT.DARV"&&opts.Length==3,"declared original Darv three native initial options");
    string[] darvPool=["ASTROLABE","BLACK_STAR","CALLING_BELL","EMPTY_CAGE","PANDORAS_BOX","RUNIC_PYRAMID","SNECKO_EYE","ECTOPLASM","SOZU","PHILOSOPHERS_STONE","VELVET_CHOKER","DUSTY_TOME"];
    R(opts.Select(o=>T(o,"TextKey")).Distinct().Count()==3,"distinct Darv native choices");
    for(int i=0;i<opts.Length;i++){
     var o=opts[i];R(o.EnumerateObject().Select(x=>x.Name).Order().SequenceEqual(new[]{"index","reference","TextKey","IsLocked","IsProceed"}.Order())&&o.GetProperty("index").GetInt32()==i&&refs.Add(T(o.GetProperty("reference"),"$ref"))&&identities[T(o.GetProperty("reference"),"$ref")].Type=="EventOption"&&darvPool.Any(key=>T(o,"TextKey")=="DARV.pages.INITIAL.options."+key)&&!o.GetProperty("IsLocked").GetBoolean()&&!o.GetProperty("IsProceed").GetBoolean(),"Darv option actual native shape/identity/pool");
    }
    // Fixed standard A0 second-act profile: eleven valid sets, ten shuffle draws,
    // one DustyTome-presence roll. The event implementation remains native.
    var dr=ev.GetProperty("rng");R(dr.GetProperty("counter").GetInt64()==22,"standard second-act Darv eleven set draws plus shuffle/boolean");foreach(var field in new[]{"s0","s1","s2","s3"})_=dr.GetProperty(field).GetUInt64();return;
   }
   R(T(ev,"model")=="EVENT.TEZCATARA"&&opts.Length==3,"pinned Tezcatara three native initial options");
   string[][] pools=[["VERY_HOT_COCOA","YUMMY_COOKIE","NUTRITIOUS_SOUP"],["BIIIG_HUG","STORYBOOK","TOASTY_MITTENS"],["GOLDEN_COMPASS","PUMPKIN_CANDLE","TOY_BOX","SEAL_OF_GOLD"]];
   for(int i=0;i<opts.Length;i++){
    var o=opts[i];R(o.EnumerateObject().Select(x=>x.Name).Order().SequenceEqual(new[]{"index","reference","TextKey","IsLocked","IsProceed"}.Order())&&o.GetProperty("index").GetInt32()==i&&refs.Add(T(o.GetProperty("reference"),"$ref"))&&identities[T(o.GetProperty("reference"),"$ref")].Type=="EventOption","first native option shape/identity/order");
    R(pools[i].Any(key=>T(o,"TextKey")=="TEZCATARA.pages.INITIAL.options."+key)&&!o.GetProperty("IsLocked").GetBoolean()&&!o.GetProperty("IsProceed").GetBoolean(),"first option actual native pool/source");
   }
   R(opts.Any(o=>!o.GetProperty("IsLocked").GetBoolean()),"actionable first options");
   var rng=ev.GetProperty("rng");R(rng.GetProperty("counter").GetInt64()==3,"Tezcatara native three initial option RNG draws");foreach(var field in new[]{"s0","s1","s2","s3"})_ = rng.GetProperty(field).GetUInt64();
  }
 }
}
