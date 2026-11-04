"""Arrival-bound Major histories, verified by actual production reader calls.

Only marked Lab copies are mutated. Real resource/RNG differences are observed,
not synthesized to manufacture a successful native execution.
"""
from pathlib import Path
import argparse,copy,hashlib,json,shutil,subprocess

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--manifest',required=True);ap.add_argument('--source',required=True);ap.add_argument('--output',required=True);a=ap.parse_args()
 R=Path(__file__).resolve().parents[2];B=Path(a.manifest).resolve();S=Path(a.source).resolve();O=Path(a.output).resolve();D=B.parent/'bin/Release/net9.0/SimulatorLab.NativeRun.dll'
 if O.exists() or not O.is_relative_to(R/'.build/runs'):raise ValueError('fresh Lab output required')
 O.mkdir();rd=lambda p:json.loads(p.read_text(encoding='utf-8-sig'));sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper();rows=[];locks={};checks=[]
 def p(r):return r['payload']
 def k(r):return p(r)['kind']
 def f(r):return p(r)['detail'].get('facts')
 def s(r):return p(r)['detail'].get('state')
 def root_for(rs,kind):return p(next(r for r in rs if k(r)=='dispatch' and f(r)['kind']==kind))['root']
 def histories(rs,kind):
  root=root_for(rs,kind);return [r for r in rs if k(r)=='combat-history' and p(r)['root']==root]
 def call(name,src,expected,mut=None,group='transition'):
  d=O/f'control-{len(rows):03d}';d.mkdir();reseal=0
  for file in ['trace.jsonl','trace-seal.json','result.json','process.json']:locks[str(src/file)]=sha(src/file);shutil.copy2(src/file,d/file)
  if mut:
   rs=[json.loads(l) for l in (d/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()];mut(rs)
   (d/'.copy-for-control').write_text('arrival-bound history control',encoding='utf-8');(d/'trace.jsonl').write_text('\n'.join(json.dumps(r,ensure_ascii=False) for r in rs)+'\n',encoding='utf-8')
   q=subprocess.run(['pwsh','-NoProfile','-File',str(R/'tools/native-run/reseal-native-control.ps1'),'-Directory',str(d)],capture_output=True,timeout=60);reseal=q.returncode;(d/'reseal.log').write_bytes(q.stdout+q.stderr)
  q=subprocess.run(['dotnet',str(D),'--review',str(d)],capture_output=True,timeout=60);msg=(q.stdout+q.stderr).decode('utf-8','replace');(d/'production-review.log').write_text(msg,encoding='utf-8')
  rows.append(dict(name=name,group=group,source=str(src),directory=d.name,expected=expected,rawExit=q.returncode,resealExit=reseal,passed=reseal==0 and q.returncode==expected and 'Trace chain/order/schema' not in msg,message=msg))
  (O/'progress.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
  if not rows[-1]['passed']:print('FAIL',name,expected,q.returncode,msg[-300:],flush=True)
  if len(rows)%40==0:print('reader calls',len(rows),flush=True)
 # Original29, phase82, latest8, independent linked mutations, and legal copies.
 for x in rd(R/'.build/runs/n3d3-action-independent-review01/reader-summary.json')['rows']:
  src=R/x['directory'];rs=[json.loads(l) for l in (src/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()]
  legacy=x['expected']==0 and any(k(r)=='battle-victory-before-cleanup' for r in rs) and not any(k(r)=='major-victory-condition' for r in rs)
  if legacy:
   # The old reader/evidence remain immutable. New readers require the native
   # predicate witness; certify new-build replacements instead of accepting gaps.
   if rs[0]['payload']['detail']['buildId']==rd(B)['buildId']:raise ValueError('Current evidence cannot opt into legacy-format handling')
   call('legacy mandatory-witness rejection '+x['name'],src,1,group='legacy-format-rejection')
   rows[-1]['originalExpected']=0
   old=R/'.build/native-build/n3d3-preparation-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll'
   q=subprocess.run(['dotnet',str(old),'--review',str(O/rows[-1]['directory'])],capture_output=True,timeout=60)
   (O/rows[-1]['directory']/'legacy-reader.log').write_bytes(q.stdout+q.stderr);rows[-1]['legacyReaderExit']=q.returncode
   rows[-1]['passed']=rows[-1]['passed'] and q.returncode==0 and 'Major victory complete immutable native predicate anchor' in rows[-1]['message']
   name=x['name'];base='major-boss-1' if 'major-boss-1' in name else 'major-natural-1' if 'major-natural-1' in name else 'major-elite-minimal' if 'minimal' in name else 'major-boss-off' if 'off' in name else 'major-elite-1'
   def legal(rs,name=name):
    def reorder(v):
     if isinstance(v,dict):
      items=list(v.items())[::-1];v.clear();v.update(items)
      for q in v.values():reorder(q)
     elif isinstance(v,list):
      for q in v:reorder(q)
    if 'key order' in name:reorder(rs)
    elif 'coherent extension' in name:
     for r in rs:
      st=s(r)
      if isinstance(st,dict) and isinstance(st.get('majorCombat'),dict):st['majorCombat']['reviewExtension']={'nested':[True,{'value':'same'}]}
   call('current mandatory-witness replacement '+name,S/base,0,legal,group='legacy-current-replacement')
  else:call('review '+x['name'],src,x['expected'],group='review-'+x['group'])
 fields=['act','node','room','encounter','type','shouldGiveRewards','identity-extension','reference-extension']
 for kind in ['Travel','MajorFixture']:
  for scope in ['first','last','all']:
   for field in fields:
    def mutate(rs,kind=kind,scope=scope,field=field):
     hs=histories(rs,kind);hs=hs[:1] if scope=='first' else hs[-1:] if scope=='last' else hs
     dispatch=next(r for r in rs if k(r)=='dispatch' and p(r)['root']==root_for(rs,kind))
     for h in hs:
      st=s(h);mc=st['majorCombat']
      if field=='act':st['act']+=1
      elif field=='node':st['node']='MapCoord (0, 0)'
      elif field=='room':st['rooms']['reference']=copy.deepcopy(s(dispatch)['rooms']['reference']);mc['room']=copy.deepcopy(st['rooms']['reference'])
      elif field=='encounter':mc['encounter']='ENCOUNTER.UNREGISTERED'
      elif field=='type':mc['type']='Boss' if mc['type']!='Boss' else 'Monster'
      elif field=='shouldGiveRewards':mc[field]=not mc[field]
      elif field=='identity-extension':mc['unownedIdentity']={'nested':[1,{'changed':True}]}
      else:st['rooms']['reference']['unownedIdentity']={'nested':True};mc['room']=copy.deepcopy(st['rooms']['reference'])
    call(kind+'/'+scope+'/'+field,S/'major-elite-1',1,mutate)
  for field in ['act','node','room','majorCombat']:
   for corruption in ['missing','null','wrong-type']:
    def malformed(rs,kind=kind,field=field,corruption=corruption):
     st=s(histories(rs,kind)[0]);container=st['rooms'] if field=='room' else st;key='reference' if field=='room' else field
     if corruption=='missing':container.pop(key)
     else:container[key]=None if corruption=='null' else []
    call(kind+'/required/'+field+'/'+corruption,S/'major-elite-1',1,malformed)
  # A restored confirmation must reject coedited history groups too. Conversely
  # changing the confirmation/whole suffix cannot legitimize a cross-act/node.
  for field in ['act','node']:
   def suffix(rs,kind=kind,field=field):
    root=root_for(rs,kind);start=next(i for i,r in enumerate(rs) if k(r)=='combat-history' and p(r)['root']==root)
    for r in rs[start:]:
     if isinstance(s(r),dict):s(r)[field]=1 if field=='act' else 'MapCoord (0, 0)'
   call(kind+'/joint-histories-and-confirmed-suffix/'+field,S/'major-elite-1',1,suffix)
 def reorder(rs):
  def go(v):
   if isinstance(v,dict):
    items=list(v.items())[::-1];v.clear();v.update(items)
    for q in v.values():go(q)
   elif isinstance(v,list):
    for q in v:go(q)
  go(rs)
 call('legal complete property order',S/'major-elite-1',0,reorder,group='equivalent')
 def extension(rs):
  for r in rs:
   st=s(r)
   if isinstance(st,dict) and isinstance(st.get('majorCombat'),dict):st['majorCombat']['stableIdentityExtension']={'nested':[1,True]}
 call('legal coherent immutable identity extension',S/'major-elite-1',0,extension,group='equivalent')
 facts=[]
 for name in ['major-elite-1','major-boss-1','major-natural-1','major-elite-minimal','major-boss-off']:
  call('current normal '+name,S/name,0,group='current-positive')
  rs=[json.loads(l) for l in (S/name/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()];n=diff=0
  for dispatch in [r for r in rs if k(r)=='dispatch' and f(r)['kind'] in ['Travel','MajorFixture']]:
   root=p(dispatch)['root'];cs=[r for r in rs if k(r)=='confirmed' and p(r)['root']==root]
   if not cs:continue
   actual=s(cs[0]);hs=[r for r in rs if k(r)=='combat-history' and p(r)['root']==root]
   for h in hs:
    st=s(h);n+=1;loc=lambda v:(v['act'],v['node'],v['rooms']['reference'])
    checks.append(dict(name=name+'/'+root+'/actual arrival history '+str(n),passed=loc(st)==loc(actual) and all(st['majorCombat'][x]==actual['majorCombat'][x] for x in ['room','encounter','type','shouldGiveRewards'])))
    diff+=int(st!=actual)
  detailed=rd(S/name/'process.json')['observation']=='detailed';checks.append(dict(name=name+' real business changes preserved',passed=(n>0 and diff>0) if detailed else n==0));facts.append(dict(id=name,histories=n,historyStatesDifferentFromConfirmation=diff))
 for name in ['major-natural-02','major-natural-03','major-action-fault','major-action-budget','major-export-fault']:call('current incomplete '+name,S/name,1,group='current-incomplete')
 unchanged=all(sha(Path(p))==h for p,h in locks.items());fails=[x for x in rows+checks if not x['passed']]
 (O/'original-lock.json').write_text(json.dumps(locks,indent=2),encoding='utf-8');(O/'summary.json').write_text(json.dumps(dict(buildId=rd(B)['buildId'],calls=len(rows),rows=rows,checks=checks,nativeWitnesses=facts,originalsUnchanged=unchanged,failures=fails),ensure_ascii=False,indent=2),encoding='utf-8')
 print('calls',len(rows),'checks',len(checks),'failures',len(fails),'originals unchanged',unchanged,flush=True);return int(bool(fails) or not unchanged)
if __name__=='__main__':raise SystemExit(main())
