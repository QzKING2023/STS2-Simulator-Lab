"""Registered original event slices, real Resume Tasks and finite production-reader controls."""
from pathlib import Path
import argparse,json,subprocess,hashlib,shutil,importlib.util
spec=importlib.util.spec_from_file_location('event_flow_controls',Path(__file__).with_name('event_flow_controls.py'));mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
def main():
 ap=argparse.ArgumentParser();ap.add_argument('--manifest',required=True);ap.add_argument('--output',required=True);a=ap.parse_args()
 root=Path(__file__).resolve().parents[2];out=Path(a.output).resolve();manifest=Path(a.manifest).resolve();m=json.loads(manifest.read_text(encoding='utf-8-sig'));dll=manifest.parent/'bin/Release/net9.0/SimulatorLab.NativeRun.dll'
 if not out.is_relative_to(root/'.build/runs') or out.exists():raise ValueError('Fresh Lab evidence directory required')
 out.mkdir();checks=[];processes=[];controls=[];originals={}
 rd=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
 def wr(name,v):(out/name).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
 def ck(name,v,detail=None):checks.append(dict(name=name,passed=bool(v),detail=detail))
 def execute(id,case,expected=0,replay=None,mode='detailed'):
  d=out/id;cmd=['pwsh','-NoProfile','-File',str(root/'tools/native-run/run-native-slice.ps1'),'-BuildManifest',str(manifest),'-OutputDirectory',str(d),'-Scenario',case,'-Seed','LAB_V0111_NATIVE_DEV_03','-Observation',mode]
  if replay:cmd+=['-ReplayPath',str(replay)]
  with (out/(id+'.log')).open('wb') as log:p=subprocess.run(cmd,stdout=log,stderr=subprocess.STDOUT,timeout=720)
  r=rd(d/'result.json');z=r['result'];pr=rd(d/'process.json');rows=[json.loads(l)['payload'] for l in (d/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()]
  ck(id+' raw exit/build',p.returncode==r['rawExit']==pr['rawExit']==expected and pr['exitConsistent'] and not pr['watchdogExpired'] and pr['buildId']==m['buildId'],z)
  iso=r['guards']['isolation'];ck(id+' isolation',iso['backend']=='MemorySaveStore' and not any(iso[x] for x in ['steamInitialized','shouldSave','uiInstance']) and not set(r['loadedAssemblies'])&{'MyMod','CombatSolver','BaseLib'})
  ck(id+' origin/mode',pr['scenarioKind']==('fault-control' if expected else 'natural-run' if case=='event-natural' else 'api-fixture') and pr['executionKind']==('observation-control' if mode!='detailed' else 'frozen-replay' if replay else 'live'))
  if expected:
   ck(id+' unconfirmed actual prefix',z['Stop']==('EngineError' if expected==1 else 'BudgetExceeded') and z['Dispatched']==z['Confirmed']+1 and not z['DataComplete'] and not r['trace']['complete'])
   ck(id+' outer does not certify actual task',sum(x['kind']=='ef-resume-start' for x in rows)==1 and sum(x['kind']=='ef-room-resume-return' for x in rows)==1 and not any(x['kind'] in ['ef-resume-return','ef-proceed-return','ef-complete'] for x in rows))
  else:
   q=subprocess.run(['dotnet',str(dll),'--review',str(d)],capture_output=True,timeout=60);(d/'production-review.log').write_bytes(q.stdout+q.stderr);ck(id+' production semantic reader',q.returncode==0,(q.stdout+q.stderr).decode('utf-8',errors='replace'))
   ck(id+' complete slice',z['DataComplete'] and z['Dispatched']==z['Confirmed'] and z['Comparison']=='NotCompared' and z['Stop']=='SliceBoundary' and z['Terminal']=='Ongoing')
   ck(id+' explicit goal',sum(x['kind']=='ef-complete' for x in rows)==1)
   if case=='event-combat':
    ck(id+' actual task and native kill',sum(x['kind']=='ef-resume-start' for x in rows)==sum(x['kind']=='ef-resume-return' for x in rows)==sum(x['kind']=='ef-proceed-return' for x in rows)==1 and not next(x['detail']['facts'] for x in rows if x['kind']=='ef-battle-complete')['timedOut'])
   else:ck(id+' natural next Play',rd(d/'final-state.json')['combat']['phase']=='Play')
  processes.append(dict(id=id,scenario=case,expected=expected,rawExit=pr['rawExit'],confirmed=z['Confirmed'],dispatched=z['Dispatched'],stop=z['Stop']))
  wr('progress.json',dict(processes=processes,failures=[x for x in checks if not x['passed']]));print(id,pr['rawExit'],z['Stop'],flush=True);return d
 def same(a,b):
  for f in ['route.json','choices.json','reward-choices.json','final-state.json']:ck(b.name+' complete repeated '+f,rd(a/f)==rd(b/f))
 for case in ['event-natural','event-combat']:
  first=execute(case+'-1',case);same(first,execute(case+'-2',case,replay=first/'route.json'))
  for mode in ['minimal','off']:same(first,execute(case+'-'+mode,case,replay=first/'route.json',mode=mode))
 for case,expected in [('event-resume-fault',1),('event-resume-budget',3)]:execute(case,case,expected)
 for i,(name,source,mutate,expected) in enumerate(mod.specifications()):
  d=out/('control-'+str(i));d.mkdir();(d/'.copy-for-control').write_text('explicit event review copy',encoding='utf-8')
  for f in ['trace.jsonl','trace-seal.json','result.json','process.json']:
   src=out/source/f;originals[str(src)]=hashlib.sha256(src.read_bytes()).hexdigest().upper();shutil.copy2(src,d/f)
  rows=[json.loads(l) for l in (d/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()];mutate(rows);(d/'trace.jsonl').write_text('\n'.join(json.dumps(x,ensure_ascii=False) for x in rows)+'\n',encoding='utf-8')
  p=subprocess.run(['pwsh','-NoProfile','-File',str(root/'tools/native-run/reseal-native-control.ps1'),'-Directory',str(d)],capture_output=True,timeout=60);(d/'reseal.log').write_bytes(p.stdout+p.stderr)
  q=subprocess.run(['dotnet',str(dll),'--review',str(d)],capture_output=True,timeout=60);message=(q.stdout+q.stderr).decode('utf-8',errors='replace');(d/'production-review.log').write_text(message,encoding='utf-8');ok=p.returncode==0 and q.returncode==expected and 'Trace chain/order/schema error' not in message
  controls.append(dict(name=name,source=source,directory=d.name,expected=expected,resealExit=p.returncode,rawExit=q.returncode,passed=ok,message=message));ck(name+' production control',ok,message)
  if i%20==0:print('controls',i+1,flush=True)
 wr('controls.json',controls);wr('original-packet-lock.json',originals);ck('originals preserved',all(hashlib.sha256(Path(p).read_bytes()).hexdigest().upper()==h for p,h in originals.items()))
 wr('summary.json',dict(buildId=m['buildId'],processCount=len(processes),processes=processes,checkCount=len(checks),checks=checks,controls=len(controls),failures=[x for x in checks if not x['passed']],normalStartFullRun=False))
 return int(any(not x['passed'] for x in checks))
if __name__=='__main__':raise SystemExit(main())
