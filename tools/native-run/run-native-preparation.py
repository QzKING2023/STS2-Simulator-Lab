"""Finite earned-preparation slice; target fixture victories are not full-run wins."""
from pathlib import Path
import argparse,copy,hashlib,json,shutil,subprocess

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--manifest',required=True);ap.add_argument('--output',required=True);a=ap.parse_args()
 R=Path(__file__).resolve().parents[2];B=Path(a.manifest).resolve();O=Path(a.output).resolve();D=B.parent/'bin/Release/net9.0/SimulatorLab.NativeRun.dll'
 if O.exists() or not O.is_relative_to(R/'.build/runs'):raise ValueError('fresh Lab output required')
 O.mkdir();rd=lambda p:json.loads(p.read_text(encoding='utf-8-sig'));sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper();bid=rd(B)['buildId'];checks=[];processes=[];controls=[];locks={}
 def ck(n,v,detail=None):checks.append(dict(name=n,passed=bool(v),detail=detail))
 def wr(n,v):(O/n).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
 def trace(d):return [json.loads(l) for l in (d/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()]
 def p(r):return r['payload']
 def k(r):return p(r)['kind']
 def f(r):return p(r)['detail'].get('facts') or {}
 def s(r):return p(r)['detail'].get('state')
 def run(name,scenario,seed,replay=None,mode='detailed'):
  d=O/name;args=['pwsh','-NoProfile','-File',str(R/'tools/native-run/run-native-slice.ps1'),'-BuildManifest',str(B),'-OutputDirectory',str(d),'-Scenario',scenario,'-Seed','LAB_V0111_NATIVE_DEV_'+seed,'-Observation',mode]
  if replay:args+=['-ReplayPath',str(replay)]
  with (O/(name+'.log')).open('wb') as log:q=subprocess.run(args,stdout=log,stderr=subprocess.STDOUT,timeout=420)
  re=rd(d/'result.json');pr=rd(d/'process.json');z=re['result'];rs=trace(d)
  ck(name+' actual raw/build/isolation',q.returncode==pr['rawExit']==re['rawExit'] and pr['buildId']==bid and pr['exitConsistent'] and not pr['watchdogExpired'] and re['guards']['isolation']['backend']=='MemorySaveStore' and not any(re['guards']['isolation'][x] for x in ['steamInitialized','shouldSave','uiInstance']) and not set(re['loadedAssemblies'])&{'MyMod','BaseLib','CombatSolver'},z)
  ck(name+' result/exit classification',q.returncode==({'Completed':0,'SliceBoundary':0,'Unsupported':2,'BudgetExceeded':3,'InvalidAction':1,'EngineError':1,'DataError':1}[z['Stop']]))
  ck(name+' provenance',pr['scenarioKind']==('natural-run' if scenario=='major-prepared-natural' else 'api-fixture') and pr['executionKind']==('observation-control' if mode!='detailed' else 'frozen-replay' if replay else 'live'))
  v=subprocess.run(['dotnet',str(D),'--review',str(d)],capture_output=True,timeout=60);(d/'production-review.log').write_bytes(v.stdout+v.stderr)
  complete=z['Stop'] in ['Completed','SliceBoundary'];ck(name+' production reader completeness',v.returncode==(0 if complete else 1) and bool(z['DataComplete'])==complete)
  ck(name+' no unexplained execution/data failure',z['Stop'] not in ['EngineError','InvalidAction','DataError'],z.get('Error'))
  if z['Terminal']=='Death':ck(name+' actual Death prefix',sum(k(r)=='battle-loss-confirmed' for r in rs)==1 and z['Confirmed']==z['Dispatched'])
  if z['Stop']=='SliceBoundary':
   target=next(f(r)['target'] for r in rs if k(r)=='major-plan');won=[r for r in rs if k(r)=='major-battle-result' and s(r)['majorCombat']['encounter']==target]
   ck(name+' actual target victory and corresponding native reward',len(won)==1 and any(k(r)=='major-rewards-return' and f(r)['room']==f(won[0])['room'] for r in rs))
   if scenario=='major-prepared-boss':
    end=rd(d/'final-state.json');ck(name+' actual next act task/map',sum(k(r)=='act-transition-completed' for r in rs)==1 and end['act']==1 and end['room']=='MapRoom' and end['lifecycle']['mapGeneration']==2)
  processes.append(dict(id=name,scenario=scenario,seed=seed,observation=mode,expected=q.returncode,rawExit=q.returncode,confirmed=z['Confirmed'],dispatched=z['Dispatched'],stop=z['Stop'],terminal=z['Terminal']));wr('progress.json',dict(processes=processes,failures=[x for x in checks if not x['passed']]));print(name,z['Stop'],z['Terminal'],z['Confirmed'],z['Dispatched'],'reader',v.returncode,flush=True);return d
 def same(a,b):
  for n in ['route.json','choices.json','reward-choices.json','final-state.json']:ck(b.name+' exact full JSON '+n,rd(a/n)==rd(b/n))
 def replay_route(d):
  route=rd(d/'route.json');rs=trace(d);confirmed={p(r)['root'] for r in rs if k(r)=='confirmed'};pending=[r for r in rs if k(r)=='root-request' and p(r)['root'] not in confirmed]
  if not pending:return d/'route.json'
  route.append(dict(action=p(pending[-1])['detail']['action'],before=p(pending[-1])['detail']['before'],after='unconfirmed-not-a-boundary'));out=d/'failure-replay-route.json';out.write_text(json.dumps(route,indent=2),encoding='utf-8');return out
 # Preset samples; never replace a Death/Unsupported with a successful seed.
 for seed in ['01','02','03']:
  a=run('boss-'+seed,'major-prepared-boss',seed);same(a,run('boss-'+seed+'-replay','major-prepared-boss',seed,replay_route(a)))
  if seed=='03':
   for mode in ['minimal','off']:same(a,run('boss-03-'+mode,'major-prepared-boss',seed,replay_route(a),mode))
 for name,scenario,seed in [('elite-03','major-prepared-elite','03'),('natural-01','major-prepared-natural','01')]:
  a=run(name,scenario,seed);same(a,run(name+'-replay',scenario,seed,replay_route(a)))
  if name=='elite-03':
   for mode in ['minimal','off']:same(a,run(name+'-'+mode,scenario,seed,replay_route(a),mode))
 ck('fixed Boss target actually won/next act in preset samples',any(x['scenario']=='major-prepared-boss' and x['stop']=='SliceBoundary' for x in processes))
 ck('fixed Elite target actually won in preset sample',any(x['scenario']=='major-prepared-elite' and x['stop']=='SliceBoundary' for x in processes))

 # Invoke the production reader on resealed copies, never on weakened test substitutes.
 C=[]
 def add(name,mut,expected=1):C.append((name,mut,expected))
 def one(rs,kind):return next(r for r in rs if k(r)==kind)
 for kind in ['prep-chest-exit-start','prep-chest-exit-return','major-ready-start','act-transition-start','act-transition-completed','major-ready-return']:
  add('missing '+kind,lambda rs,kind=kind:rs.remove(one(rs,kind)))
  def repeat(rs,kind=kind):r=one(rs,kind);rs.insert(rs.index(r)+1,copy.deepcopy(r))
  add('duplicate '+kind,repeat)
  def owner(rs,kind=kind):p(one(rs,kind)).update(root='a1',context='wrong')
  add('owner '+kind,owner)
  def state(rs,kind=kind):s(one(rs,kind))['unknownBoundary']={'nested':[1,True]}
  add('complete snapshot '+kind,state)
 def coedit_chest(rs):
  for kind in ['prep-chest-exit-start','prep-chest-exit-return']:s(one(rs,kind))['gold']+=19
 add('joint chest exit states then restored',coedit_chest)
 def closed(rs):f(one(rs,'prep-chest-exit-return'))['closed']=False
 add('original chest not closed',closed)
 for fld in ['id','scenario','policy','features']:
  def profile(rs,fld=fld):p(rs[0])['detail']['preparationProfile'][fld]='wrong'
  add('locked capability '+fld,profile)
 def missing_profile(rs):p(rs[0])['detail'].pop('preparationProfile')
 add('missing capability profile',missing_profile)
 for field in ['hp','gold','deck','relics','potions']:
  def persistent(rs,field=field):
   st=s(one(rs,'act-transition-completed'));st[field]=st[field]+1 if isinstance(st[field],int) else []
  add('cross act persistent '+field,persistent)
 def potion_frame(rs):
  ops={};ans={}
  for r in rs:
   if k(r)=='n3c-open':ops[f(r)['id']]=r
   if k(r)=='n3c-answer':ans[f(r)['id']]=f(r)
   if k(r)=='n3c-return' and f(r)['id'] in ops:
    op=f(ops[f(r)['id']]);a=ans.get(op['id']);result=f(r)['result']
    if op['kind']=='reward-item' and a and not result['success'] and any(o['Handle']==a['handle'] and o['Kind']=='PotionReward' for o in op['options']):s(r)['gold']+=1;return
  raise ValueError('missing actual full potion refusal')
 add('full potion refusal changes unrelated state',potion_frame)
 def wrong_order(rs):
  # After a hole was refilled, coedit only its result projection into append order.
  for r in rs:
   if k(r)=='n3c-return' and len((s(r) or {}).get('potions',[]))==3:
    st=s(r);slots=[x['potion']['$ref'] for x in st['inventory']['slots'] if x['potion']];potions=st['potions']
    if [x['id'] for x in potions]==slots:st['potions']=potions[1:]+potions[:1];return
  raise ValueError('missing actual ordered slots')
 add('potion projection wrong ordered instances',wrong_order)
 def reordered(rs):
  def go(v):
   if isinstance(v,dict):
    a=list(v.items())[::-1];v.clear();v.update(a)
    for x in v.values():go(x)
   elif isinstance(v,list):
    for x in v:go(x)
  go(rs)
 add('legal structural property order',reordered,0)
 for i,(name,mut,expected) in enumerate(C):
  src=O/'boss-03';d=O/f'control-{i:03d}';d.mkdir();(d/'.copy-for-control').write_text('preparation contract control',encoding='utf-8')
  for n in ['trace.jsonl','trace-seal.json','result.json','process.json']:locks[str(src/n)]=sha(src/n);shutil.copy2(src/n,d/n)
  rs=trace(d);mut(rs);(d/'trace.jsonl').write_text('\n'.join(json.dumps(r,ensure_ascii=False) for r in rs)+'\n',encoding='utf-8')
  q=subprocess.run(['pwsh','-NoProfile','-File',str(R/'tools/native-run/reseal-native-control.ps1'),'-Directory',str(d)],capture_output=True,timeout=60);(d/'reseal.log').write_bytes(q.stdout+q.stderr)
  v=subprocess.run(['dotnet',str(D),'--review',str(d)],capture_output=True,timeout=60);msg=(v.stdout+v.stderr).decode('utf-8','replace');(d/'production-review.log').write_text(msg,encoding='utf-8')
  ok=q.returncode==0 and v.returncode==expected and 'Trace chain/order/schema' not in msg;controls.append(dict(name=name,directory=d.name,expected=expected,rawExit=v.returncode,resealExit=q.returncode,passed=ok,message=msg));ck(name,ok,msg)
 ck('all original evidence unchanged',all(sha(Path(p))==h for p,h in locks.items()));wr('original-lock.json',locks);wr('controls.json',controls)
 wr('summary.json',dict(buildId=bid,processCount=len(processes),processes=processes,checkCount=len(checks),checks=checks,failures=[x for x in checks if not x['passed']],controls=len(controls),normalStartFullRun=False,scope='finite legal-preparation fixtures/natural Elite; fixed targets; actual next-act task; no full-run/performance claims'))
 return int(any(not x['passed'] for x in checks))
if __name__=='__main__':raise SystemExit(main())
