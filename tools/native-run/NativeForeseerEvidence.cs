using System.Text.Json;
namespace OfflineSearchHarness;
// Finite source-reuse observation contract, independent of native dispatch.
internal static class NativeForeseerEvidence
{
 internal static void Review(string directory){
  void Need(bool ok,string why){if(!ok)throw new NativeDataException("Foreseer evidence: "+why);}
  JsonElement Read(string name)=>JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,name))).RootElement;
  bool Same(JsonElement a,JsonElement b)=>NativeBoundaryContract.Equal(a,b);
  string T(JsonElement e,string k)=>e.GetProperty(k).GetString()!;
  var rows=File.ReadLines(Path.Combine(directory,"trace.jsonl")).Select(s=>JsonDocument.Parse(s).RootElement.GetProperty("payload").Clone()).ToArray();
  JsonElement Boundary(JsonElement file)=>rows.Single(e=>T(e,"kind")=="confirmed"&&T(e,"root")==T(file,"root")&&T(e,"context")==T(file,"context")).GetProperty("detail").GetProperty("state");
  if(!File.Exists(Path.Combine(directory,"foreseer-next-act.json"))){Need(!rows.Any(e=>T(e,"kind")=="major-ready-return"),"missing required preview at actual act transition");return;}
  var p=Read("foreseer-next-act.json");
  Need(T(p,"source")=="RandomForeseer/0.13.19/NextActPrediction"&&T(p,"information")=="Oracle"&&p.GetProperty("sourceUnchanged").GetBoolean()&&Same(p.GetProperty("before"),p.GetProperty("after"))&&Same(p.GetProperty("before"),Boundary(p)),"pure preview complete owner/state");
  var actual=Read("foreseer-next-act-actual.json");var state=actual.GetProperty("state");Need(Same(state,Boundary(actual))&&state.GetProperty("act").GetInt32()==1&&T(state,"room")=="MapRoom","actual original next-act full boundary");
  var content=actual.GetProperty("actual");Need(T(content,"ancient")==T(state.GetProperty("naturalMap"),"ancientModel")&&T(content,"boss")==T(state.GetProperty("naturalMap"),"bossModel"),"actual generated content identity");
  if(T(p,"status")=="Available")Need(actual.GetProperty("exact").GetBoolean()&&T(actual,"comparison")=="Compared"&&T(p,"ancient")==T(content,"ancient")&&T(p,"boss")==T(content,"boss"),"real single-node content comparison");
  else Need(T(p,"status")=="UnsupportedUninitializedNextAct"&&actual.GetProperty("exact").ValueKind==JsonValueKind.Null&&T(actual,"comparison")=="NotCompared","unavailable prediction not passed");
  if(!File.Exists(Path.Combine(directory,"foreseer-darv-options.json"))){Need(!rows.Any(e=>T(e,"kind")=="natural-first-node"),"missing required reached Darv option coverage");return;}
  var o=Read("foreseer-darv-options.json");Need(T(o,"information")=="Oracle"&&o.GetProperty("sourceUnchanged").GetBoolean()&&Same(o.GetProperty("before"),o.GetProperty("after"))&&Same(o.GetProperty("before"),Boundary(o))&&T(o,"bagBefore")==T(o,"bagAfter"),"option read full source/RNG/bag purity");
  var options=o.GetProperty("options").EnumerateArray().ToArray();var advertised=o.GetProperty("before").GetProperty("rooms").GetProperty("eventOptions").EnumerateArray().ToArray();
  Need(options.Length==advertised.Length&&options.Length>0,"actual original complete candidate list");
  for(int i=0;i<options.Length;i++)Need(options[i].GetProperty("index").GetInt32()==i&&Same(options[i].GetProperty("reference"),advertised[i].GetProperty("reference"))&&T(options[i],"key")==T(advertised[i],"TextKey")&&options[i].GetProperty("IsLocked").GetBoolean()==advertised[i].GetProperty("IsLocked").GetBoolean()&&T(options[i],"key")=="DARV.pages.INITIAL.options."+T(options[i],"relic").Replace("RELIC.",""),"native Darv option/relic binding");
  var predictions=o.GetProperty("predictions").EnumerateArray().ToArray();Need(predictions.Length==options.Length,"one explicit prediction coverage per option");
  for(int i=0;i<options.Length;i++){
   Need(Same(predictions[i].GetProperty("option"),options[i].GetProperty("reference")),"prediction actual option owner");
   bool bell=T(options[i],"relic")=="RELIC.CALLING_BELL";
   Need(T(predictions[i],"status")== (bell?"AvailableNotSelected":"UnsupportedBySelectedSourceSubset")&&predictions[i].GetProperty("models").GetArrayLength()==(bell?3:0),"honest prediction subset coverage");
  }
  if(!File.Exists(Path.Combine(directory,"darv-selection.json"))){Need(!rows.Any(e=>T(e,"kind")=="natural-ancient-selected"),"missing actual selected Darv record");return;}
  var selected=Read("darv-selection.json");int at=selected.GetProperty("index").GetInt32();
  Need(at>=0&&at<options.Length&&!options[at].GetProperty("IsLocked").GetBoolean()&&!options[at].GetProperty("pickup").GetBoolean()&&T(selected,"relic")==T(options[at],"relic")&&T(selected,"key")==T(options[at],"key")&&Same(selected.GetProperty("option"),options[at].GetProperty("reference")),"actual declared supported option");
  Need(T(selected,"information")=="player-visible-options"&&Same(selected.GetProperty("before"),o.GetProperty("before"))&&Same(selected.GetProperty("after"),Boundary(selected)),"selection actual original complete boundaries");
  NativeDarv.CheckPickup(selected.GetProperty("before"),selected.GetProperty("after"),T(selected,"relic"));
 }
}
