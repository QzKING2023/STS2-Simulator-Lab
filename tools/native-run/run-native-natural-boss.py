"""Natural Boss -> next-act first decision, fixed DEV01/02/03; no fixture entry."""
from pathlib import Path
import argparse,collections,copy,hashlib,json,shutil,subprocess

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--manifest',required=True);ap.add_argument('--output',required=True);a=ap.parse_args()
 R=Path(__file__).resolve().parents[2];M=Path(a.manifest).resolve();O=Path(a.output).resolve();D=M.parent/'bin/Release/net9.0/SimulatorLab.NativeRun.dll'
 if O.exists() or not O.is_relative_to(R/'.build/runs'):raise ValueError('fresh Lab output required')
 O.mkdir();rd=lambda p:json.loads(p.read_text('utf-8-sig'));sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper();checks=[];processes=[];controls=[];locks={}
 def ck(n,v,detail=None):checks.append(dict(name=n,passed=bool(v),detail=detail))
 def wr(n,v):(O/n).write_text(json.dumps(v,ensure_ascii=False,indent=2),'utf-8')
 def trace(d):return [json.loads(l) for l in (d/'trace.jsonl').read_text('utf-8-sig').splitlines()]
 def p(x):return x['payload']
 def k(x):return p(x)['kind']
 def s(x):return p(x)['detail'].get('state')
 def f(x):return p(x)['detail'].get('facts')
 def one(rs,kind):return next(r for r in rs if k(r)==kind)
 def execute(name,seed,replay=None,mode='detailed',expected_failure=False):
  d=O/name;args=['pwsh','-NoProfile','-File',str(R/'tools/native-run/run-native-slice.ps1'),'-BuildManifest',str(M),'-OutputDirectory',str(d),'-Seed','LAB_V0111_NATIVE_DEV_'+seed,'-Scenario','major-natural-boss','-Observation',mode]
  if replay:args+=['-ReplayPath',str(replay)]
  if expected_failure:args+=['-InputControl','invalid-route']
  with (O/(name+'.log')).open('wb') as log:q=subprocess.run(args,stdout=log,stderr=subprocess.STDOUT,timeout=420)
  re=rd(d/'result.json');pr=rd(d/'process.json');z=re['result'];rs=trace(d);complete=z['Stop'] in ['Completed','SliceBoundary']
  ck(name+' raw exit/build',q.returncode==re['rawExit']==pr['rawExit'] and pr['exitConsistent'] and not pr['watchdogExpired'] and pr['buildId']==rd(M)['buildId'],z)
  ck(name+' failure classification',z['Stop']=='InvalidAction' and q.returncode==1 if expected_failure else complete and q.returncode==0,z)
  iso=re['guards']['isolation'];ck(name+' isolated native host',iso['backend']=='MemorySaveStore' and not any(iso[x] for x in ['steamInitialized','shouldSave','uiInstance']) and not set(re['loadedAssemblies'])&{'MyMod','BaseLib','CombatSolver'})
  ck(name+' provenance',pr['scenarioKind']==('fault-control' if expected_failure else 'natural-run') and pr['executionKind']==('observation-control' if mode!='detailed' else 'frozen-replay' if replay else 'live'))
  ck(name+' no fixture',not any(k(r)=='fixture-setup-declared' or k(r)=='dispatch' and f(r)['kind']=='MajorFixture' for r in rs))
  v=subprocess.run(['dotnet',str(D),'--review',str(d)],capture_output=True,timeout=60);(d/'production-review.log').write_bytes(v.stdout+v.stderr);ck(name+' production contract',v.returncode==(0 if complete else 1),(v.stdout+v.stderr).decode('utf-8','replace'))
  if z['Terminal']=='Death':ck(name+' actual native Death',sum(k(r)=='battle-loss-confirmed' for r in rs)==1 and z['Confirmed']==z['Dispatched'])
  if z['Stop']=='SliceBoundary':
   victories=[r for r in rs if k(r)=='major-battle-result' and s(r)['majorCombat']['type']=='Boss'];end=rd(d/'final-state.json')
   ck(name+' actual generated Boss',len(victories)==1 and s(victories[0])['majorCombat']['encounter']==s(victories[0])['naturalMap']['bossModel'])
   ck(name+' reward before original act task',len([r for r in rs if k(r)=='major-rewards-return' and f(r)['room']==f(victories[0])['room']])==1 and sum(k(r)=='act-transition-completed' for r in rs)==1)
   ck(name+' next first actionable Ancient',sum(k(r)=='natural-first-node' for r in rs)==1 and end['act']==1 and end['node']==end['naturalMap']['start'] and end['room']=='EventRoom' and end['hp']==end['maxHp'] and end['rooms']['eventOptions'] and not end['lifecycle']['eventModel']['IsFinished'])
  processes.append(dict(id=name,seed=seed,rawExit=q.returncode,stop=z['Stop'],terminal=z['Terminal'],confirmed=z['Confirmed'],dispatched=z['Dispatched']));wr('progress.json',dict(processes=processes,failures=[x for x in checks if not x['passed']]));print(name,z['Stop'],z['Terminal'],z['Confirmed'],z['Dispatched'],'reader',v.returncode,flush=True);return d
 def same(a,b):
  for fn in ['route.json','choices.json','reward-choices.json','final-state.json']:ck(b.name+' full exact '+fn,rd(a/fn)==rd(b/fn))
 for seed in ['01','02','03']:
  d=execute('natural-'+seed,seed);same(d,execute('natural-'+seed+'-replay',seed,d/'route.json'))
  if seed=='03':
   for mode in ['minimal','off']:same(d,execute('natural-03-'+mode,seed,d/'route.json',mode))
 source=O/'natural-03';rs=trace(source);ck('DEV03 actual natural goal reached',rd(source/'result.json')['result']['Stop']=='SliceBoundary')
 if rd(source/'result.json')['result']['Stop']!='SliceBoundary':wr('summary.json',dict(processes=processes,checks=checks,failures=[x for x in checks if not x['passed']]));return 1
 bad=rd(source/'route.json');bad[-1]['action']['decision']['to']='MapCoord (-99, -99)';inputs=O/'invalid-start-inputs';inputs.mkdir();(inputs/'route.json').write_text(json.dumps(bad,indent=2),'utf-8')
 for fn in ['choices.json','reward-choices.json']:shutil.copy2(source/fn,inputs/fn)
 d=execute('invalid-start-replay','03',inputs/'route.json',expected_failure=True);ck('invalid start retained confirmed prefix/no dispatch',rd(d/'result.json')['result']['Confirmed']==rd(source/'result.json')['result']['Confirmed']-1==rd(d/'result.json')['result']['Dispatched'])
 C=[]
 def add(name,mut,expected=1):C.append((name,mut,expected))
 for kind in ['natural-start-candidate','natural-first-node']:
  add('missing '+kind,lambda rs,kind=kind:rs.remove(one(rs,kind)))
  def dup(rs,kind=kind):x=one(rs,kind);rs.insert(rs.index(x)+1,copy.deepcopy(x))
  add('duplicate '+kind,dup)
  def owner(rs,kind=kind):p(one(rs,kind)).update(root='a1',context='old')
  add('owner '+kind,owner)
  def state(rs,kind=kind):s(one(rs,kind))['unknownExtension']={'nested':[True]}
  add('complete state '+kind,state)
 def suffix(rs,field):
  start=next(i for i,r in enumerate(rs) if k(r)=='confirmed' and f(r)['action']['kind']=='NextActStart')
  for r in rs[start:]:
   st=s(r)
   if st is None:continue
   if field=='hp':st['hp']=0
   elif field=='gold':st['gold']+=17
   elif field=='deck':st['deck']=st['deck'][::-1]
   elif field=='rng':st['runRng']['rngs']['Shuffle']['s0']^=1
   elif field=='node':st['node']='MapCoord (1, 1)'
   elif field=='room':st['room']='MapRoom'
   elif field=='model':st['lifecycle']['eventModel']['model']='EVENT.NEOW'
   elif field=='finished':st['lifecycle']['eventModel']['IsFinished']=True
   elif field=='rooms-extension':st['rooms']['unknown']={'extra':1}
   elif field=='map-extension':st['naturalMap']['unknown']={'extra':1}
   elif field=='options':st['rooms']['eventOptions'].pop()
   elif field=='option-source':st['rooms']['eventOptions'][0]['TextKey']='NEOW.fake'
   elif field=='option-instance':st['rooms']['eventOptions'][0]['reference']=copy.deepcopy(st['rooms']['eventOptions'][1]['reference'])
   elif field=='option-order':st['rooms']['eventOptions'][0]['index']=99
 for field in ['hp','gold','deck','rng','node','room','model','finished','rooms-extension','map-extension','options','option-source','option-instance','option-order']:add('joint first-node suffix '+field,lambda rs,field=field:suffix(rs,field))
 for field in ['coord','type','act']:
  def wrong(rs,field=field):f(one(rs,'natural-start-candidate'))[field]={'coord':'bad','type':'Monster','act':0}[field]
  add('candidate '+field,wrong)
 def noedge(rs):f(one(rs,'map-candidates')).pop()
 add('missing actual edge',noedge)
 def edgetype(rs):f(one(rs,'map-candidates'))[0]['type']='Boss'
 add('wrong edge type',edgetype)
 def graph(rs):s(one(rs,'major-plan'))['naturalMap']['points'][0]['children']=[]
 add('changed immutable graph',graph)
 def stale_map(rs):
  start=next(i for i,r in enumerate(rs) if k(r)=='act-transition-completed');old=s(one(rs,'major-plan'))['naturalMap']['reference']
  for r in rs[start:]:
   if s(r):s(r)['naturalMap']['reference']=copy.deepcopy(old)
 add('joint next map old identity',stale_map)
 def old_first_instance(rs,what):
  begin=next(i for i,r in enumerate(rs) if k(r)=='confirmed' and f(r)['action']['kind']=='NextActStart')
  oldroom=next(s(r)['rooms']['reference'] for r in rs[:begin] if k(r)=='room-entered' and s(r)['room']=='EventRoom')
  oldoptions=[p(r)['detail']['id'] for r in rs[:begin] if k(r)=='identity' and p(r)['detail']['type']=='EventOption'][:3]
  for r in rs[begin:]:
   if s(r):
    if what=='room':s(r)['rooms']['reference']=copy.deepcopy(oldroom)
    else:
     for i,o in enumerate(s(r)['rooms']['eventOptions']):o['reference']={'$ref':oldoptions[i]}
   if what=='room' and k(r) in ['room-entered','natural-first-node']:f(r)['room']=copy.deepcopy(oldroom)
 for what in ['room','options']:add('joint first-node stale '+what,lambda rs,what=what:old_first_instance(rs,what))
 def generated_boss(rs):
  for r in rs:
   if s(r) and s(r).get('majorCombat',{}).get('type')=='Boss':s(r)['majorCombat']['encounter']='ENCOUNTER.CEREMONIAL_BEAST_BOSS'
 add('joint Boss differs from generated map',generated_boss)
 def reorder(rs):
  def go(x):
   if isinstance(x,dict):
    pairs=list(x.items())[::-1];x.clear();x.update(pairs)
    for v in x.values():go(v)
   elif isinstance(x,list):
    for v in x:go(v)
  go(rs)
 add('legal complete native changes and property order',reorder,0)
 for i,(name,mut,expected) in enumerate(C):
  d=O/f'control-{i:03d}';d.mkdir();(d/'.copy-for-control').write_text('natural run production control','utf-8')
  for fn in ['trace.jsonl','trace-seal.json','result.json','process.json']:locks[str(source/fn)]=sha(source/fn);shutil.copy2(source/fn,d/fn)
  rr=trace(d);mut(rr);(d/'trace.jsonl').write_text('\n'.join(json.dumps(x,ensure_ascii=False) for x in rr)+'\n','utf-8')
  q=subprocess.run(['pwsh','-NoProfile','-File',str(R/'tools/native-run/reseal-native-control.ps1'),'-Directory',str(d)],capture_output=True,timeout=60);(d/'reseal.log').write_bytes(q.stdout+q.stderr)
  v=subprocess.run(['dotnet',str(D),'--review',str(d)],capture_output=True,timeout=60);msg=(v.stdout+v.stderr).decode('utf-8','replace');(d/'production-review.log').write_text(msg,'utf-8');ok=q.returncode==0 and v.returncode==expected and 'Trace chain/order/schema' not in msg
  controls.append(dict(name=name,directory=d.name,expected=expected,rawExit=v.returncode,passed=ok,message=msg));ck(name,ok,msg)
 ck('original sources preserved',all(sha(Path(p))==h for p,h in locks.items()));wr('original-lock.json',locks);wr('controls.json',controls)
 wr('summary.json',dict(buildId=rd(M)['buildId'],processCount=len(processes),processes=processes,checkCount=len(checks),checks=checks,controls=len(controls),failures=[x for x in checks if not x['passed']],scope='natural first Boss, original next-act task and first Ancient decision; choice not taken; not full run'))
 return int(any(not x['passed'] for x in checks))
if __name__=='__main__':raise SystemExit(main())
