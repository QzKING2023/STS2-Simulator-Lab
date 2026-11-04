"""Layered natural continuation checks; broad final M1 regression remains a later gate."""
from pathlib import Path
import argparse,copy,hashlib,json,shutil,subprocess
def main():
 ap=argparse.ArgumentParser();ap.add_argument('--manifest',required=True);ap.add_argument('--output',required=True);a=ap.parse_args()
 R=Path(__file__).resolve().parents[2];M=Path(a.manifest).resolve();O=Path(a.output).resolve();D=M.parent/'bin/Release/net9.0/SimulatorLab.NativeRun.dll'
 if O.exists() or not O.is_relative_to(R/'.build/runs'):raise ValueError('fresh Lab output required')
 O.mkdir();rd=lambda p:json.loads(p.read_text('utf-8-sig'));sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper();checks=[];processes=[];controls=[];locks={}
 def ck(n,v,d=None):checks.append(dict(name=n,passed=bool(v),detail=d))
 def wr(n,v):(O/n).write_text(json.dumps(v,ensure_ascii=False,indent=2),'utf-8')
 def trace(d):return [json.loads(l) for l in (d/'trace.jsonl').read_text('utf-8-sig').splitlines()]
 def p(r):return r['payload']
 def k(r):return p(r)['kind']
 def s(r):return p(r)['detail'].get('state')
 def f(r):return p(r)['detail'].get('facts')
 def one(rs,kind):return next(r for r in rs if k(r)==kind)
 def execute(name,seed,scenario='major-natural-second',replay=None,mode='detailed'):
  d=O/name;args=['pwsh','-NoProfile','-File',str(R/'tools/native-run/run-native-slice.ps1'),'-BuildManifest',str(M),'-OutputDirectory',str(d),'-Seed','LAB_V0111_NATIVE_DEV_'+seed,'-Scenario',scenario,'-Observation',mode]
  if replay:args+=['-ReplayPath',str(replay)]
  with (O/(name+'.log')).open('wb') as log:q=subprocess.run(args,stdout=log,stderr=subprocess.STDOUT,timeout=420)
  rr=rd(d/'result.json');z=rr['result'];pr=rd(d/'process.json')
  ck(name+' exact raw/build/outcome',q.returncode==pr['rawExit']==rr['rawExit']==0 and pr['exitConsistent'] and not pr['watchdogExpired'] and pr['buildId']==rd(M)['buildId'] and z['Stop'] in ['Completed','SliceBoundary'],z)
  iso=rr['guards']['isolation'];ck(name+' native offline isolation',iso['backend']=='MemorySaveStore' and not any(iso[x] for x in ['steamInitialized','shouldSave','uiInstance']) and not set(rr['loadedAssemblies'])&{'MyMod','BaseLib','CombatSolver'})
  v=subprocess.run(['dotnet',str(D),'--review',str(d)],capture_output=True,timeout=90);(d/'production-review.log').write_bytes(v.stdout+v.stderr);ck(name+' production integrity',v.returncode==0,(v.stdout+v.stderr).decode('utf-8','replace'))
  rs=trace(d);end=rd(d/'final-state.json');ck(name+' natural origin',pr['scenarioKind']=='natural-run' and not any(k(r)=='fixture-setup-declared' for r in rs))
  if z['Terminal']=='Death':ck(name+' real Death',sum(k(r)=='battle-loss-confirmed' for r in rs)==1 and z['Confirmed']==z['Dispatched'])
  if scenario=='major-natural-second' and z['Stop']=='SliceBoundary':
   boss=[r for r in rs if k(r)=='major-battle-result' and s(r)['majorCombat']['type']=='Boss'];second=[r for r in rs if k(r)=='major-battle-result' and s(r)['act']==1]
   ck(name+' actual same-run Boss/act/Ancient/battle/reward',len(boss)==len(second)==1 and sum(k(r)=='act-transition-completed' for r in rs)==1 and sum(k(r)=='natural-ancient-selected' for r in rs)==1 and end['act']==1 and end['floor']==2 and end['room']=='CombatRoom' and not end['majorCombat']['inProgress'])
   ck(name+' acquired native Soup',sum(r['model']=='RELIC.NUTRITIOUS_SOUP' for r in end['relics'])==1)
   strikes=[c for c in end['deck'] if c['model']=='CARD.STRIKE_IRONCLAD'];ck(name+' persistent real enchantments',bool(strikes) and all(c['native']['enchantment']['id']=={'Category':'ENCHANTMENT','Entry':'TEZCATARAS_EMBER'} and c['native']['enchantment']['amount']==1 for c in strikes))
   battle=second[0];ret=next(r for r in rs if k(r)=='major-rewards-return' and f(r)['room']==f(battle)['room']);ck(name+' second native rewards linked',rs.index(ret)>rs.index(battle))
  processes.append(dict(id=name,scenario=scenario,rawExit=q.returncode,stop=z['Stop'],terminal=z['Terminal'],confirmed=z['Confirmed'],dispatched=z['Dispatched'],farthest={x:end[x] for x in ['act','floor','node','hp','gold']}));wr('progress.json',dict(processes=processes,failures=[c for c in checks if not c['passed']]));print(name,z['Stop'],z['Terminal'],z['Confirmed'],'reader',v.returncode,flush=True);return d
 def same(a,b):
  for fn in ['route.json','choices.json','reward-choices.json','final-state.json']:ck(b.name+' complete replay/RNG '+fn,rd(a/fn)==rd(b/fn))
 for seed in ['01','02','03']:
  d=execute('second-'+seed,seed);same(d,execute('second-'+seed+'-replay',seed,replay=d/'route.json'))
  if seed=='03':
   for mode in ['minimal','off']:same(d,execute('second-03-'+mode,seed,replay=d/'route.json',mode=mode))
 execute('first-boundary-smoke','03','major-natural-boss')
 for sc in ['slice-skip','slice-take']:execute(sc,'01',sc)
 source=O/'second-03';ck('DEV03 continuation reached',rd(source/'result.json')['result']['Stop']=='SliceBoundary')
 C=[]
 def add(n,mut,expected=1):C.append((n,mut,expected))
 for kind in ['natural-ancient-selected','major-complete']:
  add('missing '+kind,lambda rs,kind=kind:rs.remove(one(rs,kind)))
  def duplicate(rs,kind=kind):r=one(rs,kind);rs.insert(rs.index(r)+1,copy.deepcopy(r))
  add('duplicate '+kind,duplicate)
  def owner(rs,kind=kind):p(one(rs,kind)).update(root='a1',context='old')
  add('wrong owner '+kind,owner)
  def state(rs,kind=kind):s(one(rs,kind))['extension']={'new':[1]}
  add('complete state '+kind,state)
 for fld in ['model','key','option']:
  def choice(rs,fld=fld):
   q=f(one(rs,'natural-ancient-selected'));q[fld]={'model':'EVENT.NEOW','key':'TEZCATARA.pages.INITIAL.options.GOLDEN_COMPASS','option':{'$ref':'i1'}}[fld]
  add('Ancient selection '+fld,choice)
 def missing_native(rs):
  for r in rs:
   if s(r):
    for c in s(r)['deck']:c.pop('native',None)
    combat=s(r).get('combat')
    if combat:
     for pile in ['hand','draw','discard','exhaust']:
      for c in combat[pile]:c.pop('native',None)
 add('all required native card data absent',missing_native)
 def model(rs):s(one(rs,'new-run'))['deck'][0]['native']['id']['Entry']='BASH'
 add('serialized model disagrees with card',model)
 def upgrade(rs):s(one(rs,'new-run'))['deck'][0]['native']['current_upgrade_level']=99
 add('serialized upgrade disagrees with card',upgrade)
 def soup(rs):
  start=next(i for i,r in enumerate(rs) if k(r)=='confirmed' and f(r)['action']['kind']=='RoomOption' and f(r)['action']['decision']['key']=='TEZCATARA.pages.INITIAL.options.NUTRITIOUS_SOUP')
  for r in rs[start:]:
   st=s(r)
   if st:
    for c in st['deck']:
     if c['model']=='CARD.STRIKE_IRONCLAD':c['native'].pop('enchantment',None)
 add('joint removal of native Soup enchantments',soup)
 def floor(rs):
  for r in rs:
   if k(r)=='n3c-return' and isinstance(f(r).get('result'),dict):
    result=f(r)['result'];card=result.get('acquiredCard')
    if card and 'native' in card:card['native']['floor_added_to_deck']=999;return
  raise ValueError('missing actual native card acquisition')
 add('card floor stamp differs from source run floor',floor)
 def source_floor(rs):
  for r in rs:
   if k(r)=='n3c-return' and isinstance(f(r).get('result'),dict):
    result=f(r)['result'];card=result.get('acquiredCard')
    if card and 'native' in card:card['runFloor']+=1;return
 add('acquisition crosses source run-floor scope',source_floor)
 def order(rs):
  def visit(x):
   if isinstance(x,dict):
    z=list(x.items())[::-1];x.clear();x.update(z)
    for v in x.values():visit(v)
   elif isinstance(x,list):
    for v in x:visit(v)
  visit(rs)
 add('legal native full data/property order',order,0)
 for i,(name,mut,expected) in enumerate(C):
  d=O/f'control-{i:03d}';d.mkdir();(d/'.copy-for-control').write_text('layered native continuation control','utf-8')
  for fn in ['trace.jsonl','trace-seal.json','result.json','process.json']:locks[str(source/fn)]=sha(source/fn);shutil.copy2(source/fn,d/fn)
  rs=trace(d);mut(rs);(d/'trace.jsonl').write_text('\n'.join(json.dumps(r,ensure_ascii=False) for r in rs)+'\n','utf-8')
  q=subprocess.run(['pwsh','-NoProfile','-File',str(R/'tools/native-run/reseal-native-control.ps1'),'-Directory',str(d)],capture_output=True,timeout=60);(d/'reseal.log').write_bytes(q.stdout+q.stderr)
  v=subprocess.run(['dotnet',str(D),'--review',str(d)],capture_output=True,timeout=90);msg=(v.stdout+v.stderr).decode('utf-8','replace');(d/'production-review.log').write_text(msg,'utf-8')
  ok=q.returncode==0 and v.returncode==expected and 'Trace chain/order/schema' not in msg;controls.append(dict(name=name,directory=d.name,rawExit=v.returncode,expected=expected,passed=ok,message=msg));ck(name,ok,msg)
 ck('control sources preserved',all(sha(Path(p))==h for p,h in locks.items()));wr('original-lock.json',locks);wr('controls.json',controls)
 wr('summary.json',dict(buildId=rd(M)['buildId'],processCount=len(processes),processes=processes,checkCount=len(checks),checks=checks,controls=len(controls),failures=[x for x in checks if not x['passed']],scope='layered natural continuation; full M1 regression pending'))
 return int(any(not c['passed'] for c in checks))
if __name__=='__main__':raise SystemExit(main())

