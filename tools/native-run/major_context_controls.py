"""Finite Major combat-location contracts, via the actual production reader.

Only marked copies are mutated; old evidence keeps its original build identity.
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
 def s(r):return p(r)['detail'].get('state')
 def f(r):return p(r)['detail'].get('facts')
 def one(rs,kind):return next(r for r in rs if k(r)==kind)
 def history(rs):
  active=''
  for r in rs:
   if k(r)=='dispatch':active=f(r)['kind']
   if k(r)=='combat-history' and active in ['PlayCard','EndTurn','UsePotion']:return r
   if k(r)=='confirmed':active=''
  raise ValueError('no combat-owned history')
 def call(name,src,expected,mut=None,group='location'):
  d=O/f'control-{len(rows):03d}';d.mkdir();reseal=0
  for file in ['trace.jsonl','trace-seal.json','result.json','process.json']:locks[str(src/file)]=sha(src/file);shutil.copy2(src/file,d/file)
  if mut:
   rs=[json.loads(l) for l in (d/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()];mut(rs)
   (d/'.copy-for-control').write_text('finite combat location control',encoding='utf-8');(d/'trace.jsonl').write_text('\n'.join(json.dumps(r,ensure_ascii=False) for r in rs)+'\n',encoding='utf-8')
   q=subprocess.run(['pwsh','-NoProfile','-File',str(R/'tools/native-run/reseal-native-control.ps1'),'-Directory',str(d)],capture_output=True,timeout=60);reseal=q.returncode;(d/'reseal.log').write_bytes(q.stdout+q.stderr)
  q=subprocess.run(['dotnet',str(D),'--review',str(d)],capture_output=True,timeout=60);msg=(q.stdout+q.stderr).decode('utf-8','replace');(d/'production-review.log').write_text(msg,encoding='utf-8')
  rows.append(dict(name=name,group=group,source=str(src),directory=d.name,expected=expected,rawExit=q.returncode,resealExit=reseal,passed=reseal==0 and q.returncode==expected and 'Trace chain/order/schema' not in msg,message=msg))
  (O/'progress.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
  if not rows[-1]['passed']:print('FAIL',name,expected,q.returncode,msg[-200:],flush=True)
  if len(rows)%40==0:print('reader calls',len(rows),flush=True)
 # Includes original19, the four new counterexamples, real positive witnesses,
 # and all prior event/mutation controls. Historical packages are not new runs.
 old=rd(R/'.build/runs/n3d3-repair-independent-review01/reader-summary.json')['rows']
 for x in old:call('review '+x['name'],R/x['directory'],x['expected'],group='review-'+x['group'])
 def change(st,field):
  if field=='act':st['act']+=1
  elif field=='node':st['node']='MapCoord (0, 0)'
  elif field=='room':st['rooms']['reference']={'$ref':'other-room'};st['majorCombat']['room']={'$ref':'other-room'}
  else:st['rooms']['reference']['extra']={'nested':[1,2]}
 for kind in ['combat-history','battle-loss-start','battle-loss-confirmed','confirmed','battle-victory-before-cleanup']:
  for field in ['act','node','room','nested-room']:
   def mut(rs,kind=kind,field=field):
    r=history(rs) if kind=='combat-history' else next(r for r in reversed(rs) if k(r)=='confirmed') if kind=='confirmed' else one(rs,kind)
    change(s(r),field)
   call(kind+'/'+field,S/'major-elite-1',1,mut)
 for source in ['major-elite-1','major-elite-minimal','major-boss-off']:
  for field in ['act','node','room','nested-room']:
   def linked(rs,field=field):
    i=rs.index(one(rs,'battle-loss-start'))
    if k(rs[i-1])=='combat-history':i-=1
    for r in rs[i:]:
     if isinstance(s(r),dict):change(s(r),field)
   call(source+' linked death suffix '+field,S/source,1,linked)
 for field in ['act','node']:
  for typ in ['missing','null','object']:
   def invalid(rs,field=field,typ=typ):
    st=s(history(rs))
    if typ=='missing':st.pop(field)
    else:st[field]=None if typ=='null' else {'wrong':'type'}
   call('history required '+field+'/'+typ,S/'major-elite-1',1,invalid)
 # Actual travel histories use the old dispatch context and new room/state.
 # Prove that this semantic difference exists in the positive reader witness.
 for source in ['major-elite-1','major-boss-1','major-natural-1','major-elite-minimal','major-boss-off']:
  src=S/source;call('final actual positive '+source,src,0,group='final-positive')
  rs=[json.loads(l) for l in (src/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()];active='';cross=[];combat=[]
  for r in rs:
   if k(r)=='dispatch':active=f(r)['kind']
   if k(r)=='combat-history':
    st=s(r);same=p(r)['context']==str(st['act'])+':'+st['node']+':'+st['rooms']['reference']['$ref']
    if active in ['Travel','MajorFixture'] and not same:cross.append(active)
    if active in ['PlayCard','EndTurn','UsePotion']:combat.append(same)
   if k(r)=='confirmed':active=''
  detailed=rd(src/'process.json')['observation']=='detailed'
  checks.append(dict(name=source+' native crossing and stationary combat context',passed=(bool(cross) and bool(combat) and all(combat)) if detailed else not cross and not combat,crossRoomHistoryCount=len(cross),combatHistoryCount=len(combat)))
 unchanged=all(sha(Path(p))==h for p,h in locks.items());fails=[x for x in rows+checks if not x['passed']]
 (O/'original-lock.json').write_text(json.dumps(locks,indent=2),encoding='utf-8')
 (O/'summary.json').write_text(json.dumps(dict(buildId=rd(B)['buildId'],calls=len(rows),rows=rows,checks=checks,originalsUnchanged=unchanged,failures=fails),ensure_ascii=False,indent=2),encoding='utf-8')
 print('calls',len(rows),'checks',len(checks),'failures',len(fails),'originals unchanged',unchanged,flush=True)
 return int(bool(fails) or not unchanged)
if __name__=='__main__':raise SystemExit(main())
