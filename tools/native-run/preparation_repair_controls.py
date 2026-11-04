"""Production-reader controls for the finite native major preparation contracts."""
from pathlib import Path
import copy,hashlib,json,shutil,subprocess

import argparse
args=argparse.ArgumentParser();args.add_argument('--manifest',required=True);args.add_argument('--source',required=True);args.add_argument('--output',required=True);args=args.parse_args()
R=Path(__file__).resolve().parents[2];B=Path(args.manifest).resolve().parent;S=Path(args.source).resolve();O=Path(args.output).resolve();D=B/'bin/Release/net9.0/SimulatorLab.NativeRun.dll'
if O.exists() or not O.is_relative_to(R/'.build/runs'):raise ValueError('fresh Lab evidence output required')
O.mkdir();rd=lambda p:json.loads(p.read_text(encoding='utf-8-sig'));sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper();rows=[];locks={}
def p(r):return r['payload']
def k(r):return p(r)['kind']
def f(r):return p(r)['detail'].get('facts')
def s(r):return p(r)['detail'].get('state')
def owner(rs,typ):return p(next(r for r in rs if k(r)=='dispatch' and f(r)['kind']==typ))['root']
def subset(rs,typ):
 root=owner(rs,typ);hs=[r for r in rs if k(r)=='combat-history' and p(r)['root']==root]
 c=next(r for r in rs if k(r)=='confirmed' and p(r)['root']==root)
 return root,hs,c
