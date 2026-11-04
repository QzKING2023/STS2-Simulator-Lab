using System.Text.Json;
using System.Text.Json.Nodes;
namespace OfflineSearchHarness;
internal static class NativeEventResourceEvidence {
 static string T(JsonElement e,string k)=>e.GetProperty(k).GetString()!;
 static JsonElement F(JsonElement e)=>e.GetProperty("detail").GetProperty("facts");
 static JsonElement S(JsonElement e)=>e.GetProperty("detail").GetProperty("state");
 static void R(bool ok,string why)=>NativeBoundaryContract.Require(ok,"Event resources: "+why);
 internal static int Review(JsonElement[] rows){int count=0;
  foreach(var group in rows.GroupBy(x=>T(x,"root"))){var a=group.ToArray();var starts=a.Where(x=>T(x,"kind")=="event-resource-start").ToArray();if(starts.Length==0){
    bool confirmedPlain=a.Any(x=>T(x,"kind")=="confirmed")&&a.Any(x=>T(x,"kind")=="room-option-start"&&F(x).TryGetProperty("key",out var key)&&key.GetString()=="THIS_OR_THAT.pages.INITIAL.options.PLAIN");
    R(!confirmedPlain,"confirmed Plain requires original resource ledger");continue;
   }
   R(starts.Length==1,"unique ledger");var start=starts[0];var ends=a.Where(x=>T(x,"kind")=="event-resource-complete").ToArray();var confirms=a.Where(x=>T(x,"kind")=="confirmed").ToArray();if(ends.Length==0){R(confirms.Length==0,"uncompleted ledger cannot confirm");continue;}
   R(ends.Length==1&&confirms.Length==1,"complete unique ledger");var end=ends[0];var report=F(end);var dispatch=a.Single(x=>T(x,"kind")=="dispatch");
   R(T(F(dispatch),"kind")=="RoomOption"&&T(S(start).GetProperty("lifecycle").GetProperty("eventModel"),"model")=="EVENT.THIS_OR_THAT"&&Array.IndexOf(a,dispatch)<Array.IndexOf(a,start)&&Array.IndexOf(a,start)<Array.IndexOf(a,end)&&Array.IndexOf(a,end)<Array.IndexOf(a,confirms[0]),"native selected event and action order");
   NativeBoundaryContract.Snapshot(S(start),S(dispatch),"event ledger dispatch state");
   var commands=a.Where(x=>T(x,"kind")=="event-command-start").ToArray();R(commands.Length==2&&commands.Count(x=>T(F(x),"kind")=="Damage")==1&&commands.Count(x=>T(F(x),"kind")=="GainGold")==1,"required original damage and income");
   var damageStart=commands.Single(x=>T(F(x),"kind")=="Damage");var goldStart=commands.Single(x=>T(F(x),"kind")=="GainGold");
   var damageReturn=a.Single(x=>T(x,"kind")=="event-command-return"&&T(F(x),"id")==T(F(damageStart),"id"));
   R(Array.IndexOf(a,damageReturn)<Array.IndexOf(a,goldStart),"damage task completes before native gold command");
   var deltas=new Dictionary<string,int>{{"gold",0},{"hp",0},{"maxHp",0}};
   foreach(var cmd in commands){string id=T(F(cmd),"id"),kind=T(F(cmd),"kind");var ret=a.Single(x=>T(x,"kind")=="event-command-return"&&T(F(x),"id")==id);R(Array.IndexOf(a,start)<Array.IndexOf(a,cmd)&&Array.IndexOf(a,cmd)<Array.IndexOf(a,ret)&&Array.IndexOf(a,ret)<Array.IndexOf(a,end)&&NativeBoundaryContract.Equal(F(cmd),F(ret)),"original command owner/input/completion");
    var writes=a.Where(x=>T(x,"kind")=="event-resource-write-start"&&T(F(x),"id")==id).ToArray();R(writes.Length==a.Count(x=>T(x,"kind")=="event-resource-write-return"&&T(F(x),"id")==id),"complete setters");
    foreach(var w in writes){var f=F(w);string field=T(f,"field");var wr=a.Single(x=>T(x,"kind")=="event-resource-write-return"&&NativeBoundaryContract.Equal(F(x),f));R(deltas.ContainsKey(field)&&Array.IndexOf(a,cmd)<Array.IndexOf(a,w)&&Array.IndexOf(a,w)<Array.IndexOf(a,wr)&&Array.IndexOf(a,wr)<Array.IndexOf(a,ret)&&NativeBoundaryContract.Equal(f.GetProperty("subject"),F(start).GetProperty("subject")),"original write owner/order");int from=f.GetProperty("from").GetInt32(),to=f.GetProperty("to").GetInt32();R(S(w).GetProperty(field).GetInt32()==from&&S(wr).GetProperty(field).GetInt32()==to,"actual resource write");var frame=JsonNode.Parse(S(w).GetRawText())!;frame[field]=to;NativeBoundaryContract.Snapshot(S(wr),JsonSerializer.SerializeToElement(frame),"resource setter full state");deltas[field]+=to-from;}
    if(kind=="GainGold"){var mod=a.Single(x=>T(x,"kind")=="event-gold-modified"&&T(F(x),"id")==id);R(F(mod).GetProperty("input").GetDecimal()==F(cmd).GetProperty("amount").GetDecimal()&&F(cmd).GetProperty("amount").GetDecimal()==report.GetProperty("goldInput").GetDecimal()&&deltas["gold"]==Math.Max(0,(int)F(mod).GetProperty("modified").GetDecimal()),"native gold input/modification/delta");}
    else{var damage=a.Single(x=>T(x,"kind")=="event-damage-result"&&T(F(x),"id")==id);R(F(cmd).GetProperty("amount").GetDecimal()==report.GetProperty("damageInput").GetDecimal()&&Array.IndexOf(a,damage)<Array.IndexOf(a,ret),"original awaited damage result");}
   }
   foreach(string field in new[]{"gold","hp"})R(S(end).GetProperty(field).GetInt32()-S(start).GetProperty(field).GetInt32()==deltas[field]&&report.GetProperty(field+"Delta").GetInt32()==deltas[field],"net resources from actual setters");
   R(report.GetProperty("damageCommands").GetInt32()==1&&report.GetProperty("goldCommands").GetInt32()==1,"summary counts");count++;
  }return count;
 }
}
