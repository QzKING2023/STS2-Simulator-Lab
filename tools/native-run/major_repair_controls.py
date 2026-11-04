"""Production-reader controls for the finite 4D3 loss/history/fixture contracts.

Mutates marked evidence copies only. A refusal by hash-chain validation is not
counted as a semantic refusal. Historical reader calls are separate from native runs.
"""
from pathlib import Path
import argparse,copy,hashlib,json,shutil,subprocess

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--manifest',required=True);ap.add_argument('--source',required=True);ap.add_argument('--output',required=True);a=ap.parse_args()
 R=Path(__file__).resolve().parents[2];O=Path(a.output).resolve();B=Path(a.manifest).resolve();S=Path(a.source).resolve();D=B.parent/'bin/Release/net9.0/SimulatorLab.NativeRun.dll'
 if O.exists() or not O.is_relative_to(R/'.build/runs'):raise ValueError('fresh Lab output required')
 O.mkdir();rd=lambda p:json.loads(p.read_text(encoding='utf-8-sig'));sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper();rows=[];locks={};tests=[]
 def p(r):return r['payload']
 def k(r):return p(r)['kind']
 def f(r):return p(r)['detail'].get('facts')
 def s(r):return p(r)['detail'].get('state')
 def one(rs,kind):return next(r for r in rs if k(r)==kind)
 def add(name,fn,expected=1,source='major-elite-1'):tests.append((name,fn,expected,S/source))
 def edit(rs,kind,path,value):
  x=s(one(rs,kind))
  for part in path[:-1]:x=x[part]
  x[path[-1]]=value(x[path[-1]])
 for kind in ['battle-loss-start','battle-loss-confirmed']:
  for name,path,change in [('gold',['gold'],lambda x:x+19),('hp',['hp'],lambda x:80),('block',['block'],lambda x:x+1),('deck',['deck'],lambda x:x[::-1]),('nested',['lifecycle'],lambda x:dict(x,unowned={'v':[1,2]})),('encounter',['majorCombat','encounter'],lambda x:'ENCOUNTER.VANTOM_BOSS'),('room-type',['majorCombat','type'],lambda x:'Boss'),('rng',['runRng','rngs','UpFront','counter'],lambda x:x+1)]:
   add(kind+'/'+name,lambda rs,kind=kind,path=path,change=change:edit(rs,kind,path,change))
 def pair(rs):
  for kind in ['battle-loss-start','battle-loss-confirmed']:s(one(rs,kind))['gold']+=19
 add('paired callbacks gold restored at confirm',pair)
 def suffix(rs):
  start=rs.index(one(rs,'battle-loss-start'))
  for r in rs[start:]:
   if isinstance(s(r),dict):s(r)['gold']+=19
 add('all death tail coedited against actual preceding history',suffix)
 for kind in ['battle-loss-start','battle-loss-confirmed']:
  add('missing '+kind,lambda rs,kind=kind:rs.remove(one(rs,kind)))
  add('duplicate '+kind,lambda rs,kind=kind:rs.insert(rs.index(one(rs,kind))+1,copy.deepcopy(one(rs,kind))))
  add('owner '+kind,lambda rs,kind=kind:p(one(rs,kind)).update(root='a1',context='other'))
 add('terminal contract false type',lambda rs:f(one(rs,'terminal-contract')).update(terminal='RunVictory'))
 add('terminal native facts contradiction',lambda rs:f(one(rs,'terminal-stamp'))['native'].update(Losses=0,Won=True))
 add('terminal source contradiction',lambda rs:f(one(rs,'terminal-stamp')).update(scope='natural-run'))
 add('retail loss false claim',lambda rs:f(one(rs,'terminal-stamp')).update(retailLossFinalization=True))
 for kind in ['combat-history','history-clearing','history-checkpoint']:
  add(kind+' wrong root',lambda rs,kind=kind:p(one(rs,kind)).update(root='a1'))
  add(kind+' wrong context',lambda rs,kind=kind:p(one(rs,kind)).update(context='other-room'))
 def late_history(rs):
  r=one(rs,'combat-history');rs.remove(r);i=next(i for i,x in enumerate(rs) if k(x)=='confirmed' and p(x)['root']==p(r)['root']);rs.insert(i+1,r)
 add('history after owning root confirmed',late_history)
 add('fixture missing declaration',lambda rs:rs.remove(one(rs,'fixture-setup-declared')))
 add('fixture duplicate declaration',lambda rs:rs.insert(rs.index(one(rs,'fixture-setup-declared'))+1,copy.deepcopy(one(rs,'fixture-setup-declared'))))
 for field,value in [('name','major-boss'),('fixtureKind','other'),('encounter','ENCOUNTER.VANTOM_BOSS')]:add('fixture wrong '+field,lambda rs,field=field,value=value:f(one(rs,'fixture-setup-declared')).update({field:value}))
 add('fixture declaration nested state',lambda rs:s(one(rs,'fixture-setup-declared'))['lifecycle'].update(unowned={'v':1}))
 def early_fixture(rs):
  r=one(rs,'fixture-setup-declared');rs.remove(r);rs.insert(rs.index(one(rs,'major-plan'))+1,r)
 add('fixture declaration before prelude',early_fixture)
 def changed_root(rs):
  for r in rs:
   detail=p(r)['detail'];kind=k(r);act=detail.get('action') if kind=='root-request' else f(r) if kind=='dispatch' else f(r).get('action') if kind=='confirmed' else None
   if act and act['kind']=='MajorFixture':act['decision'].update(encounter='ENCOUNTER.VANTOM_BOSS',type='Boss')
  f(one(rs,'fixture-setup-declared'))['encounter']='ENCOUNTER.VANTOM_BOSS'
 add('fixture coedited root and declaration',changed_root)
 def reversed_properties(rs):
  def walk(x):
   if isinstance(x,dict):
    items=list(x.items())[::-1];x.clear();x.update(items)
    for v in x.values():walk(v)
   elif isinstance(x,list):
    for v in x:walk(v)
  walk(rs)
 add('legal reordered properties',reversed_properties,0)
 def consistent_extension(rs):
  for r in rs:
   if isinstance(s(r),dict):s(r)['lifecycle']['consistentExtra']={'v':[1,{'yes':True}]}
 add('legal complete consistent nested extension',consistent_extension,0)
 for name in ['major-elite-1','major-boss-1','major-natural-1','major-elite-minimal','major-boss-off']:add('actual normal '+name,None,0,name)
 for name in ['major-natural-02','major-natural-03','major-action-fault','major-action-budget','major-export-fault']:add('actual incomplete '+name,None,1,name)
 def call(name,fn,expected,src,group):
  dest=O/f'control-{len(rows):03d}';dest.mkdir()
  for file in ['trace.jsonl','trace-seal.json','result.json','process.json']:
   locks[str(src/file)]=sha(src/file);shutil.copy2(src/file,dest/file)
  reseal=0
  if fn:
   rs=[json.loads(l) for l in (dest/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()];fn(rs);(dest/'trace.jsonl').write_text('\n'.join(json.dumps(r,ensure_ascii=False) for r in rs)+'\n',encoding='utf-8');(dest/'.copy-for-control').write_text('4D3 repair control',encoding='utf-8')
   q=subprocess.run(['pwsh','-NoProfile','-File',str(R/'tools/native-run/reseal-native-control.ps1'),'-Directory',str(dest)],capture_output=True,timeout=60);reseal=q.returncode;(dest/'reseal.log').write_bytes(q.stdout+q.stderr)
  q=subprocess.run(['dotnet',str(D),'--review',str(dest)],capture_output=True,timeout=60);msg=(q.stdout+q.stderr).decode('utf-8',errors='replace');(dest/'production-review.log').write_text(msg,encoding='utf-8')
  rows.append(dict(name=name,group=group,directory=dest.name,expected=expected,rawExit=q.returncode,resealExit=reseal,passed=reseal==0 and q.returncode==expected and 'Trace chain/order/schema' not in msg,message=msg))
  (O/'progress.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
  if not rows[-1]['passed']:print('FAIL',name,expected,q.returncode,msg[-200:],flush=True)
 for args in tests:call(*args,'systematic')
 print('systematic',len(tests),flush=True)
 # Old counterexamples retain their original source/build identity. These are
 # compatibility reader controls, not new native runs from the final build.
 old=R/'.build/runs/n3d3-independent-review01/summary.json'
 for x in rd(old)['unexpected']:call('original '+x['name'],None,1,R/x['directory'],'original19')
 for x in rd(R/'.build/runs/n3d3-work01/prior-review-gate02.json')['rows']:call('prior '+x['name'],None,x['expected'],R/x['directory'],'prior-event79')
 unchanged=all(sha(Path(p))==h for p,h in locks.items());fails=[x for x in rows if not x['passed']]
 (O/'original-lock.json').write_text(json.dumps(locks,indent=2),encoding='utf-8')
 (O/'summary.json').write_text(json.dumps(dict(readerBuildId=rd(B)['buildId'],calls=len(rows),systematic=len(tests),original19=19,priorEvent79=79,rows=rows,originalsUnchanged=unchanged,failures=fails),ensure_ascii=False,indent=2),encoding='utf-8')
 print('calls',len(rows),'failures',len(fails),'originalsUnchanged',unchanged,flush=True)
 return int(bool(fails) or not unchanged)
if __name__=='__main__':raise SystemExit(main())
