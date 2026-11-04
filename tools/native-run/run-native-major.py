"""Finite first Elite/Boss driver: actual Death is valid, no Boss victory claim."""
from pathlib import Path
import argparse,json,subprocess,hashlib,shutil,copy
def main():
 ap=argparse.ArgumentParser();ap.add_argument('--manifest',required=True);ap.add_argument('--output',required=True);a=ap.parse_args()
 R=Path(__file__).resolve().parents[2];O=Path(a.output).resolve();B=Path(a.manifest).resolve();D=B.parent/'bin/Release/net9.0/SimulatorLab.NativeRun.dll';rd=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
 if not O.is_relative_to(R/'.build/runs') or O.exists():raise ValueError('fresh Lab directory required')
 O.mkdir();m=rd(B);checks=[];processes=[];controls=[];originals={}
 def wr(n,v):(O/n).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
 def ck(n,v,detail=None):checks.append(dict(name=n,passed=bool(v),detail=detail))
 def readtrace(d):return [json.loads(x) for x in (d/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()]
 def p(x):return x['payload']
 def k(x):return p(x)['kind']
 def f(x):return p(x)['detail'].get('facts') or {}
 def s(x):return p(x)['detail'].get('state')
 def one(rs,kind):return next(x for x in rs if k(x)==kind)
 def run(id,case,seed,expected=0,replay=None,mode='detailed'):
  d=O/id;args=['pwsh','-NoProfile','-File',str(R/'tools/native-run/run-native-slice.ps1'),'-BuildManifest',str(B),'-OutputDirectory',str(d),'-Scenario',case,'-Seed','LAB_V0111_NATIVE_DEV_'+seed,'-Observation',mode]
  if replay:args+=['-ReplayPath',str(replay)]
  with (O/(id+'.log')).open('wb') as log:q=subprocess.run(args,stdout=log,stderr=subprocess.STDOUT,timeout=420)
  re=rd(d/'result.json');pr=rd(d/'process.json');z=re['result'];rs=readtrace(d)
  ck(id+' actual raw/build',q.returncode==pr['rawExit']==re['rawExit']==expected and pr['buildId']==m['buildId'] and pr['exitConsistent'] and not pr['watchdogExpired'],z)
  iso=re['guards']['isolation'];ck(id+' isolation',iso['backend']=='MemorySaveStore' and not any(iso[x] for x in ['steamInitialized','shouldSave','uiInstance']) and not set(re['loadedAssemblies'])&{'MyMod','CombatSolver','BaseLib'})
  ck(id+' provenance',pr['scenarioKind']==('fault-control' if 'action-' in case or case=='major-export-fault' else 'natural-run' if case=='major-natural' else 'api-fixture') and pr['executionKind']==('observation-control' if mode!='detailed' else 'frozen-replay' if replay else 'live'))
  q=subprocess.run(['dotnet',str(D),'--review',str(d)],capture_output=True,timeout=60);(d/'production-review.log').write_bytes(q.stdout+q.stderr);ck(id+' production reader',q.returncode==(0 if expected==0 else 1))
  if expected==0:
   ck(id+' real complete Death',z['Terminal']=='Death' and z['Stop']=='Completed' and z['DataComplete'] and z['Confirmed']==z['Dispatched'] and sum(k(x)=='terminal-stamp' for x in rs)==1)
   ck(id+' actual target death',s(one(rs,'battle-loss-confirmed'))['majorCombat']['type']=='Elite' if case=='major-natural' else s(one(rs,'battle-loss-confirmed'))['majorCombat']['type']==('Boss' if case=='major-boss' else 'Elite'))
   ck(id+' first monster actual reward',sum(k(x)=='major-rewards-return' for x in rs)>=1)
  else:
   ck(id+' truthful incomplete',not z['DataComplete'] and not re['trace']['complete'] and z['Stop']==('DataError' if case=='major-export-fault' else {1:'EngineError',2:'Unsupported',3:'BudgetExceeded'}[expected]))
   if case=='major-action-fault':ck(id+' actual native card unconfirmed',z['Dispatched']==z['Confirmed']+1 and any(k(x)=='combat-history' for x in rs))
   if case=='major-action-budget':ck(id+' one actual combat action confirmed',z['Dispatched']==z['Confirmed'] and len([x for x in rs if k(x)=='confirmed' and f(x)['action']['kind'] in ['PlayCard','EndTurn']])==1)
  processes.append(dict(id=id,scenario=case,expected=expected,rawExit=pr['rawExit'],confirmed=z['Confirmed'],dispatched=z['Dispatched'],stop=z['Stop']));wr('progress.json',dict(processes=processes,failures=[x for x in checks if not x['passed']]));print(id,z['Stop'],z['Confirmed'],z['Dispatched'],flush=True);return d
 def same(x,y):
  for name in ['route.json','choices.json','reward-choices.json','final-state.json']:ck(y.name+' complete JSON '+name,rd(x/name)==rd(y/name))
 for case,seed in [('major-elite','03'),('major-boss','03'),('major-natural','01')]:
  first=run(case+'-1',case,seed);same(first,run(case+'-2',case,seed,replay=first/'route.json'))
  for mode in ['minimal','off']:same(first,run(case+'-'+mode,case,seed,replay=first/'route.json',mode=mode))
 for seed in ['02','03']:
  first=run('major-natural-'+seed,'major-natural',seed,2)
  route=rd(first/'route.json');rs=readtrace(first);unconfirmed=[x for x in rs if k(x)=='root-request' and p(x)['root'] not in {p(y)['root'] for y in rs if k(y)=='confirmed'}]
  # A persisted failed root has no confirmed after-stamp. Its real body must fail
  # before confirmation on replay as well; never synthesize a confirmed boundary.
  if unconfirmed:route.append(dict(action=p(unconfirmed[-1])['detail']['action'],before=p(unconfirmed[-1])['detail']['before'],after='unconfirmed-not-a-boundary'))
  failure=first/'failure-replay-route.json';failure.write_text(json.dumps(route,indent=2),encoding='utf-8')
  same(first,run('major-natural-'+seed+'-replay','major-natural',seed,2,replay=failure))
 for case,exit in [('major-action-fault',1),('major-action-budget',3),('major-export-fault',1)]:run(case,case,'03',exit)
 C=[]
 def add(n,fn,expected=1):C.append((n,fn,expected))
 def remove(rs,kind):rs.remove(one(rs,kind))
 def duplicate(rs,kind):x=one(rs,kind);rs.insert(rs.index(x)+1,copy.deepcopy(x))
 for kind in ['major-plan','room-entered','major-battle-start','first-battle-opening','battle-victory-before-cleanup','battle-victory-cleanup','major-battle-result','major-rewards-start','major-rewards-return','room-leave-ready','battle-loss-start','battle-loss-confirmed','death','terminal-stamp','terminal-contract']:
  add('missing '+kind,lambda rs,kind=kind:remove(rs,kind));add('duplicate '+kind,lambda rs,kind=kind:duplicate(rs,kind))
 for kind in ['major-plan','room-entered','major-battle-start','first-battle-opening','battle-victory-before-cleanup','battle-victory-cleanup','major-battle-result','major-rewards-start','major-rewards-return','room-leave-ready','battle-loss-confirmed','death','terminal-stamp','terminal-contract']:
  def owner(rs,kind=kind):p(one(rs,kind)).update(root='a1',context='wrong')
  add('wrong owner '+kind,owner)
  def changed(rs,kind=kind):s(one(rs,kind))['unownedNested']={'array':[5,{'changed':True}]}
  add('complete state '+kind,changed)
 def bad_arrival_type(rs):f(one(rs,'room-entered'))['type']='MerchantRoom'
 add('arrival type contradicts actual room',bad_arrival_type)
 for field in ['WinStarts','WinReturns','RunEndStarts','RunEndReturns','Losses','ActStarts','ActReturns','PlatformRefreshes','Won','Closed']:
  def authority(rs,field=field):
   n=s(one(rs,'battle-loss-start'))['lifecycle']['native'];n[field]=not n[field] if isinstance(n[field],bool) else n[field]+9
  add('callback unobserved native authority '+field,authority)
 def plan(rs):f(one(rs,'major-plan'))['target']='ENCOUNTER.UNREGISTERED'
 add('plan target mismatch',plan)
 def encounter(rs):f(one(rs,'major-battle-start'))['encounter']='ENCOUNTER.UNREGISTERED'
 add('battle actual encounter mismatch',encounter)
 def nooffer(rs):f(one(rs,'major-rewards-return'))['offered']=False
 add('actual rewards denied',nooffer)
 def wrongdest(rs):f(one(rs,'dispatch'))['decision']['act']=99
 add('root decision conflict',wrongdest)
 def coedit(rs):
  start=next(i for i,x in enumerate(rs) if k(x)=='n3c-open' and f(x)['kind']=='reward-set');end=rs.index(one(rs,'major-rewards-return'))
  for x in rs[start:end+1]:
   if isinstance(s(x),dict):s(x)['gold']+=19
 add('coedited complete reward subtree then restored',coedit)
 def hpdeath(rs):s(one(rs,'battle-loss-confirmed'))['hp']=80
 add('native loss HP80',hpdeath)
 def norewards(rs):
  opening=next(x for x in rs if k(x)=='n3c-open' and f(x)['kind']=='reward-set');parent=f(opening)['parent'];start=rs.index(opening);end=next(i for i,x in enumerate(rs) if i>start and k(x)=='n3c-return' and f(x)['id']==f(opening)['id']);rs[start:end+1]=[x for x in rs[start:end+1] if k(x)=='identity']
 add('whole native reward collection missing',norewards)
 def reordered(rs):
  def go(v):
   if isinstance(v,dict):
    items=list(v.items())[::-1];v.clear();v.update(items)
    for x in v.values():go(x)
   elif isinstance(v,list):
    for x in v:go(x)
  go(rs)
 add('legal property order',reordered,0)
 def extension(rs):
  for x in rs:
   if isinstance(s(x),dict):s(x)['coherentExtra']={'preserved':True}
 add('legal complete extension',extension,0)
 for kind in ['room-candidates','room-option-start','room-option-return','room-option-boundary']:
  add('natural missing '+kind,lambda rs,kind=kind:remove(rs,kind));add('natural duplicate '+kind,lambda rs,kind=kind:duplicate(rs,kind))
  def owner(rs,kind=kind):p(one(rs,kind)).update(root='a1',context='wrong')
  add('natural owner '+kind,owner)
  def state(rs,kind=kind):s(one(rs,kind))['unownedChoiceField']={'bad':True}
  add('natural complete state '+kind,state)
 def false_finished(rs):
  f(one(rs,'room-option-return'))['eventFinished']=False;f(one(rs,'room-option-boundary'))['eventFinished']=False
 add('natural paired false completion fact',false_finished)
 for i,(name,mutate,expected) in enumerate(C):
  src=O/('major-natural-1' if name.startswith('natural ') else 'major-elite-1')
  d=O/('control-'+str(i));d.mkdir();(d/'.copy-for-control').write_text('finite major contract control',encoding='utf-8')
  for n in ['trace.jsonl','trace-seal.json','result.json','process.json']:
   originals[str(src/n)]=hashlib.sha256((src/n).read_bytes()).hexdigest().upper();shutil.copy2(src/n,d/n)
  rs=readtrace(d);mutate(rs);(d/'trace.jsonl').write_text('\n'.join(json.dumps(x,ensure_ascii=False) for x in rs)+'\n',encoding='utf-8')
  q=subprocess.run(['pwsh','-NoProfile','-File',str(R/'tools/native-run/reseal-native-control.ps1'),'-Directory',str(d)],capture_output=True,timeout=60);(d/'reseal.log').write_bytes(q.stdout+q.stderr)
  a=subprocess.run(['dotnet',str(D),'--review',str(d)],capture_output=True,timeout=60);(d/'production-review.log').write_bytes(a.stdout+a.stderr);msg=(a.stdout+a.stderr).decode('utf-8',errors='replace');ok=q.returncode==0 and a.returncode==expected and 'Trace chain/order/schema' not in msg
  controls.append(dict(name=name,directory=d.name,expected=expected,rawExit=a.returncode,resealExit=q.returncode,passed=ok,message=msg));ck(name,ok,msg)
 wr('controls.json',controls);wr('original-lock.json',originals);ck('originals unchanged',all(hashlib.sha256(Path(p).read_bytes()).hexdigest().upper()==h for p,h in originals.items()))
 wr('summary.json',dict(buildId=m['buildId'],processCount=len(processes),processes=processes,checkCount=len(checks),checks=checks,controls=len(controls),failures=[x for x in checks if not x['passed']],coverage='first Elite/Boss actual Death; Monster reward; natural target/explicit unsupported; no Boss victory/act-ready claim',normalStartFullRun=False))
 return int(any(not x['passed'] for x in checks))
if __name__=='__main__':raise SystemExit(main())
