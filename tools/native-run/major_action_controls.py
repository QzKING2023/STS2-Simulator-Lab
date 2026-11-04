"""Finite Major action/phase gates, exercised through the production reader.

Historical evidence is copied intact and retains its own build identity.
Only explicitly marked control copies are mutated/resealed.
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
 def action(r):
  if k(r)=='root-request':return p(r)['detail']['action']
  if k(r)=='dispatch':return f(r)
  if k(r)=='confirmed':return f(r)['action']
 def call(name,src,expected,mut=None,group='action-phase'):
  d=O/f'control-{len(rows):03d}';d.mkdir();reseal=0
  for file in ['trace.jsonl','trace-seal.json','result.json','process.json']:locks[str(src/file)]=sha(src/file);shutil.copy2(src/file,d/file)
  if mut:
   rs=[json.loads(l) for l in (d/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()];mut(rs)
   (d/'.copy-for-control').write_text('finite action/phase control',encoding='utf-8');(d/'trace.jsonl').write_text('\n'.join(json.dumps(r,ensure_ascii=False) for r in rs)+'\n',encoding='utf-8')
   q=subprocess.run(['pwsh','-NoProfile','-File',str(R/'tools/native-run/reseal-native-control.ps1'),'-Directory',str(d)],capture_output=True,timeout=60);reseal=q.returncode;(d/'reseal.log').write_bytes(q.stdout+q.stderr)
  q=subprocess.run(['dotnet',str(D),'--review',str(d)],capture_output=True,timeout=60);msg=(q.stdout+q.stderr).decode('utf-8','replace');(d/'production-review.log').write_text(msg,encoding='utf-8')
  rows.append(dict(name=name,group=group,source=str(src),directory=d.name,expected=expected,rawExit=q.returncode,resealExit=reseal,passed=reseal==0 and q.returncode==expected and 'Trace chain/order/schema' not in msg,message=msg))
  (O/'progress.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
  if not rows[-1]['passed']:print('FAIL',name,expected,q.returncode,msg[-300:],flush=True)
  if len(rows)%40==0:print('reader calls',len(rows),flush=True)
 # All prior independent calls, including original23 and the six false accepts.
 old=rd(R/'.build/runs/n3d3-context-independent-review01/reader-summary.json')['rows']
 for x in old:call('review '+x['name'],R/x['directory'],x['expected'],group='review-'+x['group'])
 allowed=['EnterAct','EventOption','Travel','MajorFixture','PlayCard','EndTurn','UsePotion','RoomOption','RewardOffer','ActReady'];combat={'PlayCard','EndTurn','UsePotion'}
 traces={n:[json.loads(l) for l in (S/n/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()] for n in ['major-elite-1','major-natural-1']}
 representatives={}
 for name,rs in traces.items():
  for r in rs:
   if k(r)=='dispatch':representatives.setdefault(f(r)['kind'],(name,p(r)['root'],copy.deepcopy(f(r)['decision'])))
 # Each available real phase is challenged with every OTHER registered phase.
 # Combat kinds share a phase: swapping effects among them is a different gate.
 for original,(name,root,_) in representatives.items():
  for label in allowed:
   if label==original or original in combat and label in combat:continue
   decision=representatives[label][2] if label in representatives else {'act':0,'map':1} if label=='ActReady' else {'potion':'unused','target':None}
   def swap(rs,root=root,label=label,decision=decision):
    for r in rs:
     a=action(r)
     if p(r)['root']==root and a is not None:a['kind']=label;a['decision']=copy.deepcopy(decision)
   call('actual '+original+' relabeled '+label,S/name,1,swap)
 root=representatives['PlayCard'][1]
 for label in ['UnknownOperation','0','1','-1','enteract','EnterAct ','PlayCard, EndTurn','']:
  def unknown(rs,label=label):
   for r in rs:
    a=action(r)
    if p(r)['root']==root and a is not None:a['kind']=label
  call('unregistered kind '+repr(label),S/'major-elite-1',1,unknown)
 for kind in ['new-run','event-candidates','opening-finished']:
  for op in ['missing','duplicate']:
   def mutate(rs,kind=kind,op=op):
    r=next(r for r in rs if k(r)==kind);i=rs.index(r)
    if op=='missing':rs.pop(i)
    else:rs.insert(i+1,copy.deepcopy(r))
   call(op+' phase authority '+kind,S/'major-elite-1',1,mutate)
 for name in ['major-elite-1','major-boss-1','major-natural-1','major-elite-minimal','major-boss-off']:
  call('current normal '+name,S/name,0,group='current-positive')
  rs=[json.loads(l) for l in (S/name/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()];active='';cross=0;stationary=0
  for r in rs:
   if k(r)=='dispatch':active=f(r)['kind']
   if k(r)=='combat-history':
    st=s(r);same=p(r)['context']==str(st['act'])+':'+st['node']+':'+st['rooms']['reference']['$ref']
    if active in ['Travel','MajorFixture'] and not same:cross+=1
    if active in combat:stationary+=int(same)
   if k(r)=='confirmed':active=''
  detailed=rd(S/name/'process.json')['observation']=='detailed';checks.append(dict(name=name+' legal crossing and combat anchor',passed=bool(cross and stationary) if detailed else not cross and not stationary,crossings=cross,stationary=stationary))
 for name in ['major-natural-02','major-natural-03','major-action-fault','major-action-budget','major-export-fault']:call('current incomplete '+name,S/name,1,group='current-incomplete')
 unchanged=all(sha(Path(p))==h for p,h in locks.items());fails=[x for x in rows+checks if not x['passed']]
 (O/'original-lock.json').write_text(json.dumps(locks,indent=2),encoding='utf-8');(O/'summary.json').write_text(json.dumps(dict(buildId=rd(B)['buildId'],calls=len(rows),rows=rows,checks=checks,originalsUnchanged=unchanged,failures=fails),ensure_ascii=False,indent=2),encoding='utf-8')
 print('calls',len(rows),'checks',len(checks),'failures',len(fails),'originals unchanged',unchanged,flush=True);return int(bool(fails) or not unchanged)
if __name__=='__main__':raise SystemExit(main())
