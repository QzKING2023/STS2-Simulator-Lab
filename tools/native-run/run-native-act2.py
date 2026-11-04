"""Prespecified natural act-two development matrix, separate from final M1 acceptance."""
from pathlib import Path
import argparse,hashlib,json,subprocess

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--manifest',required=True);ap.add_argument('--output',required=True);a=ap.parse_args()
 R=Path(__file__).resolve().parents[2];M=Path(a.manifest).resolve();O=Path(a.output).resolve();B=M.parent;D=B/'bin/Release/net9.0/SimulatorLab.NativeRun.dll'
 if O.exists() or not O.is_relative_to(R/'.build/runs'):raise ValueError('fresh Lab output required')
 O.mkdir();rd=lambda p:json.loads(p.read_text('utf-8-sig'));checks=[];cases=[]
 def ck(n,v,d=None):checks.append(dict(name=n,passed=bool(v),detail=d))
 def save():
  (O/'summary.json').write_text(json.dumps(dict(buildId=rd(M)['buildId'],processes=cases,checks=checks,failures=[x for x in checks if not x['passed']],fullM1=False),indent=2),'utf-8')
 def execute(name,seed,scenario='major-natural-act2',replay=None,mode='detailed'):
  d=O/name;args=['pwsh','-NoProfile','-File',str(R/'tools/native-run/run-native-slice.ps1'),'-BuildManifest',str(M),'-OutputDirectory',str(d),'-Seed','LAB_V0111_NATIVE_DEV_'+seed,'-Scenario',scenario,'-Observation',mode]
  if replay:args+=['-ReplayPath',str(replay)]
  with (O/(name+'.log')).open('wb') as log:q=subprocess.run(args,stdout=log,stderr=subprocess.STDOUT,timeout=420)
  r=rd(d/'result.json');p=rd(d/'process.json');z=r['result'];end=rd(d/'final-state.json')
  ck(name+' physical result/exit/build',q.returncode==r['rawExit']==p['rawExit']==z['ExitCode'] and p['exitConsistent'] and not p['watchdogExpired'] and p['buildId']==rd(M)['buildId'],z)
  ck(name+' no unexpected execution/data/illegal action error',z['Stop'] in ['Completed','SliceBoundary','Unsupported','BudgetExceeded'],z['Stop'])
  ck(name+' explicit native-only',z['Comparison']=='NotCompared')
  iso=r['guards']['isolation'];ck(name+' isolation',iso['backend']=='MemorySaveStore' and not any(iso[x] for x in ['steamInitialized','shouldSave','uiInstance']) and not set(r['loadedAssemblies'])&{'MyMod','BaseLib','CombatSolver'})
  # Reader writes only derived output; preserve the raw trace/result/process files.
  before={f:hashlib.sha256((d/f).read_bytes()).hexdigest() for f in ['trace.jsonl','trace-seal.json','process.json','result.json']}
  v=subprocess.run(['dotnet',str(D),'--review',str(d)],capture_output=True,timeout=90);(d/'reader.log').write_bytes(v.stdout+v.stderr)
  expected=0 if z['DataComplete'] and z['Stop'] in ['Completed','SliceBoundary'] else 1
  ck(name+' production reader classification',v.returncode==expected,(v.stdout+v.stderr).decode('utf-8','replace'))
  ck(name+' raw evidence unchanged',all(hashlib.sha256((d/f).read_bytes()).hexdigest()==h for f,h in before.items()))
  cases.append(dict(id=name,source=str(d),rawExit=q.returncode,stop=z['Stop'],terminal=z['Terminal'],confirmed=z['Confirmed'],dispatched=z['Dispatched'],readerExit=v.returncode,complete=z['DataComplete'],final={k:end[k] for k in ['act','floor','node','room','hp','gold']}));save();print(name,z['Stop'],z['Terminal'],z['Confirmed'],'reader',v.returncode,flush=True)
  return d
 def same(a,b):
  for f in ['route.json','choices.json','reward-choices.json','final-state.json']:ck(b.name+' exact full replay '+f,rd(a/f)==rd(b/f))
  def boundaries(p):return [r['payload']['detail']['state'] for line in (p/'trace.jsonl').read_text('utf-8-sig').splitlines() if (r:=json.loads(line))['payload']['kind'] in ['dispatch','confirmed']]
  ck(b.name+' full state/RNG boundaries',boundaries(a)==boundaries(b));save()
 for seed in ['01','02','03']:
  d=execute('act2-'+seed,seed);same(d,execute('act2-'+seed+'-replay',seed,replay=d/'route.json'))
  if seed=='03':
   for mode in ['minimal','off']:same(d,execute('act2-03-'+mode,seed,replay=d/'route.json',mode=mode))
 execute('old-natural-second','03','major-natural-second')
 for scenario in ['slice-skip','slice-take']:execute(scenario,'01',scenario)
 save();return int(any(not x['passed'] for x in checks))
if __name__=='__main__':raise SystemExit(main())
