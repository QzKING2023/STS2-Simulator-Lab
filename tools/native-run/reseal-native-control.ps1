param([string]$Directory)
$ErrorActionPreference='Stop'
$allowed=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../.build/runs'))+[IO.Path]::DirectorySeparatorChar
$Directory=[IO.Path]::GetFullPath($Directory)
if(!$Directory.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)-or !(Test-Path (Join-Path $Directory '.copy-for-control'))){throw 'Only explicitly marked Lab evidence copies may be resealed'}
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
public static class ReviewReseal {
 public static void Run(string directory) {
  var path=Path.Combine(directory,"trace.jsonl");
  var rows=File.ReadAllLines(path).Select(x=>JsonNode.Parse(x).AsObject()).ToList();
  var footer=rows.Last()["payload"]["detail"];
  footer["priorCount"]=rows.Count-1;
  footer["counts"]=JsonSerializer.SerializeToNode(rows.Take(rows.Count-1).GroupBy(r=>r["payload"]["kind"].GetValue<string>()).ToDictionary(g=>g.Key,g=>g.Count()));
  string chain="";int seq=0;
  foreach(var row in rows){row["seq"]=++seq;row["prev"]=chain;string raw=JsonSerializer.Serialize(JsonSerializer.SerializeToElement(row["payload"]));chain=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chain+raw)));row["hash"]=chain;}
  string text=string.Join('\n',rows.Select(r=>r.ToJsonString()))+"\n";
  var seal=JsonNode.Parse(File.ReadAllText(Path.Combine(directory,"trace-seal.json")));
  seal["count"]=rows.Count;seal["chain"]=chain;seal["bytes"]=Encoding.UTF8.GetByteCount(text);
  File.WriteAllText(Path.Combine(directory,"trace-seal.json"),seal.ToJsonString());
  var reportPath=Path.Combine(directory,"result.json");var report=JsonNode.Parse(File.ReadAllText(reportPath));report["trace"]=seal.DeepClone();
  File.WriteAllText(reportPath,report.ToJsonString());File.WriteAllText(path,text);
 }
}
'@
[ReviewReseal]::Run($Directory)