def call(name,src,expected,mut=None,group='independent'):
 if any(x['name']==name and x['group']==group for x in rows):return
 d=O/f'case-{len(rows):03d}';d.mkdir();reseal=0
 for file in ['trace.jsonl','trace-seal.json','result.json','process.json']:
  locks[str(src/file)]=sha(src/file);shutil.copy2(src/file,d/file)
 (O/'original-lock.json').write_text(json.dumps(locks,indent=2),encoding='utf-8')
 if mut:
  rs=[json.loads(l) for l in (d/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()];mut(rs)
  (d/'.copy-for-control').write_text('independent arrival binding review',encoding='utf-8')
  (d/'trace.jsonl').write_text('\n'.join(json.dumps(r,ensure_ascii=False) for r in rs)+'\n',encoding='utf-8')
  q=subprocess.run(['pwsh','-NoProfile','-File',str(R/'tools/native-run/reseal-native-control.ps1'),'-Directory',str(d)],capture_output=True,timeout=60)
  reseal=q.returncode;(d/'reseal.log').write_bytes(q.stdout+q.stderr)
 q=subprocess.run(['dotnet',str(D),'--review',str(d)],capture_output=True,timeout=60)
 msg=(q.stdout+q.stderr).decode('utf-8','replace');(d/'production-review.log').write_text(msg,encoding='utf-8')
 row=dict(name=name,group=group,source=str(src),directory=str(d.relative_to(R)),expected=expected,rawExit=q.returncode,resealExit=reseal,passed=reseal==0 and q.returncode==expected and 'Trace chain/order/schema' not in msg,message=msg)
 rows.append(row);(O/'progress.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
 if not row['passed']:print('UNEXPECTED',d.name,name,q.returncode,expected,flush=True)
 if len(rows)%40==0:print('reader calls',len(rows),flush=True)


def one(rs,kind,last=False):
 a=[r for r in rs if k(r)==kind];return a[-1] if last else a[0]
def states_after(rs,kind):
 i=rs.index(one(rs,kind));return [s(r) for r in rs[i:] if isinstance(s(r),dict)]
def setpath(o,path,val):
 for key in path[:-1]:o=o[key]
 o[path[-1]]=copy.deepcopy(val)
# Immutable originals, replay, and observation controls are positive witnesses
# with actual healing, card acquisition, draw and RNG changes untouched.
for name in ['boss-01','boss-02','boss-03','elite-03','natural-01','boss-03-replay','elite-03-replay','boss-03-minimal','boss-03-off','elite-03-minimal','elite-03-off']:
 call('actual '+name,S/name,0,group='positive')
# Exact victory snapshot contradictions, including facts opposite to real combat.
for path,val in [(['hp'],0),(['gold'],9999),(['combat','energy'],99),(['runRng','rngs','Shuffle','s0'],1),(['combat','reviewUnknown'],{'nested':[True,37]}),(['majorCombat','inProgress'],False)]:
 def victory(rs,path=path,val=val):setpath(s(one(rs,'battle-victory-before-cleanup',True)),path,val)
 call('target victory '+'.'.join(path),S/'boss-03',1,victory,'victory')
def live_enemy(rs):
 v=one(rs,'battle-victory-before-cleanup',True);root=p(v)['root'];d=next(r for r in rs if k(r)=='dispatch' and p(r)['root']==root)
 s(v)['combat']['enemies']=copy.deepcopy(s(d)['combat']['enemies'])
call('target victory with actual living Boss restored',S/'boss-03',1,live_enemy,'victory')
# Before EnterNextAct only native ready bookkeeping may have changed.
for path,val in [(['hp'],0),(['gold'],9999),(['node'],'MapCoord (0, 0)'),(['act'],4),(['runRng','rngs','Shuffle','s0'],1),(['inventory','capacity'],88),(['lifecycle','reviewUnknown'],{'nested':[1]})]:
 def corrupt_start(rs,path=path,val=val):setpath(s(one(rs,'act-transition-start')),path,val)
 call('act start '+'.'.join(path),S/'boss-03',1,corrupt_start,'act-start')
# Coedit entire completed suffix; each event individually and copied summaries
# remain internally consistent, but may conflict with pinned native transition.
for path,val in [(['room'],'CombatRoom'),(['node'],'MapCoord (5, 15)'),(['floor'],17),(['block'],999),(['inventory','capacity'],99),(['inventory','slots'],[]),(['lifecycle','mapGeneration'],9),(['rooms','map'],9),(['rooms','stack'],0),(['combat','energy'],99),(['runRng','rngs','Shuffle','s0'],1),(['playerRng','rngs','Rewards','s0'],1),(['eventRng','s0'],1),(['inventory','reviewUnknown'],{'nested':[1]})]:
 def completed(rs,path=path,val=val):
  for st in states_after(rs,'act-transition-completed'):setpath(st,path,val)
  if path==['lifecycle','mapGeneration']:f(one(rs,'major-ready-return'))['map']=val
 call('act completed joint suffix '+'.'.join(path),S/'boss-03',1,completed,'act-complete')
def old_room(rs):
 start=s(one(rs,'act-transition-start'))
 for st in states_after(rs,'act-transition-completed'):
  st['room']=start['room'];st['rooms']=copy.deepcopy(start['rooms']);st['majorCombat']=copy.deepcopy(start['majorCombat'])
call('act completed still original Boss room joint suffix',S/'boss-03',1,old_room,'act-complete')
# Concrete target and rewards must not borrow earlier wins/card identities.
for kind in ['major-battle-result','major-rewards-start','major-rewards-return','major-ready-start']:
 def wrongroom(rs,kind=kind):f(one(rs,kind,True))['room']=copy.deepcopy(f(one(rs,'major-battle-start'))['room'])
 call('wrong earlier room '+kind,S/'boss-03',1,wrongroom,'identity')
def wrong_ready_decision(rs):
 root=p(one(rs,'major-ready-start'))['root']
 for r in rs:
  if p(r)['root']!=root:continue
  if k(r)=='root-request':p(r)['detail']['action']['decision']={'act':8,'map':9}
  if k(r)=='dispatch':f(r)['decision']={'act':8,'map':9}
  if k(r)=='confirmed':f(r)['action']['decision']={'act':8,'map':9}
call('ActReady all action copies wrong act/map',S/'boss-03',1,wrong_ready_decision,'identity')
def target_rewards_missing(rs):
 q=one(rs,'major-rewards-return',True);root=p(q)['root']
 rs[:]=[r for r in rs if not (p(r)['root']==root and k(r).startswith('n3c-') and k(r)!='n3c-boundary')]
call('target reward operation tree missing',S/'boss-03',1,target_rewards_missing,'reward')
def reward_wrong_card(rs):
 root=p(one(rs,'major-rewards-return',True))['root'];r=next(r for r in reversed(rs) if p(r)['root']==root and k(r)=='n3c-return' and isinstance(f(r)['result'].get('acquired'),dict))
 f(r)['result']['acquired']={'$ref':'i1'}
call('target reward outer acquired refers old deck card',S/'boss-03',1,reward_wrong_card,'reward')
def reward_amount(rs):
 root=p(one(rs,'major-rewards-return',True))['root'];r=next(r for r in rs if p(r)['root']==root and k(r)=='n3c-open' and f(r)['kind']=='reward-item' and any(o.get('Kind')=='GoldReward' for o in f(r)['options']))
 for o in f(r)['options']:
  if o.get('Kind')=='GoldReward':o['Amount']+=100
call('target reward Gold amount contradictory',S/'boss-03',1,reward_amount,'reward')
# System lifecycle absence/duplication/order and parent binding controls.
for kind in ['battle-victory-before-cleanup','battle-victory-cleanup','major-battle-result','major-rewards-start','major-rewards-return','major-ready-start','act-transition-start','act-transition-completed','major-ready-return']:
 def missing(rs,kind=kind):rs.remove(one(rs,kind,True))
 call('missing '+kind,S/'boss-03',1,missing,'lifecycle')
 def dupe(rs,kind=kind):r=one(rs,kind,True);rs.insert(rs.index(r)+1,copy.deepcopy(r))
 call('duplicate '+kind,S/'boss-03',1,dupe,'lifecycle')
 def ownerwrong(rs,kind=kind):p(one(rs,kind,True))['root']='a1'
 call('parent '+kind,S/'boss-03',1,ownerwrong,'lifecycle')
def order(rs):
 a=one(rs,'major-ready-return');b=one(rs,'act-transition-completed');rs.remove(a);rs.insert(rs.index(b),a)
call('ready return before actual task completed',S/'boss-03',1,order,'lifecycle')
def orderrewards(rs):
 a=one(rs,'major-rewards-return',True);b=one(rs,'major-rewards-start',True);rs.remove(a);rs.insert(rs.index(b),a)
call('target rewards return before offer',S/'boss-03',1,orderrewards,'lifecycle')
def equivalent(rs):
 def go(v):
  if isinstance(v,dict):
   a=list(v.items())[::-1];v.clear();v.update(a)
   for x in v.values():go(x)
  elif isinstance(v,list):
   for x in v:go(x)
 go(rs)
for name in ['boss-03','elite-03']:call('property order '+name,S/name,0,equivalent,'equivalent')
def one(rs,kind,last=False):
 a=[r for r in rs if k(r)==kind];return a[-1] if last else a[0]
def relic_return(rs):
 root=p(one(rs,'major-rewards-return',True))['root'];ops={f(r)['id']:f(r) for r in rs if k(r)=='n3c-open' and p(r)['root']==root};ans={f(r)['id']:f(r) for r in rs if k(r)=='n3c-answer' and p(r)['root']==root}
 for r in rs:
  if k(r)!='n3c-return' or p(r)['root']!=root:continue
  id=f(r)['id'];o=ops[id];a=ans.get(id)
  if o['kind']=='reward-item' and a and any(v['Handle']==a['handle'] and v['Kind']=='RelicReward' for v in o['options']):return r
 raise ValueError('actual target relic reward missing')
for change in ['model','gold','hp','extension']:
 def joint(rs,change=change):
  r=relic_return(rs);id=f(r)['result']['acquired']['$ref']
  for q in rs[rs.index(r):]:
   st=s(q)
   if not isinstance(st,dict):continue
   relic=next(x for x in st['relics'] if x['id']==id)
   if change=='model':relic['model']='RELIC.BURNING_BLOOD'
   elif change=='extension':relic['wrongNewField']={'nested':[1]}
   else:st[change]+=100
 call('Elite RelicReward coedited suffix '+change,S/'elite-03',1,joint,'relic-result')
def oldref(rs):f(relic_return(rs))['result']['acquired']={'$ref':'i11'}
call('Elite RelicReward acquired refers existing relic',S/'elite-03',1,oldref,'relic-result')
def dropactual(rs):
 r=relic_return(rs);id=f(r)['result']['acquired']['$ref']
 for q in rs[rs.index(r):]:
  st=s(q)
  if isinstance(st,dict):st['relics']=[x for x in st['relics'] if x['id']!=id]
call('Elite RelicReward missing actual acquired instance',S/'elite-03',1,dropactual,'relic-result')
for field in ['hp','enemies']:
 def joint_victory(rs,field=field):
  v=one(rs,'battle-victory-before-cleanup',True);h=[r for r in rs[:rs.index(v)] if k(r)=='combat-history'][-1];d=next(r for r in rs if k(r)=='dispatch' and p(r)['root']==p(v)['root'])
  for r in [h,v]:
   if field=='hp':s(r)['hp']=0
   else:s(r)['combat']['enemies']=copy.deepcopy(s(d)['combat']['enemies'])
 call('joint latest history and victory '+field,S/'boss-03',1,joint_victory,'victory-linked')
for name in ['boss-01-replay','boss-02-replay','natural-01-replay']:call('actual '+name,S/name,0,group='positive')

# Existing independent counterexamples are rerun untouched; new analogues above
# target the new native traces so a missing new witness cannot mask the old defect.
review=R/'.build/runs/n3d3-preparation-independent-review01'
for x in rd(review/'reader-summary.json')['unexpected']:call('original34 '+x['name'],R/x['directory'],1,group='original34')
for mode in ['', '-minimal','-off']:
 src=S/('boss-03'+mode)
 for mutation in ['missing','owner','false','cursor','snapshot','joint-dead','joint-live-enemy']:
  def predicate(rs,mutation=mutation):
   anchors=[r for r in rs if k(r)=='major-victory-condition'];r=anchors[-1];root=p(r)['root']
   if mutation=='missing':rs[:]=[r for r in rs if not(k(r)=='major-victory-condition' and p(r)['root']==root)];return
   if mutation=='owner':p(r)['root']='a1';return
   if mutation=='false':f(r)['ending']=False;return
   if mutation=='cursor':f(r)['index']+=1;return
   if mutation=='snapshot':s(r)['gold']+=100;return
   targets=[x for x in rs if p(x)['root']==root and k(x) in ['major-victory-condition','battle-victory-before-cleanup']]
   before=s(next(x for x in rs if k(x)=='dispatch' and p(x)['root']==root))
   for x in targets:
    if mutation=='joint-dead':s(x)['hp']=0
    else:s(x)['combat']['enemies']=copy.deepcopy(before['combat']['enemies'])
  call('mandatory predicate '+mode+'/'+mutation,src,1,predicate,'predicate')
for field in ['reference-extension','old-reference','registered-type']:
 def room_identity(rs,field=field):
  e=one(rs,'act-transition-completed');ref=s(e)['rooms']['reference']['$ref']
  if field=='registered-type':
   p(next(r for r in rs if k(r)=='identity' and p(r)['detail']['id']==ref))['detail']['type']='CombatRoom';return
  old=s(one(rs,'act-transition-start'))['rooms']['reference']
  for st in states_after(rs,'act-transition-completed'):
   if field=='reference-extension':st['rooms']['reference']['forged']={'nested':[1]}
   else:st['rooms']['reference']=copy.deepcopy(old)
 call('new MapRoom identity '+field,S/'boss-03',1,room_identity,'act-identity')
# Predicate ownership is independent of detailed combat-history output. Reproduce
# the review's four false acceptances and its two detailed rejection controls.
prior_review=R/'.build/runs/n3d3-preparation-repair-independent-review01'
for profile in ['linked','linked-elite']:
 for number in range(3):
  call('original action-owner '+profile+'/'+str(number),prior_review/profile/f'case-{number:03d}',1,group='original-owner6')
for profile in ['boss-03','elite-03']:
 for mode in ['', '-minimal','-off']:
  src=S/(profile+mode)
  for mutation in ['missing-current','previous-action','previous-single','wrong-context','callback-context','clear-owner']:
   def action_owner(rs,mutation=mutation):
    v=one(rs,'battle-victory-before-cleanup',True);root=p(v)['root']
    anchors=[r for r in rs if k(r)=='major-victory-condition' and p(r)['root']==root]
    if mutation in ['missing-current','previous-action','previous-single']:
     for r in anchors:rs.remove(r)
     if mutation=='missing-current':return
     dispatch=next(r for r in rs if k(r)=='dispatch' and p(r)['root']==root)
     previous=[r for r in rs[:rs.index(dispatch)] if k(r)=='confirmed'][-1];position=rs.index(previous)
     for r in anchors[-1:] if mutation=='previous-single' else anchors:
      p(r)['root']=p(previous)['root'];p(r)['context']=p(previous)['context'];rs.insert(position,r);position+=1
    elif mutation=='wrong-context':
     for r in anchors:p(r)['context']='unrelated-combat-context'
    elif mutation=='callback-context':p(v)['context']='unrelated-combat-context'
    else:
     r=next(r for r in rs[rs.index(v):] if k(r)=='history-clearing');p(r)['root']='a1'
   call('current action predicate '+profile+mode+'/'+mutation,src,1,action_owner,'predicate-owner')
  # Real traces already contain repeated true predicates in the same action.
  call('actual repeated predicates '+profile+mode,src,0,group='repeated-native-positive')
  def repeat_same_action(rs):
   v=one(rs,'battle-victory-before-cleanup',True);anchors=[r for r in rs if k(r)=='major-victory-condition' and p(r)['root']==p(v)['root']]
   if len(anchors)<2:raise ValueError('expected actual repeated native predicates')
   rs.insert(rs.index(v),copy.deepcopy(anchors[-1]))
  call('same action additional equivalent predicate '+profile+mode,src,0,repeat_same_action,'repeated-equivalent')
unchanged=all(sha(Path(p))==h for p,h in locks.items());fails=[x for x in rows if not x['passed']]
(O/'reader-summary.json').write_text(json.dumps(dict(buildId=rd(B/'build-manifest.json')['buildId'],calls=len(rows),rows=rows,unexpected=fails,originalsUnchanged=unchanged),ensure_ascii=False,indent=2),encoding='utf-8')
print('complete',len(rows),'unexpected',len(fails),'originals',unchanged,flush=True)
raise SystemExit(int(bool(fails) or not unchanged))
